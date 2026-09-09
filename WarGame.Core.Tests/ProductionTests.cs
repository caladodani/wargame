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
    public void CapitalLost_SpawnsInMostPopulatedControlledRegion()
    {
        var w = Build();
        w.Regions[1].ControllerId = 2;              // capital do país 1 ocupada
        w.Regions[3] = new Region { Id = 3, Name = "R3", OwnerId = 1, ControllerId = 1, Population = 50_000_000 };
        Order(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w));
        Assert.Equal(3, Assert.Single(w.Divisions.Values).RegionId);
    }


}
