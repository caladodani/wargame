using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Salto de pára-quedas: quem tem a marca `airborne` na tabela salta por cima da frente, leva
/// transportes presos ao voo durante dias e cai com metade da organização — e não salta em cima de tropa
/// inimiga, nem debaixo do céu dela, nem para fora do alcance.</summary>
public class ParadropTests
{
    private const int Para = 90;      // template de pára-quedistas do país 1, criado só para estes testes

    /// <summary>A unidade e o template nascem na base de dados, como no jogo: nenhum tipo de unidade vive no
    /// código, e a marca `airborne` que manda no salto é uma linha da tabela unit_tag.</summary>
    private const string ParaSql = @"
        INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES (90,'Paraquedistas','ground',1.5,40,1.0,35);
        INSERT INTO unit_stat VALUES (90,'soft_atk',7),(90,'hard_atk',1),(90,'defense',20),(90,'breakthrough',10),
                                    (90,'armor',0),(90,'piercing',5),(90,'hardness',0.1),(90,'hp',20);
        INSERT INTO unit_tag VALUES (90,'infantry'),(90,'ground'),(90,'airborne');
        INSERT INTO template VALUES (90,1,'Pára-quedistas');
        INSERT INTO template_unit VALUES (90,90,6);";

    /// <summary>Mapa em linha de 8 regiões (1..4 do país 1, 5..8 do país 2), em guerra e com aviões em casa.</summary>
    private static (World w, MsSqliteDatabase db) Fresh(float wings = 10f)
    {
        var (w, db) = TestWorld.Build();
        db.ExecuteScript(ParaSql);
        TestWorld.LinearMap(w, n: 8, split: 4);
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values) { c.AirPower = wings; c.IsPlayer = true; }
        w.Register(new ParadropSystem());
        return (w, db);
    }

    /// <summary>O mesmo mundo com uma divisão de pára-quedistas na região 1 — a 4 saltos da região 5, que é
    /// exactamente o alcance de origem.</summary>
    private static (World w, MsSqliteDatabase db, Division para) Build(float wings = 10f)
    {
        var (w, db) = Fresh(wings);
        return (w, db, TestWorld.AddDivision(w, 1, 1, Para, 1));
    }

    [Fact]
    public void TheRulesAndTheMarkComeFromTheDatabase()
    {
        var (w, _, para) = Build();
        Assert.Equal(4f, w.Rule("paradrop_range_hops"), 3);
        Assert.Equal(2f, w.Rule("paradrop_days"), 3);
        Assert.Equal(3f, w.Rule("paradrop_wings"), 3);
        Assert.Equal(40f, w.Rule("paradrop_min_org"), 3);
        Assert.Equal(45f, w.Rule("paradrop_org_cost"), 3);
        Assert.Equal(8f, w.Rule("paradrop_hp_cost"), 3);
        Assert.True(ParadropSystem.IsAirborne(w, para));
    }

    [Fact]
    public void OnlyParatroopersJump()
    {
        var (w, _, _) = Build();
        var foot = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1);
        Assert.Equal("não são pára-quedistas", ParadropSystem.Block(w, 1, foot.Id, 5));
        Assert.False(ParadropSystem.IsAirborne(w, foot));
        Assert.Null(ParadropSystem.Block(w, 1, 1, 5));
    }

    [Fact]
    public void TheFlightTakesDaysAndTheTransportsLeaveThePool()
    {
        var (w, _, para) = Build();
        Assert.Equal(10f, AirMissionSystem.Free(w, 1), 3);

        Assert.Null(new ParadropCommand(1, para.Id, 5).Validate(w));
        new ParadropCommand(1, para.Id, 5).Execute(w);
        Assert.True(para.InFlight);
        Assert.Equal(5, para.DropTargetId);
        Assert.Equal(3f, ParadropSystem.InFlight(w, 1), 3);      // três asas presas ao voo
        Assert.Equal(7f, AirMissionSystem.Free(w, 1), 3);        // e fora do pool livre enquanto ele durar
        Assert.Equal(1, para.RegionId);                          // ainda em terra na origem: os aviões partem de lá

        w.Tick();                                                // dia 1 de voo
        Assert.True(para.InFlight);
        Assert.Equal(1, para.RegionId);

        w.Tick();                                                // dia 2: aterra
        Assert.False(para.InFlight);
        Assert.Null(para.DropTargetId);
        Assert.Equal(5, para.RegionId);
        Assert.Contains(1, w.Regions[5].DivisionIds);
        Assert.Equal(1, w.Regions[5].ControllerId);              // região inimiga vazia: tomada ao aterrar
        Assert.Equal(1, para.Captures);
        Assert.Equal(55f, para.Org, 3);                          // 100 − paradrop_org_cost
        Assert.Equal(92f, para.Hp, 3);                           // 100 − paradrop_hp_cost
        Assert.Equal(10f, AirMissionSystem.Free(w, 1), 3);       // os transportes voltam a casa
    }

    [Fact]
    public void TheJumpLandsWithoutTakingOwnGround()
    {
        var (w, _, para) = Build();
        new ParadropCommand(1, para.Id, 3).Execute(w);           // região nossa: reforço largado atrás da frente
        TestWorld.Days(w, 2);
        Assert.Equal(3, para.RegionId);
        Assert.Equal(0, para.Captures);
        Assert.Equal(1, w.Regions[3].ControllerId);
    }

    [Fact]
    public void ThereIsNoJumpOntoEnemyTroopsNorBeyondRange()
    {
        var (w, _, para) = Build();
        TestWorld.AddDivision(w, 20, 2, TestWorld.Inf2, 5);
        Assert.Equal("há tropa inimiga no terreno de salto", ParadropSystem.Block(w, 1, para.Id, 5));
        Assert.Equal("fora do alcance (4 regiões)", ParadropSystem.Block(w, 1, para.Id, 6));
        Assert.Equal(4, ParadropSystem.Hops(w, 1, 5, 4));
        Assert.Null(ParadropSystem.Hops(w, 1, 6, 4));
        Assert.Equal(4, ParadropSystem.Reach(w, 1, 4).Count);    // regiões 2..5, e mais nenhuma
    }

    [Fact]
    public void OrganisationAndTransportsAreNeededToBoard()
    {
        var (w, _, para) = Build(wings: 2f);                     // menos asas do que as três do voo
        Assert.Equal("faltam transportes livres (3 asas por divisão)", ParadropSystem.Block(w, 1, para.Id, 5));

        var (w2, _, para2) = Build();
        para2.Org = 30f;
        Assert.Equal("organização abaixo de 40 para embarcar", ParadropSystem.Block(w2, 1, para2.Id, 5));
    }

    [Fact]
    public void TheEnemySkyKeepsTheTransportsAtHome()
    {
        var (w, _, para) = Build();
        AirMissionSystem.Assign(w, 2, 5, "superioridade", 4f);   // o céu do alvo é dele
        Assert.True(ParadropSystem.EnemySky(w, 1, 5));
        Assert.Equal("o céu sobre a região é do inimigo", ParadropSystem.Block(w, 1, para.Id, 5));

        AirMissionSystem.Assign(w, 1, 5, "superioridade", 5f);   // varrido o céu, o salto parte
        Assert.False(ParadropSystem.EnemySky(w, 1, 5));
        Assert.Null(ParadropSystem.Block(w, 1, para.Id, 5));
    }

    [Fact]
    public void InTheAirTheDivisionNeitherMarchesNorTakesOrders()
    {
        var (w, _, para) = Build();
        w.Register(new MovementSystem());
        new ParadropCommand(1, para.Id, 5).Execute(w);

        Assert.Equal("Em voo — só depois de aterrar", new MoveDivisionCommand(1, para.Id, 2).Validate(w));
        para.SetPath(new[] { 2 });                               // mesmo com rota metida à força
        w.Tick();
        Assert.Equal(1, para.RegionId);                          // não deu um passo: está dentro do avião
        Assert.True(para.InFlight);
    }

    [Fact]
    public void GroundTakenWhileInTheAirCallsOffTheJump()
    {
        var (w, _, para) = Build();
        string? why = null;
        w.Events.Subscribe<ParadropAborted>(e => why = e.Why);
        new ParadropCommand(1, para.Id, 5).Execute(w);
        TestWorld.AddDivision(w, 20, 2, TestWorld.Inf2, 5);      // o inimigo ocupou o sítio a meio do voo

        TestWorld.Days(w, 2);
        Assert.Equal("o inimigo ocupou o terreno de salto", why);
        Assert.Equal(1, para.RegionId);                          // fica onde estava
        Assert.False(para.InFlight);
        Assert.Equal(77.5f, para.Org, 3);                        // 100 − metade do custo da queda
        Assert.Equal(2, w.Regions[5].ControllerId);              // e a região continua dele
    }

    [Fact]
    public void TheFlightSurvivesASaveAndAReload()
    {
        var (w, staticDb, para) = Build();
        new ParadropCommand(1, para.Id, 5).Execute(w);
        w.Tick();                                                // um dia de voo já andado

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = Fresh();                                   // mundo novo, sem tropa: a divisão vem do save
        repo.LoadSave(w2, save);
        var back = w2.Divisions[para.Id];
        Assert.True(back.InFlight);
        Assert.Equal(5, back.DropTargetId);
        Assert.Equal(1f, back.DropDays, 3);

        w2.Tick();
        Assert.Equal(5, back.RegionId);                          // o dia que faltava cumpre-se depois de recarregar
    }
}
