using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Gabinete civil: quatro pastas, um conselheiro em cada, nomeação paga de uma vez e salário todos
/// os dias. Quem não tem com que pagar fica sem governo.</summary>
public class CabinetTests
{
    private static World Build(float money = 1000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = money;          // salários: saem do cofre de produção, todos os dias
        w.Countries[1].Political = money;      // nomeação: paga-se com poder político, uma vez (0.3.61)
        return w;
    }

    [Fact]
    public void TheCabinetAndItsAdvisorsComeFromTheDatabase()
    {
        var w = Build();
        Assert.Equal(4, w.CabinetSlots.Count);
        Assert.Equal("economia", w.CabinetSlots[0].Id);          // ordenadas pela coluna sort
        Assert.True(w.AdvisorDefs.Count >= 8);

        var industrial = w.AdvisorDefs["adv_industrial"];
        Assert.Equal("economia", industrial.Slot);
        Assert.Equal(1.10f, industrial.Effects["industry"], 3);
        Assert.Null(industrial.CountryTag);                       // sem país: serve toda a gente
    }

    [Fact]
    public void AppointingSeatsTheManPaysHimAndLiftsTheStat()
    {
        var w = Build();
        Assert.Equal(1f, w.Countries[1].Stat("industry"), 3);

        Assert.Null(new AppointAdvisorCommand(1, "adv_industrial").Validate(w));
        new AppointAdvisorCommand(1, "adv_industrial").Execute(w);

        Assert.Equal("adv_industrial", w.Countries[1].Cabinet["economia"]);
        Assert.Equal(850f, w.Countries[1].Political, 3);          // 1000 menos os 150 da nomeação
        Assert.Equal(1000f, w.Countries[1].Money, 3);             // e o cofre de produção fica intacto: aço não compra ministros
        Assert.Equal(1.10f, w.Countries[1].Stat("industry"), 3);
    }

    [Fact]
    public void AnEmptyTreasuryCannotFormAGovernment()
    {
        var w = Build(10f);
        Assert.Equal("faltam 140 de poder político", new AppointAdvisorCommand(1, "adv_industrial").Validate(w));
    }

    [Fact]
    public void EachDeskTakesOneManAndTheNewOnePushesTheOldOut()
    {
        var w = Build();
        new AppointAdvisorCommand(1, "adv_industrial").Execute(w);
        new AppointAdvisorCommand(1, "adv_planeador").Execute(w);   // mesma pasta: economia

        var c = w.Countries[1];
        Assert.Single(c.Cabinet);
        Assert.Equal("adv_planeador", c.Cabinet["economia"]);
        Assert.Equal(1f, c.Stat("industry"), 3);                   // o antigo já não conta
        Assert.Equal(1.15f, c.Stat("production_speed"), 3);
        Assert.Equal("já está no gabinete", new AppointAdvisorCommand(1, "adv_planeador").Validate(w));
    }

    [Fact]
    public void OneManCanCarryTwoEffectsAndDifferentDesksStack()
    {
        var w = Build();
        new AppointAdvisorCommand(1, "adv_logistico").Execute(w);   // ciência: move_speed e production_speed
        new AppointAdvisorCommand(1, "adv_planeador").Execute(w);   // economia: production_speed

        var c = w.Countries[1];
        Assert.Equal(2, c.Cabinet.Count);
        Assert.Equal(1.10f, c.Stat("move_speed"), 3);
        Assert.Equal(1.15f * 1.05f, c.Stat("production_speed"), 3); // as duas pastas multiplicam-se
    }

    [Fact]
    public void TheWagesComeOutOfTheTreasuryEveryDay()
    {
        var w = Build();
        new AppointAdvisorCommand(1, "adv_industrial").Execute(w);
        Assert.Equal(1.5f, CabinetSystem.Wages(w, w.Countries[1]), 3);   // 1% de 150

        w.Register(new CabinetSystem());
        w.Tick();
        Assert.Equal(998.5f, w.Countries[1].Money, 3);   // só o salário: a nomeação saiu do bolso político
    }

    [Fact]
    public void ACountryThatCannotPayLosesItsGovernment()
    {
        var w = Build();
        new AppointAdvisorCommand(1, "adv_industrial").Execute(w);
        new AppointAdvisorCommand(1, "adv_orador").Execute(w);
        w.Countries[1].Money = 2f;                                  // chega para um salário, não para os dois
        w.Register(new ChronicleSystem());
        w.Register(new CabinetSystem());
        w.Tick();

        var c = w.Countries[1];
        Assert.Single(c.Cabinet);
        Assert.Equal("adv_orador", c.Cabinet["propaganda"]);         // sai primeiro o mais caro
        Assert.Equal(1f, c.Stat("industry"), 3);
        Assert.Equal(0.8f, c.Money, 3);                              // o dia que não se pagou também não se cobrou
        Assert.Contains(w.Chronicle, e => e.Kind == "gabinete" && e.Text.Contains("não há com que lhe pagar"));
    }

    [Fact]
    public void DismissingClearsTheDeskAndTheBonusWithIt()
    {
        var w = Build();
        new AppointAdvisorCommand(1, "adv_teorico").Execute(w);
        Assert.Equal(1.20f, w.Countries[1].Stat("research_speed"), 3);

        Assert.Null(new DismissAdvisorCommand(1, "ciencia").Validate(w));
        new DismissAdvisorCommand(1, "ciencia").Execute(w);
        Assert.Empty(w.Countries[1].Cabinet);
        Assert.Equal(1f, w.Countries[1].Stat("research_speed"), 3);
        Assert.Equal("pasta vazia", new DismissAdvisorCommand(1, "ciencia").Validate(w));
    }

    [Fact]
    public void TheCandidatesOfADeskAreTheOnesThatServeThisCountry()
    {
        var w = Build();
        var list = CabinetSystem.Candidates(w, w.Countries[1], "seguranca");
        Assert.All(list, a => Assert.Equal("seguranca", a.Slot));
        Assert.Contains(list, a => a.Id == "adv_espiao");
        Assert.DoesNotContain(list, a => a.Id == "adv_industrial");
        Assert.True(list[0].Cost <= list[^1].Cost);                 // dos baratos para os caros
    }
}
