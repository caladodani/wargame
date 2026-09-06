using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Produção em série: a encomenda marcada volta à fila assim que é entregue.</summary>
public class RepeatProductionTests
{
    private static World Build(float money = 100000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ProductionSystem());
        w.Countries[1].Money = money;
        return w;
    }

    private static void Order(World w, bool repeat)
    {
        new BuildDivisionCommand(1, TestWorld.Inf).Execute(w);
        if (repeat) new SetProductionRepeatCommand(1, w.Countries[1].Queue.Count - 1, true).Execute(w);
    }

    [Fact]
    public void RepeatedOrder_QueuesItselfAgain()
    {
        var w = Build(); int days = (int)w.Rule("build_min_days");
        Order(w, repeat: true);
        TestWorld.Days(w, days);
        Assert.Single(w.Divisions);
        var o = Assert.Single(w.Countries[1].Queue);
        Assert.True(o.Repeat);
        Assert.Equal(0f, o.Progress);      // recomeça do zero

        TestWorld.Days(w, days);
        Assert.Equal(2, w.Divisions.Count);
    }

    [Fact]
    public void PlainOrder_LeavesTheQueueEmpty()
    {
        var w = Build();
        Order(w, repeat: false);
        TestWorld.Days(w, (int)w.Rule("build_min_days"));
        Assert.Single(w.Divisions);
        Assert.Empty(w.Countries[1].Queue);
    }

    [Fact]
    public void Toggle_TurnsSeriesOff()
    {
        var w = Build();
        Order(w, repeat: true);
        Assert.Null(new SetProductionRepeatCommand(1, 0, false).Validate(w));
        new SetProductionRepeatCommand(1, 0, false).Execute(w);
        TestWorld.Days(w, (int)w.Rule("build_min_days"));
        Assert.Empty(w.Countries[1].Queue);
    }

    [Fact]
    public void Validate_RejectsOrderThatIsNotThere()
    {
        var w = Build();
        Assert.Equal("Encomenda inexistente", new SetProductionRepeatCommand(1, 0, true).Validate(w));
        Order(w, repeat: false);
        Assert.Equal("Encomenda inexistente", new SetProductionRepeatCommand(1, 5, true).Validate(w));
    }

    [Fact]
    public void FullQueue_DoesNotGrowBeyondTheCap()
    {
        var w = Build();
        w.Rules["production_queue_max"] = 2;
        Order(w, repeat: true);
        Order(w, repeat: false);
        Assert.Equal("Fila cheia", new BuildDivisionCommand(1, TestWorld.Inf).Validate(w));

        TestWorld.Days(w, (int)w.Rule("build_min_days"));

        Assert.Equal(2, w.Divisions.Count);                       // as duas saíram no mesmo dia
        Assert.Single(w.Countries[1].Queue);                      // e só a repetida voltou
        Assert.True(w.Countries[1].Queue[0].Repeat);
    }

    [Fact]
    public void Repeat_SurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Order(w, repeat: true);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.True(Assert.Single(w2.Countries[1].Queue).Repeat);
    }
}
