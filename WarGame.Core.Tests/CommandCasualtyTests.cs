using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Baixas no comando: o que acontece ao exército quando o comandante cai. A probabilidade vem
/// das regras — nos testes põe-se a 1 (ou a 0) para o sorteio deixar de mandar e ficar só a mecânica.</summary>
public class CommandCasualtyTests
{
    private static (World w, Country c, ArmyGroup g) Setup(params string[] staff)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        foreach (var id in staff.Length == 0 ? new[] { "gen_ofensiva" } : staff) c.Generals.Add(id);
        var g = new ArmyGroup { Id = 1, CountryId = 1, Name = "1.º Exército", GeneralId = c.Generals[0] };
        w.ArmyGroups[1] = g;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        w.JoinGroup(g, 1);
        World.ApplyGenerals(w, c);
        return (w, c, g);
    }

    private static void Battle(World w, bool playerWon = false) =>
        w.Events.Publish(new BattleEnded(3, playerWon, 2, 1));   // país 2 ataca a região 3, o 1 defende



    [Fact]
    public void ABattleCanTakeTheCommanderOut()
    {
        var (w, c, _) = Setup();
        w.Rules["wound_chance"] = 1f;
        var seen = new List<IGameEvent>();
        w.Events.Subscribe<GeneralWounded>(seen.Add);
        w.Events.Subscribe<GeneralKilled>(seen.Add);
        w.Register(new CommandCasualtySystem());
        w.Tick();
        Battle(w);

        Assert.Single(seen);
        Assert.True(c.GeneralWound.Count == 1 || !c.Generals.Contains("gen_ofensiva"));
    }










}
