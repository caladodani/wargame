using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Entrincheiramento: quem fica quieto cava, quem marcha perde o que cavou, quem assalta gasta-o.
/// A trincheira só conta a defender e o forte da região levanta o tecto.</summary>
public class EntrenchTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EntrenchSystem());
        return w;
    }


    [Fact]
    public void StandingStillDigsInUpToTheCeiling()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);

        TestWorld.Days(w, 4);
        Assert.Equal(4 * w.Rule("entrench_per_day"), d.Entrench, 3);

        TestWorld.Days(w, 40);
        Assert.Equal(w.Rule("entrench_max"), d.Entrench, 3);      // e não passa daí em campo aberto
    }







}
