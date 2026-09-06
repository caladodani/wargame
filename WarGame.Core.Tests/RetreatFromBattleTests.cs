using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>RetreatFromBattleCommand: defensor sai para região vizinha com penalização de org;
/// batalha sem defensores resolve-se com captura; cercado não retira.</summary>
public class RetreatFromBattleTests
{
    private static (World w, Battle b, Division def) Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf, 4);
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
        w.ActiveBattles.Add(b);
        return (w, b, def);
    }

    [Fact]
    public void Defender_RetreatsToNeighbour_WithOrgPenalty()
    {
        var (w, b, def) = Setup();
        float orgBefore = def.Org;
        var cmd = new RetreatFromBattleCommand(2, 4);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Empty(b.Defenders);
        Assert.Equal(5, def.RegionId);   // vizinha própria (5 é do país 2)
        Assert.Equal(orgBefore * w.Rule("retreat_org_penalty", 0.5f), def.Org, 0.01f);
    }

    [Fact]
    public void EmptyDefence_ResolvesToCapture_NextTick()
    {
        var (w, _, _) = Setup();
        w.Register(new CombatSystem());
        new RetreatFromBattleCommand(2, 4).Execute(w);
        TestWorld.Days(w, 1);
        Assert.Empty(w.ActiveBattles);
        Assert.Equal(1, w.Regions[4].ControllerId);   // atacante capturou
    }

    [Fact]
    public void Surrounded_CannotRetreat()
    {
        var (w, _, _) = Setup();
        // vizinhas da 4 (3 e 5) hostis ao defensor 2: 3 já é do 1; toma também a 5
        w.Regions[5].ControllerId = 1;
        Assert.NotNull(new RetreatFromBattleCommand(2, 4).Validate(w));
    }

    [Fact]
    public void Attacker_LeavesBattle_InPlace()
    {
        var (w, b, _) = Setup();
        var att = w.Divisions[1];
        new RetreatFromBattleCommand(1, 4).Execute(w);
        Assert.Empty(b.Attackers);
        Assert.Equal(3, att.RegionId);   // ficou na origem
    }
}
