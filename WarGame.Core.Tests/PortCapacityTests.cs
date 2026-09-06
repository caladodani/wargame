using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Capacidade dos portos. Mesmo mapa do PortSupplyTests (ilha 7 do país 2, a 500 km da região 3
/// do país 1), mas agora o que se mede não é se o mar abastece — é quanto. Um cais tem um limite, e
/// desembarcar mais divisões do que ele carrega estrangula-as a todas.
///
/// O empilhamento (supply_stack) é desligado nestes testes: interessa o cais, não a lotação da região.</summary>
public class PortCapacityTests
{
    private const int Island = 7;

    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[3] = Coast(w.Regions[3]);
        w.Regions[Island] = new Region
        {
            Id = Island, Name = "Ilha", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1_000_000, CenterX = 300, CenterY = 500, Coastal = true
        };
        w.Regions[3].SeaNeighbours[Island] = 500f;
        w.Regions[Island].SeaNeighbours[3] = 500f;
        w.Regions[3].Buildings["porto"] = 1;
        w.Rules["supply_stack"] = 1e6f;              // a lotação da região não entra nesta conta
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

    /// <summary>Desembarque consumado com n divisões nossas na ilha.</summary>
    private static List<Division> Land(World w, int n)
    {
        w.Regions[Island].ControllerId = 1;
        return Enumerable.Range(0, n).Select(i => TestWorld.AddDivision(w, 20 + i, 1, TestWorld.Inf, Island)).ToList();
    }

    private static float Cap(World w) => w.Rule("port_capacity_per_level", 6f);

    [Fact]
    public void TheCapacityComesFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.Rule("port_capacity_per_level") > 0f);
        Assert.True(w.Rule("port_overflow_min") > 0f);
        Assert.True(w.Rule("port_overflow_min") < 1f);
    }

    [Fact]
    public void WithinTheQuay_NothingIsLost()
    {
        var w = Build();
        var landed = Land(w, (int)Cap(w));
        new SupplySystem().Tick(w);

        float sea = w.Rule("port_supply_factor", 0.85f);
        Assert.All(landed, d => Assert.Equal(sea, d.Supply, 3));
    }

    [Fact]
    public void OverTheQuay_EverybodyOnTheOtherSideSuffers()
    {
        var w = Build();
        int n = (int)Cap(w) * 2;
        var landed = Land(w, n);
        new SupplySystem().Tick(w);

        float sea = w.Rule("port_supply_factor", 0.85f);
        float expected = sea * (Cap(w) / n);
        Assert.All(landed, d => Assert.Equal(expected, d.Supply, 3));
        Assert.True(landed[0].Supply < sea);
    }

    [Fact]
    public void ABiggerPortCarriesMore()
    {
        var w = Build();
        Land(w, (int)Cap(w) + 1);
        new SupplySystem().Tick(w);
        float tight = w.Divisions[20].Supply;

        w.Regions[3].Buildings["porto"] = 2;         // segundo nível de cais
        new SupplySystem().Tick(w);

        Assert.True(w.Divisions[20].Supply > tight);
        Assert.Equal(w.Rule("port_supply_factor", 0.85f), w.Divisions[20].Supply, 3);
    }

    [Fact]
    public void TheStrangleHasAFloor()
    {
        var w = Build();
        Land(w, (int)Cap(w) * 100);
        new SupplySystem().Tick(w);

        float floor = w.Rule("port_supply_factor", 0.85f) * w.Rule("port_overflow_min", 0.35f);
        Assert.Equal(floor, w.Divisions[20].Supply, 3);
    }

    [Fact]
    public void WhoIsSuppliedByLandDoesNotPayForTheQuay()
    {
        var w = Build();
        Land(w, (int)Cap(w) * 3);
        var home = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);   // em casa, por terra
        new SupplySystem().Tick(w);

        Assert.Equal(1f, home.Supply, 3);
    }

    [Fact]
    public void TheCountrySheetKnowsTheQuayAndTheLoad()
    {
        var w = Build();
        int n = (int)Cap(w) + 2;
        Land(w, n);
        new SupplySystem().Tick(w);

        Assert.Equal(Cap(w), w.Countries[1].PortCapacity, 3);
        Assert.Equal(n, w.Countries[1].SeaSupplied);
        Assert.Equal(0f, w.Countries[2].PortCapacity, 3);      // o país 2 não tem cais nenhum
        Assert.Equal(0, w.Countries[2].SeaSupplied);
    }

    [Fact]
    public void WithoutAnybodyOverseas_TheQuayIsIdle()
    {
        var w = Build();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        new SupplySystem().Tick(w);

        Assert.Equal(Cap(w), w.Countries[1].PortCapacity, 3);   // o cais existe
        Assert.Equal(0, w.Countries[1].SeaSupplied);            // mas não carrega ninguém
    }
}
