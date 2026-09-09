using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

/// <summary>As marcas do material (equipment_mark): a cadeia investigação → fábrica → armazém → frente.
///
/// O que se prova aqui é o atraso: o laboratório abre uma geração nova, a fábrica reafina-se e paga em
/// ritmo, a prateleira mistura o novo com o velho, e a divisão só se bate melhor quando o material lhe
/// chega às mãos. E que um mundo sem tabela de marcas continua a lutar exactamente como lutava.</summary>
namespace WarGame.Core.Tests;

public class MarksTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (var c in w.Countries.Values) { c.Money = 10000f; c.IsPlayer = true; }
        return w;
    }

    [Fact]
    public void AsMarcasVemDaBaseDeDados()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.EquipmentMarks.Count >= 8);
        var inf = Marks.All(w, 1);
        Assert.True(inf.Count >= 2);
        Assert.Equal(1, inf[0].Mark);
        Assert.Equal("", inf[0].TechId);                            // a de origem não precisa de nada
        Assert.True(inf[^1].Power > inf[0].Power, "a última geração tem de valer mais em combate");
        Assert.True(inf[^1].Cost > inf[0].Cost, "e tem de custar mais a fazer");
        Assert.True(inf[^1].Wear < inf[0].Wear, "e gastar-se menos");
    }

    [Fact]
    public void AInvestigacaoEQueAbreAMarcaSeguinte()
    {
        var w = Build();
        var c = w.Countries[1];
        var inf = Marks.All(w, 1);
        float antes = Marks.Open(w, c, 1);
        Assert.Equal(inf[0].Mark, antes, 3);                        // sem tecnologia nenhuma, só a de origem

        c.Techs.Add(inf[1].TechId);
        Assert.Equal(inf[1].Mark, Marks.Open(w, c, 1), 3);
    }

    [Fact]
    public void ALinhaReafinaSeEPerdeRitmo()
    {
        var w = Build();
        var c = w.Countries[1];
        var inf = Marks.All(w, 1);
        var (mark, eff) = Marks.Retool(w, c, 1, inf[0].Mark, 1.4f);
        Assert.Equal(inf[0].Mark, mark, 3);                         // nada aberto: a linha não pára
        Assert.Equal(1.4f, eff, 3);

        c.Techs.Add(inf[1].TechId);
        (mark, eff) = Marks.Retool(w, c, 1, inf[0].Mark, 1.4f);
        Assert.Equal(inf[1].Mark, mark, 3);
        Assert.True(eff < 1.4f, "trocar de ferramenta tinha de custar ritmo");
        Assert.True(eff >= 1f, "a linha nunca fica pior do que uma fábrica nova");
    }

    [Fact]
    public void OConjuntoMelhorCustaMais()
    {
        var w = Build();
        var inf = Marks.All(w, 1);
        var linha = new ProductionOrder { UnitTypeId = 1, Mark = inf[0].Mark };
        float barato = w.OrderCost(linha);
        linha.Mark = inf[^1].Mark;
        Assert.True(w.OrderCost(linha) > barato, "a geração nova tinha de sair mais cara da fábrica");
    }

    [Fact]
    public void APrateleiraEUmaPilhaMisturada()
    {
        Assert.Equal(2f, Marks.Blend(0f, 0f, 10f, 2f), 3);          // prateleira vazia fica com o que chega
        Assert.Equal(1.5f, Marks.Blend(10f, 1f, 10f, 2f), 3);       // metade e metade
        Assert.Equal(1.1f, Marks.Blend(90f, 1f, 10f, 2f), 3);       // dez novos em noventa velhos quase não puxam
    }

    [Fact]
    public void ADivisaoSoSobeDeMarcaQuandoRecebeMaterial()
    {
        var w = Build();
        var c = w.Countries[1];
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var need = w.KitNeed(d.TemplateId);
        Assert.NotEmpty(need);

        Marks.Enlist(w, d);
        float partida = d.Mark;
        Assert.True(partida > 0f, "a tropa de partida tem de levar a marca de origem");

        foreach (var m in w.EquipmentMarks.Values) if (m.TechId.Length > 0) c.Techs.Add(m.TechId);
        Assert.True(Marks.Open(w, c, need[0].UnitTypeId) > partida, "o laboratório já abriu melhor");
        Assert.Equal(partida, d.Mark, 3);                           // e a frente continua onde estava

        // agora a prateleira enche-se de material novo e a divisão gasta reequipa-se com ele
        d.Kit = 0.5f;
        foreach (var (type, qty) in need)
        {
            c.Stock[type] = qty * 10f;
            c.StockMark[type] = Marks.Open(w, c, type);
        }
        EquipmentSystem.Refill(w, c, d, 0.4f);
        Assert.True(d.Mark > partida, "o que chegou da prateleira tinha de puxar a marca da divisão");
        Assert.True(d.Mark < Marks.Open(w, c, need[0].UnitTypeId),
                    "e não pode saltar toda de uma vez: metade do material dela ainda é o velho");
    }

    [Fact]
    public void OMaterialNovoBateComMaisForca()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var inf = Marks.All(w, w.KitNeed(d.TemplateId)[0].UnitTypeId);

        d.Mark = inf[0].Mark;
        float velha = Marks.PowerMult(w, d);
        d.Mark = inf[^1].Mark;
        float nova = Marks.PowerMult(w, d);
        Assert.True(nova > velha, $"a última geração tinha de bater mais forte ({nova:0.###} contra {velha:0.###})");
        Assert.True(Marks.WearMult(w, d) < 1f, "e gastar-se menos por cada homem que cai");
    }

    [Fact]
    public void AFabricaEntregaNaMarcaQueEstaAFazer()
    {
        var w = Build();
        var c = w.Countries[1];
        c.Money = 100_000f;
        var inf = Marks.All(w, 1);
        foreach (var m in inf) if (m.TechId.Length > 0) c.Techs.Add(m.TechId);

        w.Register(new ProductionSystem());
        var cmd = new BuildKitCommand(1, 1);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        TestWorld.Days(w, 40);

        Assert.True(c.Stocked(1) > 0f, "quarenta dias de linha tinham de encher a prateleira");
        Assert.Equal(inf[^1].Mark, c.StockedMark(1), 2);            // a linha assumiu a melhor marca aberta
        Assert.Equal(inf[^1].Mark, c.Queue[0].Mark, 3);
    }

    [Fact]
    public void UmMundoSemMarcasLutaComoAntes()
    {
        var w = Build();
        w.EquipmentMarks.Clear();
        var c = w.Countries[1];
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Mark = 0f;
        Marks.Enlist(w, d);
        Assert.Equal(0f, d.Mark, 3);                                // sem tabela não se inventa marca nenhuma
        Assert.Equal(1f, Marks.PowerMult(w, d), 3);
        Assert.Equal(1f, Marks.WearMult(w, d), 3);
        Assert.Equal(0f, Marks.Open(w, c, 1), 3);
        Assert.Equal(1f, Marks.Cost(w, 1, 3f), 3);                  // e o conjunto custa o preço de sempre
    }
}
