using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Edifícios regionais (tabela building): comando valida, obra demora Days, nível sobe até
/// MaxLevel, efeito multiplica o stat do controlador, captura cancela a obra.</summary>
public class BuildingTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.BuildingDefs["fabrica"] = new BuildingDef("fabrica", "Fábrica", 40f, 5f, "industry", 0.05f, 2);
        w.Register(new ConstructionSystem());
        w.Countries[1].Money = 100f;
        return w;
    }

    [Fact]
    public void Command_Validates()
    {
        var w = Setup();
        Assert.Null(new BuildBuildingCommand(1, 1, "fabrica").Validate(w));
        Assert.NotNull(new BuildBuildingCommand(1, 4, "fabrica").Validate(w));      // região do país 2
        Assert.NotNull(new BuildBuildingCommand(1, 1, "nada").Validate(w));         // edifício desconhecido
        w.Countries[1].Money = 10f;
        Assert.NotNull(new BuildBuildingCommand(1, 1, "fabrica").Validate(w));      // sem pontos
    }

    [Fact]
    public void Build_TakesDays_RaisesLevel_AndStat()
    {
        var w = Setup();
        float before = w.Countries[1].Stat("industry");
        var built = new List<BuildingBuilt>();
        w.Events.Subscribe<BuildingBuilt>(built.Add);
        var cmd = new BuildBuildingCommand(1, 1, "fabrica");
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);
        Assert.Equal(60f, w.Countries[1].Money, 0.01f);
        TestWorld.Days(w, 4);
        Assert.Empty(built);
        TestWorld.Days(w, 1);
        Assert.Single(built);
        Assert.Equal(1, w.Regions[1].Buildings["fabrica"]);
        Assert.Equal(before * 1.05f, w.Countries[1].Stat("industry"), 0.01f);
    }

    [Fact]
    public void MaxLevel_BlocksNewProject()
    {
        var w = Setup();
        w.Regions[1].Buildings["fabrica"] = 2;
        TestWorld.Days(w, 1);   // recalcula multiplicador
        Assert.NotNull(new BuildBuildingCommand(1, 1, "fabrica").Validate(w));
        Assert.Equal(1f + 0.05f * 2, w.Countries[1].BuildingMult["industry"], 0.001f);
    }

    [Fact]
    public void Capture_CancelsProject_EffectMovesToNewController()
    {
        var w = Setup();
        w.Regions[1].Project = "fabrica"; w.Regions[1].ProjectProgress = 3f;
        w.Regions[1].Buildings["fabrica"] = 1;
        w.Regions[1].ControllerId = 2;   // capturada
        TestWorld.Days(w, 1);
        Assert.Null(w.Regions[1].Project);
        Assert.False(w.Countries[1].BuildingMult.ContainsKey("industry"));
        Assert.Equal(1.05f, w.Countries[2].BuildingMult["industry"], 0.001f);
    }
}
