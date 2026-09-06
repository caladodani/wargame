using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Saldo da guerra: quem tomou regiões, quem perdeu divisões, quem ganhou batalhas —
/// e o arquivo que sobra quando a guerra acaba (World.WarHistory).</summary>
public class WarStatsTests
{
    private static (World w, WarStatsSystem sys) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var sys = new WarStatsSystem();
        w.Register(sys);
        sys.Bind(w);                 // as mecânicas ligam-se no primeiro Tick; aqui queremos ouvir já
        w.StartWar(1, 2);
        return (w, sys);
    }

    private static WarInfo War(World w) => w.Wars[World.WarKey(1, 2)];

    [Fact]
    public void Capture_CountsForWhoTook()
    {
        var (w, _) = Build();
        w.Events.Publish(new RegionCaptured(4, 2, 1));
        w.Events.Publish(new RegionCaptured(3, 1, 2));
        w.Events.Publish(new RegionCaptured(2, 1, 2));

        Assert.Equal(1, War(w).Side(1).RegionsTaken);
        Assert.Equal(2, War(w).Side(2).RegionsTaken);
        Assert.Equal(2, War(w).Enemy(1).RegionsTaken);
    }

    [Fact]
    public void BattleEnded_CreditsTheWinningSide()
    {
        var (w, _) = Build();
        w.Events.Publish(new BattleEnded(4, AttackerWon: true, AttackerCountryId: 1, DefenderCountryId: 2));
        w.Events.Publish(new BattleEnded(3, AttackerWon: false, AttackerCountryId: 2, DefenderCountryId: 1));

        Assert.Equal(2, War(w).Side(1).BattlesWon);   // ganhou a ataque e a defender
        Assert.Equal(0, War(w).Side(2).BattlesWon);
    }

    [Fact]
    public void DestroyedDivision_CountsAgainstItsOwner()
    {
        var (w, _) = Build();
        TestWorld.AddDivision(w, 7, 1, TestWorld.Inf, 4);   // região 4 é controlada pelo país 2
        w.Events.Publish(new DivisionDestroyed(7));

        Assert.Equal(1, War(w).Side(1).DivisionsLost);
        Assert.Equal(0, War(w).Side(2).DivisionsLost);
    }

    [Fact]
    public void PeacefulNeighbour_IsNotCounted()
    {
        var (w, _) = Build();
        w.EndWar(1, 2);
        w.Events.Publish(new RegionCaptured(4, 2, 1));
        w.Events.Publish(new BattleEnded(4, true, 1, 2));
        Assert.Empty(w.Wars);
        Assert.Empty(w.WarHistory);   // sem WarEnded publicado não há arquivo
    }

    [Fact]
    public void WarEnded_ArchivesTheTallyAndAnnouncesIt()
    {
        var (w, sys) = Build();
        var seen = new List<WarSummary>();
        w.Events.Subscribe<WarSummary>(seen.Add);
        w.Events.Publish(new RegionCaptured(4, 2, 1));
        w.Events.Publish(new BattleEnded(4, true, 1, 2));
        TestWorld.Days(w, 5);

        w.EndWar(1, 2);
        w.Events.Publish(new WarEnded(1, 2));

        var rec = Assert.Single(w.WarHistory);
        Assert.Equal(1, rec.Regions(1));
        Assert.Equal(0, rec.Regions(2));
        Assert.Equal(1, rec.Battles(1));
        Assert.Equal(1, rec.Winner);
        Assert.Equal(5, rec.Days);
        Assert.Equal(rec, Assert.Single(seen).Record);
    }

    [Fact]
    public void SameDayWar_StillGetsArchived()
    {
        var (w, _) = Build();
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 1 };
        w.StartWar(1, 3);
        w.Events.Publish(new WarDeclared(1, 3));    // declarada e fechada sem nada pelo meio
        w.EndWar(1, 3);
        w.Events.Publish(new WarEnded(1, 3));

        var rec = Assert.Single(w.WarHistory);
        Assert.Equal(0, rec.ARegions);
        Assert.Null(rec.Winner);                    // empate a zero não dá vencedor
    }

    [Fact]
    public void History_IsCappedAndKeepsTheNewestFirst()
    {
        var (w, _) = Build();
        w.Rules["war_history_max"] = 2;
        for (int i = 3; i <= 6; i++)
        {
            w.Countries[i] = new Country { Id = i, Tag = "C" + i, Name = "C" + i, CapitalRegionId = 1 };
            w.StartWar(1, i);
            w.Events.Publish(new WarDeclared(1, i));
            w.EndWar(1, i);
            w.Events.Publish(new WarEnded(1, i));
        }

        Assert.Equal(2, w.WarHistory.Count);
        Assert.Equal(6, w.WarHistory[0].B);   // a última guerra está à cabeça
        Assert.Equal(5, w.WarHistory[1].B);
    }

    [Fact]
    public void Tally_SurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var sys = new WarStatsSystem(); w.Register(sys); sys.Bind(w);
        w.StartWar(1, 2);
        w.Events.Publish(new RegionCaptured(4, 2, 1));
        w.Events.Publish(new BattleEnded(4, true, 1, 2));
        TestWorld.AddDivision(w, 7, 2, TestWorld.Inf2, 3);
        w.Events.Publish(new DivisionDestroyed(7));
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 1 };
        w.StartWar(1, 3); w.Events.Publish(new WarDeclared(1, 3));
        w.EndWar(1, 3); w.Events.Publish(new WarEnded(1, 3));

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var war = w2.Wars[World.WarKey(1, 2)];
        Assert.Equal(1, war.Side(1).RegionsTaken);
        Assert.Equal(1, war.Side(1).BattlesWon);
        Assert.Equal(1, war.Side(2).DivisionsLost);
        Assert.Equal(3, Assert.Single(w2.WarHistory).B);
    }
}
