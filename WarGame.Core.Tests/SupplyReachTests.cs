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





}
