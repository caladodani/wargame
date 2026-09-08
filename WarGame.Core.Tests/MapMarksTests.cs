using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A mobília do mapa: que praças levam chapa a cada escala, que forma leva cada grau e de que cor
/// fica cada uma aos olhos de quem está a jogar. É o verde/cinzento/vermelho do mapa do HoI4.</summary>
public class MapMarksTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["fog_of_war"] = 0f;          // o nevoeiro tem prova própria mais abaixo
        return w;
    }

    /// <summary>A forma da chapa vem da linha victory_tier e não de um switch em C#: capital estrela,
    /// metrópole pentágono, cidade quadrado, praça círculo.</summary>
    [Fact]
    public void A_forma_da_chapa_vem_da_tabela()
    {
        var w = Setup();
        Assert.Equal("estrela", w.VictoryTiers["capital"].Shape);
        Assert.Equal("pentagono", w.VictoryTiers["metropole"].Shape);
        Assert.Equal("quadrado", w.VictoryTiers["cidade"].Shape);
        Assert.Equal("circulo", w.VictoryTiers["praca"].Shape);
    }

    /// <summary>A cor é a relação de agora: verde a nossa, vermelha a de quem está em guerra connosco,
    /// cinzenta a de terceiros. Declarar guerra muda a cor da chapa no dia em que é declarada.</summary>
    [Fact]
    public void A_cor_da_chapa_e_a_relacao_de_agora()
    {
        var w = Setup();
        Assert.Equal(1, MapMarks.Side(w, 1, 1));
        Assert.Equal(0, MapMarks.Side(w, 1, 2));
        w.StartWar(1, 2);
        Assert.Equal(-1, MapMarks.Side(w, 1, 2));
    }

    /// <summary>Ao longe só as praças que mais valem; ao aproximar entram as que valem menos. O corte
    /// nunca sobe com o zoom — aproximar não apaga chapa nenhuma.</summary>
    [Fact]
    public void Aproximar_so_acrescenta_chapas()
    {
        var w = Setup();
        int antes = int.MaxValue;
        for (float zoom = 0.2f; zoom < 8f; zoom *= 1.5f)
        {
            int cut = MapMarks.Cut(w, zoom);
            Assert.True(cut <= antes, $"o corte subiu ao aproximar: {antes} → {cut}");
            antes = cut;
        }
        Assert.Equal(1, MapMarks.Cut(w, 2f));
    }

    /// <summary>A capital vê-se sempre que as chapas estão acesas, por pouca gente que lá viva — é o ponto
    /// que se procura primeiro num mapa de guerra. Abaixo do limiar não se vê chapa nenhuma.</summary>
    [Fact]
    public void A_capital_ve_se_sempre_que_as_chapas_acendem()
    {
        var w = Setup();
        var capital = w.Regions[1];
        Assert.True(MapMarks.IsCapital(w, capital));
        Assert.True(MapMarks.Shows(w, capital, 0.2f));
        Assert.False(MapMarks.Shows(w, capital, 0.05f));       // o planeta inteiro não leva mobília
        Assert.Empty(MapMarks.Prizes(w, 1, 0.05f));
    }

    /// <summary>O tecto corta pelo fim, e o fim são as praças que menos valem: capitais e metrópoles ficam.</summary>
    [Fact]
    public void O_tecto_guarda_as_pracas_que_mais_valem()
    {
        var w = Setup();
        w.Rules["mark_draw_max"] = 2f;
        var shown = MapMarks.Prizes(w, 1, 4f);
        Assert.Equal(2, shown.Count);
        Assert.All(shown, p => Assert.True(p.Capital));
        Assert.True(shown[0].Points >= shown[1].Points);
    }

    /// <summary>Praça no nevoeiro sai cinzenta: quem não vê a terra não sabe de quem ela é hoje.</summary>
    [Fact]
    public void O_nevoeiro_apaga_a_cor_da_chapa()
    {
        var w = Setup();
        w.Rules["fog_of_war"] = 1f;
        w.StartWar(1, 2);
        var enemyCapital = MapMarks.All(w, 1).First(p => p.RegionId == 6);
        Assert.False(enemyCapital.Seen);
        Assert.Equal(0, enemyCapital.Side);                    // cinzenta, não vermelha
        Assert.True(MapMarks.All(w, 1).First(p => p.RegionId == 1).Side > 0);
    }

    /// <summary>As obras que se veem no mapa: o porto leva a âncora que está na linha dele, o forte leva o
    /// muro com o nível. Obra em terra por descobrir não se desenha.</summary>
    [Fact]
    public void As_obras_levam_o_desenho_da_linha_delas()
    {
        var w = Setup();
        w.Regions[2].Buildings["porto"] = 2;
        w.Regions[3].Fort = 3;
        var works = MapMarks.Works(w, 1);
        Assert.Contains(works, k => k.RegionId == 2 && k.Glyph == "ancora" && k.Level == 2);
        Assert.Contains(works, k => k.RegionId == 3 && k.Glyph == "muro" && k.Level == 3);

        w.Rules["fog_of_war"] = 1f;
        w.Regions[5].Fort = 2;                                 // terra do outro, por descobrir
        Assert.DoesNotContain(MapMarks.Works(w, 1), k => k.RegionId == 5);
    }
}
