using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>IA no país 2; país 1 é o jogador (nunca mexido). Asserções sobre Path/Queue — não dependem do movimento.</summary>
public class AiTests
{
    private const int Player = 1, Ai = 2;

    private static World Setup(bool war = true)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);                       // 1-2-3 (país 1) | 4-5-6 (país 2); frente = 3 | 4
        w.Countries[Player].IsPlayer = true;
        if (war) { w.Countries[Player].AtWarWith.Add(Ai); w.Countries[Ai].AtWarWith.Add(Player); }
        w.Register(new MovementSystem());
        w.Register(new AiSystem());
        return w;
    }

    private static int Period(World w) => (int)w.Rule("ai_period_days", 3);
    /// <summary>Destino do Path, ou a região onde está se já chegou (robusto a um MovementSystem real).</summary>
    private static int Where(Division d) => d.DestinationRegionId ?? d.RegionId;

    [Fact]
    public void EmptyEnemyRegion_SendsOneDivision_PlayerUntouched()
    {
        var w = Setup();
        var p = TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 2);      // região 3 fica vazia
        TestWorld.AddDivision(w, 10, Ai, TestWorld.Inf2, 4);
        TestWorld.AddDivision(w, 11, Ai, TestWorld.Inf2, 4);
        TestWorld.Days(w, Period(w));
        Assert.Equal(1, w.Divisions.Values.Count(d => d.CountryId == Ai && Where(d) == 3));
        Assert.Empty(p.Path);
        Assert.Equal(2, p.RegionId);
    }










}
