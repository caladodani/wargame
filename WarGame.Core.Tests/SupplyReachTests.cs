using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Alcance da rede de abastecimento: em casa come-se sempre bem, e cada região tomada afasta a
/// tropa do depósito até o fio ficar fino. As estradas da terra tomada decidem quanto custa cada salto.</summary>
public class SupplyReachTests
{
    /// <summary>Mapa em linha de 14 regiões: 1..3 do país 1, 4..14 do país 2 — espaço para esticar bem
    /// a linha para lá do que a rede alcança de graça.</summary>
    private static World Build(int n = 14)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, n: n, split: 3);
        w.Register(new SupplySystem());
        return w;
    }

    private static float Free(World w) => w.Rule("supply_reach_free", 3f);
    private static float Decay(World w) => w.Rule("supply_reach_decay", 0.12f);
    private static float Floor(World w) => w.Rule("supply_reach_min", 0.5f);

    /// <summary>Toma as regiões 4..até (inclusive) para o país 1, como uma ofensiva que avançou.</summary>
    private static void Advance(World w, int until)
    {
        for (int i = 4; i <= until; i++) w.Regions[i].ControllerId = 1;
    }

    [Fact]
    public void TheRulesComeFromTheDatabase()
    {
        var w = Build();
        Assert.Equal(3f, Free(w), 3);
        Assert.Equal(0.12f, Decay(w), 3);
        Assert.Equal(0.5f, Floor(w), 3);
        Assert.Equal(1f, SupplySystem.Reach(w, 0f), 3);
        Assert.Equal(1f, SupplySystem.Reach(w, 3f), 3);                 // o terceiro salto ainda é de graça
        Assert.Equal(1f - 2f * 0.12f, SupplySystem.Reach(w, 5f), 3);
    }

    [Fact]
    public void HomeGroundIsAlwaysFullySupplied()
    {
        var w = Build(n: 30);
        for (int i = 1; i <= 20; i++) { w.Regions[i].OwnerId = 1; w.Regions[i].ControllerId = 1; }
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 20);      // 19 regiões da capital, tudo nosso
        TestWorld.Days(w, 1);
        Assert.Equal(0f, d.SupplyDepth, 3);                             // em casa a rede já está montada
        Assert.Equal(1f, d.Supply, 3);
    }

    [Fact]
    public void PushingDeepIntoTakenGroundThinsTheLine()
    {
        var w = Build();
        Advance(w, 9);
        var near = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 6);    // 3 regiões tomadas: dentro do alcance
        var far = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 9);     // 6 tomadas: três a mais
        TestWorld.Days(w, 1);

        Assert.Equal(3f, near.SupplyDepth, 3);
        Assert.Equal(1f, near.Supply, 3);
        Assert.Equal(6f, far.SupplyDepth, 3);
        Assert.Equal(1f - 3f * Decay(w), far.Supply, 3);
        Assert.False(far.Cut);                                          // esticada não é cortada
    }

    [Fact]
    public void TheLineNeverThinsBelowTheFloor()
    {
        var w = Build(n: 40);
        for (int i = 4; i <= 40; i++) w.Regions[i].ControllerId = 1;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 40);      // 37 regiões tomadas a fio
        TestWorld.Days(w, 1);
        Assert.Equal(37f, d.SupplyDepth, 3);
        Assert.Equal(Floor(w), d.Supply, 3);
    }

    [Fact]
    public void GoodRoadsBringTheDepotCloser()
    {
        var w = Build();
        Advance(w, 9);
        foreach (var r in w.Regions.Values) r.Infrastructure = 2f;      // via férrea por toda a terra tomada
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 9);
        TestWorld.Days(w, 1);
        Assert.Equal(3f, d.SupplyDepth, 3);                             // seis regiões, meia distância cada
        Assert.Equal(1f, d.Supply, 3);

        foreach (var r in w.Regions.Values) r.Infrastructure = 0.5f;    // caminhos de cabras: o dobro
        TestWorld.Days(w, 1);
        Assert.Equal(12f, d.SupplyDepth, 3);                            // as mesmas seis regiões, ao dobro
        Assert.Equal(Floor(w), d.Supply, 3);                            // e a linha cai ao chão
    }

    [Fact]
    public void AnnexingTheGroundRebuildsTheNetwork()
    {
        var w = Build();
        Advance(w, 9);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 9);
        TestWorld.Days(w, 1);
        Assert.Equal(1f - 3f * Decay(w), d.Supply, 3);

        for (int i = 4; i <= 8; i++) w.Regions[i].OwnerId = 1;          // a paz passou a terra a nossa
        TestWorld.Days(w, 1);
        Assert.Equal(1f, d.SupplyDepth, 3);                             // só a região 9 continua tomada
        Assert.Equal(1f, d.Supply, 3);
    }

    [Fact]
    public void ACutDivisionIsStillAPocketNotAStretchedLine()
    {
        var w = Build();
        Advance(w, 9);
        w.Regions[6].ControllerId = 2;                                  // o corredor partiu-se atrás dela
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 9);
        TestWorld.Days(w, 1);
        Assert.True(d.Cut);
        Assert.Equal(0f, d.SupplyDepth, 3);
        Assert.Equal(w.Rule("supply_pocket", 0.5f), d.Supply, 3);
    }

    [Fact]
    public void TheShortestWayHomeIsTheOneThatCounts()
    {
        var w = Build();
        Advance(w, 9);
        w.Regions[9].Neighbours.Add(3);                                 // atalho: a região 9 encosta a casa
        w.Regions[3].Neighbours.Add(9);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 9);
        TestWorld.Days(w, 1);
        Assert.Equal(1f, d.SupplyDepth, 3);                             // um salto pelo atalho, não seis pela estrada
        Assert.Equal(1f, d.Supply, 3);
    }
}
