using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Índice de potência mundial: uma nota comparável entre países feita de quatro pesos — população
/// controlada, capacidade industrial, exército em campo e avanço tecnológico. O painel Mundo ordenava as
/// potências por número de divisões, o que punha um país arrasado com muitas divisões esfarrapadas à frente
/// de um império industrial intacto; isto compara o que interessa para saber quem manda no mundo.
///
/// Não decide nada e não guarda estado: dá a fotografia do World que lhe entregam. Os pesos e os patamares
/// (tabela power_tier) são dados — mudar o que faz uma superpotência é mexer na base de dados.</summary>
public static class PowerIndex
{
    /// <summary>Lugar de um país na tabela mundial. As parcelas vêm normalizadas (0..1 do total mundial)
    /// para a UI poder desenhá-las lado a lado sem refazer as contas.</summary>
    public readonly record struct Standing(
        int CountryId, float Score, float Share, float Pop, float Industry, float Army, float Tech);

    /// <summary>Tabela mundial ordenada, do mais forte para o mais fraco. Países capitulados ficam de fora:
    /// já não são potência nenhuma.</summary>
    public static List<Standing> Rankings(World w)
    {
        var pop = new Dictionary<int, double>();
        foreach (var r in w.Regions.Values) pop[r.ControllerId] = pop.GetValueOrDefault(r.ControllerId) + r.Population;

        var army = new Dictionary<int, double>();
        foreach (var d in w.Divisions.Values) army[d.CountryId] = army.GetValueOrDefault(d.CountryId) + WarLedger.Strength(d);

        float air = w.Rule("power_air_weight", 4f), nuke = w.Rule("power_nuke_weight", 40f);
        var raw = new List<(Country C, double Pop, double Industry, double Army, double Tech)>();
        foreach (var c in w.Countries.Values)
        {
            if (c.Capitulated) continue;
            double p = pop.GetValueOrDefault(c.Id);
            double a = army.GetValueOrDefault(c.Id) + c.AirPower * air + c.Nukes * nuke;
            raw.Add((c, p, p * c.Stat("industry"), a, c.Techs.Count + c.FocusesDone.Count));
        }

        double tp = raw.Sum(x => x.Pop), ti = raw.Sum(x => x.Industry), ta = raw.Sum(x => x.Army), tt = raw.Sum(x => x.Tech);
        float wp = w.Rule("power_weight_pop", 0.3f), wi = w.Rule("power_weight_industry", 0.3f),
              wa = w.Rule("power_weight_army", 0.3f), wt = w.Rule("power_weight_tech", 0.1f);

        var list = new List<Standing>();
        foreach (var (c, p, i, a, t) in raw)
        {
            float fp = Frac(p, tp), fi = Frac(i, ti), fa = Frac(a, ta), ft = Frac(t, tt);
            float score = (fp * wp + fi * wi + fa * wa + ft * wt) * 100f;
            list.Add(new Standing(c.Id, score, 0f, fp, fi, fa, ft));
        }

        float total = list.Sum(s => s.Score);
        for (int i = 0; i < list.Count; i++) list[i] = list[i] with { Share = total > 0f ? list[i].Score / total : 0f };
        list.Sort((x, y) => y.Score.CompareTo(x.Score));
        return list;
    }

    private static float Frac(double part, double total) => total > 0d ? (float)(part / total) : 0f;

    /// <summary>Patamar do país (tabela power_tier) para a quota de potência que ele tem: o mais alto cujo
    /// mínimo já foi passado. Sem tabela carregada devolve vazio e a UI não mostra patamar nenhum.</summary>
    public static string Tier(World w, float share)
    {
        PowerTier? best = null;
        foreach (var t in w.PowerTiers)
            if (share >= t.MinShare && (best is null || t.MinShare > best.MinShare)) best = t;
        return best?.Name ?? "";
    }
}
