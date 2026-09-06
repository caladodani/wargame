using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Tabela mundial de potências. O que se mede é a ordem e o que a faz mudar — quem tem mais
/// população, mais indústria, mais exército ou mais tecnologia sobe — e não o valor absoluto da nota,
/// que é só uma soma pesada normalizada.</summary>
public class PowerIndexTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return w;
    }

    private static int Place(World w, int countryId) =>
        PowerIndex.Rankings(w).FindIndex(s => s.CountryId == countryId) + 1;

    [Fact]
    public void MoreArmy_ClimbsTheTable()
    {
        var w = Setup();
        Assert.Equal(2, PowerIndex.Rankings(w).Count);          // dois países, ambos classificados

        for (int i = 0; i < 5; i++) TestWorld.AddDivision(w, 100 + i, 2, TestWorld.Inf2, 5);
        Assert.Equal(1, Place(w, 2));

        for (int i = 0; i < 20; i++) TestWorld.AddDivision(w, 200 + i, 1, TestWorld.Inf, 1);
        Assert.Equal(1, Place(w, 1));
    }

    [Fact]
    public void WithoutTheArmyWeight_TheArmyStopsCounting()
    {
        var w = Setup();
        w.Rules["power_weight_army"] = 0f;
        for (int i = 0; i < 30; i++) TestWorld.AddDivision(w, 100 + i, 2, TestWorld.Inf2, 5);

        // 3 regiões contra 3, mesma população: sem o peso do exército empatam nas parcelas que restam
        Assert.Equal(PowerIndex.Rankings(w)[0].Score, PowerIndex.Rankings(w)[1].Score, 3);
    }

    [Fact]
    public void TechAndIndustryCount_EvenWithoutASingleSoldier()
    {
        var w = Setup();
        float tied = PowerIndex.Rankings(w).First(x => x.CountryId == 1).Score;
        Assert.Equal(tied, PowerIndex.Rankings(w).First(x => x.CountryId == 2).Score, 3);   // partem iguais

        w.Countries[1].Techs.Add("qualquer_tecnologia");
        w.Countries[1].TechMult["industry"] = 2f;

        var table = PowerIndex.Rankings(w);
        Assert.Equal(1, table[0].CountryId);
        Assert.True(table[0].Score > tied);
        Assert.True(table[0].Industry > table[1].Industry);
        Assert.True(table[0].Tech > table[1].Tech);
    }

    [Fact]
    public void ACapitulatedCountry_LeavesTheTable()
    {
        var w = Setup();
        w.Countries[2].Capitulated = true;
        var table = PowerIndex.Rankings(w);
        Assert.Single(table);
        Assert.Equal(1, table[0].CountryId);
    }

    [Fact]
    public void TheTiersComeFromTheTable_NotFromTheCode()
    {
        var w = Setup();
        Assert.NotEmpty(w.PowerTiers);
        var top = w.PowerTiers.OrderByDescending(t => t.MinShare).First();
        Assert.Equal(top.Name, PowerIndex.Tier(w, 1f));
        Assert.Equal(w.PowerTiers.OrderBy(t => t.MinShare).First().Name, PowerIndex.Tier(w, 0f));
    }

    [Fact]
    public void TheSystemWritesThePlaceAndRemembersTheOneBefore()
    {
        var w = Setup();
        w.Register(new PowerRankingSystem());
        for (int i = 0; i < 5; i++) TestWorld.AddDivision(w, 100 + i, 2, TestWorld.Inf2, 5);
        TestWorld.Days(w, (int)w.Rule("power_rank_days", 5f));

        Assert.Equal(1, w.Countries[2].PowerRank);
        Assert.Equal(2, w.Countries[1].PowerRank);
        Assert.True(w.Countries[2].PowerScore > 0f);

        for (int i = 0; i < 40; i++) TestWorld.AddDivision(w, 200 + i, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, (int)w.Rule("power_rank_days", 5f));

        Assert.Equal(1, w.Countries[1].PowerRank);
        Assert.Equal(2, w.Countries[1].PowerRankPrev);   // subiu do segundo lugar
        Assert.Equal(1, w.Countries[2].PowerRankPrev);
    }

    [Fact]
    public void ClimbingIsNews()
    {
        var w = Setup();
        var moves = new List<PowerRankChanged>();
        w.Events.Subscribe<PowerRankChanged>(moves.Add);
        w.Register(new PowerRankingSystem());

        for (int i = 0; i < 5; i++) TestWorld.AddDivision(w, 100 + i, 2, TestWorld.Inf2, 5);
        TestWorld.Days(w, (int)w.Rule("power_rank_days", 5f));
        moves.Clear();

        for (int i = 0; i < 40; i++) TestWorld.AddDivision(w, 200 + i, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, (int)w.Rule("power_rank_days", 5f));

        var up = Assert.Single(moves, m => m.CountryId == 1);
        Assert.Equal(2, up.From);
        Assert.Equal(1, up.To);
        Assert.NotEqual("", up.Tier);
    }

    [Fact]
    public void ThePlaceSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true;
        for (int i = 0; i < 5; i++) TestWorld.AddDivision(w, 100 + i, 1, TestWorld.Inf, 1);
        PowerRankingSystem.Rank(w);
        w.Countries[1].PowerRankPrev = 4;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(w.Countries[1].PowerRank, w2.Countries[1].PowerRank);
        Assert.Equal(4, w2.Countries[1].PowerRankPrev);
    }
}
