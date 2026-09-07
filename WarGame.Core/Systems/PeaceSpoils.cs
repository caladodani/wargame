using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Conferência de paz (HoI4: peace conference). Até aqui, capitular era simples de mais: o país
/// caído entregava tudo o que ainda controlava ao inimigo que mais população dele ocupava, e os outros
/// vencedores — que talvez tenham feito a guerra toda — ficavam a ver. Não havia repartição nenhuma: quem
/// chegasse primeiro à capital levava o país inteiro.
///
/// Agora há mesa. Cada vencedor chega à conferência com pontos de espólio ganhos no que fez naquela guerra:
/// a população que ocupa ao derrotado, as batalhas que ganhou e as regiões que tomou (spoil_points_*). Cada
/// região do derrotado tem um preço — população, edifícios e um extra pela capital (spoil_cost_*) — e a mesa
/// corre por rondas: em cada ronda, quem tem mais pontos escolhe a melhor região que ainda consegue pagar,
/// primeiro as que já ocupa, depois as que fazem fronteira com a terra dele. Quem fica sem pontos sai da
/// mesa. O que ninguém reclamar segue a regra velha, para nunca sobrar terra sem dono.
///
/// Tudo determinístico: nada de sorte, nada de valores no código. Quem chama isto é o PeaceSystem, no
/// momento da capitulação e antes de as guerras acabarem — depois disso já ninguém sabe quem lutou.</summary>
public static class PeaceSpoils
{
    /// <summary>Pontos de espólio de um vencedor sobre um derrotado: o que ocupa dele mais o que lhe fez em
    /// campo. Sem guerra registada entre os dois valem só as terras ocupadas.</summary>
    public static float Points(World w, int winnerId, int loserId)
    {
        float pts = w.Regions.Values.Where(r => r.OwnerId == loserId && r.ControllerId == winnerId)
                        .Sum(r => r.Population) / 1e6f * w.Rule("spoil_points_per_million", 1f);
        if (w.Wars.TryGetValue(World.WarKey(winnerId, loserId), out var war))
            pts += war.Side(winnerId).BattlesWon * w.Rule("spoil_points_per_battle", 3f)
                 + war.Side(winnerId).RegionsTaken * w.Rule("spoil_points_per_region", 2f);
        return MathF.Max(0f, pts);
    }

    /// <summary>Preço de uma região na mesa: gente, obra feita e o peso de ser capital. Nunca abaixo de
    /// spoil_cost_min — nem o descampado mais vazio se leva de graça.</summary>
    public static float Cost(World w, Region r, bool capital) =>
        MathF.Max(w.Rule("spoil_cost_min", 4f),
                  r.Population / 1e6f * w.Rule("spoil_cost_per_million", 0.8f)
                  + r.Buildings.Values.Sum() * w.Rule("spoil_cost_per_building", 6f)
                  + (capital ? w.Rule("spoil_cost_capital", 25f) : 0f));

    /// <summary>Vencedores que se sentam à mesa deste derrotado: quem está em guerra com ele e ainda de pé,
    /// dos que trazem mais pontos para os que trazem menos.</summary>
    public static List<int> Table(World w, Country loser) =>
        loser.AtWarWith.Where(id => w.Countries.TryGetValue(id, out var e) && !e.Capitulated)
            .OrderByDescending(id => Points(w, id, loser.Id)).ThenBy(id => id).ToList();

    /// <summary>Corre a conferência e entrega a terra. Devolve o que cada vencedor levou, por ordem de
    /// entrega, para a crónica e o painel contarem a história. Não acaba guerras nem mexe em exércitos:
    /// disso trata o PeaceSystem.</summary>
    public static List<SpoilClaim> Divide(World w, Country loser)
    {
        var claims = new List<SpoilClaim>();
        var left = Table(w, loser).ToDictionary(id => id, id => Points(w, id, loser.Id));
        if (left.Count == 0) return claims;

        var pool = w.Regions.Values.Where(r => r.OwnerId == loser.Id).OrderBy(r => r.Id).ToList();
        bool any = true;
        while (any && pool.Count > 0)
        {
            any = false;
            foreach (int winner in left.Keys.OrderByDescending(id => left[id]).ThenBy(id => id).ToList())
            {
                var pick = Best(w, winner, loser, pool, left[winner]);
                if (pick is null) continue;
                float price = Cost(w, pick, pick.Id == loser.CapitalRegionId);
                left[winner] -= price;
                pool.Remove(pick);
                pick.OwnerId = winner; pick.ControllerId = winner;
                claims.Add(new SpoilClaim(winner, pick.Id, price));
                any = true;
            }
        }
        return claims;
    }

    /// <summary>A melhor região que este vencedor ainda paga: primeiro as que já ocupa, depois as que tocam
    /// na terra dele, e dentro disso as que mais valem. Null quando já não chega para nada.</summary>
    private static Region? Best(World w, int winnerId, Country loser, List<Region> pool, float points) =>
        pool.Where(r => Cost(w, r, r.Id == loser.CapitalRegionId) <= points)
            .OrderByDescending(r => Reach(w, winnerId, r))
            .ThenByDescending(r => Cost(w, r, r.Id == loser.CapitalRegionId))
            .ThenBy(r => r.Id)
            .FirstOrDefault();

    /// <summary>Quanto é que esta região lhe cai na mão: 2 se já a ocupa, 1 se faz fronteira com terra
    /// dele, 0 se está do outro lado do mundo.</summary>
    private static int Reach(World w, int winnerId, Region r) =>
        r.ControllerId == winnerId ? 2
        : r.Neighbours.Any(n => w.Regions.TryGetValue(n, out var nb) && nb.ControllerId == winnerId) ? 1 : 0;
}

/// <summary>Uma região entregue na conferência: quem a levou, qual é e quantos pontos de espólio pagou.</summary>
public readonly record struct SpoilClaim(int WinnerId, int RegionId, float Cost);
