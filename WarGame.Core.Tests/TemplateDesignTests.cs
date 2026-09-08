using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A prancheta do estado-maior (TemplateDesign): o que um desenho dá antes de existir, o que a
/// margem tem a dizer sobre ele, e redesenhar um modelo já feito sem trocar de modelo. Os tipos de unidade
/// são os do mundo de teste: 1 infantaria (linha), 4 artilharia (apoio), 3 blindados (linha).</summary>
public class TemplateDesignTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return w;
    }

    [Fact]
    public void APranchetaContaBatalhoesDeLinhaEDeApoioEmSeparado()
    {
        var w = Build();
        var s = TemplateDesign.Of(w, 1, new List<(int, int)> { (1, 6), (4, 2) });
        Assert.Equal(6, s.Line);
        Assert.Equal(2, s.Support);
        Assert.Equal(8, s.Battalions);
    }

    [Fact]
    public void OsNumerosDaPranchetaSaoOsMesmosQueOCombateVaiLer()
    {
        var w = Build();
        var units = new List<(int, int)> { (1, 6), (4, 2) };
        var s = TemplateDesign.Of(w, 1, units);
        new CreateTemplateCommand(1, "Prova", units).Execute(w);
        int id = w.CustomTemplateIds[^1];
        var real = w.Stats.Get(id);
        Assert.Equal(real["soft_atk"], s.Stats["soft_atk"], 3);
        Assert.Equal(real["defense"], s.Stats["defense"], 3);
        Assert.Equal(w.TemplateCost(id), s.Cost, 3);
    }

    [Fact]
    public void ADivisaoAndaAVelocidadeDoBatalhaoMaisLento()
    {
        var w = Build();
        var s = TemplateDesign.Of(w, 1, new List<(int, int)> { (1, 4), (3, 2) });
        float slowest = MathF.Min(w.Units.GetUnitType(1).Mobility, w.Units.GetUnitType(3).Mobility);
        Assert.Equal(slowest, s.Speed, 3);
        Assert.NotEqual("", s.Slowest);
    }

    [Fact]
    public void UmaPranchetaVaziaDizQueEstaVaziaENaoRebenta()
    {
        var w = Build();
        var s = TemplateDesign.Of(w, 1, new List<(int, int)>());
        Assert.Equal(0, s.Battalions);
        Assert.Equal("prancheta vazia", s.Role);
        Assert.True(s.Bad);
    }

    [Fact]
    public void SoApoiosNaoEhDivisaoNenhuma()
    {
        var w = Build();
        var s = TemplateDesign.Of(w, 1, new List<(int, int)> { (4, 2) });
        Assert.Equal(0, s.Line);
        Assert.Contains("só apoios", s.Role);
        Assert.Contains(s.Notes, n => n.Bad && n.Text.Contains("batalhões de linha"));
    }

    [Fact]
    public void ODesenhoSemApoiosLevaReparoENaoEhGrave()
    {
        var w = Build();
        var s = TemplateDesign.Of(w, 1, new List<(int, int)> { (1, 6) });
        var note = Assert.Single(s.Notes, n => n.Text.Contains("sem companhias de apoio"));
        Assert.False(note.Bad);
    }

    [Fact]
    public void OPapelDaDivisaoSaiDosNumerosENaoDoNome()
    {
        var w = Build();
        var infantaria = TemplateDesign.Of(w, 1, new List<(int, int)> { (1, 8) });
        var blindada = TemplateDesign.Of(w, 1, new List<(int, int)> { (3, 8) });
        Assert.NotEqual(infantaria.Role, blindada.Role);
        Assert.Equal("punho blindado", blindada.Role);
    }

    [Fact]
    public void OTectoDeLinhaEDeApoioSaoRegrasEOComandoRecusaOQuePassa()
    {
        var w = Build();
        int lineMax = TemplateDesign.LineMax(w), supMax = TemplateDesign.SupportMax(w);
        Assert.True(lineMax > 0 && supMax > 0);
        Assert.NotNull(new CreateTemplateCommand(1, "Gorda", new List<(int, int)> { (1, 30), (3, 30) }).Validate(w));
        Assert.NotNull(new CreateTemplateCommand(1, "Apoios", new List<(int, int)> { (1, 2), (4, supMax + 1) }).Validate(w));
        Assert.Null(new CreateTemplateCommand(1, "Certa", new List<(int, int)> { (1, 6), (4, 2) }).Validate(w));
    }

    [Fact]
    public void RedesenharMudaAFichaSemTrocarDeModelo()
    {
        var w = Build();
        new CreateTemplateCommand(1, "Linha", new List<(int, int)> { (1, 4) }).Execute(w);
        int id = w.CustomTemplateIds[^1];
        float before = w.Stats.Get(id)["defense"];

        var edit = new EditTemplateCommand(1, id, "Linha reforçada", new List<(int, int)> { (1, 8), (4, 1) });
        Assert.Null(edit.Validate(w));
        edit.Execute(w);

        Assert.Single(w.CustomTemplateIds);                       // não nasceu modelo nenhum ao lado
        var t = w.Units.GetTemplate(id);
        Assert.Equal("Linha reforçada", t.Name);
        Assert.True(w.Stats.Get(id)["defense"] > before);          // a ficha é outra a partir de hoje
        var mine = Assert.Single(w.Units.GetTemplates(1), x => x.Id == id);
        Assert.Equal(2, mine.Units.Count);                         // e a lista do país tem o desenho novo
    }

    [Fact]
    public void SoSeRedesenhaOQueFoiDesenhadoEmJogoEQueEhNosso()
    {
        var w = Build();
        new CreateTemplateCommand(1, "Nosso", new List<(int, int)> { (1, 4) }).Execute(w);
        int id = w.CustomTemplateIds[^1];
        var units = new List<(int, int)> { (1, 5) };
        Assert.NotNull(new EditTemplateCommand(1, 1, "Doutrina", units).Validate(w));    // modelo da tabela
        Assert.NotNull(new EditTemplateCommand(2, id, "Roubado", units).Validate(w));    // modelo de outro país
        Assert.Null(new EditTemplateCommand(1, id, "Nosso", units).Validate(w));
    }

    [Fact]
    public void ORedesenhoSobreviveAoSaveEAoLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true;
        new CreateTemplateCommand(1, "Linha", new List<(int, int)> { (1, 4) }).Execute(w);
        int id = w.CustomTemplateIds[^1];
        new EditTemplateCommand(1, id, "Linha II", new List<(int, int)> { (1, 6), (4, 2) }).Execute(w);

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        var t = w2.Units.GetTemplate(Assert.Single(w2.CustomTemplateIds));
        Assert.Equal("Linha II", t.Name);
        Assert.Equal(new[] { (1, 6), (4, 2) }, t.Units);
    }

    [Fact]
    public void APranchetaDeUmModeloQueJaExisteLeOMesmoQueODesenho()
    {
        var w = Build();
        var units = new List<(int, int)> { (1, 6), (4, 2) };
        new CreateTemplateCommand(1, "Prova", units).Execute(w);
        int id = w.CustomTemplateIds[^1];
        var a = TemplateDesign.OfTemplate(w, id);
        var b = TemplateDesign.Of(w, 1, units);
        Assert.Equal(b.Role, a.Role);
        Assert.Equal(b.Line, a.Line);
        Assert.Equal(b.Cost, a.Cost, 3);
        Assert.Contains("batalhões", TemplateDesign.Short(a));
    }

    [Fact]
    public void UmModeloInexistenteDaPranchetaVaziaEmVezDeRebentar()
    {
        var w = Build();
        var s = TemplateDesign.OfTemplate(w, 999_999);
        Assert.Equal(0, s.Battalions);
    }
}
