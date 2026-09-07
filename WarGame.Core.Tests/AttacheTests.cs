using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Adidos militares: um país em paz manda um oficial ver a guerra dos outros, paga-lhe a estadia
/// todos os dias e traz para casa experiência de exército. Mundo em linha 1-2-3 (país 1) | 4-5-6 (país 2),
/// mais um terceiro país (3) para haver guerra alheia que observar.</summary>
public class AttacheTests
{
    private static World Build(float money = 100f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        foreach (var c in w.Countries.Values) { c.Money = money; c.IsPlayer = true; }   // ninguém age sozinho salvo teste
        w.Register(new AttacheSystem());
        return w;
    }

    /// <summary>Guerra alheia: o país 2 contra o país 3, para o 1 ter o que observar.</summary>
    private static void ForeignWar(World w) => w.StartWar(2, 3);

    [Fact]
    public void TheCostOfAMissionComesFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(0.5f, w.Rule("attache_cost_per_day"), 3);
        Assert.Equal(0.3f, w.Rule("attache_xp_per_day"), 3);
        Assert.Equal(10f, w.Rule("attache_min_days"), 3);
        Assert.Equal(60f, w.Rule("attache_ai_money"), 3);
    }

    [Fact]
    public void TheOfficerIsPaidEveryDayAndBringsBackWhatHeSaw()
    {
        var w = Build();
        ForeignWar(w);
        Assert.Null(new SendAttacheCommand(1, 2).Validate(w));
        new SendAttacheCommand(1, 2).Execute(w);

        var c = w.Countries[1];
        float money = c.Money;
        TestWorld.Days(w, 4);

        Assert.Equal(money - 4 * w.Rule("attache_cost_per_day"), c.Money, 3);
        Assert.Equal(4 * w.Rule("attache_xp_per_day"), c.ArmyXp, 3);
        Assert.Equal(c.ArmyXp, AttacheSystem.Learned(w, 1), 3);
    }

    [Fact]
    public void WhatTheArmyAlreadyKnowsIsNotLearnedTwice()
    {
        var w = Build();
        ForeignWar(w);
        var c = w.Countries[1];
        c.ArmyXp = w.Rule("army_xp_max") - 0.1f;
        new SendAttacheCommand(1, 2).Execute(w);

        TestWorld.Days(w, 3);
        Assert.Equal(w.Rule("army_xp_max"), c.ArmyXp, 3);      // o tecto é o mesmo das doutrinas
        Assert.Equal(0.1f, AttacheSystem.Learned(w, 1), 3);
    }

    [Fact]
    public void AnEmptyTreasuryBringsTheOfficerHome()
    {
        var w = Build();
        ForeignWar(w);
        new SendAttacheCommand(1, 2).Execute(w);
        w.Countries[1].Money = 0.2f;                            // não chega para o dia de amanhã

        w.Tick();
        Assert.Empty(w.Attaches);
        Assert.Equal(0.2f, w.Countries[1].Money, 3);            // e não se paga o que não se pôde pagar
    }

    [Fact]
    public void PeaceInTheHostEndsTheMission()
    {
        var w = Build();
        ForeignWar(w);
        new SendAttacheCommand(1, 2).Execute(w);
        TestWorld.Days(w, 2);
        Assert.Single(w.Attaches);

        w.EndWar(2, 3);
        w.Tick();
        Assert.Empty(w.Attaches);                               // sem guerra não há nada para ver
    }

    [Fact]
    public void WarAgainstTheHostSendsHimHomeTheSameDay()
    {
        var w = Build();
        ForeignWar(w);
        new SendAttacheCommand(1, 2).Execute(w);
        float money = w.Countries[1].Money;

        w.StartWar(1, 2);
        w.Tick();
        Assert.Empty(w.Attaches);
        Assert.Equal(money, w.Countries[1].Money, 3);           // nem se paga o dia em que se vem embora
    }

    [Fact]
    public void ThereIsOnlyOneOfficerAndSomePlacesHeCannotGo()
    {
        var w = Build();
        Assert.Contains("não está em guerra", new SendAttacheCommand(1, 2).Validate(w)!);   // ninguém se bate ainda
        ForeignWar(w);

        Assert.Contains("de dentro", new SendAttacheCommand(1, 1).Validate(w)!);
        Assert.Contains("anfitrião inválido", new SendAttacheCommand(1, 99).Validate(w)!);
        new SendAttacheCommand(1, 2).Execute(w);
        Assert.Contains("o adido já está com", new SendAttacheCommand(1, 3).Validate(w)!);

        w.StartWar(1, 3);
        Assert.Contains("em guerra com", new SendAttacheCommand(1, 3).Validate(w)!);
    }

    [Fact]
    public void APoorCountryDoesNotSendAnyone()
    {
        var w = Build(money: 4f);
        ForeignWar(w);
        Assert.Contains("no cofre", new SendAttacheCommand(1, 2).Validate(w)!);
        Assert.Empty(w.Attaches);
    }

    [Fact]
    public void TheOfficerCanBeCalledBack()
    {
        var w = Build();
        ForeignWar(w);
        Assert.Contains("não há adido", new RecallAttacheCommand(1).Validate(w)!);
        new SendAttacheCommand(1, 2).Execute(w);

        Assert.Null(new RecallAttacheCommand(1).Validate(w));
        new RecallAttacheCommand(1).Execute(w);
        Assert.Empty(w.Attaches);

        float money = w.Countries[1].Money;
        w.Tick();
        Assert.Equal(money, w.Countries[1].Money, 3);           // a despesa acaba com a missão
    }

    [Fact]
    public void TheMachineSendsItsOwn_AndPrefersAnAlly()
    {
        var w = Build();
        ForeignWar(w);
        w.Countries[1].IsPlayer = false;                        // este é o único que a IA move
        w.Countries[1].Money = w.Rule("attache_ai_money") - 1f;

        w.Tick();
        Assert.Empty(w.Attaches);                               // cofre curto: a missão não parte

        w.Countries[1].Money = 200f;
        w.Tick();
        var sent = Assert.Single(w.Attaches);
        Assert.Equal(2, sent.Value.HostId);                     // dos dois beligerantes, o de id mais baixo

        new RecallAttacheCommand(1).Execute(w);
        w.Factions["pacto"] = new Faction("pacto", "Pacto", "", new List<int> { 1, 3 });
        Assert.Equal(3, AttacheSystem.Pick(w, 1));              // havendo aliado em guerra, é a ele que se vai
    }

    [Fact]
    public void TheMissionSurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        w.StartWar(2, 3);
        AttacheSystem.Send(w, 1, 2);
        w.Attaches[1].Learned = 2.5f;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        w2.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        repo.LoadSave(w2, save);

        var back = Assert.Single(w2.Attaches).Value;
        Assert.Equal(2, back.HostId);
        Assert.Equal(w.Clock.Day, back.SinceDay);
        Assert.Equal(2.5f, back.Learned, 3);
    }
}
