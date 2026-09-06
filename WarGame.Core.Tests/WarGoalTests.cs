using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Objectivos de guerra: o que cada lado veio buscar. Mapa em linha 1-2-3 | 4-5-6,
/// país 1 à esquerda, país 2 à direita.</summary>
public class WarGoalTests
{
    private static (World w, WarGoalSystem sys) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var sys = new WarGoalSystem();
        w.Register(sys);
        w.StartWar(1, 2);
        return (w, sys);
    }

    private static WarInfo War(World w) => w.Wars[World.WarKey(1, 2)];

    [Fact]
    public void Border_IsTheDefaultGoal()
    {
        var (w, sys) = Build();
        sys.Tick(w);
        // só a região 4 faz fronteira com o país 1; a capital inimiga (6) não entra sem vantagem
        Assert.Equal(new[] { 4 }, War(w).Side(1).Goals.OrderBy(x => x));
        Assert.Equal(new[] { 3 }, War(w).Side(2).Goals.OrderBy(x => x));
    }

    [Fact]
    public void OwnLandUnderEnemyControl_ComesFirst()
    {
        var (w, sys) = Build();
        w.Regions[5].OwnerId = 1;              // região nossa no papel, controlada por eles
        sys.Tick(w);
        Assert.Contains(5, War(w).Side(1).Goals);   // apesar de não fazer fronteira connosco
    }

    [Fact]
    public void Capital_JoinsTheGoalWhenWeAreStronger()
    {
        var (w, sys) = Build();
        for (int i = 1; i <= 5; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 6);
        sys.Tick(w);
        Assert.Contains(6, War(w).Side(1).Goals);      // 5 contra 1 chega ao rácio de 2
        Assert.DoesNotContain(1, War(w).Side(2).Goals); // e eles não têm força para pedir a nossa
    }

    [Fact]
    public void Goal_IsChosenOnceAndStays()
    {
        var (w, sys) = Build();
        sys.Tick(w);
        var first = War(w).Side(1).Goals.ToList();
        w.Regions[4].ControllerId = 1;         // conquistada: a fronteira mudou
        TestWorld.Days(w, 3);
        sys.Tick(w);
        Assert.Equal(first, War(w).Side(1).Goals.ToList());
    }

    [Fact]
    public void Declaration_IsAnnouncedOnce()
    {
        var (w, sys) = Build();
        var seen = new List<WarGoalDeclared>();
        w.Events.Subscribe<WarGoalDeclared>(seen.Add);
        sys.Tick(w); TestWorld.Days(w, 3); sys.Tick(w);
        Assert.Equal(2, seen.Count);            // um anúncio por lado, e não mais
        Assert.Contains(seen, e => e.CountryId == 1 && e.TargetCountryId == 2 && e.RegionIds.Contains(4));
    }

    [Fact]
    public void Achieved_FiresWhenEveryGoalRegionIsHeld()
    {
        var (w, sys) = Build();
        var seen = new List<WarGoalAchieved>();
        w.Events.Subscribe<WarGoalAchieved>(seen.Add);
        sys.Tick(w);
        Assert.False(WarGoalSystem.Met(w, War(w), 1));

        w.Regions[4].ControllerId = 1;
        sys.Tick(w);
        Assert.True(WarGoalSystem.Met(w, War(w), 1));
        Assert.Single(seen, e => e.CountryId == 1);

        TestWorld.Days(w, 3); sys.Tick(w);
        Assert.Single(seen, e => e.CountryId == 1);   // não repete enquanto o estado não mudar
    }

    [Fact]
    public void AiAttacksTheGoalEvenWhenItIsBetterDefended()
    {
        var (w, _) = TestWorld.Build();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 3, Manpower = 1e9f };
        // região 1 (nossa) toca em 2 (vazia) e em 3 (defendida): sem objectivo iria à vazia
        foreach (int i in new[] { 1, 2, 3 })
        {
            int owner = i == 1 ? 1 : 2;
            w.Regions[i] = new Region { Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner, Terrain = "plain", Population = 1_000_000 };
        }
        w.Regions[1].Neighbours.Add(2); w.Regions[2].Neighbours.Add(1);
        w.Regions[1].Neighbours.Add(3); w.Regions[3].Neighbours.Add(1);
        w.StartWar(1, 2);
        for (int i = 1; i <= 6; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);
        w.Wars[World.WarKey(1, 2)].Side(1).Goals.Add(3);

        new AiSystem().Tick(w);

        Assert.Contains(w.Divisions.Values, d => d.CountryId == 1 && d.TargetRegionId == 3);
        Assert.DoesNotContain(w.Divisions.Values, d => d.CountryId == 1 && d.TargetRegionId == 2);
    }

    [Fact]
    public void Goals_SurviveSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        new WarGoalSystem().Tick(w);
        var before = w.Wars[World.WarKey(1, 2)].Side(1).Goals.ToList();
        Assert.NotEmpty(before);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(before, w2.Wars[World.WarKey(1, 2)].Side(1).Goals.ToList());
        Assert.NotEmpty(w2.Wars[World.WarKey(1, 2)].Side(2).Goals);
    }
}
