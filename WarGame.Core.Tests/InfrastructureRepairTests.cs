using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Reparação natural: região calma repõe a infraestrutura até à de origem e pára lá; batalha
/// na região ou ocupação com resistência alta travam a reparação.</summary>
public class InfrastructureRepairTests
{
    private static World Setup(float perDay = 0.1f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["infra_repair_per_day"] = perDay;
        w.Rules["infra_repair_max_resist"] = 0.3f;
        w.Register(new InfrastructureRepairSystem());
        return w;
    }

    /// <summary>LinearMap cria as regiões sem BaseInfrastructure (init-only), por isso os testes usam
    /// regiões próprias com o valor de origem explícito.</summary>
    private static Region Damaged(World w, int id, float infra, float baseInfra, int owner = 1, int controller = 1)
    {
        var r = new Region { Id = id, Name = "D" + id, OwnerId = owner, InitialOwnerId = owner, ControllerId = controller,
            Terrain = "plain", Population = 1_000_000, Infrastructure = infra, BaseInfrastructure = baseInfra };
        w.Regions[id] = r;
        return r;
    }

    [Fact]
    public void DamagedRegion_RepairsUpToBase_ThenStops()
    {
        var w = Setup();
        var r = Damaged(w, 50, infra: 0.5f, baseInfra: 1.0f);
        var evts = new List<InfrastructureRepaired>();
        w.Events.Subscribe<InfrastructureRepaired>(evts.Add);
        TestWorld.Days(w, 3);
        Assert.Equal(0.8f, r.Infrastructure, 0.001f);
        Assert.Empty(evts);
        TestWorld.Days(w, 10);
        Assert.Equal(1.0f, r.Infrastructure, 0.001f);   // pára na base, não passa
        Assert.Single(evts);
    }

    [Fact]
    public void RegionAtOrAboveBase_IsUntouched()
    {
        var w = Setup();
        var r = Damaged(w, 51, infra: 1.5f, baseInfra: 1.0f);   // obra paga acima da base
        TestWorld.Days(w, 5);
        Assert.Equal(1.5f, r.Infrastructure, 0.001f);
    }

    [Fact]
    public void ContestedRegion_DoesNotRepair()
    {
        var w = Setup();
        var r = Damaged(w, 52, infra: 0.5f, baseInfra: 1.0f);
        w.ActiveBattles.Add(new Battle { RegionId = 52, AttackerCountryId = 2 });
        TestWorld.Days(w, 5);
        Assert.Equal(0.5f, r.Infrastructure, 0.001f);
    }

    [Fact]
    public void Occupation_RepairsOnlyWhenResistanceIsLow()
    {
        var w = Setup();
        var r = Damaged(w, 53, infra: 0.5f, baseInfra: 1.0f, owner: 2, controller: 1);
        r.Resistance = 0.5f;
        TestWorld.Days(w, 3);
        Assert.Equal(0.5f, r.Infrastructure, 0.001f);
        r.Resistance = 0.1f;
        TestWorld.Days(w, 2);
        Assert.Equal(0.7f, r.Infrastructure, 0.001f);
    }

    [Fact]
    public void RuleAtZero_DisablesRepair()
    {
        var w = Setup(perDay: 0f);
        var r = Damaged(w, 54, infra: 0.5f, baseInfra: 1.0f);
        TestWorld.Days(w, 5);
        Assert.Equal(0.5f, r.Infrastructure, 0.001f);
    }
}
