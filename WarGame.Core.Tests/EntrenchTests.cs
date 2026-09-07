using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Entrincheiramento: quem fica quieto cava, quem marcha perde o que cavou, quem assalta gasta-o.
/// A trincheira só conta a defender e o forte da região levanta o tecto.</summary>
public class EntrenchTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EntrenchSystem());
        return w;
    }

    [Fact]
    public void TheSpadeWorkComesFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(0.5f, w.Rule("entrench_per_day"), 3);
        Assert.Equal(5f, w.Rule("entrench_max"), 3);
        Assert.Equal(1f, w.Rule("entrench_per_fort"), 3);
        Assert.Equal(0.06f, w.Rule("entrench_defense_per_level"), 3);
        Assert.Equal(1.5f, w.Rule("entrench_attack_loss"), 3);
    }

    [Fact]
    public void StandingStillDigsInUpToTheCeiling()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);

        TestWorld.Days(w, 4);
        Assert.Equal(4 * w.Rule("entrench_per_day"), d.Entrench, 3);

        TestWorld.Days(w, 40);
        Assert.Equal(w.Rule("entrench_max"), d.Entrench, 3);      // e não passa daí em campo aberto
    }

    [Fact]
    public void AFortRaisesWhatCanBeDugThere()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        w.Regions[1].Fort = 3;

        TestWorld.Days(w, 40);
        Assert.Equal(w.Rule("entrench_max") + 3f * w.Rule("entrench_per_fort"), d.Entrench, 3);
        Assert.Equal(8f, EntrenchSystem.Max(w, d), 3);

        w.Regions[1].Fort = 0;                                    // forte perdido: o tecto desce e corta o que sobra
        w.Tick();
        Assert.Equal(w.Rule("entrench_max"), d.Entrench, 3);
    }

    [Fact]
    public void MarchingOrdersEmptyTheTrench()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 6);
        Assert.True(d.Entrench > 0f);

        d.SetPath(new[] { 2 });
        w.Tick();
        Assert.Equal(0f, d.Entrench, 3);
    }

    [Fact]
    public void ChangingRegionLosesTheDiggingEvenWithoutOrders()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 4);
        float dug = d.Entrench;
        Assert.True(dug > 0f);

        w.PlaceDivision(d, 1);                                    // ficar onde está não custa nada
        Assert.Equal(dug, d.Entrench, 3);

        w.PlaceDivision(d, 2);
        Assert.Equal(0f, d.Entrench, 3);
    }

    [Fact]
    public void AssaultingCostsWhatWasDug()
    {
        var w = Build();
        var mine = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var foe = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 3);
        TestWorld.Days(w, 10);
        Assert.Equal(w.Rule("entrench_max"), mine.Entrench, 3);

        var battle = new Battle { RegionId = 3, AttackerCountryId = 1 };
        battle.Attackers.Add(mine.Id); battle.Defenders.Add(foe.Id);
        w.ActiveBattles.Add(battle);

        w.Tick();
        Assert.Equal(w.Rule("entrench_max") - w.Rule("entrench_attack_loss"), mine.Entrench, 3);
        Assert.Equal(w.Rule("entrench_max"), foe.Entrench, 3);    // quem espera continua a cavar

        TestWorld.Days(w, 10);
        Assert.Equal(0f, mine.Entrench, 3);                       // assalto que dura esvazia a trincheira
    }

    [Fact]
    public void TheTrenchOnlyCountsForTheSideThatWaits()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        Assert.Equal(1f, EntrenchSystem.Bonus(w, d), 3);

        d.Entrench = 5f;
        Assert.Equal(1f + 5f * w.Rule("entrench_defense_per_level"), EntrenchSystem.Bonus(w, d), 3);
    }

    [Fact]
    public void ADugInDefenderHoldsBetterThanOneCaughtInTheOpen()
    {
        // dois mundos iguais e com a mesma semente: só muda a trincheira do defensor
        static (World w, Division def) Field(float entrench)
        {
            var (w, _) = TestWorld.Build();
            TestWorld.LinearMap(w);
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 3);
            def.Entrench = entrench;
            var battle = new Battle { RegionId = 3, AttackerCountryId = 1 };
            battle.Attackers.Add(att.Id); battle.Defenders.Add(def.Id);
            w.ActiveBattles.Add(battle);
            w.Register(new CombatSystem());
            return (w, def);
        }

        var (open, bare) = Field(0f);
        var (dug, safe) = Field(10f);
        for (int i = 0; i < 5; i++) { open.Tick(); dug.Tick(); }

        Assert.True(safe.Hp > bare.Hp, $"entrincheirado {safe.Hp:0.0} devia aguentar mais do que {bare.Hp:0.0}");
    }

    [Fact]
    public void TheTrenchSurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Entrench = 3.5f;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(3.5f, w2.Divisions[1].Entrench, 3);
    }
}
