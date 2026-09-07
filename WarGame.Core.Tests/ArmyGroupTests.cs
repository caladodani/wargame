using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Grupos de exércitos com frente atribuída. Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2):
/// um grupo do país 1 posto na região 1, com frente no país 2, tem de atravessar 2 e 3 até dar de caras
/// com a região 4. Os limites vêm das regras army_group_* da base de dados.</summary>
public class ArmyGroupTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ArmyGroupSystem());
        w.Register(new MovementSystem());          // sem ele o w.Tick() não anda com as divisões
        w.Rules["army_group_order_days"] = 1f;    // uma ordem por dia: os testes andam em poucos ticks
        return w;
    }

    /// <summary>Grupo do país 1 com a frente no país 2 e uma divisão na região `at`.</summary>
    private static (ArmyGroup g, Division d) Group(World w, int at, GroupStance stance = GroupStance.Advance)
    {
        new CreateArmyGroupCommand(1).Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, at);
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        new SetArmyGroupFrontCommand(1, g.Id, 2).Execute(w);
        if (stance != GroupStance.Hold) new SetArmyGroupStanceCommand(1, g.Id, stance).Execute(w);
        return (g, d);
    }

    [Fact]
    public void NewGroup_GetsAnOrdinalName_AndTheSlotsAreLimited()
    {
        var w = Build();
        w.Rules["army_group_max"] = 2f;
        Assert.Null(new CreateArmyGroupCommand(1).Validate(w));
        new CreateArmyGroupCommand(1).Execute(w);
        Assert.Equal("1.º Exército", w.ArmyGroups.Values.Single().Name);

        new CreateArmyGroupCommand(1, "Grupo Norte").Execute(w);
        Assert.Contains(w.ArmyGroups.Values, g => g.Name == "Grupo Norte");
        Assert.Equal("não há mais estados-maiores", new CreateArmyGroupCommand(1).Validate(w));

        new CreateArmyGroupCommand(2).Execute(w);   // o limite é por país
        Assert.Equal(3, w.ArmyGroups.Count);
    }

    [Fact]
    public void ADivision_ServesOnlyOneGroup_AndLeavesWhenAssignedToNone()
    {
        var w = Build();
        new CreateArmyGroupCommand(1).Execute(w);
        new CreateArmyGroupCommand(1).Execute(w);
        var (a, b) = (w.ArmyGroups.Values.First(), w.ArmyGroups.Values.Last());
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);

        new AssignDivisionCommand(1, d.Id, a.Id).Execute(w);
        new AssignDivisionCommand(1, d.Id, b.Id).Execute(w);
        Assert.Empty(a.Divisions);
        Assert.Equal(new[] { d.Id }, b.Divisions);
        Assert.Equal(b.Id, w.GroupOf(d.Id)!.Id);

        new AssignDivisionCommand(1, d.Id, null).Execute(w);
        Assert.Null(w.GroupOf(d.Id));
    }

    [Fact]
    public void AFrontIsOnlyAssignedAgainstSomeoneWeAreFightingAndAdvanceNeedsIt()
    {
        var w = Build();
        new CreateArmyGroupCommand(1).Execute(w);
        int id = w.ArmyGroups.Values.Single().Id;

        Assert.Equal("só se atribui uma frente contra quem estás em guerra", new SetArmyGroupFrontCommand(1, id, 2).Validate(w));
        Assert.Equal("sem frente atribuída não há para onde avançar", new SetArmyGroupStanceCommand(1, id, GroupStance.Advance).Validate(w));

        w.StartWar(1, 2);
        Assert.Null(new SetArmyGroupFrontCommand(1, id, 2).Validate(w));
        new SetArmyGroupFrontCommand(1, id, 2).Execute(w);
        Assert.Null(new SetArmyGroupStanceCommand(1, id, GroupStance.Advance).Validate(w));

        Assert.Equal("grupo não é teu", new SetArmyGroupFrontCommand(2, id, 1).Validate(w));
    }

    [Fact]
    public void AnAdvancingGroup_MarchesAcrossTheMapToTheFront()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (_, d) = Group(w, at: 1);
        for (int i = 0; i < 400 && d.RegionId != 4; i++) w.Tick();

        Assert.Equal(4, d.RegionId);   // atravessou 2 e 3 até entrar em território inimigo
    }

    [Fact]
    public void HoldingGroups_GiveNoOrders()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (g, d) = Group(w, at: 1, stance: GroupStance.Hold);
        for (int i = 0; i < 40; i++) w.Tick();

        Assert.Equal(1, d.RegionId);
        Assert.Empty(d.Path);

        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Advance).Execute(w);
        for (int i = 0; i < 40 && d.Path.Count == 0; i++) w.Tick();
        Assert.NotEmpty(d.Path);
    }

    [Fact]
    public void WithoutAWar_OrWithTiredTroops_TheGroupStaysPut()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (_, d) = Group(w, at: 1);
        d.Org = w.Rule("army_group_min_org", 35f) - 1f;
        for (int i = 0; i < 20; i++) { new ArmyGroupSystem().Tick(w); }
        Assert.Empty(d.Path);

        d.Org = 100f;
        w.EndWar(1, 2);
        for (int i = 0; i < 20; i++) { new ArmyGroupSystem().Tick(w); }
        Assert.Empty(d.Path);
    }

    [Fact]
    public void TheFrontOutOfReach_IsNotChased()
    {
        var w = Build();
        w.StartWar(1, 2);
        w.Rules["army_group_march_range"] = 1f;   // a frente fica a 3 saltos da região 1
        var (_, d) = Group(w, at: 1);
        for (int i = 0; i < 20; i++) w.Tick();
        Assert.Empty(d.Path);
        Assert.Equal(1, d.RegionId);
    }

    [Fact]
    public void DeadDivisions_LeaveTheGroupOnTheirOwn()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (g, d) = Group(w, at: 1);
        Assert.Single(g.Divisions);

        w.RemoveDivision(d.Id);
        Assert.Empty(g.Divisions);            // World.RemoveDivision limpa os grupos
        new ArmyGroupSystem().Tick(w);        // e o sistema aguenta um grupo vazio
        Assert.Empty(g.Divisions);
    }

    [Fact]
    public void DisbandingLeavesTheDivisionsWhereTheyAre()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (g, d) = Group(w, at: 1);
        int at = d.RegionId;
        new DisbandArmyGroupCommand(1, g.Id).Execute(w);

        Assert.Empty(w.ArmyGroups);
        Assert.Equal(at, w.Divisions[d.Id].RegionId);
        Assert.Null(w.GroupOf(d.Id));
    }

    [Fact]
    public void Groups_SurviveSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        var d = TestWorld.AddDivision(w, 5, 1, TestWorld.Inf, 1);
        new CreateArmyGroupCommand(1, "Grupo Sul").Execute(w);
        var g = w.ArmyGroups.Values.Single();
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        new SetArmyGroupFrontCommand(1, g.Id, 2).Execute(w);
        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Advance).Execute(w);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var g2 = w2.ArmyGroups.Values.Single();
        Assert.Equal("Grupo Sul", g2.Name);
        Assert.Equal(2, g2.FrontCountryId);
        Assert.Equal(GroupStance.Advance, g2.Stance);
        Assert.True(g2.Advancing);
        Assert.Equal(new[] { 5 }, g2.Divisions);
        Assert.Equal(g2.Id, w2.GroupOf(5)!.Id);
    }

    [Fact]
    public void Strength_IsTheSumOfOrgTimesHp()
    {
        var w = Build();
        new CreateArmyGroupCommand(1).Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var a = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, org: 50f, hp: 100f);
        var b = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1, org: 100f, hp: 50f);
        new AssignDivisionCommand(1, a.Id, g.Id).Execute(w);
        new AssignDivisionCommand(1, b.Id, g.Id).Execute(w);

        Assert.Equal(100f, ArmyGroupSystem.Strength(w, g), 2);
    }

    [Fact]
    public void ADefendingGroup_MarchesToTheLastRegionOfOursAndStopsThere()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (_, d) = Group(w, at: 1, stance: GroupStance.Defend);
        for (int i = 0; i < 400 && d.RegionId != 3; i++) w.Tick();

        Assert.Equal(3, d.RegionId);            // a linha: última nossa antes do inimigo
        for (int i = 0; i < 40; i++) w.Tick();
        Assert.Equal(3, d.RegionId);            // e fica lá, não entra na região 4
        Assert.Empty(d.Path);
    }

    [Fact]
    public void ADefendingGroup_PullsBackOutOfEnemyGround()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (_, d) = Group(w, at: 4, stance: GroupStance.Defend);   // metida em casa do inimigo
        for (int i = 0; i < 400 && d.RegionId != 3; i++) w.Tick();

        Assert.Equal(3, d.RegionId);
    }

    [Fact]
    public void ChangingToDefend_HoldsWhatWasTakenInsteadOfPushingOn()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (g, d) = Group(w, at: 1);
        for (int i = 0; i < 400 && d.RegionId != 4; i++) w.Tick();
        Assert.Equal(4, d.RegionId);
        Assert.Equal(1, w.Regions[4].ControllerId);   // tomada: a linha da frente passou a ser a 5

        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Defend).Execute(w);
        for (int i = 0; i < 80; i++) w.Tick();
        Assert.Equal(4, d.RegionId);                  // segura o terreno ganho e não entra na 5
    }

    [Fact]
    public void DroppingTheFront_StandsTheGroupDown()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (g, _) = Group(w, at: 1, stance: GroupStance.Defend);
        Assert.Equal(GroupStance.Defend, g.Stance);
        Assert.True(g.NeedsFront);

        new SetArmyGroupFrontCommand(1, g.Id, null).Execute(w);
        Assert.Equal(GroupStance.Hold, g.Stance);
        Assert.False(g.NeedsFront);
        Assert.False(g.Advancing);
    }

    /// <summary>Mapa em Y a partir do hub 1: um braço curto 2→3 (inimigo a 2 saltos) e um braço comprido
    /// 4→5→6 (inimigo a 4 saltos). Serve só para testar a âncora — TheatreSystem já tem os seus próprios
    /// testes de segmentação.</summary>
    private static World YMap()
    {
        var (w, _) = TestWorld.Build();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 6, Manpower = 1e9f };
        Region R(int id, int owner) => new Region
        {
            Id = id, Name = "R" + id, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner,
            Terrain = "plain", Population = 10_000_000, CenterX = id * 100, CenterY = 0,
        };
        var r1 = R(1, 1); var r2 = R(2, 1); var r3 = R(3, 2);
        var r4 = R(4, 1); var r5 = R(5, 1); var r6 = R(6, 2);
        r1.Neighbours.AddRange(new[] { 2, 4 });
        r2.Neighbours.AddRange(new[] { 1, 3 });
        r3.Neighbours.Add(2);
        r4.Neighbours.AddRange(new[] { 1, 5 });
        r5.Neighbours.AddRange(new[] { 4, 6 });
        r6.Neighbours.Add(5);
        foreach (var r in new[] { r1, r2, r3, r4, r5, r6 }) w.Regions[r.Id] = r;
        w.Register(new ArmyGroupSystem());
        w.Register(new MovementSystem());
        w.Rules["army_group_order_days"] = 1f;
        w.StartWar(1, 2);
        return w;
    }

    [Fact]
    public void WithoutAnAnchor_TheGroupMarchesToTheNearestTheatre()
    {
        var w = YMap();
        new CreateArmyGroupCommand(1).Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        new SetArmyGroupFrontCommand(1, g.Id, 2).Execute(w);   // sem âncora: país 2 inteiro
        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Advance).Execute(w);

        for (int i = 0; i < 400 && d.RegionId != 3; i++) w.Tick();
        Assert.Equal(3, d.RegionId);   // foi pelo braço curto, que é o mais perto
    }

    [Fact]
    public void AnAnchoredFront_MakesTheGroupIgnoreTheNearerTheatreAndMarchToItsOwn()
    {
        var w = YMap();
        new CreateArmyGroupCommand(1).Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        new SetArmyGroupFrontCommand(1, g.Id, 2, 6).Execute(w);   // âncora no troço distante (região 6)
        Assert.Equal(6, g.FrontRegionId);
        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Advance).Execute(w);

        for (int i = 0; i < 400 && d.RegionId != 5; i++) w.Tick();
        Assert.Equal(5, d.RegionId);   // atravessou o braço comprido (4→5), ignorando a região 3 mais perto

        // largar a frente também larga a âncora — sem isto ficava presa ao troço antigo mesmo sem inimigo atribuído
        new SetArmyGroupFrontCommand(1, g.Id, null).Execute(w);
        Assert.Null(g.FrontRegionId);
    }

    [Fact]
    public void AnAnchorOnTerritoryNoLongerEnemyOwned_FailsValidation()
    {
        var w = YMap();
        new CreateArmyGroupCommand(1).Execute(w);
        int id = w.ArmyGroups.Values.Single().Id;
        Assert.Equal("esse troço já não é do inimigo", new SetArmyGroupFrontCommand(1, id, 2, 1).Validate(w));  // região 1 é nossa, não do país 2
    }
}
