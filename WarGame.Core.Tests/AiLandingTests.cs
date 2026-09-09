using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A IA a marcar operações anfíbias: exige vantagem maior do que em terra (bate da praia),
/// guarda organização para a travessia, junta ai_invasion_min_divisions antes de assaltar praia defendida
/// e não embarca mais do que cabe na praia.
/// Mapa: ilha A (1-2, país 1) e ilha B (3-4, país 2), travessia 2↔3.</summary>
public class AiLandingTests
{
    private static World Islands()
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
        w.Regions[2].SeaNeighbours[3] = 800f; w.Regions[3].SeaNeighbours[2] = 800f;
        w.StartWar(1, 2);
        return w;
    }

    /// <summary>Mais n divisões do país 1 prontas a embarcar, na costa (região 2).</summary>
    private static void Troops(World w, int n, float org = 100f)
    {
        int next = w.Divisions.Count == 0 ? 1 : w.Divisions.Keys.Max() + 1;
        for (int i = 0; i < n; i++) TestWorld.AddDivision(w, next + i, 1, TestWorld.Inf, 2, org: org);
    }

    /// <summary>A tropa que a IA embarcou numa operação sobre a praia deles. Já não se pergunta quem vai a
    /// caminho da região 3: desde que a invasão passou a ser planeada, a IA marca a praia e a tropa fica no
    /// cais a preparar-se — é a operação que diz quem embarcou, não a rota.</summary>
    private static List<int> Sailing(World w) =>
        w.NavalInvasions.Where(i => i.CountryId == 1 && i.TargetId == 3).SelectMany(i => i.DivisionIds).ToList();

    [Fact]
    public void EmptyBeach_GetsASingleDivision()
    {
        var w = Islands();
        Troops(w, 3);
        new AiSystem().Tick(w);
        Assert.Single(Sailing(w));
    }

    [Fact]
    public void DefendedBeach_NeedsTheNavalRatio()
    {
        var w = Islands();
        w.Rules["ai_naval_ratio"] = 3f;
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);
        Troops(w, 2);                       // 2 contra 1 chega em terra, não no mar
        new AiSystem().Tick(w);
        Assert.Empty(Sailing(w));

        Troops(w, 1);                        // com 3 contra 1 já vale a pena
        new AiSystem().Tick(w);
        Assert.NotEmpty(Sailing(w));
    }

    [Fact]
    public void Wave_IsCappedByTheBeach()
    {
        var w = Islands();
        w.Rules["ai_naval_ratio"] = 1f;
        w.Rules["naval_invasion_max_divs"] = 2f;
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);
        Troops(w, 6);
        new AiSystem().Tick(w);
        Assert.Equal(2, Sailing(w).Count);
    }

    [Fact]
    public void TiredTroops_StayHome()
    {
        var w = Islands();
        float org = w.Rule("naval_invasion_min_org") + w.Rule("ai_naval_org_margin") - 5f;
        Troops(w, 3, org);
        new AiSystem().Tick(w);
        Assert.Empty(Sailing(w));
    }

    [Fact]
    public void NoLanding_WhenTheCoastIsNotHostile()
    {
        var w = Islands();
        w.EndWar(1, 2);
        Troops(w, 3);
        new AiSystem().Tick(w);
        Assert.Empty(Sailing(w));
    }

    [Fact]
    public void LandFront_ComesFirst()
    {
        var w = Islands();
        // a região 2 passa a ter também fronteira terrestre com uma região inimiga vazia
        w.Regions[5] = new Region { Id = 5, Name = "R5", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2, Terrain = "plain", Population = 10_000_000 };
        w.Regions[2].Neighbours.Add(5); w.Regions[5].Neighbours.Add(2);
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);   // praia defendida
        Troops(w, 2);

        new AiSystem().Tick(w);

        Assert.Contains(w.Divisions.Values, d => d.CountryId == 1 && d.TargetRegionId == 5);
        Assert.Empty(Sailing(w));
    }
}
