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

    [Fact]
    public void TheTacticsComeFromTheDatabase()
    {
        var w = Map();
        Assert.NotEmpty(w.TacticDefs);
        Assert.Contains(w.TacticDefs.Values, t => t.Side == "attacker");
        Assert.Contains(w.TacticDefs.Values, t => t.Side == "defender");
        Assert.Equal(4f, w.Rule("tactic_days", 0f), 3);
        Assert.Equal(0.25f, w.Rule("tactic_counter_keep", 0f), 3);
    }

    /// <summary>O grafo das leituras tem de fechar: quem lê alguém lê alguém que existe, do outro lado, e que
    /// pode mesmo aparecer no mesmo chão — uma leitura entre duas tácticas que nunca se encontram é uma linha
    /// de tabela que não faz nada.</summary>
    [Fact]
    public void EveryCounterPointsAtSomethingItCanActuallyMeet()
    {
        var w = Map();
        foreach (var t in w.TacticDefs.Values)
        {
            if (t.CounterId.Length == 0) continue;
            Assert.True(w.TacticDefs.TryGetValue(t.CounterId, out var foe), $"{t.Id} lê {t.CounterId}, que não existe");
            Assert.NotEqual(t.Side, foe!.Side);
            Assert.True(t.Terrain.Length == 0 || foe.Terrain.Length == 0 || t.Terrain == foe.Terrain,
                $"{t.Id} ({t.Terrain}) lê {foe.Id} ({foe.Terrain}) e nunca se encontram");
        }
    }

    [Fact]
    public void EachSideOnlyPicksItsOwn()
    {
        var w = Map();
        for (int d = 0; d < 60; d++)
        {
            foreach (var r in w.Regions.Values)
            {
                Assert.Equal("attacker", Tactics.Of(w, r, attacking: true)!.Side);
                Assert.Equal("defender", Tactics.Of(w, r, attacking: false)!.Side);
            }
            w.Tick();
        }
    }

    [Fact]
    public void ATacticHoldsForDaysAndThenChanges()
    {
        var w = Map();
        var r = w.Regions[4];
        string today = Tactics.Of(w, r, attacking: true)!.Id;
        TestWorld.Days(w, 1);
        Assert.Equal(today, Tactics.Of(w, r, attacking: true)!.Id);   // o bloco dura tactic_days

        var seen = new HashSet<string>();
        for (int d = 0; d < 200; d++) { seen.Add(Tactics.Of(w, r, attacking: true)!.Id); w.Tick(); }
        Assert.True(seen.Count > 1, "a mesma táctica 200 dias seguidos");
    }

    [Fact]
    public void TheTwoSidesDoNotChooseTogether()
    {
        var w = Map();
        int apart = 0;
        for (int d = 0; d < 60; d++)
        {
            var r = w.Regions[4];
            if (Tactics.Of(w, r, attacking: true)!.Sort != Tactics.Of(w, r, attacking: false)!.Sort) apart++;
            w.Tick();
        }
        Assert.True(apart > 0, "os dois lados escolheram sempre a mesma coisa");
    }

    [Fact]
    public void TheSpearheadOnlyRollsOnOpenGroundAndTheAmbushOnlyInTheWoods()
    {
        Assert.Contains("ponta", Over(Map("plain"), 200, attacking: true));
        Assert.DoesNotContain("ponta", Over(Map("forest"), 200, attacking: true));
        Assert.Contains("emboscada", Over(Map("forest"), 200, attacking: false));
        Assert.DoesNotContain("emboscada", Over(Map("plain"), 200, attacking: false));
    }

    [Fact]
    public void ATacticThatIsReadLosesMostOfItsEdge()
    {
        var w = Map();
        bool found = false;
        for (int d = 0; d < 400 && !found; d++)
        {
            foreach (var r in w.Regions.Values)
            {
                foreach (bool attacking in new[] { true, false })
                {
                    if (!Tactics.Countered(w, r, attacking)) continue;
                    var mine = Tactics.Of(w, r, attacking)!;
                    var foe = Tactics.Of(w, r, !attacking)!;
                    Assert.Equal(mine.Id, foe.CounterId);
                    Assert.Equal(1f + (mine.Mult - 1f) * w.Rule("tactic_counter_keep", 0.25f),
                                 Tactics.Mult(w, r, attacking), 3);
                    Assert.True(Tactics.Mult(w, r, attacking) < mine.Mult, "ser lido não custou nada");
                    found = true;
                    break;
                }
                if (found) break;
            }
            if (!found) w.Tick();
        }
        Assert.True(found, "ninguém leu ninguém em 400 dias");
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

    /// <summary>E o ecrã de batalha conta a mesma história com a mesma conta: a parcela que ele mostra é o
    /// multiplicador com que o dia se bateu, não uma segunda versão dele.</summary>
    [Fact]
    public void TheBattleScreenNamesTheTactic()
    {
        var w = Map();
        w.StartWar(1, 2);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);
        var r = w.Regions[4];
        var side = CombatSystem.Explain(w, r, new List<Division> { att }, attacking: true, 1, 2);
        var tac = Tactics.Of(w, r, attacking: true)!;
        var part = side.Factors.Single(f => f.Name.StartsWith("táctica: "));
        Assert.Contains(tac.Name, part.Name);
        Assert.Equal(Tactics.Mult(w, r, attacking: true), part.Mult, 3);
    }

    [Fact]
    public void TheRegionSheetSaysWhatEachSideIsTrying()
    {
        var w = Map();
        w.StartWar(1, 2);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
        w.ActiveBattles.Add(b);
        var r = w.Regions[4];
        string line = RegionState.BattleLine(w, r)!;
        Assert.Contains("contra", line);
        Assert.Contains(Tactics.Of(w, r, attacking: true)!.Name, line);
        Assert.Contains(Tactics.Of(w, r, attacking: false)!.Name, line);
    }

    [Fact]
    public void WithoutTheTableTheBattleIsFoughtLikeBefore()
    {
        var (w, _) = TestWorld.Build();          // o Build limpa-as, como aos céus
        TestWorld.LinearMap(w);
        var r = w.Regions[1];
        Assert.Empty(w.TacticDefs);
        Assert.Null(Tactics.Of(w, r, attacking: true));
        Assert.Equal(1f, Tactics.Mult(w, r, attacking: true), 3);
        Assert.False(Tactics.Countered(w, r, attacking: true));
        Assert.Equal("", Tactics.Line(w, r));
        Assert.Null(Tactics.Plate(w, r, attacking: true));
    }
}
