using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A chapa da batalha: quem está a levar a melhor, por quanto, e sempre lido do lado de quem
/// está a olhar para o mapa.</summary>
public class BattleOddsTests
{
    private static (World w, Battle b) Fight(float attackerOrg = 100f, float defenderOrg = 100f,
                                             int attackers = 1, int defenders = 1)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["fog_of_war"] = 0f;
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        int id = 1;
        for (int i = 0; i < attackers; i++)
        {
            TestWorld.AddDivision(w, id, 1, 1, 4, attackerOrg);
            b.Attackers.Add(id++);
        }
        for (int i = 0; i < defenders; i++)
        {
            TestWorld.AddDivision(w, id, 2, 1, 4, defenderOrg);
            b.Defenders.Add(id++);
        }
        w.ActiveBattles.Add(b);
        return (w, b);
    }

    /// <summary>Força igual dos dois lados: a batalha não pende para lado nenhum e a chapa sai amarela.</summary>
    [Fact]
    public void Batalha_igual_sai_renhida()
    {
        var (w, b) = Fight();
        Assert.Equal(0f, BattleOdds.Tilt(w, b), 3);
        Assert.Equal(0, BattleOdds.Mood(w, b, 1));
        Assert.Equal(0, BattleOdds.Number(w, b, 1));
    }

    /// <summary>Mais gente e mais organização do lado de quem ataca: verde para o atacante, vermelho para
    /// quem está a defender. É a mesma batalha lida de dois sítios.</summary>
    [Fact]
    public void A_mesma_batalha_le_se_ao_contrario_de_cada_lado()
    {
        var (w, b) = Fight(attackers: 3);
        Assert.True(BattleOdds.Tilt(w, b) > 0f);
        Assert.Equal(1, BattleOdds.Mood(w, b, 1));
        Assert.Equal(-1, BattleOdds.Mood(w, b, 2));
        Assert.Equal(BattleOdds.Number(w, b, 1), BattleOdds.Number(w, b, 2));   // o número é o mesmo; a cor é que muda
    }

    /// <summary>Uma divisão desfeita conta pouco mesmo com o efectivo cheio: a organização pesa na força.</summary>
    [Fact]
    public void A_organizacao_pesa_na_forca()
    {
        var (w, b) = Fight(attackerOrg: 10f);
        Assert.True(BattleOdds.Tilt(w, b) < 0f);
        Assert.Equal(-1, BattleOdds.Mood(w, b, 1));

        w.Rules["battle_org_weight"] = 0f;                     // a regra manda: sem peso da organização, empata
        Assert.Equal(0f, BattleOdds.Tilt(w, b), 3);
    }

    /// <summary>O número vai de 0 a 99 e nunca sai fora: uma batalha só com um lado é 99, não 100.</summary>
    [Fact]
    public void O_numero_da_chapa_nunca_sai_do_mostrador()
    {
        var (w, b) = Fight();
        b.Defenders.Clear();
        Assert.Equal(99, BattleOdds.Number(w, b, 1));
        b.Attackers.Clear();
        Assert.Equal(0, BattleOdds.Number(w, b, 1));           // ninguém de nenhum lado: não pende
    }

    /// <summary>A ordem do mapa é a das batalhas que ainda podem virar: primeiro as renhidas.</summary>
    [Fact]
    public void As_batalhas_renhidas_vem_primeiro()
    {
        var (w, b) = Fight();
        var decided = new Battle { RegionId = 5, AttackerCountryId = 1 };
        TestWorld.AddDivision(w, 50, 1, 1, 5);
        decided.Attackers.Add(50);
        w.ActiveBattles.Add(decided);
        var shown = BattleOdds.Shown(w, 1);
        Assert.Equal(4, shown[0].RegionId);
        Assert.Equal(5, shown[1].RegionId);
    }

    /// <summary>Batalha em terra que o jogador não vê não aparece no mapa: uma guerra entre terceiros do
    /// outro lado do mundo é notícia, não é chapa.</summary>
    [Fact]
    public void Batalha_no_nevoeiro_nao_aparece()
    {
        var (w, _) = Fight();
        w.ActiveBattles.Clear();
        foreach (var d in w.Divisions.Values.ToList()) w.RemoveDivision(d.Id);

        var far = new Battle { RegionId = 6, AttackerCountryId = 2 };      // longe da nossa terra (1..3)
        TestWorld.AddDivision(w, 90, 2, 1, 6);
        far.Attackers.Add(90);
        w.ActiveBattles.Add(far);
        Assert.Single(BattleOdds.Shown(w, 1));                             // sem nevoeiro vê-se tudo

        w.Rules["fog_of_war"] = 1f;
        Assert.Empty(BattleOdds.Shown(w, 1));
    }
}
