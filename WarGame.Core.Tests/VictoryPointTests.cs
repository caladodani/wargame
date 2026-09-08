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
    public void TheTiersComeFromTheDatabase()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        Assert.NotEmpty(w.VictoryTiers);
        Assert.Contains(w.VictoryTiers.Values, t => t.Capital);              // há um grau da capital
        foreach (var t in w.VictoryTiers.Values)
        {
            Assert.True(t.Points > 0, $"{t.Id} não vale nada");
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
        }
        // a capital vale mais do que qualquer grau de população: senão havia cidades que valiam mais do que
        // a cadeira do governo e o mapa mentia sobre o que interessa tomar
        var capital = w.VictoryTiers.Values.First(t => t.Capital);
        Assert.All(w.VictoryTiers.Values.Where(t => !t.Capital), t => Assert.True(t.Points < capital.Points));
        // e mais gente nunca vale menos pontos
        var byPop = w.VictoryTiers.Values.Where(t => !t.Capital).OrderBy(t => t.MinPop).ToList();
        for (int i = 1; i < byPop.Count; i++) Assert.True(byPop[i].Points > byPop[i - 1].Points);
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

    /// <summary>Nada disto se guarda: mudar a capital muda os pontos no mesmo instante, sem sistema nenhum
    /// correr. É a razão de não haver um número guardado que possa contradizer o mapa.</summary>
    [Fact]
    public void MovingTheCapitalMovesThePointsWithIt()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w, population: 100);
        int seat = VictoryPoints.Of(w, w.Regions[1]);
        Assert.True(seat > 0);
        Assert.Equal(0, VictoryPoints.Of(w, w.Regions[3]));

        w.Countries[1].CapitalRegionId = 3;
        Assert.Equal(0, VictoryPoints.Of(w, w.Regions[1]));
        Assert.Equal(seat, VictoryPoints.Of(w, w.Regions[3]));
        Assert.Equal(seat, VictoryPoints.Total(w, 1));                      // o país continua a valer o mesmo
    }

    [Fact]
    public void WhatACountryIsWorthAndWhatIsInSomeoneElsesHand()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w, population: 100);
        var big = w.VictoryTiers.Values.Where(t => !t.Capital).OrderByDescending(t => t.Points).First();
        Pop(w, 5, (int)big.MinPop);                                         // uma praça no país 2

        int theirs = VictoryPoints.Total(w, 2);
        Assert.Equal(VictoryPoints.Of(w, w.Regions[5]) + VictoryPoints.Of(w, w.Regions[6]), theirs);
        Assert.Equal(0f, VictoryPoints.Taken(w, 1, 2));

        w.Regions[5].ControllerId = 1;                                      // tomámos-lhe a praça
        Assert.Equal((float)big.Points / theirs, VictoryPoints.Taken(w, 1, 2), 4);
        Assert.Equal(VictoryPoints.Total(w, 1) + big.Points, VictoryPoints.Held(w, 1));
        // as praças dele por ordem de valor: a capital primeiro, a cidade a seguir
        Assert.Equal(new[] { 6, 5 }, VictoryPoints.Prizes(w, 2, 2).Select(r => r.Id).ToArray());
    }

    /// <summary>A conta que muda o jogo: com o mesmo número de regiões tomadas, quem tomou as praças tem
    /// mais pressão na mesa do que quem tomou serra. Sem isto valia a pena avançar por onde não havia
    /// ninguém a defender.</summary>
    [Fact]
    public void TakingTheGreatCitiesPressesHarderThanTakingEmptyLand()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w, n: 8, split: 4, population: 100);
        var big = w.VictoryTiers.Values.Where(t => !t.Capital).OrderByDescending(t => t.Points).First();
        Pop(w, 6, (int)big.MinPop);                                         // a praça dele
        w.StartWar(1, 2);

        w.Regions[7].ControllerId = 1;                                      // uma região vazia
        float empty = PeaceTerms.Evaluate(w, 1, 2, System.Array.Empty<int>()).Pressure;

        w.Regions[7].ControllerId = 2; w.Regions[6].ControllerId = 1;       // a mesma região em número, mas é a praça
        float prize = PeaceTerms.Evaluate(w, 1, 2, System.Array.Empty<int>()).Pressure;

        Assert.True(prize > empty, $"a praça não pesou mais ({prize} contra {empty})");
        // a diferença é exactamente o peso da fatia de pontos: a fatia crua do território é a mesma nos dois
        // casos (uma região de quatro), e é isso que o teste isola
        Assert.Equal(w.Rule("peace_weight_vp", 0f) * VictoryPoints.Taken(w, 1, 2), prize - empty, 3);
    }

    [Fact]
    public void WithoutTheTableNothingIsWorthPointsAndThePeaceIsWeighedAsBefore()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w, population: 50_000_000);
        w.StartWar(1, 2);
        w.Regions[5].ControllerId = 1;
        float withPoints = PeaceTerms.Evaluate(w, 1, 2, System.Array.Empty<int>()).Pressure;
        float taken = VictoryPoints.Taken(w, 1, 2);
        Assert.True(taken > 0f);

        w.VictoryTiers.Clear();
        Assert.Equal(0, VictoryPoints.Of(w, w.Regions[1]));
        Assert.Equal(0, VictoryPoints.Total(w, 2));
        Assert.Equal(0f, VictoryPoints.Taken(w, 1, 2));
        Assert.Equal("", VictoryPoints.Line(w, w.Regions[1]));

        float without = PeaceTerms.Evaluate(w, 1, 2, System.Array.Empty<int>()).Pressure;
        Assert.Equal(withPoints - w.Rule("peace_weight_vp", 0f) * taken, without, 3);
    }

    /// <summary>O mapa e a ficha dizem o mesmo número que a paz: é a mesma função nos três sítios.</summary>
    [Fact]
    public void TheMapAndTheSheetSayTheSameNumber()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w, population: 100);
        var big = w.VictoryTiers.Values.Where(t => !t.Capital).OrderByDescending(t => t.Points).First();
        Pop(w, 5, (int)big.MinPop);

        Assert.Null(MapModes.Value(w, 1, w.Regions[4], "victory"));
        Assert.Equal("não conta pontos de vitória", MapModes.Text(w, 1, w.Regions[4], "victory"));
        Assert.Equal(big.Points, MapModes.Value(w, 1, w.Regions[5], "victory")!.Value, 3);
        Assert.Contains(big.Name, MapModes.Text(w, 1, w.Regions[5], "victory"));

        var sheet = RegionState.Parts(w, w.Regions[5]).Single(p => p.Name == "vitória");
        Assert.Equal(big.Points.ToString(), sheet.Value);
        Assert.Equal(big.Glyph, sheet.Glyph);

        // a região que não vale nada não gasta uma linha da ficha a dizê-lo
        Assert.DoesNotContain(RegionState.Parts(w, w.Regions[4]), p => p.Name == "vitória");
    }
}
