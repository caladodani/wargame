using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Comandantes destacados para um grupo de exércitos. A troca é o que interessa: enquanto o
/// general comanda o grupo, o país deixa de ter o bónus dele e as divisões do grupo passam a tê-lo
/// multiplicado por general_command_bonus.</summary>
public class GeneralCommandTests
{
    private static (World w, ArmyGroup g) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 10_000f;
        new CreateArmyGroupCommand(1, "1.º Exército").Execute(w);
        return (w, w.ArmyGroups.Values.Single());
    }

    /// <summary>Um comandante qualquer da base de dados que mexa nesta estatística.</summary>
    private static GeneralDef Def(World w, string statKey) => w.GeneralDefs.Values.First(g => g.StatKey == statKey);

    [Fact]
    public void ADetachedGeneral_LeavesTheCountryBonusAndArrivesAtTheGroupDoubled()
    {
        var (w, g) = Build();
        var def = Def(w, "defense");
        new HireGeneralCommand(1, def.Id).Execute(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);

        Assert.Equal(def.Mult, w.Countries[1].Stat("defense"), 3);   // no estado-maior: vale para todos
        Assert.Equal(1f, w.CommandMult(d, "defense"), 3);

        new AssignGeneralCommand(1, g.Id, def.Id).Execute(w);

        Assert.Equal(1f, w.Countries[1].Stat("defense"), 3);         // saiu do país
        Assert.Equal(1f + (def.Mult - 1f) * w.Rule("general_command_bonus", 2f), w.CommandMult(d, "defense"), 3);
    }

    [Fact]
    public void TheBonusOnlyReachesTheDivisionsOfThatGroup()
    {
        var (w, g) = Build();
        var def = Def(w, "attack");
        new HireGeneralCommand(1, def.Id).Execute(w);
        var inside = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var outside = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2);
        new AssignDivisionCommand(1, inside.Id, g.Id).Execute(w);
        new AssignGeneralCommand(1, g.Id, def.Id).Execute(w);

        Assert.True(w.CommandMult(inside, "attack") > 1f);
        Assert.Equal(1f, w.CommandMult(outside, "attack"), 3);
        Assert.Equal(1f, w.CommandMult(inside, "defense"), 3);   // só a estatística dele
    }

    [Fact]
    public void CallingTheGeneralBack_RestoresTheCountryBonus()
    {
        var (w, g) = Build();
        var def = Def(w, "defense");
        new HireGeneralCommand(1, def.Id).Execute(w);
        new AssignGeneralCommand(1, g.Id, def.Id).Execute(w);
        Assert.Equal(1f, w.Countries[1].Stat("defense"), 3);

        new AssignGeneralCommand(1, g.Id, null).Execute(w);
        Assert.Equal(def.Mult, w.Countries[1].Stat("defense"), 3);
        Assert.Null(g.GeneralId);
    }

    [Fact]
    public void OnlyHiredGenerals_AndOneGroupEach()
    {
        var (w, g) = Build();
        var def = Def(w, "defense");
        Assert.Equal("esse comandante não serve neste exército", new AssignGeneralCommand(1, g.Id, def.Id).Validate(w));

        new HireGeneralCommand(1, def.Id).Execute(w);
        Assert.Null(new AssignGeneralCommand(1, g.Id, def.Id).Validate(w));
        new AssignGeneralCommand(1, g.Id, def.Id).Execute(w);

        new CreateArmyGroupCommand(1, "2.º Exército").Execute(w);
        int other = w.ArmyGroups.Values.First(x => x.Id != g.Id).Id;
        Assert.Equal("já comanda o 1.º Exército", new AssignGeneralCommand(1, other, def.Id).Validate(w));
        Assert.Equal("grupo não é teu", new AssignGeneralCommand(2, g.Id, def.Id).Validate(w));
        Assert.Equal("grupo inexistente", new AssignGeneralCommand(1, 999, def.Id).Validate(w));
    }

    [Fact]
    public void DisbandingTheGroup_SendsTheGeneralBackToTheStaff()
    {
        var (w, g) = Build();
        var def = Def(w, "defense");
        new HireGeneralCommand(1, def.Id).Execute(w);
        new AssignGeneralCommand(1, g.Id, def.Id).Execute(w);

        new DisbandArmyGroupCommand(1, g.Id).Execute(w);
        Assert.Equal(def.Mult, w.Countries[1].Stat("defense"), 3);
    }

    [Fact]
    public void DismissingTheGeneral_TakesHimOutOfTheGroupToo()
    {
        var (w, g) = Build();
        var def = Def(w, "defense");
        new HireGeneralCommand(1, def.Id).Execute(w);
        new AssignGeneralCommand(1, g.Id, def.Id).Execute(w);

        new DismissGeneralCommand(1, def.Id).Execute(w);
        Assert.Null(g.GeneralId);
        Assert.Equal(1f, w.Countries[1].Stat("defense"), 3);
    }

    [Fact]
    public void TheDivisionKnowsItsGroup_AndTheTwoSidesNeverDisagree()
    {
        var (w, g) = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        Assert.Equal(g.Id, d.GroupId);
        Assert.Equal(g.Id, w.GroupOf(d.Id)!.Id);

        new CreateArmyGroupCommand(1, "2.º Exército").Execute(w);
        var other = w.ArmyGroups.Values.First(x => x.Id != g.Id);
        new AssignDivisionCommand(1, d.Id, other.Id).Execute(w);
        Assert.Equal(other.Id, d.GroupId);
        Assert.Empty(g.Divisions);                       // não fica em dois sítios ao mesmo tempo
        Assert.Equal(new[] { d.Id }, other.Divisions);

        new AssignDivisionCommand(1, d.Id, null).Execute(w);
        Assert.Null(d.GroupId);
        Assert.Empty(other.Divisions);

        new AssignDivisionCommand(1, d.Id, other.Id).Execute(w);
        w.RemoveDivision(d.Id);
        Assert.Empty(other.Divisions);                   // divisão destruída sai do grupo
    }

    [Fact]
    public void TheGeneralSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 10_000f;
        var def = w.GeneralDefs.Values.First(x => x.StatKey == "defense");
        new HireGeneralCommand(1, def.Id).Execute(w);
        new CreateArmyGroupCommand(1, "Grupo Norte").Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var d = TestWorld.AddDivision(w, 7, 1, TestWorld.Inf, 1);
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        new AssignGeneralCommand(1, g.Id, def.Id).Execute(w);

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
        Assert.Equal(def.Id, g2.GeneralId);
        Assert.Equal(g2.Id, w2.Divisions[7].GroupId);
        Assert.Equal(1f, w2.Countries[1].Stat("defense"), 3);                     // continua destacado
        Assert.True(w2.CommandMult(w2.Divisions[7], "defense") > 1f);
    }

    [Fact]
    public void ACommandedDivision_RecoversOrganisationFaster()
    {
        var (w, g) = Build();
        var def = Def(w, "org_regain");
        new HireGeneralCommand(1, def.Id).Execute(w);
        var led = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, org: 10f);
        var alone = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2, org: 10f);
        new AssignDivisionCommand(1, led.Id, g.Id).Execute(w);
        new AssignGeneralCommand(1, g.Id, def.Id).Execute(w);

        w.Register(new RecoverySystem());
        w.Tick();

        Assert.True(led.Org > alone.Org, $"comandada {led.Org}, sozinha {alone.Org}");
    }
}
