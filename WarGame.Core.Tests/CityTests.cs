using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>As cidades escritas no mapa: quem cabe em cada escala, quem leva nome e o que a ficha da região
/// diz. A regra do atlas — aproximar mostra mais terra, nunca menos.</summary>
public class CityTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Cities.Add(new CityDef(1, 1, "Metrópole", 8_000_000, false, 0f, 0f));
        w.Cities.Add(new CityDef(2, 1, "Vila Pequena", 30_000, false, 10f, 0f));
        w.Cities.Add(new CityDef(3, 2, "Capital Pobre", 40_000, true, 20f, 0f));
        w.Cities.Add(new CityDef(4, 1, "Cidade Média", 500_000, false, 30f, 0f));
        return w;
    }

    /// <summary>De longe cabe a metrópole; a vila só aparece quando se chega perto.</summary>
    [Fact]
    public void So_cabe_no_mapa_quem_cabe_na_escala()
    {
        var w = Setup();
        var far = Cities.Shown(w, 0.3f).Select(c => c.Name).ToList();
        Assert.Contains("Metrópole", far);
        Assert.DoesNotContain("Vila Pequena", far);

        var near = Cities.Shown(w, 20f).Select(c => c.Name).ToList();
        Assert.Contains("Vila Pequena", near);
    }

    /// <summary>Aproximar nunca tira do mapa uma cidade que já lá estava.</summary>
    [Fact]
    public void Aproximar_nunca_apaga_o_que_ja_se_via()
    {
        var w = Setup();
        for (float zoom = 0.3f; zoom < 8f; zoom *= 1.5f)
        {
            var here = Cities.Shown(w, zoom).Select(c => c.Id).ToHashSet();
            var closer = Cities.Shown(w, zoom * 1.5f).Select(c => c.Id).ToHashSet();
            Assert.Subset(closer, here);
        }
    }

    /// <summary>A capital vê-se por pequena que seja, assim que o mapa deixa de ser o planeta — e leva sempre
    /// o nome escrito, que é o ponto que se procura primeiro.</summary>
    [Fact]
    public void A_capital_ve_se_por_pequena_que_seja()
    {
        var w = Setup();
        var cap = w.Cities.First(c => c.Capital);
        Assert.True(Cities.Shows(w, cap, 0.3f));
        Assert.True(Cities.Named(w, cap, 0.3f));
        Assert.False(Cities.Shows(w, cap, 0.1f));            // planeta inteiro: nem as capitais
    }

    /// <summary>Nomes são menos do que pontos: só a terra bem maior do que o corte é que se identifica.</summary>
    [Fact]
    public void Ha_menos_nomes_do_que_pontos()
    {
        var w = Setup();
        var shown = Cities.Shown(w, 1f);
        int named = shown.Count(c => Cities.Named(w, c, 1f));
        Assert.Contains(shown, c => c.Name == "Cidade Média");     // ponto sim...
        Assert.False(Cities.Named(w, shown.First(c => c.Name == "Cidade Média"), 1f));   // ...nome não
        Assert.True(named < shown.Count);
        Assert.All(shown.Where(c => Cities.Named(w, c, 1f)), c => Assert.True(Cities.Shows(w, c, 1f)));
    }

    /// <summary>O tecto de cidades desenhadas guarda as capitais e as maiores.</summary>
    [Fact]
    public void O_tecto_guarda_as_capitais_e_as_maiores()
    {
        var w = Setup();
        w.Rules["city_draw_max"] = 2f;
        var shown = Cities.Shown(w, 20f);
        Assert.Equal(2, shown.Count);
        Assert.Contains(shown, c => c.Capital);
        Assert.Contains(shown, c => c.Name == "Metrópole");
    }

    /// <summary>A ficha da região diz as terras que lá estão, a maior à frente.</summary>
    [Fact]
    public void A_ficha_diz_as_terras_da_regiao()
    {
        var w = Setup();
        string line = Cities.Line(w, w.Regions[1]);
        Assert.StartsWith("Metrópole 8,0 M", line.Replace(".", ","));
        Assert.Contains("Cidade Média", line);
        Assert.Equal("sem cidade no mapa", Cities.Line(w, w.Regions[3]));
        Assert.Contains("★", Cities.Line(w, w.Regions[2]));
    }
}
