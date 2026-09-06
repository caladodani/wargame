using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>IA oferece paz branca em guerras paradas (peace_stale_days sem progresso)
/// em que está mais fraca; guerras frescas ou contra o jogador ficam.</summary>
public class AiPeaceTests
{
    private static World Setup(out WarInfo info)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        info = w.Wars[World.WarKey(1, 2)];
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);           // 1 tem 1 divisão
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 6);          // 2 tem 2 — o 1 é o mais fraco
        TestWorld.AddDivision(w, 3, 2, TestWorld.Inf2, 6);
        w.Register(new AiSystem());
        return w;
    }

    [Fact]
    public void StaleWar_WeakerSide_OffersAndGetsWhitePeace()
    {
        var w = Setup(out var info);
        while (w.Clock.Day < (int)w.Rule("peace_stale_days", 60f) + 7) w.Tick();
        Assert.False(w.AreAtWar(1, 2));
    }

    [Fact]
    public void FreshWar_Continues()
    {
        var w = Setup(out _);
        TestWorld.Days(w, 10);
        Assert.True(w.AreAtWar(1, 2));
    }

    [Fact]
    public void PlayerEnemy_NeverAutoPeaced()
    {
        var w = Setup(out _);
        w.Countries[2].IsPlayer = true;
        while (w.Clock.Day < (int)w.Rule("peace_stale_days", 60f) + 10) w.Tick();
        Assert.True(w.AreAtWar(1, 2));
    }
}
