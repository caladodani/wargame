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
        var c = w.Countries[1]; c.Political = 100f;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);   // 1 é mais forte (2 sem divisões)
        var cmd = new ProposeNonAggressionCommand(1, 2);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.True(w.HasPact(1, 2));
        Assert.Equal(100f - w.Rule("nap_cost", 20f), c.Political, 0.01f);
        Assert.NotNull(new DeclareWarCommand(1, 2).Validate(w));   // bloqueado
        Assert.NotNull(new DeclareWarCommand(2, 1).Validate(w));   // nos dois sentidos
    }




}
