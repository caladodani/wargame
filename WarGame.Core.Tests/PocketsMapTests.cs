using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O caldeirão (Pockets): as divisões cortadas juntas numa bolsa com terra, gente, dono do anel e
/// prazo. É o que o mapa desenha — e por isso tem de dizer exactamente o mesmo que o PocketSystem, que é
/// quem rende a tropa. Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2), como nos testes do cerco.</summary>
public class PocketsMapTests
{
    private static World Build(int n = 6, int split = 3)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, n, split);
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        w.Register(new SupplySystem());
        return w;
    }

    [Fact]
    public void SemTropaCortadaNaoHaCaldeiraoNenhum()
    {
        var w = Build();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 1);
        Assert.Empty(Pockets.All(w));
    }

    [Fact]
    public void ABolsaJuntaATerraQueOPaisAindaSeguraLaDentro()
    {
        var w = Build(8, 3);
        // tomámos a 6 e a 7 e ficámos lá: a 4 e a 5 são dele, a 8 é dele — a bolsa é a 6+7
        w.Regions[6].ControllerId = 1; w.Regions[7].ControllerId = 1;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 6);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 7);
        TestWorld.Days(w, 1);

        var pot = Assert.Single(Pockets.All(w));
        Assert.Equal(1, pot.CountryId);
        Assert.Equal(2, pot.Divisions);
        Assert.Equal(new[] { 6, 7 }, pot.RegionIds.OrderBy(x => x));
        Assert.Equal(2, pot.RingCountryId);
        Assert.True(pot.Sealed);
    }

    [Fact]
    public void DuasBolsasSeparadasNaoSeJuntamNumaSo()
    {
        var w = Build(9, 2);
        foreach (int id in new[] { 4, 8 }) w.Regions[id].ControllerId = 1;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 8);
        TestWorld.Days(w, 1);

        var pots = Pockets.All(w);
        Assert.Equal(2, pots.Count);
        Assert.All(pots, p => Assert.Equal(1, p.Divisions));
        Assert.Contains(pots, p => p.RegionIds.Single() == 4);
        Assert.Contains(pots, p => p.RegionIds.Single() == 8);
    }

    [Fact]
    public void OCoracaoDaBolsaEhOndeEstaAMaiorParteDaTropa()
    {
        var w = Build(8, 3);
        w.Regions[6].ControllerId = 1; w.Regions[7].ControllerId = 1;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 6);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 7);
        TestWorld.AddDivision(w, 3, 1, TestWorld.Inf, 7);
        TestWorld.Days(w, 1);

        Assert.Equal(7, Assert.Single(Pockets.All(w)).HeartId);
    }

    [Fact]
    public void BolsaComSaidaNaoEstaFechadaENaoTemPrazo()
    {
        var w = Build();
        w.Regions[5].ControllerId = 1;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 5);
        TestWorld.Days(w, 1);
        Assert.True(d.Cut);
        var fechada = Assert.Single(Pockets.All(w));
        Assert.True(fechada.Sealed);

        // tomar a 6 também não é saída nenhuma: é a mesma bolsa, maior. A saída é um aliado de facção na 6
        w.Regions[6].ControllerId = 1;
        TestWorld.Days(w, 1);
        var maior = Assert.Single(Pockets.All(w));
        Assert.Equal(2, maior.Regions);
        Assert.True(maior.Sealed);

        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        w.Factions["aliados"] = new Faction("aliados", "Aliados", "", new List<int> { 1, 3 });
        w.Regions[6].ControllerId = 3;
        TestWorld.Days(w, 1);
        var aberta = Assert.Single(Pockets.All(w));
        Assert.False(aberta.Sealed);
        Assert.Null(aberta.DaysLeft);
    }

    [Fact]
    public void OPrazoDaBolsaEhOMesmoQueRendeATropa()
    {
        var w = Build();
        w.Register(new PocketSystem());
        w.Regions[5].ControllerId = 1;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 5);
        TestWorld.Days(w, (int)w.Rule("pocket_grace", 3f) + 2);

        var pot = Assert.Single(Pockets.All(w));
        Assert.Equal(PocketSystem.DaysToSurrender(w, d), pot.DaysLeft);
        Assert.Equal(d.PocketDays, pot.Days);
    }

    [Fact]
    public void ABolsaSabeOsPontosDeVitoriaQueFicaramLaDentro()
    {
        var w = Build();
        w.Regions[5].ControllerId = 1;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 5);
        TestWorld.Days(w, 1);

        var pot = Assert.Single(Pockets.All(w));
        Assert.Equal(VictoryPoints.Of(w, w.Regions[5]), pot.Vp);
    }

    [Fact]
    public void QuemFechouOAnelVeAsSuasBolsasDoOutroLado()
    {
        var w = Build();
        w.Regions[5].ControllerId = 1;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 5);
        TestWorld.Days(w, 1);

        Assert.Single(Pockets.Of(w, 1));            // as nossas: a tropa lá dentro é nossa
        Assert.Empty(Pockets.Of(w, 2));
        Assert.Single(Pockets.Closed(w, 2));        // e a do país 2 é a que ele fechou
        Assert.Empty(Pockets.Closed(w, 1));
    }

    [Fact]
    public void APiorBolsaEhAQueSeRendePrimeiro()
    {
        var w = Build(9, 2);
        w.Register(new PocketSystem());
        foreach (int id in new[] { 4, 8 }) w.Regions[id].ControllerId = 1;
        var cedo = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);
        TestWorld.Days(w, 4);
        var tarde = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 8);
        TestWorld.Days(w, 2);

        var worst = Assert.IsType<Pocket>(Pockets.Worst(w, 1));
        Assert.Contains(cedo.Id, worst.DivisionIds);
        Assert.True(worst.DaysLeft < Pockets.Of(w, 1).Last().DaysLeft);
        Assert.DoesNotContain(tarde.Id, worst.DivisionIds);
    }

    [Fact]
    public void ABolsaEmPalavrasDizQuemEstaLaDentroEQuantoFalta()
    {
        var w = Build();
        w.Regions[5].ControllerId = 1;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 5);
        TestWorld.Days(w, 1);

        string texto = Pockets.Short(w, Pockets.All(w)[0]);
        Assert.Contains(w.Countries[1].Tag, texto);
        Assert.Contains("1 div", texto);
        Assert.Contains("1 reg", texto);
    }
}
