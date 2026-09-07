using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Fábricas dedicadas por encomenda: cada linha de montagem vale um dia de trabalho por dia, e o
/// que se concentra numa encomenda tira-se ao resto da fila.</summary>
public class ProductionFactoriesTests
{
    private static World Build(float money = 1000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ProductionSystem());
        w.Countries[1].Money = money;
        w.Countries[1].Manpower = 1e9f;
        return w;
    }

    private static void Order(World w, int template) => new BuildDivisionCommand(1, template).Execute(w);
    private static float Daily(World w, int template) => w.TemplateCost(template) / w.Rule("build_min_days");

    [Fact]
    public void AnOrderIsBornWithOneFactoryAndWorksAsItAlwaysDid()
    {
        var w = Build();
        Order(w, TestWorld.Inf);
        Assert.Equal(1, w.Countries[1].Queue[0].Factories);
        w.Tick();
        Assert.Equal(Daily(w, TestWorld.Inf), w.Countries[1].Queue[0].Progress, 1e-4f);
    }

    [Fact]
    public void DedicatedFactoriesMultiplyTheDaysWork()
    {
        var w = Build();
        Assert.Equal(2, Industry.Of(w, 1).Military);
        Order(w, TestWorld.Armor);

        var cmd = new SetOrderFactoriesCommand(1, 0, 2);
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);
        w.Tick();
        Assert.Equal(Daily(w, TestWorld.Armor) * 2f, w.Countries[1].Queue[0].Progress, 1e-4f);
    }

    [Fact]
    public void WhatIsGivenToOneOrderIsTakenFromTheRestOfTheQueue()
    {
        var w = Build();
        Order(w, TestWorld.Inf); Order(w, TestWorld.Inf);
        w.Tick();
        Assert.True(w.Countries[1].Queue[1].Progress > 0f, "com uma fábrica cada, as duas andam");

        var w2 = Build();
        Order(w2, TestWorld.Inf); Order(w2, TestWorld.Inf);
        new SetOrderFactoriesCommand(1, 0, 2).Execute(w2);      // as duas fábricas na primeira
        w2.Tick();
        Assert.Equal(Daily(w2, TestWorld.Inf) * 2f, w2.Countries[1].Queue[0].Progress, 1e-4f);
        Assert.Equal(0f, w2.Countries[1].Queue[1].Progress);    // a segunda fica sem fábrica nenhuma
    }

    [Fact]
    public void NoOrderWorksMoreFactoriesThanTheCountryHasOpen()
    {
        var w = Build();
        Order(w, TestWorld.Inf);
        w.Countries[1].Queue[0].Factories = 9;                  // pedido de fora do que o país tem
        w.Tick();
        Assert.Equal(Daily(w, TestWorld.Inf) * 2f, w.Countries[1].Queue[0].Progress, 1e-4f);
    }

    [Fact]
    public void TheBenchLightsUpOneLampPerDedicatedFactory()
    {
        var w = Build();
        Order(w, TestWorld.Inf);
        Assert.Equal(1, Industry.Of(w, 1).MilitaryBusy);
        new SetOrderFactoriesCommand(1, 0, 2).Execute(w);
        Assert.Equal(2, Industry.Of(w, 1).MilitaryBusy);
        Assert.Equal(0, Industry.Of(w, 1).FreeMilitary);
    }

    [Fact]
    public void SerialProductionKeepsTheLinesItWasGiven()
    {
        var w = Build();
        Order(w, TestWorld.Inf);
        new SetOrderFactoriesCommand(1, 0, 2).Execute(w);
        new SetProductionRepeatCommand(1, 0, true).Execute(w);

        TestWorld.Days(w, (int)w.Rule("build_min_days") / 2);    // com duas fábricas sai em metade dos dias
        Assert.Single(w.Divisions);
        var again = Assert.Single(w.Countries[1].Queue);
        Assert.True(again.Repeat);
        Assert.Equal(2, again.Factories);                        // a série herda a fábrica que já lá estava
    }

    [Fact]
    public void TheCeilingIsWhatTheCountryHasAndTheRuleAllows()
    {
        var w = Build();
        Order(w, TestWorld.Inf);
        Assert.Equal(8f, w.Rule("order_factories_max"));
        Assert.Equal(2, SetOrderFactoriesCommand.Cap(w, 1));     // o país só tem duas fábricas militares

        Assert.Equal("Só podes dedicar 2 fábricas a uma encomenda", new SetOrderFactoriesCommand(1, 0, 3).Validate(w));
        Assert.Equal("Uma encomenda leva pelo menos uma fábrica", new SetOrderFactoriesCommand(1, 0, 0).Validate(w));
        Assert.Equal("A encomenda já tem essas fábricas", new SetOrderFactoriesCommand(1, 0, 1).Validate(w));
        Assert.Equal("Encomenda inexistente", new SetOrderFactoriesCommand(1, 5, 2).Validate(w));
        Assert.Equal("país inválido", new SetOrderFactoriesCommand(77, 0, 2).Validate(w));
    }

    [Fact]
    public void SaveRoundTripKeepsTheFactoriesOfEachOrder()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 100f;
        Order(w, TestWorld.Inf); Order(w, TestWorld.Armor);
        new SetOrderFactoriesCommand(1, 1, 2).Execute(w);

        string schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(new[] { 1, 2 }, w2.Countries[1].Queue.Select(o => o.Factories));
    }
}
