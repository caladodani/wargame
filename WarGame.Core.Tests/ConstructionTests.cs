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




}
