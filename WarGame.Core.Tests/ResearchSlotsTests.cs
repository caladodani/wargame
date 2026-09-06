using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Ranhuras de investigação (HoI4: research slots): quantas linhas um país aguenta ao mesmo tempo,
/// o que acontece com os laboratórios cheios e o que se perde ao largar uma.</summary>
public class ResearchSlotsTests
{
    private static (World w, Country c) Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ResearchSystem());
        return (w, w.Countries[1]);
    }

    [Fact]
    public void TheNumberOfSlotsComesFromTheDatabase()
    {
        var (w, c) = Setup();
        Assert.Equal(2f, w.Rule("research_slots"));
        Assert.Equal(2, ResearchSystem.Slots(w, c));
        Assert.Equal(2, ResearchSystem.FreeSlots(w, c));
    }

    [Fact]
    public void TwoLinesRunSideBySide()
    {
        var (w, c) = Setup();
        Assert.Null(new ResearchTechCommand(1, "inf_1").Validate(w));
        new ResearchTechCommand(1, "inf_1").Execute(w);
        Assert.Null(new ResearchTechCommand(1, "log_1").Validate(w));
        new ResearchTechCommand(1, "log_1").Execute(w);

        Assert.Equal(2, c.Research.Count);
        Assert.Equal(0, ResearchSystem.FreeSlots(w, c));
        w.Tick();
        Assert.All(c.Research.Values, p => Assert.True(p > 0f));       // as duas andam no mesmo dia
    }

    [Fact]
    public void FullLaboratoriesRefuseAThirdLine()
    {
        var (w, c) = Setup();
        new ResearchTechCommand(1, "inf_1").Execute(w);
        new ResearchTechCommand(1, "log_1").Execute(w);

        Assert.Equal("Laboratórios cheios: larga uma investigação primeiro",
                     new ResearchTechCommand(1, "ind_1").Validate(w));
        Assert.Equal(2, c.Research.Count);
    }

    [Fact]
    public void ARicherCountryOpensMoreLaboratories()
    {
        var (w, c) = Setup();
        c.Stats["research_slots"] = 3f;
        Assert.Equal(3, ResearchSystem.Slots(w, c));
        new ResearchTechCommand(1, "inf_1").Execute(w);
        new ResearchTechCommand(1, "log_1").Execute(w);
        Assert.Null(new ResearchTechCommand(1, "ind_1").Validate(w));
    }

    [Fact]
    public void LettingALineGoFreesTheSlotAndLosesTheProgress()
    {
        var (w, c) = Setup();
        new ResearchTechCommand(1, "inf_1").Execute(w);
        w.Tick();
        Assert.True(c.Research["inf_1"] > 0f);

        Assert.Null(new CancelResearchCommand(1, "inf_1").Validate(w));
        new CancelResearchCommand(1, "inf_1").Execute(w);
        Assert.Empty(c.Research);

        new ResearchTechCommand(1, "inf_1").Execute(w);
        Assert.Equal(0f, c.Research["inf_1"]);                          // volta ao princípio
    }

    [Fact]
    public void LettingGoWhatIsNotThereIsRefused()
    {
        var (w, _) = Setup();
        Assert.Equal("Essa tecnologia não está em investigação", new CancelResearchCommand(1, "inf_1").Validate(w));
    }

    [Fact]
    public void FinishingOneLineFreesItsSlotForTheNext()
    {
        var (w, c) = Setup();
        new ResearchTechCommand(1, "drones_1").Execute(w);                  // 80 dias
        new ResearchTechCommand(1, "res_1").Execute(w);                     // 120 dias: ainda a meio quando a 1ª fecha
        int days = (int)MathF.Ceiling(w.Techs["drones_1"].Cost / c.Stat("research_speed"));
        for (int i = 0; i < days; i++) w.Tick();

        Assert.Contains("drones_1", c.Techs);
        Assert.DoesNotContain("drones_1", c.Research.Keys);
        Assert.Contains("res_1", c.Research.Keys);                       // a linha mais longa continua onde estava
        Assert.Equal(1, ResearchSystem.FreeSlots(w, c));
    }

    [Fact]
    public void TheSameTechDoesNotTakeTwoSlots()
    {
        var (w, _) = Setup();
        new ResearchTechCommand(1, "inf_1").Execute(w);
        Assert.Equal("Já em investigação", new ResearchTechCommand(1, "inf_1").Validate(w));
    }

    [Fact]
    public void TheOldSingleLineFacadeStillWorks()
    {
        var (w, c) = Setup();
        c.ResearchTech = "inf_1";                                       // caminho antigo (save legado, espionagem)
        c.ResearchProgress = 5f;
        Assert.Equal("inf_1", c.ResearchTech);
        Assert.Equal(5f, c.ResearchProgress);
        Assert.Equal(5f, c.Research["inf_1"]);

        c.ResearchTech = null;
        Assert.Empty(c.Research);
    }

    [Fact]
    public void TheAiFillsEverySlotItHas()
    {
        var (w, _) = Setup();
        var ai = w.Countries[2];
        w.Register(new AiSystem());
        w.Tick();
        Assert.Equal(ResearchSystem.Slots(w, ai), ai.Research.Count);
    }
}
