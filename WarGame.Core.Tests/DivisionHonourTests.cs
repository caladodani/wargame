using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Honras de batalha: nome próprio ganho em campanha. Ao contrário das condecorações, que se
/// acumulam, a divisão só carrega uma — a mais alta — e o nome fica com a região onde a mereceu.
/// Os moldes vêm do seed (ferro, lanceiros, muralha, leoes, imortal).</summary>
public class DivisionHonourTests
{
    private static (World w, Division d) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        return (w, d);
    }

    /// <summary>Avança até um dia múltiplo de honour_check_days e corre o sistema.</summary>
    private static void Confer(World w)
    {
        var sys = new DivisionHonourSystem();
        int period = Math.Max(1, (int)w.Rule("honour_check_days", 3f));
        for (int i = 0; i <= period; i++) { sys.Tick(w); TestWorld.Days(w, 1); }
    }

    [Fact]
    public void TheCatalogue_ComesFromTheDatabase()
    {
        var (w, _) = Build();
        Assert.True(w.HonourDefs.Count >= 5);
        var h = w.HonourDefs["ferro"];
        Assert.Equal("battles", h.Metric);
        Assert.True(h.Bonus > 0f);
        Assert.Contains("{r}", h.Title);          // o molde tem lugar para a região
    }

    [Fact]
    public void ANewDivision_HasNoName_AndKeepsTheTemplateOne()
    {
        var (w, d) = Build();
        Confer(w);
        Assert.Null(d.Honour);
        Assert.Null(d.HonourName);
        Assert.Equal(d.Name, d.WarName);
    }

    [Fact]
    public void ThreeBattles_EarnTheFirstHonour_NamedAfterTheGround()
    {
        var (w, d) = Build();
        d.Name = "3.ª de Infantaria";
        d.Battles = (int)w.HonourDefs["ferro"].Threshold;
        Confer(w);

        Assert.Equal("ferro", d.Honour);
        Assert.Equal($"Punhos de Ferro de {w.Regions[d.RegionId].Name}", d.HonourName);
        Assert.Equal($"3.ª de Infantaria «{d.HonourName}»", d.WarName);
    }

    [Fact]
    public void OnlyTheHighestHonourIsWorn()
    {
        var (w, d) = Build();
        d.Battles = 20; d.Captures = 20; d.Xp = 95f;
        foreach (var m in w.MedalDefs.Keys) d.Medals.Add(m);
        Confer(w);

        Assert.Equal("imortal", d.Honour);        // a de sort mais alto do seed
        Assert.StartsWith("Imortais de ", d.HonourName);
        Assert.DoesNotContain("Punhos", d.WarName!);   // a de baixo não fica agarrada ao nome
    }

    [Fact]
    public void AGreaterHonourReplacesTheOneBefore_AndTheLesserNeverComesBack()
    {
        var (w, d) = Build();
        var news = new List<DivisionHonoured>();
        w.Events.Subscribe<DivisionHonoured>(news.Add);

        d.Battles = 3;
        Confer(w);
        Assert.Equal("ferro", d.Honour);

        d.Xp = 60f;                               // "leoes" (sort 4) passa à frente de "muralha"/"ferro"
        Confer(w);
        Assert.Equal("leoes", d.Honour);
        Assert.StartsWith("Leões de ", d.HonourName);

        d.Xp = 0f; d.Battles = 3;                 // perder veterania não tira o nome ganho
        Confer(w);
        Assert.Equal("leoes", d.Honour);
        Assert.Equal(2, news.Count);
    }

    [Fact]
    public void TheNameSticksToTheDivision_NotToTheRegion()
    {
        var (w, d) = Build();
        d.Battles = 3;
        Confer(w);
        string earned = d.HonourName!;
        Assert.Contains(w.Regions[1].Name, earned);

        d.RegionId = 3;                           // marchou para outro lado
        Confer(w);
        Assert.Equal(earned, d.HonourName);
    }

    [Fact]
    public void ANamedDivision_RecoversFasterOutOfCombat()
    {
        var (w, named) = Build();
        var plain = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2);
        named.Org = plain.Org = 20f;
        named.Battles = 3;
        Confer(w);
        Assert.NotNull(named.Honour);

        named.Org = plain.Org = 20f;               // os dias do Confer já recompuseram: repor o empate
        w.Register(new RecoverySystem());
        w.Tick();

        Assert.True(named.Org > plain.Org, $"com nome {named.Org}, sem nome {plain.Org}");
        Assert.True(DivisionHonourSystem.Bonus(w, named) > 0f);
        Assert.Equal(0f, DivisionHonourSystem.Bonus(w, plain));
    }

    [Fact]
    public void TheBonusIsCappedByTheRule()
    {
        var (w, d) = Build();
        w.Rules["honour_bonus_max"] = 0.05f;
        d.Battles = 20; d.Captures = 20; d.Xp = 95f;
        foreach (var m in w.MedalDefs.Keys) d.Medals.Add(m);
        Confer(w);

        Assert.True(w.HonourDefs[d.Honour!].Bonus > 0.05f);   // a honra dá mais do que o tecto
        Assert.Equal(0.05f, DivisionHonourSystem.Bonus(w, d), 3);
    }

    [Fact]
    public void AnUnknownMetric_NeverGivesAnHonour()
    {
        var (w, d) = Build();
        w.HonourDefs.Clear();
        w.HonourDefs["fantasia"] = new HonourDef("fantasia", "Fantasmas de {r}", "métrica que não existe", "moral", 0f, 1f, 9);
        d.Battles = 99; d.Xp = 99f;
        Confer(w);
        Assert.Null(d.Honour);
    }

    [Fact]
    public void TheNameSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Battles = 8;
        new DivisionHonourSystem().Tick(w);
        Assert.NotNull(d.Honour);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var d2 = w2.Divisions[1];
        Assert.Equal(d.Honour, d2.Honour);
        Assert.Equal(d.HonourName, d2.HonourName);
        Assert.Equal(d.WarName, d2.WarName);
    }
}
