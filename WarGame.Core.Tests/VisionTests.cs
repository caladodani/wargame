using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Nevoeiro de guerra: o jogador só conta as divisões de outro país se for aliado, se a região
/// fizer fronteira com terreno seu, se lá tiver tropa ou se a espionagem lhe der olhos.</summary>
public class VisionTests
{
    /// <summary>Mapa em linha de 6 regiões: 1-3 do país 1, 4-6 do país 2. R4 faz fronteira connosco,
    /// R5 e R6 estão lá para dentro.</summary>
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true;
        return w;
    }

    [Fact]
    public void TheRuleComesFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.Rule("fog_of_war") > 0f);
        Assert.True(Vision.Enabled(w));
    }

    [Fact]
    public void OurOwnGroundIsAlwaysInSight()
    {
        var w = Setup();
        Assert.True(Vision.Sees(w, 1, w.Regions[1]));
        Assert.True(Vision.Sees(w, 1, w.Regions[3]));
    }

    [Fact]
    public void TheirBorderRegionIsInSightButTheirRearIsNot()
    {
        var w = Setup();
        Assert.True(Vision.Sees(w, 1, w.Regions[4]));      // encosta ao nosso R3
        Assert.False(Vision.Sees(w, 1, w.Regions[5]));
        Assert.False(Vision.Sees(w, 1, w.Regions[6]));
    }

    [Fact]
    public void GarrisonsInTheFogAreNotCounted()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 40, 2, TestWorld.Inf2, 4);
        TestWorld.AddDivision(w, 50, 2, TestWorld.Inf2, 5);

        Assert.Equal(1, Vision.CountIn(w, 1, w.Regions[4]));
        Assert.Equal(0, Vision.CountIn(w, 1, w.Regions[5]));
        Assert.Empty(Vision.DivisionsIn(w, 1, w.Regions[5]));
    }

    [Fact]
    public void OurOwnTroopsAreVisibleWhereverTheyStand()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 60, 1, TestWorld.Inf, 6);   // assalto lá ao fundo
        TestWorld.AddDivision(w, 61, 2, TestWorld.Inf2, 6);

        Assert.True(Vision.Sees(w, 1, w.Regions[6]));        // quem lá está, vê
        Assert.Equal(2, Vision.CountIn(w, 1, w.Regions[6]));
    }

    [Fact]
    public void AnAllySharesWhatItSees()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 50, 2, TestWorld.Inf2, 5);
        Assert.False(Vision.Sees(w, 1, w.Regions[5]));

        var f = w.CreateFaction("pacto", "Pacto", "");
        f.Members.Add(1); f.Members.Add(2);

        Assert.True(Vision.Sees(w, 1, w.Regions[5]));
        Assert.Equal(1, Vision.CountIn(w, 1, w.Regions[5]));
    }

    [Fact]
    public void IntelligenceOnACountryOpensItsWholeInterior()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 60, 2, TestWorld.Inf2, 6);
        Assert.False(Vision.Sees(w, 1, w.Regions[6]));

        w.Intel[(1, 2)] = w.Clock.Day + 10;

        Assert.True(Vision.Sees(w, 1, w.Regions[6]));
        Assert.Equal(1, Vision.CountIn(w, 1, w.Regions[6]));
    }

    [Fact]
    public void IntelligenceThatRanOutStopsShowing()
    {
        var w = Setup();
        w.Intel[(1, 2)] = w.Clock.Day - 1;                   // rede caiu ontem
        Assert.False(Vision.Sees(w, 1, w.Regions[6]));
    }

    [Fact]
    public void AnOperationOnTheGroundOpensThatRegionOnly()
    {
        var w = Setup();
        w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = 1, TargetCountryId = 2, OpId = "sabotage", DaysLeft = 3f, RegionId = 6 });

        Assert.True(Vision.Sees(w, 1, w.Regions[6]));
        Assert.False(Vision.Sees(w, 1, w.Regions[5]));
    }

    [Fact]
    public void SeaBordersCountAsBordersToo()
    {
        var w = Setup();
        w.Regions[6].SeaNeighbours[1] = 120f;                 // R6 é do outro lado do mar do nosso R1
        Assert.True(Vision.Sees(w, 1, w.Regions[6]));
    }

    /// <summary>O mapa liga costas até 3200 km para se poder navegar e abastecer. Isso não é fronteira: com
    /// todas as ligações a valerem para a vista, quem tem costa numa bacia fechada via a bacia inteira.</summary>
    [Fact]
    public void ACoastOnTheFarSideOfOpenSeaStaysInTheFog()
    {
        var w = Setup();
        w.Regions[6].SeaNeighbours[1] = 900f;                 // navega-se até lá; não se vê de cá
        Assert.False(Vision.Sees(w, 1, w.Regions[6]));
        Assert.Equal("sem olhos nossos: só se sabe de quem é a terra", Vision.Why(w, 1, w.Regions[6]));
    }

    [Fact]
    public void TheReachOfASeaBorderIsARule()
    {
        var w = Setup();
        w.Regions[6].SeaNeighbours[1] = 900f;
        w.Rules["vision_sea_km"] = 1000f;
        Assert.True(Vision.Sees(w, 1, w.Regions[6]));
    }

    [Fact]
    public void WithTheRuleOffTheMapIsOpenAgain()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 60, 2, TestWorld.Inf2, 6);
        w.Rules["fog_of_war"] = 0f;

        Assert.False(Vision.Enabled(w));
        Assert.True(Vision.Sees(w, 1, w.Regions[6]));
        Assert.Equal(1, Vision.CountIn(w, 1, w.Regions[6]));
    }

    [Fact]
    public void TheReasonForSeeingIsSaidInWords()
    {
        var w = Setup();
        Assert.Equal("terreno nosso", Vision.Why(w, 1, w.Regions[1]));
        Assert.Equal("fronteira à vista das nossas patrulhas", Vision.Why(w, 1, w.Regions[4]));
        Assert.Equal("sem olhos nossos: só se sabe de quem é a terra", Vision.Why(w, 1, w.Regions[6]));
        w.Intel[(1, 2)] = w.Clock.Day + 5;
        Assert.Equal("rede de informações montada", Vision.Why(w, 1, w.Regions[6]));
    }
}
