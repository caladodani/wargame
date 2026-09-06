using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Balanço da campanha. Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2). O relatório não é
/// guardado em lado nenhum: sai do estado do mundo, e os pesos da pontuação são regras da base de dados.</summary>
public class CampaignReportTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return w;
    }

    [Fact]
    public void FreshCampaign_CountsWhatWeStartedWith()
    {
        var w = Build();
        var r = CampaignReport.Build(w, 1);

        Assert.Equal(3, r.Regions);
        Assert.Equal(3, r.RegionsAtStart);
        Assert.Equal(0, r.NetRegions);
        Assert.Equal(0, r.WarsFought);
        Assert.Empty(r.Conquests);
        Assert.Equal(CampaignReport.Ongoing, r.Verdict);
        Assert.Equal("Paz armada", CampaignReport.Rank(w, r));
    }

    [Fact]
    public void ConqueredRegions_AreListedByFormerOwner()
    {
        var w = Build();
        foreach (int id in new[] { 4, 5 }) { w.Regions[id].ControllerId = 1; w.Regions[id].OwnerId = 1; }
        var r = CampaignReport.Build(w, 1);

        Assert.Equal(5, r.Regions);
        Assert.Equal(2, r.NetRegions);
        var c = Assert.Single(r.Conquests);
        Assert.Equal(2, c.CountryId);
        Assert.Equal(2, c.Regions);
        Assert.True(c.Population > 0);
    }

    [Fact]
    public void OccupiedButNotAnnexed_IsNotAConquest()
    {
        var w = Build();
        w.Regions[4].ControllerId = 1;      // ocupada, o dono continua a ser o país 2
        var r = CampaignReport.Build(w, 1);

        Assert.Equal(4, r.Regions);
        Assert.Empty(r.Conquests);
    }

    [Fact]
    public void ArchivedWars_FeedTheBalance()
    {
        var w = Build();
        w.WarHistory.Add(new WarRecord(1, 2, 0, 100, ARegions: 3, BRegions: 1, ALosses: 4, BLosses: 9, ABattles: 7, BBattles: 2));
        w.WarHistory.Add(new WarRecord(1, 3, 120, 200, ARegions: 0, BRegions: 2, ALosses: 6, BLosses: 1, ABattles: 1, BBattles: 5));
        var r = CampaignReport.Build(w, 1);

        Assert.Equal(2, r.WarsFought);
        Assert.Equal(1, r.WarsWon);
        Assert.Equal(1, r.WarsLost);
        Assert.Equal(3, r.RegionsTaken);
        Assert.Equal(3, r.RegionsGivenUp);
        Assert.Equal(10, r.DivisionsLost);
        Assert.Equal(8, r.BattlesWon);
    }

    [Fact]
    public void OngoingWars_CountAsFought()
    {
        var w = Build();
        w.StartWar(1, 2);
        Assert.Equal(1, CampaignReport.Build(w, 1).WarsFought);
    }

    [Fact]
    public void TheBestDivision_IsTheMostDecorated()
    {
        var w = Build();
        var plain = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        plain.Battles = 40;
        var hero = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1);
        hero.Medals.Add("baptismo"); hero.Medals.Add("aco"); hero.Battles = 12;
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf, 4).Medals.Add("imortais");   // do inimigo: não conta

        var r = CampaignReport.Build(w, 1);
        Assert.Equal(hero.Id, r.BestDivisionId);
        Assert.Equal(2, r.BestDivisionMedals);
        Assert.Equal(12, r.BestDivisionBattles);
        Assert.Equal(2, r.Medals);
        Assert.Equal(2, r.Divisions);
    }

    [Fact]
    public void ScoreWeights_ComeFromTheRules()
    {
        var w = Build();
        w.Rules["score_per_region"] = 0f; w.Rules["score_per_million"] = 0f;
        w.Rules["score_per_battle"] = 0f; w.Rules["score_per_division_lost"] = 0f;
        w.Rules["score_per_advance"] = 0f; w.Rules["score_per_war_lost"] = 0f;
        w.Rules["score_per_war_won"] = 10f;
        w.WarHistory.Add(new WarRecord(1, 2, 0, 10, 3, 1, 0, 0, 0, 0));
        Assert.Equal(10, CampaignReport.Build(w, 1).Score);

        w.Rules["score_per_war_won"] = 25f;
        Assert.Equal(25, CampaignReport.Build(w, 1).Score);
    }

    [Fact]
    public void DominationDoublesTheScore_AndDefeatCutsIt()
    {
        var w = Build();
        int plain = CampaignReport.Build(w, 1).Score;
        Assert.True(plain > 0);
        Assert.Equal((int)(plain * w.Rule("score_domination_bonus", 2f)), CampaignReport.Build(w, 1, CampaignReport.Domination).Score);
        Assert.Equal((int)(plain * w.Rule("score_defeat_penalty", 0.4f)), CampaignReport.Build(w, 1, CampaignReport.Defeat).Score);
    }

    [Fact]
    public void TheScoreNeverGoesBelowZero()
    {
        var w = Build();
        for (int i = 0; i < 20; i++) w.WarHistory.Add(new WarRecord(1, 2, 0, 10, 0, 5, 0, 0, 0, 0));
        Assert.Equal(0, CampaignReport.Build(w, 1).Score);
    }

    [Fact]
    public void TheRankFollowsTheVerdictAndTheScore()
    {
        var w = Build();
        Assert.Equal("Nação ocupada", CampaignReport.Rank(w, CampaignReport.Build(w, 1, CampaignReport.Defeat)));
        Assert.Equal("Hegemonia mundial", CampaignReport.Rank(w, CampaignReport.Build(w, 1, CampaignReport.Domination)));

        w.Rules["rank_power_score"] = 1f; w.Rules["rank_legend_score"] = 1_000_000f;
        Assert.Equal("Potência regional", CampaignReport.Rank(w, CampaignReport.Build(w, 1)));

        w.Rules["rank_legend_score"] = 1f;
        Assert.Equal("Grande potência", CampaignReport.Rank(w, CampaignReport.Build(w, 1)));
    }

    [Fact]
    public void AnUnknownCountry_StillGivesAReportInsteadOfBlowingUp()
    {
        var w = Build();
        var r = CampaignReport.Build(w, 99);
        Assert.Equal(0, r.Regions);
        Assert.Equal("país 99", r.CountryName);
        Assert.Equal(0, r.Techs);
        Assert.Null(r.BestDivisionId);
    }

    [Fact]
    public void PopulationShare_IsMeasuredAgainstTheWholeWorld()
    {
        var w = Build();
        var r = CampaignReport.Build(w, 1);
        long world = w.Regions.Values.Sum(x => (long)x.Population);
        Assert.Equal((float)r.Population / world, r.PopulationShare, 4);
        Assert.InRange(r.PopulationShare, 0f, 1f);
    }
}
