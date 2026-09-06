using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Resistência nas regiões ocupadas: cresce sem guarnição, guarnição suprime,
/// a 1.0 revolta devolve o controlo ao dono e o rendimento ocupado cai com a resistência.</summary>
public class ResistanceTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ResistanceSystem());
        w.StartWar(1, 2, 0);
        w.Regions[4].ControllerId = 1;   // região do país 2 ocupada pelo 1
        return w;
    }

    [Fact]
    public void Occupied_WithoutGarrison_Grows()
    {
        var w = Setup();
        TestWorld.Days(w, 10);
        Assert.Equal(10 * w.Rule("resistance_growth", 0.02f), w.Regions[4].Resistance, 0.001f);
        Assert.Equal(0f, w.Regions[3].Resistance);   // território próprio não mexe
    }

    [Fact]
    public void Garrison_Suppresses()
    {
        var w = Setup();
        TestWorld.Days(w, 10);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);   // guarnição do ocupante
        TestWorld.Days(w, 30);
        Assert.Equal(0f, w.Regions[4].Resistance);
    }

    [Fact]
    public void AtFull_Revolts_BackToOwner()
    {
        var w = Setup();
        var revolts = new List<RegionRevolted>();
        w.Events.Subscribe<RegionRevolted>(revolts.Add);
        TestWorld.Days(w, 60);   // 0.02/dia → 1.0 ao dia 50
        Assert.Single(revolts);
        Assert.Equal(4, revolts[0].RegionId);
        Assert.Equal(1, revolts[0].OldController);
        Assert.Equal(2, w.Regions[4].ControllerId);
        Assert.Equal(0f, w.Regions[4].Resistance);
    }

    [Fact]
    public void CapitulatedOwner_ResistanceFades()
    {
        var w = Setup();
        TestWorld.Days(w, 10);
        Assert.True(w.Regions[4].Resistance > 0f);
        w.Countries[2].Capitulated = true;
        TestWorld.Days(w, 10);
        Assert.Equal(0f, w.Regions[4].Resistance);
    }

    [Fact]
    public void Economy_OccupiedYield_DropsWithResistance()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[4].ControllerId = 1;
        float baseline = EconomySystem.Income(w, 1);
        w.Regions[4].Resistance = 1f;
        float hit = EconomySystem.Income(w, 1);
        // região 4: 10M pop × 0.1 × occupied_yield; a resistência 1.0 corta resistance_output_hit
        float occupied = 10f * 0.1f * w.Rule("occupied_yield", 0.5f) * w.Countries[1].Stat("industry") * w.Countries[1].StabilityFactor;
        Assert.Equal(baseline - occupied * w.Rule("resistance_output_hit", 0.5f), hit, 0.001f);
    }
}
