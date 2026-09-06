using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>IA em guerra propõe não-agressão a vizinhos neutros que tendem a aceitar
/// (mais fracos ou com inimigo comum); nunca ao jogador; só com dinheiro acima de ai_nap_reserve.</summary>
public class AiNapTests
{
    /// <summary>Linha 1..6: país 1 (1-2), país 3 neutro (3-4), país 2 (5-6). 1 em guerra com 2.</summary>
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 3, Manpower = 1e9f };
        foreach (int i in new[] { 3, 4 }) { w.Regions[i].OwnerId = w.Regions[i].ControllerId = 3; }
        foreach (int i in new[] { 5, 6 }) { w.Regions[i].OwnerId = w.Regions[i].ControllerId = 2; }
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        w.Countries[1].Money = 500;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2);   // 2 divisões minhas > 0 do vizinho 3
        w.Register(new AiSystem());
        return w;
    }

    [Fact]
    public void AtWar_ProposesNapToWeakerNeutralNeighbour()
    {
        var w = Setup();
        TestWorld.Days(w, 1);   // dia 0: IA corre
        Assert.True(w.HasPact(1, 3));
    }

    [Fact]
    public void PoorCountry_DoesNotPropose()
    {
        var w = Setup();
        w.Countries[1].Money = 50;   // < nap_cost + ai_nap_reserve
        TestWorld.Days(w, 1);
        Assert.False(w.HasPact(1, 3));
    }

    [Fact]
    public void Player_IsNeverTargeted()
    {
        var w = Setup();
        w.Countries[3].IsPlayer = true;
        TestWorld.Days(w, 1);
        Assert.False(w.HasPact(1, 3));
    }

    [Fact]
    public void StrongerNeighbour_WithoutCommonEnemy_NotWorthProposing()
    {
        var w = Setup();
        for (int i = 0; i < 4; i++) TestWorld.AddDivision(w, 10 + i, 3, TestWorld.Inf, 3);
        TestWorld.Days(w, 1);
        Assert.False(w.HasPact(1, 3));
    }
}
