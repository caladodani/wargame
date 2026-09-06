using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>StabilitySystem: alvo situacional e efeito no rendimento. Regras do seed:
/// speed 0.5, war_penalty 10, occupied_penalty 40.</summary>
public class StabilityTests
{
    [Fact]
    public void Peace_DriftsTowardsFifty()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Stability = 40f;
        new StabilitySystem().Tick(w);
        Assert.Equal(40.5f, c.Stability, 0.01f);
        c.Stability = 80f;
        new StabilitySystem().Tick(w);
        Assert.Equal(79.5f, c.Stability, 0.01f);
    }

    [Fact]
    public void WarAndOccupation_LowerTheTarget()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);   // país 1: regiões 1-3, 10M cada
        var c = w.Countries[1];
        c.AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        w.Regions[3].ControllerId = 2;   // 1/3 da população ocupada
        c.Stability = 27.5f;
        new StabilitySystem().Tick(w);
        // alvo = 50 − 10 − 40/3 ≈ 26.67 → desce 0.5
        Assert.Equal(27f, c.Stability, 0.01f);
        for (int i = 0; i < 10; i++) new StabilitySystem().Tick(w);
        Assert.Equal(50f - 10f - 40f / 3f, c.Stability, 0.05f);   // encosta ao alvo, não passa
    }

    [Fact]
    public void StabilityFactor_ScalesIncome()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Stability = 50f;
        float baseline = EconomySystem.Income(w, 1);
        c.Stability = 100f;
        Assert.Equal(baseline * 1.5f / 1f, EconomySystem.Income(w, 1), 0.01f);
        c.Stability = 0f;
        Assert.Equal(baseline * 0.5f / 1f, EconomySystem.Income(w, 1), 0.01f);
    }
}
