using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Leis de ocupação (grupo occupation): dura acelera a resistência e extrai mais;
/// branda o contrário. Entram como multiplicadores de stat via ApplyTechs.</summary>
public class OccupationLawTests
{
    private static World Setup(string? law)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ResistanceSystem());
        w.StartWar(1, 2, 0);
        w.Regions[4].ControllerId = 1;
        if (law is not null) { w.Countries[1].Laws["occupation"] = law; }
        w.ApplyTechs(w.Countries[1]);
        return w;
    }

    [Fact]
    public void SeedHasOccupationLaws()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(3, w.Laws.Values.Count(l => l.Group == "occupation"));
        Assert.Equal("occ_standard", w.Laws.Values.Single(l => l.Group == "occupation" && l.IsDefault).Id);
    }

    [Fact]
    public void HarshLaw_SpeedsResistance()
    {
        var w = Setup("occ_harsh");
        TestWorld.Days(w, 10);
        Assert.Equal(10 * 0.02f * 1.5f, w.Regions[4].Resistance, 0.001f);
    }

    [Fact]
    public void GentleLaw_SlowsResistance()
    {
        var w = Setup("occ_gentle");
        TestWorld.Days(w, 10);
        Assert.Equal(10 * 0.02f * 0.5f, w.Regions[4].Resistance, 0.001f);
    }

    [Fact]
    public void OccupiedYield_FollowsLaw()
    {
        var wStd = Setup(null);
        var wHarsh = Setup("occ_harsh");
        float std = EconomySystem.Income(wStd, 1);
        float harsh = EconomySystem.Income(wHarsh, 1);
        // 3 regiões próprias (1 pt) + ocupada: 1 × occupied_yield 0.5 → dura multiplica só a parte ocupada por 1.2
        Assert.True(harsh > std);
        Assert.Equal(std + 0.5f * 0.2f * wStd.Countries[1].Stat("industry") * wStd.Countries[1].StabilityFactor, harsh, 0.01f);
    }
}
