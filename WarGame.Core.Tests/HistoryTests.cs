using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>HistorySystem: amostra de history_sample_days em history_sample_days, no máximo
/// history_tracked países (jogador sempre incluído), valores certos.</summary>
public class HistoryTests
{
    private static World Setup(float sampleDays = 2, float tracked = 8)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["history_sample_days"] = sampleDays;
        w.Rules["history_tracked"] = tracked;
        w.Register(new HistorySystem());
        return w;
    }

    [Fact]
    public void Samples_OnInterval_Only()
    {
        var w = Setup(sampleDays: 2);
        TestWorld.Days(w, 5);   // ticks nos dias 0..4 → amostras nos dias 0, 2 e 4
        var days = w.History.Select(h => h.Day).Distinct().OrderBy(d => d).ToList();
        Assert.Equal(new[] { 0, 2, 4 }, days);
    }

    [Fact]
    public void Sample_HasRightValues()
    {
        var w = Setup(sampleDays: 1);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2);
        w.Countries[1].Money = 123f;
        TestWorld.Days(w, 1);
        var s = w.History.Single(h => h.CountryId == 1);
        Assert.Equal(2, s.Divisions);
        Assert.Equal(3, s.Regions);
        Assert.True(s.Money >= 123f);   // economia pode ter somado rendimento no mesmo dia
    }

    [Fact]
    public void Tracked_CapsCountries_PlayerAlwaysIn()
    {
        var w = Setup(sampleDays: 1, tracked: 1);
        w.Countries[2].IsPlayer = true;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);   // país 1 tem mais divisões, mas só há 1 vaga
        TestWorld.Days(w, 1);
        Assert.All(w.History, h => Assert.Equal(2, h.CountryId));
    }

    [Fact]
    public void Sample_CarriesThePowerScoreOfTheDay()
    {
        var w = Setup(sampleDays: 1);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);   // exército só de um lado: as notas separam-se
        TestWorld.Days(w, 1);

        var mine = w.History.Single(h => h.CountryId == 1);
        var theirs = w.History.Single(h => h.CountryId == 2);
        Assert.True(mine.Power > 0f);
        Assert.True(mine.Power > theirs.Power);
        // e é a nota do próprio dia, não a que estiver guardada no país (o ranking corre noutro compasso)
        Assert.Equal(PowerIndex.Rankings(w).First(x => x.CountryId == 1).Score, mine.Power, 1);
    }

    [Fact]
    public void ThePowerCurveGrowsWithTheArmy()
    {
        var w = Setup(sampleDays: 1);
        TestWorld.Days(w, 1);
        float before = w.History.Single(h => h.CountryId == 1 && h.Day == 0).Power;
        for (int i = 0; i < 6; i++) TestWorld.AddDivision(w, 10 + i, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 1);

        Assert.True(w.History.Single(h => h.CountryId == 1 && h.Day == 1).Power > before);
    }

    [Fact]
    public void TheReportKeepsThePeakAndTheDayItHappened()
    {
        var w = Setup(sampleDays: 1);
        w.Countries[1].IsPlayer = true;
        for (int i = 0; i < 6; i++) TestWorld.AddDivision(w, 10 + i, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 1);                              // auge: exército todo em pé, ao dia 0
        foreach (var d in w.Divisions.Values.Where(d => d.CountryId == 1).ToList()) w.RemoveDivision(d.Id);
        TestWorld.Days(w, 1);                              // e depois perde-se

        var r = CampaignReport.Build(w, 1);
        Assert.Equal(0, r.PowerPeakDay);
        Assert.True(r.PowerPeak > r.PowerNow);
        Assert.True(r.PowerFromPeak < 0f);
    }

    [Fact]
    public void ThePowerColumnSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["history_sample_days"] = 1f;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        w.Register(new HistorySystem());
        TestWorld.Days(w, 1);
        float power = w.History.Single(h => h.CountryId == 1).Power;
        Assert.True(power > 0f);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(power, w2.History.Single(h => h.CountryId == 1).Power, 3);
    }

    [Fact]
    public void Capitulated_NotSampled()
    {
        var w = Setup(sampleDays: 1);
        w.Countries[2].Capitulated = true;
        TestWorld.Days(w, 1);
        Assert.DoesNotContain(w.History, h => h.CountryId == 2);
    }
}
