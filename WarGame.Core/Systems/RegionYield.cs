using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma parcela do que uma região dá: a chapa que a mostra, o número como se lê, o nome e a frase
/// que diz de onde vem aquele número.</summary>
public readonly record struct YieldPart(string Glyph, string Value, string Name, string Note);

/// <summary>O que esta região dá a quem a controla: dinheiro por dia, homens por dia, recursos e obras.
///
/// Os números existiam todos e liam-se todos no mesmo sítio — um parágrafo corrido de emojis e pontos no
/// painel da região, onde o rendimento aparecia como "💰 0,42/dia" sem uma palavra sobre de onde vinha. No
/// HoI4 a ficha do estado é uma coluna de linhas com o seu próprio ícone e a sua própria explicação, e é
/// por ela que se decide o que se ocupa, o que se constrói e o que se deixa.
///
/// Como no Breakdown e no GroundSystem, aqui não se faz conta nova nenhuma: o dinheiro é o
/// EconomySystem.RegionIncome e os homens são a mesma parcela que o ManpowerSystem soma ao pool.</summary>
public static class RegionYield
{
    /// <summary>Homens por dia que esta região mete no pool de quem a controla — a parcela desta região na
    /// soma do ManpowerSystem. O tecto do pool é do país e não da região: uma região continua a dar isto
    /// todos os dias, mas o pool pára quando enche.</summary>
    public static float MenPerDay(World w, Region r)
    {
        if (!w.Countries.TryGetValue(r.ControllerId, out var c) || c.Capitulated) return 0f;
        return r.Population * OccupationSystem.ManpowerMult(w, r) / 1e6f
             * w.Rule("manpower_per_million_daily", 60f) * c.Stat("conscription") * c.StabilityFactor;
    }

    /// <summary>De onde vem o rendimento desta região, parcela a parcela — os mesmos multiplicadores que o
    /// EconomySystem aplica, pela ordem por que os aplica.</summary>
    public static string MoneyNote(World w, Region r)
    {
        var c = w.Countries.GetValueOrDefault(r.ControllerId);
        var bits = new List<string>
        {
            $"· {r.Population / 1e6f:0.0} M hab. × {w.Rule("points_per_million", 0.1f):0.##} por milhão",
            $"· estrada ×{r.Infrastructure:0.00}",
        };
        float terreno = EconomySystem.TerrainMult(w, r);
        if (MathF.Abs(terreno - 1f) >= 0.005f)
            bits.Add($"· chão e costa ×{terreno:0.00}");
        if (r.ControllerId != r.OwnerId)
        {
            bits.Add($"· terra ocupada ×{w.Rule("occupied_yield", 0.5f):0.00} × política ×{OccupationSystem.YieldMult(w, r):0.00}");
            if (r.Resistance > 0.005f)
                bits.Add($"· resistência {r.Resistance:P0} tira ×{1f - r.Resistance * w.Rule("resistance_output_hit", 0.5f):0.00}");
            if (c is not null) bits.Add($"· leis de ocupação ×{c.Stat("occupied_yield"):0.00}");
        }
        if (c is not null) bits.Add($"· indústria ×{c.Stat("industry"):0.00} × estabilidade ×{c.StabilityFactor:0.00}");
        return string.Join("\n", bits);
    }

    /// <summary>A ficha: uma linha por coisa que esta região dá. O dinheiro e os homens estão sempre lá
    /// (mesmo a zero, que uma região sem gente é informação); recursos e obras só quando existem.</summary>
    public static List<YieldPart> Parts(World w, Region r)
    {
        var parts = new List<YieldPart>
        {
            new("cofre", $"{EconomySystem.RegionIncome(w, r):0.00}", "por dia",
                "O que esta terra rende por dia a quem a controla:\n" + MoneyNote(w, r)),
            new("gente", Men(MenPerDay(w, r)), "homens/dia",
                $"População: {r.Population / 1e6f:0.0} M\n"
              + $"· recruta-se {w.Rule("manpower_per_million_daily", 60f):0.#} homens por milhão por dia"
              + (r.ControllerId != r.OwnerId ? $"\n· terra ocupada: só ×{OccupationSystem.ManpowerMult(w, r):0.00} da gente daqui se recruta" : "")
              + "\nO pool tem tecto de país: cheio, esta terra continua a dar e o pool é que não sobe."),
        };
        foreach (var (res, amount) in r.Resources.OrderBy(kv => kv.Key))
        {
            if (!w.ResourceDefs.TryGetValue(res, out var rd)) continue;
            string nota = $"Cada unidade vale +{rd.PerUnit:P0} de {rd.StatKey} ao país, até {rd.Cap:0} unidades.";
            if (rd.FuelPerUnit > 0f) nota += $"\nRefina {rd.FuelPerUnit:0.#} de combustível por dia por unidade.";
            parts.Add(new YieldPart(rd.Glyph, $"{amount:0}", rd.Name.ToLowerInvariant(), nota));
        }
        foreach (var (bid, lvl) in r.Buildings.OrderBy(kv => kv.Key))
        {
            if (lvl <= 0 || !w.BuildingDefs.TryGetValue(bid, out var bd)) continue;
            parts.Add(new YieldPart(bd.Glyph, $"{lvl}", bd.Name.ToLowerInvariant(),
                $"{bd.Name} nível {lvl}" + (bd.StatKey.Length > 0 ? $"\n+{bd.PerLevel:P0} de {bd.StatKey} por nível" : "")));
        }
        return parts;
    }

    /// <summary>Homens como se dizem: aos milhares até ao milhão, e daí para cima em milhões.</summary>
    private static string Men(float m) => m >= 1e6f ? $"{m / 1e6f:0.0} M" : m >= 1000f ? $"{m / 1000f:0.0} k" : $"{m:0}";
}
