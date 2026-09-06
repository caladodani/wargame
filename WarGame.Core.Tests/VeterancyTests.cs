using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Veterania: XP por dia de batalha (tecto xp_max) e bónus de força em combate.</summary>
public class VeterancyTests
{
    [Fact]
    public void BattleDays_GrantXp_Capped()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        w.Register(new CombatSystem());
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
        w.ActiveBattles.Add(b);
        TestWorld.Days(w, 5);
        Assert.True(att.Xp >= 4f, $"xp={att.Xp}");
        Assert.True(att.Xp <= w.Rule("xp_max", 100f));
    }

    [Fact]
    public void Veterans_BeatGreenTroops_OtherThingsEqual()
    {
        // dois duelos idênticos, num deles o defensor é veterano: perde menos org
        float DefOrgAfter(float defXp)
        {
            var (w, _) = TestWorld.Build();
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            w.Register(new CombatSystem());
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
            def.Xp = defXp;
            var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
            b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
            w.ActiveBattles.Add(b);
            TestWorld.Days(w, 3);
            return def.Org;
        }
        Assert.True(DefOrgAfter(100f) > DefOrgAfter(0f));
    }
}
