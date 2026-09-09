using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>MovementSystem no mapa linear 1-2-3 (país 1) / 4-5-6 (país 2). Só Movement (+ Combat quando pedido).</summary>
public class MovementTests
{
    private static World Build(bool combat = false, bool war = false)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new MovementSystem());
        if (combat) w.Register(new CombatSystem());
        if (war) { w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1); }
        return w;
    }

    /// <summary>Dias que um template leva a entrar numa região, derivados das regras (não de números fixos).</summary>
    private static int HopDays(World w, int template, string terrain = "plain", float infra = 1f) =>
        (int)MathF.Ceiling(w.Rule("move_base_days") / w.Stats.Get(template)["mobility"] * w.MoveCost(terrain)
                           / MathF.Max(w.Rule("move_infra_floor", 0.5f), infra));

    [Fact]
    public void Infantry_TakesRuleDerivedDays_PerHop()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1); d.SetPath(new[] { 2 });
        int days = HopDays(w, TestWorld.Inf);
        Assert.True(days > 1, "o teste só faz sentido com saltos de vários dias");

        TestWorld.Days(w, days - 1);
        Assert.Equal(1, d.RegionId); Assert.InRange(d.MoveProgress, 0.01f, 0.999f);
        TestWorld.Days(w, 1);
        Assert.Equal(2, d.RegionId); Assert.Empty(d.Path); Assert.Equal(0f, d.MoveProgress);
        Assert.Contains(1, w.Regions[2].DivisionIds); Assert.DoesNotContain(1, w.Regions[1].DivisionIds);
    }



    [Fact]
    public void EmptyHostileRegion_IsCaptured()
    {
        var w = Build(war: true);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3); d.SetPath(new[] { 4 });
        RegionCaptured? captured = null; w.Events.Subscribe<RegionCaptured>(e => captured = e);

        TestWorld.Days(w, HopDays(w, TestWorld.Inf));
        Assert.Equal(1, w.Regions[4].ControllerId);
        Assert.Equal(new RegionCaptured(4, 2, 1), captured);
        Assert.Equal(4, d.RegionId); Assert.Contains(1, w.Regions[4].DivisionIds); Assert.Empty(d.Path);
        Assert.Empty(w.ActiveBattles);
    }

    [Fact]
    public void DefendedHostileRegion_OpensBattle_AttackerStaysHome()
    {
        var w = Build(war: true);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3); att.SetPath(new[] { 4 });
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
        var started = new List<BattleStarted>(); w.Events.Subscribe<BattleStarted>(started.Add);

        TestWorld.Days(w, HopDays(w, TestWorld.Inf));
        var b = Assert.Single(w.ActiveBattles);
        Assert.Equal(4, b.RegionId); Assert.Equal(1, b.AttackerCountryId);
        Assert.Equal(new[] { 1 }, b.Attackers); Assert.Equal(new[] { 2 }, b.Defenders);
        Assert.Equal(3, att.RegionId); Assert.Equal(1f, att.MoveProgress); Assert.Equal(new[] { 4 }, att.Path);
        Assert.Equal(new[] { new BattleStarted(4) }, started);
        Assert.Equal(2, w.Regions[4].ControllerId);

        // Sem CombatSystem a batalha nunca acaba: o atacante espera, sem duplicar nem avançar.
        TestWorld.Days(w, 5);
        Assert.Equal(3, att.RegionId); Assert.Single(w.ActiveBattles); Assert.Single(b.Attackers); Assert.Single(started);
    }







}
