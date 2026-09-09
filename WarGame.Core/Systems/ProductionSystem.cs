using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Gasta Country.Money nas encomendas (Country.Queue, pela ordem) e cria divisões na capital.
/// HoI4: linhas de produção com output diário limitado; aqui cada encomenda avança no máximo custo/build_min_days
/// por dia (× country_stat production_speed). Quantas avançam no mesmo dia é o número de fábricas militares
/// (Industry): a fila pode ser longa, mas só as primeiras encomendas têm linha de montagem — o resto espera.
///
/// E uma linha de montagem aprende. A primeira unidade de um modelo é sempre um protótipo: sai ao ritmo de
/// origem. Da segunda em diante a linha já sabe o que está a fazer e ganha ritmo todos os dias em que produz
/// (line_efficiency_gain), até ao tecto (line_efficiency_max) — é por isso que em HoI4 se deixa a mesma
/// encomenda em série em vez de andar a trocar de modelo. Uma linha que fica parada, sem cofre ou sem
/// fábrica, arrefece (line_efficiency_decay) e nunca desce abaixo do ritmo de origem; trocar de modelo é
/// abrir linha nova, e essa começa outra vez do princípio.
///
/// Regras: build_min_days, new_division_org, factory_mil_*, line_efficiency_*.</summary>
public sealed class ProductionSystem : ISystem
{
    public string Name => "Production";

    public void Tick(World w)
    {
        float newOrg = w.Rule("new_division_org", 40f);
        foreach (var c in w.Countries.Values)
        {
            int mil = Industry.Of(w, c.Id).Military;
            if (c.Queue.Count > 0)
            {
                Retool(w, c);
                Spend(w, c, mil);
                Deliver(w, c, newOrg);
            }
            Depot(w, c);
        }
    }

    /// <summary>Gasto do dia: as fábricas militares são repartidas pela fila, de cima para baixo, dando a
    /// cada encomenda as que ela pediu (ProductionOrder.Factories, uma por omissão) enquanto houver. Cada
    /// fábrica vale um dia de trabalho: uma encomenda com três anda três vezes mais depressa e deixa duas a
    /// menos para quem vem atrás. Quem fica sem fábrica nenhuma não anda. Uma encomenda já pronta à espera
    /// de recrutas não ocupa linha: a fábrica largou-a. Money nunca fica negativo.</summary>
    private static void Spend(World w, Country c, int factories)
    {
        int free = factories;
        var worked = new HashSet<ProductionOrder>();
        foreach (var o in c.Queue)
        {
            if (c.Money <= 0f || free <= 0) break;
            float cost = w.OrderCost(o, c);
            if (o.Progress >= cost - 1e-3f) continue;      // pronta: espera homens, não linha
            int mine = Math.Clamp(o.Factories, 1, free);
            free -= mine;
            // a conta do dia é a do ProductionPlan — a mesma que o painel mostra, para não haver duas
            float spend = MathF.Min(MathF.Min(ProductionPlan.DayOutput(w, c, o, mine), cost - o.Progress), c.Money);
            if (spend <= 0f) continue;
            o.Progress += spend; c.Money -= spend;
            worked.Add(o);
        }
        // o ritmo do dia seguinte: quem trabalhou hoje ganha jeito, quem ficou parado arrefece
        foreach (var o in c.Queue) Age(w, o, worked.Contains(o));
    }

    /// <summary>Ritmo da linha ao fim do dia. Só a série ganha: a primeira unidade de um modelo é o
    /// protótipo, e enquanto ela não sair a linha anda ao ritmo de origem. O arrefecimento nunca leva a
    /// linha abaixo de 1 — desaprender ao ponto de ser pior do que uma fábrica nova não faria sentido.</summary>
    public static void Age(World w, ProductionOrder o, bool worked)
    {
        if (o.Delivered <= 0) return;                       // protótipo: ainda não há jeito nenhum para ganhar
        float max = MathF.Max(1f, w.Rule("line_efficiency_max", 1.5f));
        float step = worked ? w.Rule("line_efficiency_gain", 0.03f) : -w.Rule("line_efficiency_decay", 0.02f);
        o.Efficiency = Math.Clamp(o.Efficiency + step, 1f, max);
    }

    /// <summary>Encomendas prontas (Progress ≥ custo − 1e-3) saem da fila e nascem em SpawnRegion.
    /// Sem região controlada, ou sem homens (rule manpower_per_cost × custo), ficam à espera.</summary>
    private static void Deliver(World w, Country c, float newOrg)
    {
        float perCost = w.Rule("manpower_per_cost", 500f);
        int? spawn = null;
        for (int i = 0; i < c.Queue.Count;)
        {
            var o = c.Queue[i];
            float cost = w.OrderCost(o, c);
            if (o.Progress < cost - 1e-3f) { i++; continue; }
            // linha de material: não sai divisão nenhuma da fábrica, saem conjuntos para o armazém, e a
            // linha continua onde está — é uma torneira, não uma encomenda
            if (o.IsKit)
            {
                // a mesma folga com que a porta acima abriu: o gasto do dia é limitado a `cost - Progress` e
                // em vírgula flutuante isso pousa um cabelo abaixo do custo (1,5999998 para 1,6). Sem a folga
                // aqui, o chão dava zero conjuntos e a linha ficava presa para sempre à porta do armazém —
                // e só se via quando um custo deixou de ser redondo, que é o que as marcas trouxeram.
                int made = cost <= 0f ? 0 : (int)MathF.Floor(o.Progress / cost + 1e-3f);
                if (made > 0)
                {
                    o.Progress = MathF.Max(0f, o.Progress - made * cost);
                    o.Delivered += made;
                    // a prateleira é uma pilha misturada: o que chega puxa a marca média para cima (Marks)
                    c.StockMark[o.UnitTypeId] = Marks.Blend(c.Stocked(o.UnitTypeId), c.StockedMark(o.UnitTypeId),
                                                            made, o.Mark);
                    c.Stock[o.UnitTypeId] = c.Stocked(o.UnitTypeId) + made;
                }
                i++; continue;
            }
            float men = cost * perCost;
            if (c.Manpower < men) { i++; continue; }   // pool ainda por encher — a encomenda espera
            spawn ??= SpawnRegion(w, c);
            if (spawn < 0) return;
            c.Manpower -= men;
            w.AddDivision(new Division
            {
                Id = w.NewDivisionId(), CountryId = c.Id, TemplateId = o.TemplateId, RegionId = spawn.Value,
                Org = newOrg, Hp = 100f, Supply = 1f,
                // sai da fábrica com o melhor material que a indústria do país sabe fazer hoje (Marks)
                Mark = Marks.Fresh(w, c, o.TemplateId),
            });
            c.Queue.RemoveAt(i);
            // produção em série: a encomenda entregue volta ao fim da fila, do zero — mas a linha é a mesma
            // e leva consigo o que já aprendeu, que é a razão de se deixar uma série a andar
            if (o.Repeat && c.Queue.Count < w.Rule("production_queue_max", 30f))
                c.Queue.Add(new ProductionOrder
                {
                    TemplateId = o.TemplateId, Repeat = true, Factories = o.Factories,
                    Efficiency = o.Efficiency, Delivered = o.Delivered + 1,
                });
        }
    }

    /// <summary>Reafinar as linhas de material: quando a investigação abre uma marca melhor, a linha passa a
    /// fazê-la — e paga por isso em ritmo (mark_switch_efficiency), como uma fábrica que tem de trocar de
    /// ferramenta. Uma linha acabada de abrir nasce já na melhor marca do país e não paga nada.</summary>
    private static void Retool(World w, Country c)
    {
        foreach (var o in c.Queue)
        {
            if (!o.IsKit) continue;
            var (mark, eff) = Marks.Retool(w, c, o.UnitTypeId, o.Mark, o.Efficiency);
            o.Mark = mark; o.Efficiency = eff;
        }
    }

    /// <summary>O depósito: as fábricas militares que a fila não usou não ficam paradas — fazem material do
    /// tipo que mais falta ao exército e metem-no no armazém (Warehouse.Depot). Rendem menos do que uma linha
    /// dedicada (depot_idle_share), mas é isto que faz um país sem encomendas nenhumas continuar a ter com
    /// que repor a tropa gasta — e é o que mantém a IA de pé sem lhe ensinar a gerir armazéns.</summary>
    private static void Depot(World w, Country c)
    {
        if (c.Money <= 0f) return;
        if (Warehouse.Depot(w, c) is not (int type, float output)) return;
        float cost = Warehouse.UnitCost(w, type);
        if (cost <= 0f || output <= 0f) return;
        float spend = MathF.Min(output, c.Money);
        c.Money -= spend;
        float made = spend / cost;
        c.StockMark[type] = Marks.Blend(c.Stocked(type), c.StockedMark(type), made, Marks.Open(w, c, type));
        c.Stock[type] = c.Stocked(type) + made;
    }

    /// <summary>Onde nasce uma divisão nova: a capital se o país a controla, senão a região controlada com mais
    /// população; −1 se não controla nenhuma.</summary>
    public static int SpawnRegion(World w, Country c)
    {
        if (w.Regions.TryGetValue(c.CapitalRegionId, out var cap) && cap.ControllerId == c.Id) return cap.Id;
        Region? best = null;
        foreach (var r in w.Regions.Values)
            if (r.ControllerId == c.Id && (best is null || r.Population > best.Population)) best = r;
        return best?.Id ?? -1;
    }
}
