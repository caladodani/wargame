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

    /// <summary>O bug que o dono apanhou: "não está a dar para guardar, o autosave está a dar erro
    /// constantemente". As linhas do save saem de listas em memória e vão para tabelas de chave composta;
    /// bastava uma segunda esquadra no mesmo mar para o INSERT rebentar em UNIQUE, a transacção morrer a
    /// meio e o jogo nunca mais guardar. Guardar tem de aguentar a lista suja — e a última linha ganha.</summary>
    [Fact]
    public void Uma_lista_com_a_mesma_chave_duas_vezes_nao_parte_a_gravacao()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        string mid = w.NavalMissionDefs.Values.OrderBy(d => d.Sort).First().Id;
        w.NavalMissions.Add(new NavalMission { CountryId = 1, RegionId = 2, MissionId = mid, SinceDay = 1, Ships = 3f });
        w.NavalMissions.Add(new NavalMission { CountryId = 1, RegionId = 2, MissionId = mid, SinceDay = 1, Ships = 5f });

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SchemaFromStatic(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);                     // antes: UNIQUE constraint failed e save perdido

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(5f, w2.NavalMissions.Single(m => m.CountryId == 1 && m.RegionId == 2).Ships);
    }

    /// <summary>E o save tem de aguentar ser escrito duas vezes seguidas na mesma ligação: é o que o
    /// autosave faz de 30 em 30 dias, e é aí que uma transacção mal fechada aparecia.</summary>
    [Fact]
    public void Guardar_duas_vezes_seguidas_deixa_o_save_inteiro()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SchemaFromStatic(staticDb));
        var repo = new SqlWorldRepository(staticDb);

        repo.WriteSave(w, save);
        for (int i = 0; i < 4; i++) w.Clock.Advance();
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(4, w2.Clock.Day);
    }
}
