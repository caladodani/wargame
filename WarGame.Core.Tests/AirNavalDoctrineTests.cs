using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Escolas do ar e do mar (army_doctrine_branch.domain). Até aqui a aviação e a marinha eram um
/// número que se comprava: dois países com o mesmo número de asas tinham exactamente a mesma força aérea, e
/// a única coisa que distinguia exércitos — as escolas de guerra — não existia para as outras duas armas.
///
/// Agora cada arma tem a sua árvore, a sua experiência e a sua escolha. A experiência do ar ganha-se a voar
/// (e depressa onde se abatem aviões), a do mar a navegar e a afundar; e o que cada escola dá entra mesmo nos
/// sistemas: o caça perde menos aviões, o bombardeiro arranca mais infraestrutura, o corso aperta o bloqueio
/// com os mesmos navios. Escolher a escola do ar não fecha nenhuma escola de terra — isso é o ponto todo.</summary>
public class AirNavalDoctrineTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return w;
    }

    /// <summary>Dois países em guerra com asas no mesmo céu (região 3, a da fronteira).</summary>
    private static World AirWar(float wingsA = 10f, float wingsB = 10f)
    {
        var w = Build();
        w.Register(new AirMissionSystem());
        var (a, b) = (w.Countries[1], w.Countries[2]);
        a.AtWarWith.Add(2); b.AtWarWith.Add(1);
        a.Money = b.Money = 100000f;
        a.AirPower = wingsA; b.AirPower = wingsB;
        w.AirMissions.Add(new AirMission { CountryId = 1, RegionId = 3, MissionId = "superioridade", Wings = wingsA });
        w.AirMissions.Add(new AirMission { CountryId = 2, RegionId = 3, MissionId = "superioridade", Wings = wingsB });
        return w;
    }

    private static World SeaWar(float shipsA = 10f, float shipsB = 10f)
    {
        var w = Build();
        w.Register(new NavalMissionSystem());
        var (a, b) = (w.Countries[1], w.Countries[2]);
        a.AtWarWith.Add(2); b.AtWarWith.Add(1);
        a.Money = b.Money = 100000f;
        a.Warships = shipsA; b.Warships = shipsB;
        w.NavalMissions.Add(new NavalMission { CountryId = 1, RegionId = 4, MissionId = "bloqueio", Ships = shipsA });
        w.NavalMissions.Add(new NavalMission { CountryId = 2, RegionId = 4, MissionId = "bloqueio", Ships = shipsB });
        return w;
    }

    private static void Give(World w, Country c, params string[] doctrines)
    {
        foreach (var d in doctrines) c.Doctrines.Add(d);
        w.ApplyTechs(c);
    }

    [Fact]
    public void TheThreeArmsComeFromTheDatabaseWithTheirOwnTrees()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(3, w.DoctrineBranches.Values.Count(b => b.Domain == World.Land));
        Assert.Equal(2, w.DoctrineBranches.Values.Count(b => b.Domain == World.Air));
        Assert.Equal(2, w.DoctrineBranches.Values.Count(b => b.Domain == World.Sea));
        Assert.Equal(World.Air, w.DomainOf(w.ArmyDoctrines["ceu_1"]));
        Assert.Equal(World.Sea, w.DomainOf(w.ArmyDoctrines["cor_3"]));
        Assert.Equal(World.Land, w.DomainOf(w.ArmyDoctrines["mov_1"]));

        foreach (var id in new[] { "ceu", "bomba", "frota", "corso" })
        {
            var steps = w.ArmyDoctrines.Values.Where(d => d.Branch == id).OrderBy(d => d.Sort).ToList();
            Assert.Equal(3, steps.Count);
            string? prev = null;
            foreach (var s in steps)
            {
                Assert.Equal(prev, s.Requires);                        // corrente: cada degrau pede o anterior
                Assert.NotEmpty(w.DoctrineEffects[s.Id]);
                prev = s.Id;
            }
            Assert.True(steps[0].Cost < steps[2].Cost);
        }
    }

    [Fact]
    public void EachArmHasItsOwnPocketOfExperience()
    {
        var w = Build();
        var c = w.Countries[1];
        c.ArmyXp = 100f; c.AirXp = 50f; c.NavyXp = 25f;
        Assert.Equal(100f, World.Xp(c, World.Land), 3);
        Assert.Equal(50f, World.Xp(c, World.Air), 3);
        Assert.Equal(25f, World.Xp(c, World.Sea), 3);

        World.SpendXp(c, World.Air, 40f);
        Assert.Equal(10f, c.AirXp, 3);
        Assert.Equal(100f, c.ArmyXp, 3);                               // pagar o ar não toca no exército
        Assert.Equal(25f, c.NavyXp, 3);
    }

    [Fact]
    public void ASchoolOfTheAirIsPaidWithAirExperienceAndNothingElse()
    {
        var w = Build();
        var c = w.Countries[1];
        c.ArmyXp = 600f;                                               // rico em terra, pobre no ar
        Assert.Equal("faltam 40 de experiência aérea", new AdoptDoctrineCommand(1, "ceu_1").Validate(w));

        c.AirXp = 40f;
        Assert.Null(new AdoptDoctrineCommand(1, "ceu_1").Validate(w));
        new AdoptDoctrineCommand(1, "ceu_1").Execute(w);
        Assert.Contains("ceu_1", c.Doctrines);
        Assert.Equal(0f, c.AirXp, 3);
        Assert.Equal(600f, c.ArmyXp, 3);
    }

    [Fact]
    public void ChoosingASchoolOfOneArmDoesNotCloseTheOthers()
    {
        var w = Build();
        var c = w.Countries[1];
        c.ArmyXp = 600f; c.AirXp = 400f; c.NavyXp = 400f;
        foreach (var id in new[] { "mov_1", "ceu_1", "cor_1" })
        {
            Assert.Null(new AdoptDoctrineCommand(1, id).Validate(w));
            new AdoptDoctrineCommand(1, id).Execute(w);
        }
        Assert.Equal("movimento", w.DoctrineBranchOf(c));
        Assert.Equal("ceu", w.DoctrineBranchOf(c, World.Air));
        Assert.Equal("corso", w.DoctrineBranchOf(c, World.Sea));

        // dentro da mesma arma continua a valer a escolha de sempre
        Assert.StartsWith("Escola fechada por", new AdoptDoctrineCommand(1, "bom_1").Validate(w));
        Assert.StartsWith("Escola fechada por", new AdoptDoctrineCommand(1, "fro_1").Validate(w));
        Assert.StartsWith("Escola fechada por", new AdoptDoctrineCommand(1, "fog_1").Validate(w));
    }

    [Fact]
    public void FlyingTeachesAndBeingShotDownTeachesMuchMore()
    {
        var w = AirWar();
        var quiet = Build();
        quiet.Register(new AirMissionSystem());
        var lone = quiet.Countries[1];
        lone.Money = 100000f; lone.AirPower = 10f;
        quiet.AirMissions.Add(new AirMission { CountryId = 1, RegionId = 3, MissionId = "superioridade", Wings = 10f });

        quiet.Tick();
        float peace = lone.AirXp;
        Assert.Equal(10f * quiet.Rule("air_xp_per_wing_day"), peace, 3);   // céu vazio: só as horas de voo

        w.Tick();
        Assert.True(w.Countries[1].AirXp > peace * 2f,
                    $"o combate tinha de ensinar mais do que a patrulha ({w.Countries[1].AirXp} vs {peace})");
        Assert.Equal(0f, w.Countries[1].NavyXp, 3);                        // e não ensina nada à marinha
    }

    [Fact]
    public void TheFighterSchoolLosesFewerPlanesForTheSameDogfight()
    {
        var plain = AirWar();
        plain.Tick();
        float lostPlain = 10f - plain.Countries[1].AirPower;

        var trained = AirWar();
        Give(trained, trained.Countries[1], "ceu_1", "ceu_2", "ceu_3");
        trained.Tick();
        float lostTrained = 10f - trained.Countries[1].AirPower;

        Assert.True(lostPlain > 0f, "tinha de haver combate");
        Assert.True(lostTrained < lostPlain, $"a escola de caça não valeu nada ({lostTrained} vs {lostPlain})");
        // e o inimigo, esse, perde o mesmo: a escola é nossa
        Assert.Equal(10f - plain.Countries[2].AirPower, 10f - trained.Countries[2].AirPower, 3);
    }

    [Fact]
    public void TheBomberSchoolTearsDownMoreOfTheRoads()
    {
        static float Bomb(string[] doctrines)
        {
            var w = Build();
            w.Register(new AirMissionSystem());
            var (a, b) = (w.Countries[1], w.Countries[2]);
            a.AtWarWith.Add(2); b.AtWarWith.Add(1);
            a.Money = 100000f; a.AirPower = 6f;
            w.Regions[4].Infrastructure = 1f;
            w.AirMissions.Add(new AirMission { CountryId = 1, RegionId = 4, MissionId = "bombardeamento", Wings = 6f });
            foreach (var d in doctrines) a.Doctrines.Add(d);
            w.ApplyTechs(a);
            w.Tick();
            return 1f - w.Regions[4].Infrastructure;
        }

        float plain = Bomb(System.Array.Empty<string>());
        float trained = Bomb(new[] { "bom_1", "bom_2", "bom_3" });
        Assert.True(plain > 0f, "o bombardeamento tinha de arrancar alguma coisa");
        Assert.True(trained > plain * 1.3f, $"a escola de bombardeamento não valeu nada ({trained} vs {plain})");
    }

    [Fact]
    public void TheRaidingSchoolTightensTheBlockadeWithTheSameShips()
    {
        // dez navios de bloqueio contra doze de escolta: sem escola o mar fica aberto, com a escola do corso
        // os mesmos dez navios chegam para o fechar
        static bool Closed(string[] doctrines)
        {
            var w = Build();
            w.Register(new NavalMissionSystem());
            var (a, b) = (w.Countries[1], w.Countries[2]);
            a.AtWarWith.Add(2); b.AtWarWith.Add(1);
            a.Money = b.Money = 100000f;
            a.Warships = 10f; b.Warships = 12f;
            w.NavalMissions.Add(new NavalMission { CountryId = 1, RegionId = 4, MissionId = "bloqueio", Ships = 10f });
            w.NavalMissions.Add(new NavalMission { CountryId = 2, RegionId = 4, MissionId = "escolta", Ships = 12f });
            foreach (var d in doctrines) a.Doctrines.Add(d);
            w.ApplyTechs(a);
            return NavalMissionSystem.Blockaded(w, 4);
        }

        Assert.False(Closed(System.Array.Empty<string>()));
        Assert.True(Closed(new[] { "cor_1", "cor_2", "cor_3" }), "a guerra ao comércio tinha de apertar o bloqueio");
    }

    [Fact]
    public void TheSeaTeachesTheNavyAndTheFleetSchoolKeepsItsSteelAfloat()
    {
        var plain = SeaWar();
        plain.Tick();
        float lostPlain = 10f - plain.Countries[1].Warships;
        Assert.True(plain.Countries[1].NavyXp > 0f, "andar no mar tinha de ensinar alguma coisa");
        Assert.Equal(0f, plain.Countries[1].AirXp, 3);

        var trained = SeaWar();
        Give(trained, trained.Countries[1], "fro_1", "fro_2", "fro_3");
        trained.Tick();
        Assert.True(10f - trained.Countries[1].Warships < lostPlain, "a escola de esquadra não valeu nada");
    }

    [Fact]
    public void TheAiTrainsAllThreeArmsFromTheirOwnPockets()
    {
        var w = AirWar();
        w.Register(new ArmyXpSystem());
        var c = w.Countries[1];
        c.AirXp = 400f; c.NavyXp = 400f; c.ArmyXp = 0f;

        TestWorld.Days(w, 6);                                          // a IA adopta um degrau por dia
        Assert.Contains(c.Doctrines, id => w.DomainOf(w.ArmyDoctrines[id]) == World.Air);
        Assert.Contains(c.Doctrines, id => w.DomainOf(w.ArmyDoctrines[id]) == World.Sea);
        Assert.DoesNotContain(c.Doctrines, id => w.DomainOf(w.ArmyDoctrines[id]) == World.Land);  // sem xp de terra
    }

    [Fact]
    public void TheTwoNewPocketsSurviveSaveAndLoadEvenFromAnOlderSave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.IsPlayer = true; c.AirXp = 123f; c.NavyXp = 45f; c.ArmyXp = 7f;
        c.Doctrines.Add("ceu_1"); c.Doctrines.Add("cor_1");

        string schema = SqlWorldRepository.SchemaFromSqliteMaster(staticDb);
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        foreach (var col in new[] { "air_xp", "navy_xp" })              // um save gravado antes desta versão
            save.Execute($"ALTER TABLE s_country DROP COLUMN {col}");
        SqlWorldRepository.EnsureSaveSchema(save, schema);              // a versão nova pega nele sem estoirar

        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);
        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = w2.Countries[1];
        Assert.Equal(123f, back.AirXp, 3);
        Assert.Equal(45f, back.NavyXp, 3);
        Assert.Equal(7f, back.ArmyXp, 3);
        Assert.Equal("ceu", w2.DoctrineBranchOf(back, World.Air));
        Assert.Equal("corso", w2.DoctrineBranchOf(back, World.Sea));
        Assert.Null(w2.DoctrineBranchOf(back));
    }
}
