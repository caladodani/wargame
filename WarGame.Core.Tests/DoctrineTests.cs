using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Doutrinas militares (leis grupo doctrine): efeitos attack/defense entram no combate.</summary>
public class DoctrineTests
{
    [Fact]
    public void DoctrineGroup_LoadsFromSeed_WithDefault()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var active = w.ActiveLaw(w.Countries[1], "doctrine");
        Assert.NotNull(active);
        Assert.Equal("doc_combinada", active!.Id);
    }

    [Fact]
    public void OffensiveDoctrine_RaisesAttackStat()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1]; c.Political = 100f;
        var cmd = new ChangeLawCommand(1, "doc_ofensiva");
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.True(c.Stat("attack") > 1.05f);
        Assert.True(c.Stat("defense") < 1f);
    }

    [Fact]
    public void DefensiveDoctrine_DefenderLosesLessOrg()
    {
        float DefOrgAfter(bool defensive)
        {
            var (w, _) = TestWorld.Build();
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            if (defensive) { w.Countries[2].Political = 100f; new ChangeLawCommand(2, "doc_defensiva").Execute(w); }
            w.Register(new CombatSystem());
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
            var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
            b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
            w.ActiveBattles.Add(b);
            TestWorld.Days(w, 3);
            return def.Org;
        }
        Assert.True(DefOrgAfter(defensive: true) > DefOrgAfter(defensive: false));
    }
}
