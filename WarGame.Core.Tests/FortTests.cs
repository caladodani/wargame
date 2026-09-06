using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Fortificações: obra, bónus defensivo, dano na captura e IA na frente.</summary>
public class FortTests
{
    [Fact]
    public void Build_FinishesAfterDays_CapsAtMax()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ConstructionSystem());
        var c = w.Countries[1]; c.Money = 500f;
        var cmd = new BuildFortCommand(1, 1);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(500f - w.Rule("fort_build_cost", 30f), c.Money, 0.01f);
        TestWorld.Days(w, (int)w.Rule("fort_build_days", 20f));
        Assert.Equal(1, w.Regions[1].Fort);
        Assert.False(w.Regions[1].FortBuilding);

        w.Regions[1].Fort = (int)w.Rule("fort_max", 5f);
        Assert.NotNull(new BuildFortCommand(1, 1).Validate(w));
    }

    [Fact]
    public void FortMultiplier_MakesDefendersHitHarder()
    {
        // dois combates idênticos, um com fortMult 1.75: os atacantes sofrem mais dano de org
        float AttackerOrgAfter(float fortMult)
        {
            var (w, _) = TestWorld.Build(seed: 7);
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            var att = new List<Division> { TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3) };
            var def = new List<Division> { TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4) };
            var ctx = new ModContext().With("terrain", "plain").With("country", "A");
            var sys = new CombatSystem();
            for (int i = 0; i < 5; i++) sys.ResolveTick(w, att, def, ctx, ctx, fortMult);
            return att[0].Org;
        }
        Assert.True(AttackerOrgAfter(1.75f) < AttackerOrgAfter(1f));
    }

    [Fact]
    public void Capture_RemovesOneLevel()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var r = w.Regions[4];
        r.Fort = 3; r.FortBuilding = true; r.FortProgress = 4f;
        CombatSystem.CaptureDamage(w, r);
        Assert.Equal(2, r.Fort);
        Assert.False(r.FortBuilding);
    }

    [Fact]
    public void Ai_AtWar_FortifiesFrontRegion()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new AiSystem());
        var c = w.Countries[2]; c.Money = 500f;
        w.StartWar(1, 2);
        TestWorld.Days(w, 1);
        Assert.True(w.Regions[4].FortBuilding);   // 4 é a região da frente do país 2
    }
}
