using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>DisbandDivisionCommand: refund de manpower proporcional ao HP, bloqueado em combate.</summary>
public class DisbandTests
{
    [Fact]
    public void Disband_RefundsManpower_ByHp()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1]; c.Manpower = 0f;
        var d = TestWorld.AddDivision(w, 1, TestWorld.Inf, TestWorld.Inf, 1, hp: 50f);
        var cmd = new DisbandDivisionCommand(1, d.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        float expected = w.TemplateCost(d.TemplateId) * w.Rule("manpower_per_cost", 500f) * 0.5f
                         * w.Rule("disband_manpower_refund", 0.5f);
        Assert.Equal(expected, c.Manpower, 0.5f);
        Assert.False(w.Divisions.ContainsKey(d.Id));
        Assert.DoesNotContain(d.Id, w.Regions[1].DivisionIds);
    }

    [Fact]
    public void Disband_Blocked_InBattle_OrForeign()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        Assert.NotNull(new DisbandDivisionCommand(2, d.Id).Validate(w));   // não é dele
        w.ActiveBattles.Add(new Battle { RegionId = 1, AttackerCountryId = 2 });
        w.ActiveBattles[0].Defenders.Add(d.Id);
        Assert.NotNull(new DisbandDivisionCommand(1, d.Id).Validate(w));   // em combate
    }
}
