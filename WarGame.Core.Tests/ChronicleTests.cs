using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Crónica da campanha: o que fica escrito, o que não chega a ser escrito e o que sobrevive ao save.
/// Os géneros e os pesos vêm da tabela chronicle_kind — o que se mede aqui é a regra, não o texto.</summary>
public class ChronicleTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ChronicleSystem());
        w.Tick();                      // o Tick é que liga o sistema ao barramento
        return w;
    }

    [Fact]
    public void TheKindsComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.ChronicleKinds.Count >= 10);
        Assert.Equal(3, w.ChronicleKinds["guerra"].Weight);
        Assert.NotEqual("", w.ChronicleKinds["guerra"].Icon);
    }

    [Fact]
    public void ADeclarationOfWar_IsWrittenDown()
    {
        var w = Setup();
        w.Events.Publish(new WarDeclared(1, 2));

        var e = Assert.Single(w.Chronicle);
        Assert.Equal("guerra", e.Kind);
        Assert.Contains("Alfa", e.Text);
        Assert.Contains("Beta", e.Text);
        Assert.Equal(1, e.CountryId);
    }

    [Fact]
    public void TheDayIsTheDayItHappened()
    {
        var w = Setup();
        TestWorld.Days(w, 30);
        w.Events.Publish(new CountryCapitulated(2, 1));

        Assert.Equal(w.Clock.Day, Assert.Single(w.Chronicle).Day);
    }

    [Fact]
    public void OnlyTheFallOfACapitalIsWorthTheChronicle()
    {
        var w = Setup();
        w.Events.Publish(new RegionCaptured(5, 2, 1));      // região qualquer do país 2
        Assert.Empty(w.Chronicle);

        w.Events.Publish(new RegionCaptured(6, 2, 1));      // a capital do país 2 (LinearMap: n = 6)
        var e = Assert.Single(w.Chronicle);
        Assert.Equal("capital", e.Kind);
        Assert.Equal(6, e.RegionId);
    }

    [Fact]
    public void LightEventsAreLeftOut_AndTheRuleSaysWhich()
    {
        var w = Setup();
        w.Events.Publish(new SeasonChanged("inverno", "Inverno", w.Clock.Day));
        Assert.Empty(w.Chronicle);                          // peso 1 < chronicle_min_weight

        w.Rules["chronicle_min_weight"] = 1f;
        w.Events.Publish(new SeasonChanged("verao", "Verão", w.Clock.Day));
        Assert.Equal("estacao", Assert.Single(w.Chronicle).Kind);
    }

    [Fact]
    public void AnUnknownKind_IsNeverWritten()
    {
        var w = Setup();
        Assert.Null(ChronicleSystem.Write(w, "gato", "um gato passou", 1));
        Assert.Empty(w.Chronicle);
    }

    [Fact]
    public void TheOldestEntriesFallWhenTheBookIsFull()
    {
        var w = Setup();
        w.Rules["chronicle_max"] = 3f;
        for (int i = 0; i < 6; i++) ChronicleSystem.Write(w, "guerra", "entrada " + i, 1);

        Assert.Equal(3, w.Chronicle.Count);
        Assert.Equal("entrada 3", w.Chronicle[0].Text);
        Assert.Equal("entrada 5", w.Chronicle[^1].Text);
    }

    [Fact]
    public void TheSameWorldNeverSubscribesTwice()
    {
        var w = Setup();
        w.Tick(); w.Tick();
        w.Events.Publish(new WarDeclared(1, 2));
        Assert.Single(w.Chronicle);
    }

    [Fact]
    public void ADivisionWithANewName_MakesHistory()
    {
        var w = Setup();
        w.Events.Publish(new DivisionHonoured(1, 1, "ferro", "Punhos de Ferro de R1"));

        var e = Assert.Single(w.Chronicle);
        Assert.Equal("honra", e.Kind);
        Assert.Contains("Punhos de Ferro de R1", e.Text);
    }

    [Fact]
    public void TheChronicleSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ChronicleSystem());
        w.Tick();
        w.Events.Publish(new WarDeclared(1, 2));
        TestWorld.Days(w, 10);
        w.Events.Publish(new CountryCapitulated(2, 1));
        Assert.Equal(2, w.Chronicle.Count);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(w.Chronicle.Select(e => (e.Day, e.Kind, e.Text)), w2.Chronicle.Select(e => (e.Day, e.Kind, e.Text)));
    }

    [Fact]
    public void SavingTwiceDoesNotDuplicateTheChronicle()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        ChronicleSystem.Write(w, "guerra", "uma guerra qualquer", 1);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Single(w2.Chronicle);
    }
}
