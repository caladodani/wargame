using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Planos de batalha: um exército com frente atribuída que fica quieto prepara o terreno, e o que
/// preparou vale força de combate. Marchar e bater-se gasta o plano. Mapa em linha 1-2-3 (país 1) | 4-5-6
/// (país 2), como nos testes de grupos.</summary>
public class BattlePlanTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new BattlePlanSystem());
        return w;
    }

    /// <summary>Grupo do país 1 com frente no país 2 e `n` divisões na região 1.</summary>
    private static (ArmyGroup g, List<Division> divs) Group(World w, GroupStance stance = GroupStance.Defend, int n = 1)
    {
        new CreateArmyGroupCommand(1).Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var divs = new List<Division>();
        for (int i = 0; i < n; i++)
        {
            var d = TestWorld.AddDivision(w, 100 + i, 1, TestWorld.Inf, 1);
            new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
            divs.Add(d);
        }
        new SetArmyGroupFrontCommand(1, g.Id, 2).Execute(w);
        new SetArmyGroupStanceCommand(1, g.Id, stance).Execute(w);
        return (g, divs);
    }

    [Fact]
    public void ThePlanningNumbersComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(0.05f, w.Rule("planning_per_day"), 3);
        Assert.Equal(0.18f, w.Rule("planning_decay"), 3);
        Assert.Equal(1f, w.Rule("planning_max"), 3);
        Assert.Equal(0.25f, w.Rule("planning_bonus"), 3);
    }

    [Fact]
    public void AQuietFrontPreparesTheGroundEveryDay()
    {
        var w = Build();
        var (g, _) = Group(w);
        TestWorld.Days(w, 4);
        Assert.Equal(4 * w.Rule("planning_per_day"), g.Planning, 3);
    }

    [Fact]
    public void ThePlanNeverGoesPastItsCeiling()
    {
        var w = Build();
        var (g, _) = Group(w);
        TestWorld.Days(w, 60);
        Assert.Equal(w.Rule("planning_max"), g.Planning, 3);
    }

    [Fact]
    public void WithoutAFrontOrWithoutAMissionThereIsNoPlan()
    {
        var w = Build();
        var (g, _) = Group(w);
        TestWorld.Days(w, 5);
        Assert.True(g.Planning > 0f);

        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Hold).Execute(w);
        w.Tick();
        Assert.Equal(0f, g.Planning, 3);

        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Advance).Execute(w);
        TestWorld.Days(w, 3);
        Assert.True(g.Planning > 0f);                       // a avançar também se prepara, enquanto ninguém anda
        new SetArmyGroupFrontCommand(1, g.Id, null).Execute(w);
        w.Tick();
        Assert.Equal(0f, g.Planning, 3);
    }

    [Fact]
    public void AGroupResting_HasNoPlanToPrepare()
    {
        var w = Build();
        var (g, _) = Group(w, GroupStance.Reserve);
        TestWorld.Days(w, 6);
        Assert.Equal(0f, g.Planning, 3);
        Assert.False(BattlePlanSystem.Plans(g));
    }

    [Fact]
    public void MarchingSpendsWhatWasPrepared()
    {
        var w = Build();
        var (g, divs) = Group(w, GroupStance.Advance);
        TestWorld.Days(w, 10);
        float ready = g.Planning;
        Assert.Equal(0.5f, ready, 3);

        divs[0].SetPath(new[] { 2 });                       // a marchar: o plano começa a gastar-se
        w.Tick();
        Assert.Equal(ready - w.Rule("planning_decay"), g.Planning, 3);
    }

    [Fact]
    public void OnlyThePartOfTheArmyOnTheMoveSpendsThePlan()
    {
        var w = Build();
        var (g, divs) = Group(w, GroupStance.Advance, n: 4);
        TestWorld.Days(w, 10);
        float ready = g.Planning;

        divs[0].SetPath(new[] { 2 });                       // uma em quatro marcha: gasta-se um quarto
        w.Tick();
        Assert.Equal(ready - w.Rule("planning_decay") / 4f, g.Planning, 3);
    }

    [Fact]
    public void ABattleSpendsThePlanLikeAMarch()
    {
        var w = Build();
        var (g, divs) = Group(w, GroupStance.Advance);
        TestWorld.Days(w, 10);
        float ready = g.Planning;

        var foe = TestWorld.AddDivision(w, 201, 2, TestWorld.Inf2, 1);
        var battle = new Battle { RegionId = 1, AttackerCountryId = 2 };
        battle.Attackers.Add(foe.Id); battle.Defenders.Add(divs[0].Id);
        w.ActiveBattles.Add(battle);

        w.Tick();
        Assert.Equal(ready - w.Rule("planning_decay"), g.Planning, 3);
    }

    [Fact]
    public void ThePlanIsWorthForceInTheField()
    {
        var w = Build();
        var (g, divs) = Group(w);
        var loose = TestWorld.AddDivision(w, 300, 1, TestWorld.Inf, 1);

        Assert.Equal(1f, BattlePlanSystem.Bonus(w, loose), 3);   // divisão sem grupo não tem plano nenhum
        Assert.Equal(1f, BattlePlanSystem.Bonus(w, divs[0]), 3);

        g.Planning = 1f;
        Assert.Equal(1f + w.Rule("planning_bonus"), BattlePlanSystem.Bonus(w, divs[0]), 3);
        g.Planning = 0.5f;
        Assert.Equal(1f + w.Rule("planning_bonus") / 2f, BattlePlanSystem.Bonus(w, divs[0]), 3);
        Assert.Equal(1f, BattlePlanSystem.Bonus(w, loose), 3);
    }

    [Fact]
    public void TheCountdownSaysWhenThePlanIsDone()
    {
        var w = Build();
        var (g, _) = Group(w);
        Assert.Equal(20, BattlePlanSystem.DaysToReady(w, g));   // 1 / 0.05
        TestWorld.Days(w, 10);
        Assert.Equal(10, BattlePlanSystem.DaysToReady(w, g));
        g.Planning = 1f;
        Assert.Equal(0, BattlePlanSystem.DaysToReady(w, g));

        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Hold).Execute(w);
        Assert.Equal(-1, BattlePlanSystem.DaysToReady(w, g));   // sem missão não há contagem
    }

    [Fact]
    public void AFinishedPlanAsksToBeUsed()
    {
        var w = Build();
        var (g, _) = Group(w);
        Assert.DoesNotContain(Alerts.For(w, 1), a => a.Id == "plan");

        TestWorld.Days(w, 20);
        var alert = Assert.Single(Alerts.For(w, 1), a => a.Id == "plan");
        Assert.Contains("pronto", alert.Text);

        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Advance).Execute(w);
        Assert.DoesNotContain(Alerts.For(w, 1), a => a.Id == "plan");   // já está em marcha: não é aviso nenhum
    }

    [Fact]
    public void ThePlanSurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        var d = TestWorld.AddDivision(w, 7, 1, TestWorld.Inf, 1);
        new CreateArmyGroupCommand(1, "Grupo Norte").Execute(w);
        var g = w.ArmyGroups.Values.Single();
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        new SetArmyGroupFrontCommand(1, g.Id, 2).Execute(w);
        new SetArmyGroupStanceCommand(1, g.Id, GroupStance.Defend).Execute(w);
        g.Planning = 0.65f;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(0.65f, w2.ArmyGroups.Values.Single().Planning, 3);
    }
}
