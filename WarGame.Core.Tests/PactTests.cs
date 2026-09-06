using WarGame.Core.Commands;
using WarGame.Core.Model;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Pacto de não-agressão: aceitação determinística, bloqueio de DeclareWar, expiração.</summary>
public class PactTests
{
    [Fact]
    public void WeakerTarget_Accepts_AndBlocksWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1]; c.Money = 100f;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);   // 1 é mais forte (2 sem divisões)
        var cmd = new ProposeNonAggressionCommand(1, 2);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.True(w.HasPact(1, 2));
        Assert.Equal(100f - w.Rule("nap_cost", 20f), c.Money, 0.01f);
        Assert.NotNull(new DeclareWarCommand(1, 2).Validate(w));   // bloqueado
        Assert.NotNull(new DeclareWarCommand(2, 1).Validate(w));   // nos dois sentidos
    }

    [Fact]
    public void StrongerTarget_Rejects_WithoutCommonEnemy()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 100f;
        TestWorld.AddDivision(w, 1, 2, TestWorld.Inf, 4);   // 2 mais forte
        var cmd = new ProposeNonAggressionCommand(1, 2);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.False(w.HasPact(1, 2));
    }

    [Fact]
    public void JustifyingTarget_Rejects()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 100f;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        w.Countries[2].JustifyTarget = 1;   // já anda a justificar guerra contra o 1
        new ProposeNonAggressionCommand(1, 2).Execute(w);
        Assert.False(w.HasPact(1, 2));
    }

    [Fact]
    public void Pact_Expires()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Pacts[World.WarKey(1, 2)] = w.Clock.Day + 2;
        Assert.True(w.HasPact(1, 2));
        w.Clock.Advance(); w.Clock.Advance();
        Assert.True(w.HasPact(1, 2));    // último dia ainda conta
        w.Clock.Advance();
        Assert.False(w.HasPact(1, 2));
        Assert.Null(new DeclareWarCommand(1, 2).Validate(w));   // expirou: guerra volta a ser possível
    }
}
