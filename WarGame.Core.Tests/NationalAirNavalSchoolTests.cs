using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Escolas nacionais do ar e do mar. A escola de casa do exército já existia — a Escola
/// Expedicionária portuguesa, a Arte Operacional russa — mas no ar e no mar toda a gente aprendia o mesmo:
/// a caça alemã era a caça angolana, e o corso britânico era o corso coreano. Agora cada país traz também
/// uma escola do ar e uma escola do mar que são só dele (Asas do Atlântico e Escola do Mar Largo em
/// Portugal, Alcateia na Alemanha, Enxame do Golfo no Irão), com três degraus cada, pagas do bolso daquela
/// arma e fechadas a toda a gente menos ao dono.
///
/// A regra que isto tudo respeita é a de sempre: uma escolha por arma. Escolher a escola de casa do ar não
/// fecha nada em terra nem no mar — fecha as outras escolas do ar. É por isso que um país pode ser da
/// Guerra de Movimento, das Asas do Atlântico e do Mar Largo ao mesmo tempo.
///
/// Leem a base de dados a sério (data/static.db), que é onde as escolas vivem.</summary>
public class NationalAirNavalSchoolTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    /// <summary>O que cada arma pode melhorar — a mesma tabela que o tools/check_countries.py guarda.</summary>
    private static readonly Dictionary<string, string[]> ArmStats = new()
    {
        [World.Air] = new[] { "air_losses", "air_bombing", "air_upkeep" },
        [World.Sea] = new[] { "naval_losses", "naval_upkeep", "naval_blockade", "naval_escort", "naval_patrol" },
    };

    [Fact]
    public void EveryCountryBringsOneSchoolOfTheAirAndOneOfTheSeaOfItsOwn()
    {
        var w = FactionTests.BuildReal();
        foreach (var domain in new[] { World.Air, World.Sea })
        {
            var own = w.DoctrineBranches.Values.Where(b => b.CountryTag is not null && b.Domain == domain).ToList();
            Assert.Equal(28, own.Count);
            Assert.Equal(28, own.Select(b => b.CountryTag).Distinct().Count());   // uma por país, nunca duas

            foreach (var b in own)
            {
                Assert.StartsWith(b.CountryTag + "_", b.Id);
                Assert.NotEqual("", b.Icon);
                var c = ByTag(w, b.CountryTag!);
                var steps = w.DoctrineSteps(c, b.Id);
                Assert.Equal(3, steps.Count);
                string? prev = null;
                foreach (var d in steps)
                {
                    Assert.Equal(b.CountryTag, d.CountryTag);
                    Assert.Equal(prev, d.Requires);                               // corrente: cada degrau pede o anterior
                    Assert.True(w.DoctrineEffects.TryGetValue(d.Id, out var effs) && effs.Count > 0, $"{d.Id} sem efeitos");
                    prev = d.Id;
                }
                Assert.True(steps[0].Cost < steps[1].Cost && steps[1].Cost < steps[2].Cost, $"{b.Id}: preço não sobe");
            }
        }
    }

    [Fact]
    public void WhatEachSchoolTeachesBelongsToItsOwnArm()
    {
        // uma escola de caça não dá recrutamento nem bloqueio: se desse, o efeito não chegava a lado nenhum
        // (o motor aéreo só lê as chaves do ar) e o cartão prometia o que não cumpre
        var w = FactionTests.BuildReal();
        foreach (var (domain, keys) in ArmStats)
            foreach (var b in w.DoctrineBranches.Values.Where(b => b.CountryTag is not null && b.Domain == domain))
                foreach (var d in w.DoctrineSteps(ByTag(w, b.CountryTag!), b.Id))
                    foreach (var e in w.DoctrineEffects[d.Id])
                    {
                        Assert.Contains(e.Key, keys);
                        // e o sinal certo: perdas e sustento descem, o resto sobe
                        bool cheaper = e.Key.EndsWith("_losses") || e.Key.EndsWith("_upkeep");
                        Assert.True(cheaper ? e.Mul < 1f : e.Mul > 1f, $"{d.Id}: {e.Key}={e.Mul} com o sinal trocado");
                    }
    }

    [Fact]
    public void TheAirSchoolOnlyShowsUpInTheAirTreeOfItsOwnCountry()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var deu = ByTag(w, "DEU");

        Assert.Equal(3, w.Branches(prt, World.Air).Count);            // duas comuns + a de casa
        Assert.Equal("PRT_ar", w.Branches(prt, World.Air).Last().Id); // a de casa vem no fim, à direita das comuns
        Assert.DoesNotContain(w.Branches(prt, World.Air), b => b.Id == "DEU_ar");
        Assert.DoesNotContain(w.Branches(prt, World.Land), b => b.Id == "PRT_ar");   // e não se mistura com o exército
        Assert.DoesNotContain(w.Branches(prt, World.Sea), b => b.Id == "PRT_ar");
        Assert.Contains(w.Branches(deu, World.Air), b => b.Id == "DEU_ar");
        Assert.Empty(w.DoctrineSteps(prt, "DEU_ar"));

        Assert.Equal(3, w.Branches(prt, World.Sea).Count);
        Assert.Equal("PRT_mar", w.Branches(prt, World.Sea).Last().Id);
        Assert.Equal(4, w.Branches(prt, World.Land).Count);           // o exército continua com três comuns + a sua
    }

    [Fact]
    public void NobodyElseCanLearnThem()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.AirXp = prt.NavyXp = 400f;
        Assert.Equal("essa escola é de outro país", new AdoptDoctrineCommand(prt.Id, "DEU_ar_rotte").Validate(w));
        Assert.Equal("essa escola é de outro país", new AdoptDoctrineCommand(prt.Id, "GBR_mar_comboio_gbr").Validate(w));
        Assert.False(w.CanAdopt(prt, "DEU_ar_rotte"));
        Assert.Null(new AdoptDoctrineCommand(prt.Id, "PRT_ar_lajes").Validate(w));
        Assert.Null(new AdoptDoctrineCommand(prt.Id, "PRT_mar_descobrimentos").Validate(w));
    }

    [Fact]
    public void TheHomeAirSchoolIsPaidFromTheAirPocketAndOnlyClosesTheAirDoors()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.AirXp = 300f; prt.NavyXp = 300f; prt.ArmyXp = 300f;
        var def = w.ArmyDoctrines["PRT_ar_lajes"];

        var cmd = new AdoptDoctrineCommand(prt.Id, def.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Contains(def.Id, prt.Doctrines);
        Assert.Equal(300f - def.Cost, prt.AirXp, 2);
        Assert.Equal(300f, prt.NavyXp, 2);                            // o mar não pagou nada disto
        Assert.Equal(300f, prt.ArmyXp, 2);
        Assert.Equal("PRT_ar", w.DoctrineBranchOf(prt, World.Air));

        // fecha as outras escolas do ar e mais nada
        Assert.StartsWith("Escola fechada por", new AdoptDoctrineCommand(prt.Id, "ceu_1").Validate(w));
        Assert.Null(new AdoptDoctrineCommand(prt.Id, "PRT_mar_descobrimentos").Validate(w));
        Assert.Null(new AdoptDoctrineCommand(prt.Id, "mov_1").Validate(w));
        Assert.Null(new AdoptDoctrineCommand(prt.Id, "PRT_ar_busca").Validate(w));   // e abre-se o degrau seguinte
    }

    [Fact]
    public void WhatTheHomeSchoolsTeachReachesTheCountry()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        Assert.Equal(1f, prt.Stat("air_upkeep", 1f), 3);
        Assert.Equal(1f, prt.Stat("naval_patrol", 1f), 3);

        prt.Doctrines.Add("PRT_ar_lajes");                            // placa dos Açores: esquadrão mais barato
        prt.Doctrines.Add("PRT_mar_descobrimentos");                  // arte de navegar: patrulha mais larga
        w.ApplyTechs(prt);

        Assert.True(prt.Stat("air_upkeep", 1f) < 1f, "a placa dos Açores tinha de baratear o esquadrão");
        Assert.True(prt.Stat("naval_patrol", 1f) > 1f, "a arte de navegar tinha de valer patrulha");
        Assert.Equal(1f, prt.Stat("air_bombing", 1f), 3);             // o que a escola não ensina fica como estava
    }

    [Fact]
    public void ASchoolFromAnotherCountryInTheRollIsWorthNothing()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        // um save antigo (ou uma tag trocada) com a alcateia alemã na folha: o motor não lhe conta os efeitos
        prt.Doctrines.Add("DEU_mar_alcateia");
        w.ApplyTechs(prt);
        Assert.Equal(1f, prt.Stat("naval_blockade", 1f), 3);
    }

    [Fact]
    public void TheHomeSchoolOfEachArmIsDearerThanTheCommonRootItReplaces()
    {
        var w = FactionTests.BuildReal();
        foreach (var domain in new[] { World.Air, World.Sea })
        {
            float common = w.ArmyDoctrines.Values
                .Where(d => d.CountryTag is null && d.Requires is null && w.DomainOf(d) == domain).Max(d => d.Cost);
            foreach (var d in w.ArmyDoctrines.Values
                        .Where(d => d.CountryTag is not null && d.Requires is null && w.DomainOf(d) == domain))
                Assert.True(d.Cost > common, $"{d.Id} ({d.Cost}) não custa mais do que a raiz comum ({common})");
        }
    }

    [Fact]
    public void TheAiTrainsItsOwnAirSchoolBeforeTheCommonOnes()
    {
        var w = FactionTests.BuildReal();
        var rus = ByTag(w, "RUS");
        rus.AirXp = 400f;                                             // bolso do ar cheio, os outros vazios
        w.Register(new ArmyXpSystem());

        w.Tick();

        string picked = Assert.Single(rus.Doctrines);
        Assert.Equal("RUS_ar_frontal", picked);                       // a de casa passa à frente das comuns mais baratas
        Assert.Equal("RUS_ar", w.DoctrineBranchOf(rus, World.Air));
        Assert.Null(w.DoctrineBranchOf(rus, World.Sea));
        for (int i = 0; i < 30; i++) w.Tick();
        Assert.All(rus.Doctrines, id => Assert.True(World.DoctrineIsFor(w.ArmyDoctrines[id], rus),
                                                    $"a IA aprendeu {id}, que não é da Rússia"));
    }

    [Fact]
    public void TheTopBarAsksEachPocketWhatItCanBuyToday()
    {
        // é o que as três medalhas da barra mostram: cada arma responde do seu bolso, e a de casa primeiro
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        Assert.Null(ArmyXpSystem.Next(w, prt, World.Air));
        Assert.Null(ArmyXpSystem.Next(w, prt, World.Sea));

        prt.NavyXp = 60f;
        Assert.Null(ArmyXpSystem.Next(w, prt, World.Air));            // o mar cheio não compra nada ao ar
        Assert.Equal("PRT_mar_descobrimentos", ArmyXpSystem.Next(w, prt, World.Sea));
        Assert.Equal(World.Sea, w.DomainOf(w.ArmyDoctrines[ArmyXpSystem.Next(w, prt)!]));

        prt.AirXp = 40f;                                              // chega para uma escola comum, não para a de casa
        Assert.Equal(World.Air, w.DomainOf(w.ArmyDoctrines[ArmyXpSystem.Next(w, prt, World.Air)!]));
        Assert.Null(w.ArmyDoctrines[ArmyXpSystem.Next(w, prt, World.Air)!].CountryTag);
    }
}
