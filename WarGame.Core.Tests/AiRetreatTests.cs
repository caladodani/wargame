using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A IA retira de batalhas muito desequilibradas (org própria &lt; org inimiga ×
/// ai_retreat_ratio) via RetreatFromBattleCommand; batalhas equilibradas continuam.</summary>
public class AiRetreatTests
{
    private static (World w, Battle b, Division def) Setup(int enemyDivs)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        for (int i = 0; i < enemyDivs; i++)
            b.Attackers.Add(TestWorld.AddDivision(w, 10 + i, 1, TestWorld.Inf, 3).Id);
        var def = TestWorld.AddDivision(w, 1, 2, TestWorld.Inf, 4, org: 20);
        b.Defenders.Add(def.Id);
        w.ActiveBattles.Add(b);
        w.Register(new AiSystem());
        return (w, b, def);
    }

    [Fact]
    public void Ai_RetreatsFromHopelessBattle()
    {
        // 20 de org contra 100: 20 < 100 × 0.25 → retira para a região 5 com penalização
        var (w, b, def) = Setup(enemyDivs: 1);
        TestWorld.Days(w, 1);   // dia 0: IA corre
        Assert.Empty(b.Defenders);
        Assert.Equal(5, def.RegionId);
        Assert.Equal(20f * w.Rule("retreat_org_penalty", 0.5f), def.Org, 0.01f);
    }

    [Fact]
    public void Ai_StaysInBalancedBattle()
    {
        var (w, b, def) = Setup(enemyDivs: 1);
        def.Org = 80;   // 80 ≥ 100 × 0.25 → fica
        TestWorld.Days(w, 1);
        Assert.Contains(def.Id, b.Defenders);
        Assert.Equal(4, def.RegionId);
    }

    [Fact]
    public void Ai_StaysWhenSurrounded()
    {
        // sem fallback (vizinhas hostis) o Validate recusa e a divisão fica a lutar
        var (w, b, def) = Setup(enemyDivs: 1);
        w.Regions[5].ControllerId = 1;
        TestWorld.Days(w, 1);
        Assert.Contains(def.Id, b.Defenders);
    }
}
