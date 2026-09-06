using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Baixas no comando: o que acontece ao exército quando o comandante cai. A probabilidade vem
/// das regras — nos testes põe-se a 1 (ou a 0) para o sorteio deixar de mandar e ficar só a mecânica.</summary>
public class CommandCasualtyTests
{
    private static (World w, Country c, ArmyGroup g) Setup(params string[] staff)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        foreach (var id in staff.Length == 0 ? new[] { "gen_ofensiva" } : staff) c.Generals.Add(id);
        var g = new ArmyGroup { Id = 1, CountryId = 1, Name = "1.º Exército", GeneralId = c.Generals[0] };
        w.ArmyGroups[1] = g;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        w.JoinGroup(g, 1);
        World.ApplyGenerals(w, c);
        return (w, c, g);
    }

    private static void Battle(World w, bool playerWon = false) =>
        w.Events.Publish(new BattleEnded(3, playerWon, 2, 1));   // país 2 ataca a região 3, o 1 defende

    [Fact]
    public void TheSeveritiesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.WoundKinds.Count >= 3);
        Assert.Contains(w.WoundKinds.Values, k => k.Fatal);
        Assert.Contains(w.WoundKinds.Values, k => !k.Fatal && k.Days > 0);
        Assert.True(w.WoundKinds.Values.Where(k => !k.Fatal).Sum(k => k.Weight) > w.WoundKinds.Values.Where(k => k.Fatal).Sum(k => k.Weight));
    }

    [Fact]
    public void WithTheChanceAtZero_NobodyGetsHurt()
    {
        var (w, c, g) = Setup();
        w.Rules["wound_chance"] = 0f;
        w.Register(new CommandCasualtySystem());
        w.Tick();
        for (int i = 0; i < 20; i++) Battle(w);

        Assert.Empty(c.GeneralWound);
        Assert.Equal("gen_ofensiva", g.GeneralId);
    }

    [Fact]
    public void ABattleCanTakeTheCommanderOut()
    {
        var (w, c, _) = Setup();
        w.Rules["wound_chance"] = 1f;
        var seen = new List<IGameEvent>();
        w.Events.Subscribe<GeneralWounded>(seen.Add);
        w.Events.Subscribe<GeneralKilled>(seen.Add);
        w.Register(new CommandCasualtySystem());
        w.Tick();
        Battle(w);

        Assert.Single(seen);
        Assert.True(c.GeneralWound.Count == 1 || !c.Generals.Contains("gen_ofensiva"));
    }

    [Fact]
    public void AWoundedCommanderIsWorthNothing()
    {
        var (w, c, g) = Setup("gen_ofensiva");
        var d = w.Divisions[1];
        float before = w.CommandMult(d, "attack");
        Assert.True(before > 1f);

        c.GeneralWound["gen_ofensiva"] = w.Clock.Day + 10;
        World.ApplyGenerals(w, c);

        Assert.Equal(1f, w.CommandMult(d, "attack"));       // não amplifica no exército
        Assert.Equal(1f, c.GeneralMult.GetValueOrDefault("attack", 1f));   // nem soma ao país
        Assert.True(w.IsWounded(1, "gen_ofensiva"));
        Assert.Equal(10, w.WoundDaysLeft(1, "gen_ofensiva"));
    }

    [Fact]
    public void TheArmyPassesToTheFirstCommanderFree()
    {
        var (w, c, g) = Setup("gen_ofensiva", "gen_defesa");
        CommandCasualtySystem.Strike(w, c, g, "gen_ofensiva", 3);

        Assert.Equal("gen_defesa", g.GeneralId);
        Assert.True(w.IsWounded(1, "gen_ofensiva") || !c.Generals.Contains("gen_ofensiva"));
    }

    [Fact]
    public void WithNobodyFree_TheCommandIsLeftVacant()
    {
        var (w, c, g) = Setup("gen_ofensiva");
        var seen = new List<CommandHandedOver>();
        w.Events.Subscribe<CommandHandedOver>(seen.Add);

        CommandCasualtySystem.Strike(w, c, g, "gen_ofensiva", 3);

        Assert.Null(g.GeneralId);
        Assert.Null(Assert.Single(seen).NewGeneralId);
    }

    [Fact]
    public void AWoundedManIsNeverAStandIn()
    {
        var (w, c, g) = Setup("gen_ofensiva", "gen_defesa");
        c.GeneralWound["gen_defesa"] = w.Clock.Day + 30;      // o substituto já está no hospital
        CommandCasualtySystem.Strike(w, c, g, "gen_ofensiva", 3);

        Assert.Null(g.GeneralId);
    }

    [Fact]
    public void TheDeadLoseTheServiceRecord()
    {
        var (w, c, g) = Setup("gen_ofensiva");
        c.GeneralXp["gen_ofensiva"] = 300f;
        foreach (var k in w.WoundKinds.Values.ToList())       // só resta a morte no sorteio
            if (!k.Fatal) w.WoundKinds[k.Id] = k with { Weight = 0f };
        var dead = new List<GeneralKilled>();
        w.Events.Subscribe<GeneralKilled>(dead.Add);

        CommandCasualtySystem.Strike(w, c, g, "gen_ofensiva", 3);

        Assert.DoesNotContain("gen_ofensiva", c.Generals);
        Assert.False(c.GeneralXp.ContainsKey("gen_ofensiva"));
        Assert.Equal(3, Assert.Single(dead).RegionId);
    }

    [Fact]
    public void TheWoundHeals_AndTheManComesBack()
    {
        var (w, c, _) = Setup();
        c.GeneralWound["gen_ofensiva"] = w.Clock.Day + 3;
        var back = new List<GeneralRecovered>();
        w.Events.Subscribe<GeneralRecovered>(back.Add);
        w.Register(new CommandCasualtySystem());

        TestWorld.Days(w, 2);
        Assert.True(w.IsWounded(1, "gen_ofensiva"));

        TestWorld.Days(w, 2);
        Assert.False(w.IsWounded(1, "gen_ofensiva"));
        Assert.Single(back);
        // continua destacado, por isso o que volta é o comando do exército (o país só o recupera se o chamarem)
        Assert.True(w.CommandMult(w.Divisions[1], "attack") > 1f);
    }

    [Fact]
    public void LosingTheBattleCostsMoreCommanders()
    {
        int Casualties(bool won)
        {
            var (w, c, g) = Setup("gen_ofensiva");
            w.Rules["wound_chance"] = 0.2f;
            w.Rules["wound_loss_mult"] = 3f;
            int hit = 0;
            w.Events.Subscribe<GeneralWounded>(_ => hit++);
            w.Events.Subscribe<GeneralKilled>(_ => hit++);
            w.Register(new CommandCasualtySystem());
            w.Tick();
            for (int i = 0; i < 60; i++)
            {
                c.GeneralWound.Clear();
                if (!c.Generals.Contains("gen_ofensiva")) { c.Generals.Add("gen_ofensiva"); }
                g.GeneralId = "gen_ofensiva";
                w.Events.Publish(new BattleEnded(3, won, 2, 1));   // o país 1 defende: won=true é derrota dele
            }
            return hit;
        }

        Assert.True(Casualties(true) > Casualties(false), "a derrota tem de custar mais comandantes");
    }

    [Fact]
    public void AManAlreadyInHospitalIsNotHitTwice()
    {
        var (w, c, g) = Setup("gen_ofensiva", "gen_defesa");
        w.Rules["wound_chance"] = 1f;
        int until = w.Clock.Day + 50;
        c.GeneralWound["gen_ofensiva"] = until;
        int hit = 0;
        w.Events.Subscribe<GeneralWounded>(_ => hit++);
        w.Register(new CommandCasualtySystem());
        w.Tick();
        Battle(w);

        Assert.Equal(0, hit);
        Assert.Equal(until, c.GeneralWound["gen_ofensiva"]);
    }

    [Fact]
    public void TheWoundSurvivesASave_ButAHealedOneDoesNot()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Generals.Add("gen_ofensiva"); c.Generals.Add("gen_defesa");
        c.GeneralWound["gen_ofensiva"] = w.Clock.Day + 40;
        c.GeneralWound["gen_defesa"] = w.Clock.Day - 5;         // já sarado: não volta do save

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        var c2 = w2.Countries[1];

        Assert.Equal(c.GeneralWound["gen_ofensiva"], c2.GeneralWound["gen_ofensiva"]);
        Assert.False(c2.GeneralWound.ContainsKey("gen_defesa"));
        Assert.Equal(1f, c2.GeneralMult.GetValueOrDefault("attack", 1f));    // o ferido não conta ao carregar
        Assert.True(c2.GeneralMult.GetValueOrDefault("defense", 1f) > 1f);   // o são conta
    }

    [Fact]
    public void AFallenCommanderMakesTheChronicle()
    {
        var (w, c, g) = Setup("gen_ofensiva");
        w.Register(new ChronicleSystem());
        w.Tick();
        CommandCasualtySystem.Strike(w, c, g, "gen_ofensiva", 3);

        Assert.Equal("baixa", Assert.Single(w.Chronicle).Kind);
    }
}
