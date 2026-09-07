using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A fila de produção é a prioridade: o cofre e as linhas de montagem servem-na de cima para baixo.
/// Estes testes tratam de a poder mexer sem perder o que já foi feito.</summary>
public class ProductionQueueTests
{
    private static World Build(float money = 1000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = money;
        w.Countries[1].Manpower = 1e9f;
        return w;
    }

    private static void Order(World w, int template) => new BuildDivisionCommand(1, template).Execute(w);

    [Fact]
    public void AnOrderMovesToWhereItIsToldAndTheRestClosesTheGap()
    {
        var w = Build();
        Order(w, 1); Order(w, 2); Order(w, 3);

        Assert.Null(new MoveProductionOrderCommand(1, 2, 0).Validate(w));
        new MoveProductionOrderCommand(1, 2, 0).Execute(w);
        Assert.Equal(new[] { 3, 1, 2 }, w.Countries[1].Queue.Select(o => o.TemplateId));

        new MoveProductionOrderCommand(1, 0, 2).Execute(w);          // e daí para o fim da fila
        Assert.Equal(new[] { 1, 2, 3 }, w.Countries[1].Queue.Select(o => o.TemplateId));
    }

    [Fact]
    public void TheWorkAlreadyDoneTravelsWithTheOrder()
    {
        var w = Build();
        Order(w, 1); Order(w, 2);
        w.Countries[1].Queue[1].Progress = 7.5f;
        w.Countries[1].Queue[1].Repeat = true;

        new MoveProductionOrderCommand(1, 1, 0).Execute(w);
        Assert.Equal(2, w.Countries[1].Queue[0].TemplateId);
        Assert.Equal(7.5f, w.Countries[1].Queue[0].Progress, 3);
        Assert.True(w.Countries[1].Queue[0].Repeat);
    }

    [Fact]
    public void WhoIsAtTheHeadOfTheQueueEatsTheTreasuryFirst()
    {
        var w = Build(1f);                                            // cofre para uma encomenda só, e a custo
        w.Register(new ProductionSystem());
        Order(w, 1); Order(w, 2);

        var q = w.Countries[1].Queue;
        w.Tick();
        Assert.True(q[0].Progress > q[1].Progress * 5f, $"a cabeça da fila leva o cofre ({q[0].Progress} vs {q[1].Progress})");
        float leftovers = q[1].Progress;

        w.Countries[1].Money = 1f;
        new MoveProductionOrderCommand(1, 1, 0).Execute(w);           // a coluna blindada passa à frente
        w.Tick();
        Assert.Equal(2, q[0].TemplateId);
        Assert.True(q[0].Progress > leftovers * 5f, $"e agora é ela que come primeiro ({q[0].Progress} vs {leftovers})");
    }

    [Fact]
    public void ThereIsNoMovingWhatIsNotThere()
    {
        var w = Build();
        Order(w, 1);
        Assert.Equal("Encomenda inexistente", new MoveProductionOrderCommand(1, 3, 0).Validate(w));
        Assert.Equal("Lugar inexistente na fila", new MoveProductionOrderCommand(1, 0, 4).Validate(w));
        Assert.Equal("A encomenda já está nesse lugar", new MoveProductionOrderCommand(1, 0, 0).Validate(w));
        Assert.Equal("país inválido", new MoveProductionOrderCommand(77, 0, 0).Validate(w));
    }

    [Fact]
    public void CancellingStillHitsTheOrderItWasToldTo()
    {
        var w = Build();
        Order(w, 1); Order(w, 2); Order(w, 3);
        new MoveProductionOrderCommand(1, 2, 0).Execute(w);            // fila: 3, 1, 2

        new CancelProductionCommand(1, 1).Execute(w);
        Assert.Equal(new[] { 3, 2 }, w.Countries[1].Queue.Select(o => o.TemplateId));
    }

    [Fact]
    public void SaveRoundTripKeepsTheOrderOfTheQueue()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 100f;
        Order(w, 1); Order(w, 2); Order(w, 3);
        new MoveProductionOrderCommand(1, 2, 0).Execute(w);
        w.Countries[1].Queue[0].Progress = 3.25f;

        string schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(new[] { 3, 1, 2 }, w2.Countries[1].Queue.Select(o => o.TemplateId));
        Assert.Equal(3.25f, w2.Countries[1].Queue[0].Progress, 3);
    }
}
