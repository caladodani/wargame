using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>TruceSystem: paz branca ao fim de war_white_peace_days sem capturas, em uti possidetis.</summary>
public class TruceTests
{
    private static World Setup(int staleDays = 10)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["war_white_peace_days"] = staleDays;
        w.StartWar(1, 2);
        return w;
    }

    [Fact]
    public void Stalemate_EndsInWhitePeace()
    {
        var w = Setup(10);
        var events = new List<IGameEvent>();
        w.Events.Subscribe<WhitePeaceSigned>(events.Add);
        w.Events.Subscribe<WarEnded>(events.Add);
        var sys = new TruceSystem();
        for (int i = 0; i < 9; i++) { w.Tick(); sys.Tick(w); }
        Assert.True(w.AreAtWar(1, 2));
        w.Tick(); sys.Tick(w);
        Assert.False(w.AreAtWar(1, 2));
        Assert.Empty(w.Wars);
        Assert.Equal(new IGameEvent[] { new WhitePeaceSigned(1, 2), new WarEnded(1, 2) }, events);
    }

    [Fact]
    public void Capture_ResetsTheClock()
    {
        var w = Setup(10);
        var sys = new TruceSystem();
        for (int i = 0; i < 9; i++) { w.Tick(); sys.Tick(w); }
        w.NoteWarProgress(1, 2);   // captura no dia 9
        for (int i = 0; i < 5; i++) { w.Tick(); sys.Tick(w); }
        Assert.True(w.AreAtWar(1, 2));   // 5 < 10 desde o último progresso
        for (int i = 0; i < 5; i++) { w.Tick(); sys.Tick(w); }
        Assert.False(w.AreAtWar(1, 2));
    }

    [Fact]
    public void WhitePeace_AnnexesControlledRegions()
    {
        var w = Setup(10);
        w.Regions[4].ControllerId = 1;   // país 1 ocupa a região 4 do país 2
        w.Regions[3].ControllerId = 2;   // país 2 ocupa a região 3 do país 1
        var sys = new TruceSystem();
        for (int i = 0; i < 10; i++) { w.Tick(); sys.Tick(w); }
        Assert.False(w.AreAtWar(1, 2));
        Assert.Equal(1, w.Regions[4].OwnerId);
        Assert.Equal(2, w.Regions[3].OwnerId);
        Assert.Equal(1, w.Regions[1].OwnerId);   // não ocupadas ficam como estavam
    }
}
