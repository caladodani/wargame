using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A conta da encomenda (ProductionPlan). O painel dizia "~14 dias" a partir de uma cópia da conta
/// do ProductionSystem escrita no próprio painel: duas cópias afastam-se, e quando se afastam é o jogador
/// que aprende a jogar um jogo que não existe. Agora a conta é uma só, e é isso que aqui se guarda — o que
/// a ficha promete por dia tem de ser exactamente o que a fábrica gasta nesse dia.</summary>
public class ProductionPlanTests
{
    private static World Build(float money = 100000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ProductionSystem());
        w.Countries[1].Money = money;
        return w;
    }

    private static void Order(World w, int template = TestWorld.Inf)
    {
        var cmd = new BuildDivisionCommand(1, template);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
    }

    /// <summary>O trabalho que a ficha promete para hoje é o que o cofre paga hoje.</summary>
    [Fact]
    public void O_trabalho_do_dia_da_ficha_e_o_que_a_fabrica_gasta()
    {
        var w = Build();
        Order(w);
        var c = w.Countries[1];
        var o = c.Queue[0];
        float prometido = ProductionPlan.DayOutput(w, c, o, ProductionPlan.LinesFor(w, c, 0));
        float antes = o.Progress;
        TestWorld.Days(w, 1);
        Assert.Equal(prometido, o.Progress - antes, 3);
    }

    /// <summary>Os dias que a ficha promete são os dias que a encomenda leva mesmo — com a linha a ganhar
    /// jeito pelo caminho a divisão chega no dia prometido ou antes, nunca depois.</summary>
    [Fact]
    public void A_data_prometida_cumpre_se()
    {
        var w = Build();
        Order(w);
        var c = w.Countries[1];
        int dias = (int)MathF.Ceiling(ProductionPlan.Days(w, c, c.Queue[0], ProductionPlan.LinesFor(w, c, 0)));
        Assert.True(dias > 0);
        TestWorld.Days(w, dias);
        Assert.NotEmpty(w.Divisions);
    }

    /// <summary>Sem fábrica não há data: uma encomenda parada não pode prometer dias nenhuns.</summary>
    [Fact]
    public void Sem_fabrica_a_encomenda_nao_tem_data()
    {
        var w = Build();
        Order(w);
        var c = w.Countries[1];
        Assert.Equal(0f, ProductionPlan.DayOutput(w, c, c.Queue[0], 0));
        Assert.True(ProductionPlan.Days(w, c, c.Queue[0], 0) < 0f);
        Assert.Equal("à espera de fábrica", ProductionPlan.Blocked(w, c, c.Queue[0], 0));
    }

    /// <summary>Mais fábricas, menos dias — e o dobro das fábricas é metade dos dias, que é a promessa que a
    /// chapa das fábricas faz ao dedo que a roda.</summary>
    [Fact]
    public void Duas_fabricas_metade_dos_dias()
    {
        var w = Build();
        Order(w);
        var c = w.Countries[1];
        var o = c.Queue[0];
        Assert.Equal(ProductionPlan.Days(w, c, o, 1) / 2f, ProductionPlan.Days(w, c, o, 2), 3);
    }

    /// <summary>A linha com jeito produz mais por dia do que a linha nova, pelo factor do ritmo.</summary>
    [Fact]
    public void A_linha_com_jeito_sai_mais_depressa()
    {
        var w = Build();
        Order(w);
        var c = w.Countries[1];
        var o = c.Queue[0];
        float nova = ProductionPlan.DayOutput(w, c, o, 1);
        o.Efficiency = 1.5f;
        Assert.Equal(nova * 1.5f, ProductionPlan.DayOutput(w, c, o, 1), 3);
    }

    /// <summary>As fábricas de uma encomenda são as que sobram de quem vem à frente na fila: a primeira leva
    /// as que pediu, e quem chega depois de elas acabarem fica a zero.</summary>
    [Fact]
    public void As_fabricas_sao_as_que_sobram_de_quem_vem_a_frente()
    {
        var w = Build();
        var c = w.Countries[1];
        int fabricas = Industry.Of(w, 1).Military;
        Assert.True(fabricas >= 1);
        Order(w); Order(w);
        c.Queue[0].Factories = fabricas;                 // a da frente leva tudo
        Assert.Equal(fabricas, ProductionPlan.LinesFor(w, c, 0));
        Assert.Equal(0, ProductionPlan.LinesFor(w, c, 1));
        c.Queue[0].Factories = 1;
        Assert.Equal(1, ProductionPlan.LinesFor(w, c, 0));
        Assert.Equal(Math.Min(1, fabricas - 1), ProductionPlan.LinesFor(w, c, 1));
    }

    /// <summary>Encomenda pronta não ocupa linha e não espera fábrica: espera homens.</summary>
    [Fact]
    public void Encomenda_pronta_espera_homens_e_nao_linha()
    {
        var w = Build();
        Order(w);
        var c = w.Countries[1];
        var o = c.Queue[0];
        o.Progress = w.TemplateCost(o.TemplateId);
        c.Manpower = 0f;
        Assert.Equal(0, ProductionPlan.LinesFor(w, c, 0));
        Assert.Equal("à espera de homens", ProductionPlan.Blocked(w, c, o, 0));
        Assert.Equal(0f, ProductionPlan.Days(w, c, o, 0));
    }

    /// <summary>As parcelas multiplicam-se e dão o trabalho de um dia: é a promessa do cartão, e sem ela as
    /// chapas eram sete números soltos.</summary>
    [Fact]
    public void As_parcelas_multiplicadas_dao_o_trabalho_do_dia()
    {
        var w = Build();
        Order(w);
        var c = w.Countries[1];
        var o = c.Queue[0];
        int lines = ProductionPlan.LinesFor(w, c, 0);
        float custo = w.TemplateCost(o.TemplateId);
        float porMultiplicacao = custo / MathF.Max(1f, w.Rule("build_min_days", 10f))
                               * lines * c.Stat("production_speed") * o.Efficiency;
        Assert.Equal(porMultiplicacao, ProductionPlan.DayOutput(w, c, o, lines), 3);

        var parts = ProductionPlan.Parts(w, c, o, lines);
        Assert.Equal(7, parts.Count);
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Glyph)));
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Note)));
        Assert.Contains("custo", ProductionPlan.Why(w, c, o, lines));
    }
}
