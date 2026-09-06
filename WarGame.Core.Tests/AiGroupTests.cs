using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A IA a mandar em grupos de exércitos. Mapa 1-2-3 (jogador) | 4-5-6 (IA). O que se mede é a
/// decisão — grupo levantado, frente apontada, divisões metidas lá dentro e a postura escolhida pela
/// comparação de forças — e não para onde as divisões acabam por andar.</summary>
public class AiGroupTests
{
    private const int Player = 1, Ai = 2;

    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[Player].IsPlayer = true;
        w.Countries[Player].AtWarWith.Add(Ai);
        w.Countries[Ai].AtWarWith.Add(Player);
        w.Register(new AiSystem());
        return w;
    }

    private static void AiDivisions(World w, int n)
    {
        for (int i = 0; i < n; i++) TestWorld.AddDivision(w, 100 + i, Ai, TestWorld.Inf2, 4 + i % 3);
    }

    private static void PlayerDivisions(World w, int n)
    {
        for (int i = 0; i < n; i++) TestWorld.AddDivision(w, 200 + i, Player, TestWorld.Inf, 1 + i % 3);
    }

    private static int Period(World w) => (int)w.Rule("ai_period_days", 3);

    [Fact]
    public void WithEnoughDivisions_TheAiRaisesAnArmyGroupPointedAtUs()
    {
        var w = Setup();
        AiDivisions(w, 8);
        TestWorld.Days(w, Period(w));

        var g = Assert.Single(w.ArmyGroups.Values);
        Assert.Equal(Ai, g.CountryId);
        Assert.Equal(Player, g.FrontCountryId);
        Assert.NotEmpty(g.Divisions);
        Assert.All(g.Divisions, id => Assert.Equal(Ai, w.Divisions[id].CountryId));
    }

    [Fact]
    public void ASmallArmy_StaysLoose()
    {
        var w = Setup();
        w.Rules["ai_group_min_divisions"] = 6f;
        AiDivisions(w, 3);
        TestWorld.Days(w, Period(w) * 3);

        Assert.Empty(w.ArmyGroups);
    }

    [Fact]
    public void TheGroupNeverSwallowsTheWholeArmy()
    {
        var w = Setup();
        w.Rules["ai_group_share"] = 0.5f;
        AiDivisions(w, 10);
        TestWorld.Days(w, Period(w) * 4);

        var g = Assert.Single(w.ArmyGroups.Values);
        Assert.Equal(5, g.Divisions.Count);   // metade fica de guarnição, às ordens do Fight
    }

    [Fact]
    public void OutnumberedTheAiDigsIn_AndAheadItAdvances()
    {
        var w = Setup();
        w.Rules["ai_group_advance_ratio"] = 1.2f;
        AiDivisions(w, 8);
        PlayerDivisions(w, 20);
        TestWorld.Days(w, Period(w));

        var g = Assert.Single(w.ArmyGroups.Values);
        Assert.Equal(GroupStance.Defend, g.Stance);

        for (int i = 0; i < 20; i++) TestWorld.AddDivision(w, 300 + i, Ai, TestWorld.Inf2, 6);
        TestWorld.Days(w, Period(w));
        Assert.Equal(GroupStance.Advance, g.Stance);
    }

    [Fact]
    public void TheAiNeverTouchesThePlayersDivisions()
    {
        var w = Setup();
        AiDivisions(w, 8);
        var mine = TestWorld.AddDivision(w, 500, Player, TestWorld.Inf, 1);
        TestWorld.Days(w, Period(w) * 3);

        Assert.DoesNotContain(w.ArmyGroups.Values, g => g.CountryId == Player);
        Assert.Null(w.GroupOf(mine.Id));
    }

    [Fact]
    public void TheAiPutsACommanderInFrontOfTheGroup_ChosenByTheStance()
    {
        var w = Setup();
        var atk = w.GeneralDefs.Values.First(g => g.StatKey == "attack");
        var def = w.GeneralDefs.Values.First(g => g.StatKey == "defense");
        w.Countries[Ai].Generals.Add(atk.Id);
        w.Countries[Ai].Generals.Add(def.Id);
        World.ApplyGenerals(w, w.Countries[Ai]);

        AiDivisions(w, 8);
        PlayerDivisions(w, 20);          // em inferioridade: cava-se, e quer o comandante que defende
        TestWorld.Days(w, Period(w));
        var g = Assert.Single(w.ArmyGroups.Values);
        Assert.Equal(GroupStance.Defend, g.Stance);
        Assert.Equal(def.Id, g.GeneralId);

        for (int i = 0; i < 20; i++) TestWorld.AddDivision(w, 300 + i, Ai, TestWorld.Inf2, 6);
        TestWorld.Days(w, Period(w));    // com vantagem avança, e troca para o comandante que ataca
        Assert.Equal(GroupStance.Advance, g.Stance);
        Assert.Equal(atk.Id, g.GeneralId);
        Assert.Equal(1f, w.Countries[Ai].Stat("attack"), 3);   // destacado: já não vale para o país todo
    }

    [Fact]
    public void PeaceLeavesTheGroupStanding_ButItGivesNoMoreOrders()
    {
        var w = Setup();
        AiDivisions(w, 8);
        TestWorld.Days(w, Period(w));
        var g = Assert.Single(w.ArmyGroups.Values);

        w.EndWar(Player, Ai);
        foreach (int id in g.Divisions) w.Divisions[id].ClearPath();   // esquecer as ordens da guerra

        Assert.Equal(Player, g.FrontCountryId);   // a frente fica na história do grupo
        w.Register(new ArmyGroupSystem());
        TestWorld.Days(w, Period(w) * 2);         // e o sistema recusa marchar sem guerra
        Assert.All(g.Divisions, id => Assert.Empty(w.Divisions[id].Path));
    }
}
