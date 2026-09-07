using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Guerra naval por mar de costa: as esquadras destacadas saem do pool, custam estadia, afundam-se
/// umas às outras no mar disputado, fecham o cais de quem bloqueiam e tiram a costa do nevoeiro.
///
/// Mapa dos testes: linha 1-2-3 (país 1) | 4-5-6 (país 2), mais a ilha 7 do país 2, ligada por mar à
/// região 3 (do país 1) e à região 4 (do país 2).</summary>
public class NavalMissionTests
{
    private const int Island = 7;

    private static World Build(float ships = 10f, float money = 500f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Sea(w);
        w.StartWar(1, 2);
        // os dois países de mão humana: a IA naval tem um teste só para ela e não anda a engrossar estes
        foreach (var c in w.Countries.Values) { c.Warships = ships; c.Money = money; c.IsPlayer = true; }
        w.Register(new NavalMissionSystem());
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

    [Fact]
    public void TheMissionsAndTheirPricesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal("blockade", w.NavalMissionDefs["bloqueio"].Effect);
        Assert.Equal("escort", w.NavalMissionDefs["escolta"].Effect);
        Assert.Equal("patrol", w.NavalMissionDefs["patrulha"].Effect);
        Assert.Equal(90f, w.Rule("naval_ship_cost"), 3);
        Assert.Equal(0.8f, w.Rule("naval_mission_upkeep"), 3);
        Assert.Equal(0.05f, w.Rule("naval_battle_loss"), 3);
        Assert.Equal(1500f, w.Rule("naval_range_km"), 3);
        Assert.Equal(1f, w.Rule("naval_mission_min_ships"), 3);
    }

    [Fact]
    public void AWarshipCostsWhatTheTableSays()
    {
        var w = Build(ships: 0f, money: 200f);
        Assert.Null(new BuyWarshipCommand(1).Validate(w));
        new BuyWarshipCommand(1).Execute(w);
        Assert.Equal(1f, w.Countries[1].Warships, 3);
        Assert.Equal(200f - w.Rule("naval_ship_cost"), w.Countries[1].Money, 3);

        w.Countries[1].Money = 10f;
        Assert.Contains("faltam pontos", new BuyWarshipCommand(1).Validate(w)!);
    }

    [Fact]
    public void ShipsAtSeaLeaveTheHarbour()
    {
        var w = Build();
        Assert.Equal(10f, NavalMissionSystem.Free(w, 1), 3);

        new AssignNavalMissionCommand(1, Island, "bloqueio", 4f).Execute(w);
        Assert.Equal(4f, NavalMissionSystem.Assigned(w, 1), 3);
        Assert.Equal(6f, NavalMissionSystem.Free(w, 1), 3);

        new AssignNavalMissionCommand(1, Island, "bloqueio", 2f).Execute(w);   // o mesmo mar engrossa
        Assert.Single(w.NavalMissions);
        Assert.Equal(6f, w.NavalMissions[0].Ships, 3);

        Assert.Null(new RecallNavalMissionCommand(1, Island).Validate(w));
        new RecallNavalMissionCommand(1, Island).Execute(w);
        Assert.Empty(w.NavalMissions);
        Assert.Equal(10f, NavalMissionSystem.Free(w, 1), 3);                   // voltam todos ao porto
        Assert.Contains("não há esquadra", new RecallNavalMissionCommand(1, Island).Validate(w)!);
    }

    [Fact]
    public void ChangingTheOrdersKeepsTheShipsInTheSameSea()
    {
        var w = Build();
        new AssignNavalMissionCommand(1, Island, "bloqueio", 3f).Execute(w);
        new AssignNavalMissionCommand(1, Island, "patrulha", 2f).Execute(w);

        var m = Assert.Single(w.NavalMissions);
        Assert.Equal("patrulha", m.MissionId);
        Assert.Equal(5f, m.Ships, 3);
    }

    [Fact]
    public void TheSeaHasToBeReachableAndTheEnemyHasToBeTheEnemy()
    {
        var w = Build();
        Assert.Null(new AssignNavalMissionCommand(1, Island, "bloqueio", 2f).Validate(w));    // rota de 500 km da nossa 3
        Assert.Contains("poucos navios", new AssignNavalMissionCommand(1, Island, "bloqueio", 0.5f).Validate(w)!);
        Assert.Contains("navios no porto", new AssignNavalMissionCommand(1, Island, "bloqueio", 40f).Validate(w)!);
        Assert.Contains("não tem mar", new AssignNavalMissionCommand(1, 5, "bloqueio", 2f).Validate(w)!);
        Assert.Contains("própria costa", new AssignNavalMissionCommand(1, 3, "bloqueio", 2f).Validate(w)!);
        Assert.Contains("costa nossa", new AssignNavalMissionCommand(1, Island, "escolta", 2f).Validate(w)!);
        Assert.Null(new AssignNavalMissionCommand(1, 3, "escolta", 2f).Validate(w));          // a nossa costa escolta-se

        w.Rules["naval_range_km"] = 100f;                                                    // a rota passa a ser longa demais
        Assert.Contains("sem rota marítima", new AssignNavalMissionCommand(1, Island, "bloqueio", 2f).Validate(w)!);

        w.Rules["naval_range_km"] = 1500f;
        w.EndWar(1, 2);
        Assert.Contains("não estamos em guerra", new AssignNavalMissionCommand(1, Island, "bloqueio", 2f).Validate(w)!);
    }

    [Fact]
    public void EveryDayAtSeaIsPaidFor()
    {
        var w = Build(money: 10f);
        new AssignNavalMissionCommand(1, Island, "bloqueio", 5f).Execute(w);
        float upkeep = w.Rule("naval_mission_upkeep");

        w.Tick();
        Assert.Equal(10f - 5f * upkeep, w.Countries[1].Money, 3);

        w.Countries[1].Money = 0.5f;
        w.Tick();
        Assert.Empty(w.NavalMissions);                                        // sem cofre, a esquadra volta ao porto
        Assert.Equal(0.5f, w.Countries[1].Money, 3);
    }

    [Fact]
    public void ContestedSeasCostShipsToBothSides()
    {
        var w = Build();
        new AssignNavalMissionCommand(1, Island, "bloqueio", 6f).Execute(w);
        new AssignNavalMissionCommand(2, Island, "escolta", 2f).Execute(w);
        float loss = w.Rule("naval_battle_loss") * 2f;                        // pelo lado mais fraco

        w.Tick();
        Assert.Equal(6f - loss, w.NavalMissions.Single(m => m.CountryId == 1).Ships, 3);
        Assert.Equal(2f - loss, w.NavalMissions.Single(m => m.CountryId == 2).Ships, 3);
        Assert.Equal(10f - loss, w.Countries[1].Warships, 3);                 // os afundados saem do pool nacional
        Assert.Equal(10f - loss, w.Countries[2].Warships, 3);
    }

    [Fact]
    public void ABlockadeShutsThePortAndTheEscortReopensIt()
    {
        var w = Build();
        Assert.False(NavalMissionSystem.Blockaded(w, Island));

        NavalMissionSystem.Assign(w, 1, Island, "bloqueio", 3f);
        Assert.True(NavalMissionSystem.Blockaded(w, Island));

        NavalMissionSystem.Assign(w, 2, Island, "escolta", 3f);               // escolta igual: o mar continua aberto
        Assert.False(NavalMissionSystem.Blockaded(w, Island));

        NavalMissionSystem.Assign(w, 1, Island, "bloqueio", 1f);              // mais um navio e volta a fechar
        Assert.True(NavalMissionSystem.Blockaded(w, Island));
    }

    [Fact]
    public void TheBlockadedBeachheadStopsBeingSupplied()
    {
        // desembarque nosso na ilha deles: quem a alimenta é o cais da região 3, por 500 km de mar
        var w = Build();
        w.Regions[3].Buildings["porto"] = 2;
        w.Regions[Island].ControllerId = 1;
        var landed = TestWorld.AddDivision(w, 20, 1, TestWorld.Inf, Island);

        new SupplySystem().Tick(w);
        Assert.Equal(w.Rule("port_supply_factor", 0.85f), landed.Supply, 3);   // come pelo mar, mas come

        NavalMissionSystem.Assign(w, 2, Island, "bloqueio", 3f);
        new SupplySystem().Tick(w);
        Assert.Equal(w.Rule("supply_pocket", 0.5f), landed.Supply, 3);         // rota cortada: bolsa

        NavalMissionSystem.Assign(w, 1, Island, "escolta", 3f);                // a escolta abre outra vez a rota
        new SupplySystem().Tick(w);
        Assert.Equal(w.Rule("port_supply_factor", 0.85f), landed.Supply, 3);
    }

    [Fact]
    public void ABlockadedPortCarriesNothing()
    {
        // o cais deles, na costa deles, com a nossa esquadra à porta: deixa de contar cais nenhum
        var w = Build();
        w.Regions[4].Buildings["porto"] = 2;
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf, 4);

        new SupplySystem().Tick(w);
        Assert.True(w.Countries[2].PortCapacity > 0f);

        NavalMissionSystem.Assign(w, 1, 4, "bloqueio", 3f);
        new SupplySystem().Tick(w);
        Assert.Equal(0f, w.Countries[2].PortCapacity, 3);
    }

    [Fact]
    public void APatrolLiftsTheFogOverThatCoast()
    {
        var w = Build();
        w.Rules["fog_of_war"] = 1f;
        var far = new Region
        {
            Id = 8, Name = "Longe", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1000, CenterX = 9000, CenterY = 9000, Coastal = true,
        };
        w.Regions[8] = far;
        far.SeaNeighbours[Island] = 300f; w.Regions[Island].SeaNeighbours[8] = 300f;
        Assert.False(Vision.Sees(w, 1, far));

        NavalMissionSystem.Assign(w, 1, 8, "patrulha", 2f);
        Assert.True(Vision.Sees(w, 1, far));                                  // a patrulha vê a costa que vigia
    }

    [Fact]
    public void TheAiBlockadesTheBestEnemyCoastAndKeepsAReserve()
    {
        var w = Build(ships: 5f);
        w.Countries[2].IsPlayer = false;                                      // só a IA do país 2 despacha

        w.Tick();
        var m = Assert.Single(w.NavalMissions);
        Assert.Equal(2, m.CountryId);
        Assert.Equal(3, m.RegionId);                                          // a nossa costa, a única ao alcance dela
        Assert.Equal("bloqueio", m.MissionId);
        Assert.Equal(5f - w.Rule("naval_ai_reserve"), m.Ships, 3);
    }

    [Fact]
    public void TheFleetSurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Sea(w);
        w.StartWar(1, 2);
        w.Countries[1].Warships = 6f; w.Countries[1].Money = 100f;
        NavalMissionSystem.Assign(w, 1, Island, "bloqueio", 3f);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = Assert.Single(w2.NavalMissions);
        Assert.Equal(1, back.CountryId);
        Assert.Equal(Island, back.RegionId);
        Assert.Equal("bloqueio", back.MissionId);
        Assert.Equal(3f, back.Ships, 3);
        Assert.Equal(6f, w2.Countries[1].Warships, 3);
    }
}
