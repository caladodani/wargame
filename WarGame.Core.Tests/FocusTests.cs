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

    [Fact]
    public void Player_MustChoose_AndCommandValidates()
    {
        var w = Setup(out var c);
        c.IsPlayer = true;
        new FocusSystem().Tick(w);
        Assert.Null(c.CurrentFocus);   // IA não escolhe pelo jogador
        Assert.NotNull(new SelectFocusCommand(1, "b").Validate(w));    // falta o pré-requisito
        Assert.NotNull(new SelectFocusCommand(2, "a").Validate(w));    // foco de outro país
        Assert.Null(new SelectFocusCommand(1, "a").Validate(w));
        new SelectFocusCommand(1, "a").Execute(w);
        Assert.Equal("a", c.CurrentFocus);
    }

    [Fact]
    public void RealDb_PortugalAndBrazil_HaveTrees()
    {
        var w = FactionTests.BuildReal();
        Assert.True(w.Focuses.Values.Count(f => w.Countries[f.CountryId].Tag == "PRT") >= 5);
        Assert.True(w.Focuses.Values.Count(f => w.Countries[f.CountryId].Tag == "BRA") >= 5);
        var prtRoot = w.Focuses.Values.First(f => f.Id == "prt_atlantico");
        Assert.Null(prtRoot.Requires);
        Assert.True(w.FocusEffects.ContainsKey("prt_atlantico"));
    }
}
