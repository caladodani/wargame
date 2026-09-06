using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Modos de mapa à maneira do HoI4: em vez de um mapa só — quem manda onde — o mesmo território
/// pintado pela conta que interessa naquele momento. O jogo já tinha os números todos (abastecimento das
/// divisões, resistência da terra ocupada, edifícios, população) mas espalhados por fichas de região: para
/// saber onde a tropa bebia areia era preciso abrir província a província.
///
/// Os modos vêm da tabela `map_mode` (nome, ícone, métrica e as duas pontas da legenda) — acrescentar um
/// modo é uma linha de SQL. O que cada métrica conta está aqui, e o nevoeiro vale como no resto: o
/// abastecimento alheio só se vê onde temos olhos.
///
/// Estado derivado: não guarda nada, não entra no save e não é ISystem.</summary>
public static class MapModes
{
    /// <summary>O modo de sempre: cada região na cor de quem a controla.</summary>
    public const string Political = "politico";

    /// <summary>Os modos disponíveis, pela ordem da base de dados (o político primeiro).</summary>
    public static IReadOnlyList<MapModeDef> All(World w) =>
        w.MapModeDefs.Values.OrderBy(m => m.Sort).ThenBy(m => m.Id).ToList();

    /// <summary>Valor de 0 a 1 por região, para o mapa pintar. Uma região que não esteja no dicionário é
    /// uma região sem resposta: não se sabe (nevoeiro) ou não se aplica (resistência em terra livre).
    /// O modo político não pinta nada — cada país fica com a sua cor.</summary>
    public static Dictionary<int, float> Shades(World w, int viewerId, string metric)
    {
        var raw = new Dictionary<int, float>();
        if (metric is "owner" or "") return raw;

        foreach (var r in w.Regions.Values)
            if (Value(w, viewerId, r, metric) is float v) raw[r.Id] = v;
        if (raw.Count == 0) return raw;

        float hi = raw.Values.Max(), lo = MathF.Min(0f, raw.Values.Min());
        float span = MathF.Max(1e-4f, hi - lo);
        foreach (var id in raw.Keys.ToList()) raw[id] = Math.Clamp((raw[id] - lo) / span, 0f, 1f);
        return raw;
    }

    /// <summary>O número cru de uma região nesta métrica; null quando não há resposta.</summary>
    public static float? Value(World w, int viewerId, Region r, string metric) => metric switch
    {
        // Abastecimento: a média das divisões que lá estão, e só as que temos como ver.
        "supply" => Supply(w, viewerId, r),
        // Resistência: só faz sentido em terra ocupada; a que é do dono não tem revolta nenhuma.
        "resistance" => r.ControllerId == r.OwnerId ? null : r.Resistance,
        // Indústria: os edifícios levantados mais a infraestrutura da região.
        "industry" => r.Buildings.Values.Sum() + r.Infrastructure,
        "population" => r.Population / 1e6f,
        _ => null,
    };

    /// <summary>A frase que a ficha da região põe quando o mapa está num modo destes.</summary>
    public static string Text(World w, int viewerId, Region r, string metric)
    {
        if (Value(w, viewerId, r, metric) is not float v)
            return metric switch
            {
                "supply" => "sem tropa nossa à vista",
                "resistance" => "terra do próprio dono: sem resistência",
                _ => "",
            };
        return metric switch
        {
            "supply" => $"abastecimento {v:P0}",
            "resistance" => $"resistência {v:P0}",
            "industry" => $"indústria {v:0.00} (edifícios + infra)",
            "population" => $"{v:0.0} M habitantes",
            _ => "",
        };
    }

    private static float? Supply(World w, int viewerId, Region r)
    {
        var divs = viewerId > 0 ? Vision.DivisionsIn(w, viewerId, r).ToList()
                                : r.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>().ToList();
        if (divs.Count == 0) return null;
        return divs.Average(d => d.Supply);
    }
}
