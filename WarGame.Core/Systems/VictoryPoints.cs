using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Pontos de vitória: a conta de quem está a ganhar.
///
/// Aqui toda a terra pesava o mesmo — uma província de serra valia numa mesa de paz o que valia a cidade
/// onde estava metade da indústria do país, porque a pressão se media em número de regiões. No HoI4 o mapa
/// tem números por cima das praças que interessam, e é por eles que se lê a guerra de relance: quem tem o
/// mapa cheio de aldeias e nenhuma capital não está a ganhar nada.
///
/// Nada disto se guarda. O grau de uma região lê-se da região (população, e a capital do país é sempre o
/// grau mais alto), por isso não há um número guardado que possa contradizer o mapa depois de uma capital
/// mudar de sítio. A tabela victory_tier é que manda: os graus, o que valem e a população a que entram.</summary>
public static class VictoryPoints
{
    /// <summary>O que esta região vale. 0 quando não chega a grau nenhum — ou quando a tabela não veio.</summary>
    public static int Of(World w, Region r)
    {
        if (w.VictoryTiers.Count == 0) return 0;
        bool capital = w.Countries.TryGetValue(r.OwnerId, out var c) && c.CapitalRegionId == r.Id;
        int best = 0;
        foreach (var t in w.VictoryTiers.Values)
        {
            if (t.Capital ? !capital : r.Population < t.MinPop) continue;
            if (t.Points > best) best = t.Points;
        }
        return best;
    }

    /// <summary>O grau desta região, para lhe pôr o nome e a chapa. Null quando não vale nada.</summary>
    public static VictoryTierDef? Tier(World w, Region r)
    {
        if (w.VictoryTiers.Count == 0) return null;
        bool capital = w.Countries.TryGetValue(r.OwnerId, out var c) && c.CapitalRegionId == r.Id;
        VictoryTierDef? best = null;
        foreach (var t in w.VictoryTiers.Values)
        {
            if (t.Capital ? !capital : r.Population < t.MinPop) continue;
            if (best is null || t.Points > best.Points) best = t;
        }
        return best;
    }

    /// <summary>Tudo o que a terra de um país vale, esteja ela em mãos de quem estiver. É o denominador:
    /// quanto há para perder.</summary>
    public static int Total(World w, int ownerId)
    {
        int sum = 0;
        foreach (var r in w.Regions.Values) if (r.OwnerId == ownerId) sum += Of(w, r);
        return sum;
    }

    /// <summary>O que um país tem hoje na mão: a terra dele que ainda controla mais a terra alheia que
    /// ocupa. É esta a conta que se vê a subir enquanto se avança.</summary>
    public static int Held(World w, int holderId)
    {
        int sum = 0;
        foreach (var r in w.Regions.Values) if (r.ControllerId == holderId) sum += Of(w, r);
        return sum;
    }

    /// <summary>Quanto do país de outro é que já lhe tirámos, em pontos e não em número de regiões: a fatia
    /// dos pontos da terra dele que estão debaixo da nossa bota. 0 quando ele não tem país nenhum.</summary>
    public static float Taken(World w, int holderId, int ownerId)
    {
        int total = 0, mine = 0;
        foreach (var r in w.Regions.Values)
        {
            if (r.OwnerId != ownerId) continue;
            int v = Of(w, r);
            total += v;
            if (r.ControllerId == holderId) mine += v;
        }
        return total == 0 ? 0f : (float)mine / total;
    }

    /// <summary>As regiões que mais valem no país de alguém, das melhores para baixo — a lista de alvos.</summary>
    public static List<Region> Prizes(World w, int ownerId, int take = 5) =>
        w.Regions.Values.Where(r => r.OwnerId == ownerId && Of(w, r) > 0)
            .OrderByDescending(r => Of(w, r)).ThenByDescending(r => r.Population).ThenBy(r => r.Id)
            .Take(take).ToList();

    /// <summary>A linha da ficha da região: o que ela vale e porquê.</summary>
    public static string Line(World w, Region r)
    {
        if (Tier(w, r) is not VictoryTierDef t) return "";
        string hand = w.Countries.TryGetValue(r.ControllerId, out var h) && r.ControllerId != r.OwnerId
                    ? $" — na mão de {h.Name}" : "";
        return $"{t.Icon} {t.Name}: {t.Points} ponto{(t.Points == 1 ? "" : "s")} de vitória{hand}";
    }
}
