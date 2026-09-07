using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Doutrinas de exército: a experiência que se junta em campanha paga escolas de guerra, e a escola
/// escolhida fecha as outras. Nada disto são as leis do grupo doctrine (essas mudam-se por decreto).</summary>
public class ArmyDoctrineTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ArmyXpSystem());
        return w;
    }

    [Fact]
    public void TheSchoolsAndTheirPriceComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(3, w.DoctrineBranches.Count);
        Assert.Equal(12, w.ArmyDoctrines.Count);
        Assert.Equal(0.4f, w.Rule("army_xp_per_battle_day"), 3);
        Assert.Equal(0.1f, w.Rule("army_xp_per_day"), 3);
        Assert.Equal(600f, w.Rule("army_xp_max"), 3);

        var first = w.ArmyDoctrines["mov_1"];
        Assert.Equal("movimento", first.Branch);
        Assert.Null(first.Requires);
        Assert.Equal("mov_1", w.ArmyDoctrines["mov_2"].Requires);
        Assert.True(w.ArmyDoctrines["mov_4"].Cost > w.ArmyDoctrines["mov_1"].Cost);
        Assert.NotEmpty(w.DoctrineEffects["mov_1"]);
    }

    [Fact]
    public void AnArmyInTheFieldLearnsEveryDay()
    {
        var w = Build();
        TestWorld.Days(w, 5);
        Assert.Equal(0f, w.Countries[1].ArmyXp, 3);          // país sem tropa não aprende nada

        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 4);
        Assert.Equal(4 * w.Rule("army_xp_per_day"), w.Countries[1].ArmyXp, 3);
    }

    [Fact]
    public void BattleTeachesFasterThanManoeuvres()
    {
        var w = Build();
        var mine = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var foe = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 3);
        var battle = new Battle { RegionId = 3, AttackerCountryId = 2 };
        battle.Attackers.Add(foe.Id); battle.Defenders.Add(mine.Id);
        w.ActiveBattles.Add(battle);

        w.Tick();
        Assert.Equal(w.Rule("army_xp_per_battle_day") + w.Rule("army_xp_per_day"), w.Countries[1].ArmyXp, 3);
        Assert.Equal(w.Rule("army_xp_per_battle_day") + w.Rule("army_xp_per_day"), w.Countries[2].ArmyXp, 3);
    }

    [Fact]
    public void ThereIsACeilingToWhatAnArmyCarries()
    {
        var w = Build();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        w.Countries[1].IsPlayer = true;                      // sem isto a IA ia gastando a experiência
        w.Countries[1].ArmyXp = w.Rule("army_xp_max") - 0.05f;
        TestWorld.Days(w, 3);
        Assert.Equal(w.Rule("army_xp_max"), w.Countries[1].ArmyXp, 3);
    }

    [Fact]
    public void ASchoolIsPaidForAndItsLessonsShowUpInTheStats()
    {
        var w = Build();
        var c = w.Countries[1];
        c.ArmyXp = 100f;
        float before = c.Stat("move_speed");

        Assert.Null(new AdoptDoctrineCommand(1, "mov_1").Validate(w));
        new AdoptDoctrineCommand(1, "mov_1").Execute(w);

        Assert.Contains("mov_1", c.Doctrines);
        Assert.Equal(100f - w.ArmyDoctrines["mov_1"].Cost, c.ArmyXp, 3);
        Assert.Equal(before * 1.08f, c.Stat("move_speed"), 0.001f);
    }

    [Fact]
    public void WithoutTheExperienceThereIsNoSchool()
    {
        var w = Build();
        var c = w.Countries[1];
        c.ArmyXp = w.ArmyDoctrines["mov_1"].Cost - 1f;
        Assert.False(w.CanAdopt(c, "mov_1"));
        Assert.Contains("experiência", new AdoptDoctrineCommand(1, "mov_1").Validate(w)!);
        Assert.Empty(c.Doctrines);
    }

    [Fact]
    public void OneDegreeAtATime()
    {
        var w = Build();
        var c = w.Countries[1];
        c.ArmyXp = 500f;
        Assert.Equal("mov_1", w.DoctrineBlock(c, "mov_2"));
        Assert.Contains("Precisa", new AdoptDoctrineCommand(1, "mov_2").Validate(w)!);

        new AdoptDoctrineCommand(1, "mov_1").Execute(w);
        Assert.Null(w.DoctrineBlock(c, "mov_2"));
        Assert.Null(new AdoptDoctrineCommand(1, "mov_2").Validate(w));
    }

    [Fact]
    public void ChoosingASchoolClosesTheOthers()
    {
        var w = Build();
        var c = w.Countries[1];
        c.ArmyXp = 500f;
        Assert.Null(w.DoctrineBlock(c, "fog_1"));

        new AdoptDoctrineCommand(1, "mov_1").Execute(w);
        Assert.Equal("movimento", w.DoctrineBranchOf(c));
        Assert.Equal("!mov_1", w.DoctrineBlock(c, "fog_1"));
        Assert.Contains("Escola fechada", new AdoptDoctrineCommand(1, "fog_1").Validate(w)!);
        Assert.Null(new AdoptDoctrineCommand(1, "mov_2").Validate(w));   // dentro do ramo continua a subir
    }

    [Fact]
    public void ASchoolIsNeverLearnedTwice()
    {
        var w = Build();
        var c = w.Countries[1];
        c.ArmyXp = 500f;
        new AdoptDoctrineCommand(1, "mov_1").Execute(w);
        float left = c.ArmyXp;

        Assert.Contains("já é doutrina", new AdoptDoctrineCommand(1, "mov_1").Validate(w)!);
        new AdoptDoctrineCommand(1, "mov_1").Execute(w);
        Assert.Equal(left, c.ArmyXp, 3);                      // e não se paga outra vez
        Assert.Contains("desconhecida", new AdoptDoctrineCommand(1, "nao_existe").Validate(w)!);
        Assert.Contains("inválido", new AdoptDoctrineCommand(99, "mov_1").Validate(w)!);
    }

    [Fact]
    public void TheMachineChoosesAlone_TheGeneralStaffOfThePlayerDoesNot()
    {
        var w = Build();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
        w.Countries[1].IsPlayer = true;
        w.Countries[1].ArmyXp = 500f;
        w.Countries[2].ArmyXp = 500f;

        w.Tick();
        Assert.Empty(w.Countries[1].Doctrines);               // o jogador escolhe no painel
        var picked = Assert.Single(w.Countries[2].Doctrines);
        Assert.Equal(40f, w.ArmyDoctrines[picked].Cost, 3);    // e a IA compra sempre o degrau mais barato

        Assert.Equal("fog_1", ArmyXpSystem.Next(w, w.Countries[1]));   // empate de preço desempata pelo id
    }

    [Fact]
    public void WhatTheArmyLearnedSurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.ArmyXp = 500f;
        new AdoptDoctrineCommand(1, "fog_1").Execute(w);
        new AdoptDoctrineCommand(1, "fog_2").Execute(w);
        float left = c.ArmyXp;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = w2.Countries[1];
        Assert.Equal(new[] { "fog_1", "fog_2" }, back.Doctrines.OrderBy(x => x).ToArray());
        Assert.Equal(left, back.ArmyXp, 3);
        Assert.Equal(w2.Countries[1].Stat("defense"), c.Stat("defense"), 0.001f);   // e os efeitos voltam com elas
    }
}
