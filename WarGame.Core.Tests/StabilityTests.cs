using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>StabilitySystem: alvo situacional e efeito no rendimento. Regras do seed:
/// speed 0.5, war_penalty 10, occupied_penalty 40.</summary>
public class StabilityTests
{


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
