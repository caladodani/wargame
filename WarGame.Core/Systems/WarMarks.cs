using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A guerra do ar e do mar vista no mapa, como no HoI4: onde estão as asas e as esquadras.
///
/// O céu e o mar tinham tudo menos presença. Havia modelos de avião, campos onde dormem, porta-aviões,
/// submarinos que se escondem e caça anti-submarina — e nada disso se via no mapa: para saber onde andava
/// a nossa aviação abria-se um painel, lia-se uma lista e fechava-se. No HoI4 o mapa é o jogo: as esquadras
/// estão desenhadas em cima do mar onde patrulham e o céu ocupado vê-se sem se abrir nada. Um jogador que
/// olha para o mapa tem de saber, sem carregar em coisa nenhuma, quantas asas deles estão em cima da nossa
/// frente e que esquadra é aquela que fecha o nosso cais.
///
/// Aqui decide-se QUEM SE VÊ e o que se diz de cada um; o desenho é do lado do Godot (WarMapMarks). São
/// três leis, todas já do jogo:
/// • as nossas e as dos aliados vêem-se sempre — é aviação de casa;
/// • as dos outros só onde o nevoeiro deixa (Vision.Sees), que é a mesma porta das divisões;
/// • debaixo de água só se vê o que a busca do inimigo já levantou (Subs.Hidden) — uma alcateia inteira
///   escondida não aparece no mapa de ninguém, e é essa a razão de haver caça anti-submarina.
///
/// Estado derivado: não guarda nada, não entra no save, não é ISystem. Corre-se a cada pintura do mapa.</summary>
public static class WarMarks
{
    /// <summary>Uma chapa da guerra por cima do mapa: uma missão de ar ou uma esquadra no mar.
    /// Count é o que se vê (asas ou cascos), Hidden o que fica por baixo de água e só o dono conta.</summary>
    public readonly record struct Mark(int RegionId, string RegionName, float X, float Y, bool Sea, int Side,
                                       int CountryId, string CountryTag, string Name, string Glyph,
                                       string MissionGlyph, string MissionName, float Count, float Hidden,
                                       string Detail);

    /// <summary>Todas as chapas que este país vê hoje, do ar primeiro e depois do mar, por região. A ordem
    /// não depende de sementes nem de dicionários: duas máquinas desenham o mesmo mapa.</summary>
    public static List<Mark> All(World w, int viewerId)
    {
        var marks = new List<Mark>();
        foreach (var m in w.AirMissions.OrderBy(x => x.RegionId).ThenBy(x => x.CountryId)
                           .ThenBy(x => x.MissionId, StringComparer.Ordinal))
            if (Air(w, viewerId, m) is Mark air) marks.Add(air);
        foreach (var m in w.NavalMissions.OrderBy(x => x.RegionId).ThenBy(x => x.CountryId)
                           .ThenBy(x => x.MissionId, StringComparer.Ordinal))
            if (Sea(w, viewerId, m) is Mark sea) marks.Add(sea);
        return marks;
    }

    /// <summary>Uma missão de ar: a chapa é a do modelo que leva mais asas, porque é esse que se vê do
    /// chão. Null quando o céu daquela região está fora da nossa vista.</summary>
    private static Mark? Air(World w, int viewerId, AirMission m)
    {
        if (!w.Regions.TryGetValue(m.RegionId, out var r)) return null;
        int side = MapMarks.Side(w, viewerId, m.CountryId);
        if (side != 1 && !Vision.Sees(w, viewerId, r)) return null;
        float wings = m.Wings;
        if (wings <= 0.05f) return null;

        string cls = Biggest(m.Squadron);
        var mis = w.AirMissionDefs.GetValueOrDefault(m.MissionId);
        string mission = mis?.Name ?? m.MissionId;
        return new Mark(r.Id, r.Name, r.CenterX, r.CenterY, false, side, m.CountryId, Tag(w, m.CountryId),
                        m.Name, Systems.Air.Glyph(w, cls), mis?.Glyph ?? "asa", mission, wings, 0f,
                        $"{Name(w, m.CountryId)} — {wings:0.#} asa{(wings < 1.95f ? "" : "s")} em {mission.ToLowerInvariant()}"
                        + $" sobre {r.Name}: {Systems.Air.Describe(w, m.Squadron)}");
    }

    /// <summary>Uma esquadra no mar: conta-se o que está à superfície, e o que anda por baixo só o dono
    /// sabe. Uma alcateia inteiramente escondida não dá chapa nenhuma ao inimigo — o mar parece vazio, que
    /// é exactamente o que o submarino faz.</summary>
    private static Mark? Sea(World w, int viewerId, NavalMission m)
    {
        if (!w.Regions.TryGetValue(m.RegionId, out var r)) return null;
        int side = MapMarks.Side(w, viewerId, m.CountryId);
        bool ours = side == 1;
        if (!ours && !Vision.Sees(w, viewerId, r)) return null;

        var hidden = Subs.Hidden(w, m);
        float below = hidden.Values.Sum();
        float ships = m.Ships;
        float seen = ours ? ships : MathF.Max(0f, ships - below);
        if (seen <= 0.05f) return null;

        // a chapa vem do casco mais numeroso do que se vê: um comboio de escolta não se marca com o
        // submarino que vai lá no meio escondido
        var visible = new Dictionary<string, float>();
        foreach (var (cls, n) in m.Squadron)
        {
            float x = ours ? n : n - hidden.GetValueOrDefault(cls);
            if (x > 0.0001f) visible[cls] = x;
        }
        var mis = w.NavalMissionDefs.GetValueOrDefault(m.MissionId);
        string mission = mis?.Name ?? m.MissionId;
        string detail = $"{Name(w, m.CountryId)} — {seen:0.#} navio{(seen < 1.95f ? "" : "s")} em"
                      + $" {mission.ToLowerInvariant()} no mar de {r.Name}: {Navy.Describe(w, visible)}"
                      + (ours && below > 0.05f ? $" ({below:0.#} escondido{(below < 1.95f ? "" : "s")})" : "");
        return new Mark(r.Id, r.Name, r.CenterX, r.CenterY, true, side, m.CountryId, Tag(w, m.CountryId),
                        m.Name, Navy.Glyph(w, Biggest(visible)), mis?.Glyph ?? "barco", mission,
                        seen, ours ? below : 0f, detail);
    }

    /// <summary>O que mais lá há dentro, com desempate pelo id: a chapa do grupo não pode mudar de máquina
    /// para máquina por causa da ordem de um dicionário.</summary>
    private static string Biggest(IReadOnlyDictionary<string, float> bag)
    {
        string best = ""; float most = 0f;
        foreach (var (id, n) in bag.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            if (n > most) { most = n; best = id; }
        return best;
    }

    private static string Tag(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Tag : "";

    private static string Name(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Name : "Desconhecidos";

    /// <summary>Uma linha para a prova headless e para quem quiser contar o que o mapa mostra.</summary>
    public static string Short(World w, int viewerId)
    {
        var all = All(w, viewerId);
        int air = all.Count(m => !m.Sea), sea = all.Count - air;
        float theirs = all.Where(m => m.Side == -1).Sum(m => m.Count);
        float below = all.Sum(m => m.Hidden);
        return $"{air} chapa{(air == 1 ? "" : "s")} de ar e {sea} de mar, {theirs:0.#} deles à vista, "
             + $"{below:0.#} nossos por baixo de água";
    }
}
