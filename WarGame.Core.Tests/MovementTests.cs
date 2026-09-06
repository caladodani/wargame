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
    public void LowInfrastructure_SlowsDown_ButOnlyToFloor()
    {
        var w = Build();
        w.Regions[2].Infrastructure = 0.1f;   // abaixo do chão: conta como move_infra_floor
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1); d.SetPath(new[] { 2 });
        int days = HopDays(w, TestWorld.Inf, infra: 0.1f);
        Assert.True(days > HopDays(w, TestWorld.Inf));
        TestWorld.Days(w, days - 1); Assert.Equal(1, d.RegionId);
        TestWorld.Days(w, 1); Assert.Equal(2, d.RegionId);
    }

    [Fact]
    public void TwoHopPath_ArrivesWithEmptyPath()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1); d.SetPath(new[] { 2, 3 });
        int hop = HopDays(w, TestWorld.Inf);
        TestWorld.Days(w, hop);
        Assert.Equal(2, d.RegionId); Assert.Equal(new[] { 3 }, d.Path);
        TestWorld.Days(w, hop);
        Assert.Equal(3, d.RegionId); Assert.Empty(d.Path); Assert.Contains(1, w.Regions[3].DivisionIds);
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

    [Fact]
    public void ArmorVsInfantry_WithCombat_CapturesAndEntersWithin60Days()
    {
        var w = Build(combat: true, war: true);
        var a1 = TestWorld.AddDivision(w, 1, 1, TestWorld.Armor, 3); a1.SetPath(new[] { 4 });
        var a2 = TestWorld.AddDivision(w, 2, 1, TestWorld.Armor, 3); a2.SetPath(new[] { 4 });
        TestWorld.AddDivision(w, 3, 2, TestWorld.Inf2, 4);
        int captureDay = -1; w.Events.Subscribe<RegionCaptured>(e => captureDay = w.Clock.Day);

        int day;
        for (day = 1; day <= 60; day++)
        {
            w.Tick();
            if (w.Regions[4].ControllerId == 1 && a1.RegionId == 4 && a2.RegionId == 4) break;
        }
        Assert.True(day <= 60, "as blindadas deviam ter capturado a região 4 em ≤ 60 dias");
        Assert.True(captureDay > HopDays(w, TestWorld.Armor), "a captura só pode vir depois de chegar à fronteira e lutar");
        Assert.Empty(w.ActiveBattles); Assert.Empty(a1.Path); Assert.Empty(a2.Path);
        Assert.Equal(new[] { 1, 2 }, w.Regions[4].DivisionIds.OrderBy(x => x));
        // O defensor, se sobreviveu, recuou para território próprio.
        foreach (var d in w.Divisions.Values.Where(d => d.CountryId == 2)) Assert.Equal(2, w.Regions[d.RegionId].ControllerId);
    }

    [Fact]
    public void DivisionInCapturedRegion_RetreatsToOwnNeighbour()
    {
        var w = Build(war: true);
        w.Regions[4].ControllerId = 1;
        var d = TestWorld.AddDivision(w, 1, 2, TestWorld.Inf2, 4); d.SetPath(new[] { 3 });

        TestWorld.Days(w, 1);
        Assert.Equal(5, d.RegionId); Assert.Empty(d.Path); Assert.Equal(0f, d.MoveProgress);
        Assert.Contains(1, w.Regions[5].DivisionIds); Assert.DoesNotContain(1, w.Regions[4].DivisionIds);
    }

    [Fact]
    public void Retreat_PrefersNeighbourWithMostOwnDivisions()
    {
        var w = Build(war: true);
        w.Regions[3].ControllerId = 2; w.Regions[4].ControllerId = 1;   // país 2 tem 3 e 5 à volta da 4
        TestWorld.AddDivision(w, 10, 2, TestWorld.Inf2, 3);
        TestWorld.AddDivision(w, 11, 2, TestWorld.Inf2, 5); TestWorld.AddDivision(w, 12, 2, TestWorld.Inf2, 5);
        var d = TestWorld.AddDivision(w, 1, 2, TestWorld.Inf2, 4);

        TestWorld.Days(w, 1);
        Assert.Equal(5, d.RegionId);   // 5 tem 2 divisões próprias; 3 (id menor) só 1
    }

    [Fact]
    public void DivisionWithNoEscape_IsDestroyed()
    {
        var w = Build(war: true);
        w.Regions[4].ControllerId = 1; w.Regions[5].ControllerId = 1;
        TestWorld.AddDivision(w, 1, 2, TestWorld.Inf2, 4);
        var destroyed = new List<DivisionDestroyed>(); w.Events.Subscribe<DivisionDestroyed>(destroyed.Add);

        TestWorld.Days(w, 1);
        Assert.Equal(new[] { new DivisionDestroyed(1) }, destroyed);
        Assert.DoesNotContain(1, w.Divisions.Keys); Assert.Empty(w.Regions[4].DivisionIds);
    }

    [Fact]
    public void ThirdPartyRegion_StopsWithoutCapture()
    {
        var w = Build(war: true);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama" };
        w.Regions[2].ControllerId = 3;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1); d.SetPath(new[] { 2, 3 });
        bool captured = false; w.Events.Subscribe<RegionCaptured>(_ => captured = true);

        TestWorld.Days(w, HopDays(w, TestWorld.Inf) + 1);
        Assert.Equal(1, d.RegionId); Assert.Empty(d.Path); Assert.Equal(0f, d.MoveProgress);
        Assert.Equal(3, w.Regions[2].ControllerId); Assert.False(captured); Assert.Empty(w.ActiveBattles);
    }

    [Fact]
    public void LowOrgDivision_DoesNotOpenBattle()
    {
        var w = Build(war: true);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3, org: 5f); att.SetPath(new[] { 4 });
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);

        TestWorld.Days(w, HopDays(w, TestWorld.Inf));
        Assert.Empty(att.Path); Assert.Equal(3, att.RegionId); Assert.Empty(w.ActiveBattles);
        Assert.Equal(2, w.Regions[4].ControllerId);
    }

    [Fact]
    public void ArrivingInOwnRegionUnderAttack_JoinsDefenders()
    {
        var w = Build(war: true);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 3);
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 4);
        var b = new Battle { RegionId = 3, AttackerCountryId = 2 }; b.Attackers.Add(9); b.Defenders.Add(2);
        w.ActiveBattles.Add(b);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2); d.SetPath(new[] { 3 });

        TestWorld.Days(w, HopDays(w, TestWorld.Inf));
        Assert.Equal(3, d.RegionId); Assert.Equal(new[] { 2, 1 }, b.Defenders); Assert.True(w.InBattle(1));
    }
}
