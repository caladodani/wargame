using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Grava amostras periódicas (dinheiro, divisões, regiões e nota de potência) do jogador e das
/// maiores potências para os gráficos de evolução. De history_sample_days em history_sample_days, no máximo
/// history_tracked países por amostra; persiste no save (s_history).
///
/// A nota é a do PowerIndex no próprio dia da amostra, e não a que estiver guardada no país: o
/// PowerRankingSystem corre noutro compasso (power_rank_days) e a história ficaria com notas de dias que
/// não são o seu. Contar divisões diz quem tem mais tropa; a nota diz quem está a ganhar a campanha.</summary>
public sealed class HistorySystem : ISystem
{
    public string Name => "History";

    public void Tick(World w)
    {
        int every = Math.Max(1, (int)w.Rule("history_sample_days", 7f));
        if (w.Clock.Day % every != 0) return;
        int tracked = Math.Max(1, (int)w.Rule("history_tracked", 8f));

        var divs = new Dictionary<int, int>();
        foreach (var d in w.Divisions.Values) divs[d.CountryId] = divs.GetValueOrDefault(d.CountryId) + 1;
        var regions = new Dictionary<int, int>();
        foreach (var r in w.Regions.Values) regions[r.ControllerId] = regions.GetValueOrDefault(r.ControllerId) + 1;

        var power = new Dictionary<int, float>();
        foreach (var st in PowerIndex.Rankings(w)) power[st.CountryId] = st.Score;

        var picked = w.Countries.Values.Where(c => !c.Capitulated)
            .OrderByDescending(c => c.IsPlayer).ThenByDescending(c => divs.GetValueOrDefault(c.Id))
            .Take(tracked);
        foreach (var c in picked)
            w.History.Add(new HistorySample(w.Clock.Day, c.Id, c.Money, divs.GetValueOrDefault(c.Id),
                                            regions.GetValueOrDefault(c.Id), power.GetValueOrDefault(c.Id)));
    }
}
