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




}
