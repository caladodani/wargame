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


}
