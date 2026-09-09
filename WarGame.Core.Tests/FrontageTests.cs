using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Largura de frente (HoI4: combat width): quantas divisões cabem na batalha, quem fica em reserva
/// e como a reserva rende a linha partida sem ninguém mandar.</summary>
public class FrontageTests
{
    private static (World w, Region r) Setup(string terrain = "plain", bool river = false)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var old = w.Regions[1];
        var r = new Region
        {
            Id = old.Id, Name = old.Name, OwnerId = old.OwnerId, InitialOwnerId = old.OwnerId,
            ControllerId = old.ControllerId, Terrain = terrain, Population = old.Population, River = river,
        };
        foreach (int nb in old.Neighbours) r.Neighbours.Add(nb);
        w.Regions[1] = r;
        return (w, r);
    }


    [Fact]
    public void TheTerrainSaysHowManyFit()
    {
        Assert.Equal(6, Frontage.Width(Setup("plain").w, Setup("plain").r));
        Assert.Equal(3, Frontage.Width(Setup("mountain").w, Setup("mountain").r));
        Assert.Equal(3, Frontage.Width(Setup("urban").w, Setup("urban").r));
    }










}
