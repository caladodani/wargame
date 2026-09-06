using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Defesa da retaguarda: a guarnição do dono da região apanha equipas de sabotagem. As
/// probabilidades vêm das regras — nos testes põem-se a 1 ou a 0 para ficar só a mecânica.</summary>
public class CounterIntelTests
{
    private static (World w, Country a, Country b) Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var a = w.Countries[1]; var b = w.Countries[2];
        a.AtWarWith.Add(2); b.AtWarWith.Add(1);
        a.Money = 1000f;
        return (w, a, b);
    }

    private static SpyOp Op(World w, string effect = "sabotage_fort") => w.SpyOps.Values.First(o => o.Effect == effect);

    /// <summary>Lança a sabotagem do país 1 contra a região 5 (do país 2).</summary>
    private static void Launch(World w, string effect = "sabotage_fort")
    {
        w.Regions[5].Fort = 3;
        new StartSpyOpCommand(1, 2, Op(w, effect).Id, 5).Execute(w);
    }

    [Fact]
    public void TheOddsComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.Rule("catch_base") > 0f);
        Assert.True(w.Rule("catch_guard") > 0f);
        Assert.True(w.Rule("catch_max") > w.Rule("catch_base"));
    }

    [Fact]
    public void WithTheOddsAtZero_TheTeamGetsThrough()
    {
        var (w, _, _) = Setup();
        w.Rules["catch_base"] = 0f; w.Rules["catch_guard"] = 0f;
        Launch(w);
        w.Register(new CounterIntelSystem());
        TestWorld.Days(w, 10);

        Assert.Single(w.ActiveSpyOps);
    }

    [Fact]
    public void ACaughtTeamLosesTheOperation()
    {
        var (w, a, _) = Setup();
        w.Rules["catch_base"] = 1f; w.Rules["catch_max"] = 1f;   // o tecto também sobe: aqui quer-se o caso certo
        Launch(w);
        float money = a.Money;
        var seen = new List<SabotageFoiled>();
        w.Events.Subscribe<SabotageFoiled>(seen.Add);
        w.Register(new CounterIntelSystem());
        w.Tick();

        Assert.Empty(w.ActiveSpyOps);
        Assert.Equal(5, Assert.Single(seen).RegionId);
        Assert.Equal(money, a.Money);                     // apanhados é dinheiro perdido: não há devolução
    }

    [Fact]
    public void TheGarrisonRaisesTheOdds()
    {
        var (w, _, _) = Setup();
        var r = w.Regions[5];
        float bare = CounterIntelSystem.Chance(w, 2, r);
        TestWorld.AddDivision(w, 30, 2, TestWorld.Inf2, 5);
        TestWorld.AddDivision(w, 31, 2, TestWorld.Inf2, 5);

        Assert.Equal(2, CounterIntelSystem.Guards(w, 2, r));
        Assert.Equal(bare + 2 * w.Rule("catch_guard", 0.02f), CounterIntelSystem.Chance(w, 2, r), 3);
    }

    [Fact]
    public void OurOwnDivisionsInTheirGroundDoNotGuardIt()
    {
        var (w, _, _) = Setup();
        TestWorld.AddDivision(w, 30, 1, TestWorld.Inf, 5);      // tropa nossa na região deles
        Assert.Equal(0, CounterIntelSystem.Guards(w, 2, w.Regions[5]));
    }

    [Fact]
    public void TheOddsHaveACeiling()
    {
        var (w, _, _) = Setup();
        for (int i = 0; i < 200; i++) TestWorld.AddDivision(w, 100 + i, 2, TestWorld.Inf2, 5);

        Assert.Equal(w.Rule("catch_max", 0.35f), CounterIntelSystem.Chance(w, 2, w.Regions[5]), 3);
    }

    [Fact]
    public void TheSecurityLawMakesTheRearHarder()
    {
        var (w, _, b) = Setup();
        float before = CounterIntelSystem.Chance(w, 2, w.Regions[5]);
        b.Stats["counter_intel"] = 2f;

        Assert.True(CounterIntelSystem.Chance(w, 2, w.Regions[5]) > before);
    }

    [Fact]
    public void AnOfficeNetworkAgainstTheCountryIsNeverCaughtThisWay()
    {
        var (w, a, _) = Setup();
        w.Rules["catch_base"] = 1f; w.Rules["catch_max"] = 1f;   // o tecto também sobe: aqui quer-se o caso certo
        new StartSpyOpCommand(1, 2, w.SpyOps.Values.First(o => o.Effect == "steal_money").Id).Execute(w);
        w.Register(new CounterIntelSystem());
        TestWorld.Days(w, 5);

        Assert.Single(w.ActiveSpyOps);                    // essa é assunto da contra-espionagem, não da guarnição
    }

    [Fact]
    public void ARegionWeTookBackNoLongerCatchesAnybody()
    {
        var (w, _, _) = Setup();
        w.Rules["catch_base"] = 1f; w.Rules["catch_max"] = 1f;   // o tecto também sobe: aqui quer-se o caso certo
        Launch(w);
        w.Regions[5].ControllerId = 1;                    // a região passou para as nossas mãos
        w.Register(new CounterIntelSystem());
        w.Tick();

        Assert.Single(w.ActiveSpyOps);
    }

    [Fact]
    public void TheCatchMakesTheChronicle()
    {
        var (w, _, _) = Setup();
        w.Rules["catch_base"] = 1f; w.Rules["catch_max"] = 1f;   // o tecto também sobe: aqui quer-se o caso certo
        Launch(w);
        w.Register(new ChronicleSystem());
        w.Register(new CounterIntelSystem());
        w.Tick();

        var line = Assert.Single(w.Chronicle, x => x.Kind == "sabotagem");
        Assert.Contains("Beta apanha", line.Text);
        Assert.Equal(5, line.RegionId);
    }

    [Fact]
    public void ACaughtTeamNeverBlowsAnythingUp()
    {
        var (w, _, _) = Setup();
        w.Rules["catch_base"] = 1f; w.Rules["catch_max"] = 1f;   // o tecto também sobe: aqui quer-se o caso certo
        Launch(w);
        w.Register(new CounterIntelSystem());
        w.Register(new EspionageSystem());
        TestWorld.Days(w, (int)Op(w).Days + 5);

        Assert.Equal(3, w.Regions[5].Fort);               // as casamatas ficaram de pé
    }
}
