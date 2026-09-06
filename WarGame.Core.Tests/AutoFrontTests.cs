using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Avanço automático: divisão parada com a ordem ligada ataca sozinha a região inimiga vizinha
/// menos defendida; sem guerra, sem vizinho inimigo, sem organização ou em combate fica quieta, e uma
/// ordem manual desliga a ordem permanente.</summary>
public class AutoFrontTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new AutoFrontSystem());
        return w;
    }

    [Fact]
    public void AdvancesIntoAdjacentEnemyRegion()
    {
        var w = Setup();
        w.StartWar(1, 2);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, region: 3, org: 100f, hp: 100f);
        d.AutoAdvance = true;
        TestWorld.Days(w, 1);
        Assert.Equal(4, d.TargetRegionId);   // região 4 é do país 2
    }

    [Fact]
    public void PicksTheLeastDefendedNeighbour()
    {
        var w = Setup();
        w.StartWar(1, 2);
        w.Regions[2].OwnerId = 2; w.Regions[2].ControllerId = 2;   // inimigo dos dois lados da região 3
        TestWorld.AddDivision(w, 20, 2, TestWorld.Inf2, region: 2, org: 100f, hp: 100f);
        TestWorld.AddDivision(w, 21, 2, TestWorld.Inf2, region: 2, org: 100f, hp: 100f);
        TestWorld.AddDivision(w, 22, 2, TestWorld.Inf2, region: 4, org: 100f, hp: 100f);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, region: 3, org: 100f, hp: 100f);
        d.AutoAdvance = true;
        TestWorld.Days(w, 1);
        Assert.Equal(4, d.TargetRegionId);   // 1 defensor contra 2
    }

    [Fact]
    public void WithoutWar_StaysPut()
    {
        var w = Setup();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, region: 3, org: 100f, hp: 100f);
        d.AutoAdvance = true;
        TestWorld.Days(w, 3);
        Assert.Null(d.TargetRegionId);
        Assert.True(d.AutoAdvance);   // a ordem fica de pé à espera da frente
    }

    [Fact]
    public void LowOrganisation_DoesNotAttack()
    {
        var w = Setup();
        w.StartWar(1, 2);
        w.Rules["auto_advance_min_org"] = 40f;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, region: 3, org: 20f, hp: 100f);
        d.AutoAdvance = true;
        TestWorld.Days(w, 1);
        Assert.Null(d.TargetRegionId);
    }

    [Fact]
    public void ManualOrder_ClearsTheStandingOrder()
    {
        var w = Setup();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, region: 3, org: 100f, hp: 100f);
        new SetAutoAdvanceCommand(1, 1, true).Execute(w);
        Assert.True(d.AutoAdvance);
        new MoveDivisionCommand(1, 1, 2).Execute(w);
        Assert.False(d.AutoAdvance);
    }

    [Fact]
    public void Command_RejectsForeignDivisions()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, region: 3, org: 100f, hp: 100f);
        Assert.NotNull(new SetAutoAdvanceCommand(2, 1, true).Validate(w));
        Assert.NotNull(new SetAutoAdvanceCommand(1, 999, true).Validate(w));
    }
}
