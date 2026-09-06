using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

public class EconomyTests
{
    /// <summary>LinearMap: 3 regiões × 10M do país 1, 3 do país 2. Só o EconomySystem está registado.</summary>
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EconomySystem());
        return w;
    }

    /// <summary>Rendimento de uma região de 10M com infra 1, derivado da regra.</summary>
    private static float PerRegion(World w) => 10_000_000 / 1e6f * w.Rule("points_per_million");

    [Fact]
    public void OneTick_AddsYieldOfControlledRegions()
    {
        var w = Build();
        w.Tick();
        Assert.Equal(3 * PerRegion(w), w.Countries[1].Money, 1e-4f);
        Assert.Equal(3 * PerRegion(w), w.Countries[2].Money, 1e-4f);
    }

    [Fact]
    public void Money_AccumulatesEveryDay()
    {
        var w = Build();
        TestWorld.Days(w, 5);
        Assert.Equal(5 * 3 * PerRegion(w), w.Countries[1].Money, 1e-3f);
    }

    [Fact]
    public void OccupiedRegion_YieldsFractionToController()
    {
        var w = Build();
        w.Regions[3].ControllerId = 2;              // país 2 ocupa a região 3 (dona: país 1)
        w.Tick();
        float per = PerRegion(w), occ = w.Rule("occupied_yield");
        Assert.Equal(2 * per, w.Countries[1].Money, 1e-4f);
        Assert.Equal(3 * per + per * occ, w.Countries[2].Money, 1e-4f);
    }

    [Fact]
    public void Infrastructure_ScalesYield()
    {
        var w = Build();
        w.Regions[1].Infrastructure = 0.5f;
        Assert.Equal(2.5f * PerRegion(w), EconomySystem.Income(w, 1), 1e-4f);
    }

    [Fact]
    public void Income_MatchesTick()
    {
        var w = Build();
        w.Regions[2].ControllerId = 2; w.Regions[5].Infrastructure = 0.7f;
        float i1 = EconomySystem.Income(w, 1), i2 = EconomySystem.Income(w, 2);
        Assert.True(i1 > 0f && i2 > 0f);
        w.Tick();
        Assert.Equal(i1, w.Countries[1].Money, 1e-5f);
        Assert.Equal(i2, w.Countries[2].Money, 1e-5f);
    }

    [Fact]
    public void CountryWithoutRegions_EarnsNothing()
    {
        var w = Build();
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama" };
        TestWorld.Days(w, 3);
        Assert.Equal(0f, w.Countries[3].Money);
        Assert.Equal(0f, EconomySystem.Income(w, 3));
    }

    [Fact]
    public void RegionOfUnknownController_IsIgnored()
    {
        var w = Build();
        w.Regions[1].ControllerId = 99;             // controlador sem Country (dados estranhos): não rebenta
        w.Tick();
        Assert.Equal(2 * PerRegion(w), w.Countries[1].Money, 1e-4f);
    }
}
