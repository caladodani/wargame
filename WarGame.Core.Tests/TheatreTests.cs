using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Teatros de operações: a linha de contacto partida em troços, cada um com a sua guarnição,
/// os seus buracos e o avanço da guerra a que pertence.
///
/// Mapa base: linha 1-2-3 (país 1) | 4-5-6 (país 2). Em guerra, a única região de contacto do país 1 é a 3.</summary>
public class TheatreTests
{
    private static World AtWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        return w;
    }


    [Fact]
    public void TheContactLineBecomesOneTheatreNamedAfterItsBiggestRegion()
    {
        var w = AtWar();
        var fronts = TheatreSystem.Of(w, 1);

        var front = Assert.Single(fronts);
        Assert.Equal(2, front.FoeId);
        Assert.Equal(new[] { 3 }, front.RegionIds);          // só a 3 encosta a terra deles
        Assert.Equal("Frente de R3", front.Name);
    }






}
