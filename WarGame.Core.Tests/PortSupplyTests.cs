using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Abastecimento por mar. Mapa: linha 1-2-3 (país 1) | 4-5-6 (país 2), mais a ilha 7,
/// do país 2, a 500 km da região 3 por mar. Sem porto, quem desembarca na ilha fica em bolsa.</summary>
public class PortSupplyTests
{
    private const int Island = 7;

    /// <summary>Mundo com a ilha 7 (país 2) ligada por mar à região 3 (país 1) e a região 3 costeira.</summary>
    private static World Build(float km = 500f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[3] = Coast(w.Regions[3]);
        w.Regions[Island] = new Region
        {
            Id = Island, Name = "Ilha", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1_000_000, CenterX = 300, CenterY = 500, Coastal = true
        };
        w.Regions[3].SeaNeighbours[Island] = km;
        w.Regions[Island].SeaNeighbours[3] = km;
        return w;
    }

    /// <summary>Cópia costeira de uma região (Coastal é init-only).</summary>
    private static Region Coast(Region r)
    {
        var c = new Region
        {
            Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
            ControllerId = r.ControllerId, Terrain = r.Terrain, Population = r.Population,
            CenterX = r.CenterX, CenterY = r.CenterY, Coastal = true
        };
        foreach (int n in r.Neighbours) c.Neighbours.Add(n);
        return c;
    }

    /// <summary>Desembarque consumado: a ilha passa a controlada por nós, com uma divisão lá.</summary>
    private static Division Land(World w)
    {
        w.Regions[Island].ControllerId = 1;
        return TestWorld.AddDivision(w, 20, 1, TestWorld.Inf, Island);
    }


    [Fact]
    public void Port_SuppliesTheBeachhead_ButWorseThanByLand()
    {
        var w = Build();
        var d = Land(w);
        w.Regions[3].Buildings["porto"] = 1;
        new SupplySystem().Tick(w);

        float sea = w.Rule("port_supply_factor", 0.85f);
        Assert.Equal(sea, d.Supply, 3);
        Assert.True(sea < 1f && sea > w.Rule("supply_pocket", 0.5f));
    }







}
