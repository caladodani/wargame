using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Termos sugeridos de paz: a maior exigência que o derrotado ainda assina.
/// Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2).</summary>
public class PeaceSuggestTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        return w;
    }

    /// <summary>País 1 esmaga o país 2: ocupa-lhe as regiões pedidas e tem exército de sobra.</summary>
    private static void Crush(World w, params int[] occupied)
    {
        foreach (int id in occupied) w.Regions[id].ControllerId = 1;
        for (int i = 1; i <= 10; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 1);
        w.Countries[2].WarExhaustion = w.Rule("exhaustion_max", 30f);
    }

    [Fact]
    public void NoPressure_NoTerms()
    {
        var w = Build();
        Assert.Empty(PeaceTerms.Suggest(w, 1, 2));
    }

    [Fact]
    public void WhatIsSuggested_IsAlwaysAccepted()
    {
        var w = Build();
        Crush(w, 4, 5, 6);
        var terms = PeaceTerms.Suggest(w, 1, 2);
        Assert.NotEmpty(terms);
        Assert.True(PeaceTerms.Evaluate(w, 1, 2, terms).Accepted);
    }

    [Fact]
    public void OnlyEnemyRegions_AreSuggested()
    {
        var w = Build();
        Crush(w, 4, 5, 6);
        Assert.All(PeaceTerms.Suggest(w, 1, 2), id => Assert.Equal(2, w.Regions[id].OwnerId));
    }

    [Fact]
    public void TheWarGoal_ComesFirst()
    {
        var w = Build();
        Crush(w, 4, 5, 6);
        w.Wars[World.WarKey(1, 2)].Side(1).Goals.Add(6);   // a capital deles é o que viemos buscar
        Assert.Equal(6, PeaceTerms.Suggest(w, 1, 2)[0]);
    }

    [Fact]
    public void WeakOccupier_GetsLessThanEverythingItHolds()
    {
        var w = Build();
        // ocupa uma região só e sem vantagem nenhuma: a pressão não paga uma exigência grande
        w.Regions[4].ControllerId = 1;
        w.Rules["peace_demand_greed"] = 5f;
        var terms = PeaceTerms.Suggest(w, 1, 2);
        Assert.True(terms.Count < PeaceTerms.OccupiedRegions(w, 1, 2).Count + 1);
        Assert.True(terms.Count == 0 || PeaceTerms.Evaluate(w, 1, 2, terms).Accepted);
    }

    [Fact]
    public void SuggestedTerms_CanBeSignedByTheCommand()
    {
        var w = Build();
        Crush(w, 4, 5, 6);
        var terms = PeaceTerms.Suggest(w, 1, 2);
        var cmd = new DemandPeaceCommand(1, 2, terms);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.False(w.AreAtWar(1, 2));
        Assert.All(terms, id => Assert.Equal(1, w.Regions[id].OwnerId));
    }

    [Fact]
    public void UnknownCountries_GiveNoTerms()
    {
        var w = Build();
        Assert.Empty(PeaceTerms.Suggest(w, 1, 99));
        Assert.Empty(PeaceTerms.Suggest(w, 99, 2));
    }

    [Fact]
    public void Ai_FallsBackToTheSuggestionWhenAskingForEverythingFails()
    {
        var w = Build();
        Crush(w, 4, 5, 6);
        // pedir as três de uma vez não passa; a sugestão corta até ao que o outro assina
        w.Rules["peace_demand_greed"] = 4f;
        Assert.False(PeaceTerms.Evaluate(w, 1, 2, new[] { 4, 5, 6 }).Accepted);
        var terms = PeaceTerms.Suggest(w, 1, 2);
        Assert.NotEmpty(terms);

        new AiSystem().Tick(w);
        Assert.False(w.AreAtWar(1, 2));
    }
}
