using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Redespacho estratégico: atravessar a retaguarda de comboio em vez de a marchar. Chega em
/// redeploy_speed do tempo, paga organização ao embarcar, quase não se recompõe pelo caminho e desce do
/// comboio se a frente lhe cortar a linha. O comboio não entra em terra inimiga nem atravessa o mar.</summary>
public class RedeployTests
{
    /// <summary>Mapa em linha de 10 regiões: 1..8 do país 1 (retaguarda funda), 9 e 10 do país 2.</summary>
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, n: 10, split: 8);
        w.StartWar(1, 2);
        w.Register(new MovementSystem());
        return w;
    }

    private static float Speed(World w) => w.Rule("redeploy_speed", 0.35f);
    private static float Cost(World w) => w.Rule("redeploy_org_cost", 40f);


    [Fact]
    public void TheTrainRunsFasterThanTheMarch()
    {
        var w = Build();
        var march = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var rail = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1);
        float onFoot = MovementSystem.HopDays(w, march, w.Regions[1], w.Regions[2]);
        new RedeployCommand(1, rail.Id, 8).Execute(w);
        float byRail = MovementSystem.HopDays(w, rail, w.Regions[1], w.Regions[2]);
        Assert.Equal(onFoot * Speed(w), byRail, 3);
        Assert.True(byRail < onFoot);
    }







}
