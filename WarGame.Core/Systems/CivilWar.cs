using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A guerra civil: o dia em que o país se parte em dois.
///
/// No HoI4 um golpe não é uma troca de cadeiras — é o mapa a rachar. Metade das províncias iça outra
/// bandeira, as guarnições que lá estão mudam de lado, nasce um país com nome e cor próprios e as duas
/// metades batem-se até uma cair. Aqui o golpe era mudo: o `PartySystem` trocava o partido no poder, tirava
/// uns pontos de estabilidade, e o mundo continuava exactamente igual. O país mais instável do mapa nunca
/// dava um único acontecimento que se visse.
///
/// Agora o golpe só passa por troca de cadeiras em país pequeno (menos de `civil_war_min_regions`
/// províncias). Acima disso rebenta a guerra civil: as províncias mais LONGE da capital levantam-se — que é
/// o que a história faz, o governo segura a capital e perde o interior —, a tropa que lá está muda de
/// bandeira, o país novo herda a ciência e as escolas de guerra do que se partiu (é o mesmo exército, os
/// mesmos quartéis) e leva a sua fatia de homens, de cofre e de armazém. A fatia sai da POPULARIDADE do
/// partido levantado, travada entre `civil_war_share_min` e `civil_war_share_max`.
///
/// A cara do país que nasce — nome, tag, cor no mapa, chapa e a manchete do dia — vem da tabela
/// `rebel_style`, uma linha por partido. Um partido sem linha lá não chega a partir o país.
///
/// A identidade do rebelde é a única que não vem da static.db: vive no save, na tabela `s_rebel`.</summary>
public static class CivilWar
{
    /// <summary>A cara do levantamento deste partido, ou null se ele não tem forma de se levantar.</summary>
    public static RebelStyleDef? Style(World w, string party) => w.RebelStyles.GetValueOrDefault(party);

    /// <summary>As províncias que o país controla e governa de facto — as que estão em jogo no dia da
    /// ruptura. Terra ocupada a outros não se levanta: quem lá manda é o exército, não a política.</summary>
    public static List<Region> Held(World w, Country c) =>
        w.Regions.Values.Where(r => r.OwnerId == c.Id && r.ControllerId == c.Id).ToList();

    /// <summary>Este golpe parte o país? Precisa de um partido com cara na tabela, de terra que chegue para
    /// dois governos, e de um país que não seja já ele próprio filho de um levantamento a decorrer.</summary>
    public static bool Erupts(World w, Country c, string party)
    {
        if (Style(w, party) is null || c.Capitulated) return false;
        if (w.Countries.Values.Any(x => x.RebelOf == c.Id && !x.Capitulated)) return false;   // já se partiu uma vez
        return Held(w, c).Count >= (int)w.Rule("civil_war_min_regions", 4f);
    }

    /// <summary>A fatia do país que se levanta, da popularidade do partido travada pelas regras.</summary>
    public static float Share(World w, Country c, string party)
    {
        float pop = c.Parties.GetValueOrDefault(party) / 100f;
        return Math.Clamp(pop, w.Rule("civil_war_share_min", 0.2f), w.Rule("civil_war_share_max", 0.65f));
    }

    /// <summary>As províncias que se levantam: as mais longe da capital primeiro, e sempre pelo menos uma —
    /// nunca todas, que o governo tem de ficar com casa. Determinístico (desempate pelo id): o mesmo save
    /// parte-se sempre da mesma maneira.</summary>
    public static List<Region> RebelRegions(World w, Country c, float share)
    {
        var held = Held(w, c);
        if (held.Count < 2) return new List<Region>();
        var capital = w.Regions.GetValueOrDefault(c.CapitalRegionId) ?? held[0];
        int take = Math.Clamp((int)MathF.Round(held.Count * share), 1, held.Count - 1);
        return held.OrderByDescending(r => World.Km(capital, r)).ThenBy(r => r.Id).Take(take).ToList();
    }

    /// <summary>Parte o país em dois e devolve o que nasceu (null se não havia condições). Faz tudo o que o
    /// dia da ruptura faz: terra, tropa, ciência, homens, cofre, armazém, governo dos dois lados e a guerra
    /// entre eles. Quem chama fica só com a manchete.</summary>
    public static Country? Erupt(World w, Country c, string party)
    {
        if (!Erupts(w, c, party) || Style(w, party) is not RebelStyleDef style) return null;
        var mine = RebelRegions(w, c, Share(w, c, party));
        if (mine.Count == 0) return null;

        var rebel = new Country
        {
            Id = w.Countries.Keys.Max() + 1, Tag = FreeTag(w, style, c), Name = style.Name.Replace("{pais}", c.Name),
            Colour = style.Colour,
        };
        rebel.RebelOf = c.Id;
        rebel.BornDay = w.Clock.Day;
        w.Countries[rebel.Id] = rebel;

        // o exército é o mesmo exército: leva a ciência, as escolas de guerra e as características de casa
        foreach (var (k, v) in c.Stats.All) rebel.Stats[k] = v;
        foreach (var t in c.Techs) rebel.Techs.Add(t);
        foreach (var d in c.Doctrines) rebel.Doctrines.Add(d);

        float frac = (float)mine.Count / Math.Max(1, Held(w, c).Count);
        rebel.Money = c.Money * frac; c.Money -= rebel.Money;
        rebel.Manpower = MathF.Max(0f, c.Manpower) * frac; if (c.Manpower > 0f) c.Manpower -= rebel.Manpower;
        foreach (var (unit, qty) in c.Stock.ToList())
        {
            rebel.Stock[unit] = qty * frac; c.Stock[unit] = qty - rebel.Stock[unit];
            if (c.StockMark.TryGetValue(unit, out var mk)) rebel.StockMark[unit] = mk;
        }
        rebel.Stability = w.Rule("civil_war_rebel_stability", 45f);
        rebel.Political = w.Rule("civil_war_rebel_political", 0f);

        // a terra: a que se levantou passa a ser dele, de dono e de facto
        foreach (var r in mine) { r.OwnerId = rebel.Id; r.ControllerId = rebel.Id; }
        rebel.CapitalRegionId = mine.OrderByDescending(r => r.Population).ThenBy(r => r.Id).First().Id;

        // a tropa que lá estava muda de bandeira (menos os voluntários alheios, que não são desta casa)
        var rebelLand = mine.Select(r => r.Id).ToHashSet();
        foreach (var d in w.Divisions.Values.Where(d => d.CountryId == c.Id && !d.IsVolunteer && rebelLand.Contains(d.RegionId)).ToList())
        {
            w.LeaveGroup(d.Id);
            d.CountryId = rebel.Id;
        }

        // o governo dos dois lados: quem se levanta leva os seus, quem fica expurga o que sobrou
        foreach (var (id, pop) in c.Parties) rebel.Parties[id] = pop;
        rebel.Parties[party] = rebel.Parties.GetValueOrDefault(party) + w.Rule("civil_war_purge", 25f) * 2f;
        World.NormalizeParties(rebel);
        rebel.Party = party;
        c.Parties[party] = MathF.Max(0f, c.Parties.GetValueOrDefault(party) - w.Rule("civil_war_purge", 25f));
        World.NormalizeParties(c);
        c.Stability = MathF.Max(0f, c.Stability - w.Rule("civil_war_stability_hit", 30f));

        w.ApplyTechs(rebel);
        World.ApplyParty(w, rebel);
        World.ApplyParty(w, c);
        w.StartWar(c.Id, rebel.Id);
        return rebel;
    }

    /// <summary>Uma tag de três letras livre para o país novo: a letra do estilo mais as duas primeiras do
    /// pai, e um dígito à frente se já houver alguém com ela (uma guerra civil da guerra civil).</summary>
    public static string FreeTag(World w, RebelStyleDef style, Country parent)
    {
        string head = style.Tag.Length > 0 ? style.Tag[..1] : "R";
        string body = (parent.Tag.Length >= 2 ? parent.Tag[..2] : (parent.Tag + "XX")[..2]).ToUpperInvariant();
        string tag = head + body;
        for (int n = 2; w.Countries.Values.Any(x => x.Tag == tag); n++) tag = head + body[..1] + n;
        return tag;
    }

    /// <summary>O que acontece a este país se o golpe pegar hoje: que partido se levanta, com quantas das
    /// suas províncias, e que fatia isso é. É o aviso que o HoI4 dá antes da coisa rebentar — saber que o
    /// país se vai partir ao meio e por onde vale mais do que qualquer número da ficha.</summary>
    public readonly record struct Forecast(string Party, string PartyName, int Regions, int Held, float Share);

    /// <summary>A previsão da ruptura, ou null se este país não se parte (pequeno de mais, já partido, ou
    /// sem oposição com bandeira de levantamento). Olha para o partido de oposição mais popular, chegue ele
    /// hoje ao limiar do golpe ou não — a previsão serve exactamente para se ver a coisa a chegar.</summary>
    public static Forecast? Coming(World w, Country c)
    {
        var top = c.Parties.Where(kv => kv.Key != c.Party && Style(w, kv.Key) is not null)
                           .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
                           .Select(kv => kv.Key).FirstOrDefault();
        if (top is null || !Erupts(w, c, top)) return null;
        float share = Share(w, c, top);
        var mine = RebelRegions(w, c, share);
        if (mine.Count == 0) return null;
        return new Forecast(top, w.PartyDefs.TryGetValue(top, out var d) ? d.Name : top, mine.Count, Held(w, c).Count, share);
    }

    /// <summary>As duas metades de uma guerra civil a arder: terra e tropa de cada lado. Serve o cartão do
    /// painel — quem está a ganhar a casa vê-se aqui e não na frente.</summary>
    public static (int Regions, int Divisions) Strength(World w, int countryId) =>
        (w.Regions.Values.Count(r => r.ControllerId == countryId),
         w.Divisions.Values.Count(d => d.CountryId == countryId));

    /// <summary>A guerra civil em que este país anda metido, dos dois lados: (rebelde, pai). Null quando o
    /// país não está em nenhuma — a maior parte dos países, a maior parte do tempo.</summary>
    public static (Country Rebel, Country Parent)? Of(World w, int countryId) =>
        Live(w).Where(p => p.Rebel.Id == countryId || p.Parent.Id == countryId)
               .Select(p => ((Country, Country)?)(p.Rebel, p.Parent)).FirstOrDefault();

    /// <summary>A manchete do dia, para a crónica e para o aviso.</summary>
    public static string Headline(World w, Country parent, Country rebel, string party)
    {
        string cry = Style(w, party)?.Cry ?? "o país partiu-se em dois";
        return $"Guerra civil em {parent.Name}: {cry}. Nasceu {rebel.Name}.";
    }

    /// <summary>Guerras civis a decorrer hoje: pares (rebelde, pai) ainda em guerra um com o outro.</summary>
    public static List<(Country Rebel, Country Parent)> Live(World w) =>
        w.Countries.Values.Where(x => x.RebelOf != 0 && w.Countries.ContainsKey(x.RebelOf))
                          .Select(x => (x, w.Countries[x.RebelOf]))
                          .Where(p => w.AreAtWar(p.x.Id, p.Item2.Id))
                          .Select(p => (p.x, p.Item2)).ToList();

    /// <summary>Uma linha para o --smoke: quantos partidos sabem levantar-se, quem no mundo de hoje está
    /// perto do golpe, e as guerras civis a decorrer.</summary>
    public static string Smoke(World w)
    {
        int near = w.Countries.Values.Count(c => !c.Capitulated && PartySystem.CoupCandidate(w, c) is not null);
        var live = Live(w);
        string wars = live.Count == 0 ? "nenhuma a arder"
            : string.Join("; ", live.Take(2).Select(p => $"{p.Rebel.Name} contra {p.Parent.Name}"));
        return $"{w.RebelStyles.Count} partidos com bandeira de levantamento, {near} pa{(near == 1 ? "ís" : "íses")} "
             + $"à beira do golpe, {wars}";
    }
}
