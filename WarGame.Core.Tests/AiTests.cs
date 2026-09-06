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

    [Fact]
    public void DefendedRegion_NoAttackWithoutSuperiority()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 3);
        TestWorld.AddDivision(w, 2, Player, TestWorld.Inf, 3);
        TestWorld.AddDivision(w, 10, Ai, TestWorld.Inf2, 4);
        TestWorld.AddDivision(w, 11, Ai, TestWorld.Inf2, 4);              // 2 < 2 × ai_attack_ratio
        TestWorld.Days(w, Period(w));
        Assert.All(w.Divisions.Values, d => Assert.Empty(d.Path));
    }

    [Fact]
    public void DefendedRegion_AttacksWithWholeGroupWhenSuperior()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 3);
        TestWorld.AddDivision(w, 2, Player, TestWorld.Inf, 3);
        int needed = (int)MathF.Ceiling(2 * w.Rule("ai_attack_ratio", 1.5f)) + 1;   // 4 com o rácio da tabela
        for (int i = 0; i < needed; i++) TestWorld.AddDivision(w, 10 + i, Ai, TestWorld.Inf2, 4);
        TestWorld.Days(w, Period(w));
        Assert.All(w.Divisions.Values.Where(d => d.CountryId == Ai), d => Assert.Equal(3, Where(d)));
        Assert.All(w.Divisions.Values.Where(d => d.CountryId == Player), d => Assert.Empty(d.Path));
    }

    [Fact]
    public void RearDivision_MovesToNearestFrontRegion()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 3);
        var rear = TestWorld.AddDivision(w, 10, Ai, TestWorld.Inf2, 6);
        TestWorld.Days(w, Period(w));
        Assert.Equal(4, Where(rear));
    }

    [Fact]
    public void Production_FillsQueue_PlayerQueueStaysEmpty()
    {
        var w = Setup(war: false);
        w.Countries[Ai].Money = 100f; w.Countries[Player].Money = 100f;
        TestWorld.Days(w, Period(w));
        var ai = w.Countries[Ai];
        int maxQueue = (int)w.Rule("ai_max_queue", 3), heavyEvery = (int)w.Rule("ai_heavy_every", 3);
        Assert.Equal(maxQueue, ai.Queue.Count);
        Assert.Empty(w.Countries[Player].Queue);
        var templates = w.Units.GetTemplates(Ai);
        int cheapest = templates.MinBy(t => w.TemplateCost(t.Id))!.Id;
        int strongest = templates.MaxBy(t => w.Stats.Get(t.Id)["breakthrough"])!.Id;
        Assert.Equal(cheapest, ai.Queue[0].TemplateId);
        if (heavyEvery <= maxQueue) Assert.Equal(strongest, ai.Queue[heavyEvery - 1].TemplateId);
    }

    [Fact]
    public void Production_RespectsBudget()
    {
        var w = Setup(war: false);
        float cheapest = w.Units.GetTemplates(Ai).Min(t => w.TemplateCost(t.Id));
        w.Countries[Ai].Money = cheapest * 1.5f;                              // chega para uma, não para duas
        TestWorld.Days(w, Period(w));
        Assert.Single(w.Countries[Ai].Queue);
    }

    [Fact]
    public void LowOrgDivision_IsNotUsed()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 2);
        var tired = TestWorld.AddDivision(w, 10, Ai, TestWorld.Inf2, 4, org: w.Rule("ai_min_org", 50) - 1f);
        TestWorld.Days(w, Period(w));
        Assert.Empty(tired.Path);
    }

    [Fact]
    public void DivisionInBattle_IsNotUsed_LoneDivisionStillAttacksEmptyRegion()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 1);
        var busy = TestWorld.AddDivision(w, 10, Ai, TestWorld.Inf2, 4);
        var free = TestWorld.AddDivision(w, 11, Ai, TestWorld.Inf2, 4);
        var b = new Battle { RegionId = 3, AttackerCountryId = Ai }; b.Attackers.Add(busy.Id); w.ActiveBattles.Add(b);
        TestWorld.Days(w, Period(w));
        Assert.Empty(busy.Path);
        Assert.Equal(3, Where(free));                                         // sozinha, mas sem ameaça ao lado
    }

    [Fact]
    public void NoWar_DoesNothing()
    {
        var w = Setup(war: false);
        TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 2);
        var d = TestWorld.AddDivision(w, 10, Ai, TestWorld.Inf2, 4);
        TestWorld.Days(w, Period(w) * 2);
        Assert.Empty(d.Path);
    }

    [Fact]
    public void EnemyOverseas_DoesNothing()
    {
        var w = Setup(war: false);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 7 };
        w.Regions[7] = new Region { Id = 7, Name = "Ilha", OwnerId = 3, ControllerId = 3 };   // sem vizinhos
        w.Countries[Ai].AtWarWith.Add(3); w.Countries[3].AtWarWith.Add(Ai);
        var d = TestWorld.AddDivision(w, 10, Ai, TestWorld.Inf2, 4);
        TestWorld.Days(w, Period(w));
        Assert.Empty(d.Path);
    }

    [Fact]
    public void RunsOnlyEveryPeriod()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 3);
        int period = Period(w);
        TestWorld.Days(w, 1);                                                 // dia 0: a IA correu sem divisões
        var rear = TestWorld.AddDivision(w, 10, Ai, TestWorld.Inf2, 6);
        TestWorld.Days(w, period - 1);                                        // dias 1..period-1: não corre
        if (period > 1) Assert.Empty(rear.Path);
        TestWorld.Days(w, 1);                                                 // dia `period`: corre
        Assert.Equal(4, Where(rear));
    }
}
