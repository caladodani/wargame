using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Troca negociada de prisioneiros: homem por homem com a guerra a decorrer. Os números da
/// viagem e da aceitação vêm das regras — aqui mede-se a mecânica.</summary>
public class PrisonerExchangeTests
{
    private static (World w, Country a, Country b) Setup(int weHold = 100_000, int theyHold = 100_000)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var a = w.Countries[1]; var b = w.Countries[2];
        a.AtWarWith.Add(2); b.AtWarWith.Add(1);
        a.Manpower = 5_000_000f; b.Manpower = 5_000_000f;
        if (weHold > 0) a.Prisoners[2] = weHold;
        if (theyHold > 0) b.Prisoners[1] = theyHold;
        return (w, a, b);
    }

    [Fact]
    public void TheTermsComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.InRange(w.Rule("exchange_return"), 0.01f, 1f);
        Assert.True(w.Rule("exchange_ai_edge") > 1f);
        Assert.True(w.Rule("exchange_need_men") > 0f);
    }

    [Fact]
    public void ItIsManForMan_AndTheSmallerCampSetsTheSize()
    {
        var (w, _, _) = Setup(weHold: 30_000, theyHold: 90_000);
        var offer = PrisonerExchange.Evaluate(w, 1, 2);

        Assert.Equal(30_000, offer.Men);
        Assert.Equal((int)(30_000 * w.Rule("exchange_return", 0.85f)), offer.Home);
    }

    [Fact]
    public void WithOneCampEmpty_ThereIsNothingToTrade()
    {
        var (w, _, _) = Setup(weHold: 50_000, theyHold: 0);
        var offer = PrisonerExchange.Evaluate(w, 1, 2);

        Assert.Equal(0, offer.Men);
        Assert.False(offer.Accepted);
        Assert.Equal("a troca é homem por homem: um dos campos está vazio",
                     new ExchangePrisonersCommand(1, 2).Validate(w));
    }

    [Fact]
    public void BothCampsEmptyIsSaidPlainly()
    {
        var (w, _, _) = Setup(weHold: 0, theyHold: 0);
        Assert.Equal("não há prisioneiros de lado nenhum", PrisonerExchange.Evaluate(w, 1, 2).Reason);
    }

    [Fact]
    public void TheExchangeEmptiesBothCampsAndFillsBothPools()
    {
        var (w, a, b) = Setup(weHold: 40_000, theyHold: 40_000);
        float mine = a.Manpower, theirs = b.Manpower;
        float back = w.Rule("exchange_return", 0.85f);

        Assert.Null(new ExchangePrisonersCommand(1, 2).Validate(w));
        new ExchangePrisonersCommand(1, 2).Execute(w);

        Assert.Empty(a.Prisoners);
        Assert.Empty(b.Prisoners);
        Assert.Equal(mine + 40_000 * back, a.Manpower, 0);
        Assert.Equal(theirs + 40_000 * back, b.Manpower, 0);
    }

    [Fact]
    public void TheBiggerCampKeepsWhatWasNotTraded()
    {
        var (w, a, b) = Setup(weHold: 90_000, theyHold: 20_000);

        new ExchangePrisonersCommand(1, 2).Execute(w);

        Assert.Equal(70_000, a.Prisoners[2]);      // o resto continua a trabalhar na nossa retaguarda
        Assert.Empty(b.Prisoners);
    }

    [Fact]
    public void NotEverybodySurvivesTheTrip()
    {
        var (w, a, _) = Setup(weHold: 10_000, theyHold: 10_000);
        w.Rules["exchange_return"] = 0.5f;
        float mine = a.Manpower;

        new ExchangePrisonersCommand(1, 2).Execute(w);

        Assert.Equal(mine + 5_000f, a.Manpower, 0);
    }

    [Fact]
    public void AnEnemyHoldingFarMoreMenRefuses()
    {
        var (w, _, _) = Setup(weHold: 10_000, theyHold: 100_000);
        var offer = PrisonerExchange.Evaluate(w, 1, 2);

        Assert.False(offer.Accepted);
        Assert.StartsWith("recusam a troca:", new ExchangePrisonersCommand(1, 2).Validate(w));
    }

    [Fact]
    public void AnEnemyOutOfMenAcceptsEvenSo()
    {
        var (w, _, b) = Setup(weHold: 10_000, theyHold: 100_000);
        b.Manpower = w.Rule("exchange_need_men", 150_000f) - 1f;

        Assert.True(PrisonerExchange.Evaluate(w, 1, 2).Accepted);
        Assert.Null(new ExchangePrisonersCommand(1, 2).Validate(w));
    }

    [Fact]
    public void WithoutAWar_TheCampsStayShut()
    {
        var (w, a, b) = Setup();
        a.AtWarWith.Clear(); b.AtWarWith.Clear();

        Assert.Equal("sem guerra não há campos para abrir", new ExchangePrisonersCommand(1, 2).Validate(w));
    }

    [Fact]
    public void TheExchangeIsAnnouncedAndMakesTheChronicle()
    {
        var (w, _, _) = Setup(weHold: 40_000, theyHold: 40_000);
        var seen = new List<PrisonersExchanged>();
        w.Events.Subscribe<PrisonersExchanged>(seen.Add);
        w.Register(new ChronicleSystem());
        w.Tick();

        new ExchangePrisonersCommand(1, 2).Execute(w);

        var e = Assert.Single(seen);
        Assert.Equal(40_000, e.Men);
        Assert.Equal((int)(40_000 * w.Rule("exchange_return", 0.85f)), e.Home);
        var line = Assert.Single(w.Chronicle, x => x.Kind == "prisioneiros");
        Assert.Contains("trocam", line.Text);
    }

    [Fact]
    public void TradedMenStopWorkingForTheIndustry()
    {
        var (w, a, _) = Setup(weHold: 500_000, theyHold: 500_000);
        var sys = new PrisonerSystem();
        sys.Tick(w);
        float before = a.PrisonerMult.GetValueOrDefault("industry", 1f);

        new ExchangePrisonersCommand(1, 2).Execute(w);
        sys.Tick(w);

        Assert.True(before > 1f);
        Assert.Equal(1f, a.PrisonerMult.GetValueOrDefault("industry", 1f));
    }
}
