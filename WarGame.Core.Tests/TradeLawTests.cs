using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Leis de comércio: quanto dos depósitos controlados é que pode sair do país, e a que preço.
/// Apertar a lei rasga os contratos que ficarem acima do tecto.</summary>
public class TradeLawTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.ResourceDefs["aco"] = new ResourceDef("aco", "Aço", "production_speed", 0.02f, 10f);
        w.Regions[4].Resources["aco"] = 4f;      // país 2 tem 4 de aço
        w.Countries[1].Money = 300f;
        w.Countries[2].Money = 300f;
        // O TestWorld cria os países depois do LoadStatic, e é o LoadStatic que aplica as leis por omissão;
        // sem isto os dois andariam sem lei de comércio nenhuma (export_share a 1).
        foreach (var c in w.Countries.Values) w.ApplyTechs(c);
        w.Register(new TradeSystem());
        w.Register(new ResourceSystem());
        return w;
    }

    [Fact]
    public void TheLawsOfTradeComeFromTheDatabaseAndTheDefaultIsExportFocus()
    {
        var w = Setup();
        var laws = w.Laws.Values.Where(l => l.Group == "trade").OrderBy(l => l.Sort).ToList();
        Assert.Equal(4, laws.Count);
        Assert.Equal(new[] { "com_livre", "com_exportacao", "com_limitado", "com_fechado" }, laws.Select(l => l.Id));

        Assert.Equal("com_exportacao", w.ActiveLaw(w.Countries[2], "trade")!.Id);
        Assert.Equal(0.8f, w.Countries[2].Stat("export_share", 1f), 3);
    }

    [Fact]
    public void TheLawSetsTheCeilingOfWhatThisCountryMaySell()
    {
        var w = Setup();
        Assert.Equal(3.2f, TradeSystem.ExportCap(w, 2, "aco"), 3);      // 4 controladas × 0,8
        Assert.Equal(3.2f, TradeSystem.Free(w, 2, "aco"), 3);

        Assert.Null(new CreateTradeDealCommand(1, 2, "aco", 3f).Validate(w));
        Assert.Equal("a lei de comércio dele só deixa vender 3.2 unidades",
                     new CreateTradeDealCommand(1, 2, "aco", 3.5f).Validate(w));
    }

    [Fact]
    public void WhatIsAlreadyPromisedComesOutOfTheCeiling()
    {
        var w = Setup();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        Assert.Equal(0.2f, TradeSystem.Free(w, 2, "aco"), 3);
    }

    [Fact]
    public void TighteningTheLawTearsUpTheContractsAboveTheNewCeiling()
    {
        var w = Setup();
        new CreateTradeDealCommand(1, 2, "aco", 3f).Execute(w);
        var ended = new List<TradeDealEnded>();
        w.Events.Subscribe<TradeDealEnded>(ended.Add);

        TestWorld.Days(w, 1);
        Assert.Single(w.TradeDeals);                                    // com a lei de sempre o contrato aguenta

        new ChangeLawCommand(2, "com_limitado").Execute(w);             // metade fica em casa: tecto de 2
        Assert.Equal(2f, TradeSystem.ExportCap(w, 2, "aco"), 3);
        TestWorld.Days(w, 1);
        Assert.Empty(w.TradeDeals);
        Assert.Single(ended);
    }

    [Fact]
    public void OpenPortsSellCheapAndAClosedEconomyChargesAPremium()
    {
        var w = Setup();
        float focus = TradeSystem.Price(w, 2, "aco");

        new ChangeLawCommand(2, "com_livre").Execute(w);
        float open = TradeSystem.Price(w, 2, "aco");
        new ChangeLawCommand(2, "com_fechado").Execute(w);
        float shut = TradeSystem.Price(w, 2, "aco");

        Assert.True(open < focus, $"portos abertos vendem barato ({open} < {focus})");
        Assert.True(shut > focus, $"economia fechada cobra prémio ({shut} > {focus})");
        Assert.Equal(open * (1.3f / 0.85f), shut, 3);
    }

    [Fact]
    public void AClosedEconomyTradesItsLaboratoriesForFactories()
    {
        var w = Setup();
        var c = w.Countries[1];
        Assert.Equal(1.05f, c.Stat("research_speed"), 3);               // o foco na exportação já dá ciência

        new ChangeLawCommand(1, "com_fechado").Execute(w);
        Assert.Equal(0.90f, c.Stat("research_speed"), 3);
        Assert.Equal(1.10f, c.Stat("industry"), 3);
        Assert.Equal(0.2f, c.Stat("export_share", 1f), 3);
    }

    [Fact]
    public void ThePriceLockedInTheTreatySurvivesTheLawThatComesAfter()
    {
        var w = Setup();
        var cmd = new CreateTradeDealCommand(1, 2, "aco", 2f, 30);
        cmd.Execute(w);
        float locked = w.TradeDeals[0].PricePerUnit;

        new ChangeLawCommand(2, "com_fechado").Execute(w);              // o mercado sobe; o contrato não
        Assert.True(TradeSystem.Price(w, 2, "aco") > locked);
        Assert.Equal(locked, TradeSystem.Paid(w, w.TradeDeals[0]), 3);
    }

    [Fact]
    public void AnApprovedLawGoesIntoTheChronicle()
    {
        var w = Setup();
        w.Register(new ChronicleSystem());
        w.Tick();
        new ChangeLawCommand(1, "com_fechado").Execute(w);

        Assert.Contains(w.Chronicle, e => e.Kind == "lei" && e.Text.Contains("Economia fechada"));
    }
}
