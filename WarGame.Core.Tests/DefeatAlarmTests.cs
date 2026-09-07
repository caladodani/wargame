using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Alarme de derrota: a série de batalhas perdidas seguidas, o desgaste que ela cobra quando chega
/// à regra, o aviso que fica na faixa enquanto a derrota é fresca e o revés que a crónica escreve. O que se
/// mede é a contagem e o que ela custa, não o combate — por isso as batalhas entram pelo barramento, que é
/// exactamente o que o DefeatAlarmSystem ouve.</summary>
public class DefeatAlarmTests
{
    private const int Player = 1, Foe = 2;

    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[Player].IsPlayer = true;
        w.Countries[Player].AtWarWith.Add(Foe);
        w.Countries[Foe].AtWarWith.Add(Player);
        w.Register(new DefeatAlarmSystem());
        w.Tick();                                   // liga o sistema ao barramento
        return w;
    }

    /// <summary>Uma batalha em que o atacante ganhou: quem perde é o defensor.</summary>
    private static void Lose(World w, int loser, int regionId = 3)
    {
        int winner = loser == Player ? Foe : Player;
        w.Events.Publish(new BattleEnded(regionId, true, winner, loser));
    }

    [Fact]
    public void TheAlarmRulesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(3f, w.Rule("defeat_streak_alarm"));
        Assert.Equal(1.5f, w.Rule("defeat_exhaustion"), 3);
        Assert.Equal(7f, w.Rule("alert_defeat_days"));
    }

    [Fact]
    public void EachLostBattleAddsToTheStreakAndOneWonBattleThrowsItAway()
    {
        var w = Build();
        var c = w.Countries[Player];
        Assert.Equal(-1, c.LastDefeatDay);

        Lose(w, Player); Lose(w, Player);
        Assert.Equal(2, c.DefeatStreak);
        Assert.Equal(w.Clock.Day, c.LastDefeatDay);
        Assert.Equal(3, c.LastDefeatRegion);

        Lose(w, Foe);                               // ganhámos uma: a série morre aqui
        Assert.Equal(0, c.DefeatStreak);
        Assert.Equal(1, w.Countries[Foe].DefeatStreak);
    }

    [Fact]
    public void OnlyTheThirdDefeatInARowCostsWarExhaustion()
    {
        var w = Build();
        var c = w.Countries[Player];
        Lose(w, Player); Lose(w, Player);
        Assert.Equal(0f, c.WarExhaustion, 3);       // dois empurrões ainda não são a frente a ceder

        Lose(w, Player);
        Assert.Equal(w.Rule("defeat_exhaustion"), c.WarExhaustion, 3);
        Lose(w, Player);                            // e daí para a frente cada uma cobra o seu
        Assert.Equal(2f * w.Rule("defeat_exhaustion"), c.WarExhaustion, 3);
    }

    [Fact]
    public void TheEventSaysWhereItWasHowManyInARowAndWhetherItIsAlarm()
    {
        var w = Build();
        var seen = new List<BattleLost>();
        w.Events.Subscribe<BattleLost>(seen.Add);

        Lose(w, Player, 4);
        w.Events.Publish(new BattleEnded(5, false, Player, Foe));   // assalto nosso travado: perdemos sem perder chão

        Assert.Equal(2, seen.Count);
        Assert.Equal(new BattleLost(Player, 4, 1, true, false), seen[0]);
        Assert.Equal(new BattleLost(Player, 5, 2, false, false), seen[1]);

        Lose(w, Player, 4);
        Assert.True(seen[2].Alarm);
        Assert.Equal(3, seen[2].Streak);
    }

    [Fact]
    public void TheDefeatStaysOnTheAlertStripWhileItIsFresh()
    {
        var w = Build();
        var c = w.Countries[Player];
        Assert.DoesNotContain(Alerts.For(w, Player), a => a.Id == "defeat");

        Lose(w, Player);
        var warn = Assert.Single(Alerts.For(w, Player), a => a.Id == "defeat");
        Assert.Equal(AlertLevel.Warn, warn.Level);
        Assert.Equal(3, warn.RegionId);             // o aviso leva o mapa ao sítio
        Assert.Contains("hoje", warn.Text);

        Lose(w, Player); Lose(w, Player);
        Assert.Equal(AlertLevel.Danger, Assert.Single(Alerts.For(w, Player), a => a.Id == "defeat").Level);

        c.LastDefeatDay = w.Clock.Day - (int)w.Rule("alert_defeat_days") - 1;
        Assert.DoesNotContain(Alerts.For(w, Player), a => a.Id == "defeat");
    }

    [Fact]
    public void TheChronicleOnlyWritesTheDefeatsThatRaisedTheAlarm()
    {
        var w = Build();
        w.Register(new ChronicleSystem());
        w.Tick();

        Lose(w, Player); Lose(w, Player);
        Assert.DoesNotContain(w.Chronicle, e => e.Kind == "reves");

        Lose(w, Player);
        var entry = Assert.Single(w.Chronicle, e => e.Kind == "reves");
        Assert.Contains("3.ª batalha seguida", entry.Text);
        Assert.Equal(3, entry.RegionId);
    }

    [Fact]
    public void SaveRoundTripKeepsTheStreakAndWhereItEnded()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[Player].IsPlayer = true;
        w.Register(new DefeatAlarmSystem());
        w.Tick();
        Lose(w, Player, 4); Lose(w, Player, 4);

        string schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        var back = w2.Countries[Player];
        Assert.Equal(2, back.DefeatStreak);
        Assert.Equal(w.Clock.Day, back.LastDefeatDay);
        Assert.Equal(4, back.LastDefeatRegion);
        Assert.Equal(-1, w2.Countries[Foe].LastDefeatDay);   // quem nunca perdeu continua sem data
    }
}
