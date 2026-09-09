using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Tácticas de combate: de quatro em quatro dias cada lado escolhe como se bate, a tabela diz quem
/// lê quem, e quem é lido perde quase tudo o que a esperteza lhe valia. O sorteio é determinista — o mesmo
/// dia no mesmo mundo dá sempre as mesmas tácticas — e a ponta de lança só rola em terreno aberto, como a
/// emboscada só espera no arvoredo.</summary>
public class TacticsTests
{
    /// <summary>Mapa em linha com o chão à escolha, já com as tácticas devolvidas ao mundo.</summary>
    private static World Map(string terrain = "plain", int n = 6)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.Tactic(w);
        TestWorld.LinearMap(w, n, terrain: terrain);
        return w;
    }

    /// <summary>Todas as tácticas que este lado escolheu, em todas as regiões, ao longo de tantos dias.</summary>
    private static HashSet<string> Over(World w, int days, bool attacking)
    {
        var seen = new HashSet<string>();
        for (int d = 0; d < days; d++)
        {
            foreach (var r in w.Regions.Values)
                if (Tactics.Of(w, r, attacking) is TacticDef t) seen.Add(t.Id);
            w.Tick();
        }
        return seen;
    }








    /// <summary>A táctica não é um enfeite do ecrã: com ela o assalto do dia faz mais estrago do que sem ela,
    /// no mesmo mundo, com a mesma tropa e a mesma sorte (o dado é semeado e gasta-se na mesma ordem).</summary>
    [Fact]
    public void TheTacticReachesTheBattle()
    {
        float DefOrgAfterOneDay(bool withTactics)
        {
            var (w, _) = TestWorld.Build();
            if (withTactics) TestWorld.Tactic(w);
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            w.Register(new CombatSystem());
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
            var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
            b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
            w.ActiveBattles.Add(b);
            TestWorld.Days(w, 1);
            return def.Org;
        }
        Assert.True(DefOrgAfterOneDay(true) < DefOrgAfterOneDay(false),
            "o assalto com táctica não custou mais ao defensor do que o assalto sem nenhuma");
    }



}
