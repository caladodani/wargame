using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Comandantes de casa (general.country_tag). Até aqui os cinco comandantes eram os mesmos para toda
/// a gente: o Mestre da ofensiva de Portugal era o Mestre da ofensiva da Rússia, e o estado-maior de qualquer
/// país lia-se igual. Agora cada país traz dois comandantes que só ele chama — o General Inverno é russo, os
/// Comandos são portugueses — e o que se mede aqui é que existem, que valem mais do que os mercenários, que
/// ninguém de fora lhes toca e que a IA prefere os seus.
///
/// Leem a base de dados a sério (data/static.db), que é onde os comandantes vivem.</summary>
public class NationalGeneralTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    [Fact]
    public void EveryCountryBringsItsOwnCommandersAndTheMercenariesStayForEveryone()
    {
        var w = FactionTests.BuildReal();
        // os mercenários de terra são os cinco de sempre; a asa e a esquadra têm os seus, à parte
        var mercs = w.GeneralDefs.Values.Where(g => g.CountryTag is null && g.Domain == World.Land).ToList();
        Assert.Equal(5, mercs.Count);
        Assert.Equal(6, w.GeneralDefs.Values.Count(g => g.CountryTag is null && g.Domain != World.Land));
        Assert.All(mercs, g => Assert.NotEqual("", g.Icon));

        var home = w.GeneralDefs.Values.Where(g => g.CountryTag is not null && g.Domain == World.Land).ToList();
        Assert.Equal(56, home.Count);                       // 28 países × 2 de terra
        Assert.Equal(56, w.GeneralDefs.Values.Count(g => g.CountryTag is not null && g.Domain != World.Land));
        foreach (var g in home)
        {
            Assert.StartsWith(g.CountryTag + "_", g.Id);
            Assert.NotEqual("", g.Icon);
            Assert.NotEqual("", g.Note);                    // a folha de serviço é o que o retrato mostra
            Assert.InRange(g.Mult, 1.05f, 1.20f);
        }
        Assert.Equal(28, home.Select(g => g.CountryTag).Distinct().Count());
    }

    [Fact]
    public void TheRosterOfACountryIsItsOwnMenFirstAndThenTheMercenaries()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var pool = w.GeneralPool(prt);
        var land = pool.Where(g => g.Domain == World.Land).ToList();

        Assert.Equal(15, pool.Count);                       // as três armas: 7 de terra + 4 de asa + 4 de esquadra
        Assert.Equal(7, land.Count);                        // 2 de casa + 5 mercenários
        Assert.All(land.Take(2), g => Assert.Equal("PRT", g.CountryTag));
        Assert.All(land.Skip(2), g => Assert.Null(g.CountryTag));
        Assert.DoesNotContain(pool, g => g.CountryTag == "RUS");
        // e o russo tem os dele, que não são os nossos
        Assert.Contains(w.GeneralPool(ByTag(w, "RUS")), g => g.Id == "RUS_gen_inverno");
    }

    [Fact]
    public void NobodyElseCanHireThem()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Money = 9999f;
        Assert.Equal("esse comandante é de outro país", new HireGeneralCommand(prt.Id, "RUS_gen_inverno").Validate(w));
        Assert.Null(new HireGeneralCommand(prt.Id, "PRT_gen_comandos_prt").Validate(w));
    }

    [Fact]
    public void HiringTheHomeCommanderCostsHisPriceAndTheArmyIsWorthMore()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Money = 9999f;
        var def = w.GeneralDefs["PRT_gen_comandos_prt"];
        float before = prt.Stat(def.StatKey, 1f);

        var cmd = new HireGeneralCommand(prt.Id, def.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Contains(def.Id, prt.Generals);
        Assert.Equal(9999f - def.Cost, prt.Money, 2);
        Assert.Equal(before * def.Mult, prt.Stat(def.StatKey, 1f), 3);
    }

    [Fact]
    public void ACommanderFromAnotherCountryInTheRollIsWorthNothing()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        // um save antigo (ou uma tag trocada) com o russo na folha: o motor não lhe conta o multiplicador
        prt.Generals.Add("RUS_gen_inverno");
        World.ApplyGenerals(w, prt);
        Assert.Empty(prt.GeneralMult);
    }

    [Fact]
    public void TheAiFillsItsStaffWithItsOwnMenFirst()
    {
        var w = FactionTests.BuildReal();
        var rus = ByTag(w, "RUS");
        var ukr = ByTag(w, "UKR");
        rus.Money = 9999f;
        rus.AtWarWith.Add(ukr.Id);
        ukr.AtWarWith.Add(rus.Id);
        w.Register(new AiSystem());

        for (int i = 0; i < 40; i++) w.Tick();

        Assert.Equal((int)w.Rule("general_slots", 3f), rus.Generals.Count);
        Assert.Contains(rus.Generals, id => w.GeneralDefs[id].CountryTag == "RUS");
        Assert.All(rus.Generals, id => Assert.True(World.GeneralIsFor(w.GeneralDefs[id], rus),
                                                   $"a IA contratou {id}, que não é da Rússia"));
    }

    [Fact]
    public void TheHomeCommandersAreWorthMoreThanTheMercenariesTheyReplace()
    {
        var w = FactionTests.BuildReal();
        // é a razão de existirem: um comandante de casa custa e vale mais do que o mercenário do mesmo ofício
        foreach (var g in w.GeneralDefs.Values.Where(g => g.CountryTag is not null))
        {
            var merc = w.GeneralDefs.Values.FirstOrDefault(m => m.CountryTag is null && m.StatKey == g.StatKey);
            if (merc is null) continue;
            // em perdas e sustento valer mais é o número descer: um chefe de material de casa gasta menos
            bool cheaper = g.StatKey.EndsWith("_losses") || g.StatKey.EndsWith("_upkeep");
            Assert.True(cheaper ? g.Mult <= merc.Mult : g.Mult >= merc.Mult,
                        $"{g.Id} ({g.Mult}) não vale mais do que {merc.Id} ({merc.Mult})");
        }
    }
}
