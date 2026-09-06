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
    public void Build_RequiresTech_ThenPaysAndStocks()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1]; c.Money = 1000f;
        Assert.NotNull(new BuildNukeCommand(1).Validate(w));   // sem programa nuclear
        c.Techs.Add("nuc_2"); w.ApplyTechs(c);
        var cmd = new BuildNukeCommand(1);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(1, c.Nukes);
        Assert.Equal(1000f - w.Rule("nuke_cost", 400f), c.Money, 0.01f);
    }

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

    [Fact]
    public void Strike_NeedsWarNukesAndEnemyRegion()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        Assert.NotNull(new NuclearStrikeCommand(1, 5).Validate(w));   // sem ogivas
        c.Nukes = 1;
        Assert.NotNull(new NuclearStrikeCommand(1, 5).Validate(w));   // sem guerra
        w.StartWar(1, 2);
        Assert.NotNull(new NuclearStrikeCommand(1, 2).Validate(w));   // região própria
        Assert.Null(new NuclearStrikeCommand(1, 5).Validate(w));
    }

    [Fact]
    public void Ai_BuildsThenStrikes_BiggestEnemyStack()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new AiSystem());
        w.StartWar(1, 2);
        var c = w.Countries[1];
        c.Techs.Add("nuc_2"); w.ApplyTechs(c);
        c.Money = 5000f;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        for (int i = 2; i <= 4; i++) TestWorld.AddDivision(w, i, 2, TestWorld.Inf, 5);
        TestWorld.AddDivision(w, 5, 2, TestWorld.Inf, 6);

        TestWorld.Days(w, 1);   // dia 0: IA constrói a ogiva
        Assert.Equal(1, c.Nukes);
        float infra5 = w.Regions[5].Infrastructure, infra6 = w.Regions[6].Infrastructure;
        TestWorld.Days(w, 3);   // dia 3: IA lança na região 5 (3 divisões > região 6 com 1)
        Assert.Equal(0, c.Nukes);
        Assert.Equal(infra5 * w.Rule("nuke_infra_mult", 0.5f), w.Regions[5].Infrastructure, 0.01f);
        Assert.Equal(infra6, w.Regions[6].Infrastructure, 0.01f);   // região 6 intacta
    }
}
