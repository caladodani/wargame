using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A conta da rendição (Capitulation). O país caía por população ocupada e mais nada — ocupar-lhe
/// serra despovoada valia o mesmo que tomar-lhe a cidade onde está o país todo. O que aqui se prova é que a
/// conta passou a pesar também as praças de pontos de vitória, na dose da regra, que a capital baixa a
/// fasquia, e que sem praças nenhumas a conta volta a ser a de antes.
/// Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2, capital 6).</summary>
public class CapitulationTests
{
    private static World War(int n = 6, int split = 3, int population = 10_000_000)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, n, split, population: population);
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        return w;
    }

    /// <summary>A conta é a mistura das duas medidas, à vírgula: a fatia da gente e a fatia das praças,
    /// pesadas por capitulate_weight_vp. Sem isto seria um rótulo por cima da conta velha.</summary>
    [Fact]
    public void A_conta_e_a_mistura_da_gente_com_as_pracas()
    {
        var w = War();
        var c = w.Countries[2];
        w.Regions[4].ControllerId = 1;
        var (pop, vp, weight) = Capitulation.Parts(w, c);
        Assert.True(weight > 0f, "a regra do peso das praças não veio da tabela");
        Assert.Equal(w.Rule("capitulate_weight_vp", 0f), weight, 4);
        Assert.Equal(1f / 3f, pop, 3);                                     // uma região de três, gente igual
        Assert.Equal((float)VictoryPoints.Of(w, w.Regions[4]) / VictoryPoints.Total(w, 2), vp, 4);
        Assert.Equal((1f - weight) * pop + weight * vp, Capitulation.Progress(w, c), 4);
    }





    /// <summary>Quem já não é dono de nada está caído, e quem não tem guerra nenhuma não cai por muito
    /// ocupado que esteja: são as duas pontas da conta.</summary>
    [Fact]
    public void Sem_terra_cai_e_sem_guerra_nao_cai()
    {
        var w = War();
        var c = w.Countries[2];
        foreach (var r in w.Regions.Values.Where(r => r.OwnerId == 2)) { r.OwnerId = 1; r.ControllerId = 1; }
        Assert.Equal(1f, Capitulation.Progress(w, c));
        Assert.True(Capitulation.Falls(w, c));

        var peace = War();
        var p = peace.Countries[2];
        p.AtWarWith.Clear(); peace.Countries[1].AtWarWith.Clear();
        foreach (var r in peace.Regions.Values.Where(r => r.OwnerId == 2)) r.ControllerId = 1;
        Assert.Equal(0f, Capitulation.Progress(peace, p));                 // ocupação sem guerra não conta
        Assert.False(Capitulation.Falls(peace, p));
    }
}
