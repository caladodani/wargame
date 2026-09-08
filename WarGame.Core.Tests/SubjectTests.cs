using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Estados-fantoche: ganhar a guerra sem comer o mapa. O que se prova aqui é que os degraus vêm da
/// tabela e não do código, que o degrau se lê da autonomia (e por isso nunca a pode contradizer), que o
/// tributo é uma fatia do dia que o vassalo acabou de ganhar — contada pelas mesmas funções do rendimento e
/// dos homens — e que a coleira se solta sozinha, mais depressa a quem se bate.</summary>
public class SubjectTests
{
    private static (World w, MsSqliteDatabase db) Vassalage()
    {
        var (w, db) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EconomySystem());     // o rendimento do dia
        w.Register(new ManpowerSystem());    // e os homens do dia
        w.Register(new SubjectSystem());     // é sobre eles que se cobra
        return (w, db);
    }

    [Fact]
    public void TheStepsComeFromTheDatabase()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        Assert.NotEmpty(w.SubjectTypeDefs);
        foreach (var t in w.SubjectTypeDefs.Values)
        {
            Assert.InRange(t.AutonomyMin, 0f, 1f);
            Assert.InRange(t.YieldShare, 0f, 1f);
            Assert.InRange(t.ManpowerShare, 0f, 1f);
            Assert.True(t.Drift > 0f, $"{t.Id} nunca se soltava");
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
        }
        // há um degrau de entrada (autonomia zero) e os mínimos não se repetem: para cada autonomia há um
        // degrau e só um, senão o mapa dizia uma coisa e a ficha outra
        Assert.Contains(w.SubjectTypeDefs.Values, t => t.AutonomyMin == 0f);
        Assert.Equal(w.SubjectTypeDefs.Count, w.SubjectTypeDefs.Values.Select(t => t.AutonomyMin).Distinct().Count());
    }

    /// <summary>A coleira mais apertada é também a que paga mais e a que se solta mais devagar. Se a tabela
    /// deixasse de ser assim havia um degrau que ninguém escolhia — mais autonomia por menos tributo.</summary>
    [Fact]
    public void ALooserCollarPaysLessAndBreaksFasterThanATighterOne()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        var steps = w.SubjectTypeDefs.Values.OrderBy(t => t.AutonomyMin).ToList();
        for (int i = 1; i < steps.Count; i++)
        {
            Assert.True(steps[i].YieldShare < steps[i - 1].YieldShare, $"{steps[i].Id} não paga menos");
            Assert.True(steps[i].ManpowerShare <= steps[i - 1].ManpowerShare, $"{steps[i].Id} não dá menos homens");
            Assert.True(steps[i].Drift > steps[i - 1].Drift, $"{steps[i].Id} não se solta mais depressa");
        }
    }

    [Fact]
    public void TheStepIsReadFromTheAutonomyAndAFreeCountryHasNone()
    {
        var (w, db) = Vassalage(); using var _ = db;
        var sub = w.Countries[2];
        Assert.Null(Subjects.Level(w, sub));                       // livre: não está degrau nenhum

        Subjects.Puppet(w, 1, 2);
        var steps = w.SubjectTypeDefs.Values.OrderBy(t => t.AutonomyMin).ToList();
        Assert.Equal(steps[0].Id, Subjects.Level(w, sub)!.Id);     // entra no mais fundo

        foreach (var t in steps)
        {
            sub.Autonomy = t.AutonomyMin;
            Assert.Equal(t.Id, Subjects.Level(w, sub)!.Id);
        }
    }

    /// <summary>O tributo não é uma segunda conta do rendimento: é a fatia do degrau sobre a mesma função
    /// pública com que o EconomySystem paga o dia. Se alguém mudar a fórmula do rendimento, o tributo muda
    /// com ela — que é a razão de ele não ter cópia nenhuma.</summary>
    [Fact]
    public void TheTributeIsASliceOfTheSameDayTheVassalEarned()
    {
        var (w, db) = Vassalage(); using var _ = db;
        Subjects.Puppet(w, 1, 2);
        var sub = w.Countries[2];
        var lvl = Subjects.Level(w, sub)!;

        Assert.Equal(EconomySystem.Income(w, 2) * lvl.YieldShare, Subjects.Tribute(w, sub), 4);
        Assert.Equal(ManpowerSystem.Gain(w, sub, ManpowerSystem.Pop(w, 2)) * lvl.ManpowerShare, Subjects.Levy(w, sub), 4);
    }

    [Fact]
    public void WhatTheVassalPaysLandsInTheOverlordsPocket()
    {
        var (w, db) = Vassalage(); using var _ = db;
        Subjects.Puppet(w, 1, 2);
        var lord = w.Countries[1]; var sub = w.Countries[2];
        // os dois a zero: o que estiver lá depois do dia é o dia, e o pool não bate no tecto por cima
        sub.Money = 100f; lord.Money = 0f; sub.Manpower = 0f; lord.Manpower = 0f;
        float tribute = Subjects.Tribute(w, sub), levy = Subjects.Levy(w, sub);
        float lordDay = ManpowerSystem.Gain(w, lord, ManpowerSystem.Pop(w, 1));
        float subDay = ManpowerSystem.Gain(w, sub, ManpowerSystem.Pop(w, 2));
        Assert.True(tribute > 0f && levy > 0f, "o vassalo não rendia nada — o teste não provava nada");

        w.Tick();

        // o suserano recebeu exactamente o tributo (o rendimento próprio dele entra por cima, e por isso
        // compara-se a diferença e não o total)
        Assert.Equal(tribute, lord.Money - EconomySystem.Income(w, 1), 3);
        Assert.Equal(lordDay + levy, lord.Manpower, 2);
        Assert.Equal(subDay - levy, sub.Manpower, 2);
        Assert.Equal(100f + EconomySystem.Income(w, 2) - tribute, sub.Money, 3);
    }

    [Fact]
    public void TheCollarLoosensEveryDayAndTheVassalWalksAwayAtTheTop()
    {
        var (w, db) = Vassalage(); using var _ = db;
        Subjects.Puppet(w, 1, 2);
        var sub = w.Countries[2];
        int freed = 0;
        w.Events.Subscribe<SubjectFreed>(e => { if (e.SubjectId == 2 && e.OverlordId == 1) freed++; });

        w.Tick();
        Assert.True(sub.Autonomy > 0f, "a autonomia não andou");

        // na véspera do topo ainda obedece; ao chegar lá levanta-se e ninguém lhe cobra mais nada
        sub.Autonomy = w.Rule("subject_free_autonomy", 1f) - Subjects.Level(w, sub)!.Drift * 0.5f;
        w.Tick();
        Assert.False(sub.IsSubject);
        Assert.Equal(0f, sub.Autonomy);
        Assert.Equal(1, freed);

        float lordMoney = w.Countries[1].Money;
        w.Tick();
        Assert.Equal(EconomySystem.Income(w, 1), w.Countries[1].Money - lordMoney, 3);   // já não entra tributo nenhum
    }

    /// <summary>Quem se bate cobra a conta mais depressa: um vassalo em guerra ganha autonomia ao ritmo do
    /// degrau mais a regra da guerra. É a diferença entre uma coleira que se compra com sangue e uma que só
    /// se espera.</summary>
    [Fact]
    public void AVassalAtWarEarnsItsFreedomFaster()
    {
        var (w, db) = Vassalage(); using var _ = db;
        Subjects.Puppet(w, 1, 2);
        var sub = w.Countries[2];
        w.Tick();
        float quiet = sub.Autonomy;

        var (w2, db2) = Vassalage(); using var _2 = db2;
        w2.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 1 };
        Subjects.Puppet(w2, 1, 2);
        w2.StartWar(2, 3);
        w2.Tick();

        Assert.True(w2.Countries[2].Autonomy > quiet, "a guerra não acelerou nada");
        Assert.Equal(quiet + w.Rule("subject_autonomy_war", 0.0035f), w2.Countries[2].Autonomy, 5);
        Assert.True(Subjects.DaysToFreedom(w2, w2.Countries[2]) < Subjects.DaysToFreedom(w, sub));
    }

    [Fact]
    public void NoOneKneelsWhileTheyStillHaveACountryLeft()
    {
        var (w, db) = Vassalage(); using var _ = db;
        w.StartWar(1, 2);
        var v = PeaceTerms.PuppetVerdict(w, 1, 2);
        Assert.False(v.Accepted);                              // guerra acabada de começar: nada ocupado
        Assert.Equal(w.Rule("puppet_price", 0.85f), v.Price, 4);

        new PuppetCommand(1, 2).Execute(w);
        Assert.False(w.Countries[2].IsSubject);
        Assert.True(w.AreAtWar(1, 2));                         // recusa não faz paz nenhuma
    }

    [Fact]
    public void WithTheWholeCountryOccupiedTheDefeatedKneelsAndKeepsItsLand()
    {
        var (w, db) = Vassalage(); using var _ = db;
        w.StartWar(1, 2);
        foreach (var r in w.Regions.Values.Where(r => r.OwnerId == 2)) r.ControllerId = 1;
        w.Countries[2].WarExhaustion = w.Rule("exhaustion_max", 30f);
        Assert.True(PeaceTerms.PuppetVerdict(w, 1, 2).Accepted);

        int made = 0;
        w.Events.Subscribe<SubjectMade>(e => { if (e.SubjectId == 2 && e.OverlordId == 1) made++; });
        new PuppetCommand(1, 2).Execute(w);

        Assert.Equal(1, made);
        Assert.Equal(1, w.Countries[2].OverlordId);
        Assert.False(w.AreAtWar(1, 2));
        // a terra continua dele e volta à mão dele: um fantoche não é uma ocupação
        Assert.All(w.Regions.Values.Where(r => r.OwnerId == 2), r => Assert.Equal(2, r.ControllerId));
        Assert.Contains(w.Countries[2], Subjects.Of(w, 1));
    }

    [Fact]
    public void TheCommandRefusesWhatWouldMakeNoSense()
    {
        var (w, db) = Vassalage(); using var _ = db;
        Assert.NotNull(new PuppetCommand(1, 1).Validate(w));        // a si próprio
        Assert.NotNull(new PuppetCommand(1, 99).Validate(w));       // país que não existe
        Assert.NotNull(new PuppetCommand(1, 2).Validate(w));        // sem guerra

        w.StartWar(1, 2);
        Assert.Null(new PuppetCommand(1, 2).Validate(w));

        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 1 };
        Subjects.Puppet(w, 3, 2);
        Assert.NotNull(new PuppetCommand(1, 2).Validate(w));        // já é fantoche de outro
        Assert.NotNull(new PuppetCommand(2, 1).Validate(w));        // um vassalo não faz vassalos
    }

    [Fact]
    public void TheOverlordCanLetGoAndTheVassalStopsPaying()
    {
        var (w, db) = Vassalage(); using var _ = db;
        Subjects.Puppet(w, 1, 2);
        Assert.NotNull(new ReleaseSubjectCommand(2, 1).Validate(w));   // ao contrário não vale

        new ReleaseSubjectCommand(1, 2).Execute(w);
        Assert.False(w.Countries[2].IsSubject);
        Assert.Empty(Subjects.Of(w, 1));
        Assert.Equal(0f, Subjects.Tribute(w, w.Countries[2]));
    }

    /// <summary>O mapa: a terra de quem obedece pinta-se, a de quem manda em si não. É o modo em que se vê
    /// de relance onde acaba um império.</summary>
    [Fact]
    public void TheMapPaintsTheLandOfWhoeverObeys()
    {
        var (w, db) = Vassalage(); using var _ = db;
        var theirs = w.Regions.Values.First(r => r.OwnerId == 2);
        Assert.Null(MapModes.Value(w, 1, theirs, "subject"));
        Assert.Equal("país livre", MapModes.Text(w, 1, theirs, "subject"));

        Subjects.Puppet(w, 1, 2);
        Assert.Equal(1f, MapModes.Value(w, 1, theirs, "subject")!.Value, 3);   // acabado de cair: preso todo
        Assert.Contains("Alfa", MapModes.Text(w, 1, theirs, "subject"));
        Assert.Null(MapModes.Value(w, 1, w.Regions.Values.First(r => r.OwnerId == 1), "subject"));

        w.Countries[2].Autonomy = w.Rule("subject_free_autonomy", 1f) * 0.5f;
        Assert.Equal(0.5f, MapModes.Value(w, 1, theirs, "subject")!.Value, 2);
    }

    /// <summary>A vassalagem é das poucas coisas que o país carrega no save — o degrau não, que se lê da
    /// autonomia. Sem isto um jogo recarregado devolvia impérios inteiros à liberdade.</summary>
    [Fact]
    public void TheSaveCarriesTheOverlordAndTheAutonomy()
    {
        var (w, staticDb) = Vassalage(); using var _ = staticDb;
        Subjects.Puppet(w, 1, 2);
        w.Countries[2].Autonomy = 0.42f;

        using var save = new MsSqliteDatabase();
        var repo = new SqlWorldRepository(staticDb);
        SqlWorldRepository.EnsureSaveSchema(save, File.ReadAllText("data/schema.sql"));
        repo.WriteSave(w, save);

        var (w2, db2) = Vassalage(); using var _2 = db2;
        repo.LoadSave(w2, save);
        Assert.Equal(1, w2.Countries[2].OverlordId);
        Assert.Equal(0.42f, w2.Countries[2].Autonomy, 4);
        Assert.Equal(Subjects.Level(w, w.Countries[2])!.Id, Subjects.Level(w2, w2.Countries[2])!.Id);
    }

    /// <summary>Sem a tabela carregada o mundo corre como antes: ninguém cobra nada a ninguém. É o mesmo
    /// contrato do tempo e das tácticas — uma base de dados velha não parte o jogo.</summary>
    [Fact]
    public void WithoutTheTableNobodyPaysTribute()
    {
        var (w, db) = Vassalage(); using var _ = db;
        Subjects.Puppet(w, 1, 2);
        w.SubjectTypeDefs.Clear();
        var sub = w.Countries[2];
        sub.Autonomy = 0f;
        float lordMoney = w.Countries[1].Money;

        w.Tick();

        Assert.Equal(0f, sub.Autonomy);
        Assert.Equal(EconomySystem.Income(w, 1), w.Countries[1].Money - lordMoney, 3);
        Assert.Null(Subjects.Level(w, sub));
        Assert.Equal(0f, Subjects.Tribute(w, sub));
        Assert.NotNull(new PuppetCommand(1, 2).Validate(w));
    }
}
