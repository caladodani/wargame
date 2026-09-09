using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Guerra naval por mar de costa: as esquadras destacadas saem do pool, custam estadia, afundam-se
/// umas às outras no mar disputado, fecham o cais de quem bloqueiam e tiram a costa do nevoeiro.
///
/// Mapa dos testes: linha 1-2-3 (país 1) | 4-5-6 (país 2), mais a ilha 7 do país 2, ligada por mar à
/// região 3 (do país 1) e à região 4 (do país 2).</summary>
public class NavalMissionTests
{
    private const int Island = 7;

    private static World Build(float ships = 10f, float money = 500f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Sea(w);
        w.StartWar(1, 2);
        // os dois países de mão humana: a IA naval tem um teste só para ela e não anda a engrossar estes
        foreach (var c in w.Countries.Values) { c.Warships = ships; c.Money = money; c.IsPlayer = true; }
        w.Register(new NavalMissionSystem());
        return w;
    }

    /// <summary>Põe mar no mapa linear: a ilha 7 do país 2 a 500 km da região 3 (nossa) e da região 4.</summary>
    private static void Sea(World w)
    {
        w.Regions[Island] = new Region
        {
            Id = Island, Name = "Ilha", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1_000_000, CenterX = 300, CenterY = 500, Coastal = true,
        };
        w.Regions[3] = Coast(w.Regions[3]);
        w.Regions[4] = Coast(w.Regions[4]);
        w.Regions[3].SeaNeighbours[Island] = 500f; w.Regions[Island].SeaNeighbours[3] = 500f;
        w.Regions[4].SeaNeighbours[Island] = 500f; w.Regions[Island].SeaNeighbours[4] = 500f;
    }

    /// <summary>Cópia costeira de uma região (Coastal é init-only).</summary>
    private static Region Coast(Region r)
    {
        var c = new Region
        {
            Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
            ControllerId = r.ControllerId, Terrain = r.Terrain, Population = r.Population,
            CenterX = r.CenterX, CenterY = r.CenterY, Coastal = true,
        };
        foreach (int n in r.Neighbours) c.Neighbours.Add(n);
        return c;
    }








    [Fact]
    public void ABlockadeShutsThePortAndTheEscortReopensIt()
    {
        var w = Build();
        Assert.False(NavalMissionSystem.Blockaded(w, Island));

        NavalMissionSystem.Assign(w, 1, Island, "bloqueio", 3f);
        Assert.True(NavalMissionSystem.Blockaded(w, Island));

        NavalMissionSystem.Assign(w, 2, Island, "escolta", 3f);               // escolta igual: o mar continua aberto
        Assert.False(NavalMissionSystem.Blockaded(w, Island));

        NavalMissionSystem.Assign(w, 1, Island, "bloqueio", 1f);              // mais um navio e volta a fechar
        Assert.True(NavalMissionSystem.Blockaded(w, Island));
    }





}
