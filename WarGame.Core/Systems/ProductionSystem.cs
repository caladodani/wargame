using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Gasta Country.Money nas encomendas (Country.Queue, pela ordem) e cria divisões na capital.
/// HoI4: linhas de produção com output diário limitado; aqui cada encomenda avança no máximo custo/build_min_days
/// por dia (× country_stat production_speed). Quantas avançam no mesmo dia é o número de fábricas militares
/// (Industry): a fila pode ser longa, mas só as primeiras encomendas têm linha de montagem — o resto espera.
/// Regras: build_min_days, new_division_org, factory_mil_*.</summary>
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

    /// <summary>Gasto do dia: min(custo/minDays, o que falta, Money) por encomenda, e só nas primeiras
    /// `lines` encomendas por acabar — as outras não têm fábrica militar livre. Uma encomenda já pronta à
    /// espera de recrutas não ocupa linha: a fábrica largou-a. Money nunca fica negativo.</summary>
    private static void Spend(World w, Country c, float minDays, int lines)
    {
        int used = 0;
        foreach (var o in c.Queue)
        {
            if (c.Money <= 0f || used >= lines) break;
            float cost = w.TemplateCost(o.TemplateId);
            if (o.Progress >= cost - 1e-3f) continue;      // pronta: espera homens, não linha
            used++;
            float spend = MathF.Min(MathF.Min(cost / minDays * c.Stat("production_speed"), cost - o.Progress), c.Money);
            if (spend <= 0f) continue;
            o.Progress += spend; c.Money -= spend;
        }
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
            // produção em série: a encomenda entregue volta ao fim da fila, do zero
            if (o.Repeat && c.Queue.Count < w.Rule("production_queue_max", 30f))
                c.Queue.Add(new ProductionOrder { TemplateId = o.TemplateId, Repeat = true });
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
