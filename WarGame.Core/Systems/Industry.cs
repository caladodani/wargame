using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>As três filas de fábricas de um país e quantas estão ocupadas hoje.</summary>
/// <param name="Civil">Fábricas civis: obras (infraestrutura, edifícios, fortificações) em paralelo.</param>
/// <param name="Military">Fábricas militares: linhas de montagem da fila de produção a andar por dia.</param>
/// <param name="Naval">Estaleiros: os cais que levam abastecimento do outro lado do mar.</param>
public readonly record struct Yards(int Civil, int CivilBusy, int Military, int MilitaryBusy, int Naval, int NavalBusy)
{
    public int FreeCivil => Math.Max(0, Civil - CivilBusy);
    public int FreeMilitary => Math.Max(0, Military - MilitaryBusy);
}

/// <summary>Capacidade industrial à maneira do HoI4: em vez de o dinheiro ser o único travão, o país tem um
/// número de fábricas e cada obra ou linha de montagem ocupa uma. Antes disto o cofre cheio deixava começar
/// obras em todas as regiões ao mesmo tempo e a fila de produção inteira avançava no mesmo dia — a economia
/// não tinha forma, só saldo.
///
/// Três filas, com contas todas em regras da base de dados: civis (obras), militares (linhas de produção) e
/// estaleiros (o mar). Cada uma nasce de uma base, cresce com o tamanho do país e sobe com os edifícios que
/// a tabela building marca na coluna `yard` — a Fábrica abre uma fila civil, o Arsenal uma militar e o Porto
/// um estaleiro. Nenhum destes números vive no C#.
///
/// Estado derivado: não guarda nada, não entra no save e não é ISystem — quem manda são o ProductionSystem
/// (linhas) e os comandos de obra (fábricas civis).</summary>
public static class Industry
{
    /// <summary>Quantas fábricas este país tem e quantas estão ocupadas neste momento.</summary>
    public static Yards Of(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return default;

        int regions = 0, sites = 0;
        var levels = new Dictionary<string, int>();
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId != countryId) continue;
            regions++;
            foreach (var (bid, lvl) in r.Buildings)
                if (lvl > 0 && w.BuildingDefs.TryGetValue(bid, out var def) && def.Yard.Length > 0)
                    levels[def.Yard] = levels.GetValueOrDefault(def.Yard) + lvl;
            // terra ocupada não constrói (o ConstructionSystem cancela-lhe as obras), por isso não gasta fábrica
            if (r.OwnerId == countryId && Working(r)) sites++;
        }

        float per = w.Rule("factory_per_building", 1f);
        int civil = Count(w.Rule("factory_civil_base", 2f), regions * w.Rule("factory_civil_per_region", 0.25f),
                          levels.GetValueOrDefault("civil") * per);
        int mil = Count(w.Rule("factory_mil_base", 2f), regions * w.Rule("factory_mil_per_region", 0.15f),
                        levels.GetValueOrDefault("militar") * per);
        int naval = Count(0f, 0f, levels.GetValueOrDefault("naval") * per);

        int lines = Math.Min(mil, LinesBusy(w, c));
        float perYard = MathF.Max(1f, w.Rule("yard_divisions", 3f));
        int yardsAtSea = Math.Min(naval, (int)MathF.Ceiling(c.SeaSupplied / perYard));
        return new Yards(civil, Math.Min(civil, sites), mil, lines, naval, yardsAtSea);
    }

    /// <summary>Esta região tem obra em curso? Qualquer uma das quatro (estrada, forte, edifício ou carril)
    /// ocupa uma fábrica civil.</summary>
    public static bool Working(Region r) => r.Building || r.FortBuilding || r.RailBuilding || r.Project is not null;

    /// <summary>Encomendas que ainda gastam pontos. As que já estão prontas e esperam recrutas não ocupam
    /// linha nenhuma — a fábrica já as largou.</summary>
    public static int Unfinished(World w, Country c)
    {
        int n = 0;
        foreach (var o in c.Queue) if (o.Progress < w.OrderCost(o, c) - 1e-3f) n++;
        return n;
    }

    /// <summary>Fábricas militares que a fila pede hoje: a soma das dedicadas a cada encomenda por acabar
    /// (ProductionOrder.Factories). Uma encomenda com três fábricas acende três lâmpadas na bancada.</summary>
    public static int LinesBusy(World w, Country c)
    {
        int n = 0;
        foreach (var o in c.Queue)
            if (o.Progress < w.OrderCost(o, c) - 1e-3f) n += Math.Max(1, o.Factories);
        return n;
    }

    private static int Count(float bas, float bySize, float byBuildings) =>
        Math.Max(0, (int)MathF.Floor(bas + bySize + byBuildings));
}
