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

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = Assert.Single(w2.Offers);
        Assert.Equal(2, back.FromId);
        Assert.Equal("prisioneiros", back.Kind);
        Assert.Equal(100_000, back.Men);
    }
}
