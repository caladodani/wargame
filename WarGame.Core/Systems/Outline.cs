using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma linha da coluna do estado: o que está a andar, quanto já andou e quantos dias faltam.
/// Progress é 0..1 e vale −1 para "isto não tem barra" (uma asa no céu não está a caminho de lado nenhum,
/// está lá). Days é 0 quando não há conta de dias a fazer. RegionId é a região a que a linha pertence (0 =
/// nenhuma) e é o que o mapa usa para saltar lá.</summary>
public readonly record struct OutlineRow(string Section, string Title, string Note, float Progress, int Days, int RegionId = 0)
{
    public bool HasBar => Progress >= 0f;
}

/// <summary>A coluna do estado do HoI4 (o "outliner"), derivada — não guarda nada, não tem tick, lê o mundo
/// e responde. O jogo já tinha tudo isto: o foco no ecrã dos focos, a investigação na ficha do país, a fila
/// na produção, as obras dentro de cada província, os exércitos no painel deles, as asas e as esquadras no
/// painel da guerra. O que faltava era vê-lo TUDO ao mesmo tempo, sem abrir nada — que é exactamente o que
/// aquela coluna à direita do mapa faz e porque é que ela é o móvel mais usado daquele jogo.
///
/// As secções vêm da tabela outline_section (nome, chapa, ordem e o ecrã que o toque abre): nenhuma lista
/// de secções vive aqui. O que vive aqui é como se conta o progresso de cada família de coisas.</summary>
public static class Outline
{
    /// <summary>As secções da coluna, pela ordem da tabela.</summary>
    public static List<OutlineSectionDef> Sections(World w) =>
        w.OutlineSections.Values.OrderBy(s => s.Sort).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();

    /// <summary>As linhas de uma secção para um país. Secção desconhecida dá lista vazia.</summary>
    public static List<OutlineRow> Rows(World w, Country c, string section)
    {
        var rows = new List<OutlineRow>();
        switch (section)
        {
            case "foco": Focus(w, c, rows); break;
            case "investigacao": Research(w, c, rows); break;
            case "producao": Production(w, c, rows); break;
            case "obras": Works(w, c, rows); break;
            case "exercitos": Armies(w, c, rows); break;
            case "ar": Air(w, c, rows); break;
            case "mar": Sea(w, c, rows); break;
            case "missoes": Missions(w, c, rows); break;
        }
        return rows;
    }

    /// <summary>A coluna inteira: cada secção com as linhas que tem hoje. Secções vazias vêm na mesma — a
    /// coluna do HoI4 mostra a ranhura vazia, que é meio ponto do que ela serve (ver o que falta fazer).</summary>
    public static List<(OutlineSectionDef Sec, List<OutlineRow> Rows)> Board(World w, int countryId)
    {
        var board = new List<(OutlineSectionDef, List<OutlineRow>)>();
        if (!w.Countries.TryGetValue(countryId, out var c)) return board;
        foreach (var s in Sections(w)) board.Add((s, Rows(w, c, s.Id)));
        return board;
    }

    /// <summary>Quantas linhas a coluna tem ao todo (o número que a chapa do botão mostra).</summary>
    public static int Count(World w, int countryId)
    {
        int n = 0;
        foreach (var (_, rows) in Board(w, countryId)) n += rows.Count;
        return n;
    }

    // ---- as famílias -------------------------------------------------------

    private static void Focus(World w, Country c, List<OutlineRow> rows)
    {
        if (c.CurrentFocus is string id && w.Focuses.TryGetValue(id, out var f))
        {
            float days = MathF.Max(1f, f.Days);
            rows.Add(new OutlineRow("foco", f.Name, "", Clamp(c.FocusProgress / days),
                                    (int)MathF.Ceiling(MathF.Max(0f, days - c.FocusProgress))));
            return;
        }
        // sem foco escolhido é uma linha na mesma: é assim que se dá pela falta dele
        bool any = w.Focuses.Values.Any(x => x.CountryId == c.Id);
        if (any) rows.Add(new OutlineRow("foco", "Sem foco", "escolher um", -1f, 0));
    }

    private static void Research(World w, Country c, List<OutlineRow> rows)
    {
        float speed = MathF.Max(0.0001f, c.Stat("research_speed"));
        foreach (var (id, done) in c.Research.OrderByDescending(kv => kv.Value))
        {
            if (!w.Techs.TryGetValue(id, out var t)) continue;
            float cost = MathF.Max(1f, t.Cost);
            rows.Add(new OutlineRow("investigacao", t.Name, "", Clamp(done / cost),
                                    (int)MathF.Ceiling(MathF.Max(0f, cost - done) / speed)));
        }
        int free = ResearchSystem.FreeSlots(w, c);
        for (int i = 0; i < free; i++) rows.Add(new OutlineRow("investigacao", "Laboratório parado", "sem nada a estudar", -1f, 0));
    }

    private static void Production(World w, Country c, List<OutlineRow> rows)
    {
        for (int i = 0; i < c.Queue.Count; i++)
        {
            var o = c.Queue[i];
            int lines = ProductionPlan.LinesFor(w, c, i);
            float cost = MathF.Max(1f, w.OrderCost(o, c));
            string note = ProductionPlan.Blocked(w, c, o, lines)
                          ?? (lines == 1 ? "1 fábrica" : $"{lines} fábricas");
            float days = ProductionPlan.Days(w, c, o, lines);
            rows.Add(new OutlineRow("producao", OrderName(w, o), note, Clamp(o.Progress / cost),
                                    days <= 0f || float.IsInfinity(days) ? 0 : (int)MathF.Ceiling(days)));
        }
    }

    /// <summary>O nome do que uma linha da fila fabrica — a divisão, ou o material de um tipo de unidade.</summary>
    public static string OrderName(World w, ProductionOrder o)
    {
        try { return o.IsKit ? "Material · " + w.Units.GetUnitType(o.UnitTypeId).Name : w.Units.GetTemplate(o.TemplateId).Name; }
        catch { return o.IsKit ? "Material U" + o.UnitTypeId : "T" + o.TemplateId; }
    }

    private static void Works(World w, Country c, List<OutlineRow> rows)
    {
        float infra = MathF.Max(1f, w.Rule("infra_build_days", 30f));
        float rail = MathF.Max(1f, w.Rule("rail_days", 20f));
        float fort = MathF.Max(1f, w.Rule("fort_build_days", 20f));
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId != c.Id && r.ProjectOwner != c.Id) continue;
            if (r.Building && r.OwnerId == c.Id)
                rows.Add(Work("Estradas", r.Name, r.BuildProgress, infra, r.Id));
            if (r.RailBuilding && r.OwnerId == c.Id)
                rows.Add(Work("Via férrea", r.Name, r.RailProgress, rail, r.Id));
            if (r.FortBuilding && r.OwnerId == c.Id)
                rows.Add(Work("Fortificação", r.Name, r.FortProgress, fort, r.Id));
            if (r.Project is string p && w.BuildingDefs.TryGetValue(p, out var def)
                && (r.ProjectOwner == c.Id || (r.ProjectOwner == 0 && r.OwnerId == c.Id)))
                rows.Add(Work(def.Name, r.Name, r.ProjectProgress, MathF.Max(1f, def.Days), r.Id));
        }
        rows.Sort((a, b) => a.Days.CompareTo(b.Days));
    }

    private static OutlineRow Work(string what, string where, float done, float days, int regionId) =>
        new("obras", what, where, Clamp(done / days), (int)MathF.Ceiling(MathF.Max(0f, days - done)), regionId);

    private static void Armies(World w, Country c, List<OutlineRow> rows)
    {
        float max = MathF.Max(0.0001f, w.Rule("planning_max", 1f));
        foreach (var g in w.ArmyGroups.Values.Where(g => g.CountryId == c.Id).OrderBy(g => g.Id))
        {
            string front = g.FrontCountryId is int fid && w.Countries.TryGetValue(fid, out var e) ? e.Name : "sem frente";
            string note = $"{g.Divisions.Count} div · {StanceName(g.Stance)} · {front}";
            // a barra do exército é o plano de batalha: é a única coisa dele que enche com o tempo
            bool plans = g.NeedsFront && g.FrontCountryId is not null;
            rows.Add(new OutlineRow("exercitos", g.Name, note, plans ? Clamp(g.Planning / max) : -1f, 0));
        }
    }

    /// <summary>O nome da postura de um grupo, como se lê na coluna.</summary>
    public static string StanceName(GroupStance s) => s switch
    {
        GroupStance.Advance => "a avançar",
        GroupStance.Defend => "a defender",
        GroupStance.Reserve => "em reserva",
        _ => "parado",
    };

    private static void Air(World w, Country c, List<OutlineRow> rows)
    {
        foreach (var m in w.AirMissions.Where(m => m.CountryId == c.Id))
        {
            string what = w.AirMissionDefs.TryGetValue(m.MissionId, out var d) ? d.Name : m.MissionId;
            string where = w.Regions.TryGetValue(m.RegionId, out var r) ? r.Name : "?";
            rows.Add(new OutlineRow("ar", m.Name.Length > 0 ? m.Name : what,
                                    $"{what} · {where} · {m.Wings:0.#} asas", -1f, 0, m.RegionId));
        }
    }

    private static void Sea(World w, Country c, List<OutlineRow> rows)
    {
        foreach (var m in w.NavalMissions.Where(m => m.CountryId == c.Id))
        {
            string what = w.NavalMissionDefs.TryGetValue(m.MissionId, out var d) ? d.Name : m.MissionId;
            string where = w.Regions.TryGetValue(m.RegionId, out var r) ? r.Name : "?";
            rows.Add(new OutlineRow("mar", m.Name.Length > 0 ? m.Name : what,
                                    $"{what} · {where} · {m.Ships:0.#} navios", -1f, 0, m.RegionId));
        }
    }

    private static void Missions(World w, Country c, List<OutlineRow> rows)
    {
        foreach (var a in Decisions.Missions(w, c.Id))
        {
            if (!w.DecisionDefs.TryGetValue(a.DecisionId, out var def)) continue;
            rows.Add(new OutlineRow("missoes", def.Name, Decisions.GoalText(w, c, def, a),
                                    Clamp(Decisions.GoalProgress(w, c, def, a)),
                                    Math.Max(0, a.MissionUntil - w.Clock.Day)));
        }
    }

    private static float Clamp(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>Uma linha para o --smoke: quantas secções, quantas linhas e a mais adiantada de cada uma.</summary>
    public static string Smoke(World w, int countryId)
    {
        var board = Board(w, countryId);
        int rows = board.Sum(b => b.Rows.Count), bars = board.Sum(b => b.Rows.Count(r => r.HasBar));
        var busiest = board.Where(b => b.Rows.Count > 0).OrderByDescending(b => b.Rows.Count).FirstOrDefault();
        string top = busiest.Sec is null ? "coluna vazia" : $"a mais cheia é {busiest.Sec.Name} com {busiest.Rows.Count}";
        return $"{board.Count} secções, {rows} linhas ({bars} com barra), {top}";
    }
}
