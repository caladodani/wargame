using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A marinha por classes de casco (ship_class): a esquadra deixa de ser um número e passa a ser
/// uma composição. A escolta leva os tiros primeiro, a linha pesa no combate, o submarino aperta o
/// bloqueio e não protege ninguém — e um mundo sem classes nenhumas continua a lutar como antes.
///
/// Mapa: a mesma linha 1-2-3 (país 1) | 4-5-6 (país 2) dos outros testes navais, com a costa em 3 e 4.</summary>
public class NavyTests
{
    private static World Build(float money = 5000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (int id in new[] { 3, 4 })
        {
            var r = w.Regions[id];
            w.Regions[id] = new Region
            {
                Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
                ControllerId = r.ControllerId, Terrain = r.Terrain, Population = r.Population, Coastal = true,
                SeaZoneId = "golfo",           // as duas costas dão para o mesmo mar: é lá que se encontram
            };
            foreach (int n in r.Neighbours) w.Regions[id].Neighbours.Add(n);
        }
        w.Regions[3].SeaNeighbours[4] = 200f; w.Regions[4].SeaNeighbours[3] = 200f;
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values) { c.Money = money; c.IsPlayer = true; }
        return w;
    }

    [Fact]
    public void AsClassesVemDaBaseDeDados()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.ShipClasses.Count >= 5);
        var sub = w.ShipClasses["submarino"];
        Assert.Equal(0f, sub.Screen, 3);                       // não protege ninguém, nem a si
        Assert.True(sub.Blockade > w.ShipClasses["destroier"].Blockade);
        Assert.True(w.ShipClasses["destroier"].Screen > w.ShipClasses["cruzador"].Screen);
        Assert.Equal("corveta", Navy.Basic(w));                // é a que o botão de sempre compra
    }









}
