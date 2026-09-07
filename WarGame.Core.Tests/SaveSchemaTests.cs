using WarGame.Core.Data;
using WarGame.Core.Model;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Garantia de que a apresentação depende: no APK os *.sql ficam fora do export, por isso o Game
/// reconstrói o schema do save a partir do sqlite_master do static.db (que foi criado com schema.sql inteiro).
/// O SQLite guarda o CREATE sem IF NOT EXISTS — o Game repõe-no por texto; aqui espelha-se essa transformação.</summary>
public class SaveSchemaTests
{
    private static string SchemaFromStatic(IDatabase staticDb) =>
        SqlWorldRepository.SchemaFromSqliteMaster(staticDb);      // o mesmo texto que o Game usa no telemóvel

    [Fact]
    public void SchemaFromSqliteMaster_CreatesSaveTables_AndIsIdempotent()
    {
        var (w, staticDb) = TestWorld.Build();
        var schema = SchemaFromStatic(staticDb);
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        SqlWorldRepository.EnsureSaveSchema(save, schema);   // reabrir um save existente não pode falhar
        Assert.False(SqlWorldRepository.HasSave(save));
        foreach (var t in new[] { "save_meta", "s_country", "s_region", "s_division", "s_war", "s_production_queue", "s_battle", "s_battle_division" })
            Assert.Single(save.Query("SELECT name FROM sqlite_master WHERE type='table' AND name=?", t));
    }

    /// <summary>No telemóvel o schema do save é o sqlite_master do data/static.db — os *.sql não entram no
    /// APK. Um static.db construído antes de uma tabela de save nova deixa o jogo sem essa tabela e o save
    /// rebenta no DELETE FROM dela. Este teste é o alarme: static.db tem de trazer o schema.sql inteiro.</summary>
    [Fact]
    public void TheShippedStaticDb_CarriesEverySaveTableOfTheSchema()
    {
        var wanted = System.Text.RegularExpressions.Regex
            .Matches(File.ReadAllText("data/schema.sql"), @"CREATE TABLE IF NOT EXISTS\s+(\w+)")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(wanted);

        using var db = new MsSqliteDatabase("Data Source=data/static.db;Mode=ReadOnly");
        var have = db.Query("SELECT name FROM sqlite_master WHERE type='table'").Select(r => (string)r["name"]!).ToHashSet();
        Assert.All(wanted, t => Assert.Contains(t, have));
    }

    [Fact]
    public void SaveRoundTrip_OnFallbackSchema_RestoresPlayerDayAndDivisions()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true; w.Countries[1].Money = 12.5f;
        var d = TestWorld.AddDivision(w, 7, 1, TestWorld.Inf, 2); d.SetPath(new[] { 3 });
        w.Countries[1].Queue.Add(new ProductionOrder { TemplateId = TestWorld.Armor, Progress = 3f });
        for (int i = 0; i < 6; i++) w.Clock.Advance();

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SchemaFromStatic(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);
        Assert.True(SqlWorldRepository.HasSave(save));

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(6, w2.Clock.Day);
        Assert.True(w2.Countries[1].IsPlayer);
        Assert.Equal(12.5f, w2.Countries[1].Money);
        Assert.Equal(new[] { 3 }, w2.Divisions[7].Path);
        Assert.Contains(7, w2.Regions[2].DivisionIds);
        Assert.Equal(TestWorld.Armor, w2.Countries[1].Queue.Single().TemplateId);
    }
}
