using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Comandantes nacionais de asa e de esquadra. O estado-maior já tinha três armas, mas os homens
/// de casa eram todos de terra: qualquer país chamava o mesmo chefe de caça e o mesmo almirante que o
/// vizinho. Agora cada um dos 28 países traz também um comandante de asa e um de esquadra que são só dele
/// — o Chefe da Patrulha Atlântica e o Almirante da Costa Atlântica em Portugal, a Matilha na Alemanha, as
/// Lanchas do Golfo no Irão — com selo ⚜, chapa própria, e pagos com o dinheiro E com a experiência da arma.
///
/// A regra que isto respeita: um de casa por arma (dois disputavam a mesma cadeira e o segundo nunca era
/// chamado), e o de casa tem de ser mais exigente do que o comum que substitui, senão ninguém o chamava.
///
/// Leem a base de dados a sério (data/static.db), que é onde os comandantes vivem.</summary>
public class NationalAirNavalCommanderTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    /// <summary>O que cada arma pode comandar — a mesma tabela do tools/check_countries.py.</summary>
    private static readonly Dictionary<string, string[]> ArmStats = new()
    {
        [World.Air] = new[] { "air_losses", "air_bombing", "air_upkeep" },
        [World.Sea] = new[] { "naval_losses", "naval_upkeep", "naval_blockade", "naval_escort", "naval_patrol" },
    };

    [Fact]
    public void EveryCountryBringsOneCommanderOfTheAirAndOneOfTheSeaOfItsOwn()
    {
        var w = FactionTests.BuildReal();
        foreach (var domain in new[] { World.Air, World.Sea })
        {
            var own = w.GeneralDefs.Values.Where(g => g.CountryTag is not null && g.Domain == domain).ToList();
            Assert.Equal(28, own.Count);
            Assert.Equal(28, own.Select(g => g.CountryTag).Distinct().Count());   // um por país, nunca dois
            foreach (var g in own)
            {
                Assert.StartsWith($"{g.CountryTag}_{domain}_gen_", g.Id);
                Assert.NotEqual("", g.Icon);
                Assert.NotEqual("", g.Note);                                      // a folha de serviço do retrato
                Assert.Contains(g.StatKey, ArmStats[domain]);
                bool cheaper = g.StatKey.EndsWith("_losses") || g.StatKey.EndsWith("_upkeep");
                Assert.True(cheaper ? g.Mult < 1f : g.Mult > 1f, $"{g.Id}: {g.StatKey}={g.Mult} com o sinal trocado");
            }
        }
        Assert.Equal(112, w.GeneralDefs.Values.Count(g => g.CountryTag is not null));   // 28 × 3 armas + 28 de terra
    }

    [Fact]
    public void TheHomeCommanderOfEachArmIsDearerThanTheMercenariesHeReplaces()
    {
        var w = FactionTests.BuildReal();
        foreach (var domain in new[] { World.Air, World.Sea })
        {
            float mercCost = w.GeneralDefs.Values.Where(g => g.CountryTag is null && g.Domain == domain).Max(g => g.Cost);
            float mercXp = w.GeneralDefs.Values.Where(g => g.CountryTag is null && g.Domain == domain).Max(g => g.Xp);
            foreach (var g in w.GeneralDefs.Values.Where(g => g.CountryTag is not null && g.Domain == domain))
            {
                Assert.True(g.Cost > mercCost, $"{g.Id} ({g.Cost}) não custa mais do que o mercenário ({mercCost})");
                Assert.True(g.Xp > mercXp, $"{g.Id} pede {g.Xp} de experiência, o mercenário já pedia {mercXp}");
            }
        }
    }

    [Fact]
    public void NoCountryGivesTwoCommandersTheSameFaceplate()
    {
        // duas chapas iguais no mesmo estado-maior e os dois retratos passavam a ser o mesmo homem
        var w = FactionTests.BuildReal();
        foreach (var tag in w.GeneralDefs.Values.Where(g => g.CountryTag is not null).Select(g => g.CountryTag!).Distinct())
        {
            var icons = w.GeneralDefs.Values.Where(g => g.CountryTag == tag).Select(g => g.Icon).ToList();
            Assert.Equal(icons.Count, icons.Distinct().Count());
        }
    }

    [Fact]
    public void NobodyElseCanHireThem()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Money = 5000f; prt.AirXp = prt.NavyXp = 400f;

        Assert.Equal("esse comandante é de outro país", new HireGeneralCommand(prt.Id, "DEU_ar_gen_caca_deu").Validate(w));
        Assert.Equal("esse comandante é de outro país", new HireGeneralCommand(prt.Id, "GBR_mar_gen_escolta_gbr").Validate(w));
        Assert.Null(new HireGeneralCommand(prt.Id, "PRT_ar_gen_patrulha_prt").Validate(w));
        Assert.Null(new HireGeneralCommand(prt.Id, "PRT_mar_gen_costa_prt").Validate(w));
        Assert.DoesNotContain(w.GeneralPool(prt), g => g.CountryTag == "DEU");
    }

    [Fact]
    public void TheHomeWingCommanderIsPaidFromTheAirPocketAndTakesAnAirChair()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Money = 5000f; prt.AirXp = 100f; prt.NavyXp = 100f; prt.ArmyXp = 100f;
        var def = w.GeneralDefs["PRT_ar_gen_patrulha_prt"];

        var cmd = new HireGeneralCommand(prt.Id, def.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Contains(def.Id, prt.Generals);
        Assert.Equal(100f - def.Xp, prt.AirXp, 2);
        Assert.Equal(100f, prt.NavyXp, 2);                       // o mar não pagou nada disto
        Assert.Equal(100f, prt.ArmyXp, 2);
        Assert.Equal(1, w.GeneralsInService(prt, World.Air));
        Assert.Equal(0, w.GeneralsInService(prt, World.Land));
        Assert.Equal(def.Mult, prt.Stat("air_losses", 1f), 3);   // é o que o AirMissionSystem pergunta
        Assert.Equal(1f, prt.Stat("naval_escort", 1f), 3);
    }

    [Fact]
    public void ACommanderFromAnotherCountryInTheRollIsWorthNothing()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        // um save antigo (ou uma tag trocada) com a matilha alemã na folha: o motor não lhe conta o bónus
        prt.Generals.Add("DEU_mar_gen_matilha_deu");
        Assert.Equal(1f, prt.Stat("naval_blockade", 1f), 3);
    }

    [Fact]
    public void TheRosterOfEachArmPutsTheHomeManFirst()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var pool = w.GeneralPool(prt);

        Assert.Equal(15, pool.Count);                            // 7 de terra + 4 de asa + 4 de esquadra
        foreach (var domain in World.Domains)
        {
            var arm = pool.Where(g => g.Domain == domain).ToList();
            Assert.NotEmpty(arm);
            Assert.Equal("PRT", arm[0].CountryTag);              // o de casa abre a folha da arma
            Assert.All(arm.Where(g => g.CountryTag is not null), g => Assert.Equal("PRT", g.CountryTag));
        }
        Assert.Equal(4, pool.Count(g => g.Domain == World.Air));
        Assert.Equal(4, pool.Count(g => g.Domain == World.Sea));
    }

    [Fact]
    public void TheAiHiresItsOwnAdmiralBeforeTheMercenaryOne()
    {
        var w = FactionTests.BuildReal();
        var rus = ByTag(w, "RUS");
        rus.Money = 20000f; rus.NavyXp = 200f; rus.AirXp = 0f;
        w.Register(new AiSystem());

        for (int i = 0; i < 40; i++) w.Tick();

        // enche primeiro o comando de terra (é o que a guerra pede) e, chegado ao mar, chama o seu antes
        // do mercenário mais barato
        Assert.Contains("RUS_mar_gen_norte_rus", rus.Generals);
        Assert.Equal(3, w.GeneralsInService(rus, World.Land));
        Assert.Equal(0, w.GeneralsInService(rus, World.Air));    // sem horas de voo não nomeia ninguém de asa
        Assert.All(rus.Generals, id => Assert.True(World.GeneralIsFor(w.GeneralDefs[id], rus),
                                                   $"a IA chamou {id}, que não é da Rússia"));
        Assert.True(w.GeneralsInService(rus, World.Sea) <= w.GeneralSlots(World.Sea));
    }
}
