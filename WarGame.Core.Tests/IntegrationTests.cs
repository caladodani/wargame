using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>IntegrationSystem: ocupação calma acumula, resistência alta faz recuar, aos
/// integration_days a região muda de dono; leis de ocupação mexem na velocidade.</summary>
public class IntegrationTests
{
    private static World Setup(float need = 5f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["integration_days"] = need;
        w.Register(new IntegrationSystem());
        w.Regions[4].ControllerId = 1;   // região do país 2 ocupada pelo 1
        return w;
    }

    [Fact]
    public void QuietOccupation_TransfersOwnership()
    {
        var w = Setup(need: 3f);
        var evts = new List<RegionIntegrated>();
        w.Events.Subscribe<RegionIntegrated>(evts.Add);
        TestWorld.Days(w, 3);
        Assert.Single(evts);
        Assert.Equal(1, w.Regions[4].OwnerId);
        Assert.Equal(0f, w.Regions[4].Integration);
        Assert.Equal((4, 2, 1), (evts[0].RegionId, evts[0].OldOwner, evts[0].NewOwner));
    }



    [Fact]
    public void OccupationLaw_ChangesSpeed()
    {
        var w = Setup(need: 100f);
        w.Countries[1].Stats["integration_speed"] = 1.5f;   // occ_gentle via lei
        TestWorld.Days(w, 4);
        Assert.Equal(6f, w.Regions[4].Integration, 0.01f);
    }

}
