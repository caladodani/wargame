using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Comandantes (tabela general): HireGeneralCommand paga e aplica o multiplicador via
/// ApplyTechs; batalhas ganhas dão XP e amplificam o bónus por nível; a IA contrata com folga.</summary>
public class GeneralTests
{
    private static void Def(World w, string id = "g1", string stat = "attack", float mult = 1.10f, float cost = 100f)
        => w.GeneralDefs[id] = new GeneralDef(id, "G " + id, stat, mult, cost);

    [Fact]
    public void Hire_PaysAndAppliesMult()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Def(w);
        var c = w.Countries[1]; c.Money = 150f;
        float before = c.Stat("attack");
        var cmd = new HireGeneralCommand(1, "g1");
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(50f, c.Money, 0.01f);
        Assert.Equal(before * 1.10f, c.Stat("attack"), 0.001f);
        Assert.NotNull(cmd.Validate(w));   // já contratado
    }

    [Fact]
    public void Hire_RespectsSlotsAndMoney()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["general_slots"] = 1f;
        Def(w, "g1"); Def(w, "g2");
        var c = w.Countries[1]; c.Money = 50f;
        Assert.NotNull(new HireGeneralCommand(1, "g1").Validate(w));   // sem dinheiro
        c.Money = 500f;
        new HireGeneralCommand(1, "g1").Execute(w);
        Assert.NotNull(new HireGeneralCommand(1, "g2").Validate(w));   // quadro cheio
    }

    [Fact]
    public void WonBattle_GrantsXp_AndLevelAmplifiesBonus()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new CombatSystem());
        w.Rules["general_xp_per_win"] = 10f;   // 1 vitória = 1 nível
        Def(w, "g1", "attack", 1.10f);
        w.StartWar(1, 2);
        var c = w.Countries[1]; c.Money = 200f;
        new HireGeneralCommand(1, "g1").Execute(w);
        float lvl0 = c.Stat("attack");

        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4, org: 100f, hp: 100f);
        var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf, 4, org: 11f, hp: 5f);
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
        w.ActiveBattles.Add(b);
        TestWorld.Days(w, 10);

        Assert.Empty(w.ActiveBattles);
        Assert.Equal(10f, c.Generals["g1"], 0.01f);
        // nível 1 com general_level_bonus 0.25: 1 + 0.10×1.25 = 1.125 (vs 1.10 no nível 0)
        Assert.True(c.Stat("attack") > lvl0 + 0.01f);
    }

    [Fact]
    public void Ai_HiresCheapestWhenRich()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new AiSystem());
        Def(w, "caro", "attack", 1.10f, 300f); Def(w, "barato", "defense", 1.05f, 80f);
        var c = w.Countries[1]; c.Money = 3000f;
        TestWorld.Days(w, 1);
        Assert.True(c.Generals.ContainsKey("barato"));
        Assert.False(c.Generals.ContainsKey("caro") && c.Generals.Count == 1);
    }
}
