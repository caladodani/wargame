using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Publica a tabela mundial de potências: de power_rank_days em power_rank_days corre o PowerIndex
/// e escreve em cada país a nota, o lugar e o lugar anterior. É o que dá sentido de progresso fora da guerra
/// — subir do 11.º ao 6.º lugar sem disparar um tiro é uma vitória de indústria, e agora vê-se.
///
/// Só o lugar muda de mãos: quem faz a conta é o PowerIndex, e quem a mostra é o painel Mundo.</summary>
public sealed class PowerRankingSystem : ISystem
{
    public string Name => "PowerRanking";

    public void Tick(World w)
    {
        int every = Math.Max(1, (int)w.Rule("power_rank_days", 5f));
        if (w.Clock.Day % every != 0) return;
        Rank(w);
    }

    /// <summary>Recalcula a tabela toda de uma vez (usado pelo Tick e por quem quiser a fotografia já feita,
    /// como o arranque de um jogo novo).</summary>
    public static void Rank(World w)
    {
        var standings = PowerIndex.Rankings(w);
        var seen = new HashSet<int>();
        for (int i = 0; i < standings.Count; i++)
        {
            var s = standings[i];
            if (!w.Countries.TryGetValue(s.CountryId, out var c)) continue;
            seen.Add(c.Id);
            int place = i + 1;
            c.PowerScore = s.Score;
            if (c.PowerRank != place)
            {
                c.PowerRankPrev = c.PowerRank;
                c.PowerRank = place;
                w.Events.Publish(new PowerRankChanged(c.Id, c.PowerRankPrev, place, PowerIndex.Tier(w, s.Share)));
            }
        }
        // capitulados saem da tabela: ficam sem lugar em vez de guardar o último que tiveram
        foreach (var c in w.Countries.Values)
            if (!seen.Contains(c.Id)) { c.PowerScore = 0f; c.PowerRankPrev = c.PowerRank; c.PowerRank = 0; }
    }
}
