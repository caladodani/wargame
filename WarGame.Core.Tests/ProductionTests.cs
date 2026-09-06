using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

public class ProductionTests
{
    /// <summary>Só o ProductionSystem está registado e Money é fixado à mão, para as contas serem exactas.</summary>
    private static World Build(float money = 1000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ProductionSystem());
        w.Countries[1].Money = money;
        return w;
    }

    private static void Order(World w, int country, int template)
    {
        var cmd = new BuildDivisionCommand(country, template);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
    }

    private static int MinDays(World w) => (int)w.Rule("build_min_days");

    [Fact]
    public void Division_AppearsInCapital_ExactlyAfterMinDays()
    {
        var w = Build(); int days = MinDays(w);
        Order(w, 1, TestWorld.Inf);
        TestWorld.Days(w, days - 1);
        Assert.Empty(w.Divisions);
        Assert.Single(w.Countries[1].Queue);
        w.Tick();
        var d = Assert.Single(w.Divisions.Values);
        Assert.Equal(1, d.CountryId); Assert.Equal(TestWorld.Inf, d.TemplateId);
        Assert.Equal(w.Countries[1].CapitalRegionId, d.RegionId);
        Assert.Contains(d.Id, w.Regions[d.RegionId].DivisionIds);
        Assert.Equal(w.Rule("new_division_org"), d.Org);
        Assert.Equal(100f, d.Hp); Assert.Equal(1f, d.Supply);
        Assert.Empty(w.Countries[1].Queue);
        Assert.Equal(1000f - w.TemplateCost(TestWorld.Inf), w.Countries[1].Money, 1e-3f);
    }

    [Fact]
    public void DailySpend_IsCostOverMinDays()
    {
        var w = Build();
        Order(w, 1, TestWorld.Armor);
        w.Tick();
        float daily = w.TemplateCost(TestWorld.Armor) / MinDays(w);
        Assert.Equal(daily, w.Countries[1].Queue[0].Progress, 1e-4f);
        Assert.Equal(1000f - daily, w.Countries[1].Money, 1e-3f);
    }

    [Fact]
    public void NoMoney_NothingAdvances()
    {
        var w = Build(money: 0f);
        Order(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w) + 5);
        Assert.Equal(0f, w.Countries[1].Queue[0].Progress);
        Assert.Equal(0f, w.Countries[1].Money);
        Assert.Empty(w.Divisions);
    }

    [Fact]
    public void ScarceMoney_SpentInQueueOrder_NeverNegative()
    {
        var w = Build();
        float daily = w.TemplateCost(TestWorld.Inf) / MinDays(w);
        w.Countries[1].Money = daily * 1.5f;        // chega para a 1ª inteira e metade da 2ª
        Order(w, 1, TestWorld.Inf); Order(w, 1, TestWorld.Inf);
        w.Tick();
        var q = w.Countries[1].Queue;
        Assert.Equal(daily, q[0].Progress, 1e-4f);
        Assert.Equal(daily * 0.5f, q[1].Progress, 1e-4f);
        Assert.Equal(0f, w.Countries[1].Money, 1e-5f);
        TestWorld.Days(w, 3);
        Assert.True(w.Countries[1].Money >= 0f);
        Assert.Equal(daily, q[0].Progress, 1e-4f);  // sem dinheiro, nada anda
    }

    [Fact]
    public void MultipleOrders_AdvanceOnTheSameDay()
    {
        var w = Build();
        Order(w, 1, TestWorld.Inf); Order(w, 1, TestWorld.Armor);
        TestWorld.Days(w, MinDays(w));
        Assert.Equal(2, w.Divisions.Count);
        Assert.Empty(w.Countries[1].Queue);
        Assert.Equal(new[] { TestWorld.Inf, TestWorld.Armor }, w.Divisions.Values.OrderBy(d => d.Id).Select(d => d.TemplateId));
    }

    [Fact]
    public void Cancel_RefundsProgressToMoney()
    {
        var w = Build();
        Order(w, 1, TestWorld.Armor);
        TestWorld.Days(w, 3);
        float progress = w.Countries[1].Queue[0].Progress;
        Assert.True(progress > 0f);
        Assert.Equal(1000f - progress, w.Countries[1].Money, 1e-3f);
        var cancel = new CancelProductionCommand(1, 0);
        Assert.Null(cancel.Validate(w)); cancel.Execute(w);
        Assert.Empty(w.Countries[1].Queue);
        Assert.Equal(1000f, w.Countries[1].Money, 1e-3f);
    }

    [Fact]
    public void NewDivisionId_DoesNotCollideWithExisting()
    {
        var w = Build();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 7, 2, TestWorld.Inf2, 6);
        TestWorld.AddDivision(w, 4, 1, TestWorld.Inf, 2);
        Order(w, 1, TestWorld.Inf); Order(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w));
        Assert.Equal(new[] { 1, 4, 7, 8, 9 }, w.Divisions.Keys.OrderBy(x => x));
    }

    [Fact]
    public void ForeignOrUnknownTemplate_IsRejectedByValidate()
    {
        var w = Build();
        Assert.Equal("Template não é teu", new BuildDivisionCommand(1, TestWorld.Inf2).Validate(w));
        Assert.Equal("Template inexistente", new BuildDivisionCommand(1, 999).Validate(w));
        Assert.Empty(w.Countries[1].Queue);
    }

    [Fact]
    public void CapitalLost_SpawnsInMostPopulatedControlledRegion()
    {
        var w = Build();
        w.Regions[1].ControllerId = 2;              // capital do país 1 ocupada
        w.Regions[3] = new Region { Id = 3, Name = "R3", OwnerId = 1, ControllerId = 1, Population = 50_000_000 };
        Order(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w));
        Assert.Equal(3, Assert.Single(w.Divisions.Values).RegionId);
    }

    [Fact]
    public void NoControlledRegion_OrderWaitsReady()
    {
        var w = Build();
        foreach (var r in w.Regions.Values) r.ControllerId = 2;   // país 1 perdeu tudo
        Order(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w) + 3);
        Assert.Empty(w.Divisions);
        float cost = w.TemplateCost(TestWorld.Inf);
        Assert.Equal(cost, Assert.Single(w.Countries[1].Queue).Progress, 1e-3f);
        Assert.Equal(1000f - cost, w.Countries[1].Money, 1e-3f);  // pronta: não gasta mais
        w.Regions[1].ControllerId = 1;                          // recupera a capital → sai no tick seguinte
        w.Tick();
        Assert.Equal(1, Assert.Single(w.Divisions.Values).RegionId);
        Assert.Empty(w.Countries[1].Queue);
    }

    [Fact]
    public void WithEconomy_IncomeFundsProduction()
    {
        // Ciclo completo na ordem do Game.cs: Economy antes de Production no mesmo tick.
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EconomySystem()); w.Register(new ProductionSystem());
        Order(w, 1, TestWorld.Inf);
        float income = EconomySystem.Income(w, 1), cost = w.TemplateCost(TestWorld.Inf);
        Assert.True(income >= cost / MinDays(w));   // o rendimento cobre o gasto diário máximo
        TestWorld.Days(w, MinDays(w));
        Assert.Single(w.Divisions);
        Assert.Equal(MinDays(w) * income - cost, w.Countries[1].Money, 1e-3f);
    }
}
