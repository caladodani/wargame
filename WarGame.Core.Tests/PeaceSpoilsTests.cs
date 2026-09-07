using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Conferência de paz: quando um país cai, os vencedores repartem-lhe a terra por pontos de espólio
/// em vez de o maior ocupante levar tudo.
///
/// Mapa dos testes: linha 1-2-3 (país 1) | 4-5-6 (país 2, capital na 6) e a região 7 do país 3, colada à 6.</summary>
public class PeaceSpoilsTests
{
    private const int Far = 7;

    /// <summary>Mundo de três: o país 2 é o que vai cair, com os países 1 e 3 em cima dele.</summary>
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = Far, Manpower = 1e9f };
        var r = new Region
        {
            Id = Far, Name = "R7", OwnerId = 3, InitialOwnerId = 3, ControllerId = 3,
            Terrain = "plain", Population = 10_000_000, CenterX = 700, CenterY = 0,
        };
        r.Neighbours.Add(6); w.Regions[6].Neighbours.Add(Far);
        w.Regions[Far] = r;
        w.StartWar(1, 2); w.StartWar(3, 2);
        return w;
    }

    [Fact]
    public void ThePriceOfTheTableComesFromTheDatabase()
    {
        var w = Build();
        Assert.Equal(1f, w.Rule("spoil_points_per_million"), 3);
        Assert.Equal(3f, w.Rule("spoil_points_per_battle"), 3);

        // 10M de gente a 0,8 = 8 pontos; a capital pesa mais 25
        Assert.Equal(8f, PeaceSpoils.Cost(w, w.Regions[4], false), 3);
        Assert.Equal(33f, PeaceSpoils.Cost(w, w.Regions[6], true), 3);

        w.Regions[4].Buildings["fabrica"] = 2;
        Assert.Equal(20f, PeaceSpoils.Cost(w, w.Regions[4], false), 3);   // obra feita encarece a região
    }

    [Fact]
    public void PointsComeFromWhatEachOneOccupiesAndFoughtFor()
    {
        var w = Build();
        Assert.Equal(0f, PeaceSpoils.Points(w, 1, 2), 3);

        w.Regions[4].ControllerId = 1;                       // 10M ocupados = 10 pontos
        Assert.Equal(10f, PeaceSpoils.Points(w, 1, 2), 3);

        w.Wars[World.WarKey(1, 2)].Side(1).BattlesWon = 4;   // e 4 batalhas ganhas = mais 12
        w.Wars[World.WarKey(1, 2)].Side(1).RegionsTaken = 1; // e a região tomada = mais 2
        Assert.Equal(24f, PeaceSpoils.Points(w, 1, 2), 3);
    }

    [Fact]
    public void EachWinnerTakesTheLandItOccupiesAndTheLeftoverFallsToTheOldRule()
    {
        var w = Build();
        w.Regions[4].ControllerId = 1;                       // cada um com a sua terra ocupada
        w.Regions[5].ControllerId = 3;

        var claims = PeaceSpoils.Divide(w, w.Countries[2]);
        Assert.Equal(2, claims.Count);                       // 10 pontos cada: dá para uma região a 8
        Assert.Equal(1, w.Regions[4].OwnerId);
        Assert.Equal(3, w.Regions[5].OwnerId);
        Assert.Equal(2, w.Regions[6].OwnerId);               // a capital ficou por pagar: não muda de dono aqui
        Assert.All(claims, c => Assert.Equal(8f, c.Cost, 3));
    }

    [Fact]
    public void WhoeverFoughtTheWarBuysTheCapital()
    {
        var w = Build();
        w.Regions[4].ControllerId = 1;
        w.Regions[5].ControllerId = 3;
        w.Wars[World.WarKey(3, 2)].Side(3).BattlesWon = 12;  // 36 pontos de campanha por cima dos 10 da ocupação

        PeaceSpoils.Divide(w, w.Countries[2]);
        Assert.Equal(3, w.Regions[5].OwnerId);               // primeiro leva o que já ocupava
        Assert.Equal(3, w.Regions[6].OwnerId);               // e depois paga os 33 da capital
        Assert.Equal(1, w.Regions[4].OwnerId);
    }

    [Fact]
    public void AWinnerWithoutPointsLeavesTheTableEmptyHanded()
    {
        var w = Build();
        w.Regions[4].ControllerId = 1;
        w.Regions[5].ControllerId = 1;                        // o país 3 declarou guerra e não fez nada

        var claims = PeaceSpoils.Divide(w, w.Countries[2]);
        Assert.All(claims, c => Assert.Equal(1, c.WinnerId));
        Assert.Equal(new[] { 1, 3 }, PeaceSpoils.Table(w, w.Countries[2]));   // senta-se à mesa, mas sem pontos
        Assert.Equal(0f, PeaceSpoils.Points(w, 3, 2), 3);
    }

    [Fact]
    public void ASingleWinnerStillGetsTheWholeCountry()
    {
        var w = Build();
        w.Countries[3].AtWarWith.Clear();                     // guerra só entre o 1 e o 2, como dantes
        w.Countries[2].AtWarWith.Remove(3);
        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].ControllerId = 1;
        w.Register(new PeaceSystem());
        w.Tick();

        Assert.True(w.Countries[2].Capitulated);
        foreach (int id in new[] { 4, 5, 6 }) Assert.Equal(1, w.Regions[id].OwnerId);
    }

    [Fact]
    public void TheConferenceGoesIntoTheChronicle()
    {
        var w = Build();
        w.Regions[4].ControllerId = 1;
        w.Regions[5].ControllerId = 3;
        w.Regions[6].ControllerId = 1;                        // capital tomada: o país 2 cai neste tick
        w.Register(new ChronicleSystem());
        w.Register(new PeaceSystem());
        w.Tick();

        Assert.True(w.Countries[2].Capitulated);
        var lines = w.Chronicle.Where(e => e.Kind == "espolio").ToList();
        Assert.Equal(2, lines.Count);                         // um por vencedor que levou terra
        Assert.Contains(lines, e => e.Text.Contains("conferência de paz"));
        Assert.Equal(3, w.Regions[5].OwnerId);                // e a terra do país 3 é mesmo dele
    }
}
