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

    /// <summary>Uma classe de um mapa que pinta por classe e não por escala: a cor sai da tabela, o nome e
    /// o desenho vão para a legenda. É o que o terreno precisa — montanha não é "mais" do que planície, é
    /// outra coisa, e um degradê quente-frio mentia sobre isso.</summary>
    public readonly record struct MapClass(string Id, string Name, string Color, string Glyph, string Note);

    /// <summary>Este modo pinta por classe (cor de tabela) em vez de escala?</summary>
    public static bool ByClass(string metric) => metric is "terrain" or "zone";

    /// <summary>A classe desta região neste modo, ou null quando não há resposta.</summary>
    public static MapClass? Of(World w, Region r, string metric) => metric switch
    {
        "terrain" => w.TerrainDefs.TryGetValue(r.Terrain, out var t)
            ? new MapClass(t.Id, t.Name, t.Color, t.Glyph, $"passo ×{t.MoveCost:0.00}")
            : null,
        // zonas estratégicas: a cor é da zona de céu (é nela que a aviação se bate) e a nota diz o mar a
        // que aquela costa pertence — as duas divisões do mundo que o jogo passou a ter
        "zone" => w.Zones.TryGetValue(r.ZoneId, out var z)
            ? new MapClass(z.Id, z.Name, z.Color, z.Glyph,
                           r.SeaZoneId.Length > 0 ? Zones.Name(w, r.SeaZoneId) : "sem mar")
            : null,
        _ => null,
    };

    /// <summary>A legenda de um modo por classe: as classes que o mundo tem mesmo, da mais fácil de
    /// atravessar para a mais dura — que é a ordem por que se lê um mapa de terreno.</summary>
    public static List<MapClass> Key(World w, string metric)
    {
        var key = new List<MapClass>();
        if (metric == "zone")
        {
            // são dezenas: mostram-se as maiores, que é o que cabe no ecrã de um telemóvel (map_key_max)
            var count = new Dictionary<string, int>();
            foreach (var r in w.Regions.Values)
                if (r.ZoneId.Length > 0) count[r.ZoneId] = count.GetValueOrDefault(r.ZoneId) + 1;
            foreach (var (id, n) in count.OrderByDescending(p => p.Value).ThenBy(p => p.Key)
                                        .Take((int)w.Rule("map_key_max", 12f)))
                if (w.Zones.TryGetValue(id, out var z))
                    key.Add(new MapClass(z.Id, z.Name, z.Color, z.Glyph, $"{n} regiões"));
            return key;
        }
        if (metric != "terrain") return key;
        var used = new HashSet<string>(w.Regions.Values.Select(r => r.Terrain));
        foreach (var t in w.TerrainDefs.Values.Where(t => used.Contains(t.Id)).OrderBy(t => t.MoveCost).ThenBy(t => t.Id))
            key.Add(new MapClass(t.Id, t.Name, t.Color, t.Glyph, $"passo ×{t.MoveCost:0.00}"));
        return key;
    }

    /// <summary>Os modos disponíveis, pela ordem da base de dados (o político primeiro).</summary>
    public static IReadOnlyList<MapModeDef> All(World w) =>
        w.MapModeDefs.Values.OrderBy(m => m.Sort).ThenBy(m => m.Id).ToList();

    /// <summary>Valor de 0 a 1 por região, para o mapa pintar. Uma região que não esteja no dicionário é
    /// uma região sem resposta: não se sabe (nevoeiro) ou não se aplica (resistência em terra livre).
    /// O modo político não pinta nada — cada país fica com a sua cor.</summary>
    public static Dictionary<int, float> Shades(World w, int viewerId, string metric)
    {
        var raw = new Dictionary<int, float>();
        if (metric is "owner" or "" || ByClass(metric)) return raw;   // pintam por classe: não há escala nenhuma

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
        // Tempo: pinta-se a severidade do céu (0 = limpo), que é o que interessa a quem vai atacar amanhã.
        // Sem tabela de tempo carregada não há resposta nenhuma, e o modo fica cinzento como qualquer outro.
        "weather" => Weather.Of(w, r) is WeatherDef sky ? 1f - sky.MoveMult : null,
        // Vassalagem: pinta-se a terra de quem obedece a outro, tanto mais escura quanto menos autonomia
        // lhe resta. Um país livre não tem resposta nenhuma — e é assim que se vê de relance onde acaba o
        // império de alguém e começa gente que ainda manda em si.
        "subject" => Subject(w, r),
        // Pontos de vitória: o que a região vale na conta da guerra. Terra que não chega a grau nenhum não
        // tem resposta — o mapa fica com as praças acesas e o resto apagado, que é como se lê uma frente.
        "victory" => VictoryPoints.Of(w, r) is int vp && vp > 0 ? vp : null,
        _ => null,
    };

    /// <summary>A frase que a ficha da região põe quando o mapa está num modo destes.</summary>
    public static string Text(World w, int viewerId, Region r, string metric)
    {
        if (ByClass(metric))
            return Of(w, r, metric) is MapClass k ? $"{k.Name} · {k.Note}" : "chão por classificar";
        if (Value(w, viewerId, r, metric) is not float v)
            return metric switch
            {
                "supply" => "sem tropa nossa à vista",
                "resistance" => "terra do próprio dono: sem resistência",
                "weather" => "sem tempo carregado",
                "subject" => "país livre",
                "victory" => "não conta pontos de vitória",
                _ => "",
            };
        // {v*100:0}% em vez de {v:P0}: o formato P depende da cultura do sistema (o padrão invariant, o que
        // corre num runner de CI sem locale definida, mete um espaço antes do "%" que a en-US não mete) —
        // sem casas decimais o "0" é só dígitos, sem separador nenhum a variar.
        return metric switch
        {
            "weather" => Weather.Line(w, r),
            "subject" => w.Countries.TryGetValue(r.OwnerId, out var owner) ? Subjects.Line(w, owner) : "",
            "victory" => VictoryPoints.Line(w, r),
            "supply" => $"abastecimento {v * 100:0}%",
            "resistance" => $"resistência {v * 100:0}%",
            "industry" => $"indústria {v:0.00} (edifícios + infra)",
            "population" => $"{v:0.0} M habitantes",
            _ => "",
        };
    }

    /// <summary>Quanto deste chão é de facto de outro: 1 quando o dono acaba de cair, 0 na véspera de se
    /// levantar. Sem tabela de vassalagem carregada não há resposta nenhuma.</summary>
    private static float? Subject(World w, Region r)
    {
        if (w.SubjectTypeDefs.Count == 0) return null;
        if (!w.Countries.TryGetValue(r.OwnerId, out var owner) || !owner.IsSubject) return null;
        float free = MathF.Max(1e-4f, w.Rule("subject_free_autonomy", 1f));
        return Math.Clamp(1f - owner.Autonomy / free, 0f, 1f);
    }

    private static float? Supply(World w, int viewerId, Region r)
    {
        var divs = viewerId > 0 ? Vision.DivisionsIn(w, viewerId, r).ToList()
                                : r.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>().ToList();
        if (divs.Count == 0) return null;
        return divs.Average(d => d.Supply);
    }
}
