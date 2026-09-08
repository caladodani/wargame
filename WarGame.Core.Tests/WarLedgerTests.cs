using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O saldo da guerra (WarLedger). O WarStatsSystem contava regiões tomadas, divisões perdidas e
/// batalhas ganhas desde sempre, e o painel Mundo mostrava "⚔ Alfa vs Beta (12 dias)" e uma barra de blocos
/// de texto com o número de divisões. O que aqui se guarda é que a folha que a substituiu lê os contadores
/// do sistema, decide quem está por cima pela MESMA regra com que o arquivo decide quem ganhou, e mede a
/// força pela fórmula única do jogo — que antes disto estava copiada em seis sítios.</summary>
public class WarLedgerTests
{
    private static (World w, WarInfo war) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        return (w, w.Wars.Values.Single());
    }

    /// <summary>A força de uma divisão é a organização pesada pela saúde — e a do país é a soma da dele e
    /// só da dele.</summary>
    [Fact]
    public void A_forca_e_organizacao_pesada_pela_saude()
    {
        var (w, _) = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, org: 80f, hp: 50f);
        Assert.Equal(80f * 50f / 100f, WarLedger.Strength(d), 3);
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4, org: 100f, hp: 100f);
        Assert.Equal(WarLedger.Strength(d), WarLedger.Strength(w, 1), 3);
        Assert.Equal(100f, WarLedger.Strength(w, 2), 3);
    }

    /// <summary>A tabela mundial pesa o exército pela mesma fórmula: se um dia ela mudar de ideias sobre o
    /// que é força, muda no mesmo dia na folha da guerra.</summary>
    [Fact]
    public void A_tabela_mundial_pesa_a_forca_pela_mesma_formula()
    {
        var (w, _) = Build();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, org: 60f, hp: 40f);
        var soma = w.Divisions.Values.Where(d => d.CountryId == 1).Sum(WarLedger.Strength);
        Assert.Equal(soma, WarLedger.Strength(w, 1), 3);
        Assert.All(w.Divisions.Values, d => Assert.Equal(d.Org * d.Hp / 100f, WarLedger.Strength(d), 3));
    }

    /// <summary>A balança é a fatia da força total que está de um lado: sem tropa nenhuma dos dois, é meio a
    /// meio — uma barra vazia mentiria a dizer que o lado A não vale nada.</summary>
    [Fact]
    public void A_balanca_e_a_fatia_da_forca_de_cada_lado()
    {
        var (w, war) = Build();
        Assert.Equal(0.5f, WarLedger.Balance(w, war), 3);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, org: 100f, hp: 100f);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2, org: 100f, hp: 100f);
        TestWorld.AddDivision(w, 3, 2, TestWorld.Inf2, 4, org: 100f, hp: 100f);
        Assert.Equal(2f / 3f, WarLedger.Balance(w, war), 3);
    }

    /// <summary>As cinco parcelas de um lado estão sempre lá, com chapa, número e explicação — zero baixas é
    /// notícia tão boa como muitas é má, e uma parcela que desaparece a zero mente por omissão.</summary>
    [Fact]
    public void As_cinco_parcelas_de_cada_lado_estao_sempre_la()
    {
        var (w, war) = Build();
        foreach (int id in new[] { war.A, war.B })
        {
            var parts = WarLedger.Parts(w, war, id);
            Assert.Equal(5, parts.Count);
            foreach (var nome in new[] { "divisões", "força", "terra tomada", "divisões perdidas", "batalhas ganhas" })
                Assert.Single(parts, p => p.Name == nome);
            Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Note)));
        }
    }

    /// <summary>As parcelas são os contadores do WarStatsSystem, não uma segunda contagem: mexer no
    /// contador do sistema mexe na folha.</summary>
    [Fact]
    public void As_parcelas_sao_os_contadores_do_sistema()
    {
        var (w, war) = Build();
        war.SideA.RegionsTaken = 3; war.SideA.DivisionsLost = 2; war.SideA.BattlesWon = 5;
        var parts = WarLedger.Parts(w, war, war.A);
        Assert.Equal("3", Assert.Single(parts, p => p.Name == "terra tomada").Value);
        Assert.Equal("2", Assert.Single(parts, p => p.Name == "divisões perdidas").Value);
        Assert.Equal("5", Assert.Single(parts, p => p.Name == "batalhas ganhas").Value);
    }

    /// <summary>Quem está por cima é decidido pela mesma regra do arquivo: quem tomou mais terra. Empate não
    /// dá vencedor nenhum.</summary>
    [Fact]
    public void Quem_esta_por_cima_e_a_regra_do_arquivo()
    {
        var (w, war) = Build();
        Assert.Null(WarLedger.Ahead(w, war));
        war.SideB.RegionsTaken = 4;
        Assert.Equal(war.B, WarLedger.Ahead(w, war));
        Assert.Equal(WarLedger.Snapshot(w, war).Winner, WarLedger.Ahead(w, war));
        war.SideA.RegionsTaken = 4;
        Assert.Null(WarLedger.Ahead(w, war));
    }

    /// <summary>A fotografia a meio da guerra tem a mesma forma da que fica no arquivo quando a paz é
    /// assinada — é o mesmo objecto, com os dias contados até hoje.</summary>
    [Fact]
    public void A_fotografia_e_a_mesma_do_arquivo()
    {
        var (w, war) = Build();
        war.SideA.RegionsTaken = 1; war.SideB.DivisionsLost = 7;
        TestWorld.Days(w, 3);
        var snap = WarLedger.Snapshot(w, war);
        Assert.Equal(war.A, snap.A);
        Assert.Equal(war.B, snap.B);
        Assert.Equal(w.Clock.Day - war.StartDay, snap.Days);
        Assert.Equal(1, snap.Regions(war.A));
        Assert.Equal(7, snap.Losses(war.B));
    }

    /// <summary>O veredicto diz a frente parada quando ninguém tomou nada, e quem está à frente e por
    /// quanto quando alguém tomou.</summary>
    [Fact]
    public void O_veredicto_diz_a_frente_parada_ou_quem_esta_a_frente()
    {
        var (w, war) = Build();
        Assert.Contains("frente parada", WarLedger.Verdict(w, war));
        war.SideA.RegionsTaken = 5; war.SideB.RegionsTaken = 2;
        var vd = WarLedger.Verdict(w, war);
        Assert.Contains(w.Countries[war.A].Name, vd);
        Assert.Contains("3", vd);
        war.SideB.RegionsTaken = 5;
        Assert.Contains("empate a 5", WarLedger.Verdict(w, war));
    }

    /// <summary>As batalhas de hoje são as desta guerra: um combate entre outros dois países não conta
    /// para aqui.</summary>
    [Fact]
    public void As_batalhas_de_hoje_sao_as_desta_guerra()
    {
        var (w, war) = Build();
        Assert.Equal(0, WarLedger.Battles(w, war));
        var r = w.Regions.Values.First(x => x.ControllerId == war.B);
        w.ActiveBattles.Add(new Battle { RegionId = r.Id, AttackerCountryId = war.A, Days = 1 });
        Assert.Equal(1, WarLedger.Battles(w, war));
        // uma batalha em terra de quem ataca não é combate desta guerra
        var meu = w.Regions.Values.First(x => x.ControllerId == war.A);
        w.ActiveBattles.Add(new Battle { RegionId = meu.Id, AttackerCountryId = war.A, Days = 1 });
        Assert.Equal(1, WarLedger.Battles(w, war));
    }

    /// <summary>A linha em palavras diz o mesmo que as chapas dos dois lados — se um dia se afastarem, é
    /// aqui que se sabe.</summary>
    [Fact]
    public void A_linha_diz_o_mesmo_que_as_chapas()
    {
        var (w, war) = Build();
        war.SideA.RegionsTaken = 2;
        string line = WarLedger.Line(w, war);
        foreach (int id in new[] { war.A, war.B })
            foreach (var p in WarLedger.Parts(w, war, id))
                Assert.Contains($"{p.Name} {p.Value}", line);
        Assert.Contains(WarLedger.Verdict(w, war), line);
    }

    /// <summary>Toda a chapa da folha é uma chapa que o Glyph sabe desenhar.</summary>
    [Fact]
    public void Todas_as_chapas_da_folha_existem()
    {
        var (w, war) = Build();
        foreach (int id in new[] { war.A, war.B })
            foreach (var p in WarLedger.Parts(w, war, id))
                Assert.Contains(p.Glyph, GlyphDataTests.Desenhados);
    }
}
