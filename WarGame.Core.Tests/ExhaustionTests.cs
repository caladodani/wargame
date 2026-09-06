using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Desgaste de guerra: divisões destruídas acumulam WarExhaustion (tecto exhaustion_max)
/// que baixa o alvo da estabilidade; em paz decai exhaustion_decay/dia.</summary>
public class ExhaustionTests
{
    [Fact]
    public void DivisionDeath_AddsExhaustion_Capped()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        w.Register(new CombatSystem());
        // batalha montada à mão: defensor fraquíssimo morre no combate
        var a1 = TestWorld.AddDivision(w, 1, 1, TestWorld.Armor, 3);
        var a2 = TestWorld.AddDivision(w, 2, 1, TestWorld.Armor, 3);
        var weak = TestWorld.AddDivision(w, 3, 2, TestWorld.Inf2, 4, org: 50, hp: 1);
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        b.Attackers.Add(a1.Id); b.Attackers.Add(a2.Id); b.Defenders.Add(weak.Id);
        w.ActiveBattles.Add(b);
        TestWorld.Days(w, 40);
        Assert.False(w.Divisions.ContainsKey(weak.Id));
        Assert.True(w.Countries[2].WarExhaustion >= w.Rule("exhaustion_per_division", 2f));
        Assert.True(w.Countries[2].WarExhaustion <= w.Rule("exhaustion_max", 30f));
    }

    [Fact]
    public void Exhaustion_LowersStabilityTarget()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new StabilitySystem());
        w.Countries[1].WarExhaustion = 20f;
        w.Countries[2].WarExhaustion = 0f;
        TestWorld.Days(w, 100);   // em paz o desgaste decai (0.1/dia): a meio ainda separa os dois
        Assert.True(w.Countries[1].Stability < w.Countries[2].Stability - 5f);
    }

    [Fact]
    public void Exhaustion_DecaysInPeace()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new StabilitySystem());
        w.Countries[1].WarExhaustion = 10f;
        TestWorld.Days(w, 50);
        Assert.True(w.Countries[1].WarExhaustion < 10f - 4f);   // ~0.1/dia
    }

    [Fact]
    public void Exhaustion_FrozenAtWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        w.Register(new StabilitySystem());
        w.Countries[1].WarExhaustion = 10f;
        TestWorld.Days(w, 50);
        Assert.Equal(10f, w.Countries[1].WarExhaustion, 0.01f);
    }
}
