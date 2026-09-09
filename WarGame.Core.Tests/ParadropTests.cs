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
    public void TheJumpLandsWithoutTakingOwnGround()
    {
        var (w, _, para) = Build();
        new ParadropCommand(1, para.Id, 3).Execute(w);           // região nossa: reforço largado atrás da frente
        TestWorld.Days(w, 2);
        Assert.Equal(3, para.RegionId);
        Assert.Equal(0, para.Captures);
        Assert.Equal(1, w.Regions[3].ControllerId);
    }






}
