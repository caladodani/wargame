using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Escolas nacionais de guerra (army_doctrine_branch.country_tag + army_doctrine.country_tag). Até
/// aqui havia três escolas e eram as mesmas para toda a gente: a Guerra de Movimento de Portugal era a da
/// Rússia, e um exército não se distinguia do outro por aquilo que sabe fazer. Agora cada país traz uma quarta
/// coluna que é só dele — a Escola Expedicionária portuguesa, a Arte Operacional russa, a Guerra Subterrânea
/// coreana — e mede-se aqui que ela existe, que aparece na árvore dele e em mais nenhuma, que fecha as comuns
/// como qualquer escolha de escola, e que nem o jogador nem a IA lhe tocam de fora.
///
/// Leem a base de dados a sério (data/static.db), que é onde as escolas vivem.</summary>
public class NationalDoctrineTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    [Fact]
    public void EveryCountryBringsOneSchoolOfItsOwnWithTheStepsChained()
    {
        var w = FactionTests.BuildReal();
        var own = w.DoctrineBranches.Values.Where(b => b.CountryTag is not null && b.Domain == World.Land).ToList();
        Assert.Equal(28, own.Count);
        Assert.Equal(3, w.DoctrineBranches.Values.Count(b => b.CountryTag is null && b.Domain == World.Land));

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
                Assert.Equal(prev, d.Requires);                 // corrente: cada degrau pede o anterior
                Assert.True(w.DoctrineEffects.TryGetValue(d.Id, out var effs) && effs.Count > 0, $"{d.Id} sem efeitos");
                prev = d.Id;
            }
            Assert.True(steps[0].Cost < steps[1].Cost && steps[1].Cost < steps[2].Cost, $"{b.Id}: preço não sobe");
        }
    }

    [Fact]
    public void TheSchoolOnlyShowsUpInTheTreeOfItsOwnCountry()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var rus = ByTag(w, "RUS");

        Assert.Equal(4, w.Branches(prt).Count);                 // três comuns + a de casa
        Assert.Contains(w.Branches(prt), b => b.Id == "PRT_escola");
        Assert.DoesNotContain(w.Branches(prt), b => b.Id == "RUS_escola");
        Assert.Equal("PRT_escola", w.Branches(prt).Last().Id);  // a de casa vem no fim, à direita das comuns
        Assert.Contains(w.Branches(rus), b => b.Id == "RUS_escola");
        Assert.Empty(w.DoctrineSteps(prt, "RUS_escola"));
    }

    [Fact]
    public void NobodyElseCanLearnIt()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.ArmyXp = 600f;
        Assert.Equal("essa escola é de outro país", new AdoptDoctrineCommand(prt.Id, "RUS_doc_artilharia").Validate(w));
        Assert.Null(new AdoptDoctrineCommand(prt.Id, "PRT_doc_africa").Validate(w));
        Assert.False(w.CanAdopt(prt, "RUS_doc_artilharia"));
    }

    [Fact]
    public void LearningTheHomeSchoolCostsExperienceAndClosesTheCommonOnes()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.ArmyXp = 600f;
        float before = prt.Stat("org_regain", 1f);
        var def = w.ArmyDoctrines["PRT_doc_africa"];

        var cmd = new AdoptDoctrineCommand(prt.Id, def.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Contains(def.Id, prt.Doctrines);
        Assert.Equal(600f - def.Cost, prt.ArmyXp, 2);
        Assert.Equal("PRT_escola", w.DoctrineBranchOf(prt));
        Assert.True(prt.Stat("org_regain", 1f) > before, "a lição de África tem de valer recuperação");
        // escolhida a escola de casa, as comuns fecham-se como qualquer outra escolha
        Assert.StartsWith("Escola fechada por", new AdoptDoctrineCommand(prt.Id, "mov_1").Validate(w));
        // e o segundo degrau da nossa abre-se
        Assert.Null(new AdoptDoctrineCommand(prt.Id, "PRT_doc_comandos").Validate(w));
    }

    [Fact]
    public void ASchoolFromAnotherCountryInTheRollIsWorthNothing()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        // um save antigo (ou uma tag trocada) com a escola russa na folha: o motor não lhe conta os efeitos
        prt.Doctrines.Add("RUS_doc_inverno");
        w.ApplyTechs(prt);
        Assert.Equal(1f, prt.Stat("defense", 1f), 3);
    }

    [Fact]
    public void TheAiTrainsItsOwnSchoolFirst()
    {
        var w = FactionTests.BuildReal();
        var rus = ByTag(w, "RUS");
        var ukr = ByTag(w, "UKR");
        rus.AtWarWith.Add(ukr.Id);
        ukr.AtWarWith.Add(rus.Id);
        rus.ArmyXp = 600f;
        w.Register(new ArmyXpSystem());

        w.Tick();

        var picked = Assert.Single(rus.Doctrines);
        Assert.Equal("RUS_doc_artilharia", picked);             // a de casa passa à frente das comuns mais baratas
        Assert.Equal("RUS_escola", w.DoctrineBranchOf(rus));
        for (int i = 0; i < 30; i++) w.Tick();
        Assert.All(rus.Doctrines, id => Assert.True(World.DoctrineIsFor(w.ArmyDoctrines[id], rus),
                                                    $"a IA aprendeu {id}, que não é da Rússia"));
    }

    [Fact]
    public void TheHomeSchoolIsDearerThanTheCommonOneItReplaces()
    {
        var w = FactionTests.BuildReal();
        // é a razão de ser uma escolha: a escola de casa custa mais experiência do que a raiz comum
        float common = w.ArmyDoctrines.Values.Where(d => d.CountryTag is null && d.Requires is null).Max(d => d.Cost);
        foreach (var d in w.ArmyDoctrines.Values.Where(d => d.CountryTag is not null && d.Requires is null))
            Assert.True(d.Cost > common, $"{d.Id} ({d.Cost}) não custa mais do que a raiz comum ({common})");
    }
}
