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
    public void Beachhead_WithoutPort_IsAPocket()
    {
        var w = Build();
        var d = Land(w);
        new SupplySystem().Tick(w);
        Assert.Equal(w.Rule("supply_pocket", 0.5f), d.Supply, 3);
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

    [Fact]
    public void Port_DoesNotReachBeyondItsRange()
    {
        var w = Build(km: 5000f);            // travessia maior do que o alcance do porto
        var d = Land(w);
        w.Regions[3].Buildings["porto"] = 1;
        new SupplySystem().Tick(w);
        Assert.Equal(w.Rule("supply_pocket", 0.5f), d.Supply, 3);
    }

    [Fact]
    public void SecondLevel_DoublesTheReach()
    {
        float reach = Build().BuildingDefs["porto"].SupplyRange;
        var w = Build(km: reach * 1.5f);
        var d = Land(w);
        w.Regions[3].Buildings["porto"] = 2;
        new SupplySystem().Tick(w);
        Assert.Equal(w.Rule("port_supply_factor", 0.85f), d.Supply, 3);
    }

    [Fact]
    public void ChainContinuesInlandFromTheBeachhead()
    {
        var w = Build();
        // 8 fica atrás da ilha, por terra: cai também na rede quando o porto alcança a ilha
        w.Regions[8] = new Region { Id = 8, Name = "Interior", OwnerId = 2, InitialOwnerId = 2, ControllerId = 1, Terrain = "plain", Population = 500_000, CenterX = 300, CenterY = 600 };
        w.Regions[8].Neighbours.Add(Island); w.Regions[Island].Neighbours.Add(8);
        Land(w);
        var inland = TestWorld.AddDivision(w, 21, 1, TestWorld.Inf, 8);
        w.Regions[3].Buildings["porto"] = 1;
        new SupplySystem().Tick(w);
        Assert.Equal(w.Rule("port_supply_factor", 0.85f), inland.Supply, 3);
    }

    [Fact]
    public void EnemyPort_DoesNotSupplyUs()
    {
        var w = Build();
        var d = Land(w);
        w.Regions[6].Buildings["porto"] = 1;         // porto deles, no continente deles
        w.Regions[6].SeaNeighbours[Island] = 100f;
        new SupplySystem().Tick(w);
        Assert.Equal(w.Rule("supply_pocket", 0.5f), d.Supply, 3);
    }

    [Fact]
    public void Port_IsCoastalOnly()
    {
        var w = Build();
        w.Countries[1].Money = 1000f;
        Assert.Equal("só na costa", new BuildBuildingCommand(1, 2, "porto").Validate(w));
        Assert.Null(new BuildBuildingCommand(1, 3, "porto").Validate(w));
    }

    [Fact]
    public void Ai_BuildsAPortForItsStarvingOverseasTroops()
    {
        var w = Build();
        Land(w);
        w.Countries[1].Money = 1000f;
        new SupplySystem().Tick(w);                  // a divisão da ilha fica em bolsa
        new AiSystem().Tick(w);
        Assert.Equal("porto", w.Regions[3].Project);
    }

    [Fact]
    public void PortLevel_SurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[3].Buildings["porto"] = 2;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(2, w2.Regions[3].Buildings.GetValueOrDefault("porto"));
        Assert.True(w2.BuildingDefs["porto"].Coastal);
        Assert.True(w2.BuildingDefs["porto"].SupplyRange > 0f);
    }
}
