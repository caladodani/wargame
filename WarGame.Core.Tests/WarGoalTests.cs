using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Objectivos de guerra: o que cada lado veio buscar. Mapa em linha 1-2-3 | 4-5-6,
/// país 1 à esquerda, país 2 à direita.</summary>
public class WarGoalTests
{
    private static (World w, WarGoalSystem sys) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var sys = new WarGoalSystem();
        w.Register(sys);
        w.StartWar(1, 2);
        return (w, sys);
    }

    private static WarInfo War(World w) => w.Wars[World.WarKey(1, 2)];






    [Fact]
    public void Achieved_FiresWhenEveryGoalRegionIsHeld()
    {
        var (w, sys) = Build();
        var seen = new List<WarGoalAchieved>();
        w.Events.Subscribe<WarGoalAchieved>(seen.Add);
        sys.Tick(w);
        Assert.False(WarGoalSystem.Met(w, War(w), 1));

        w.Regions[4].ControllerId = 1;
        sys.Tick(w);
        Assert.True(WarGoalSystem.Met(w, War(w), 1));
        Assert.Single(seen, e => e.CountryId == 1);

        TestWorld.Days(w, 3); sys.Tick(w);
        Assert.Single(seen, e => e.CountryId == 1);   // não repete enquanto o estado não mudar
    }


}
