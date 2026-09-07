using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Condecorações nacionais (medal.country_tag). A tabela de medalhas era uma só para o mundo
/// inteiro: a divisão britânica que aguentava dez batalhas recebia uma "Cruz de Aço", que não é fita
/// nenhuma daquele exército.
///
/// Agora cada país traz o medalheiro dele — cinco graus, com os nomes da tradição — e condecora só por
/// ele; quem não trouxer nenhum continua a receber as fitas comuns. O que o medalheiro nacional muda é o
/// NOME: a métrica, o limiar e o bónus são os mesmos, para nenhuma bandeira ter fitas mais baratas.
///
/// Lêem a base a sério (data/static.db), que é onde os medalheiros vivem.</summary>
public class NationalMedalTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    /// <summary>Uma divisão daquele país, com a folha de serviço já feita.</summary>
    private static Division Div(World w, int id, Country c, int battles = 0, int captures = 0, float xp = 0f) =>
        w.AddDivision(new Division { Id = id, CountryId = c.Id, TemplateId = 1, RegionId = 1,
                                     Battles = battles, Captures = captures, Xp = xp });

    /// <summary>Corre o sistema num dia em que ele condecora.</summary>
    private static void Award(World w)
    {
        int period = Math.Max(1, (int)w.Rule("medal_check_days", 2f));
        while (w.Clock.Day % period != 0) w.Clock.Advance();
        new MedalSystem().Tick(w);
    }

    [Fact]
    public void EveryCountryThatBringsACaseBringsTheFiveGrades()
    {
        var w = FactionTests.BuildReal();
        var tags = w.MedalDefs.Values.Where(m => m.CountryTag is not null).Select(m => m.CountryTag!).Distinct().ToList();
        Assert.Equal(28, tags.Count);
        foreach (string tag in tags)
        {
            var c = ByTag(w, tag);
            Assert.True(w.HasOwnMedals(c), $"{tag} não tem medalheiro");
            var set = w.Medals(c);
            Assert.Equal(5, set.Count);
            Assert.All(set, m => Assert.Equal(tag, m.CountryTag));
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, set.Select(m => m.Sort));
            Assert.Equal(set.Count, set.Select(m => m.Name).Distinct().Count());
            Assert.All(set, m => Assert.StartsWith(tag + "_", m.Id));
        }
        Assert.Equal(28 * 5, w.MedalDefs.Values.Count(m => m.CountryTag is not null));
    }

    [Fact]
    public void TheHomeCaseChangesTheNameAndNotTheBalance()
    {
        var w = FactionTests.BuildReal();
        var common = w.Medals();
        foreach (string tag in w.MedalDefs.Values.Where(m => m.CountryTag is not null).Select(m => m.CountryTag!).Distinct())
        {
            var own = w.Medals(ByTag(w, tag));
            Assert.Equal(common.Count, own.Count);
            for (int i = 0; i < own.Count; i++)
            {
                Assert.Equal(common[i].Metric, own[i].Metric);            // pede o mesmo
                Assert.Equal(common[i].Threshold, own[i].Threshold, 3);
                Assert.Equal(common[i].Bonus, own[i].Bonus, 3);           // e vale o mesmo
            }
            // e tem alguma coisa de nacional: um medalheiro com os nomes das comuns não era medalheiro nenhum
            Assert.NotEqual(common.Select(m => m.Name), own.Select(m => m.Name));
        }
    }

    [Fact]
    public void NobodyWearsAnotherCountrysDecoration()
    {
        var w = FactionTests.BuildReal();
        var gbr = ByTag(w, "GBR");
        var arg = ByTag(w, "ARG");

        var vc = w.MedalDefs.Values.Single(m => m.Name == "Victoria Cross" && m.CountryTag == "GBR");
        Assert.True(World.MedalIsFor(vc, gbr));
        Assert.False(World.MedalIsFor(vc, arg));
        Assert.DoesNotContain(w.Medals(arg), m => m.Name == "Victoria Cross");
        Assert.All(w.Medals(arg), m => Assert.True(World.MedalIsFor(m, arg)));
    }

    [Fact]
    public void TheSameWarGivesEachCountryItsOwnRibbon()
    {
        var w = FactionTests.BuildReal();
        int id = 1;
        foreach (var (tag, first, highest) in new[]
                 {
                     ("GBR", "General Service Medal", "Victoria Cross"),
                     ("PRT", "Medalha de Comportamento Exemplar", "Ordem da Torre e Espada"),
                     ("DEU", "Einsatzmedaille", "Großes Ehrenzeichen der Bundeswehr"),
                     ("USA", "Combat Action Ribbon", "Medal of Honor"),
                 })
        {
            var c = ByTag(w, tag);
            var d = Div(w, id++, c, battles: 12, captures: 5, xp: 95f);   // a mesma guerra para todos
            Award(w);
            var worn = d.Medals.Select(x => w.MedalDefs[x]).OrderBy(m => m.Sort).ToList();
            Assert.Equal(5, worn.Count);
            Assert.Equal(first, worn[0].Name);
            Assert.Equal(highest, worn[4].Name);
            Assert.All(worn, m => Assert.Equal(tag, m.CountryTag));
        }
    }

    [Fact]
    public void ACountryWithoutItsOwnCaseGetsTheCommonRibbons()
    {
        var w = FactionTests.BuildReal();
        var c = w.Countries.Values.First(x => !w.HasOwnMedals(x));
        Assert.Equal(w.Medals().Select(m => m.Id), w.Medals(c).Select(m => m.Id));

        var d = Div(w, 1, c, battles: 1);
        Award(w);
        Assert.Equal(new[] { "baptismo" }, d.Medals);
    }

    [Fact]
    public void TheCommonTwinIsNeverPinnedOnTopOfTheHomeOne()
    {
        // save antigo: a divisão portuguesa já traz a fita comum do grau 1. Passar a haver medalheiro
        // nacional não lhe pode pregar a nacional do mesmo grau por cima — era o bónus a dobrar.
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var d = Div(w, 1, prt, battles: 12);
        d.Medals.Add("baptismo");
        var seen = new List<MedalAwarded>();
        w.Events.Subscribe<MedalAwarded>(seen.Add);

        Award(w);
        Assert.Contains("baptismo", d.Medals);
        Assert.DoesNotContain("PRT_baptismo", d.Medals);
        Assert.Contains("PRT_aco", d.Medals);                      // os outros graus entram na mesma
        Assert.DoesNotContain(seen, e => e.MedalId == "PRT_baptismo");
        Assert.Equal(1, d.Medals.Count(x => w.MedalDefs[x].Sort == 1));
    }

    [Fact]
    public void TheHomeRibbonIsWorthTheSameInTheField()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var d = Div(w, 1, prt, battles: 12, captures: 5, xp: 95f);
        Award(w);

        var common = new Division { Id = 2, CountryId = prt.Id, TemplateId = 1, RegionId = 1 };
        foreach (var m in w.Medals()) common.Medals.Add(m.Id);
        Assert.Equal(MedalSystem.Bonus(w, common), MedalSystem.Bonus(w, d), 4);
    }

    [Fact]
    public void TheNationalRibbonSurvivesTheSave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        // o mundo de teste não carrega data/countries: põe-se um medalheiro nacional à mão no país 1
        var c = w.Countries[1];
        w.MedalDefs["A_baptismo"] = new MedalDef("A_baptismo", "Fita de Casa", "", "battles", 1f, 0.01f, 1, c.Tag);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Battles = 3;
        Assert.True(w.HasOwnMedals(c));
        new MedalSystem().Tick(w);
        Assert.Equal(new[] { "A_baptismo" }, d.Medals);          // e não a comum do mesmo grau

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(new[] { "A_baptismo" }, w2.Divisions[1].Medals);
    }
}
