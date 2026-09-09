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







}
