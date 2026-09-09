using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma linha da conta de uma característica: quem multiplica, como se chama e por quanto.
/// Mult é o factor (1.15 = mais 15%, 0.9 = menos 10%).</summary>
public readonly record struct StatLine(string Kind, string Name, float Mult)
{
    /// <summary>Quanto isto muda, em percentagem com sinal (+15, −10).</summary>
    public float Percent => (Mult - 1f) * 100f;
    public bool Helps => Mult >= 1f;
}

/// <summary>De onde vem cada número do país — a conta aberta de uma característica, linha a linha.
///
/// É o que o HoI4 faz com o rato por cima de qualquer valor: abre-se uma caixa que diz esta lei mais 10%,
/// aquela tecnologia mais 15%, este conselheiro mais 5%. É por isso que aquele jogo é legível apesar de ter
/// centenas de modificadores. Aqui os números saíam certos e MUDOS: `Country.Stat` multiplica oito
/// dicionários (TechMult, ResourceMult, BuildingMult, DecisionMult, GeneralMult, PrisonerMult, CabinetMult,
/// PartyMult) e o TechMult sozinho já mistura tecnologias, focos, escolas de guerra, leis e acontecimentos.
/// Nenhum ecrã sabia responder "porquê?".
///
/// Isto refaz a mesma conta pela origem em vez de pelo bolo: cada tecnologia, cada foco, cada lei, cada
/// notícia, cada decisão, cada conselheiro, cada comandante, o governo, cada recurso, cada edifício e os
/// prisioneiros dão uma linha com nome próprio. Estado DERIVADO: não guarda nada, não tem tick. O contrato
/// é o teste: base × todas as linhas TEM de dar exactamente `Country.Stat(key)` — se alguém acrescentar um
/// multiplicador novo ao país e se esquecer de o contar aqui, o teste apanha-o.</summary>
public static class StatLedger
{
    /// <summary>O valor de partida da característica, antes de qualquer multiplicador. Sem linha na ficha
    /// do país vale 1 — que é o mesmo que `Country.Stat` assume quando ninguém lhe passa outro.</summary>
    public static float Base(Country c, string key) => c.Stats.Has(key) ? c.Stats[key] : 1f;

    /// <summary>A conta aberta: uma linha por fonte que multiplica esta característica, pela ordem da
    /// tabela stat_source. Fontes que não mexem nesta chave não aparecem.</summary>
    public static List<StatLine> Lines(World w, Country c, string key)
    {
        var lines = new List<StatLine>();

        // 1..5 — o bolo do TechMult, aberto pelas origens que World.ApplyTechs lá mete
        foreach (var id in c.Techs)
            if (w.Techs.TryGetValue(id, out var t) && World.TechIsFor(t, c) && w.TechEffects.TryGetValue(id, out var effs))
                Add(lines, "tecnologia", t.Name, effs, key);
        foreach (var id in c.FocusesDone)
            if (w.FocusEffects.TryGetValue(id, out var effs))
                Add(lines, "foco", w.Focuses.TryGetValue(id, out var f) ? f.Name : id, effs, key);
        foreach (var id in c.Doctrines)
            if (w.ArmyDoctrines.TryGetValue(id, out var d) && World.DoctrineIsFor(d, c)
                && w.DoctrineEffects.TryGetValue(id, out var effs))
                Add(lines, "doutrina", d.Name, effs, key);
        foreach (var grp in w.LawGroups(c))
            if (w.ActiveLaw(c, grp) is Law law && w.LawEffects.TryGetValue(law.Id, out var effs))
                Add(lines, "lei", law.Name, effs, key);
        foreach (var e in w.NewsEvents.Values)
        {
            if (!w.NewsHit(e, c)) continue;
            if (w.NewsEffects.TryGetValue(e.Id, out var effs)) Add(lines, "noticia", e.Title, effs, key);
            if (w.NewsChoices.TryGetValue(e.Id, out var opt) && w.NewsOptionEffects.TryGetValue(opt, out var oeffs))
                Add(lines, "noticia", e.Title, oeffs, key);
        }

        // 6 — decisões assinadas e a correr
        foreach (var a in w.ActiveDecisions.Where(a => a.CountryId == c.Id))
            if (w.DecisionEffects.TryGetValue(a.DecisionId, out var effs))
                Add(lines, "decisao", w.DecisionDefs.TryGetValue(a.DecisionId, out var def) ? def.Name : a.DecisionId, effs, key);

        // 7 — o gabinete, com a rodagem de cada pasta (é ela que faz o conselheiro velho valer mais)
        foreach (var (slot, id) in c.Cabinet)
            if (w.AdvisorDefs.TryGetValue(id, out var a) && a.Effects.TryGetValue(key, out var mult))
            {
                float factor = 1f + w.Rule("advisor_tenure_bonus", 0.5f) * World.CabinetTenure(w, c, slot);
                One(lines, "conselheiro", a.Name, 1f + (mult - 1f) * factor);
            }

        // 8 — comandantes ao serviço do país (um destacado para um grupo já não conta para a casa)
        var detached = w.ArmyGroups.Values.Where(x => x.CountryId == c.Id && x.GeneralId is not null)
                                          .Select(x => x.GeneralId!).ToHashSet();
        foreach (var id in c.Generals)
            if (!detached.Contains(id) && !w.IsWounded(c.Id, id) && w.GeneralDefs.TryGetValue(id, out var g)
                && World.GeneralIsFor(g, c) && g.StatKey == key)
                One(lines, "general", g.Name, g.Mult);

        // 9 — quem governa
        if (w.PartyDefs.TryGetValue(c.Party, out var p) && p.StatKey == key && !string.IsNullOrEmpty(p.StatKey))
            One(lines, "partido", p.Name, p.StatMult);

        // 10 — recursos à mão (a mesma conta do ResourceSystem: o que se controla, mais o que se compra)
        if (!c.Capitulated)
            foreach (var def in w.ResourceDefs.Values)
            {
                if (def.StatKey != key) continue;
                float units = MathF.Min(MathF.Max(ResourceSystem.Available(w, c.Id, def.Id), 0f), def.Cap);
                if (units <= 0f) continue;
                One(lines, "recurso", $"{def.Name} ({units:0.#})", 1f + def.PerUnit * units);
            }

        // 11 — edifícios levantados na terra que controlamos, juntos por tipo
        var built = new Dictionary<string, float>();
        foreach (var r in w.Regions.Values)
        {
            if (r.Buildings.Count == 0 || r.ControllerId != c.Id) continue;
            foreach (var (bid, lvl) in r.Buildings)
                if (w.BuildingDefs.TryGetValue(bid, out var def) && def.StatKey == key)
                    built[bid] = built.GetValueOrDefault(bid, 1f) * (1f + def.PerLevel * lvl);
        }
        foreach (var (bid, mult) in built)
            One(lines, "edificio", w.BuildingDefs.TryGetValue(bid, out var bd) ? bd.Name : bid, mult);

        // 12 — braços presos: os prisioneiros que trabalham
        if (c.PrisonerMult.TryGetValue(key, out var pm) && MathF.Abs(pm - 1f) > 0.0001f)
            One(lines, "prisioneiro", "prisioneiros a trabalhar", pm);

        int Rank(string kind) => w.StatSourceDefs.TryGetValue(kind, out var s) ? s.Sort : 99;
        lines.Sort((a, b) => Rank(a.Kind) != Rank(b.Kind) ? Rank(a.Kind).CompareTo(Rank(b.Kind))
                                                          : MathF.Abs(b.Mult - 1f).CompareTo(MathF.Abs(a.Mult - 1f)));
        return lines;
    }

    private static void Add(List<StatLine> lines, string kind, string name,
                            IEnumerable<(string Key, float Mul)> effects, string key)
    {
        foreach (var (k, mul) in effects) if (k == key) One(lines, kind, name, mul);
    }

    private static void One(List<StatLine> lines, string kind, string name, float mult)
    {
        if (MathF.Abs(mult - 1f) > 0.000001f) lines.Add(new StatLine(kind, name, mult));
    }

    /// <summary>A conta fechada: o valor de partida vezes todas as linhas. Tem de dar o mesmo que
    /// `Country.Stat(key)` — é isso que o teste guarda.</summary>
    public static float Total(World w, Country c, string key)
    {
        float v = Base(c, key);
        foreach (var l in Lines(w, c, key)) v *= l.Mult;
        return v;
    }

    /// <summary>As características que vale a pena mostrar deste país: as que a tabela country_stat_def
    /// nomeia, mais qualquer outra que alguém já esteja a multiplicar. Pela ordem da tabela.</summary>
    public static List<string> Keys(World w, Country c)
    {
        var keys = new HashSet<string>(w.CountryStatDefs.Keys);
        foreach (var d in new[] { c.TechMult, c.ResourceMult, c.BuildingMult, c.DecisionMult,
                                  c.GeneralMult, c.PrisonerMult, c.CabinetMult, c.PartyMult })
            foreach (var k in d.Keys) keys.Add(k);
        int Rank(string k) => w.CountryStatDefs.TryGetValue(k, out var d) ? d.Sort : 999;
        return keys.OrderBy(Rank).ThenBy(k => k, StringComparer.Ordinal).ToList();
    }

    /// <summary>As características deste país que alguém está mesmo a mexer (as que têm linhas).</summary>
    public static List<string> Touched(World w, Country c) =>
        Keys(w, c).Where(k => Lines(w, c, k).Count > 0).ToList();

    /// <summary>O nome de uma característica, como a tabela lhe chama.</summary>
    public static string Name(World w, string key) =>
        w.CountryStatDefs.TryGetValue(key, out var d) ? d.Name : key;

    /// <summary>Uma linha para o --smoke: quantas características o país tem mexidas, quantas fontes ao
    /// todo e qual é a conta mais cheia.</summary>
    public static string Smoke(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "sem país";
        var touched = Touched(w, c);
        int sources = touched.Sum(k => Lines(w, c, k).Count);
        var families = touched.SelectMany(k => Lines(w, c, k)).Select(l => l.Kind).Distinct().Count();
        string worst = "nenhuma";
        int most = 0;
        foreach (var k in touched)
        {
            int n = Lines(w, c, k).Count;
            if (n <= most) continue;
            most = n;
            worst = $"{Name(w, k)} com {n} fontes (de {Base(c, k):0.##} para {Total(w, c, k):0.##})";
        }
        return $"{w.StatSourceDefs.Count} famílias de fonte na tabela, {touched.Count}/{Keys(w, c).Count} "
             + $"características mexidas por {sources} fontes de {families} famílias, a mais cheia é {worst}";
    }
}
