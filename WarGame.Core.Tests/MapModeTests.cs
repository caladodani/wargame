using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Modos de mapa (HoI4: map modes): o mesmo território pintado por abastecimento, resistência,
/// indústria ou população — o que se sabe, o que o nevoeiro tapa e como a escala se estica.</summary>
public class MapModeTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true;
        return w;
    }

    [Fact]
    public void TheModesComeFromTheDatabaseInOrder()
    {
        var w = Setup();
        var modes = MapModes.All(w);
        Assert.True(modes.Count >= 5);
        Assert.Equal(MapModes.Political, modes[0].Id);
        Assert.Equal("owner", modes[0].Metric);
        var supply = modes.First(m => m.Metric == "supply");
        Assert.Equal("a seco", supply.Low);
        Assert.Equal("cheio", supply.High);
        Assert.NotEmpty(supply.Icon);
    }

    [Fact]
    public void ThePoliticalMapPaintsNothingByItself()
    {
        var w = Setup();
        Assert.Empty(MapModes.Shades(w, 1, "owner"));
        Assert.Empty(MapModes.Shades(w, 1, "não existe"));
    }

    [Fact]
    public void SupplyComesFromTheTroopsOnTheGround()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 1).Supply = 1f;
        TestWorld.AddDivision(w, 11, 1, TestWorld.Inf, 2).Supply = 0.2f;

        var shades = MapModes.Shades(w, 1, "supply");
        Assert.Equal(1f, shades[1]);                       // a melhor abastecida fica no topo da escala
        Assert.True(shades[2] < 0.5f);
        Assert.False(shades.ContainsKey(3));               // região sem tropa: sem resposta
    }

    [Fact]
    public void TwoDivisionsInARegionAverageOut()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 1).Supply = 1f;
        TestWorld.AddDivision(w, 11, 1, TestWorld.Inf, 1).Supply = 0f;
        Assert.Equal(0.5f, MapModes.Value(w, 1, w.Regions[1], "supply"));
    }

    [Fact]
    public void TheirSupplyIsHiddenByTheFog()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 20, 2, TestWorld.Inf2, 6).Supply = 0.3f;   // longe da nossa fronteira
        Assert.Null(MapModes.Value(w, 1, w.Regions[6], "supply"));
        Assert.Equal("sem tropa nossa à vista", MapModes.Text(w, 1, w.Regions[6], "supply"));

        w.Intel[(1, 2)] = w.Clock.Day + 10;                                  // com espionagem já se vê
        Assert.Equal(0.3f, MapModes.Value(w, 1, w.Regions[6], "supply")!.Value, 3);
    }

    [Fact]
    public void AnOmniscientViewerSeesEveryTroop()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 20, 2, TestWorld.Inf2, 6).Supply = 0.3f;
        Assert.Equal(0.3f, MapModes.Value(w, 0, w.Regions[6], "supply")!.Value, 3);
    }

    [Fact]
    public void ResistanceOnlyExistsOnOccupiedGround()
    {
        var w = Setup();
        w.Regions[1].Resistance = 0.7f;                    // terra do próprio dono: não conta
        Assert.Null(MapModes.Value(w, 1, w.Regions[1], "resistance"));
        Assert.Contains("sem resistência", MapModes.Text(w, 1, w.Regions[1], "resistance"));

        w.Regions[4].ControllerId = 1; w.Regions[4].Resistance = 0.4f;
        Assert.Equal(0.4f, MapModes.Value(w, 1, w.Regions[4], "resistance"));
        Assert.Equal("resistência 40%", MapModes.Text(w, 1, w.Regions[4], "resistance"));
    }

    [Fact]
    public void IndustryCountsBuildingsAndRoads()
    {
        var w = Setup();
        w.Regions[1].Buildings["fabrica"] = 2;
        w.Regions[1].Infrastructure = 1.5f;
        Assert.Equal(3.5f, MapModes.Value(w, 1, w.Regions[1], "industry"));
        Assert.Contains("indústria 3.50", MapModes.Text(w, 1, w.Regions[1], "industry"));
    }

    [Fact]
    public void TheScaleStretchesBetweenTheEmptiestAndTheFullest()
    {
        var w = Setup();
        w.Regions[1].Buildings["fabrica"] = 4;
        var shades = MapModes.Shades(w, 1, "industry");
        Assert.Equal(1f, shades[1]);
        Assert.All(w.Regions.Values.Where(r => r.Id != 1), r => Assert.True(shades[r.Id] < 0.5f));
    }

    [Fact]
    public void PopulationIsCountedInMillions()
    {
        var w = Setup();
        Assert.Equal(10f, MapModes.Value(w, 1, w.Regions[1], "population"));
        Assert.Equal("10.0 M habitantes", MapModes.Text(w, 1, w.Regions[1], "population"));
    }

    [Fact]
    public void AFlatWorldPaintsEverythingTheSame()
    {
        var w = Setup();
        var shades = MapModes.Shades(w, 1, "population");   // todas com a mesma população
        Assert.Equal(w.Regions.Count, shades.Count);
        Assert.All(shades.Values, v => Assert.Equal(1f, v));
    }

    [Fact]
    public void ShadesNeverLeaveTheZeroOneRange()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 1).Supply = 1.4f;   // sobreabastecida
        TestWorld.AddDivision(w, 11, 1, TestWorld.Inf, 2).Supply = -0.2f;
        Assert.All(MapModes.Shades(w, 1, "supply").Values, v => Assert.InRange(v, 0f, 1f));
    }
}
