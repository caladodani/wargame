using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Capacidade industrial (HoI4: fábricas civis, militares e estaleiros): quantas obras e quantas
/// linhas de montagem andam ao mesmo tempo, de onde vêm as fábricas e o que acontece quando estão todas
/// ocupadas.</summary>
public class IndustryTests
{
    private static (World w, Country c) Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Money = 1000f;
        return (w, c);
    }

    /// <summary>Põe a região na costa (Region.Coastal é só de arranque): troca-a por uma igual com cais.</summary>
    private static Region Coast(World w, int id)
    {
        var old = w.Regions[id];
        var r = new Region
        {
            Id = old.Id, Name = old.Name, OwnerId = old.OwnerId, InitialOwnerId = old.OwnerId,
            ControllerId = old.ControllerId, Terrain = old.Terrain, Population = old.Population, Coastal = true,
        };
        w.Regions[id] = r;
        return r;
    }

    [Fact]
    public void TheFactoryCountsComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(2f, w.Rule("factory_civil_base"));
        Assert.Equal(0.25f, w.Rule("factory_civil_per_region"), 3);
        Assert.Equal(2f, w.Rule("factory_mil_base"));
        Assert.Equal(0.15f, w.Rule("factory_mil_per_region"), 3);
        Assert.Equal(1f, w.Rule("factory_per_building"));
        Assert.Equal(3f, w.Rule("yard_divisions"));
    }

    [Fact]
    public void ASmallCountryStartsWithTheBaseFactories()
    {
        var (w, _) = Setup();
        var y = Industry.Of(w, 1);
        Assert.Equal(2, y.Civil);            // 2 + 3 regiões × 0.25 = 2.75 → 2
        Assert.Equal(2, y.Military);
        Assert.Equal(0, y.Naval);
        Assert.Equal(0, y.CivilBusy);
    }

    [Fact]
    public void ABiggerCountryOpensMoreFactories()
    {
        var (w, _) = Setup();
        for (int i = 7; i <= 14; i++)        // +8 regiões: 11 controladas ao todo
            w.Regions[i] = new Region { Id = i, Name = "R" + i, OwnerId = 1, ControllerId = 1, Population = 1_000_000 };

        var y = Industry.Of(w, 1);
        Assert.Equal(4, y.Civil);            // 2 + 11 × 0.25 = 4.75 → 4
        Assert.Equal(3, y.Military);         // 2 + 11 × 0.15 = 3.65 → 3
    }

    [Fact]
    public void EachYardBuildingAddsToItsOwnQueue()
    {
        var (w, _) = Setup();
        w.Regions[1].Buildings["fabrica"] = 2;
        w.Regions[2].Buildings["arsenal"] = 1;
        Coast(w, 3).Buildings["porto"] = 2;
        w.Regions[1].Buildings["laboratorio"] = 3;   // laboratório não é fila de fábricas

        var y = Industry.Of(w, 1);
        Assert.Equal(4, y.Civil);
        Assert.Equal(3, y.Military);
        Assert.Equal(2, y.Naval);
    }

    [Fact]
    public void FactoriesInOccupiedGroundWorkForWhoeverHoldsIt()
    {
        var (w, _) = Setup();
        w.Regions[4].Buildings["fabrica"] = 3;       // região do país 2
        Assert.Equal(5, Industry.Of(w, 2).Civil);
        w.Regions[4].ControllerId = 1;               // tomada: as fábricas mudam de mão
        Assert.Equal(6, Industry.Of(w, 1).Civil);    // 2 + 4 regiões × 0.25 + 3 fábricas
        Assert.Equal(2, Industry.Of(w, 2).Civil);
    }

    [Fact]
    public void EveryKindOfWorksiteEatsACivilFactory()
    {
        var (w, _) = Setup();
        w.Regions[1].Building = true;
        w.Regions[2].FortBuilding = true;
        var y = Industry.Of(w, 1);
        Assert.Equal(2, y.CivilBusy);
        Assert.Equal(0, y.FreeCivil);

        w.Regions[1].Building = false;
        w.Regions[3].Project = "fabrica";
        Assert.Equal(2, Industry.Of(w, 1).CivilBusy);
    }

    [Fact]
    public void WithEveryCivilFactoryTakenTheNextWorkIsRefused()
    {
        var (w, c) = Setup();
        Assert.Null(new BuildInfrastructureCommand(1, 1).Validate(w));
        new BuildInfrastructureCommand(1, 1).Execute(w);
        Assert.Null(new BuildFortCommand(1, 2).Validate(w));
        new BuildFortCommand(1, 2).Execute(w);

        Assert.Equal("fábricas civis todas ocupadas", new BuildInfrastructureCommand(1, 3).Validate(w));
        Assert.Equal("fábricas civis todas ocupadas", new BuildFortCommand(1, 3).Validate(w));
        Assert.Equal("fábricas civis todas ocupadas", new BuildBuildingCommand(1, 3, "fabrica").Validate(w));
        Assert.True(c.Money > 0f);                   // não é falta de dinheiro: é falta de fábrica
    }

    [Fact]
    public void FinishingAWorkGivesTheFactoryBack()
    {
        var (w, _) = Setup();
        w.Register(new ConstructionSystem());
        new BuildFortCommand(1, 1).Execute(w);
        new BuildFortCommand(1, 2).Execute(w);
        Assert.Equal(0, Industry.Of(w, 1).FreeCivil);

        TestWorld.Days(w, (int)w.Rule("fort_build_days") + 1);
        Assert.Equal(2, Industry.Of(w, 1).FreeCivil);
        Assert.Null(new BuildInfrastructureCommand(1, 3).Validate(w));
    }

    [Fact]
    public void OnlyAsManyOrdersAdvanceAsThereAreMilitaryFactories()
    {
        var (w, c) = Setup();
        w.Register(new ProductionSystem());
        w.Rules["factory_mil_base"] = 1f;
        w.Rules["factory_mil_per_region"] = 0f;
        new BuildDivisionCommand(1, TestWorld.Inf).Execute(w);
        new BuildDivisionCommand(1, TestWorld.Inf).Execute(w);

        w.Tick();
        Assert.True(c.Queue[0].Progress > 0f);
        Assert.Equal(0f, c.Queue[1].Progress);       // sem linha de montagem, a segunda não anda
        Assert.Equal(1, Industry.Of(w, 1).MilitaryBusy);
    }

    [Fact]
    public void AnOrderWaitingForRecruitsDoesNotHoldALine()
    {
        var (w, c) = Setup();
        w.Register(new ProductionSystem());
        w.Rules["factory_mil_base"] = 1f;
        w.Rules["factory_mil_per_region"] = 0f;
        c.Manpower = 0f;                             // a primeira fica pronta e espera homens
        float cost = w.TemplateCost(TestWorld.Inf);
        c.Queue.Add(new ProductionOrder { TemplateId = TestWorld.Inf, Progress = cost });
        new BuildDivisionCommand(1, TestWorld.Inf).Execute(w);

        w.Tick();
        Assert.True(c.Queue[1].Progress > 0f);       // a linha passou à seguinte
        Assert.Equal(1, Industry.Of(w, 1).MilitaryBusy);
    }

    [Fact]
    public void MoreArsenalsMeanMoreLines()
    {
        var (w, c) = Setup();
        w.Register(new ProductionSystem());
        w.Rules["factory_mil_base"] = 1f;
        w.Rules["factory_mil_per_region"] = 0f;
        w.Regions[1].Buildings["arsenal"] = 2;
        new BuildDivisionCommand(1, TestWorld.Inf).Execute(w);
        new BuildDivisionCommand(1, TestWorld.Inf).Execute(w);
        new BuildDivisionCommand(1, TestWorld.Inf).Execute(w);

        Assert.Equal(3, Industry.Of(w, 1).Military);
        w.Tick();
        Assert.All(c.Queue, o => Assert.True(o.Progress > 0f));
    }

    [Fact]
    public void DockyardsAreBusyWithWhatCrossesTheSea()
    {
        var (w, c) = Setup();
        Coast(w, 1).Buildings["porto"] = 2;
        Assert.Equal(0, Industry.Of(w, 1).NavalBusy);

        c.SeaSupplied = 4;                           // 4 divisões do outro lado, 3 por estaleiro
        Assert.Equal(2, Industry.Of(w, 1).NavalBusy);
        c.SeaSupplied = 30;                          // nunca passa dos estaleiros que existem
        Assert.Equal(2, Industry.Of(w, 1).NavalBusy);
    }

    [Fact]
    public void ACountryThatDoesNotExistHasNoIndustry()
    {
        var (w, _) = Setup();
        Assert.Equal(default, Industry.Of(w, 999));
    }
}
