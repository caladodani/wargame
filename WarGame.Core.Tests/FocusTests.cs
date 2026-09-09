using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>FocusSystem + SelectFocusCommand. Focos inseridos à mão no World (TestWorld não
/// carrega countries/*.sql); Tick chamado directo.</summary>
public class FocusTests
{
    private static World Setup(out Country c)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        c = w.Countries[1];
        w.Focuses["a"] = new Focus("a", 1, "Primeiro", "", 3, null, 1);
        w.Focuses["b"] = new Focus("b", 1, "Segundo", "", 2, "a", 2);
        w.FocusEffects["a"] = new() { ("industry", 1.5f) };
        return w;
    }

    [Fact]
    public void Ai_PicksCompletesAndChains_EffectApplies()
    {
        var w = Setup(out var c);
        var done = new List<FocusCompleted>();
        w.Events.Subscribe<FocusCompleted>(done.Add);
        var sys = new FocusSystem();
        sys.Tick(w);
        Assert.Equal("a", c.CurrentFocus);
        sys.Tick(w); sys.Tick(w);   // 3 dias
        Assert.Contains("a", c.FocusesDone);
        Assert.Equal(new FocusCompleted(1, "a"), Assert.Single(done));
        Assert.Equal(1.5f, c.Stat("industry"), 0.001f);   // Stats sem industry → 1 × mult
        sys.Tick(w);
        Assert.Equal("b", c.CurrentFocus);   // encadeia no que ficou disponível
    }






}
