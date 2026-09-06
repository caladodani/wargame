using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Propostas do outro lado: a IA olha para os campos e bate à porta com uma troca de
/// prisioneiros. Contra outra IA resolve-se no dia; contra o jogador fica em cima da mesa.</summary>
public class OfferTests
{
    /// <summary>País 1 = jogador, país 2 = IA, em guerra, com campos dos dois lados. Por omissão a IA
    /// guarda menos gente do que nós — a troca convém-lhe.</summary>
    private static (World w, Country a, Country b) Setup(int weHold = 200_000, int theyHold = 100_000)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var a = w.Countries[1]; var b = w.Countries[2];
        a.IsPlayer = true; b.IsPlayer = false;
        a.AtWarWith.Add(2); b.AtWarWith.Add(1);
        a.Manpower = 5_000_000f; b.Manpower = 5_000_000f;
        if (weHold > 0) a.Prisoners[2] = weHold;
        if (theyHold > 0) b.Prisoners[1] = theyHold;
        w.Rules["offer_period_days"] = 1f;      // uma ronda de propostas por dia: o teste não espera
        return (w, a, b);
    }

    [Fact]
    public void TheTermsOfTheTableComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.Rule("offer_period_days") >= 1f);
        Assert.True(w.Rule("offer_days") > 0f);
    }

    [Fact]
    public void TheEnemyPutsATradeOnOurTable()
    {
        var (w, _, _) = Setup();
        var seen = new List<OfferMade>();
        w.Events.Subscribe<OfferMade>(seen.Add);
        w.Register(new OfferSystem());
        w.Tick();

        var offer = Assert.Single(w.Offers);
        Assert.Equal(2, offer.FromId);
        Assert.Equal(1, offer.ToId);
        Assert.Equal(100_000, offer.Men);                 // o mínimo dos dois campos
        Assert.Equal(100_000, Assert.Single(seen).Men);
    }

    [Fact]
    public void NobodyProposesATradeThatDoesNotSuitThem()
    {
        var (w, _, _) = Setup(weHold: 20_000, theyHold: 200_000);   // eles é que têm a vantagem
        w.Register(new OfferSystem());
        w.Tick();

        Assert.Empty(w.Offers);
    }

    [Fact]
    public void OnlyOneOfferPerEnemySitsOnTheTable()
    {
        var (w, _, _) = Setup();
        w.Register(new OfferSystem());
        w.Tick(); w.Tick(); w.Tick();

        Assert.Single(w.Offers);
    }

    [Fact]
    public void BetweenTwoAiCountriesTheAnswerComesTheSameDay()
    {
        // campos parecidos: com uma vantagem grande de mão-de-obra o outro lado recusa e não há troca
        var (w, a, b) = Setup(weHold: 120_000, theyHold: 100_000);
        a.IsPlayer = false;                                // ninguém aqui é jogador
        w.Register(new OfferSystem());
        w.Tick();

        Assert.Empty(w.Offers);                            // nada fica em cima da mesa
        Assert.Empty(b.Prisoners);                         // e a troca foi feita
        Assert.Equal(20_000, a.Prisoners[2]);              // o campo maior guarda o resto
    }

    [Fact]
    public void BetweenAiCountriesTheOneWithTheEdgeStillRefuses()
    {
        var (w, a, b) = Setup(weHold: 200_000, theyHold: 100_000);
        a.IsPlayer = false;
        w.Register(new OfferSystem());
        w.Tick();

        Assert.Equal(200_000, a.Prisoners[2]);             // ninguém entrega a vantagem de mão-de-obra
        Assert.Equal(100_000, b.Prisoners[1]);
    }

    [Fact]
    public void AcceptingOpensBothCamps()
    {
        var (w, a, b) = Setup();
        w.Register(new OfferSystem());
        w.Tick();
        float mine = a.Manpower;

        Assert.Null(new AnswerOfferCommand(1, 2, true).Validate(w));
        new AnswerOfferCommand(1, 2, true).Execute(w);

        Assert.Empty(w.Offers);
        Assert.Empty(b.Prisoners);
        Assert.Equal(mine + 100_000 * w.Rule("exchange_return", 0.85f), a.Manpower, 0);
    }

    [Fact]
    public void RefusingCostsNothingButTakesItOffTheTable()
    {
        var (w, a, b) = Setup();
        w.Register(new OfferSystem());
        w.Tick();
        var seen = new List<OfferAnswered>();
        w.Events.Subscribe<OfferAnswered>(seen.Add);

        new AnswerOfferCommand(1, 2, false).Execute(w);

        Assert.Empty(w.Offers);
        Assert.Equal(200_000, a.Prisoners[2]);
        Assert.Equal(100_000, b.Prisoners[1]);
        Assert.False(Assert.Single(seen).Accepted);
    }

    [Fact]
    public void AnswerWithoutAnOfferIsRefused()
    {
        var (w, _, _) = Setup();
        Assert.Equal("essa proposta já não está em cima da mesa", new AnswerOfferCommand(1, 2, true).Validate(w));
    }

    [Fact]
    public void AnOfferFallsOffTheTableWhenItsTimeIsUp()
    {
        var (w, _, _) = Setup();
        w.Rules["offer_days"] = 3f;
        var seen = new List<OfferExpired>();
        w.Events.Subscribe<OfferExpired>(seen.Add);
        w.Register(new OfferSystem());
        w.Tick();
        w.Rules["offer_period_days"] = 999f;                // não voltam a propor durante o teste
        TestWorld.Days(w, 5);

        Assert.Empty(w.Offers);
        Assert.Single(seen);
    }

    [Fact]
    public void PeaceClearsTheTable()
    {
        var (w, a, b) = Setup();
        w.Register(new OfferSystem());
        w.Tick();
        a.AtWarWith.Clear(); b.AtWarWith.Clear();
        w.Tick();

        Assert.Empty(w.Offers);
        Assert.Equal("essa proposta já não está em cima da mesa", new AnswerOfferCommand(1, 2, true).Validate(w));
    }

    [Fact]
    public void ACampEmptiedMeanwhileKillsTheOffer()
    {
        var (w, a, _) = Setup();
        w.Register(new OfferSystem());
        w.Tick();
        a.Prisoners.Clear();                                // libertámo-los por outra via
        w.Tick();

        Assert.Empty(w.Offers);
    }

    /// <summary>Exércitos dos dois lados. Com a IA batida mas não esmagada (acima de cede_ratio das nossas
    /// divisões) ela pede paz branca em vez de pagar com terra — é o caso destes testes.</summary>
    private static void Armies(World w, int mine, int theirs)
    {
        for (int i = 0; i < mine; i++) TestWorld.AddDivision(w, 30 + i, 1, TestWorld.Inf, 1);
        for (int i = 0; i < theirs; i++) TestWorld.AddDivision(w, 60 + i, 2, TestWorld.Inf2, 6);
    }

    /// <summary>Guerra registada e parada há mais tempo do que peace_stale_days.</summary>
    private static void StaleWar(World w, int a, int b)
    {
        w.StartWar(a, b, w.Clock.Day - (int)w.Rule("peace_stale_days", 60f) - 1);
    }

    [Fact]
    public void AStalledWarBringsAPeaceOfferToOurTable()
    {
        var (w, _, b) = Setup(weHold: 0, theyHold: 0);
        StaleWar(w, 1, 2);
        Armies(w, mine: 5, theirs: 3);                           // estamos por cima, mas não os esmagámos
        w.Register(new OfferSystem());
        w.Tick();

        var offer = Assert.Single(w.Offers);
        Assert.Equal("paz", offer.Kind);
        Assert.Equal(2, offer.FromId);
    }

    [Fact]
    public void AFreshWarBringsNoPeaceOffer()
    {
        var (w, _, _) = Setup(weHold: 0, theyHold: 0);
        w.StartWar(1, 2);                                        // a guerra é de hoje
        TestWorld.AddDivision(w, 30, 1, TestWorld.Inf, 1);
        w.Register(new OfferSystem());
        w.Tick();

        Assert.Empty(w.Offers);
    }

    [Fact]
    public void TheOneWinningOnTheGroundDoesNotAskForPeace()
    {
        var (w, _, _) = Setup(weHold: 0, theyHold: 0);
        StaleWar(w, 1, 2);
        TestWorld.AddDivision(w, 30, 2, TestWorld.Inf2, 5);       // o exército é deles e não ocupamos nada
        w.Register(new OfferSystem());
        w.Tick();

        Assert.Empty(w.Offers);
    }

    [Fact]
    public void SigningTheirPeaceEndsTheWarWhereItStands()
    {
        var (w, a, _) = Setup(weHold: 0, theyHold: 0);
        StaleWar(w, 1, 2);
        Armies(w, mine: 5, theirs: 3);
        w.Regions[5].ControllerId = 1;                           // ocupamos uma região deles
        w.Register(new OfferSystem());
        w.Tick();
        Assert.Single(w.Offers);

        Assert.Null(new AnswerOfferCommand(1, 2, true, "paz").Validate(w));
        new AnswerOfferCommand(1, 2, true, "paz").Execute(w);

        Assert.False(w.AreAtWar(1, 2));
        Assert.Equal(1, w.Regions[5].OwnerId);                   // uti possidetis: fica nossa
        Assert.Empty(w.Offers);
        Assert.Empty(a.AtWarWith);
    }

    [Fact]
    public void RefusingTheirPeaceKeepsTheWar()
    {
        var (w, _, _) = Setup(weHold: 0, theyHold: 0);
        StaleWar(w, 1, 2);
        Armies(w, mine: 5, theirs: 3);
        w.Register(new OfferSystem());
        w.Tick();

        new AnswerOfferCommand(1, 2, false, "paz").Execute(w);

        Assert.True(w.AreAtWar(1, 2));
        Assert.Empty(w.Offers);
    }

    [Fact]
    public void TheTwoSubjectsSitOnTheTableAtTheSameTime()
    {
        var (w, _, _) = Setup(weHold: 200_000, theyHold: 100_000);
        StaleWar(w, 1, 2);
        Armies(w, mine: 5, theirs: 3);
        w.Register(new OfferSystem());
        w.Tick();

        Assert.Equal(2, w.Offers.Count);
        Assert.Contains(w.Offers, o => o.Kind == "paz");
        Assert.Contains(w.Offers, o => o.Kind == "prisioneiros");
        // e responder a uma não mexe na outra
        new AnswerOfferCommand(1, 2, false, "paz").Execute(w);
        Assert.Equal("prisioneiros", Assert.Single(w.Offers).Kind);
    }

    /// <summary>Guerra parada, a IA sem exército nenhum e nós com um: a derrota clara que faz um país
    /// pagar a paz com terra.</summary>
    private static World Beaten()
    {
        var (w, _, _) = Setup(weHold: 0, theyHold: 0);
        StaleWar(w, 1, 2);
        TestWorld.AddDivision(w, 30, 1, TestWorld.Inf, 1);
        w.Register(new OfferSystem());
        return w;
    }

    [Fact]
    public void TheTermsOfACessionComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.Rule("cede_ratio") > 0f);
    }

    [Fact]
    public void ABeatenEnemyPaysThePeaceWithLand()
    {
        var w = Beaten();
        var seen = new List<OfferMade>();
        w.Events.Subscribe<OfferMade>(seen.Add);
        w.Tick();

        var offer = Assert.Single(w.Offers);
        Assert.Equal("regiao", offer.Kind);
        Assert.Equal(4, offer.RegionId);                  // a primeira que é deles e nos faz fronteira
        Assert.Equal(4, Assert.Single(seen).RegionId);
    }

    [Fact]
    public void WithLandOnTheTableTheyDoNotAlsoOfferPlainPeace()
    {
        var w = Beaten();
        w.Tick(); w.Tick();

        Assert.DoesNotContain(w.Offers, o => o.Kind == "paz");
    }

    [Fact]
    public void TheyGiveTheCheapestRegionThatTouchesUs()
    {
        var w = Beaten();
        // uma segunda fronteira: R5 também encosta ao nosso R3, e é a de menos gente
        w.Regions[3].Neighbours.Add(5); w.Regions[5].Neighbours.Add(3);
        var poor = new Region { Id = 5, Name = "R5", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
                                Population = 1_000, CenterX = 500 };
        poor.Neighbours.AddRange(new[] { 4, 6, 3 });
        w.Regions[5] = poor;
        w.Tick();

        Assert.Equal(5, Assert.Single(w.Offers).RegionId);
    }

    [Fact]
    public void AnEnemyStillOnItsFeetKeepsItsLand()
    {
        var (w, _, _) = Setup(weHold: 0, theyHold: 0);
        StaleWar(w, 1, 2);
        TestWorld.AddDivision(w, 30, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 31, 2, TestWorld.Inf2, 5);
        TestWorld.AddDivision(w, 32, 2, TestWorld.Inf2, 6);
        w.Register(new OfferSystem());
        w.Tick();

        Assert.DoesNotContain(w.Offers, o => o.Kind == "regiao");
    }

    [Fact]
    public void NobodyOffersLandWhileTheFrontIsStillMoving()
    {
        var (w, _, _) = Setup(weHold: 0, theyHold: 0);
        w.StartWar(1, 2);                                  // guerra de hoje
        TestWorld.AddDivision(w, 30, 1, TestWorld.Inf, 1);
        w.Register(new OfferSystem());
        w.Tick();

        Assert.Empty(w.Offers);
    }

    [Fact]
    public void TakingTheLandOurselvesKillsThePromise()
    {
        var w = Beaten();
        w.Tick();
        Assert.Single(w.Offers);

        w.Regions[4].OwnerId = 1;                          // anexada pela força entretanto
        w.Rules["offer_period_days"] = 999f;               // e não voltam a propor no mesmo tick
        w.Tick();

        Assert.Empty(w.Offers);
    }

    [Fact]
    public void AcceptingTakesTheLandAndEndsTheWar()
    {
        var w = Beaten();
        var seen = new List<RegionCeded>();
        w.Events.Subscribe<RegionCeded>(seen.Add);
        w.Register(new ChronicleSystem());
        w.Tick();

        Assert.Null(new AnswerOfferCommand(1, 2, true, "regiao").Validate(w));
        new AnswerOfferCommand(1, 2, true, "regiao").Execute(w);

        Assert.Equal(1, w.Regions[4].OwnerId);
        Assert.Equal(1, w.Regions[4].ControllerId);
        Assert.Equal(0f, w.Regions[4].Resistance);         // entregue à mesa: ninguém resiste a um tratado
        Assert.False(w.AreAtWar(1, 2));
        Assert.Equal(4, Assert.Single(seen).RegionId);
        Assert.Contains(w.Chronicle, x => x.Text.Contains("cede"));
    }

    [Fact]
    public void RefusingLeavesTheirLandWhereItWas()
    {
        var w = Beaten();
        w.Tick();

        new AnswerOfferCommand(1, 2, false, "regiao").Execute(w);

        Assert.Equal(2, w.Regions[4].OwnerId);
        Assert.True(w.AreAtWar(1, 2));
        Assert.Empty(w.Offers);
    }

    [Fact]
    public void ThePromisedLandIsCheckedAgainBeforeSigning()
    {
        var w = Beaten();
        w.Tick();
        w.Regions[4].OwnerId = 3;                          // mudou de dono sem passar por nós

        Assert.Equal("a região prometida já não é deles",
                     new AnswerOfferCommand(1, 2, true, "regiao").Validate(w));
    }

    [Fact]
    public void TheTableSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true; w.Countries[2].IsPlayer = false;
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        w.Countries[1].Prisoners[2] = 200_000; w.Countries[2].Prisoners[1] = 100_000;
        w.Rules["offer_period_days"] = 1f;
        w.Register(new OfferSystem());
        w.Tick();
        Assert.Single(w.Offers);
        w.Offers.Add(new PendingOffer { FromId = 2, ToId = 1, Kind = "regiao", RegionId = 4,
                                        Day = w.Clock.Day, ExpiresDay = w.Clock.Day + 20 });

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(2, w2.Offers.Count);
        var back = Assert.Single(w2.Offers, o => o.Kind == "prisioneiros");
        Assert.Equal(2, back.FromId);
        Assert.Equal(100_000, back.Men);
        Assert.Equal(4, Assert.Single(w2.Offers, o => o.Kind == "regiao").RegionId);   // a coluna nova volta
    }
}
