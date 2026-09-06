using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Ligações marítimas (sea_link): caminho, dias por distância e IA além-mar.
/// Mapa: ilha A (regiões 1-2, país 1) e ilha B (3-4, país 2), travessia 2↔3.</summary>
public class SeaTests
{
    private static World Islands(float km = 1200f)
    {
        var (w, _) = TestWorld.Build();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 4, Manpower = 1e9f };
        for (int i = 1; i <= 4; i++)
        {
            int owner = i <= 2 ? 1 : 2;
            var r = new Region { Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner, Terrain = "plain", Population = 10_000_000, Coastal = i is 2 or 3 };
            w.Regions[i] = r;
        }
        w.Regions[1].Neighbours.Add(2); w.Regions[2].Neighbours.Add(1);
        w.Regions[3].Neighbours.Add(4); w.Regions[4].Neighbours.Add(3);
        w.Regions[2].SeaNeighbours[3] = km; w.Regions[3].SeaNeighbours[2] = km;
        return w;
    }

    [Fact]
    public void FindPath_CrossesSea_OnlyWhenTransitable()
    {
        var w = Islands();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        // Em paz o território do país 2 não é transitável — sem caminho.
        Assert.NotNull(new MoveDivisionCommand(1, 1, 4).Validate(w));
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        Assert.Null(new MoveDivisionCommand(1, 1, 4).Validate(w));
        Assert.Equal(new List<int> { 2, 3, 4 }, MoveDivisionCommand.FindPath(w, 1, 4, 1));
    }

    [Fact]
    public void Movement_SeaHop_TakesDistanceDays()
    {
        var w = Islands(km: 1200f);   // 1200 / 400 = 3 dias
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        new MoveDivisionCommand(1, 1, 3).Execute(w);
        var sys = new MovementSystem();
        sys.Tick(w); sys.Tick(w);
        Assert.Equal(2, d.RegionId);   // ainda a atravessar
        sys.Tick(w);
        Assert.Equal(3, d.RegionId);   // desembarcou e capturou (região vazia)
        Assert.Equal(1, w.Regions[3].ControllerId);
    }

    [Fact]
    public void Movement_SeaHop_HasMinimumDays()
    {
        var w = Islands(km: 100f);   // 100/400 < 1, mas sea_min_days = 2
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        new MoveDivisionCommand(1, 1, 3).Execute(w);
        var sys = new MovementSystem();
        sys.Tick(w);
        Assert.Equal(2, d.RegionId);
        sys.Tick(w);
        Assert.Equal(3, d.RegionId);
    }

    [Fact]
    public void AiWarGoal_TargetsAcrossSea()
    {
        var w = Islands();
        w.Rules["ai_war_chance"] = 1f; w.Rules["ai_war_min_day"] = 0; w.Rules["ai_war_ratio"] = 1f;
        w.Rules["war_justify_days"] = 2f;
        w.Countries[1].Stats["aggression"] = 1f;
        for (int i = 0; i < 3; i++) TestWorld.AddDivision(w, 10 + i, 1, TestWorld.Inf, 2);
        w.Register(new DiplomacySystem()); w.Register(new AiSystem());
        int period = Math.Max(1, (int)w.Rule("ai_period_days", 3));
        for (int i = 0; i < 6 * period; i++) w.Tick();
        Assert.True(w.AreAtWar(1, 2));
    }

    [Fact]
    public void AiFight_InvadesAcrossSea()
    {
        var w = Islands();
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        w.Register(new AiSystem());
        int period = Math.Max(1, (int)w.Rule("ai_period_days", 3));
        for (int i = 0; i < 2 * period; i++) w.Tick();
        Assert.True(d.Path.Count > 0 || d.RegionId >= 3);   // embarcou (ou já desembarcou) rumo à ilha B
    }
}

/// <summary>Guarnição costeira: com frente terrestre E costa ameaçada, a costa também é frente —
/// as reservas não a abandonam.</summary>
public class CoastalGarrisonTests
{
    [Fact]
    public void Reserve_StaysOnThreatenedCoast()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);                                   // 1-2-3 país 1, 4-5-6 país 2
        w.Regions[6].SeaNeighbours[1] = 800f; w.Regions[1].SeaNeighbours[6] = 800f;   // costa 6 ↔ costa 1
        w.Register(new AiSystem());
        w.StartWar(1, 2);
        TestWorld.AddDivision(w, 1, 2, TestWorld.Inf2, 6);        // reserva do país 2 na costa 6
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);        // frente terrestre guarnecida
        TestWorld.Days(w, 6);
        Assert.Equal(6, w.Divisions[1].RegionId);                 // não foi puxada para a frente 4
    }
}
