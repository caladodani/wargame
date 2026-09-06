using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>PeaceSystem: capitulação quando os inimigos controlam população a mais
/// (capitulate_share; com capital perdida basta capitulate_share_capital) ou quando o
/// país fica sem regiões. TestWorld não regista sistemas — Tick é chamado à mão.</summary>
public class PeaceTests
{
    private static void War(World w, int a, int b)
    { w.Countries[a].AtWarWith.Add(b); w.Countries[b].AtWarWith.Add(a); }

    /// <summary>Mapa 1..n; país 2 (capital n) perde o controlo das regiões `lost` para o país 1.</summary>
    private static World Front(int n, int split, params int[] lost)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, n, split);
        War(w, 1, 2);
        foreach (var r in lost) w.Regions[r].ControllerId = 1;
        return w;
    }

    [Fact]
    public void BelowThreshold_NoCapitulation()
    {
        var w = Front(7, 3, 4, 5);   // 2/4 = 50% sem capital perdida < 75%
        new PeaceSystem().Tick(w);
        Assert.False(w.Countries[2].Capitulated);
        Assert.True(w.AreAtWar(1, 2));
    }

    [Fact]
    public void ThreeQuartersLost_Capitulates_RegionsGoToWinner()
    {
        var w = Front(7, 3, 4, 5, 6);   // 3/4 = 75%
        new PeaceSystem().Tick(w);
        var c = w.Countries[2];
        Assert.True(c.Capitulated);
        Assert.Equal(0, c.CapitulatedDay);
        Assert.Empty(c.AtWarWith);
        foreach (var r in new[] { 4, 5, 6, 7 })
        { Assert.Equal(1, w.Regions[r].OwnerId); Assert.Equal(1, w.Regions[r].ControllerId); }
    }

    [Fact]
    public void CapitalLost_HalfIsEnough_ButThirdWithCapitalSafeIsNot()
    {
        var wCap = Front(6, 3, 5, 6);   // 2/3 ≥ 50% e capital (6) perdida
        new PeaceSystem().Tick(wCap);
        Assert.True(wCap.Countries[2].Capitulated);

        var wSafe = Front(6, 3, 4);   // 1/3 e capital intacta
        new PeaceSystem().Tick(wSafe);
        Assert.False(wSafe.Countries[2].Capitulated);
    }

    [Fact]
    public void Capitulation_RemovesDivisions_EndsWars_PublishesEvents()
    {
        var w = Front(7, 3, 4, 5, 6);
        TestWorld.AddDivision(w, 21, 2, TestWorld.Inf2, 7);
        var caps = new List<CountryCapitulated>(); var ends = new List<WarEnded>(); var dead = new List<int>();
        w.Events.Subscribe<CountryCapitulated>(caps.Add);
        w.Events.Subscribe<WarEnded>(ends.Add);
        w.Events.Subscribe<DivisionDestroyed>(e => dead.Add(e.DivisionId));
        new PeaceSystem().Tick(w);
        Assert.Equal(new CountryCapitulated(2, 1), Assert.Single(caps));
        Assert.Equal(new WarEnded(1, 2), Assert.Single(ends));
        Assert.Equal(21, Assert.Single(dead));
        Assert.False(w.Divisions.ContainsKey(21));
        Assert.Empty(w.Countries[1].AtWarWith);
    }

    [Fact]
    public void NoRegionsOwned_CapitulatesImmediately()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        War(w, 1, 2);
        foreach (var r in w.Regions.Values.Where(r => r.OwnerId == 2)) { r.OwnerId = 1; r.ControllerId = 1; }
        new PeaceSystem().Tick(w);
        Assert.True(w.Countries[2].Capitulated);
    }

    [Fact]
    public void SaveRoundTrip_PreservesOwnerAndCapitulated()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w, 7, 3);
        War(w, 1, 2);
        foreach (var r in new[] { 4, 5, 6 }) w.Regions[r].ControllerId = 1;
        new PeaceSystem().Tick(w);
        Assert.True(w.Countries[2].Capitulated);

        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2, 7, 3);
        repo.LoadSave(w2, save);
        Assert.True(w2.Countries[2].Capitulated);
        Assert.Equal(0, w2.Countries[2].CapitulatedDay);
        foreach (var r in new[] { 4, 5, 6, 7 })
        { Assert.Equal(1, w2.Regions[r].OwnerId); Assert.Equal(1, w2.Regions[r].ControllerId); }
        Assert.Empty(w2.Countries[2].AtWarWith);
    }
}
