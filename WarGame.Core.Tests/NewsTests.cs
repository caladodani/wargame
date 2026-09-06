using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>NewsSystem: dispara no dia exacto e os efeitos contam em ApplyTechs para eventos passados.</summary>
public class NewsTests
{
    [Fact]
    public void FiresOnExactDay_AndEffectSticksAfterReapply()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.NewsEvents["crise"] = new NewsEvent("crise", 2, null, "Crise", "corpo");
        w.NewsEffects["crise"] = new() { ("industry", 0.9f) };
        var fired = new List<NewsFired>();
        w.Events.Subscribe<NewsFired>(fired.Add);
        var sys = new NewsSystem();
        w.Clock.Advance(); sys.Tick(w);   // dia 1
        Assert.Empty(fired);
        Assert.Equal(1f, w.Countries[1].Stat("industry"), 0.001f);
        w.Clock.Advance(); sys.Tick(w);   // dia 2
        Assert.Single(fired);
        Assert.Equal(0.9f, w.Countries[1].Stat("industry"), 0.001f);
        w.ApplyTechs(w.Countries[1]);     // recarregar/recalcular não perde o efeito
        Assert.Equal(0.9f, w.Countries[1].Stat("industry"), 0.001f);
    }

    [Fact]
    public void CountryEvent_OnlyHitsThatCountry()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.NewsEvents["boom"] = new NewsEvent("boom", 1, 1, "Boom", "");
        w.NewsEffects["boom"] = new() { ("industry", 1.2f) };
        w.Clock.Advance(); new NewsSystem().Tick(w);
        Assert.Equal(1.2f, w.Countries[1].Stat("industry"), 0.001f);
        w.ApplyTechs(w.Countries[2]);
        Assert.Equal(1f, w.Countries[2].Stat("industry"), 0.001f);
    }
}
