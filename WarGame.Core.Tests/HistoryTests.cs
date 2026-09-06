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
    public void Capitulated_NotSampled()
    {
        var w = Setup(sampleDays: 1);
        w.Countries[2].Capitulated = true;
        TestWorld.Days(w, 1);
        Assert.DoesNotContain(w.History, h => h.CountryId == 2);
    }
}
