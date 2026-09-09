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
        w.Countries[1].Political = 100f;   // o pretexto fabrica-se com poder político (0.3.61)
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


}
