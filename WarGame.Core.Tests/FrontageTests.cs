using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Largura de frente (HoI4: combat width): quantas divisões cabem na batalha, quem fica em reserva
/// e como a reserva rende a linha partida sem ninguém mandar.</summary>
public class FrontageTests
{
    private static (World w, Region r) Setup(string terrain = "plain", bool river = false)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var old = w.Regions[1];
        var r = new Region
        {
            Id = old.Id, Name = old.Name, OwnerId = old.OwnerId, InitialOwnerId = old.OwnerId,
            ControllerId = old.ControllerId, Terrain = terrain, Population = old.Population, River = river,
        };
        foreach (int nb in old.Neighbours) r.Neighbours.Add(nb);
        w.Regions[1] = r;
        return (w, r);
    }

    [Fact]
    public void TheWidthsComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(4f, w.Rule("front_width"));
        Assert.Equal(6f, w.Rule("front_width_plain"));
        Assert.Equal(3f, w.Rule("front_width_mountain"));
        Assert.Equal(3f, w.Rule("front_width_urban"));
        Assert.Equal(1f, w.Rule("front_width_river"));
    }

    [Fact]
    public void TheTerrainSaysHowManyFit()
    {
        Assert.Equal(6, Frontage.Width(Setup("plain").w, Setup("plain").r));
        Assert.Equal(3, Frontage.Width(Setup("mountain").w, Setup("mountain").r));
        Assert.Equal(3, Frontage.Width(Setup("urban").w, Setup("urban").r));
    }

    [Fact]
    public void AnUnknownTerrainFallsBackToTheDefault()
    {
        var (w, r) = Setup("pântano");
        Assert.Equal(4, Frontage.Width(w, r));
    }

    [Fact]
    public void ARiverNarrowsThePassage()
    {
        var (w, r) = Setup("plain", river: true);
        Assert.Equal(5, Frontage.Width(w, r));
    }

    [Fact]
    public void TheFrontIsNeverNarrowerThanOne()
    {
        var (w, r) = Setup("mountain", river: true);
        w.Rules["front_width_mountain"] = 0.5f;
        Assert.Equal(1, Frontage.Width(w, r));
    }

    [Fact]
    public void WhatDoesNotFitStaysInReserve()
    {
        var (w, r) = Setup("mountain");
        var divs = new List<Division>();
        for (int i = 1; i <= 5; i++) divs.Add(TestWorld.AddDivision(w, 100 + i, 1, TestWorld.Inf, 1));

        var (line, reserve) = Frontage.Split(w, r, divs);
        Assert.Equal(3, line.Count);
        Assert.Equal(2, reserve.Count);
    }

    [Fact]
    public void TheFittestTroopsHoldTheLine()
    {
        var (w, r) = Setup("mountain");
        var divs = new List<Division>();
        for (int i = 1; i <= 4; i++) divs.Add(TestWorld.AddDivision(w, 100 + i, 1, TestWorld.Inf, 1));
        divs[0].Org = 10f; divs[0].Hp = 20f;          // esta está feita em cacos
        divs[3].Org = 100f; divs[3].Hp = 100f;

        var (line, reserve) = Frontage.Split(w, r, divs);
        Assert.DoesNotContain(divs[0], line);
        Assert.Contains(divs[0], reserve);
        Assert.Contains(divs[3], line);
    }

    [Fact]
    public void ABattlefieldWithRoomForEveryoneKeepsNoReserve()
    {
        var (w, r) = Setup("plain");
        var divs = new List<Division>();
        for (int i = 1; i <= 3; i++) divs.Add(TestWorld.AddDivision(w, 100 + i, 1, TestWorld.Inf, 1));
        var (line, reserve) = Frontage.Split(w, r, divs);
        Assert.Equal(3, line.Count);
        Assert.Empty(reserve);
    }

    [Fact]
    public void ReservesNeitherHitNorGetHit()
    {
        var (w, r) = Setup("mountain");
        w.Register(new CombatSystem());
        w.Rules["front_width_mountain"] = 1f;
        var a1 = TestWorld.AddDivision(w, 101, 1, TestWorld.Inf, 1);
        var a2 = TestWorld.AddDivision(w, 102, 1, TestWorld.Inf, 1);
        a2.Org = 40f;                                  // fica na reserva: entra menos inteira
        var d1 = TestWorld.AddDivision(w, 201, 2, TestWorld.Inf2, 1);
        var battle = new Battle { RegionId = 1, AttackerCountryId = 1 };
        battle.Attackers.Add(a1.Id); battle.Attackers.Add(a2.Id); battle.Defenders.Add(d1.Id);
        w.ActiveBattles.Add(battle);

        float reserveHp = a2.Hp, reserveOrg = a2.Org;
        w.Tick();
        Assert.Equal(reserveHp, a2.Hp, 3);
        Assert.Equal(reserveOrg, a2.Org, 3);
        Assert.True(a1.Hp < 100f || a1.Org < 100f);    // a linha é que apanhou
    }

    [Fact]
    public void TheReserveTakesOverWhenTheLineIsSpent()
    {
        var (w, r) = Setup("mountain");
        w.Rules["front_width_mountain"] = 1f;
        var tired = TestWorld.AddDivision(w, 101, 1, TestWorld.Inf, 1);
        var fresh = TestWorld.AddDivision(w, 102, 1, TestWorld.Inf, 1);
        fresh.Org = 30f;
        Assert.Contains(tired, Frontage.Split(w, r, new[] { tired, fresh }).Line);

        tired.Org = 5f; tired.Hp = 20f;                // partida: a reserva passa à frente sozinha
        Assert.Contains(fresh, Frontage.Split(w, r, new[] { tired, fresh }).Line);
    }

    [Fact]
    public void ABattleGoesOnWhileTheReserveStandsUp()
    {
        var (w, _) = Setup("mountain");
        w.Register(new CombatSystem());
        w.Rules["front_width_mountain"] = 1f;
        var a1 = TestWorld.AddDivision(w, 101, 1, TestWorld.Inf, 1);
        var a2 = TestWorld.AddDivision(w, 102, 1, TestWorld.Inf, 1);
        var d1 = TestWorld.AddDivision(w, 201, 2, TestWorld.Inf2, 1);
        d1.Hp = 0.5f;                                  // o defensor cai já
        var battle = new Battle { RegionId = 1, AttackerCountryId = 1 };
        battle.Attackers.Add(a1.Id); battle.Attackers.Add(a2.Id); battle.Defenders.Add(d1.Id);
        w.ActiveBattles.Add(battle);

        w.Tick();
        Assert.Empty(w.ActiveBattles);                 // acabou por falta de defensores, não por largura
        Assert.Equal(1, w.Regions[1].ControllerId);
    }

    [Fact]
    public void PilingUpTroopsOnAMountainStopsPayingOff()
    {
        // Mesma força bruta, dois terrenos: na planície cabem todos e o ataque anda, na montanha não.
        static float Damage(string terrain, int attackers)
        {
            var (w, _) = Setup(terrain);
            w.Register(new CombatSystem());
            var def = TestWorld.AddDivision(w, 201, 2, TestWorld.Inf2, 1);
            var battle = new Battle { RegionId = 1, AttackerCountryId = 1 };
            for (int i = 0; i < attackers; i++) battle.Attackers.Add(TestWorld.AddDivision(w, 100 + i, 1, TestWorld.Inf, 1).Id);
            battle.Defenders.Add(def.Id);
            w.ActiveBattles.Add(battle);
            w.Tick();
            return 100f - def.Hp;
        }

        Assert.True(Damage("plain", 6) > Damage("mountain", 6));
    }
}
