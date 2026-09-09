using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Resistência nas regiões ocupadas: cresce sem guarnição, guarnição suprime,
/// a 1.0 revolta devolve o controlo ao dono e o rendimento ocupado cai com a resistência.</summary>
public class ResistanceTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ResistanceSystem());
        w.StartWar(1, 2, 0);
        w.Regions[4].ControllerId = 1;   // região do país 2 ocupada pelo 1
        return w;
    }



    [Fact]
    public void AtFull_Revolts_BackToOwner()
    {
        var w = Setup();
        var revolts = new List<RegionRevolted>();
        w.Events.Subscribe<RegionRevolted>(revolts.Add);
        TestWorld.Days(w, 60);   // 0.02/dia → 1.0 ao dia 50
        Assert.Single(revolts);
        Assert.Equal(4, revolts[0].RegionId);
        Assert.Equal(1, revolts[0].OldController);
        Assert.Equal(2, w.Regions[4].ControllerId);
        Assert.Equal(0f, w.Regions[4].Resistance);
    }


}
