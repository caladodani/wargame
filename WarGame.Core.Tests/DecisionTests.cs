using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Decisões nacionais: comando valida (custo, activa, espera), efeito imediato no Stat,
/// expira aos Days e o cooldown bloqueia até passar.</summary>
public class DecisionTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.DecisionDefs["mob"] = new DecisionDef("mob", "Mobilização", 30f, 3, 5, "industry", 1.15f);
        w.Register(new DecisionSystem());
        w.Countries[1].Money = 100f;
        return w;
    }

    [Fact]
    public void Activate_AppliesImmediately_AndPays()
    {
        var w = Setup();
        float before = w.Countries[1].Stat("industry");
        var cmd = new ActivateDecisionCommand(1, "mob");
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);
        Assert.Equal(70f, w.Countries[1].Money, 0.01f);
        Assert.Equal(before * 1.15f, w.Countries[1].Stat("industry"), 0.01f);
        Assert.NotNull(new ActivateDecisionCommand(1, "mob").Validate(w));   // já activa
    }

    [Fact]
    public void Expires_ThenCooldown_ThenAvailable()
    {
        var w = Setup();
        float baseVal = w.Countries[1].Stat("industry");
        new ActivateDecisionCommand(1, "mob").Execute(w);
        var expired = new List<DecisionExpired>();
        w.Events.Subscribe<DecisionExpired>(expired.Add);
        TestWorld.Days(w, 4);   // dias 0..3: activa até dia 3
        Assert.Empty(expired);
        TestWorld.Days(w, 1);   // dia 4: expira
        Assert.Single(expired);
        Assert.Equal(baseVal, w.Countries[1].Stat("industry"), 0.01f);
        Assert.Contains("espera", new ActivateDecisionCommand(1, "mob").Validate(w));
        TestWorld.Days(w, 4);   // até passar o cooldown (dia 3+5=8)
        Assert.Null(new ActivateDecisionCommand(1, "mob").Validate(w));
    }

    [Fact]
    public void Validate_RejectsPoorAndUnknown()
    {
        var w = Setup();
        Assert.NotNull(new ActivateDecisionCommand(1, "nada").Validate(w));
        w.Countries[1].Money = 5f;
        Assert.NotNull(new ActivateDecisionCommand(1, "mob").Validate(w));
    }
}
