using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Comboios mercantes: a marinha que carrega o abastecimento por mar e as importações, quem a
/// afunda (o bloqueio) e o que acontece a quem fica sem ela — exército à fome do outro lado do mar e
/// contratos parados no cais.
///
/// Mapa dos testes: linha 1-2-3 (país 1) | 4-5-6 (país 2), mais a ilha 7 do país 2, ligada por mar à
/// região 3 (do país 1) e à região 4 (do país 2) — o mesmo mapa da guerra naval.</summary>
public class ConvoyTests
{
    private const int Island = 7;

    private static World Build(float ships = 10f, float money = 500f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Sea(w);
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values) { c.Warships = ships; c.Money = money; c.IsPlayer = true; }
        w.Register(new NavalMissionSystem());
        w.Register(new ConvoySystem());
        return w;
    }

    /// <summary>Põe mar no mapa linear: a ilha 7 do país 2 a 500 km da região 3 (nossa) e da região 4.</summary>
    private static void Sea(World w)
    {
        w.Regions[Island] = new Region
        {
            Id = Island, Name = "Ilha", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1_000_000, CenterX = 300, CenterY = 500, Coastal = true,
        };
        w.Regions[3] = Coast(w.Regions[3]);
        w.Regions[4] = Coast(w.Regions[4]);
        w.Regions[3].SeaNeighbours[Island] = 500f; w.Regions[Island].SeaNeighbours[3] = 500f;
        w.Regions[4].SeaNeighbours[Island] = 500f; w.Regions[Island].SeaNeighbours[4] = 500f;
    }

    /// <summary>Cópia costeira de uma região (Coastal é init-only).</summary>
    private static Region Coast(Region r)
    {
        var c = new Region
        {
            Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
            ControllerId = r.ControllerId, Terrain = r.Terrain, Population = r.Population,
            CenterX = r.CenterX, CenterY = r.CenterY, Coastal = true,
        };
        foreach (int n in r.Neighbours) c.Neighbours.Add(n);
        return c;
    }

    /// <summary>Mundo de comércio: aço no país 2, o país 1 a comprar, sem mar nem abastecimento pelo meio.</summary>
    private static World Market()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.ResourceDefs["aco"] = new ResourceDef("aco", "Aço", "production_speed", 0.02f, 10f);
        w.ResourceDefs["borracha"] = new ResourceDef("borracha", "Borracha", "research_speed", 0.02f, 10f);
        w.Regions[4].Resources["aco"] = 20f;
        w.Regions[4].Resources["borracha"] = 20f;
        w.Countries[1].Money = 1000f;
        w.Register(new TradeSystem());
        w.Register(new ResourceSystem());
        return w;
    }






    [Fact]
    public void AnArmyAcrossTheSeaOnlyEatsWhatTheConvoysCarry()
    {
        // desembarque nosso na ilha deles, alimentado pelo cais da região 3
        var w = Build();
        w.Regions[3].Buildings["porto"] = 2;
        w.Regions[Island].ControllerId = 1;
        var landed = TestWorld.AddDivision(w, 20, 1, TestWorld.Inf, Island);
        float sea = w.Rule("port_supply_factor", 0.85f);

        new SupplySystem().Tick(w);
        Assert.Equal(sea, landed.Supply, 3);                       // marinha inteira: come tudo o que o cais dá

        w.Countries[1].Convoys = -19.5f;                           // meio comboio para uma divisão
        new SupplySystem().Tick(w);
        Assert.Equal(sea * 0.5f, landed.Supply, 3);

        w.Countries[1].Convoys = -20f;                             // sem um único mercante
        new SupplySystem().Tick(w);
        Assert.Equal(sea * w.Rule("port_overflow_min", 0.35f), landed.Supply, 3);
    }








}
