using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Redespacho estratégico: atravessar a retaguarda de comboio em vez de a marchar. Chega em
/// redeploy_speed do tempo, paga organização ao embarcar, quase não se recompõe pelo caminho e desce do
/// comboio se a frente lhe cortar a linha. O comboio não entra em terra inimiga nem atravessa o mar.</summary>
public class RedeployTests
{
    /// <summary>Mapa em linha de 10 regiões: 1..8 do país 1 (retaguarda funda), 9 e 10 do país 2.</summary>
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, n: 10, split: 8);
        w.StartWar(1, 2);
        w.Register(new MovementSystem());
        return w;
    }

    private static float Speed(World w) => w.Rule("redeploy_speed", 0.35f);
    private static float Cost(World w) => w.Rule("redeploy_org_cost", 40f);

    [Fact]
    public void TheRulesComeFromTheDatabase()
    {
        var w = Build();
        Assert.Equal(0.35f, Speed(w), 3);
        Assert.Equal(40f, Cost(w), 3);
        Assert.Equal(0.25f, w.Rule("redeploy_org_regain", 0.25f), 3);
    }

    [Fact]
    public void TheTrainRunsFasterThanTheMarch()
    {
        var w = Build();
        var march = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var rail = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1);
        float onFoot = MovementSystem.HopDays(w, march, w.Regions[1], w.Regions[2]);
        new RedeployCommand(1, rail.Id, 8).Execute(w);
        float byRail = MovementSystem.HopDays(w, rail, w.Regions[1], w.Regions[2]);
        Assert.Equal(onFoot * Speed(w), byRail, 3);
        Assert.True(byRail < onFoot);
    }

    [Fact]
    public void TheTicketIsPaidInOrganisation()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, org: 100f);
        Assert.Null(new RedeployCommand(1, d.Id, 8).Validate(w));
        new RedeployCommand(1, d.Id, 8).Execute(w);
        Assert.True(d.Redeploying);
        Assert.Equal(100f - Cost(w), d.Org, 3);
        Assert.Equal(8, d.DestinationRegionId);
    }

    [Fact]
    public void ItArrivesAndGetsOffTheTrain()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new RedeployCommand(1, d.Id, 8).Execute(w);
        TestWorld.Days(w, 200);
        Assert.Equal(8, d.RegionId);
        Assert.False(d.Redeploying);
        Assert.Empty(d.Path);
    }

    [Fact]
    public void NoTrainIntoEnemyGroundAndNoneAcrossTheSea()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        Assert.Equal("O comboio só chega a terra nossa ou de aliado", new RedeployCommand(1, d.Id, 9).Validate(w));

        // uma ilha nossa do outro lado de uma travessia: os carris acabam na costa
        w.Regions[20] = new Region { Id = 20, Name = "Ilha", OwnerId = 1, InitialOwnerId = 1, ControllerId = 1, Terrain = "plain" };
        w.Regions[8].SeaNeighbours[20] = 300f; w.Regions[20].SeaNeighbours[8] = 300f;
        Assert.Equal("Sem carris: a retaguarda não vai lá dar", new RedeployCommand(1, d.Id, 20).Validate(w));
    }

    [Fact]
    public void NobodyBoardsATrainWhileFighting()
    {
        var w = Build();
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 8);
        var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 9);
        w.ActiveBattles.Add(new Battle { RegionId = 9, AttackerCountryId = 1, Attackers = { att.Id }, Defenders = { def.Id } });
        Assert.Equal("Em combate: não se embarca tropa a bater-se", new RedeployCommand(1, att.Id, 1).Validate(w));
    }

    [Fact]
    public void TheFrontCutsTheLineAndTheTrainStops()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new RedeployCommand(1, d.Id, 8).Execute(w);
        TestWorld.Days(w, 3);
        int at = d.RegionId;
        w.Regions[at + 1].ControllerId = 2;             // a frente rompeu por cima dos carris
        TestWorld.Days(w, 1);
        Assert.False(d.Redeploying);
        Assert.Empty(d.Path);
        Assert.Equal(at, d.RegionId);                   // fica onde estava, em terra nossa
    }

    [Fact]
    public void OnTheTrainNobodyRests()
    {
        var w = Build();
        w.Register(new RecoverySystem());
        var rail = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, org: 50f);
        var rest = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1, org: 50f);
        new RedeployCommand(1, rail.Id, 8).Execute(w);
        float boarded = rail.Org;
        TestWorld.Days(w, 1);
        Assert.True(rail.Org - boarded < (rest.Org - 50f) * 0.5f, $"comboio +{rail.Org - boarded}, parada +{rest.Org - 50f}");
        Assert.True(rest.Org > 50f);
    }

    [Fact]
    public void AMarchOrderTakesTheTroopsOffTheTrain()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new RedeployCommand(1, d.Id, 8).Execute(w);
        Assert.True(d.Redeploying);
        new MoveDivisionCommand(1, d.Id, 9).Execute(w);   // ordem de marcha, e para o inimigo
        Assert.False(d.Redeploying);
        Assert.Equal(9, d.DestinationRegionId);
    }
}
