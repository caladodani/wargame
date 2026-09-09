using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Condecorações: limiares da tabela medal, bónus de força somado e o que sobrevive ao save.
/// As medalhas vêm da base de dados — os testes usam os ids do seed (baptismo, assalto, campanha, aco, imortais).</summary>
public class MedalTests
{
    private static (World w, Division d) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        return (w, d);
    }

    /// <summary>Avança até um dia múltiplo de medal_check_days e corre o sistema.</summary>
    private static void Award(World w)
    {
        var sys = new MedalSystem();
        int period = Math.Max(1, (int)w.Rule("medal_check_days", 2f));
        for (int i = 0; i <= period; i++) { sys.Tick(w); TestWorld.Days(w, 1); }
    }


    [Fact]
    public void FirstBattle_EarnsTheFirstMedal()
    {
        var (w, d) = Build();
        Award(w);
        Assert.Empty(d.Medals);      // sem batalhas não há condecoração

        d.Battles = 1;
        Award(w);
        Assert.Contains("baptismo", d.Medals);
        Assert.DoesNotContain("aco", d.Medals);
    }







    /// <summary>Estrago feito ao defensor num dia de batalha, num mundo montado de raiz.</summary>
    private static float Dealt(bool withMedal)
    {
        var (w, _) = TestWorld.Build(seed: 7);
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var def = TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 4);
        if (withMedal)
        {
            // medalha de teste com um bónus grande: a diferença tem de sair do ruído do combate
            w.MedalDefs["teste"] = new MedalDef("teste", "Teste", "", "xp", 0f, 5f, 99);
            w.Rules["medal_bonus_max"] = 5f;
            att.Medals.Add("teste");
        }
        w.ActiveBattles.Add(new Battle { RegionId = 4, AttackerCountryId = 1, Attackers = { att.Id }, Defenders = { def.Id } });
        new CombatSystem().Tick(w);
        return 100f - def.Hp;
    }

}
