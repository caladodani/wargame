using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma parcela da tensão mundial: o que a puxa, quanto puxa e quem é.</summary>
/// <param name="Label">A frase que o mostrador escreve ("guerra Espanha–Portugal").</param>
/// <param name="Points">Pontos de tensão que esta parcela vale.</param>
public readonly record struct TensionPart(string Label, float Points);

/// <summary>O termómetro do mundo, de 0 a 100 — e as parcelas que o levantaram.
///
/// No HoI4 a tensão mundial é o relógio da década: enquanto o mundo está calmo, uma democracia não pode
/// mandar voluntários, não pode garantir ninguém e não pode justificar uma guerra do outro lado do mapa.
/// Cada guerra que rebenta, cada país que cai e cada justificação a decorrer sobem o número, e a certa
/// altura o que era impensável passa a ser rotina. Aqui era o buraco mais visível da política: as guerras
/// aconteciam e o mundo não dava por nada — mandar voluntários para o outro hemisfério no dia 1 custava o
/// mesmo que no dia 500.
///
/// Estado derivado: não guarda nada, não é ISystem, lê-se a cada olhada a partir do estado do mundo.
/// Cada parcela sai de uma regra (tension_*) e todas se explicam ao dedo — um número que ninguém sabe de
/// onde vem é um oráculo, e a barra deste jogo não tem oráculos.</summary>
public static class WorldTension
{
    /// <summary>A tensão de hoje, 0..100.</summary>
    public static float Of(World w) => MathF.Min(100f, Parts(w).Sum(p => p.Points));

    /// <summary>De onde vem a tensão de hoje, da parcela maior para a menor.</summary>
    public static List<TensionPart> Parts(World w)
    {
        var parts = new List<TensionPart>();
        float perWar = w.Rule("tension_per_war", 6f), perCap = w.Rule("tension_per_capitulation", 8f);
        float perJustify = w.Rule("tension_per_justify", 2f), weight = w.Rule("tension_power_weight", 2f);

        // Guerra grande pesa mais do que guerra pequena: a fatia de mundo que os beligerantes valem entra
        // no número. Duas potências às turras assustam a Terra inteira; duas ilhas não.
        float world = 0f;
        foreach (var c in w.Countries.Values) world += Weight(w, c);
        world = MathF.Max(1f, world);

        var seen = new HashSet<(int, int)>();
        foreach (var c in w.Countries.Values)
        {
            if (c.Capitulated) continue;
            foreach (int other in c.AtWarWith)
            {
                var key = World.WarKey(c.Id, other);
                if (!seen.Add(key) || !w.Countries.TryGetValue(other, out var o)) continue;
                float share = (Weight(w, c) + Weight(w, o)) / world;
                parts.Add(new TensionPart($"guerra {c.Name}–{o.Name}", perWar * (1f + share * weight)));
            }
        }

        int fallen = w.Countries.Values.Count(c => c.Capitulated);
        if (fallen > 0) parts.Add(new TensionPart($"{fallen} país{(fallen == 1 ? "" : "es")} de joelhos", perCap * fallen));

        int justifying = w.Countries.Values.Count(c => !c.Capitulated && c.JustifyTarget is not null);
        if (justifying > 0)
            parts.Add(new TensionPart($"{justifying} guerra{(justifying == 1 ? "" : "s")} a ser justificada{(justifying == 1 ? "" : "s")}",
                                      perJustify * justifying));

        return parts.OrderByDescending(p => p.Points).ThenBy(p => p.Label).ToList();
    }

    /// <summary>O peso de um país no mundo: a população que controla. É o que faz uma guerra entre
    /// potências valer mais do que uma escaramuça entre vizinhos pequenos.</summary>
    private static float Weight(World w, Country c)
    {
        float pop = 0f;
        foreach (var r in w.Regions.Values) if (r.ControllerId == c.Id) pop += r.Population;
        return pop / 1e6f;
    }

    /// <summary>A tensão chega para isto? A porta de todas as coisas que o mundo calmo não deixa fazer.</summary>
    public static bool Allows(World w, string ruleKey, float fallback)
    {
        float need = w.Rule(ruleKey, fallback);
        return need <= 0f || Of(w) >= need;
    }

    /// <summary>O termómetro em palavras — a barra de cima e a prova headless leem o mesmo.</summary>
    public static string Mood(World w)
    {
        float t = Of(w);
        return t < w.Rule("tension_calm", 15f) ? "mundo em paz"
             : t < w.Rule("tension_uneasy", 40f) ? "mundo inquieto"
             : t < w.Rule("tension_grave", 70f) ? "mundo à beira"
             : "mundo em chamas";
    }

    public static string Short(World w) => $"{Of(w):0} de 100 — {Mood(w)}";
}
