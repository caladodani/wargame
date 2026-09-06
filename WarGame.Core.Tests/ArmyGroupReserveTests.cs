using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Postura de reserva: o grupo sai da linha, recolhe à retaguarda em terreno nosso e
/// recompõe-se mais depressa. Mapa 1-2-3 (jogador) | 4-5-6 (inimigo), por isso a região 3 é a linha e a
/// 1 é a retaguarda funda.</summary>
public class ArmyGroupReserveTests
{
    private const int Player = 1, Foe = 2;

    private static (World w, ArmyGroup g) Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[Player].AtWarWith.Add(Foe);
        w.Countries[Foe].AtWarWith.Add(Player);
        new CreateArmyGroupCommand(Player, "1.º Exército").Execute(w);
        var g = w.ArmyGroups.Values.Single();
        new SetArmyGroupFrontCommand(Player, g.Id, Foe).Execute(w);
        return (w, g);
    }

    private static Division Member(World w, ArmyGroup g, int id, int region, float org = 100f)
    {
        var d = TestWorld.AddDivision(w, id, Player, TestWorld.Inf, region, org: org);
        new AssignDivisionCommand(Player, d.Id, g.Id).Execute(w);
        return d;
    }

    [Fact]
    public void InReserve_TheDivisionPullsBackFromTheLine()
    {
        var (w, g) = Setup();
        var d = Member(w, g, 1, 3);                       // encostada à frente
        new SetArmyGroupStanceCommand(Player, g.Id, GroupStance.Reserve).Execute(w);

        w.Register(new ArmyGroupSystem());
        w.Register(new MovementSystem());
        TestWorld.Days(w, 40);

        Assert.True(d.RegionId <= 1, $"ficou na região {d.RegionId}");   // recuou até à profundidade pedida
        Assert.Equal(Player, w.Regions[d.RegionId].ControllerId);
    }

    [Fact]
    public void TheDepthComesFromTheRules()
    {
        var (w, g) = Setup();
        w.Rules["army_group_reserve_depth"] = 1f;         // basta sair da linha da frente
        var d = Member(w, g, 1, 3);
        new SetArmyGroupStanceCommand(Player, g.Id, GroupStance.Reserve).Execute(w);

        w.Register(new ArmyGroupSystem());
        w.Register(new MovementSystem());
        TestWorld.Days(w, 40);

        Assert.Equal(3, d.RegionId);                      // a região 3 já está a um salto do inimigo
    }

    [Fact]
    public void AReserveNeverWalksIntoEnemyGround()
    {
        var (w, g) = Setup();
        var d = Member(w, g, 1, 3);
        new SetArmyGroupStanceCommand(Player, g.Id, GroupStance.Reserve).Execute(w);

        w.Register(new ArmyGroupSystem());
        w.Register(new MovementSystem());
        TestWorld.Days(w, 40);

        Assert.All(w.Divisions.Values, x => Assert.Equal(Player, w.Regions[x.RegionId].ControllerId));
    }

    [Fact]
    public void RestingRecoversFaster_ThanStandingInTheLine()
    {
        var (w, g) = Setup();
        var resting = Member(w, g, 1, 1, org: 20f);
        var line = TestWorld.AddDivision(w, 2, Player, TestWorld.Inf, 2, org: 20f);
        new SetArmyGroupStanceCommand(Player, g.Id, GroupStance.Reserve).Execute(w);

        w.Register(new RecoverySystem());
        w.Tick();

        Assert.True(resting.Org > line.Org, $"reserva {resting.Org}, linha {line.Org}");
    }

    [Fact]
    public void OutOfReserve_TheBonusGoesAway()
    {
        var (w, g) = Setup();
        var d = Member(w, g, 1, 1, org: 20f);
        var alone = TestWorld.AddDivision(w, 2, Player, TestWorld.Inf, 2, org: 20f);
        new SetArmyGroupStanceCommand(Player, g.Id, GroupStance.Defend).Execute(w);

        w.Register(new RecoverySystem());
        w.Tick();

        Assert.Equal(alone.Org, d.Org, 3);
    }

    [Fact]
    public void AWornOutAiGroup_GoesToTheRear_AndOnlyComesBackRecomposed()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[Player].IsPlayer = true;
        w.Countries[Player].AtWarWith.Add(Foe);
        w.Countries[Foe].AtWarWith.Add(Player);
        w.Register(new AiSystem());
        for (int i = 0; i < 8; i++) TestWorld.AddDivision(w, 100 + i, Foe, TestWorld.Inf2, 4 + i % 3, org: 20f);
        int period = (int)w.Rule("ai_period_days", 3f);

        TestWorld.Days(w, period);
        var g = Assert.Single(w.ArmyGroups.Values);
        Assert.Equal(GroupStance.Reserve, g.Stance);      // exército gasto: recolhe-se

        foreach (int id in g.Divisions) w.Divisions[id].Org = 60f;   // meio recomposto ainda não chega
        TestWorld.Days(w, period);
        Assert.Equal(GroupStance.Reserve, g.Stance);

        foreach (int id in g.Divisions) w.Divisions[id].Org = 95f;
        TestWorld.Days(w, period);
        Assert.NotEqual(GroupStance.Reserve, g.Stance);
    }

    [Fact]
    public void TheStanceSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[Player].AtWarWith.Add(Foe);
        w.Countries[Foe].AtWarWith.Add(Player);
        new CreateArmyGroupCommand(Player, "Reserva Geral").Execute(w);
        var g = w.ArmyGroups.Values.Single();
        new SetArmyGroupFrontCommand(Player, g.Id, Foe).Execute(w);
        new SetArmyGroupStanceCommand(Player, g.Id, GroupStance.Reserve).Execute(w);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(GroupStance.Reserve, w2.ArmyGroups.Values.Single().Stance);
        Assert.True(w2.ArmyGroups.Values.Single().Resting);
    }
}
