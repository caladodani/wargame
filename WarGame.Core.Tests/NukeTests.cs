using WarGame.Core.Commands;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Armas nucleares: BuildNukeCommand exige a tecnologia (Stat "nuclear" &gt; 1) e paga nuke_cost;
/// NuclearStrikeCommand arrasa divisões/infra da região inimiga e custa estabilidade aos dois lados;
/// a IA constrói com tesouro folgado e lança sobre a maior concentração inimiga.</summary>
public class NukeTests
{

    [Fact]
    public void Strike_DevastatesRegion_AndCostsStabilityBothSides()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        var c = w.Countries[1]; var t = w.Countries[2];
        c.Nukes = 1; c.Stability = 60f; t.Stability = 60f;
        var d = TestWorld.AddDivision(w, 1, 2, TestWorld.Inf, 5, org: 50f, hp: 100f);
        w.Regions[5].Infrastructure = 1.5f; w.Regions[5].Fort = 3;

        var cmd = new NuclearStrikeCommand(1, 5);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Equal(0, c.Nukes);
        Assert.Equal(100f * w.Rule("nuke_div_hp_mult", 0.3f), d.Hp, 0.01f);
        Assert.Equal(50f * w.Rule("nuke_div_org_mult", 0.2f), d.Org, 0.01f);
        Assert.Equal(1.5f * w.Rule("nuke_infra_mult", 0.5f), w.Regions[5].Infrastructure, 0.01f);
        Assert.Equal(1, w.Regions[5].Fort);
        Assert.Equal(60f - w.Rule("nuke_stability_hit", 10f), t.Stability, 0.01f);
        Assert.Equal(w.Rule("nuke_exhaustion", 5f), t.WarExhaustion, 0.01f);
        Assert.Equal(60f - w.Rule("nuke_self_stability_hit", 4f), c.Stability, 0.01f);
    }


}
