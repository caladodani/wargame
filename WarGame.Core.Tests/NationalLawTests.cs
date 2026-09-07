using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Leis próprias de cada país (law.country_tag + law_group.country_tag). Até aqui a escada de leis
/// era a mesma para toda a gente: a conscrição de Portugal era a conscrição da Coreia do Norte. Agora cada
/// país traz uma escada só sua — a questão nacional que só ele vota — e o que se mede aqui é que ela existe,
/// que aparece na lista dele e em mais nenhuma, e que nem o jogador nem a IA lhe conseguem tocar de fora.
///
/// Estes testes leem a base de dados a sério (data/static.db), que é onde as escadas vivem.</summary>

public class NationalLawTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    [Fact]
    public void EveryCountryFileBringsItsOwnLadderWithTheGroupDescribedInTheDatabase()
    {
        var w = FactionTests.BuildReal();
        var own = w.LawGroupDefs.Values.Where(g => g.CountryTag is not null).ToList();
        Assert.True(own.Count >= 28, $"só {own.Count} escadas próprias");
        foreach (var g in own)
        {
            Assert.StartsWith(g.CountryTag + "_", g.Id);
            Assert.NotEqual("", g.Name);
            Assert.NotEqual("", g.Icon);                       // o cartão do painel precisa da chapa
            var steps = w.Laws.Values.Where(l => l.Group == g.Id).ToList();
            Assert.Equal(3, steps.Count);
            Assert.All(steps, l => Assert.Equal(g.CountryTag, l.CountryTag));
            Assert.Single(steps, l => l.IsDefault);            // um e um só degrau de arranque
            Assert.All(steps, l => Assert.True(w.LawEffects.TryGetValue(l.Id, out var e) && e.Count > 0,
                                               $"{l.Id} sem efeitos"));
        }
    }

    [Fact]
    public void TheLadderOnlyShowsUpInTheListOfTheCountryItBelongsTo()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var esp = ByTag(w, "ESP");

        Assert.Contains("PRT_mar", w.LawGroups(prt));
        Assert.DoesNotContain("PRT_mar", w.LawGroups(esp));
        Assert.Contains("ESP_autonomias", w.LawGroups(esp));
        // a escada própria vem no fim (law_group.sort 10, as comuns são 0..5)
        Assert.Equal("PRT_mar", w.LawGroups(prt).Last());
        // e as comuns continuam lá para toda a gente
        Assert.Contains("conscription", w.LawGroups(prt));
        Assert.Equal(w.LawGroups(prt).Count, w.LawGroups(esp).Count);
    }

    [Fact]
    public void TheFirstStepIsTheOneInForceOnDayOneAndItsEffectsAreAlreadyCounted()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var start = w.ActiveLaw(prt, "PRT_mar");
        Assert.NotNull(start);
        Assert.Equal("PRT_law_pescas", start!.Id);
        Assert.True(start.IsDefault);
        // pesca e cabotagem vale +10% na fatia que sai do país, e isso já está no stat no dia 1: a Espanha,
        // que arranca com as mesmas leis comuns e uma escada própria que não fala de comércio, exporta menos
        var esp = ByTag(w, "ESP");
        w.ApplyTechs(prt); w.ApplyTechs(esp);
        Assert.Equal(esp.Stat("export_share", 1f) * 1.10f, prt.Stat("export_share", 1f), 3);
    }

    [Fact]
    public void NobodyElseCanVoteThatLaw()
    {
        var w = FactionTests.BuildReal();
        var esp = ByTag(w, "ESP");
        esp.Money = 9999f;
        Assert.Equal("essa lei é de outro país", new ChangeLawCommand(esp.Id, "PRT_law_acores").Validate(w));
        // e a sua, essa passa
        Assert.Null(new ChangeLawCommand(esp.Id, "ESP_law_centralizacao").Validate(w));
    }

    [Fact]
    public void ClimbingTheOwnLadderCostsTheSameAndChangesWhatTheCountryIsWorth()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Money = 9999f;
        float before = prt.Stat("defense", 1f);

        var cmd = new ChangeLawCommand(prt.Id, "PRT_law_acores");
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Equal("PRT_law_acores", w.ActiveLaw(prt, "PRT_mar")!.Id);
        Assert.Equal(9999f - w.Rule("law_change_cost", 30f), prt.Money, 2);
        Assert.True(prt.Stat("defense", 1f) > before, "a base atlântica tem de valer defesa");
    }

    [Fact]
    public void TheAiOnlyEscalatesWithinItsOwnLadders()
    {
        var w = FactionTests.BuildReal();
        var rus = ByTag(w, "RUS");
        var ukr = ByTag(w, "UKR");
        rus.Money = 9999f;
        rus.AtWarWith.Add(ukr.Id);
        ukr.AtWarWith.Add(rus.Id);
        w.Register(new AiSystem());

        for (int i = 0; i < 60; i++) w.Tick();

        foreach (var grp in w.LawGroups(rus))
        {
            var law = w.ActiveLaw(rus, grp);
            Assert.NotNull(law);
            Assert.True(World.LawIsFor(law!, rus), $"a IA sentou a lei {law!.Id} num país que não é o dela");
        }
        // nenhuma lei da Ucrânia foi parar ao registo da Rússia
        Assert.DoesNotContain(rus.Laws.Values, id => w.Laws[id].CountryTag is string t && t != "RUS");
    }

    [Fact]
    public void ADeadLadderFallsBackToTheDefaultOfTheCountryItself()
    {
        var w = FactionTests.BuildReal();
        var esp = ByTag(w, "ESP");
        // registo com a lei de outro país lá dentro (um save antigo, ou uma tag trocada): a escada não fica sem lei
        esp.Laws["ESP_autonomias"] = "PRT_law_acores";
        var back = w.ActiveLaw(esp, "ESP_autonomias");
        Assert.NotNull(back);
        Assert.Equal("ESP_law_alargada", back!.Id);
    }
}
