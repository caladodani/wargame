using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Ritmo da linha de montagem (HoI4: production efficiency). Até aqui uma fábrica militar produzia
/// sempre ao mesmo ritmo: a centésima divisão de infantaria saía tão devagar como a primeira, e trocar de
/// modelo todas as semanas não custava nada. Agora a linha aprende — a primeira unidade é um protótipo e sai
/// ao ritmo de origem, mas a série seguinte ganha jeito todos os dias em que produz, até um tecto, e arrefece
/// nos dias em que fica parada por falta de cofre ou de fábrica. Trocar de modelo abre linha nova, que começa
/// outra vez do princípio: é essa a decisão que o jogo passa a pedir.
///
/// O ritmo do dia 1 tem de ficar exactamente como estava — os ProductionTests medem-no ao milésimo.</summary>
public class LineEfficiencyTests
{
    private static World Build(float money = 100000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ProductionSystem());
        w.Countries[1].Money = money;
        w.Countries[1].Manpower = 10_000_000f;
        return w;
    }

    /// <summary>Encomenda em série (a que fica na fila para sempre e é a que ganha ritmo).</summary>
    private static void Series(World w, int country, int template)
    {
        new BuildDivisionCommand(country, template).Execute(w);
        w.Countries[country].Queue[^1].Repeat = true;
    }

    private static ProductionOrder Line(World w) => w.Countries[1].Queue[0];

    /// <summary>O esquema do save sai do mesmo sítio de onde sai no jogo: o static.db.</summary>
    private static string Schema(IDatabase staticDb) =>
        SqlWorldRepository.SchemaFromSqliteMaster(staticDb);
    private static int MinDays(World w) => (int)w.Rule("build_min_days");

    [Fact]
    public void TheRulesComeFromTheDatabase()
    {
        var w = Build();
        Assert.Equal(0.03f, w.Rule("line_efficiency_gain"), 4);
        Assert.Equal(0.02f, w.Rule("line_efficiency_decay"), 4);
        Assert.Equal(1.5f, w.Rule("line_efficiency_max"), 4);
    }

    [Fact]
    public void TheFirstUnitIsAPrototype_TheLineRunsAtTheBaseRateUntilItLeaves()
    {
        var w = Build();
        Series(w, 1, TestWorld.Inf);
        float cost = w.TemplateCost(TestWorld.Inf);

        for (int i = 0; i < MinDays(w) - 1; i++)
        {
            w.Tick();
            Assert.Equal(1f, Line(w).Efficiency, 4);          // enquanto o protótipo não sai, não há jeito nenhum
            Assert.Equal(0, Line(w).Delivered);
        }
        Assert.Equal(cost / MinDays(w) * (MinDays(w) - 1), Line(w).Progress, 3);
        w.Tick();
        Assert.Single(w.Divisions);                            // exactamente ao fim de build_min_days, como sempre
    }

    [Fact]
    public void TheSecondUnitOfTheSeriesGainsRhythmEveryDayItProduces()
    {
        var w = Build();
        Series(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w));                         // sai o protótipo e a linha volta à fila
        var line = Line(w);
        Assert.Equal(1, line.Delivered);
        Assert.True(line.Repeat);

        float gain = w.Rule("line_efficiency_gain");
        for (int i = 1; i <= 3; i++)
        {
            w.Tick();
            Assert.Equal(1f + gain * i, Line(w).Efficiency, 4);
        }
    }

    [Fact]
    public void TheRhythmStopsAtTheCeiling()
    {
        var w = Build();
        Series(w, 1, TestWorld.Armor);
        TestWorld.Days(w, 400);
        Assert.Equal(w.Rule("line_efficiency_max"), Line(w).Efficiency, 4);
        Assert.True(Line(w).Delivered > 1, "a série tinha de ter entregue várias");
    }

    [Fact]
    public void ASeasonedLineBuildsFasterThanANewOne()
    {
        var w = Build();
        Series(w, 1, TestWorld.Inf);
        int first = 0;
        while (w.Divisions.Count == 0) { w.Tick(); first++; }
        int had = w.Divisions.Count;
        int second = 0;
        while (w.Divisions.Count == had) { w.Tick(); second++; }
        Assert.True(second < first, $"a segunda unidade demorou {second} dias e a primeira {first}");
        Assert.Equal(MinDays(w), first);                       // e a primeira continua a ser o ritmo de origem
    }

    [Fact]
    public void AStoppedLineCoolsDownAndNeverFallsBelowTheBaseRate()
    {
        var w = Build();
        Series(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w) + 20);                    // uma série já com jeito
        float hot = Line(w).Efficiency;
        Assert.True(hot > 1f);

        w.Countries[1].Money = 0f;                             // cofre vazio: a linha pára
        w.Tick();
        Assert.Equal(hot - w.Rule("line_efficiency_decay"), Line(w).Efficiency, 4);
        TestWorld.Days(w, 200);
        Assert.Equal(1f, Line(w).Efficiency, 4);               // arrefece até ao ritmo de origem e fica lá
    }

    [Fact]
    public void ChangingModelOpensANewLineThatStartsFromScratch()
    {
        var w = Build();
        Series(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w) + 15);
        Assert.True(Line(w).Efficiency > 1f);

        new BuildDivisionCommand(1, TestWorld.Armor).Execute(w);
        var fresh = w.Countries[1].Queue[^1];
        Assert.Equal(1f, fresh.Efficiency, 4);
        Assert.Equal(0, fresh.Delivered);
        // e a linha antiga não perde o que aprendeu por causa da nova
        Assert.True(Line(w).Efficiency > 1f);
    }

    [Fact]
    public void AnOldSaveWithoutTheRhythmColumnsIsBroughtForward()
    {
        // um save gravado antes desta versão não tem as colunas do ritmo. Se ninguém as acrescentar, a gravação
        // seguinte estoira no meio de um tick — e no jogo isso não é um teste vermelho, é o motor a ir abaixo.
        var (w, staticDb) = TestWorld.Build();
        string schema = Schema(staticDb);
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        foreach (var col in new[] { "efficiency", "delivered" })            // desfaz-se a versão nova: fica o save velho
            save.Execute($"ALTER TABLE s_production_queue DROP COLUMN {col}");
        Assert.DoesNotContain(save.Query("PRAGMA table_info(s_production_queue)"), r => (string)r["name"]! == "efficiency");

        SqlWorldRepository.EnsureSaveSchema(save, schema);                  // a versão nova pega no save velho
        Assert.Contains(save.Query("PRAGMA table_info(s_production_queue)"), r => (string)r["name"]! == "efficiency");
        Assert.Contains(save.Query("PRAGMA table_info(s_production_queue)"), r => (string)r["name"]! == "delivered");

        TestWorld.LinearMap(w);
        w.Register(new ProductionSystem());
        w.Countries[1].Money = 100000f;
        w.Countries[1].Manpower = 10_000_000f;
        Series(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w) + 5);
        new SqlWorldRepository(staticDb).WriteSave(w, save);                // é isto que estoirava
        Assert.Equal(Line(w).Efficiency,
                     Convert.ToSingle(save.Query("SELECT efficiency FROM s_production_queue").Single()["efficiency"]), 4);
    }

    [Fact]
    public void TheRhythmOfTheLineSurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ProductionSystem());
        w.Countries[1].Money = 100000f;
        w.Countries[1].Manpower = 10_000_000f;
        Series(w, 1, TestWorld.Inf);
        TestWorld.Days(w, MinDays(w) + 8);
        var before = Line(w);
        Assert.True(before.Efficiency > 1f);

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, Schema(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = w2.Countries[1].Queue[0];
        Assert.Equal(before.Efficiency, back.Efficiency, 4);
        Assert.Equal(before.Delivered, back.Delivered);
        Assert.Equal(before.Progress, back.Progress, 3);
    }
}
