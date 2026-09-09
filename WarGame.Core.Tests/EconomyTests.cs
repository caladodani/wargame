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
    public void Infrastructure_ScalesYield()
    {
        var w = Build();
        w.Regions[1].Infrastructure = 0.5f;
        Assert.Equal(2.5f * PerRegion(w), EconomySystem.Income(w, 1), 1e-4f);
    }



}
