using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Desembarques: custo de organização ao sair do barco, organização mínima para assaltar
/// uma costa inimiga, lotação da praia e desvantagem de quem bate do mar.
/// Mapa: ilha A (1-2, país 1) e ilha B (3-4, país 2), travessia 2↔3.</summary>
public class NavalInvasionTests
{
    private static World Islands(float km = 800f)
    {
        var (w, _) = TestWorld.Build();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 4, Manpower = 1e9f };
        for (int i = 1; i <= 4; i++)
        {
            int owner = i <= 2 ? 1 : 2;
            w.Regions[i] = new Region { Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner, Terrain = "plain", Population = 10_000_000, Coastal = i is 2 or 3 };
        }
        w.Regions[1].Neighbours.Add(2); w.Regions[2].Neighbours.Add(1);
        w.Regions[3].Neighbours.Add(4); w.Regions[4].Neighbours.Add(3);
        w.Regions[2].SeaNeighbours[3] = km; w.Regions[3].SeaNeighbours[2] = km;
        w.StartWar(1, 2);
        return w;
    }

    private static void Sail(World w, MovementSystem sys, int days = 3) { for (int i = 0; i < days; i++) sys.Tick(w); }

    [Fact]
    public void Landing_CostsOrganisation()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        new MoveDivisionCommand(1, 1, 3).Execute(w);
        Sail(w, new MovementSystem());
        Assert.Equal(3, d.RegionId);
        Assert.Equal(100f - w.Rule("naval_invasion_org_cost"), d.Org, 3);
    }

    [Fact]
    public void TiredDivision_TurnsBackInsteadOfLanding()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2, org: 30f);   // abaixo de naval_invasion_min_org
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 3);                    // praia defendida
        var events = new List<IGameEvent>();
        w.Events.Subscribe<LandingAborted>(events.Add);
        new MoveDivisionCommand(1, 1, 3).Execute(w);

        Sail(w, new MovementSystem());

        Assert.Equal(2, d.RegionId);
        Assert.Empty(d.Path);
        Assert.Empty(w.ActiveBattles);
        Assert.Equal(new IGameEvent[] { new LandingAborted(1, 3) }, events);
    }

    [Fact]
    public void OnlyMaxDivsAssaultTheSameBeach()
    {
        var w = Islands();
        w.Rules["naval_invasion_max_divs"] = 2;
        for (int i = 1; i <= 4; i++)
        {
            TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 2);
            new MoveDivisionCommand(1, i, 3).Execute(w);
        }
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);

        Sail(w, new MovementSystem());

        var b = Assert.Single(w.ActiveBattles);
        Assert.Equal(2, b.Attackers.Count);
        Assert.All(w.Divisions.Values.Where(d => d.CountryId == 1 && !b.Attackers.Contains(d.Id)),
                   d => Assert.Equal(2, d.RegionId));   // as outras esperam ao largo, ainda com ordem
    }

    [Fact]
    public void WaitingDivisionsLandAfterTheBeachClears()
    {
        var w = Islands();
        w.Rules["naval_invasion_max_divs"] = 1;
        for (int i = 1; i <= 2; i++)
        {
            TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 2);
            new MoveDivisionCommand(1, i, 3).Execute(w);
        }
        var def = TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);
        var sys = new MovementSystem();
        Sail(w, sys);
        Assert.Single(w.ActiveBattles[0].Attackers);

        w.Regions[3].DivisionIds.Remove(def.Id); w.Divisions.Remove(def.Id);   // defensor sai de cena
        w.ActiveBattles.Clear();
        for (int i = 0; i < 3; i++) sys.Tick(w);

        Assert.All(w.Divisions.Values.Where(d => d.CountryId == 1), d => Assert.Equal(3, d.RegionId));
    }

    [Fact]
    public void AttackerFromTheSea_FightsWeaker()
    {
        var w = Islands();
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);   // ainda do outro lado da travessia
        Assert.Equal(w.Rule("naval_invasion_penalty"), CombatSystem.AmphibiousMult(w, att, w.Regions[3]), 3);
    }

    [Fact]
    public void DefenderAndLandAttacker_KeepFullStrength()
    {
        var w = Islands();
        var def = TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);
        Assert.Equal(1f, CombatSystem.AmphibiousMult(w, def, w.Regions[3]), 3);   // quem defende a praia
        var land = TestWorld.AddDivision(w, 8, 2, TestWorld.Inf2, 4);
        Assert.Equal(1f, CombatSystem.AmphibiousMult(w, land, w.Regions[3]), 3);  // e quem ataca por terra
    }

    [Fact]
    public void LandMove_IsNotTreatedAsALanding()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new MoveDivisionCommand(1, 1, 2).Execute(w);
        var sys = new MovementSystem();
        for (int i = 0; i < 40 && d.RegionId != 2; i++) sys.Tick(w);
        Assert.Equal(2, d.RegionId);
        Assert.Equal(100f, d.Org, 3);   // atravessar por terra não desembarca ninguém
    }
}
