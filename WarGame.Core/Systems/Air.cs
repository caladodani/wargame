using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A aviação por modelos de avião (HoI4: fighter / CAS / tactical / strategic / transport).
///
/// O céu deste jogo tinha um número só: Country.AirPower. Dez asas eram dez asas, fossem caças ou
/// transportes, e por isso a aviação não tinha decisão nenhuma — só quantidade. O mar já tinha deixado de
/// ser um número (ship_class, 0.3.63); o céu era a última arma inteira do jogo em que comprar era carregar
/// num botão e esquecer.
///
/// Aqui está a conta toda: o que cada modelo vale em cada tarefa (plane_class.superiority/support/bombing/
/// transport), o que pesa num combate aéreo (air) e o que sai do campo para cada missão. Os modelos vêm
/// todos da tabela: não há um único tipo de avião escrito em código. O modelo vazio é o avião sem modelo —
/// o que os saves velhos e os mundos de teste têm — e vale 1 em tudo, para o jogo antigo continuar a dar
/// exactamente os mesmos números.
///
/// O que o céu tem de seu, e o mar não: no mar a escolta põe-se à frente e leva os tiros de propósito
/// (ship_class.screen); no ar ninguém protege ninguém — cai quem não sabe lutar. Por isso as perdas
/// repartem-se pelo INVERSO do valor de combate (air_loss_focus): a esquadrilha de bombardeiros
/// desacompanhada é abatida quase toda enquanto os caças ao lado voltam a casa. É a razão de haver caças.
///
/// Estado derivado: não guarda nada de seu, não entra no save e não é ISystem.</summary>
public static class Air
{
    /// <summary>Quanto vale uma asa deste modelo nesta tarefa ("superiority" | "support" | "bombing" |
    /// "transport"). Sem modelo (ou modelo que a tabela não conhece) vale 1: é a asa genérica de sempre.</summary>
    public static float Value(World w, string classId, string effect)
    {
        if (classId.Length == 0 || !w.PlaneClasses.TryGetValue(classId, out var d)) return 1f;
        return effect switch
        {
            "superiority" => d.Superiority, "support" => d.Support,
            "bombing" => d.Bombing, "transport" => d.Transport, "naval" => d.Naval, _ => 1f,
        };
    }

    /// <summary>Peso no combate aéreo (1 sem modelo). É o que decide o céu — e o que salva o avião.</summary>
    public static float Battle(World w, string classId) =>
        classId.Length > 0 && w.PlaneClasses.TryGetValue(classId, out var d) ? d.Air : 1f;

    /// <summary>Estadia diária desta asa, em multiplicadores de air_mission_upkeep.</summary>
    public static float Upkeep(World w, string classId) =>
        classId.Length > 0 && w.PlaneClasses.TryGetValue(classId, out var d) ? d.Upkeep : 1f;

    /// <summary>O que custa comprar uma asa deste modelo, já em pontos de produção.</summary>
    public static float Cost(World w, string classId) =>
        w.Rule("air_wing_cost", 60f) * (classId.Length > 0 && w.PlaneClasses.TryGetValue(classId, out var d) ? d.Cost : 1f);

    /// <summary>O modelo que o botão de sempre compra (plane_class.basic), ou o mais barato se a tabela não
    /// marcar nenhum. Vazio num mundo sem modelos nenhuns.</summary>
    public static string Basic(World w)
    {
        if (w.PlaneClasses.Count == 0) return "";
        var basic = w.PlaneClasses.Values.FirstOrDefault(d => d.Basic);
        return (basic ?? w.PlaneClasses.Values.OrderBy(d => d.Cost).ThenBy(d => d.Sort).First()).Id;
    }

    /// <summary>Que modelo é que se compra a seguir com este dinheiro para servir esta tarefa: o melhor que
    /// o cofre paga, e o mais barato em caso de empate. Vazio num mundo sem modelos. É a mesma conta para a
    /// IA e para o botão que compra sem perguntar — ninguém escolhe aviões por uma lista escrita em código.</summary>
    public static string Choose(World w, float budget, string effect)
    {
        if (w.PlaneClasses.Count == 0) return "";
        var afford = w.PlaneClasses.Values.Where(d => Cost(w, d.Id) <= budget).ToList();
        if (afford.Count == 0) return "";
        return afford.OrderByDescending(d => effect == "air" ? d.Air : Value(w, d.Id, effect))
                     .ThenBy(d => d.Cost).ThenBy(d => d.Sort).First().Id;
    }

    /// <summary>Peso de combate aéreo de uma asa inteira (ou do campo todo).</summary>
    public static float Power(World w, IReadOnlyDictionary<string, float> squadron)
    {
        float t = 0f;
        foreach (var (cls, n) in squadron) t += n * Battle(w, cls);
        return t;
    }

    /// <summary>Qualidade média do que ali está, por avião: é o que decide um combate aéreo, e não a
    /// contagem — senão a força maior era castigada por ser maior, quando o tamanho já conta à parte.</summary>
    public static float Quality(World w, IReadOnlyDictionary<string, float> squadron)
    {
        float wings = 0f;
        foreach (var n in squadron.Values) wings += n;
        return wings <= 0f ? 0f : Power(w, squadron) / wings;
    }

    /// <summary>Aviões deste modelo que estão em casa: os do pool menos os que já andam no ar. Os
    /// transportes que levam pára-quedistas também estão fora enquanto o salto não acaba.</summary>
    public static float Free(World w, int countryId, string classId)
    {
        float have = w.Countries.TryGetValue(countryId, out var c) ? c.Planes.GetValueOrDefault(classId) : 0f;
        foreach (var m in w.AirMissions)
            if (m.CountryId == countryId) have -= m.Squadron.GetValueOrDefault(classId);
        return MathF.Max(0f, have);
    }

    /// <summary>O que o país tem no campo, modelo a modelo, do mais valioso para esta tarefa para o menos.
    /// É a lista que o painel do céu desenha e de onde sai a composição de uma missão nova.</summary>
    public static List<(string Class, float Wings)> Field(World w, int countryId, string effect = "")
    {
        var list = new List<(string Class, float Wings)>();
        if (!w.Countries.TryGetValue(countryId, out var c)) return list;
        foreach (var cls in c.Planes.Keys)
        {
            float free = Free(w, countryId, cls);
            if (free > 0.0001f) list.Add((cls, free));
        }
        return effect.Length == 0
            ? list.OrderBy(x => Sort(w, x.Class)).ThenBy(x => x.Class, StringComparer.Ordinal).ToList()
            : list.OrderByDescending(x => Value(w, x.Class, effect)).ThenByDescending(x => Battle(w, x.Class))
                  .ThenBy(x => x.Class, StringComparer.Ordinal).ToList();
    }

    private static int Sort(World w, string classId) =>
        classId.Length > 0 && w.PlaneClasses.TryGetValue(classId, out var d) ? d.Sort : 99;

    /// <summary>Que aviões é que levantam para esta tarefa: os melhores para ela primeiro, até perfazer as
    /// asas pedidas. Modelos que não servem de nada naquela tarefa (valor zero) ficam no chão — não se
    /// mandam transportes varrer o céu. É aqui que o jogo decide sozinho o que ninguém quer escolher à mão,
    /// e é a mesma conta para o jogador e para a IA.</summary>
    public static Dictionary<string, float> Pick(World w, int countryId, string effect, float wings)
    {
        var take = new Dictionary<string, float>();
        float left = wings;
        foreach (var (cls, free) in Field(w, countryId, effect))
        {
            if (left <= 0.0001f) break;
            if (effect.Length > 0 && Value(w, cls, effect) <= 0f) continue;
            float n = MathF.Min(free, left);
            take[cls] = n; left -= n;
        }
        // um país de que ninguém sabe a aviação (mundo de teste montado à mão) destaca asas sem modelo;
        // quem tem campo destaca o que lá tem e mais nada — não se inventam aviões
        if (left > 0.0001f && (!w.Countries.TryGetValue(countryId, out var c) || c.Planes.Count == 0))
            take[""] = take.GetValueOrDefault("") + left;
        return take;
    }

    /// <summary>Abate aviões de uma asa: cai primeiro quem não sabe lutar no céu. air_loss_focus diz quanto
    /// é que isso pesa — a 0 caem todos por igual (o jogo antigo), a 1 as perdas vão quase todas para os
    /// modelos de valor de combate mais baixo. Devolve o que ficou lá, modelo a modelo, para o pool
    /// nacional pagar a mesma conta.</summary>
    public static Dictionary<string, float> Down(World w, Dictionary<string, float> squadron, float wings)
    {
        var gone = new Dictionary<string, float>();
        if (wings <= 0f || squadron.Count == 0) return gone;

        float focus = Math.Clamp(w.Rule("air_loss_focus", 0.7f), 0f, 1f);
        float best = 0f;
        foreach (var cls in squadron.Keys) best = MathF.Max(best, Battle(w, cls));
        best = MathF.Max(best, 0.0001f);

        var weights = new Dictionary<string, float>();
        foreach (var (cls, n) in squadron)
        {
            if (n <= 0f) continue;
            // 1 para o melhor caça da asa e até 1/(1-focus) para quem não se defende: é o que faz da
            // escolta de caças uma decisão e não um pormenor
            float frail = 1f - focus * (Battle(w, cls) / best);
            weights[cls] = n * MathF.Max(0.05f, frail);
        }
        float total = weights.Values.Sum();
        if (total <= 0f) return gone;

        float pool = squadron.Values.Sum();
        float take = MathF.Min(wings, pool);
        foreach (var (cls, weight) in weights.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            float n = MathF.Min(squadron.GetValueOrDefault(cls), take * weight / total);
            if (n <= 0f) continue;
            squadron[cls] = squadron.GetValueOrDefault(cls) - n;
            gone[cls] = gone.GetValueOrDefault(cls) + n;
        }
        foreach (var cls in squadron.Keys.ToList()) if (squadron[cls] <= 0.0001f) squadron.Remove(cls);
        return gone;
    }

    /// <summary>Tira do pool nacional o que foi abatido, modelo a modelo.</summary>
    public static void Lose(World w, Country c, Dictionary<string, float> gone)
    {
        foreach (var (cls, n) in gone)
        {
            float left = c.Planes.GetValueOrDefault(cls) - n;
            if (left <= 0.0001f) c.Planes.Remove(cls); else c.Planes[cls] = left;
        }
    }

    /// <summary>Compra uma asa: tira o preço do modelo do cofre e põe o avião no campo.</summary>
    public static void Buy(World w, Country c, string classId)
    {
        c.Money -= Cost(w, classId);
        c.Planes[classId] = c.Planes.GetValueOrDefault(classId) + 1f;
    }

    /// <summary>Dá modelo às asas sem modelo de um país: air_start_mix vai para o caça (o modelo de combate
    /// aéreo mais barato acima da média) e o resto para quem melhor bate no chão. É o que faz uma aviação de
    /// partida — e o que arruma um save antigo, onde o céu era só um número, sem lhe mexer no total. Os
    /// modelos não estão escritos aqui: sai tudo da forma dos números da tabela.
    ///
    /// As asas que já andam no ar levam a MESMA repartição: o pool e o céu têm de falar a mesma língua,
    /// senão abate-se um avião sem modelo que o campo já não tem e a perda não sai do total.</summary>
    public static void Classify(World w, Country c)
    {
        float loose = c.Planes.GetValueOrDefault("");
        if (loose <= 0.0001f || w.PlaneClasses.Count == 0) return;

        float mean = w.PlaneClasses.Values.Average(d => d.Air);
        var fighter = w.PlaneClasses.Values.Where(d => d.Air > mean)
                                    .OrderBy(d => d.Cost).ThenBy(d => d.Sort).FirstOrDefault();
        var striker = w.PlaneClasses.Values.OrderByDescending(d => d.Support).ThenBy(d => d.Cost)
                                    .ThenBy(d => d.Sort).FirstOrDefault();
        if (striker is null) return;

        float share = fighter is null ? 0f : Math.Clamp(w.Rule("air_start_mix", 0.45f), 0f, 1f);
        Split(c.Planes, loose, fighter, striker, share);
        foreach (var m in w.AirMissions)
            if (m.CountryId == c.Id && m.Squadron.GetValueOrDefault("") > 0.0001f)
                Split(m.Squadron, m.Squadron[""], fighter, striker, share);
    }

    /// <summary>Reparte asas sem modelo entre o caça e quem bate no chão, no mesmo saco onde estavam.</summary>
    private static void Split(Dictionary<string, float> bag, float loose, PlaneClassDef? fighter,
                              PlaneClassDef striker, float share)
    {
        float toFighter = loose * share;
        bag.Remove("");
        if (fighter is not null && toFighter > 0f)
            bag[fighter.Id] = bag.GetValueOrDefault(fighter.Id) + toFighter;
        bag[striker.Id] = bag.GetValueOrDefault(striker.Id) + (loose - toFighter);
    }

    /// <summary>A asa em palavras: "Caça multifunções ×4, Bombardeiro táctico ×2". É o que o painel escreve
    /// por baixo do nome da formação e o que a prova headless lê.</summary>
    public static string Describe(World w, IReadOnlyDictionary<string, float> squadron)
    {
        var parts = squadron.Where(kv => kv.Value > 0.0001f)
            .OrderBy(kv => Sort(w, kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Name(w, kv.Key)} ×{kv.Value:0.#}");
        string s = string.Join(", ", parts);
        return s.Length > 0 ? s : "sem aviões";
    }

    /// <summary>O nome que se lê de um modelo (a asa sem modelo chama-se esquadrão).</summary>
    public static string Name(World w, string classId) =>
        classId.Length > 0 && w.PlaneClasses.TryGetValue(classId, out var d) ? d.Name : "Esquadrão";

    /// <summary>A chapa de um modelo.</summary>
    public static string Glyph(World w, string classId) =>
        classId.Length > 0 && w.PlaneClasses.TryGetValue(classId, out var d) ? d.Glyph : "asa";

    /// <summary>A aviação de um país em palavras, para o painel e para a prova: quantas asas, de que
    /// modelos, quantas no ar e que peso de combate aéreo é que aquilo tem.</summary>
    public static string Short(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "sem aviação";
        float sky = w.AirMissions.Where(m => m.CountryId == countryId).Sum(m => m.Wings);
        return $"{c.AirPower:0.#} asas ({Describe(w, c.Planes)}), {sky:0.#} no ar, peso {Power(w, c.Planes):0.#}";
    }
}
