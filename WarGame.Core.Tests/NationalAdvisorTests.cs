using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Conselheiros próprios de cada país (advisor.country_tag) e a rodagem do gabinete: quem serve há
/// muito tempo vale mais do que quem chegou ontem.</summary>
public class NationalAdvisorTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    [Fact]
    public void EveryCountryFileBringsItsOwnAdvisorsAndTheyAreNotForSaleToTheRest()
    {
        var w = FactionTests.BuildReal();
        var mine = w.AdvisorDefs.Values.Where(a => a.CountryTag == "PRT").ToList();
        Assert.Equal(2, mine.Count);
        Assert.Contains(mine, a => a.Name == "Mestre dos Estaleiros do Tejo" && a.Effects["port_capacity"] == 1.25f);

        var prt = ByTag(w, "PRT");
        var esp = ByTag(w, "ESP");
        Assert.Contains(CabinetSystem.Candidates(w, prt, "economia"), a => a.Id == "PRT_adv_estaleiros");
        Assert.DoesNotContain(CabinetSystem.Candidates(w, esp, "economia"), a => a.Id == "PRT_adv_estaleiros");
        // e os de toda a gente continuam à mão de qualquer um
        Assert.Contains(CabinetSystem.Candidates(w, esp, "economia"), a => a.Id == "adv_industrial");
    }

    [Fact]
    public void ThereIsAtLeastOneOwnAdvisorForEveryCountryWithAFileOfItsOwn()
    {
        var w = FactionTests.BuildReal();
        var byTag = w.AdvisorDefs.Values.Where(a => a.CountryTag is not null).GroupBy(a => a.CountryTag!).ToList();
        Assert.True(byTag.Count >= 28, $"só {byTag.Count} países com conselheiros próprios");
        foreach (var g in byTag)
        {
            Assert.All(g, a => Assert.True(a.Effects.Count > 0, $"{a.Id} não faz nada"));
            Assert.All(g, a => Assert.Contains(a.Slot, w.CabinetSlots.Select(s => s.Id)));
        }
    }

    [Fact]
    public void AForeignAdvisorIsRefusedAtTheDoor()
    {
        var w = FactionTests.BuildReal();
        var esp = ByTag(w, "ESP");
        esp.Money = esp.Political = 1000f;
        Assert.NotNull(new AppointAdvisorCommand(esp.Id, "PRT_adv_estaleiros").Validate(w));
        Assert.Null(new AppointAdvisorCommand(esp.Id, "ESP_adv_ferrol").Validate(w));
    }

    [Fact]
    public void TheTenureRulesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(365f, w.Rule("advisor_tenure_days"));
        Assert.Equal(0.5f, w.Rule("advisor_tenure_bonus"), 3);
    }

    [Fact]
    public void AManWhoJustArrivedIsWorthWhatHeSaysOnTheTinAndNoMore()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Money = c.Political = 1000f;
        new AppointAdvisorCommand(1, "adv_industrial").Execute(w);
        Assert.Equal(0f, World.CabinetTenure(w, c, "economia"), 3);
        Assert.Equal(1.10f, c.Stat("industry"), 3);
    }

    [Fact]
    public void YearsInTheChairMakeTheSameManWorthMore()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Money = c.Political = 1e6f;
        w.Register(new CabinetSystem());
        new AppointAdvisorCommand(1, "adv_industrial").Execute(w);

        TestWorld.Days(w, 183);                                   // meio ano de casa: meia rodagem
        Assert.Equal(0.5f, World.CabinetTenure(w, c, "economia"), 1);
        Assert.Equal(1.125f, c.Stat("industry"), 2);              // +10% × (1 + 0.5 × 0.5)

        TestWorld.Days(w, 365);                                   // rodado de todo, e não passa daí
        Assert.Equal(1f, World.CabinetTenure(w, c, "economia"), 3);
        Assert.Equal(1.15f, c.Stat("industry"), 3);               // +10% × 1.5
    }

    [Fact]
    public void ChangingTheManThrowsTheTenureAway()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Money = c.Political = 1e6f;
        w.Register(new CabinetSystem());
        new AppointAdvisorCommand(1, "adv_industrial").Execute(w);
        TestWorld.Days(w, 365);
        Assert.Equal(1.15f, c.Stat("industry"), 3);

        new AppointAdvisorCommand(1, "adv_planeador").Execute(w);  // mesma pasta: o rodado sai
        Assert.Equal(0f, World.CabinetTenure(w, c, "economia"), 3);
        Assert.Equal(1.15f, c.Stat("production_speed"), 3);        // o novo vale o que diz a tabela e nada mais
        Assert.Equal(1f, c.Stat("industry"), 3);
    }
}
