using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma linha do armazém: o material de um tipo de unidade, o que há, o que se faz por dia, o que
/// as divisões pedem e quantos dias é que aquilo aguenta.</summary>
public readonly record struct StockRow(int UnitTypeId, string Name, string Category, float Have, float PerDay,
                                       float Missing, float Days);

/// <summary>O armazém de material do país, lido do estado do mundo — não guarda nada seu.
///
/// É a metade que faltava à produção. Até aqui uma fábrica militar só sabia montar divisões novas: uma
/// divisão desfeita voltava a si de graça, e o cofre pagava os homens. No HoI4 a guerra gasta-se em
/// equipamento — as perdas destroem material, o material sai das fábricas para um armazém, e é do armazém
/// que saem os reforços. Um exército grande com o armazém vazio é um exército que não se levanta.
///
/// Aqui um "conjunto" é o material que arma um batalhão de um tipo (Country.Stock: tipo → conjuntos, com
/// fracções). Uma divisão pede tantos conjuntos de cada tipo quantos batalhões o modelo lhe dá; a fatia que
/// ela tem hoje é o Division.Kit. Quem enche o armazém é o ProductionSystem (linhas de material e as
/// fábricas que sobram sem encomenda); quem o esvazia é o EquipmentSystem, ao repor as divisões gastas.</summary>
public static class Warehouse
{
    /// <summary>Conjuntos daquele tipo que faltam ao exército todo deste país: por cada divisão, a fatia que
    /// lhe falta do que o modelo pede. É o que o armazém tem de servir para pôr toda a gente a 100%.</summary>
    public static float Missing(World w, Country c, int unitTypeId)
    {
        float sum = 0f;
        foreach (var d in w.Divisions.Values)
        {
            if (d.HomeId != c.Id || d.Kit >= 1f) continue;
            foreach (var (type, qty) in w.KitNeed(d.TemplateId))
                if (type == unitTypeId) sum += qty * (1f - d.Kit);
        }
        return sum;
    }

    /// <summary>Conjuntos daquele tipo que saem das fábricas por dia: as linhas de material dedicadas mais a
    /// parte do depósito (as fábricas que ficaram sem encomenda), se calhar a este tipo.</summary>
    public static float PerDay(World w, Country c, int unitTypeId)
    {
        float cost = UnitCost(w, unitTypeId);
        if (cost <= 0f) return 0f;
        float made = 0f;
        for (int i = 0; i < c.Queue.Count; i++)
        {
            var o = c.Queue[i];
            if (!o.IsKit || o.UnitTypeId != unitTypeId) continue;
            made += ProductionPlan.DayOutput(w, c, o, ProductionPlan.LinesFor(w, c, i));
        }
        if (Depot(w, c) is (int type, float spend) && type == unitTypeId) made += spend;
        return made / cost;
    }

    /// <summary>O que o depósito faz hoje sozinho: as fábricas militares que a fila não usou não ficam de
    /// braços cruzados — fazem material do tipo que mais falta ao exército. Null = não sobra fábrica
    /// nenhuma, ou não há tipo nenhum a que valha a pena pegar. O valor é o trabalho de um dia (em pontos,
    /// não em conjuntos).</summary>
    public static (int UnitTypeId, float Output)? Depot(World w, Country c)
    {
        if (w.Rule("depot_idle_lines", 1f) < 0.5f) return null;
        int free = Industry.Of(w, c.Id).Military - Industry.LinesBusy(w, c);
        if (free <= 0) return null;
        if (Neediest(w, c) is not int type) return null;
        // o depósito serve para tapar buracos, não para encher despensa sem fim: só pega no trabalho se
        // faltar mesmo material àquele tipo. Um país em paz, com o exército armado, não gasta cofre nenhum
        // com fábricas paradas — é a mesma leitura do HoI4, onde stockpile a mais é dinheiro parado
        if (Missing(w, c, type) <= c.Stocked(type)) return null;
        float cost = UnitCost(w, type);
        if (cost <= 0f) return null;
        float share = w.Rule("depot_idle_share", 0.6f);           // uma fábrica de depósito não rende como uma linha dedicada
        return (type, cost / MathF.Max(1f, w.Rule("build_min_days", 10f)) * c.Stat("production_speed") * free * share);
    }

    /// <summary>O tipo de material que mais falta hoje: o que mais falta às divisões e, se não faltar nada a
    /// ninguém, o que o exército mais usa (para o depósito ir enchendo a despensa antes da guerra). Null num
    /// país sem exército nenhum.</summary>
    public static int? Neediest(World w, Country c)
    {
        int? best = null; float bestScore = 0f;
        foreach (int type in Types(w, c))
        {
            float score = Missing(w, c, type) - c.Stocked(type);
            if (best is null || score > bestScore) { best = type; bestScore = score; }
        }
        if (best is not null && bestScore > 0f) return best;
        // ninguém a pedir: enche-se o que mais se usa
        int? most = null; float mostQty = -1f;
        foreach (int type in Types(w, c))
        {
            float qty = Fleet(w, c, type) - c.Stocked(type);
            if (qty > mostQty) { most = type; mostQty = qty; }
        }
        return most;
    }

    /// <summary>Conjuntos daquele tipo que o exército inteiro leva quando está a 100% — o tamanho da
    /// despensa que faz sentido ter.</summary>
    public static float Fleet(World w, Country c, int unitTypeId)
    {
        float sum = 0f;
        foreach (var d in w.Divisions.Values)
        {
            if (d.HomeId != c.Id) continue;
            foreach (var (type, qty) in w.KitNeed(d.TemplateId))
                if (type == unitTypeId) sum += qty;
        }
        return sum;
    }

    /// <summary>Os tipos de material que a este país interessam: os que os modelos das suas divisões usam,
    /// mais os que já tem em armazém ou em linha. Um país que nunca viu um blindado não tem prateleira
    /// nenhuma para blindados.</summary>
    public static List<int> Types(World w, Country c)
    {
        var set = new HashSet<int>();
        foreach (var d in w.Divisions.Values)
            if (d.HomeId == c.Id)
                foreach (var (type, _) in w.KitNeed(d.TemplateId)) set.Add(type);
        foreach (var o in c.Queue)
            if (o.IsKit) set.Add(o.UnitTypeId);
            else foreach (var (type, _) in w.KitNeed(o.TemplateId)) set.Add(type);
        foreach (var (type, qty) in c.Stock) if (qty > 0f) set.Add(type);
        var list = set.ToList(); list.Sort();
        return list;
    }

    /// <summary>A folha do armazém, prateleira a prateleira, do que mais falta para o que menos falta.</summary>
    public static List<StockRow> Rows(World w, Country c)
    {
        var rows = new List<StockRow>();
        foreach (int type in Types(w, c))
        {
            float have = c.Stocked(type), day = PerDay(w, c, type), missing = Missing(w, c, type);
            string name = type.ToString(), cat = "";
            try { var t = w.Units.GetUnitType(type); name = t.Name; cat = t.Category; } catch { }
            rows.Add(new StockRow(type, name, cat, have, day, missing, Days(w, c, type)));
        }
        rows.Sort((a, b) => (b.Missing - b.Have).CompareTo(a.Missing - a.Have));
        return rows;
    }

    /// <summary>Dias que o armazém deste tipo aguenta ao ritmo a que hoje se gasta e se faz. Infinito
    /// (float.PositiveInfinity) quando se faz mais do que se gasta — é o que se quer ver antes de atacar.</summary>
    public static float Days(World w, Country c, int unitTypeId)
    {
        float need = DailyDraw(w, c, unitTypeId), made = PerDay(w, c, unitTypeId);
        float net = need - made;
        if (net <= 1e-4f) return float.PositiveInfinity;
        return c.Stocked(unitTypeId) / net;
    }

    /// <summary>O que as divisões vão pedir hoje deste tipo: a reposição de um dia de cada divisão a que
    /// falte material (o tecto de kit_refill_day), sem contar com o que o armazém tem.</summary>
    public static float DailyDraw(World w, Country c, int unitTypeId)
    {
        float step = w.Rule("kit_refill_day", 0.06f);
        float sum = 0f;
        foreach (var d in w.Divisions.Values)
        {
            if (d.HomeId != c.Id || d.Kit >= 1f) continue;
            float want = MathF.Min(step, 1f - d.Kit);
            foreach (var (type, qty) in w.KitNeed(d.TemplateId))
                if (type == unitTypeId) sum += qty * want;
        }
        return sum;
    }

    /// <summary>A divisão pior armada deste país, ou null se estão todas a 100%.</summary>
    public static Division? Worst(World w, Country c)
    {
        Division? worst = null;
        foreach (var d in w.Divisions.Values)
            if (d.HomeId == c.Id && d.Kit < 1f && (worst is null || d.Kit < worst.Kit)) worst = d;
        return worst;
    }

    /// <summary>Material médio do exército (0..1); 1 num país sem divisões — não há nada por armar.</summary>
    public static float Average(World w, Country c)
    {
        float sum = 0f; int n = 0;
        foreach (var d in w.Divisions.Values) if (d.HomeId == c.Id) { sum += d.Kit; n++; }
        return n == 0 ? 1f : sum / n;
    }

    public static float UnitCost(World w, int unitTypeId)
    {
        try { return w.Units.GetUnitType(unitTypeId).Cost; } catch { return 0f; }
    }
}
