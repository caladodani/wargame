using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>ManpowerSystem + custos de homens na produção e nos reforços. Regras usadas
/// (seed_world): per_million_daily 60, cap_share 0.05, start_share 0.5, per_cost 500,
/// reinforce_hp_manpower 30, reinforce_hp_money 0.05. LinearMap: 10M por região.</summary>
public class ManpowerTests
{
    [Fact]
    public void FirstTick_Initializes_ThenGrowsToCap()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);   // país 1 controla 3 regiões = 30M
        var c = w.Countries[1];
        c.Manpower = -1f;
        var sys = new ManpowerSystem();
        sys.Tick(w);
        Assert.Equal(30_000_000f * 0.05f * 0.5f, c.Manpower, 0.5f);   // tecto 1.5M, arranca a metade
        sys.Tick(w);
        Assert.Equal(750_000f + 30f * 60f, c.Manpower, 0.5f);         // +60/milhão/dia
        c.Manpower = 2_000_000f;   // acima do tecto → corta
        sys.Tick(w);
        Assert.Equal(1_500_000f, c.Manpower, 0.5f);
    }

    [Fact]
    public void Capitulated_GetsNoManpower()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[2].Capitulated = true; w.Countries[2].Manpower = -1f;
        new ManpowerSystem().Tick(w);
        Assert.Equal(-1f, w.Countries[2].Manpower);
    }

    [Fact]
    public void Delivery_WaitsForMen_ThenSpendsThem()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        float cost = w.TemplateCost(TestWorld.Inf);
        c.Queue.Add(new ProductionOrder { TemplateId = TestWorld.Inf, Progress = cost });   // pronta
        c.Manpower = cost * 500f - 1f;   // falta 1 homem
        new ProductionSystem().Tick(w);
        Assert.Single(c.Queue);   // espera
        c.Manpower = cost * 500f + 10f;
        new ProductionSystem().Tick(w);
        Assert.Empty(c.Queue);
        Assert.Equal(10f, c.Manpower, 0.5f);
    }

    [Fact]
    public void Reinforce_CostsMenAndMoney_AndStopsWhenBroke()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        var d = TestWorld.AddDivision(w, 5, 1, TestWorld.Inf, 2, org: 100f, hp: 90f);
        c.Manpower = 60f; c.Money = 100f;   // homens chegam para exactamente 2 HP
        new RecoverySystem().Tick(w);
        Assert.Equal(92f, d.Hp, 0.01f);
        Assert.Equal(0f, c.Manpower, 0.01f);
        Assert.Equal(100f - 2f * 0.05f, c.Money, 0.01f);
        new RecoverySystem().Tick(w);   // sem homens → HP não mexe, Org continua a subir de graça
        Assert.Equal(92f, d.Hp, 0.01f);
    }

    [Fact]
    public void Reinforce_PartialWhenMoneyShort()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        var d = TestWorld.AddDivision(w, 5, 1, TestWorld.Inf, 2, hp: 50f);
        c.Money = 0.05f;   // dinheiro só para 1 HP
        new RecoverySystem().Tick(w);
        Assert.Equal(51f, d.Hp, 0.01f);
        Assert.Equal(0f, c.Money, 0.01f);
    }
}
