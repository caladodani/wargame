using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>DiplomacySystem: justificar durante war_justify_days (30 no seed) e declarar sozinho.</summary>
public class DiplomacyTests
{
    [Fact]
    public void Justify_CountsDown_ThenDeclaresWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var declared = new List<WarDeclared>();
        w.Events.Subscribe<WarDeclared>(declared.Add);
        Assert.Null(new JustifyWarCommand(1, 2).Validate(w));
        new JustifyWarCommand(1, 2).Execute(w);
        var sys = new DiplomacySystem();
        for (int i = 0; i < 29; i++) sys.Tick(w);
        Assert.False(w.AreAtWar(1, 2));
        sys.Tick(w);
        Assert.True(w.AreAtWar(1, 2));
        Assert.Equal(new WarDeclared(1, 2), Assert.Single(declared));
        Assert.Null(w.Countries[1].JustifyTarget);
    }

    [Fact]
    public void Justify_CancelsWhenTargetCapitulates()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        new JustifyWarCommand(1, 2).Execute(w);
        w.Countries[2].Capitulated = true;
        new DiplomacySystem().Tick(w);
        Assert.Null(w.Countries[1].JustifyTarget);
        Assert.False(w.AreAtWar(1, 2));
    }

    [Fact]
    public void Validate_RejectsWarAndSelfAndRepeat()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Assert.NotNull(new JustifyWarCommand(1, 1).Validate(w));
        new JustifyWarCommand(1, 2).Execute(w);
        Assert.NotNull(new JustifyWarCommand(1, 2).Validate(w));   // já a justificar
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        Assert.NotNull(new JustifyWarCommand(1, 2).Validate(w));   // já em guerra
    }
}
