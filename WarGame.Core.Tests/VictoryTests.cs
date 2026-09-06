using WarGame.Core.Events;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>VictorySystem: WorldDominated quando um país controla ≥ victory_pop_share da população.</summary>
public class VictoryTests
{
    [Fact]
    public void Domination_FiresOnceAtThreshold()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);   // 6 regiões de 10M; 3+3
        w.Rules["victory_pop_share"] = 0.6f; w.Rules["victory_check_days"] = 1;
        var events = new List<WorldDominated>();
        w.Events.Subscribe<WorldDominated>(events.Add);
        var sys = new VictorySystem();
        w.Tick(); sys.Tick(w);
        Assert.Empty(events);            // 3/6 = 50% < 60%
        w.Regions[4].ControllerId = 1;   // 4/6 ≈ 67%
        w.Tick(); sys.Tick(w);
        w.Tick(); sys.Tick(w);
        Assert.Equal(new[] { new WorldDominated(1) }, events);   // uma única vez
    }

    [Fact]
    public void Capitulated_NeverDominates()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["victory_pop_share"] = 0.6f; w.Rules["victory_check_days"] = 1;
        for (int i = 1; i <= 5; i++) w.Regions[i].ControllerId = 1;
        w.Countries[1].Capitulated = true;
        var events = new List<WorldDominated>();
        w.Events.Subscribe<WorldDominated>(events.Add);
        var sys = new VictorySystem();
        w.Tick(); sys.Tick(w);
        Assert.Empty(events);
    }
}
