using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Pontos de vitória: nem toda a terra vale o mesmo. O que se prova aqui é que os graus vêm da
/// tabela, que o valor de uma região se lê da região (e por isso segue a capital quando ela muda), e que a
/// mesa de paz passou a distinguir quem tomou as praças de quem tomou serra.</summary>
public class VictoryPointTests
{
    /// <summary>Troca a gente de uma região: Region.Population é só de leitura depois de criada, e um teste
    /// de pontos de vitória precisa de mexer nela para percorrer os graus da tabela.</summary>
    private static void Pop(World w, int id, int people)
    {
        var r = w.Regions[id];
        var fresh = new Region
        {
            Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
            ControllerId = r.ControllerId, Terrain = r.Terrain, Population = people,
            CenterX = r.CenterX, CenterY = r.CenterY,
        };
        foreach (int nb in r.Neighbours) fresh.Neighbours.Add(nb);
        w.Regions[id] = fresh;
    }


    [Fact]
    public void ARegionIsWorthWhatItsPeopleAndItsCapitalSay()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w, population: 100);                            // aldeias: não contam
        Assert.Equal(0, VictoryPoints.Of(w, w.Regions[2]));
        Assert.Null(VictoryPoints.Tier(w, w.Regions[2]));

        var steps = w.VictoryTiers.Values.Where(t => !t.Capital).OrderBy(t => t.MinPop).ToList();
        foreach (var t in steps)
        {
            Pop(w, 2, (int)t.MinPop);
            Assert.Equal(t.Points, VictoryPoints.Of(w, w.Regions[2]));
        }

        // a capital vale o grau da capital, por muito pequena que seja
        var seat = w.VictoryTiers.Values.First(t => t.Capital);
        Assert.Equal(seat.Points, VictoryPoints.Of(w, w.Regions[1]));
        Assert.Equal(seat.Id, VictoryPoints.Tier(w, w.Regions[1])!.Id);
    }





}
