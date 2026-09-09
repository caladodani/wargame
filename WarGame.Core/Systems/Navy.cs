using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A marinha por classes de casco (HoI4: hull/role — screens, capitais, submarinos).
///
/// O mar deste jogo tinha um número só: Country.Warships. Dez navios eram dez navios, fossem lanchas de
/// patrulha ou porta-aviões, e por isso a marinha não tinha decisões nenhumas — só quantidade. No HoI4 (e
/// na guerra) uma esquadra é uma composição: a escolta à frente para levar os tiros, a linha atrás para os
/// dar, e o submarino sozinho a cortar o mar a quem dele vive. Uma esquadra de cruzadores sem escolta é
/// aço a afundar; uma parede de contratorpedeiros sem linha não afunda ninguém.
///
/// Aqui está a conta toda: o que cada classe vale em cada tarefa (ship_class.blockade/escort/patrol), o
/// peso no combate (battle), a couraça da esquadra (screen) e como se escolhe o que sai do porto para uma
/// tarefa. As classes vêm todas da tabela: não há um único tipo de navio escrito em código. A classe vazia
/// é o casco sem classe — o que os saves velhos e os mundos de teste têm — e vale 1 em tudo, para o jogo
/// antigo continuar a dar exactamente os mesmos números.
///
/// Estado derivado: não guarda nada, não entra no save e não é ISystem.</summary>
public static class Navy
{
    /// <summary>Quanto vale um casco desta classe nesta tarefa ("blockade" | "escort" | "patrol").
    /// Sem classe (ou classe que a tabela não conhece) vale 1: é o navio genérico de sempre.</summary>
    public static float Value(World w, string classId, string effect)
    {
        if (classId.Length == 0 || !w.ShipClasses.TryGetValue(classId, out var d)) return 1f;
        return effect switch { "blockade" => d.Blockade, "escort" => d.Escort, "patrol" => d.Patrol, _ => 1f };
    }

    /// <summary>Peso no combate de esquadra (1 sem classe).</summary>
    public static float Battle(World w, string classId) =>
        classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Battle : 1f;

    /// <summary>Couraça: quanto este casco se põe à frente da linha. Zero é um navio que não protege
    /// ninguém — o submarino e o porta-aviões.</summary>
    public static float Screen(World w, string classId) =>
        classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Screen : 1f;

    /// <summary>Estadia diária deste casco, em multiplicadores de naval_mission_upkeep.</summary>
    public static float Upkeep(World w, string classId) =>
        classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Upkeep : 1f;

    /// <summary>O que custa comprar um casco desta classe, já em pontos de produção.</summary>
    public static float Cost(World w, string classId) =>
        w.Rule("naval_ship_cost", 90f) * (classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Cost : 1f);

    /// <summary>A classe que o botão de sempre compra (ship_class.basic), ou a mais barata se a tabela não
    /// marcar nenhuma. Vazio num mundo sem classes nenhumas.</summary>
    public static string Basic(World w)
    {
        if (w.ShipClasses.Count == 0) return "";
        var basic = w.ShipClasses.Values.FirstOrDefault(d => d.Basic);
        return (basic ?? w.ShipClasses.Values.OrderBy(d => d.Cost).ThenBy(d => d.Sort).First()).Id;
    }

    /// <summary>Peso de combate de uma esquadra inteira.</summary>
    public static float Power(World w, IReadOnlyDictionary<string, float> squadron)
    {
        float t = 0f;
        foreach (var (cls, n) in squadron) t += n * Battle(w, cls);
        return t;
    }

    /// <summary>Navios desta classe que estão no porto: os do pool menos os que já andam no mar.</summary>
    public static float Free(World w, int countryId, string classId)
    {
        float have = w.Countries.TryGetValue(countryId, out var c) ? c.Ships.GetValueOrDefault(classId) : 0f;
        foreach (var m in w.NavalMissions)
            if (m.CountryId == countryId) have -= m.Squadron.GetValueOrDefault(classId);
        return MathF.Max(0f, have);
    }

    /// <summary>O que o país tem no porto, classe a classe, da mais valiosa para esta tarefa para a menos.
    /// É a lista que o painel do estaleiro desenha e de onde sai a composição de uma esquadra nova.</summary>
    public static List<(string Class, float Ships)> Harbour(World w, int countryId, string effect = "")
    {
        var list = new List<(string Class, float Ships)>();
        if (!w.Countries.TryGetValue(countryId, out var c)) return list;
        foreach (var cls in c.Ships.Keys)
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
        classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Sort : 99;

    /// <summary>Que cascos é que saem do porto para esta tarefa: os melhores para ela primeiro, até
    /// perfazer os navios pedidos. É aqui que o jogo decide sozinho o que ninguém quer escolher à mão —
    /// e é a mesma conta para o jogador e para a IA.</summary>
    public static Dictionary<string, float> Pick(World w, int countryId, string effect, float ships)
    {
        var take = new Dictionary<string, float>();
        float left = ships;
        foreach (var (cls, free) in Harbour(w, countryId, effect))
        {
            if (left <= 0.0001f) break;
            float n = MathF.Min(free, left);
            take[cls] = n; left -= n;
        }
        // um país de que ninguém sabe a marinha (mundo de teste montado à mão) destaca cascos sem classe;
        // quem tem porto destaca o que tem lá dentro e mais nada — não se inventam navios
        if (left > 0.0001f && (!w.Countries.TryGetValue(countryId, out var c) || c.Ships.Count == 0))
            take[""] = take.GetValueOrDefault("") + left;
        return take;
    }

    /// <summary>Afunda navios de uma esquadra: a escolta leva naval_screen_share das perdas por si — é para
    /// isso que ela lá está — e só o resto chega à linha. Uma esquadra sem escolta leva tudo na linha.
    /// Devolve o que foi ao fundo, por classe, para o pool nacional pagar a mesma conta.</summary>
    public static Dictionary<string, float> Sink(World w, Dictionary<string, float> squadron, float ships)
    {
        var gone = new Dictionary<string, float>();
        if (ships <= 0f || squadron.Count == 0) return gone;

        float share = Math.Clamp(w.Rule("naval_screen_share", 0.75f), 0f, 1f);
        float screens = squadron.Sum(kv => kv.Value * Screen(w, kv.Key));
        float toScreen = screens > 0f ? MathF.Min(ships * share, squadron.Where(kv => Screen(w, kv.Key) > 0f).Sum(kv => kv.Value)) : 0f;
        Spread(w, squadron, gone, toScreen, byScreen: true);
        Spread(w, squadron, gone, ships - toScreen, byScreen: false);
        foreach (var cls in squadron.Keys.ToList()) if (squadron[cls] <= 0.0001f) squadron.Remove(cls);
        return gone;
    }

    /// <summary>Reparte perdas por uma parte da esquadra: pela couraça (a escolta) ou pelo que resta.</summary>
    private static void Spread(World w, Dictionary<string, float> squadron, Dictionary<string, float> gone,
                               float ships, bool byScreen)
    {
        if (ships <= 0.0001f) return;
        var weights = new Dictionary<string, float>();
        foreach (var (cls, n) in squadron)
        {
            float scr = Screen(w, cls);
            float weight = byScreen ? n * scr : n;
            if (byScreen && scr <= 0f) continue;
            if (weight > 0f) weights[cls] = weight;
        }
        float total = weights.Values.Sum();
        if (total <= 0f)
        {   // não há escolta nenhuma para levar esta parte: cai toda na esquadra, seja ela qual for
            if (byScreen) return;
            foreach (var cls in squadron.Keys.ToList()) weights[cls] = squadron[cls];
            total = weights.Values.Sum();
            if (total <= 0f) return;
        }
        foreach (var (cls, weight) in weights.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            float take = MathF.Min(squadron.GetValueOrDefault(cls), ships * weight / total);
            if (take <= 0f) continue;
            squadron[cls] = squadron.GetValueOrDefault(cls) - take;
            gone[cls] = gone.GetValueOrDefault(cls) + take;
        }
    }

    /// <summary>Tira do pool nacional o que foi ao fundo, classe a classe.</summary>
    public static void Lose(World w, Country c, Dictionary<string, float> gone)
    {
        foreach (var (cls, n) in gone)
        {
            float left = c.Ships.GetValueOrDefault(cls) - n;
            if (left <= 0.0001f) c.Ships.Remove(cls); else c.Ships[cls] = left;
        }
    }

    /// <summary>Compra um casco: tira o preço da classe do cofre e põe o navio no porto.</summary>
    public static void Buy(World w, Country c, string classId)
    {
        c.Money -= Cost(w, classId);
        c.Ships[classId] = c.Ships.GetValueOrDefault(classId) + 1f;
    }

    /// <summary>Dá classe aos cascos sem classe de um país: navy_start_mix vai para a linha (o casco de
    /// combate mais barato acima da média de peso) e o resto para a escolta (a maior couraça). É o que faz
    /// uma marinha de partida — e o que arruma um save antigo, onde a marinha era só um número, sem lhe
    /// mexer no total. As classes não estão escritas aqui: sai tudo da forma dos números da tabela.
    ///
    /// As esquadras que já andam no mar levam a MESMA repartição: o pool e o mar têm de falar a mesma
    /// língua, senão afunda-se um casco sem classe que o porto já não tem e a perda não sai do total.</summary>
    public static void Classify(World w, Country c)
    {
        float loose = c.Ships.GetValueOrDefault("");
        if (loose <= 0.0001f || w.ShipClasses.Count == 0) return;

        float mean = w.ShipClasses.Values.Average(d => d.Battle);
        var line = w.ShipClasses.Values.Where(d => d.Battle > mean)
                                .OrderBy(d => d.Cost).ThenBy(d => d.Sort).FirstOrDefault();
        var guard = w.ShipClasses.Values.OrderByDescending(d => d.Screen).ThenBy(d => d.Cost)
                                 .ThenBy(d => d.Sort).FirstOrDefault();
        if (guard is null) return;

        float share = line is null ? 0f : Math.Clamp(w.Rule("navy_start_mix", 0.35f), 0f, 1f);
        Split(c.Ships, loose, line, guard, share);
        foreach (var m in w.NavalMissions)
            if (m.CountryId == c.Id && m.Squadron.GetValueOrDefault("") > 0.0001f)
                Split(m.Squadron, m.Squadron[""], line, guard, share);
    }

    /// <summary>Reparte cascos sem classe entre linha e escolta no mesmo saco onde estavam.</summary>
    private static void Split(Dictionary<string, float> bag, float loose, ShipClassDef? line, ShipClassDef guard,
                              float share)
    {
        float toLine = loose * share;
        bag.Remove("");
        if (line is not null && toLine > 0f) bag[line.Id] = bag.GetValueOrDefault(line.Id) + toLine;
        bag[guard.Id] = bag.GetValueOrDefault(guard.Id) + (loose - toLine);
    }

    /// <summary>A esquadra em palavras: "Contratorpedeiro ×4, Submarino ×2". É o que o painel escreve por
    /// baixo do nome da esquadra e o que a prova headless lê.</summary>
    public static string Describe(World w, IReadOnlyDictionary<string, float> squadron)
    {
        var parts = squadron.Where(kv => kv.Value > 0.0001f)
            .OrderBy(kv => Sort(w, kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Name(w, kv.Key)} ×{kv.Value:0.#}");
        string s = string.Join(", ", parts);
        return s.Length > 0 ? s : "sem navios";
    }

    /// <summary>O nome que se lê de uma classe (o casco sem classe chama-se navio).</summary>
    public static string Name(World w, string classId) =>
        classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Name : "Navio";

    /// <summary>A chapa de uma classe.</summary>
    public static string Glyph(World w, string classId) =>
        classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Glyph : "barco";

    /// <summary>A marinha de um país em palavras, para a barra e para a prova: quantos cascos, de que
    /// classes, quantos no mar e que peso de combate é que aquilo tem.</summary>
    public static string Short(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "sem marinha";
        float sea = w.NavalMissions.Where(m => m.CountryId == countryId).Sum(m => m.Ships);
        return $"{c.Warships:0.#} cascos ({Describe(w, c.Ships)}), {sea:0.#} no mar, peso {Power(w, c.Ships):0.#}";
    }
}
