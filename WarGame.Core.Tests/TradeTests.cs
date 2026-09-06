using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Comércio de recursos: alugar depósitos move o bónus e paga por dia;
/// o acordo cai com guerra ou falta de dinheiro.</summary>
public class TradeTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.ResourceDefs["aco"] = new ResourceDef("aco", "Aço", "production_speed", 0.02f, 10f);
        w.Regions[4].Resources["aco"] = 4f;   // país 2 tem 4 de aço
        w.Countries[1].Money = 100f;
        w.Register(new TradeSystem());
        w.Register(new ResourceSystem());
        return w;
    }

    [Fact]
    public void Deal_MovesUnits_AndPays()
    {
        var w = Setup();
        var cmd = new CreateTradeDealCommand(1, 2, "aco", 3f);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        float m1 = w.Countries[1].Money, m2 = w.Countries[2].Money;
        TestWorld.Days(w, 1);
        Assert.Equal(1.06f, w.Countries[1].ResourceMult["production_speed"], 0.001f);
        Assert.Equal(1.02f, w.Countries[2].ResourceMult["production_speed"], 0.001f);   // 4 − 3 vendidas
        Assert.True(w.Countries[1].Money < m1 - 5f);          // pagou 6 (mais rendimento próprio)
        Assert.True(w.Countries[2].Money > m2 + 5f);
    }

    [Fact]
    public void Oversell_Rejected()
    {
        var w = Setup();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        Assert.NotNull(new CreateTradeDealCommand(1, 2, "aco", 2f).Validate(w));   // só resta 1 livre
    }

    [Fact]
    public void War_KillsDeal()
    {
        var w = Setup();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        var ended = new List<TradeDealEnded>();
        w.Events.Subscribe<TradeDealEnded>(ended.Add);
        w.StartWar(1, 2, 0);
        TestWorld.Days(w, 1);
        Assert.Single(ended);
        Assert.Empty(w.TradeDeals);
    }

    [Fact]
    public void NoMoney_KillsDeal()
    {
        var w = Setup();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        w.Countries[1].Money = 0f;
        // sem rendimento que chegue no próprio dia: bloquear rendimento tirando as regiões
        foreach (var r in w.Regions.Values) if (r.ControllerId == 1) r.ControllerId = 3;
        TestWorld.Days(w, 1);
        Assert.Empty(w.TradeDeals);
    }

    [Fact]
    public void Cancel_ByEitherSide()
    {
        var w = Setup();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        Assert.Null(new CancelTradeDealCommand(2, 1, "aco").Validate(w));
        new CancelTradeDealCommand(2, 1, "aco").Execute(w);
        Assert.Empty(w.TradeDeals);
    }
}
