using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Guerra aérea por região: as asas destacadas saem do pool, custam estadia, abatem-se umas às
/// outras no céu disputado e pesam na batalha que se dá por baixo delas.</summary>
public class AirMissionTests
{
    private static World Build(float wings = 10f, float money = 500f, float lonStep = 2f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, lonStep: lonStep);
        w.StartWar(1, 2);
        // os dois países de mão humana: a IA aérea tem um teste só para ela e não anda a engrossar estes
        foreach (var c in w.Countries.Values) { c.AirPower = wings; c.Money = money; c.IsPlayer = true; }
        w.Register(new AirMissionSystem());
        return w;
    }

    [Fact]
    public void TheMissionsAndTheirPricesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal("superiority", w.AirMissionDefs["superioridade"].Effect);
        Assert.Equal("support", w.AirMissionDefs["apoio"].Effect);
        Assert.Equal("bombing", w.AirMissionDefs["bombardeamento"].Effect);
        Assert.Equal(0.6f, w.Rule("air_mission_upkeep"), 3);
        Assert.Equal(0.04f, w.Rule("air_dogfight_loss"), 3);
        Assert.Equal(0.25f, w.Rule("air_bomb_infra_min"), 3);
        Assert.Equal(0.35f, w.Rule("air_support_max"), 3);
        Assert.Equal(1f, w.Rule("air_mission_min_wings"), 3);
    }

    [Fact]
    public void WingsOnAMissionLeaveThePool()
    {
        var w = Build();
        Assert.Equal(10f, AirMissionSystem.Free(w, 1), 3);

        new AssignAirMissionCommand(1, 4, "superioridade", 4f).Execute(w);
        Assert.Equal(4f, AirMissionSystem.Assigned(w, 1), 3);
        Assert.Equal(6f, AirMissionSystem.Free(w, 1), 3);

        new AssignAirMissionCommand(1, 4, "superioridade", 2f).Execute(w);   // o mesmo céu engrossa
        Assert.Single(w.AirMissions);
        Assert.Equal(6f, w.AirMissions[0].Wings, 3);

        Assert.True(new RecallAirMissionCommand(1, 4).Validate(w) is null);
        new RecallAirMissionCommand(1, 4).Execute(w);
        Assert.Empty(w.AirMissions);
        Assert.Equal(10f, AirMissionSystem.Free(w, 1), 3);                   // voltam todas ao pool
    }

    [Fact]
    public void ChangingTheTaskKeepsThePlanesInTheSameSky()
    {
        var w = Build();
        new AssignAirMissionCommand(1, 4, "superioridade", 3f).Execute(w);
        new AssignAirMissionCommand(1, 4, "bombardeamento", 2f).Execute(w);

        var m = Assert.Single(w.AirMissions);
        Assert.Equal("bombardeamento", m.MissionId);
        Assert.Equal(5f, m.Wings, 3);
    }

    [Fact]
    public void TheSkyHasToBeReachableAndTheEnemyHasToBeTheEnemy()
    {
        // linha esticada a oito graus por província (~890 km): a região 4 fica a um salto dos nossos campos e
        // a 6 a três, longe do que uma asa sem modelo aguenta (air_range_default). É a distância a decidir,
        // e não a fronteira: antes bastava fazer beira a terra nossa para a aviação aparecer em qualquer céu
        var w = Build(lonStep: 8f);
        Assert.Null(new AssignAirMissionCommand(1, 4, "superioridade", 2f).Validate(w));   // vizinha da nossa 3
        Assert.Contains("fora do alcance", new AssignAirMissionCommand(1, 6, "superioridade", 2f).Validate(w)!);
        Assert.Contains("poucas asas", new AssignAirMissionCommand(1, 4, "superioridade", 0.5f).Validate(w)!);
        Assert.Contains("asas em casa", new AssignAirMissionCommand(1, 4, "superioridade", 40f).Validate(w)!);
        Assert.Contains("própria casa", new AssignAirMissionCommand(1, 2, "bombardeamento", 2f).Validate(w)!);
        Assert.Null(new AssignAirMissionCommand(1, 2, "superioridade", 2f).Validate(w));   // o nosso céu defende-se

        w.EndWar(1, 2);
        Assert.Contains("não estamos em guerra", new AssignAirMissionCommand(1, 4, "superioridade", 2f).Validate(w)!);
    }

    [Fact]
    public void EveryDayInTheAirIsPaidFor()
    {
        var w = Build(money: 10f);
        new AssignAirMissionCommand(1, 4, "superioridade", 5f).Execute(w);
        float upkeep = w.Rule("air_mission_upkeep");

        w.Tick();
        Assert.Equal(10f - 5f * upkeep, w.Countries[1].Money, 3);

        w.Countries[1].Money = 0.5f;
        w.Tick();
        Assert.Empty(w.AirMissions);                                          // sem cofre, os aviões vêm para casa
        Assert.Equal(0.5f, w.Countries[1].Money, 3);
    }

    [Fact]
    public void ContestedSkiesCostPlanesToBothSides()
    {
        var w = Build();
        new AssignAirMissionCommand(1, 4, "superioridade", 6f).Execute(w);
        new AssignAirMissionCommand(2, 4, "superioridade", 2f).Execute(w);
        float loss = w.Rule("air_dogfight_loss") * 2f;                        // pelo lado mais fraco

        w.Tick();
        Assert.Equal(6f - loss, w.AirMissions.Single(m => m.CountryId == 1).Wings, 3);
        Assert.Equal(2f - loss, w.AirMissions.Single(m => m.CountryId == 2).Wings, 3);
        Assert.Equal(10f - loss, w.Countries[1].AirPower, 3);                 // os abatidos saem do pool nacional
        Assert.Equal(10f - loss, w.Countries[2].AirPower, 3);
    }

    [Fact]
    public void BombingGrindsTheEnemyInfrastructureDownToTheFloor()
    {
        var w = Build();
        var r = w.Regions[4];
        r.Infrastructure = 1f;
        // aviação toda do mesmo modelo: agora que uma asa vale o que o avião dela vale a bombardear, o que
        // cai lê-se da tabela e não de uma contagem de asas — e sem passar pela repartição de partida
        string bomber = w.PlaneClasses.Values.OrderByDescending(d => d.Bombing).ThenBy(d => d.Sort).First().Id;
        var c = w.Countries[1];
        c.Planes.Clear(); c.Planes[bomber] = 10f;
        new AssignAirMissionCommand(1, 4, "bombardeamento", 4f).Execute(w);
        float per = 4f * Air.Value(w, bomber, "bombing") * w.AirMissionDefs["bombardeamento"].Value;

        w.Tick();
        Assert.Equal(1f - per, r.Infrastructure, 3);

        TestWorld.Days(w, 60);
        Assert.Equal(w.Rule("air_bomb_infra_min"), r.Infrastructure, 3);      // e não abaixo disso
    }

    [Fact]
    public void PlanesOverTheBattleAreWorthMoreThanPlanesAtHome()
    {
        // dois mundos iguais: num deles o atacante destacou as asas para o céu da batalha
        static (World w, Division def) Field(bool overhead)
        {
            var (w, _) = TestWorld.Build();
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            w.Countries[1].AirPower = 8f; w.Countries[1].Money = 500f;
            w.Countries[2].AirPower = 8f;                                // céu equilibrado sem missões nenhumas
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
            if (overhead)
            {
                AirMissionSystem.Assign(w, 1, 4, "superioridade", 8f);
                AirMissionSystem.Assign(w, 1, 4, "apoio", 0f);                // não muda nada: zero asas
            }
            var battle = new Battle { RegionId = 4, AttackerCountryId = 1 };
            battle.Attackers.Add(att.Id); battle.Defenders.Add(def.Id);
            w.ActiveBattles.Add(battle);
            w.Register(new CombatSystem());
            return (w, def);
        }

        var (home, safe) = Field(false);
        var (sky, hit) = Field(true);
        for (int i = 0; i < 5; i++) { home.Tick(); sky.Tick(); }

        Assert.True(hit.Hp < safe.Hp, $"com o céu limpo o defensor devia estar pior: {hit.Hp:0.0} contra {safe.Hp:0.0}");
    }

    [Fact]
    public void CloseAirSupportIsCappedByTheRule()
    {
        var w = Build(wings: 100f);
        AirMissionSystem.Assign(w, 1, 4, "apoio", 4f);
        Assert.Equal(4f * w.AirMissionDefs["apoio"].Value, AirMissionSystem.Support(w, 4, 1), 3);

        AirMissionSystem.Assign(w, 1, 4, "apoio", 90f);
        Assert.Equal(w.Rule("air_support_max"), AirMissionSystem.Support(w, 4, 1), 3);
        Assert.Equal(0f, AirMissionSystem.Support(w, 4, 2), 3);
    }

    [Fact]
    public void TheAiSendsWhatItHasToTheFrontAndKeepsAReserve()
    {
        var w = Build(wings: 5f);
        w.Countries[2].IsPlayer = false;                                      // só a IA do país 2 despacha
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);

        w.Tick();
        var m = Assert.Single(w.AirMissions);
        Assert.Equal(2, m.CountryId);
        Assert.Equal(3, m.RegionId);                                          // a nossa região da frente
        Assert.Equal("superioridade", m.MissionId);
        Assert.Equal(5f - w.Rule("air_ai_reserve"), m.Wings, 3);
    }

    [Fact]
    public void TheSquadronsSurviveSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        w.Countries[1].AirPower = 6f; w.Countries[1].Money = 100f;
        AirMissionSystem.Assign(w, 1, 4, "bombardeamento", 3f);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = Assert.Single(w2.AirMissions);
        Assert.Equal(1, back.CountryId);
        Assert.Equal(4, back.RegionId);
        Assert.Equal("bombardeamento", back.MissionId);
        Assert.Equal(3f, back.Wings, 3);
    }
}
