using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>CreateTemplateCommand (desenhador HoI4): validação, produção com o template novo e save.</summary>
public class TemplateDesignerTests
{
    [Fact]
    public void Validate_RejectsBadInput()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var ok = new List<(int, int)> { (1, 6), (4, 2) };
        Assert.NotNull(new CreateTemplateCommand(1, "", ok).Validate(w));
        Assert.NotNull(new CreateTemplateCommand(1, "X", new List<(int, int)>()).Validate(w));
        Assert.NotNull(new CreateTemplateCommand(1, "X", new List<(int, int)> { (1, 3), (1, 3) }).Validate(w));
        Assert.NotNull(new CreateTemplateCommand(1, "X", new List<(int, int)> { (1, 61) }).Validate(w));
        Assert.NotNull(new CreateTemplateCommand(1, "X", new List<(int, int)> { (9999, 1) }).Validate(w));
        Assert.Null(new CreateTemplateCommand(1, "Montanha PT", ok).Validate(w));
    }

    [Fact]
    public void Create_AppearsInTemplates_AndBuilds()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var cmd = new CreateTemplateCommand(1, "Pesada", new List<(int, int)> { (2, 4), (3, 4) });
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        int id = Assert.Single(w.CustomTemplateIds);
        Assert.Equal(World.CustomTemplateBase, id);
        Assert.Contains(w.Units.GetTemplates(1), t => t.Id == id && t.Name == "Pesada");
        Assert.True(w.TemplateCost(id) > 0f);
        Assert.True(w.Stats.Get(id)["soft_atk"] > 0f);
        Assert.Null(new BuildDivisionCommand(1, id).Validate(w));
        Assert.NotNull(new BuildDivisionCommand(2, id).Validate(w));   // template de outro país
    }

    [Fact]
    public void SaveRoundTrip_RestoresCustomTemplates()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true;
        new CreateTemplateCommand(1, "Pesada", new List<(int, int)> { (2, 4) }).Execute(w);

        string schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);
        repo.WriteSave(w, save);   // segundo save não pode duplicar PKs

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        int id = Assert.Single(w2.CustomTemplateIds);
        var t = w2.Units.GetTemplate(id);
        Assert.Equal("Pesada", t.Name);
        Assert.Equal(new[] { (2, 4) }, t.Units);
    }
}
