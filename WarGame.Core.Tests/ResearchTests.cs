using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>ResearchSystem + ResearchTechCommand sobre a árvore de data/seed_tech.sql (custos lidos da tabela).</summary>
public class ResearchTests
{
    private static (World w, Country c) Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ResearchSystem());
        return (w, w.Countries[1]);
    }

    [Fact]
    public void Arvore_carregada_da_tabela()
    {
        var (w, _) = Setup();
        Assert.True(w.Techs.Count >= 20);
        Assert.Equal("inf_1", w.Techs["inf_2"].Requires);
        Assert.Contains(w.TechEffects["log_2"], e => e.Key == "move_speed");
    }

    [Fact]
    public void Conclui_ao_fim_de_cost_dias_e_publica_evento()
    {
        var (w, c) = Setup();
        int days = (int)MathF.Ceiling(w.Techs["inf_1"].Cost);
        TechResearched? got = null; w.Events.Subscribe<TechResearched>(e => got = e);
        Assert.Null(new ResearchTechCommand(1, "inf_1").Validate(w));
        new ResearchTechCommand(1, "inf_1").Execute(w);
        for (int i = 0; i < days - 1; i++) w.Tick();
        Assert.DoesNotContain("inf_1", c.Techs);
        w.Tick();
        Assert.Contains("inf_1", c.Techs);
        Assert.Null(c.ResearchTech);
        Assert.NotNull(got); Assert.Equal("inf_1", got!.TechId);
    }

    [Fact]
    public void Pre_requisito_e_repeticao_sao_recusados()
    {
        var (w, c) = Setup();
        Assert.NotNull(new ResearchTechCommand(1, "inf_2").Validate(w));      // precisa de inf_1
        c.Techs.Add("inf_1");
        Assert.Null(new ResearchTechCommand(1, "inf_2").Validate(w));
        Assert.NotNull(new ResearchTechCommand(1, "inf_1").Validate(w));      // já investigada
        Assert.NotNull(new ResearchTechCommand(1, "xpto").Validate(w));       // inexistente
    }

    [Fact]
    public void Research_speed_do_pais_acelera()
    {
        var (w, c) = Setup();
        c.Stats["research_speed"] = 2f;
        int days = (int)MathF.Ceiling(w.Techs["inf_1"].Cost / 2f);
        new ResearchTechCommand(1, "inf_1").Execute(w);
        for (int i = 0; i < days; i++) w.Tick();
        Assert.Contains("inf_1", c.Techs);
    }

    [Fact]
    public void Tech_effect_multiplica_stat_do_pais()
    {
        var (w, c) = Setup();
        c.Stats["org_regain"] = 1.5f;
        Assert.Equal(1.5f, c.Stat("org_regain"), 3);
        c.Techs.Add("log_1"); w.ApplyTechs(c);
        float expected = 1.5f * w.TechEffects["log_1"].Single(e => e.Key == "org_regain").Mul;
        Assert.Equal(expected, c.Stat("org_regain"), 3);
        Assert.Equal(1f, c.Stat("move_speed"), 3);                             // sem tech nem linha = neutro
    }

    [Fact]
    public void Ia_escolhe_a_mais_barata_disponivel()
    {
        var (w, _) = Setup();
        w.Register(new AiSystem());
        var ai = w.Countries[2]; w.Countries[1].IsPlayer = true;
        int period = Math.Max(1, (int)w.Rule("ai_period_days", 3));
        for (int i = 0; i < period + 1; i++) w.Tick();
        Assert.NotNull(ai.ResearchTech);
        float cheapest = w.Techs.Values.Where(t => t.Requires is null).Min(t => t.Cost);
        Assert.Equal(cheapest, w.Techs[ai.ResearchTech!].Cost);
        Assert.Null(w.Countries[1].ResearchTech);                              // o jogador não é mexido
    }
}
