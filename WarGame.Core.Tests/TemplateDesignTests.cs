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











}
