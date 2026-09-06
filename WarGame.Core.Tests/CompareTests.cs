using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Folha de comparação directa entre dois países: o que é público, o que é segredo sem
/// espionagem, e quem leva vantagem em cada conta.</summary>
public class CompareTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true;
        return w;
    }

    private static CompareRow Row(World w, int viewer, string label) =>
        Compare.Sheet(w, viewer, 1, 2).First(r => r.Label == label);

    [Fact]
    public void TheSheetHasTheThreeSections()
    {
        var w = Setup();
        var groups = Compare.Sheet(w, 0, 1, 2).Select(r => r.Group).Distinct().ToList();
        Assert.Equal(3, groups.Count);
        Assert.Contains("Terra", groups); Assert.Contains("Economia", groups); Assert.Contains("Guerra", groups);
    }

    [Fact]
    public void LandAndPeopleAreAlwaysPublic()
    {
        var w = Setup();
        var regions = Row(w, 1, "Regiões");
        Assert.False(regions.Secret);
        Assert.Equal("3", regions.Left);
        Assert.Equal("3", regions.Right);
        Assert.Equal(0, regions.Winner);                       // 3 contra 3: empate
    }

    [Fact]
    public void MoreGroundWinsTheLine()
    {
        var w = Setup();
        w.Regions[4].ControllerId = 1;                          // tomámos-lhes uma
        var regions = Row(w, 1, "Regiões");
        Assert.Equal("4", regions.Left);
        Assert.Equal("2", regions.Right);
        Assert.Equal(1, regions.Winner);
    }

    [Fact]
    public void TheirBarracksAreASecretWithoutIntelligence()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 20, 2, TestWorld.Inf2, 5);
        var divs = Row(w, 1, "Divisões");
        Assert.True(divs.Secret);
        Assert.Equal("?", divs.Right);
        Assert.Equal(0, divs.Winner);                           // não se ganha uma conta que não se sabe
    }

    [Fact]
    public void OurOwnSideIsNeverASecret()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 1);
        Assert.Equal("1", Row(w, 1, "Divisões").Left);
    }

    [Fact]
    public void IntelligenceOpensTheirBarracks()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 20, 2, TestWorld.Inf2, 5);
        TestWorld.AddDivision(w, 21, 2, TestWorld.Inf2, 6);
        w.Intel[(1, 2)] = w.Clock.Day + 10;

        var divs = Row(w, 1, "Divisões");
        Assert.False(divs.Secret);
        Assert.Equal("2", divs.Right);
        Assert.Equal(2, divs.Winner);                           // eles à frente, e agora sabe-se
    }

    [Fact]
    public void AnAllyShowsItsBooks()
    {
        var w = Setup();
        var f = w.CreateFaction("pacto", "Pacto", "");
        f.Members.Add(1); f.Members.Add(2);
        Assert.False(Row(w, 1, "Cofre").Secret);
    }

    [Fact]
    public void WithTheFogOffEverythingIsOnTheTable()
    {
        var w = Setup();
        w.Rules["fog_of_war"] = 0f;
        Assert.False(Row(w, 1, "Divisões").Secret);
    }

    [Fact]
    public void AnOmniscientViewerSeesBothSides()
    {
        var w = Setup();
        Assert.False(Row(w, 0, "Cofre").Secret);
    }

    [Fact]
    public void WarExhaustionIsBetterWhenItIsLower()
    {
        var w = Setup();
        w.Countries[1].WarExhaustion = 10f;
        w.Countries[2].WarExhaustion = 3f;
        Assert.Equal(2, Row(w, 0, "Desgaste de guerra").Winner);
    }

    [Fact]
    public void TheVerdictCountsTheLinesEachSideWins()
    {
        var w = Setup();
        w.Regions[4].ControllerId = 1;
        var rows = Compare.Sheet(w, 0, 1, 2);
        var (left, right, verdict) = Compare.Tally(rows, "Alfa", "Beta");
        Assert.True(left > 0);
        Assert.Equal(left > right ? "Alfa" : "Beta", verdict.Split(' ')[0]);
        Assert.Contains("vantagem", verdict);
    }

    [Fact]
    public void ATiedSheetSaysSo()
    {
        var w = Setup();
        var (_, _, verdict) = Compare.Tally(Compare.Sheet(w, 0, 1, 1), "Alfa", "Alfa");
        Assert.StartsWith("Estão a par", verdict);
    }

    [Fact]
    public void ACountryThatDoesNotExistGivesAnEmptySheet()
    {
        var w = Setup();
        Assert.Empty(Compare.Sheet(w, 1, 1, 999));
    }
}
