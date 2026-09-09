using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A guerra submarina (HoI4: submarine visibility e anti-submarine warfare — a Batalha do
/// Atlântico).
///
/// O submarino existia na tabela desde que a marinha ganhou classes, mas era um navio como os outros: dava
/// mais bloqueio, não protegia ninguém e ia ao fundo no combate de esquadra ao lado dos cruzadores. Faltava
/// o que faz de um submarino um submarino — ANDAR ESCONDIDO. Sem isso não havia guerra ao comércio nenhuma:
/// mandar submarinos a um mar patrulhado era perdê-los no dia seguinte, e por isso a única marinha que valia
/// a pena era a maior. Do outro lado faltava a resposta: o contratorpedeiro dizia na ficha que caçava
/// submarinos e não tinha uma única linha de código que o fizesse.
///
/// A conta é uma só e está toda aqui:
///   VISTO = busca ÷ (busca + sub_hide × submarinos daquele lado naquele mar)
/// A busca é o `ship_class.asw` de tudo o que o inimigo tem naquele mar — a esquadra toda vê alguma coisa
/// (asw_passive), a missão de caça anti-submarina vê tudo. Um bando de submarinos dilui a busca: cobrir dez
/// é mais difícil do que cobrir um. O que fica ESCONDIDO (stealth × o que não se vê) não leva tiro nenhum,
/// nem no combate de esquadra nem do ar — mas também não dá tiro nenhum: quem se esconde não está na linha.
/// Afundar o que se vê é obra da missão de caça (asw_kill); as outras esquadras revelam e mais nada.
///
/// O bloqueio continua a ser bloqueio: o submarino escondido corta o comércio na mesma — é para isso que
/// ele lá está. Quem quiser o mar aberto tem de o ir buscar ao fundo, não de o esperar à porta.
///
/// Estado derivado: não guarda nada, não entra no save e não é ISystem. Uma marinha sem stealth nem asw na
/// tabela (save antigo, mundo de teste) dá zero em tudo e o mar luta exactamente como sempre lutou.</summary>
public static class Subs
{
    /// <summary>Quanto este casco se esconde (0 = anda à vista, como todos menos o submarino).</summary>
    public static float Stealth(World w, string classId) =>
        classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Stealth : 0f;

    /// <summary>Quanto este casco vê e afunda do que anda por baixo do mar.</summary>
    public static float Asw(World w, string classId) =>
        classId.Length > 0 && w.ShipClasses.TryGetValue(classId, out var d) ? d.Asw : 0f;

    /// <summary>Cascos que se escondem numa esquadra (os que têm stealth: hoje, os submarinos).</summary>
    public static float Hulls(World w, IReadOnlyDictionary<string, float> squadron)
    {
        float t = 0f;
        foreach (var (cls, n) in squadron) if (Stealth(w, cls) > 0f) t += n;
        return t;
    }

    /// <summary>A missão que caça submarinos, lida da tabela pelo efeito (vazio se o mundo não a tiver).
    /// O jogo não sabe o nome de missão nenhuma: sabe efeitos.</summary>
    public static string MissionId(World w) =>
        w.NavalMissionDefs.Values.Where(d => d.Effect == "asw").OrderBy(d => d.Sort)
            .ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "";

    /// <summary>A busca que um lado faz naquele mar: o `asw` de todos os cascos que lá tem, a peso inteiro
    /// se estão em caça anti-submarina e a asw_passive se andam noutra coisa qualquer. Uma esquadra de
    /// escolta já vê alguma coisa — é o que a torna escolta.</summary>
    public static float Hunt(World w, string zone, Func<int, bool> side)
    {
        float passive = w.Rule("asw_passive", 0.25f), total = 0f;
        foreach (var m in w.NavalMissions)
        {
            if (!side(m.CountryId) || Zones.Sea(w, m.RegionId) != zone) continue;
            bool hunter = w.NavalMissionDefs.TryGetValue(m.MissionId, out var def) && def.Effect == "asw";
            float mult = hunter ? def!.Value : passive;
            if (mult <= 0f) continue;
            foreach (var (cls, n) in m.Squadron) total += n * Asw(w, cls) * mult;
        }
        return total;
    }

    /// <summary>Fatia dos submarinos daquele lado que a busca chega a ver (0..1). Sem busca nenhuma não se
    /// vê nada; sem submarinos nenhuns vê-se tudo, que é o mundo de sempre.</summary>
    public static float Seen(World w, float hunt, float subs)
    {
        if (subs <= 0f) return 1f;
        if (hunt <= 0f) return 0f;
        float k = MathF.Max(0.0001f, w.Rule("sub_hide", 3f));
        return Math.Clamp(hunt / (hunt + k * subs), 0f, 1f);
    }

    /// <summary>Fatia dos submarinos deste país naquele mar que hoje está escondida (0..1). É o número que o
    /// painel escreve e o que decide se o aço deles lhes acerta.</summary>
    public static float HiddenShare(World w, int countryId, string zone)
    {
        float subs = Pack(w, countryId, zone);
        if (subs <= 0f) return 0f;
        float hunt = Hunt(w, zone, cid => w.AreAtWar(cid, countryId));
        return 1f - Seen(w, hunt, subs);
    }

    /// <summary>Submarinos que este país tem naquele mar ao todo (o bando é que dilui a busca, não cada
    /// esquadra por si).</summary>
    public static float Pack(World w, int countryId, string zone)
    {
        float t = 0f;
        foreach (var m in w.NavalMissions)
            if (m.CountryId == countryId && Zones.Sea(w, m.RegionId) == zone) t += Hulls(w, m.Squadron);
        return t;
    }

    /// <summary>Os cascos desta esquadra em que hoje não se pode tocar, classe a classe: stealth vezes o que
    /// a busca do inimigo não vê. Vazio quando não há submarino nenhum — e então tudo continua como era.</summary>
    public static Dictionary<string, float> Hidden(World w, NavalMission m)
    {
        var safe = new Dictionary<string, float>();
        if (Hulls(w, m.Squadron) <= 0f) return safe;
        float share = HiddenShare(w, m.CountryId, Zones.Sea(w, m.RegionId));
        if (share <= 0f) return safe;
        foreach (var (cls, n) in m.Squadron)
        {
            float s = Stealth(w, cls);
            if (s > 0f) safe[cls] = n * s * share;
        }
        return safe;
    }

    /// <summary>O mesmo para uma lista de esquadras, para quem afunda aço só ter de perguntar uma vez.
    /// Devolve null quando não há um único submarino em jogo: assim o caminho antigo nem se toca.</summary>
    public static Dictionary<NavalMission, Dictionary<string, float>>? Hide(World w, List<NavalMission> missions)
    {
        Dictionary<NavalMission, Dictionary<string, float>>? map = null;
        foreach (var m in missions)
        {
            var safe = Hidden(w, m);
            if (safe.Count == 0) continue;
            (map ??= new())[m] = safe;
        }
        return map;
    }

    /// <summary>O contrário do esconderijo: os cascos desta esquadra que HOJE se podem afundar numa caça
    /// anti-submarina — os submarinos vistos, e mais nada. Tudo o que não é submarino fica intocável, que a
    /// caça é ao que anda por baixo do mar e não à esquadra de superfície.</summary>
    public static Dictionary<string, float> Surfaced(World w, NavalMission m)
    {
        var safe = new Dictionary<string, float>();
        var hidden = Hidden(w, m);
        foreach (var (cls, n) in m.Squadron)
            safe[cls] = Stealth(w, cls) > 0f ? hidden.GetValueOrDefault(cls) : n;
        return safe;
    }

    /// <summary>Submarinos vistos deste país naquele mar: o que a caça pode ir buscar hoje.</summary>
    public static float Visible(World w, int countryId, string zone) =>
        Pack(w, countryId, zone) * (1f - HiddenShare(w, countryId, zone));

    /// <summary>Costa nossa cujo mar tem mais aço deles escondido por baixo: é para lá que a IA manda a
    /// caça. Empates pelo id da região, para o mundo não depender de sementes.</summary>
    public static int? Prey(World w, int countryId)
    {
        int? best = null; float most = 0.001f;
        foreach (var r in w.Regions.Values.OrderBy(x => x.Id))
        {
            if (r.SeaNeighbours.Count == 0) continue;
            if (r.ControllerId != countryId && !w.AreAtWar(countryId, r.ControllerId)) continue;
            if (!NavalMissionSystem.InRange(w, countryId, r)) continue;
            string zone = Zones.Sea(w, r.Id);
            float subs = 0f;
            foreach (var m in w.NavalMissions)
                if (w.AreAtWar(countryId, m.CountryId) && Zones.Sea(w, m.RegionId) == zone)
                    subs += Hulls(w, m.Squadron);
            if (subs > most) { most = subs; best = r.Id; }
        }
        return best;
    }

    /// <summary>A guerra submarina de um país em palavras, para o painel e para a prova headless.</summary>
    public static string Short(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "sem marinha";
        float subs = Hulls(w, c.Ships);
        var seas = w.NavalMissions.Where(m => m.CountryId == countryId && Hulls(w, m.Squadron) > 0f)
                    .Select(m => Zones.Sea(w, m.RegionId)).Distinct().ToList();
        float sea = seas.Sum(z => Pack(w, countryId, z)), hid = seas.Sum(z => Pack(w, countryId, z) * HiddenShare(w, countryId, z));
        return $"{subs:0.#} submarinos, {sea:0.#} no mar, {hid:0.#} escondidos";
    }
}
