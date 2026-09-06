using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Peso económico do terreno: regras terrain_income_* e coastal_income_bonus entram no
/// rendimento; sem regra o terreno é neutro (1).</summary>
public class TerrainEconomyTests
{
    [Fact]
    public void TerrainRule_ScalesIncome()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        float baseInc = EconomySystem.Income(w, 1);
        w.Rules["terrain_income_plain"] = 2f;   // LinearMap usa plain
        Assert.Equal(baseInc * 2f, EconomySystem.Income(w, 1), 0.01f);
    }

    [Fact]
    public void UnknownTerrain_IsNeutral()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Assert.Equal(1f, EconomySystem.TerrainMult(w, w.Regions[1]), 0.001f);
    }

    [Fact]
    public void Coastal_GetsBonus()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["coastal_income_bonus"] = 1.1f;
        var coastal = new Region { Id = 99, Name = "Porto", OwnerId = 1, InitialOwnerId = 1, ControllerId = 1, Terrain = "plain", Coastal = true, Population = 1_000_000 };
        Assert.Equal(1.1f, EconomySystem.TerrainMult(w, coastal), 0.001f);
    }
}
