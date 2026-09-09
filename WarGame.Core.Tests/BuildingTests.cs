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


}
