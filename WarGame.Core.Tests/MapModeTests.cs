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











    /// <summary>Muda o chão de uma região (Region.Terrain é init: refaz-se a região).</summary>
    private static void Ground(World w, int id, string terrain)
    {
        var r = w.Regions[id];
        var copy = new Region
        {
            Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
            ControllerId = r.ControllerId, Terrain = terrain, River = r.River, Population = r.Population,
            CenterX = r.CenterX, CenterY = r.CenterY,
        };
        foreach (int n in r.Neighbours) copy.Neighbours.Add(n);
        w.Regions[id] = copy;
    }




}
