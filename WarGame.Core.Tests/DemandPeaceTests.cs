using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Paz negociada: exigir território ao derrotado. O país 2 tem as regiões 4,5,6 (capital 6).</summary>
public class DemandPeaceTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        return w;
    }

    /// <summary>Ocupa o país todo (capital incluída): pressão máxima paga qualquer exigência.</summary>
    private static void Occupy(World w, params int[] regions)
    {
        foreach (int id in regions) w.Regions[id].ControllerId = 1;
    }

    [Fact]
    public void Occupier_TakesWhatItHolds()
    {
        var w = Setup();
        Occupy(w, 4, 5, 6);
        var events = new List<IGameEvent>();
        w.Events.Subscribe<PeaceSigned>(events.Add);

        var cmd = new DemandPeaceCommand(1, 2, new[] { 4, 5 });
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.False(w.AreAtWar(1, 2));
        Assert.Equal(1, w.Regions[4].OwnerId);
        Assert.Equal(1, w.Regions[5].OwnerId);
        Assert.Equal(2, w.Regions[6].OwnerId);
        Assert.Equal(2, w.Regions[6].ControllerId);   // o que não foi exigido é devolvido
        Assert.Equal(new IGameEvent[] { new PeaceSigned(1, 2, 2) }, events);
    }

    [Fact]
    public void UnbeatenEnemy_RefusesTheDemand()
    {
        var w = Setup();
        var events = new List<IGameEvent>();
        w.Events.Subscribe<PeaceOfferRejected>(events.Add);

        new DemandPeaceCommand(1, 2, new[] { 4 }).Execute(w);

        Assert.True(w.AreAtWar(1, 2));
        Assert.Equal(2, w.Regions[4].OwnerId);
        Assert.Equal(new IGameEvent[] { new PeaceOfferRejected(1, 2) }, events);
    }

    [Fact]
    public void DemandingUnoccupiedLand_CostsMore()
    {
        var w = Setup();
        Occupy(w, 4);                                     // um terço do país dele
        var held = PeaceTerms.Evaluate(w, 1, 2, new[] { 4 });
        var free = PeaceTerms.Evaluate(w, 1, 2, new[] { 5 });
        Assert.True(free.Price > held.Price);
        Assert.Equal(held.Pressure, free.Pressure, 4);    // a pressão não muda com o que se pede
    }

    [Fact]
    public void HoldingTheCapital_AddsPressure()
    {
        var w = Setup();
        Occupy(w, 4);
        float without = PeaceTerms.Evaluate(w, 1, 2, new[] { 4 }).Pressure;
        w.Regions[4].ControllerId = 2; Occupy(w, 6);       // mesma fatia ocupada, mas é a capital
        float with = PeaceTerms.Evaluate(w, 1, 2, new[] { 6 }).Pressure;
        Assert.True(with > without);
    }

    [Fact]
    public void Validate_RejectsLandThatIsNotTheirs()
    {
        var w = Setup();
        Assert.Equal("só podes exigir regiões dele", new DemandPeaceCommand(1, 2, new[] { 1 }).Validate(w));
        Assert.Equal("não exigiste nada (usa a paz branca)", new DemandPeaceCommand(1, 2, Array.Empty<int>()).Validate(w));
        w.EndWar(1, 2);
        Assert.Equal("não estás em guerra com ele", new DemandPeaceCommand(1, 2, new[] { 4 }).Validate(w));
    }

    [Fact]
    public void Signing_ClearsBattlesAndResistance()
    {
        var w = Setup();
        Occupy(w, 4, 5, 6);
        w.Regions[4].Resistance = 0.5f;
        var d1 = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);
        var d2 = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
        w.ActiveBattles.Add(new Battle { RegionId = 4, AttackerCountryId = 1, Attackers = { d1.Id }, Defenders = { d2.Id } });

        PeaceTerms.Sign(w, 1, 2, new[] { 4 });

        Assert.Empty(w.ActiveBattles);
        Assert.Equal(0f, w.Regions[4].Resistance);
        Assert.Equal(1, w.Regions[4].OwnerId);
    }

    [Fact]
    public void Ai_DemandsWhatItOccupies()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = false;
        w.StartWar(1, 2);
        Occupy(w, 4, 5, 6);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);
        var ai = new AiSystem();

        ai.Tick(w);

        Assert.False(w.AreAtWar(1, 2));
        Assert.Equal(1, w.Regions[4].OwnerId);
        Assert.Equal(1, w.Regions[6].OwnerId);
    }

    [Fact]
    public void Ai_DoesNotDemandFromThePlayer()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[2].IsPlayer = true;
        w.StartWar(1, 2);
        Occupy(w, 4, 5, 6);
        new AiSystem().Tick(w);
        Assert.True(w.AreAtWar(1, 2));
        Assert.Equal(2, w.Regions[4].OwnerId);
    }
}
