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

    /// <summary>A capital pesa: com as mesmas regiões tomadas em número, quem levou a capital está mais
    /// perto de o derrubar — e a fasquia dele desce ainda por cima. É a diferença entre avançar por onde não
    /// há ninguém e avançar por onde interessa.</summary>
    [Fact]
    public void Tomar_a_capital_aproxima_mais_a_queda_e_baixa_a_fasquia()
    {
        var w = War();
        var c = w.Countries[2];
        w.Regions[4].ControllerId = 1;                                     // uma região qualquer dele
        float plain = Capitulation.Progress(w, c);
        Assert.False(Capitulation.CapitalLost(w, c));
        Assert.Equal(w.Rule("capitulate_share", 0.75f), Capitulation.Limit(w, c), 4);

        w.Regions[4].ControllerId = 2; w.Regions[6].ControllerId = 1;      // a mesma região em número: a capital
        Assert.True(Capitulation.Progress(w, c) > plain, "a capital não pesou mais do que uma região qualquer");
        Assert.True(Capitulation.CapitalLost(w, c));
        Assert.Equal(w.Rule("capitulate_share_capital", 0.5f), Capitulation.Limit(w, c), 4);
    }

    /// <summary>Três regiões de quatro sem a capital já não derrubam ninguém: era a conta velha, de mancha
    /// no mapa. Com a capital, cai. É o próprio HoI4 — a rendição lê-se nas praças.</summary>
    [Fact]
    public void Mancha_no_mapa_sem_capital_ja_nao_derruba()
    {
        var w = War(7, 3);                                                 // país 2: 4-5-6-7, capital 7
        var c = w.Countries[2];
        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].ControllerId = 1;
        Assert.False(Capitulation.Falls(w, c));
        new PeaceSystem().Tick(w);
        Assert.False(c.Capitulated);

        w.Regions[7].ControllerId = 1;
        Assert.True(Capitulation.Falls(w, c));
        new PeaceSystem().Tick(w);
        Assert.True(c.Capitulated);
    }

    /// <summary>O caminho mais curto para o derrubar: as praças que faltam tomar-lhe, das maiores para
    /// baixo — e tomadas essas, ele cai mesmo. A lista não é um palpite: é a mesma conta.</summary>
    [Fact]
    public void A_lista_do_que_falta_tomar_derruba_mesmo()
    {
        var w = War(7, 3);
        var c = w.Countries[2];
        w.Regions[4].ControllerId = 1;
        var need = Capitulation.Needed(w, c);
        Assert.NotEmpty(need);
        Assert.DoesNotContain(need, r => r.Id == 4);                       // o que já é nosso não se pede outra vez
        Assert.All(need, r => Assert.Equal(2, r.OwnerId));
        Assert.Equal(7, need[0].Id);                                       // a capital dele vale mais: vai à frente
        Assert.False(Capitulation.Falls(w, c));
        foreach (var r in need) r.ControllerId = 1;
        Assert.True(Capitulation.Falls(w, c));
        // e nem uma região a mais do que é preciso
        Assert.True(need.Count <= w.Regions.Values.Count(r => r.OwnerId == 2));
    }

    /// <summary>Sem pontos de vitória nenhuns — tabela apagada ou terra sem gente que chegue — a conta volta
    /// a ser a de população, tal e qual era antes de haver praças. Nada rebenta por falta de tabela.</summary>
    [Fact]
    public void Sem_pracas_a_conta_volta_a_ser_a_de_populacao()
    {
        var w = War(7, 3);
        var c = w.Countries[2];
        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].ControllerId = 1;
        w.VictoryTiers.Clear();
        var (pop, vp, weight) = Capitulation.Parts(w, c);
        Assert.Equal(0f, vp);
        Assert.Equal(0f, weight);                                          // sem praças, o peso cai sozinho
        Assert.Equal(pop, Capitulation.Progress(w, c), 4);
        Assert.Equal(0.75f, pop, 3);
        Assert.True(Capitulation.Falls(w, c));                             // a conta velha, à vírgula
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
