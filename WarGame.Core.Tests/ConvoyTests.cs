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
    public void TheMerchantMarineRulesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(20f, w.Rule("convoy_base"), 3);
        Assert.Equal(25f, w.Rule("convoy_cost"), 3);
        Assert.Equal(1f, w.Rule("convoy_per_sea_division"), 3);
        Assert.Equal(2f, w.Rule("convoy_per_trade_unit"), 3);
        Assert.Equal(0.25f, w.Rule("convoy_raid_sink"), 3);
    }

    [Fact]
    public void EveryCountryStartsWithAMarineAndCanBuildMore()
    {
        var w = Build(money: 60f);
        Assert.Equal(20f, ConvoySystem.Available(w, 1), 3);        // a marinha de partida, sem comprar nada

        Assert.Null(new BuyConvoyCommand(1).Validate(w));
        new BuyConvoyCommand(1).Execute(w);
        new BuyConvoyCommand(1).Execute(w);
        Assert.Equal(22f, ConvoySystem.Available(w, 1), 3);
        Assert.Equal(10f, w.Countries[1].Money, 3);

        Assert.Contains("faltam pontos", new BuyConvoyCommand(1).Validate(w)!);
    }

    [Fact]
    public void ABlockadeEatsTheEnemyMerchantMarineDayAfterDay()
    {
        var w = Build();
        NavalMissionSystem.Assign(w, 1, Island, "bloqueio", 4f);
        float sink = w.Rule("convoy_raid_sink");

        w.Tick();
        Assert.Equal(-4f * sink, w.Countries[2].Convoys, 3);       // a ilha é deles: pagam eles
        Assert.Equal(0f, w.Countries[1].Convoys, 3);

        TestWorld.Days(w, 40);
        Assert.Equal(0f, ConvoySystem.Available(w, 2), 3);         // a marinha mercante deles foi ao fundo
        Assert.True(w.Countries[2].Convoys >= -w.Rule("convoy_base"), "não se afunda mais do que existia");
    }

    [Fact]
    public void AnEscortedCoastLosesNoMerchants()
    {
        var w = Build();
        NavalMissionSystem.Assign(w, 1, Island, "bloqueio", 3f);
        NavalMissionSystem.Assign(w, 2, Island, "escolta", 3f);    // escolta que iguala: os comboios passam

        w.Tick();
        Assert.Equal(0f, w.Countries[2].Convoys, 3);
        Assert.False(NavalMissionSystem.Blockaded(w, Island));
    }

    [Fact]
    public void OnlyABlockadeRaidsTrade()
    {
        var w = Build();
        NavalMissionSystem.Assign(w, 1, Island, "patrulha", 4f);   // vigiar não afunda mercante nenhum

        w.Tick();
        Assert.Equal(0f, w.Countries[2].Convoys, 3);
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

    [Fact]
    public void WhatTheConvoysCannotCarryIsNotDeliveredAndIsNotPaid()
    {
        var w = Market();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        w.Countries[1].Convoys = -20f;                             // marinha mercante no fundo
        float m1 = w.Countries[1].Money, m2 = w.Countries[2].Money;

        Assert.True(ConvoySystem.Grounded(w, w.TradeDeals[0]));
        Assert.Equal(1, ConvoySystem.GroundedCount(w, 1));
        w.Tick();

        Assert.Single(w.TradeDeals);                               // o contrato não morre: a falta é de navios
        Assert.Equal(m1, w.Countries[1].Money, 3);
        Assert.Equal(m2, w.Countries[2].Money, 3);
        Assert.Equal(0f, TradeSystem.Effective(w, 1, "aco"), 3);   // o aço ficou no cais do vendedor
        Assert.Equal(1f, w.Countries[1].ResourceMult.GetValueOrDefault("production_speed", 1f), 3);
        Assert.Equal(1.2f, w.Countries[2].ResourceMult["production_speed"], 3);   // e o bónus ficou com ele (tecto do recurso)
    }

    [Fact]
    public void TheDealsComeBackTheDayTheShipsDo()
    {
        var w = Market();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        w.Countries[1].Convoys = -20f;
        w.Tick();
        Assert.Equal(0f, TradeSystem.Effective(w, 1, "aco"), 3);

        w.Countries[1].Convoys = 0f;                               // marinha reconstruída
        float m2 = w.Countries[2].Money;
        w.Tick();

        Assert.Equal(3f, TradeSystem.Effective(w, 1, "aco"), 3);
        Assert.True(w.Countries[2].Money > m2, "com navios, o vendedor volta a receber");
    }

    [Fact]
    public void TheArmyFillsTheHoldsBeforeTheTraders()
    {
        var w = Market();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        Assert.False(ConvoySystem.Grounded(w, w.TradeDeals[0]));   // 6 mercantes de 20: cabe à vontade

        w.Countries[1].SeaSupplied = 14;                           // 14 divisões a beber por mar levam 14
        Assert.Equal(14f, ConvoySystem.SupplyNeed(w, 1), 3);
        Assert.False(ConvoySystem.Grounded(w, w.TradeDeals[0]));   // sobram 6, que é o que o contrato pede

        w.Countries[1].SeaSupplied = 15;
        Assert.True(ConvoySystem.Grounded(w, w.TradeDeals[0]));    // mais uma divisão e o aço fica no cais
    }

    [Fact]
    public void TheHoldsFillInAFixedOrderAndOnlyTheLastOnesAreLeftBehind()
    {
        var w = Market();
        new CreateTradeDealCommand(1, 2, "aco", 6f).Execute(w);        // 12 mercantes
        new CreateTradeDealCommand(1, 2, "borracha", 5f).Execute(w);   // mais 10: não cabem os dois em 20

        var aco = w.TradeDeals.Single(d => d.ResourceId == "aco");
        var borracha = w.TradeDeals.Single(d => d.ResourceId == "borracha");
        Assert.False(ConvoySystem.Grounded(w, aco));                   // a ordem é vendedor, depois recurso
        Assert.True(ConvoySystem.Grounded(w, borracha));
        Assert.Equal(1, ConvoySystem.GroundedCount(w, 1));

        w.Countries[1].Convoys = 10f;                                  // mais dez mercantes e passam os dois
        Assert.False(ConvoySystem.Grounded(w, borracha));
    }

    [Fact]
    public void TheMerchantMarineSurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Convoys = -4.5f;
        w.Countries[2].Convoys = 7f;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(-4.5f, w2.Countries[1].Convoys, 3);
        Assert.Equal(7f, w2.Countries[2].Convoys, 3);
        Assert.Equal(15.5f, ConvoySystem.Available(w2, 1), 3);
    }
}
