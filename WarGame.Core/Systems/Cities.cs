using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>As cidades escritas no mapa. Um mapa de guerra sem nomes de terra é um mapa de fronteiras: no
/// HoI4 lê-se Varsóvia, Kiev, Estalinegrado antes de se saber que província é aquela, e é assim que se
/// decide para onde vai a seta. Aqui o mapa só tinha o nome da província e o do país — a terra era anónima.
///
/// A regra é a do atlas: mostra-se o que cabe na escala a que se está a olhar. De longe, as capitais e as
/// metrópoles; de perto, a terra pequena também. Uma conta só, monótona no zoom — a cidade que já se via não
/// desaparece por se aproximar mais.
///
/// As cidades vêm da tabela city (Natural Earth, domínio público) e não entram na simulação: quem tem contas
/// é a região. Estado derivado: não guarda nada, não entra no save e não é ISystem.</summary>
public static class Cities
{
    /// <summary>A gente mínima para uma cidade caber neste zoom. Quanto mais perto, mais pequena a terra que
    /// cabe no mapa.</summary>
    public static float Cut(World w, float zoom) =>
        w.Rule("city_pop_cut", 400_000f) / MathF.Max(0.05f, zoom);

    /// <summary>Esta cidade vê-se neste zoom? Uma capital vê-se por pequena que seja, assim que o mapa deixa
    /// de ser o planeta inteiro — é o ponto que se procura primeiro.</summary>
    public static bool Shows(World w, CityDef c, float zoom) =>
        c.Population >= Cut(w, zoom) || (c.Capital && zoom >= w.Rule("city_capital_zoom", 0.25f));

    /// <summary>E leva o nome escrito? Pontos podem ser muitos; nomes a mais tapam o mapa, por isso só a
    /// cidade que é bem maior do que o corte é que se identifica — e a capital, sempre.</summary>
    public static bool Named(World w, CityDef c, float zoom) =>
        Shows(w, c, zoom)
        && (c.Capital || c.Population >= Cut(w, zoom) * MathF.Max(1f, w.Rule("city_name_factor", 4f)));

    /// <summary>As cidades a desenhar neste zoom: capitais primeiro, depois as maiores, com tecto — um mapa
    /// com três mil pontos não se lê e não se desenha depressa.</summary>
    public static List<CityDef> Shown(World w, float zoom)
    {
        int max = (int)MathF.Max(1f, w.Rule("city_draw_max", 900f));
        var shown = w.Cities.Where(c => Shows(w, c, zoom)).ToList();
        if (shown.Count <= max) return shown;
        return shown.OrderByDescending(c => c.Capital).ThenByDescending(c => c.Population).Take(max).ToList();
    }

    /// <summary>As cidades de uma região, da maior para a menor.</summary>
    public static List<CityDef> In(World w, int regionId) =>
        w.Cities.Where(c => c.RegionId == regionId).OrderByDescending(c => c.Population).ToList();

    /// <summary>Gente à maneira de quem lê um mapa: "2,8 M", "740 mil", "9 mil".</summary>
    public static string Size(int pop) =>
        pop >= 1_000_000 ? $"{pop / 1e6f:0.0} M" : pop >= 1_000 ? $"{pop / 1000f:0} mil" : $"{pop}";

    /// <summary>A linha da ficha da região: as terras que lá estão, a maior à frente.</summary>
    public static string Line(World w, Region r)
    {
        var here = In(w, r.Id);
        if (here.Count == 0) return "sem cidade no mapa";
        var top = here.Take(3).Select(c => $"{c.Name} {Size(c.Population)}{(c.Capital ? " ★" : "")}");
        return string.Join(", ", top) + (here.Count > 3 ? $" (+{here.Count - 3})" : "");
    }
}
