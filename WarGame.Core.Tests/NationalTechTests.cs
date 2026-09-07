using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Aviação e Marinha na árvore de investigação, e os programas nacionais que as fecham. A árvore
/// era toda de terra e de fábrica: comprava-se um esquadrão ou um navio e não havia uma única linha de
/// investigação onde os melhorar — as escolas de guerra do ar e do mar pagavam-se com experiência da arma,
/// e o laboratório não tinha nada a dizer sobre o assunto.
///
/// Agora há dois ramos comuns (Aviação e Marinha, três degraus cada) e, no fim do primeiro degrau de cada
/// um, o programa de casa: 28 países × 2 programas com selo ⚜, mais caros do que qualquer degrau comum e
/// mais fortes do que ele. Ninguém investiga o programa de outro país, e um que apareça na lista por um
/// save antigo não vale nada.
///
/// Leem a base de dados a sério (data/static.db), que é onde a árvore vive.</summary>
public class NationalTechTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    /// <summary>O que cada arma pode melhorar — a mesma tabela do tools/check_countries.py.</summary>
    private static readonly Dictionary<string, string[]> ArmStats = new()
    {
        ["Aviação"] = new[] { "air_losses", "air_bombing", "air_upkeep" },
        ["Marinha"] = new[] { "naval_losses", "naval_upkeep", "naval_blockade", "naval_escort", "naval_patrol" },
    };

    private static bool Cheaper(string stat) => stat.EndsWith("_losses") || stat.EndsWith("_upkeep");

    [Fact]
    public void TheTreeHasAnAirBranchAndANavalBranch()
    {
        var w = FactionTests.BuildReal();
        foreach (string branch in ArmStats.Keys)
        {
            var common = w.Techs.Values.Where(t => t.Branch == branch && t.CountryTag is null)
                                       .OrderBy(t => t.Cost).ToList();
            Assert.Equal(3, common.Count);
            Assert.Null(common[0].Requires);                       // o primeiro degrau abre-se sozinho
            for (int i = 1; i < common.Count; i++)
                Assert.Equal(common[i - 1].Id, common[i].Requires); // e os outros vêm em corrente
            foreach (var t in common)
            {
                var effs = w.TechEffects[t.Id];
                Assert.NotEmpty(effs);
                foreach (var (key, mul) in effs)
                {
                    Assert.Contains(key, ArmStats[branch]);
                    Assert.True(Cheaper(key) ? mul < 1f : mul > 1f, $"{t.Id}: {key}={mul} com o sinal trocado");
                }
            }
        }
    }

    [Fact]
    public void EveryCountryBringsOneAirProgrammeAndOneNavalProgrammeOfItsOwn()
    {
        var w = FactionTests.BuildReal();
        foreach (var (branch, arm) in new[] { ("Aviação", "ar"), ("Marinha", "mar") })
        {
            var own = w.Techs.Values.Where(t => t.CountryTag is not null && t.Branch == branch).ToList();
            Assert.Equal(28, own.Count);
            Assert.Equal(28, own.Select(t => t.CountryTag).Distinct().Count());   // um por país, nunca dois
            float top = w.Techs.Values.Where(t => t.Branch == branch && t.CountryTag is null).Max(t => t.Cost);
            foreach (var t in own)
            {
                Assert.StartsWith($"{t.CountryTag}_tech_{arm}_", t.Id);
                Assert.True(t.Cost > top, $"{t.Id} custa {t.Cost} e o degrau comum mais caro custa {top}");
                Assert.NotNull(t.Requires);
                Assert.Null(w.Techs[t.Requires!].CountryTag);                     // apanha-se a um degrau comum
                Assert.False(string.IsNullOrWhiteSpace(t.Description));
                foreach (var (key, mul) in w.TechEffects[t.Id])
                {
                    Assert.Contains(key, ArmStats[branch]);
                    Assert.True(Cheaper(key) ? mul < 1f : mul > 1f, $"{t.Id}: {key}={mul} com o sinal trocado");
                }
            }
        }
        Assert.Equal(56, w.Techs.Values.Count(t => t.CountryTag is not null));
    }

    [Fact]
    public void TheHomeProgrammeIsWorthMoreThanTheCommonStepItReplaces()
    {
        var w = FactionTests.BuildReal();
        foreach (var t in w.Techs.Values.Where(t => t.CountryTag is not null))
            foreach (var (key, mul) in w.TechEffects[t.Id])
            {
                var common = w.Techs.Values.Where(x => x.CountryTag is null && w.TechEffects.TryGetValue(x.Id, out var e)
                                                       && e.Any(p => p.Key == key))
                                           .SelectMany(x => w.TechEffects[x.Id].Where(p => p.Key == key))
                                           .Select(p => p.Mul).ToList();
                if (common.Count == 0) continue;
                float best = Cheaper(key) ? common.Min() : common.Max();
                Assert.True(Cheaper(key) ? mul < best : mul > best,
                            $"{t.Id}: {key}={mul} não vale mais do que o degrau comum ({best})");
            }
    }

    [Fact]
    public void NobodyElseCanResearchThem()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Techs.Add("air_1"); prt.Techs.Add("nav_1");

        Assert.Equal("essa tecnologia é de outro país",
                     new ResearchTechCommand(prt.Id, "DEU_tech_ar_intercepcao").Validate(w));
        Assert.Equal("essa tecnologia é de outro país",
                     new ResearchTechCommand(prt.Id, "USA_tech_mar_grupo_porta_avioes").Validate(w));
        Assert.False(w.CanResearch(prt, "DEU_tech_ar_intercepcao"));
        Assert.Null(new ResearchTechCommand(prt.Id, "PRT_tech_ar_lajes").Validate(w));
        Assert.Null(new ResearchTechCommand(prt.Id, "PRT_tech_mar_fragatas_zee").Validate(w));
    }

    [Fact]
    public void TheProgrammeOnlyOpensAfterTheCommonStepBelowIt()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Techs.Remove("nav_1");

        Assert.False(w.CanResearch(prt, "PRT_tech_mar_fragatas_zee"));
        Assert.Equal("Precisa de Guerra anti-submarina",
                     new ResearchTechCommand(prt.Id, "PRT_tech_mar_fragatas_zee").Validate(w));

        prt.Techs.Add("nav_1");
        Assert.True(w.CanResearch(prt, "PRT_tech_mar_fragatas_zee"));
    }

    [Fact]
    public void TheBranchesReachTheEnginesThatAskForThem()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Techs.Clear();
        prt.Techs.Add("air_1");
        prt.Techs.Add("PRT_tech_ar_lajes");
        w.ApplyTechs(prt);

        // é isto que o AirMissionSystem pergunta ao país todos os dias
        Assert.Equal(w.TechEffects["air_1"].Single(p => p.Key == "air_losses").Mul, prt.Stat("air_losses", 1f), 3);
        Assert.Equal(w.TechEffects["PRT_tech_ar_lajes"].Single(p => p.Key == "air_upkeep").Mul, prt.Stat("air_upkeep", 1f), 3);
        Assert.Equal(1f, prt.Stat("naval_losses", 1f), 3);            // o mar ainda não foi investigado
    }

    [Fact]
    public void AForeignProgrammeInTheListIsWorthNothing()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        prt.Techs.Clear();
        // um save antigo (ou uma tag trocada) com o programa alemão na lista: o motor não lhe conta nada
        prt.Techs.Add("DEU_tech_ar_intercepcao");
        w.ApplyTechs(prt);

        Assert.Equal(1f, prt.Stat("air_losses", 1f), 3);
    }

    [Fact]
    public void TheAiOnlyResearchesWhatIsItsOwn()
    {
        var w = FactionTests.BuildReal();
        foreach (var c in w.Countries.Values) c.Money = 5000f;
        w.Register(new AiSystem());
        w.Register(new ResearchSystem());

        for (int i = 0; i < 12; i++) w.Tick();

        foreach (var c in w.Countries.Values)
            foreach (string id in c.Research.Keys.Concat(c.Techs))
                Assert.True(!w.Techs.TryGetValue(id, out var t) || World.TechIsFor(t, c),
                            $"a IA de {c.Tag} investiga {id}, que é de outro país");
        // e a árvore que lhe é oferecida tem as armas lá dentro — o que a IA escolher de entre elas é conta dela,
        // mas o programa de casa está em cima da mesa e o do vizinho nunca
        foreach (var c in w.Countries.Values)
        {
            var open = w.Techs.Values.Where(t => w.CanResearch(c, t.Id)).ToList();
            Assert.All(open, t => Assert.True(World.TechIsFor(t, c), $"a IA de {c.Tag} vê {t.Id}, que é de outro país"));
        }
        Assert.Contains(w.Countries.Values, c => w.Techs.Values.Any(t => t.Branch is "Aviação" or "Marinha"
                                                                        && w.CanResearch(c, t.Id)));
    }
}
