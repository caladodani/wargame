using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma linha do quadro de zonas: quem lá tem o quê e de quem é aquele céu (ou aquele mar).</summary>
/// <param name="Mine">Peso do país que pergunta (asas ou navios, já com o que a tabela diz que valem).</param>
/// <param name="Theirs">Peso de quem está em guerra com ele na mesma zona.</param>
public readonly record struct ZoneLine(string Id, string Name, string Glyph, float Mine, float Theirs, int Regions)
{
    /// <summary>De quem é a zona: "nosso" enquanto ninguém nos disputar, "disputado" com os dois lá dentro,
    /// "deles" quando só lá está o inimigo. É a frase que a barra e o painel escrevem.</summary>
    public string Who => Mine > 0f && Theirs > 0f ? (Mine > Theirs * 1.5f ? "nosso" : Theirs > Mine * 1.5f ? "deles" : "disputado")
                       : Mine > 0f ? "nosso" : Theirs > 0f ? "deles" : "vazio";
}

/// <summary>Zonas estratégicas (HoI4: strategic regions e sea zones) — o mapa por cima do mapa.
///
/// A guerra do ar e a guerra do mar aconteciam aqui província a província: uma asa destacada sobre Lisboa
/// não valia nada em Setúbal, e uma esquadra que bloqueasse o Porto deixava o cais do lado aberto. No HoI4
/// não é assim e não podia ser: o céu ganha-se por zona — um pedaço de mundo com nome, do tamanho de meia
/// península — e o mar fecha-se por zona. É isso que faz a aviação e a marinha serem armas de teatro e não
/// de província: concentrar ganha uma frente inteira, espalhar não ganha nenhuma.
///
/// As zonas de terra saem do Natural Earth (sub-região da ONU cortada por longitude, tools/import_map.py);
/// as de mar são semeadas à mão com a caixa de lat/lon do mar (seed_world.sql). Um mundo de teste sem
/// zonas nenhumas continua a funcionar: sem zona, cada região é o seu próprio céu — a chave passa a ser
/// "r&lt;id&gt;" e tudo se comporta como antes.
///
/// Estado derivado: não guarda nada, não entra no save e não é ISystem.</summary>
public static class Zones
{
    /// <summary>A chave do céu desta região: a zona de terra, ou a própria região quando não há zonas.</summary>
    public static string Air(World w, int regionId) =>
        w.Regions.TryGetValue(regionId, out var r) && r.ZoneId.Length > 0 ? r.ZoneId : "r" + regionId;

    /// <summary>A chave do mar desta costa: a zona naval, ou a própria região quando não há zonas.</summary>
    public static string Sea(World w, int regionId) =>
        w.Regions.TryGetValue(regionId, out var r) && r.SeaZoneId.Length > 0 ? r.SeaZoneId : "r" + regionId;

    /// <summary>Quanto vale, no céu (ou no mar) de A, uma missão que está em B: tudo quando é o mesmo sítio,
    /// a fatia da regra quando é a mesma zona, nada quando é outra zona. É a única conta que muda o jogo
    /// todo — e está aqui num sítio só para o ar e o mar não divergirem.</summary>
    public static float Reach(World w, int regionA, int regionB, bool sea)
    {
        if (regionA == regionB) return 1f;
        string ka = sea ? Sea(w, regionA) : Air(w, regionA), kb = sea ? Sea(w, regionB) : Air(w, regionB);
        if (ka != kb || ka.StartsWith('r')) return 0f;   // "r<id>" é região sem zona: não alcança mais nada
        return w.Rule(sea ? "sea_zone_share" : "air_zone_share", 1f);
    }

    /// <summary>O nome que se lê de uma zona (a região sozinha diz o nome dela).</summary>
    public static string Name(World w, string zoneId)
    {
        if (w.Zones.TryGetValue(zoneId, out var z)) return z.Name;
        return zoneId.StartsWith('r') && int.TryParse(zoneId[1..], out int id)
               && w.Regions.TryGetValue(id, out var r) ? r.Name : zoneId;
    }

    /// <summary>A chapa de uma zona (asa por terra, onda por mar) — as zonas vêem-se, não se lêem.</summary>
    public static string Glyph(World w, string zoneId) => w.Zones.TryGetValue(zoneId, out var z) ? z.Glyph : "globo";

    /// <summary>As regiões de uma zona. Percorre o mundo: é para painéis e legendas, não para o motor.</summary>
    public static List<Region> Members(World w, string zoneId, bool sea = false) =>
        w.Regions.Values.Where(r => (sea ? Sea(w, r.Id) : Air(w, r.Id)) == zoneId).OrderBy(r => r.Id).ToList();

    /// <summary>Quadro do céu do mundo visto por um país: uma linha por zona onde alguém tem asas, das mais
    /// disputadas para as mais vazias. É o que o painel da Guerra desenha e a prova headless lê.</summary>
    public static List<ZoneLine> AirBoard(World w, int viewerId) =>
        Board(w, viewerId, false, w.AirMissions.Select(m => (m.CountryId, m.RegionId, m.Wings)));

    /// <summary>O mesmo para o mar: uma linha por zona naval onde há esquadras.</summary>
    public static List<ZoneLine> SeaBoard(World w, int viewerId) =>
        Board(w, viewerId, true, w.NavalMissions.Select(m => (m.CountryId, m.RegionId, m.Ships)));

    private static List<ZoneLine> Board(World w, int viewerId, bool sea, IEnumerable<(int CountryId, int RegionId, float Force)> src)
    {
        var mine = new Dictionary<string, float>(); var theirs = new Dictionary<string, float>();
        foreach (var (cid, rid, force) in src)
        {
            string key = sea ? Sea(w, rid) : Air(w, rid);
            if (cid == viewerId || w.SameFaction(cid, viewerId)) mine[key] = mine.GetValueOrDefault(key) + force;
            else if (w.AreAtWar(cid, viewerId)) theirs[key] = theirs.GetValueOrDefault(key) + force;
        }
        var counts = new Dictionary<string, int>();
        foreach (var r in w.Regions.Values)
        {
            if (sea && !r.Coastal) continue;
            string key = sea ? Sea(w, r.Id) : Air(w, r.Id);
            if (mine.ContainsKey(key) || theirs.ContainsKey(key)) counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return mine.Keys.Concat(theirs.Keys).Distinct()
            .Select(k => new ZoneLine(k, Name(w, k), Glyph(w, k), mine.GetValueOrDefault(k), theirs.GetValueOrDefault(k),
                                      counts.GetValueOrDefault(k)))
            .OrderByDescending(l => l.Mine + l.Theirs).ThenBy(l => l.Name).ToList();
    }

    /// <summary>As zonas em palavras, para a barra e para a prova: quantas há, quantas estão disputadas e
    /// qual é a que hoje mais gente disputa.</summary>
    public static string Short(World w, int viewerId)
    {
        var air = AirBoard(w, viewerId); var seas = SeaBoard(w, viewerId);
        int land = w.Zones.Values.Count(z => z.Kind == "terra"), water = w.Zones.Values.Count(z => z.Kind == "mar");
        string hot = air.FirstOrDefault(l => l.Theirs > 0f) is { Id: not null } c
            ? $"{c.Name} ({c.Mine:0.#} nossas contra {c.Theirs:0.#}, céu {c.Who})"
            : air.Count > 0 ? $"{air[0].Name} sem ninguém a disputar" : "nenhuma com asas";
        string mar = seas.Count > 0 ? $"{seas[0].Name} ({seas[0].Mine:0.#} contra {seas[0].Theirs:0.#}, mar {seas[0].Who})" : "nenhuma com esquadras";
        return $"{land} zonas de céu e {water} de mar; ar: {hot}; mar: {mar}";
    }
}
