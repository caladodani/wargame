using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Tropas especiais de terreno: a especialidade é da unidade, não do país. Quem treinou para a
/// serra desconta a penalização da serra, ande sob a bandeira que andar; os fuzileiros saem do barco a
/// bater e atravessam rios melhor. Tudo isto são linhas das tabelas unit_tag e modifier — nenhum tipo de
/// tropa especial vive no código.</summary>
public class SpecialForcesTests
{
    private const int Alpino = 91, Fuzileiro = 92;   // templates criados só para estes testes

    /// <summary>As unidades nascem na base de dados como no jogo: a marca de terreno é uma linha de
    /// unit_tag ao lado de 'infantry' e 'ground'.</summary>
    private const string SpecialSql = @"
        INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
            (91,'Caçadores alpinos','ground',1.4,40,1.0,30),
            (92,'Fuzileiros','ground',1.4,40,1.0,30);
        INSERT INTO unit_stat VALUES (91,'soft_atk',8),(91,'hard_atk',2),(91,'defense',22),(91,'breakthrough',8),
                                    (91,'armor',0),(91,'piercing',6),(91,'hardness',0.1),(91,'hp',22),
                                    (92,'soft_atk',8),(92,'hard_atk',2),(92,'defense',22),(92,'breakthrough',8),
                                    (92,'armor',0),(92,'piercing',6),(92,'hardness',0.1),(92,'hp',22);
        INSERT INTO unit_tag VALUES (91,'infantry'),(91,'ground'),(91,'montanha'),
                                    (92,'infantry'),(92,'ground'),(92,'anfibio');
        INSERT INTO template VALUES (91,1,'Caçadores alpinos'),(92,1,'Fuzileiros');
        INSERT INTO template_unit VALUES (91,91,6),(92,92,6);";

    private static (World w, MsSqliteDatabase db) Fresh()
    {
        var (w, db) = TestWorld.Build();
        db.ExecuteScript(SpecialSql);
        return (w, db);
    }

    /// <summary>Mapa em linha de 6 regiões (1..3 do país 1) todo do mesmo terreno, já em guerra.</summary>
    private static (World w, MsSqliteDatabase db) Land(string terrain, bool river = false)
    {
        var (w, db) = Fresh();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 6, Manpower = 1e9f };
        for (int i = 1; i <= 6; i++)
        {
            int owner = i <= 3 ? 1 : 2;
            var r = new Region { Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner,
                                 Terrain = terrain, River = river, Population = 10_000_000, CenterX = i * 100 };
            if (i > 1) r.Neighbours.Add(i - 1);
            if (i < 6) r.Neighbours.Add(i + 1);
            w.Regions[i] = r;
        }
        w.StartWar(1, 2);
        return (w, db);
    }

    /// <summary>Ilhas 1-2 (país 1) e 3-4 (país 2), com travessia 2↔3 — o mesmo mapa dos desembarques.</summary>
    private static World Islands()
    {
        var (w, _) = Fresh();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 4, Manpower = 1e9f };
        for (int i = 1; i <= 4; i++)
        {
            int owner = i <= 2 ? 1 : 2;
            w.Regions[i] = new Region { Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner,
                                        ControllerId = owner, Terrain = "plain", Population = 10_000_000, Coastal = i is 2 or 3 };
        }
        w.Regions[1].Neighbours.Add(2); w.Regions[2].Neighbours.Add(1);
        w.Regions[3].Neighbours.Add(4); w.Regions[4].Neighbours.Add(3);
        w.Regions[2].SeaNeighbours[3] = 800f; w.Regions[3].SeaNeighbours[2] = 800f;
        w.StartWar(1, 2);
        return w;
    }

    /// <summary>Quanto vale o multiplicador de terreno deste template a atacar (ou a defender) aqui.</summary>
    private static float Terrain(World w, int template, string key = "str_attacker", int region = 4)
    {
        var ctx = CombatSystem.BuildContext(w, w.Regions[region], 1);
        var (flat, mul) = w.Modifiers.Evaluate(key, w.Stats.Get(template), ctx);
        return mul + flat;
    }

    [Fact]
    public void TheMarksAndTheirWorthComeFromTheDatabase()
    {
        var (w, _) = Land("mountain");
        Assert.Contains("montanha", w.Stats.Get(Alpino).Tags);
        Assert.Contains("anfibio", w.Stats.Get(Fuzileiro).Tags);
        Assert.DoesNotContain("montanha", w.Stats.Get(TestWorld.Inf).Tags);
        Assert.Equal(0.8f, w.Rule("naval_invasion_marine"), 3);
    }

    [Fact]
    public void MountainTroopsClimbWhereTheLineCrawls()
    {
        var (w, _) = Land("mountain");
        Assert.Equal(0.5f, Terrain(w, TestWorld.Inf), 3);       // a serra come metade a quem lá não sabe andar
        Assert.Equal(0.5f * 1.7f, Terrain(w, Alpino), 3);       // e devolve quase tudo a quem sabe
    }

    [Fact]
    public void AndTheyHoldTheRidgeBetterToo()
    {
        var (w, _) = Land("mountain");
        Assert.Equal(1.3f, Terrain(w, TestWorld.Inf, "str_defender"), 3);
        Assert.Equal(1.3f * 1.15f, Terrain(w, Alpino, "str_defender"), 3);
    }

    [Fact]
    public void TheSpecialtyIsWorthNothingOffItsGround()
    {
        var (w, _) = Land("plain");
        Assert.Equal(Terrain(w, TestWorld.Inf), Terrain(w, Alpino), 3);   // em campo aberto é infantaria
        var (w2, _) = Land("urban");
        Assert.Equal(Terrain(w2, TestWorld.Inf), Terrain(w2, Alpino), 3); // e na cidade também
    }

    [Fact]
    public void TheMarkTravelsWithTheUnitNotTheFlag()
    {
        var (w, db) = Land("mountain");
        db.ExecuteScript("INSERT INTO template VALUES (93,2,'Alpinos do Beta'); INSERT INTO template_unit VALUES (93,91,6);");
        // o mesmo caçador alpino sob a outra bandeira sobe a mesma montanha: a marca é da ficha da unidade
        Assert.Equal(Terrain(w, Alpino), Terrain(w, 93), 3);
    }

    [Fact]
    public void MarinesComeOffTheBoatFighting()
    {
        var w = Islands();
        var line = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        var marine = TestWorld.AddDivision(w, 2, 1, Fuzileiro, 2);
        Assert.Equal(w.Rule("naval_invasion_penalty"), CombatSystem.AmphibiousMult(w, line, w.Regions[3]), 3);
        Assert.Equal(w.Rule("naval_invasion_marine"), CombatSystem.AmphibiousMult(w, marine, w.Regions[3]), 3);
        Assert.True(CombatSystem.IsMarine(w, marine));
        Assert.False(CombatSystem.IsMarine(w, line));
    }

    [Fact]
    public void OnDryLandAMarineIsJustInfantry()
    {
        var w = Islands();
        var marine = TestWorld.AddDivision(w, 1, 1, Fuzileiro, 1);
        Assert.Equal(1f, CombatSystem.AmphibiousMult(w, marine, w.Regions[2]), 3);   // ataque por terra
        Assert.Equal(1f, CombatSystem.AmphibiousMult(w, marine, null), 3);           // e fora de batalha
    }

    [Fact]
    public void MarinesCrossRiversDrier()
    {
        var (w, _) = Land("plain", river: true);
        Assert.Equal(1f - 0.3f, Terrain(w, TestWorld.Inf), 3);
        Assert.Equal(1f - 0.3f + 0.2f, Terrain(w, Fuzileiro), 3);
    }

    [Fact]
    public void TheDifferenceShowsUpInTheBattleItself()
    {
        var (w, _) = Land("mountain");
        var ctx = CombatSystem.BuildContext(w, w.Regions[4], 1);
        var line = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var alp = TestWorld.AddDivision(w, 2, 1, Alpino, 3);
        var str = CombatSystem.SideStrength(w, new List<Division> { line, alp }, ctx, attacking: true, w.Regions[4]);
        Assert.True(str[1] > str[0] * 1.6f, $"alpinos {str[1]} vs linha {str[0]}");
    }
}
