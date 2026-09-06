using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Obras de infraestrutura: comando, conclusão, dano na captura e IA.</summary>
public class ConstructionTests
{
    [Fact]
    public void Build_PaysUpfront_FinishesAfterDays_CapsAtMax()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ConstructionSystem());
        var c = w.Countries[1]; c.Money = 100f;
        float cost = w.Rule("infra_build_cost", 40f);
        int days = (int)w.Rule("infra_build_days", 30f);

        var cmd = new BuildInfrastructureCommand(1, 1);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(100f - cost, c.Money, 0.01f);
        Assert.True(w.Regions[1].Building);
        Assert.NotNull(new BuildInfrastructureCommand(1, 1).Validate(w));   // já em obra

        var done = new List<InfrastructureBuilt>();
        w.Events.Subscribe<InfrastructureBuilt>(done.Add);
        TestWorld.Days(w, days);
        Assert.Single(done);
        Assert.Equal(1f + w.Rule("infra_step", 0.25f), w.Regions[1].Infrastructure, 0.001f);
        Assert.False(w.Regions[1].Building);

        // no tecto recusa
        w.Regions[1].Infrastructure = w.Rule("infra_max", 2f);
        Assert.NotNull(new BuildInfrastructureCommand(1, 1).Validate(w));
    }

    [Fact]
    public void Validate_RejectsForeignRegionAndNoMoney()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 1000f;
        Assert.NotNull(new BuildInfrastructureCommand(1, 4).Validate(w));   // região do país 2
        w.Countries[1].Money = 0f;
        Assert.NotNull(new BuildInfrastructureCommand(1, 1).Validate(w));   // sem dinheiro
    }

    [Fact]
    public void Capture_DamagesInfrastructure_AndKillsBuild()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var r = w.Regions[4];
        r.Infrastructure = 1f; r.Building = true; r.BuildProgress = 10f;
        CombatSystem.CaptureDamage(w, r);
        Assert.Equal(1f - w.Rule("capture_infra_hit", 0.15f), r.Infrastructure, 0.001f);
        Assert.False(r.Building);
        // nunca abaixo do chão
        r.Infrastructure = w.Rule("infra_min", 0.3f);
        CombatSystem.CaptureDamage(w, r);
        Assert.Equal(w.Rule("infra_min", 0.3f), r.Infrastructure, 0.001f);
    }

    [Fact]
    public void Ai_BuildsWeakestOwnRegion_WhenRich()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new AiSystem());
        var c = w.Countries[2]; c.Money = 500f;
        w.Regions[5].Infrastructure = 0.6f;   // a mais fraca do país 2
        TestWorld.Days(w, 6);
        Assert.True(w.Regions[5].Building);
    }

    [Fact]
    public void OccupiedRegion_LosesBuild()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ConstructionSystem());
        var r = w.Regions[1];
        r.Building = true; r.BuildProgress = 5f;
        r.ControllerId = 2;                    // ocupada
        TestWorld.Days(w, 1);
        Assert.False(r.Building);
        Assert.Equal(0f, r.BuildProgress);
    }
}
