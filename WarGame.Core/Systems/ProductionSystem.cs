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
        float minDays = MathF.Max(1f, w.Rule("build_min_days", 10f));
        float newOrg = w.Rule("new_division_org", 40f);
        foreach (var c in w.Countries.Values)
        {
            if (c.Queue.Count == 0) continue;
            Spend(w, c, minDays, Industry.Of(w, c.Id).Military);
            Deliver(w, c, newOrg);
        }
    }

    /// <summary>Gasto do dia: as fábricas militares são repartidas pela fila, de cima para baixo, dando a
    /// cada encomenda as que ela pediu (ProductionOrder.Factories, uma por omissão) enquanto houver. Cada
    /// fábrica vale um dia de trabalho: uma encomenda com três anda três vezes mais depressa e deixa duas a
    /// menos para quem vem atrás. Quem fica sem fábrica nenhuma não anda. Uma encomenda já pronta à espera
    /// de recrutas não ocupa linha: a fábrica largou-a. Money nunca fica negativo.</summary>
    private static void Spend(World w, Country c, float minDays, int factories)
    {
        int free = factories;
        var worked = new HashSet<ProductionOrder>();
        foreach (var o in c.Queue)
        {
            if (c.Money <= 0f || free <= 0) break;
            float cost = w.TemplateCost(o.TemplateId);
            if (o.Progress >= cost - 1e-3f) continue;      // pronta: espera homens, não linha
            int mine = Math.Clamp(o.Factories, 1, free);
            free -= mine;
            float spend = MathF.Min(MathF.Min(cost / minDays * c.Stat("production_speed") * mine * o.Efficiency, cost - o.Progress), c.Money);
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
            float cost = w.TemplateCost(o.TemplateId);
            if (o.Progress < cost - 1e-3f) { i++; continue; }
            float men = cost * perCost;
            if (c.Manpower < men) { i++; continue; }   // pool ainda por encher — a encomenda espera
            spawn ??= SpawnRegion(w, c);
            if (spawn < 0) return;
            c.Manpower -= men;
            w.AddDivision(new Division
            {
                Id = w.NewDivisionId(), CountryId = c.Id, TemplateId = o.TemplateId, RegionId = spawn.Value,
                Org = newOrg, Hp = 100f, Supply = 1f,
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
