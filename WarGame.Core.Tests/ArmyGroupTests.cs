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
    private static (ArmyGroup g, Division d) Group(World w, int at, bool advancing = true)
    {
        new CreateArmyGroupCommand(1).Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, at);
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        new SetArmyGroupFrontCommand(1, g.Id, 2).Execute(w);
        if (advancing) new SetArmyGroupStanceCommand(1, g.Id, true).Execute(w);
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
        Assert.Equal("sem frente atribuída não há para onde avançar", new SetArmyGroupStanceCommand(1, id, true).Validate(w));

        w.StartWar(1, 2);
        Assert.Null(new SetArmyGroupFrontCommand(1, id, 2).Validate(w));
        new SetArmyGroupFrontCommand(1, id, 2).Execute(w);
        Assert.Null(new SetArmyGroupStanceCommand(1, id, true).Validate(w));

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
        var (g, d) = Group(w, at: 1, advancing: false);
        for (int i = 0; i < 40; i++) w.Tick();

        Assert.Equal(1, d.RegionId);
        Assert.Empty(d.Path);

        new SetArmyGroupStanceCommand(1, g.Id, true).Execute(w);
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
        new SetArmyGroupStanceCommand(1, g.Id, true).Execute(w);

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
}
