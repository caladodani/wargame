using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Veterania: XP por dia de batalha (tecto xp_max), os graus da tabela veterancy — recruta,
/// treinada, veterana, elite — e o bónus de força que cada grau dá em combate. O grau não se guarda em lado
/// nenhum: lê-se do Xp da divisão, e é isso que aqui se prova.</summary>
public class VeterancyTests
{
    [Fact]
    public void BattleDays_GrantXp_Capped()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        w.Register(new CombatSystem());
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
        w.ActiveBattles.Add(b);
        TestWorld.Days(w, 5);
        Assert.True(att.Xp >= 4f, $"xp={att.Xp}");
        Assert.True(att.Xp <= w.Rule("xp_max", 100f));
    }

    [Fact]
    public void Veterans_BeatGreenTroops_OtherThingsEqual()
    {
        // dois duelos idênticos, num deles o defensor é veterano: perde menos org
        float DefOrgAfter(float defXp)
        {
            var (w, _) = TestWorld.Build();
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            w.Register(new CombatSystem());
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
            def.Xp = defXp;
            var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
            b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
            w.ActiveBattles.Add(b);
            TestWorld.Days(w, 3);
            return def.Org;
        }
        Assert.True(DefOrgAfter(100f) > DefOrgAfter(0f));
    }

    /// <summary>Os graus vêm da tabela, e a tabela tem de fazer sentido: começa no zero (toda a tropa tem
    /// grau), e quem tem mais XP nunca vale menos nem traz menos galões do que quem tem menos.</summary>
    [Fact]
    public void Os_graus_vem_da_tabela_e_sobem_todos_no_mesmo_sentido()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        Assert.NotEmpty(w.VeterancyTiers);
        var steps = w.VeterancyTiers.Values.OrderBy(t => t.MinXp).ToList();
        Assert.Equal(0f, steps[0].MinXp);                                   // tropa acabada de fazer também tem grau
        for (int i = 1; i < steps.Count; i++)
        {
            Assert.True(steps[i].MinXp > steps[i - 1].MinXp, $"{steps[i].Id} não vem depois de {steps[i - 1].Id}");
            Assert.True(steps[i].Bonus > steps[i - 1].Bonus, $"{steps[i].Id} não vale mais do que {steps[i - 1].Id}");
            Assert.True(steps[i].Chevrons >= steps[i - 1].Chevrons, $"{steps[i].Id} traz menos galões");
        }
        foreach (var t in steps)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Name), $"grau sem nome: {t.Id}");
            Assert.False(string.IsNullOrWhiteSpace(t.Note), $"grau sem explicação: {t.Id}");
            Assert.Contains(t.Glyph, GlyphDataTests.Desenhados);            // chapa que alguém desenha
        }
        // o topo não pode dar menos do que a recta antiga dava a XP máximo: era essa a força de um veterano
        Assert.Equal(w.Rule("veterancy_bonus", 0.25f), steps[^1].Bonus, 3);
    }

    /// <summary>O grau lê-se do Xp, degrau a degrau: no XP exacto do degrau já se está nele, e um ponto
    /// abaixo ainda se está no de baixo. É a prova de que não há grau guardado nenhum.</summary>
    [Fact]
    public void O_grau_le_se_do_xp_e_muda_no_instante()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var steps = w.VeterancyTiers.Values.OrderBy(t => t.MinXp).ToList();
        foreach (var t in steps)
        {
            d.Xp = t.MinXp;
            Assert.Equal(t.Id, Veterancy.Tier(w, d)!.Id);
            Assert.Equal(t.Bonus, Veterancy.Bonus(w, d), 4);
            Assert.Equal(t.Chevrons, Veterancy.Chevrons(w, d.Xp));
            if (t.MinXp > 0f)
            {
                d.Xp = t.MinXp - 0.5f;                                      // meio ponto abaixo ainda é o grau de baixo
                Assert.NotEqual(t.Id, Veterancy.Tier(w, d)!.Id);
            }
        }
        // e o que falta para o degrau seguinte diz-se; no topo não falta nada
        d.Xp = 0f;
        var (next, missing) = Veterancy.Ahead(w, d.Xp)!.Value;
        Assert.Equal(steps[1].Id, next.Id);
        Assert.Equal(steps[1].MinXp, missing, 3);
        Assert.Contains(next.Name, Veterancy.Line(w, d));
        d.Xp = steps[^1].MinXp;
        Assert.Null(Veterancy.Ahead(w, d.Xp));
    }

    /// <summary>O combate soma o bónus do GRAU e não uma recta: duas divisões com XP diferente dentro do
    /// mesmo degrau batem-se exactamente igual, e a passagem ao degrau seguinte é que se vê. Sem isto, os
    /// graus eram um rótulo por cima de uma conta que continuava contínua.</summary>
    [Fact]
    public void O_combate_soma_o_bonus_do_grau_e_nao_uma_recta()
    {
        var steps = new List<VeterancyDef>();
        float DefOrgAfter(float defXp)
        {
            var (w, db) = TestWorld.Build(); using var _ = db;
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            w.Register(new CombatSystem());
            if (steps.Count == 0) steps.AddRange(w.VeterancyTiers.Values.OrderBy(t => t.MinXp));
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
            def.Xp = defXp;
            var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
            b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
            w.ActiveBattles.Add(b);
            TestWorld.Days(w, 3);
            return def.Org;
        }
        float floorOrg = DefOrgAfter(0f);                                   // (a primeira chamada preenche `steps`)
        var second = steps[1];
        // dez pontos abaixo do degrau: os três dias de batalha rendem XP mas não chegam a fazê-la subir
        Assert.Equal(floorOrg, DefOrgAfter(MathF.Max(0f, second.MinXp - 10f)), 3);
        Assert.True(DefOrgAfter(second.MinXp) > floorOrg, "o degrau seguinte não se sentiu no combate");
    }

    /// <summary>Sem tabela carregada nada disto rebenta: o bónus volta à recta de veterancy_bonus, que é
    /// como o combate media a experiência antes de haver graus.</summary>
    [Fact]
    public void Sem_tabela_o_bonus_volta_a_recta_antiga()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Xp = 50f;
        w.VeterancyTiers.Clear();
        Assert.Null(Veterancy.Tier(w, d));
        Assert.Equal("", Veterancy.Line(w, d));
        Assert.Equal(0, Veterancy.Chevrons(w, d.Xp));
        Assert.Null(Veterancy.Ahead(w, d.Xp));
        Assert.Equal(50f / w.Rule("xp_max", 100f) * w.Rule("veterancy_bonus", 0.25f), Veterancy.Bonus(w, d), 4);
    }

    /// <summary>O contador do mapa mostra uma pilha, não uma divisão: o grau dela é o da experiência média,
    /// que é o que interessa saber de longe.</summary>
    [Fact]
    public void A_pilha_conta_pela_experiencia_media()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w);
        var steps = w.VeterancyTiers.Values.OrderBy(t => t.MinXp).ToList();
        var green = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var old = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1);
        green.Xp = 0f; old.Xp = steps[^1].MinXp * 2f;                       // a média fica no topo ou abaixo dele
        Assert.Equal(Veterancy.Tier(w, (green.Xp + old.Xp) / 2f)!.Id, Veterancy.Stack(w, new[] { green, old })!.Id);
        Assert.Null(Veterancy.Stack(w, System.Array.Empty<Division>()));
    }
}
