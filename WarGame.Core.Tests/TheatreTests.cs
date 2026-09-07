using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Teatros de operações: a linha de contacto partida em troços, cada um com a sua guarnição,
/// os seus buracos e o avanço da guerra a que pertence.
///
/// Mapa base: linha 1-2-3 (país 1) | 4-5-6 (país 2). Em guerra, a única região de contacto do país 1 é a 3.</summary>
public class TheatreTests
{
    private static World AtWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        return w;
    }

    [Fact]
    public void APeacefulCountryHasNoFrontAtAll()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Assert.Empty(TheatreSystem.Of(w, 1));
    }

    [Fact]
    public void TheContactLineBecomesOneTheatreNamedAfterItsBiggestRegion()
    {
        var w = AtWar();
        var fronts = TheatreSystem.Of(w, 1);

        var front = Assert.Single(fronts);
        Assert.Equal(2, front.FoeId);
        Assert.Equal(new[] { 3 }, front.RegionIds);          // só a 3 encosta a terra deles
        Assert.Equal("Frente de R3", front.Name);
    }

    [Fact]
    public void TwoStretchesOfBorderApartAreTwoFronts()
    {
        var w = AtWar();
        // uma segunda entrada, longe da primeira: região 7 nossa colada à 8 deles, sem tocar na linha velha
        foreach (var (id, owner, x) in new[] { (7, 1, 100), (8, 2, 200) })
            w.Regions[id] = new Region
            {
                Id = id, Name = "R" + id, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner,
                Terrain = "plain", Population = 5_000_000, CenterX = x, CenterY = 500,
            };
        w.Regions[7].Neighbours.Add(8); w.Regions[8].Neighbours.Add(7);

        var fronts = TheatreSystem.Of(w, 1);
        Assert.Equal(2, fronts.Count);
        Assert.Equal(new[] { 3 }, fronts[0].RegionIds);      // troços ordenados pelo tamanho, depois pelo id
        Assert.Equal(new[] { 7 }, fronts[1].RegionIds);
    }

    [Fact]
    public void CoverageComparesTheDivisionsWeHaveWithWhatTheDatabaseAsks()
    {
        var w = AtWar();
        Assert.Equal(1.5f, w.Rule("theatre_need_per_region"), 3);

        Assert.Equal(0f, TheatreSystem.Of(w, 1)[0].Coverage, 3);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        Assert.Equal(1f / 1.5f, TheatreSystem.Of(w, 1)[0].Coverage, 3);

        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 3);
        var full = TheatreSystem.Of(w, 1)[0];
        Assert.Equal(1f, full.Coverage, 3);                  // guarnição a mais não passa dos 100%
        Assert.Equal(1.5f, full.Need, 3);
        Assert.Equal(2, full.Divisions);
    }

    [Fact]
    public void AContactRegionWithoutTroopsIsAHoleInTheFront()
    {
        var w = AtWar();
        Assert.Equal(1, TheatreSystem.Of(w, 1)[0].Holes);

        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        Assert.Equal(0, TheatreSystem.Of(w, 1)[0].Holes);
    }

    [Fact]
    public void TheFrontSeesWhoIsWaitingOnTheOtherSide()
    {
        var w = AtWar();
        TestWorld.AddDivision(w, 1, 2, TestWorld.Inf2, 4);    // encostada à nossa linha
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 6);    // na retaguarda deles: não conta
        Assert.Equal(1, TheatreSystem.Of(w, 1)[0].FoeDivisions);
    }

    [Fact]
    public void ProgressIsTheShareOfHisLandWeHold()
    {
        var w = AtWar();
        Assert.Equal(0f, TheatreSystem.Progress(w, 1, 2), 3);

        w.Regions[4].ControllerId = 1;
        Assert.Equal(1f / 3f, TheatreSystem.Progress(w, 1, 2), 3);
        Assert.Equal(1f / 3f, TheatreSystem.Of(w, 1)[0].Progress, 3);

        foreach (int id in new[] { 5, 6 }) w.Regions[id].ControllerId = 1;
        Assert.Equal(1f, TheatreSystem.Progress(w, 1, 2), 3);
    }

    [Fact]
    public void CrossingAStepOfAdvanceGoesIntoTheChronicleOnceOnly()
    {
        var w = AtWar();
        w.Register(new ChronicleSystem());
        w.Register(new TheatreSystem());
        w.Tick();
        Assert.DoesNotContain(w.Chronicle, e => e.Kind == "frente");

        w.Regions[4].ControllerId = 1;                        // 33% da terra dele: passa o degrau dos 25%
        w.Tick(); w.Tick();
        var lines = w.Chronicle.Where(e => e.Kind == "frente").ToList();
        Assert.Single(lines);
        Assert.Contains("A frente de Alfa rompe", lines[0].Text);
    }
}
