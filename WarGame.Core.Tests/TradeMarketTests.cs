using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Mercado de recursos e tratados: o preço move-se com a escassez do vendedor e com a guerra dele,
/// e um contrato trava esse preço até ao prazo acabar.</summary>
public class TradeMarketTests
{
    private static World Setup(float money = 400f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        w.ResourceDefs["aco"] = new ResourceDef("aco", "Aço", "production_speed", 0.02f, 10f);
        w.Regions[4].Resources["aco"] = 10f;              // país 2 é o vendedor
        foreach (var c in w.Countries.Values) c.Money = money;
        w.Register(new TradeSystem());
        return w;
    }

    [Fact]
    public void TheMarketRulesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(1.5f, w.Rule("trade_price_scarcity"), 3);
        Assert.Equal(1.4f, w.Rule("trade_war_premium"), 3);
        Assert.Equal(1f, w.Rule("trade_price_min"), 3);
        Assert.Equal(12f, w.Rule("trade_price_max"), 3);
        Assert.Equal(180f, w.Rule("trade_deal_days"), 3);
        Assert.Equal(10f, w.Rule("trade_deal_deposit_days"), 3);
    }

    [Fact]
    public void WhatIsAlreadyPromisedMakesTheRestExpensive()
    {
        var w = Setup();
        float table = w.Rule("trade_price_per_unit");
        Assert.Equal(table, TradeSystem.Price(w, 2, "aco"), 3);      // nada vendido: preço de tabela

        new CreateTradeDealCommand(1, 2, "aco", 5f).Execute(w);      // metade dos depósitos prometida
        Assert.Equal(table * (1f + 0.5f * w.Rule("trade_price_scarcity")), TradeSystem.Price(w, 2, "aco"), 3);
    }

    [Fact]
    public void AWarringSellerChargesAPremium()
    {
        var w = Setup();
        float peace = TradeSystem.Price(w, 2, "aco");
        w.StartWar(2, 3);
        Assert.Equal(peace * w.Rule("trade_war_premium"), TradeSystem.Price(w, 2, "aco"), 3);
    }

    [Fact]
    public void ThePriceNeverLeavesTheFloorOrTheCeiling()
    {
        var w = Setup();
        w.Rules["trade_price_scarcity"] = 100f;
        new CreateTradeDealCommand(1, 2, "aco", 9f).Execute(w);
        Assert.Equal(w.Rule("trade_price_max"), TradeSystem.Price(w, 2, "aco"), 3);

        w.Rules["trade_price_per_unit"] = 0.01f;
        w.Rules["trade_price_scarcity"] = 0f;
        Assert.Equal(w.Rule("trade_price_min"), TradeSystem.Price(w, 2, "aco"), 3);
    }

    [Fact]
    public void TheContractLocksTheDayPriceEvenWhenTheMarketMoves()
    {
        var w = Setup();
        float signed = TradeSystem.Price(w, 2, "aco");
        new CreateTradeDealCommand(1, 2, "aco", 2f).Execute(w);
        var deal = Assert.Single(w.TradeDeals);
        Assert.Equal(signed, deal.PricePerUnit, 3);

        w.StartWar(2, 3);                                            // o mercado sobe para quem vier a seguir
        Assert.True(TradeSystem.Price(w, 2, "aco") > signed);
        Assert.Equal(signed, TradeSystem.Paid(w, deal), 3);           // o contrato assinado não sobe

        float buyer = w.Countries[1].Money, seller = w.Countries[2].Money;
        w.Tick();
        Assert.Equal(buyer - 2f * signed, w.Countries[1].Money, 3);
        Assert.Equal(seller + 2f * signed, w.Countries[2].Money, 3);
    }

    [Fact]
    public void ADealWithADateEndsOnThatDate()
    {
        var w = Setup();
        int day = w.Clock.Day;
        float money = w.Countries[1].Money;
        new CreateTradeDealCommand(1, 2, "aco", 1f, Days: 5).Execute(w);
        float price = w.TradeDeals[0].PricePerUnit;
        Assert.Equal(day + 5, w.TradeDeals[0].UntilDay);
        Assert.Equal(5, TradeSystem.DaysLeft(w, w.TradeDeals[0]));

        TestWorld.Days(w, 4);
        Assert.Single(w.TradeDeals);
        Assert.Equal(1, TradeSystem.DaysLeft(w, w.TradeDeals[0]));

        w.Tick();                                                     // o quinto dia ainda se entrega
        Assert.Single(w.TradeDeals);
        Assert.Equal(0, TradeSystem.DaysLeft(w, w.TradeDeals[0]));

        w.Tick();
        Assert.Empty(w.TradeDeals);                                   // cumprido o prazo, o contrato acaba
        Assert.Equal(money - 5f * price, w.Countries[1].Money, 3);    // pagaram-se cinco dias, nem mais um
    }

    [Fact]
    public void ALongContractAsksForACoffer()
    {
        var w = Setup(money: 20f);
        Assert.Null(new CreateTradeDealCommand(1, 2, "aco", 1f).Validate(w));            // um dia, chega
        Assert.Contains("no cofre", new CreateTradeDealCommand(1, 2, "aco", 4f, Days: 90).Validate(w)!);

        w.Countries[1].Money = 400f;
        Assert.Null(new CreateTradeDealCommand(1, 2, "aco", 4f, Days: 90).Validate(w));
    }

    [Fact]
    public void TheSameResourceIsNotBoughtTwiceFromTheSameSeller()
    {
        var w = Setup();
        new CreateTradeDealCommand(1, 2, "aco", 2f).Execute(w);
        Assert.Contains("já há contrato", new CreateTradeDealCommand(1, 2, "aco", 1f).Validate(w)!);
        Assert.Contains("prazo inválido", new CreateTradeDealCommand(1, 2, "aco", 1f, Days: -3).Validate(w)!);
    }

    [Fact]
    public void ATreatySurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.ResourceDefs["aco"] = new ResourceDef("aco", "Aço", "production_speed", 0.02f, 10f);
        w.Regions[4].Resources["aco"] = 10f;
        w.Countries[1].Money = 400f;
        new CreateTradeDealCommand(1, 2, "aco", 3f, Days: 60).Execute(w);
        var signed = w.TradeDeals[0];

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = Assert.Single(w2.TradeDeals);
        Assert.Equal(signed.PricePerUnit, back.PricePerUnit, 3);
        Assert.Equal(signed.UntilDay, back.UntilDay);
        Assert.Equal(3f, back.Units, 3);
    }
}
