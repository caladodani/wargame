using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A ficha da nação (NationSheet). O painel do país dizia isto num parágrafo corrido —
/// "Indústria ×1,00   Produção ×1,00   Organização ×1,00   Investigação ×1,00" e por baixo "Divisões 12 ·
/// Regiões 40 · Rendimento 8,4/dia" — e o que aqui se guarda é que a grelha que o substituiu não inventa
/// contas: as fábricas são as do Industry, o cofre é o do EconomySystem, o tecto e o ganho de homens são os
/// do ManpowerSystem, o alvo da estabilidade é o do StabilitySystem e as ranhuras são as do
/// ResearchSystem.</summary>
public class NationSheetTests
{
    private static (World w, Country c) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return (w, w.Countries[1]);
    }

    /// <summary>As parcelas de casa estão sempre lá: um país sem divisões é informação, não é ausência de
    /// informação. Todas trazem chapa, valor e uma frase que diz de onde vem o número.</summary>
    [Fact]
    public void As_parcelas_de_casa_estao_sempre_na_ficha()
    {
        var (w, c) = Build();
        var parts = NationSheet.Parts(w, c);
        foreach (var nome in new[] { "fábricas civis", "fábricas militares", "rendimento", "homens",
                                     "estabilidade", "divisões", "regiões", "laboratórios" })
            Assert.Single(parts, p => p.Name == nome);
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Value)));
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Note)));
        // e o que a ficha diz por palavras é o que as chapas mostram
        Assert.Equal(string.Join(" · ", parts.Select(p => $"{p.Name} {p.Value}")), NationSheet.Line(w, c));
    }

    /// <summary>As três filas de fábricas são as do Industry, à unidade — que é o ponto de não haver duas
    /// contagens de fábricas no jogo.</summary>
    [Fact]
    public void As_fabricas_da_ficha_sao_as_do_Industry()
    {
        var (w, c) = Build();
        var yards = Industry.Of(w, c.Id);
        var parts = NationSheet.Parts(w, c);
        Assert.Equal($"{yards.FreeCivil} de {yards.Civil}", Assert.Single(parts, p => p.Name == "fábricas civis").Value);
        Assert.Equal($"{yards.MilitaryBusy} de {yards.Military}", Assert.Single(parts, p => p.Name == "fábricas militares").Value);
        // estaleiros só quando os há: um país sem portos não leva uma chapa a zero
        Assert.Equal(yards.Naval > 0, parts.Any(p => p.Name == "estaleiros"));
    }

    /// <summary>O rendimento da ficha é o do EconomySystem — o mesmo número que o cofre engorda por dia.</summary>
    [Fact]
    public void O_rendimento_da_ficha_e_o_do_EconomySystem()
    {
        var (w, c) = Build();
        var cofre = Assert.Single(NationSheet.Parts(w, c), p => p.Name == "rendimento");
        Assert.Equal($"{EconomySystem.Income(w, c.Id):0.0}/dia", cofre.Value);
        Assert.Contains($"×{c.Stat("industry"):0.00}", cofre.Note);
    }

    /// <summary>Os homens dizem o tecto e o ganho do ManpowerSystem — e os dois são mesmo os do sistema:
    /// passado um dia, o pool andou o que a ficha disse que andava.</summary>
    [Fact]
    public void Os_homens_dizem_o_tecto_e_o_ganho_do_sistema()
    {
        var (w, c) = Build();
        float pop = ManpowerSystem.Pop(w, c.Id);
        Assert.True(pop > 0f);
        c.Manpower = 0f;
        var homens = Assert.Single(NationSheet.Parts(w, c), p => p.Name == "homens");
        Assert.Equal(NationSheet.People(0f), homens.Value);
        Assert.Contains($"Tecto {NationSheet.People(ManpowerSystem.Cap(w, c, pop))}", homens.Note);

        float ganho = ManpowerSystem.Gain(w, c, pop);
        w.Register(new ManpowerSystem());
        TestWorld.Days(w, 1);
        Assert.Equal(ganho, c.Manpower, 1);
    }

    /// <summary>A estabilidade diz para onde anda, e é para lá que o StabilitySystem a leva: a guerra e a
    /// terra ocupada entram na conta pela mesma fórmula.</summary>
    [Fact]
    public void A_estabilidade_diz_o_alvo_do_sistema()
    {
        var (w, c) = Build();
        var paz = Assert.Single(NationSheet.Parts(w, c), p => p.Name == "estabilidade");
        Assert.Equal($"{c.Stability:0}%", paz.Value);
        Assert.Contains($"para {StabilitySystem.Target(w, c, StabilitySystem.OccupiedShare(w, c.Id)):0}%", paz.Note);

        w.StartWar(1, 2);
        var guerra = Assert.Single(NationSheet.Parts(w, c), p => p.Name == "estabilidade");
        Assert.Contains("guerra(s) pesam", guerra.Note);
        float alvo = StabilitySystem.Target(w, c, StabilitySystem.OccupiedShare(w, c.Id));
        Assert.True(alvo < 50f);
        w.Register(new StabilitySystem());
        TestWorld.Days(w, 200);
        Assert.Equal(alvo, c.Stability, 1);
    }

    /// <summary>A terra ocupada pelo inimigo pesa na estabilidade, e a ficha diz quanto — com a fracção que
    /// o próprio sistema conta.</summary>
    [Fact]
    public void A_terra_ocupada_pesa_e_a_ficha_diz_quanto()
    {
        var (w, c) = Build();
        w.StartWar(1, 2);
        var minha = w.Regions.Values.First(r => r.OwnerId == 1);
        minha.ControllerId = 2;
        float share = StabilitySystem.OccupiedShare(w, c.Id);
        Assert.True(share > 0f);
        var est = Assert.Single(NationSheet.Parts(w, c), p => p.Name == "estabilidade");
        Assert.Contains($"{share:P0} do nosso povo está ocupado", est.Note);
    }

    /// <summary>Os laboratórios são as ranhuras do ResearchSystem, não um número escrito à mão.</summary>
    [Fact]
    public void Os_laboratorios_sao_as_ranhuras_do_ResearchSystem()
    {
        var (w, c) = Build();
        var lab = Assert.Single(NationSheet.Parts(w, c), p => p.Name == "laboratórios");
        Assert.Equal($"{c.Research.Count} de {ResearchSystem.Slots(w, c)}", lab.Value);
        Assert.Contains($"{c.Stat("research_speed"):0.00}", lab.Note);
    }

    /// <summary>O lugar no mundo só aparece depois de a tabela ter corrido uma vez — antes disso não há
    /// lugar nenhum e a ficha não inventa um.</summary>
    [Fact]
    public void O_lugar_no_mundo_so_aparece_depois_da_tabela_correr()
    {
        var (w, c) = Build();
        Assert.DoesNotContain(NationSheet.Parts(w, c), p => p.Name == "no mundo");
        c.PowerRank = 4; c.PowerRankPrev = 7; c.PowerScore = 12.5f;
        var lugar = Assert.Single(NationSheet.Parts(w, c), p => p.Name == "no mundo");
        Assert.Equal("4.º", lugar.Value);
        Assert.Contains($"{12.5f:0.0}", lugar.Note);
        Assert.Contains("Subiu 3", lugar.Note);
    }

    /// <summary>Os recursos são os que o ResourceSystem conta, com a chapa da tabela e o que cada um dá — e
    /// um recurso que não temos não põe chapa nenhuma.</summary>
    [Fact]
    public void Os_recursos_sao_os_do_ResourceSystem()
    {
        var w = FactionTests.BuildReal();
        var c = w.Countries.Values.OrderByDescending(x => w.Regions.Values.Count(r => r.ControllerId == x.Id)).First();
        var res = NationSheet.Resources(w, c);
        foreach (var p in res)
        {
            var def = Assert.Single(w.ResourceDefs.Values, d => d.Name == p.Name);
            Assert.Equal($"{ResourceSystem.Controlled(w, c.Id, def.Id):0}", p.Value);
            Assert.Equal(def.Glyph, p.Glyph);
        }
        foreach (var d in w.ResourceDefs.Values)
            if (ResourceSystem.Controlled(w, c.Id, d.Id) <= 0f)
                Assert.DoesNotContain(res, p => p.Name == d.Name);
    }

    /// <summary>O nome da característica passa por quem sabe traduzi-lo: a ficha não tem uma segunda lista
    /// de nomes escrita em código.</summary>
    [Fact]
    public void O_nome_da_caracteristica_vem_de_fora()
    {
        var w = FactionTests.BuildReal();
        var c = w.Countries.Values.OrderByDescending(x => w.Regions.Values.Count(r => r.ControllerId == x.Id)).First();
        var res = NationSheet.Resources(w, c, k => "NOME-" + k);
        Assert.NotEmpty(res);
        Assert.All(res, p => Assert.Contains("NOME-", p.Note));
    }

    /// <summary>Toda a chapa que a ficha pede é uma chapa que o Glyph sabe desenhar — a mesma guarda das
    /// tabelas, agora para as parcelas escritas em código.</summary>
    [Fact]
    public void Todas_as_chapas_da_ficha_existem()
    {
        var w = FactionTests.BuildReal();
        foreach (var c in w.Countries.Values.Take(40))
        {
            foreach (var p in NationSheet.Parts(w, c))
                Assert.Contains(p.Glyph, GlyphDataTests.Desenhados);
            foreach (var p in NationSheet.Resources(w, c))
                Assert.Contains(p.Glyph, GlyphDataTests.Desenhados);
        }
    }
}
