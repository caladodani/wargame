using Godot;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Folha de serviço de uma divisão: nome de guerra, galões da honra de batalha, estado da tropa,
/// veterania e as fitas das condecorações. Vive à parte porque a mesma tropa aparece em dois sítios — a lista
/// da região e o quadro de honra do país — e o que ela ganhou em campanha tem de ler-se igual nos dois.
///
/// Só lê o World: quem dá as medalhas é o MedalSystem e quem dá o nome é o DivisionHonourSystem.</summary>
public static class DivisionView
{
    /// <summary>Galões da honra: um por patamar, como as barras de manga dos veteranos.</summary>
    public static string Chevrons(int sort) => new string('▮', Math.Clamp(sort, 1, 5));

    /// <summary>Frio nas honras baixas, brasa nas altas — dá para ordenar a tropa de relance.</summary>
    public static Color HonourTint(int sort) => (Math.Clamp(sort, 1, 5)) switch
    {
        1 => new Color(0.58f, 0.68f, 0.80f),
        2 => new Color(0.45f, 0.75f, 0.60f),
        3 => new Color(0.85f, 0.75f, 0.40f),
        4 => new Color(0.92f, 0.60f, 0.32f),
        _ => new Color(0.95f, 0.42f, 0.55f),
    };

    /// <summary>Nome da tropa como ela é chamada no terreno: o do modelo, ou o próprio se já ganhou honra.</summary>
    public static string Title(World w, Division d)
    {
        string name = d.Name ?? "";
        if (name.Length == 0) { try { name = w.Units.GetTemplate(d.TemplateId).Name; } catch { name = "Divisão " + d.Id; } }
        return d.HonourName is null ? name : $"{name} «{d.HonourName}»";
    }

    /// <summary>Cartão completo. `place` mostra onde a tropa está — não interessa na lista da própria região.</summary>
    public static PanelContainer Card(World w, Division d, bool place = true)
    {
        var hon = d.Honour is string id ? w.HonourDefs.GetValueOrDefault(id) : null;
        var tint = hon is null ? Ui.TextDim : HonourTint(hon.Sort);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(hon is null
            ? Ui.Surface with { A = 0.85f }
            : new Color(tint.R * 0.20f, tint.G * 0.18f, tint.B * 0.20f, 0.92f), 8));
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 4); card.AddChild(body);

        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); body.AddChild(head);
        if (hon is not null)
        {
            var galoes = Ui.Lbl(Chevrons(hon.Sort), 16);
            galoes.AddThemeColorOverride("font_color", tint);
            galoes.TooltipText = hon.Description;
            head.AddChild(galoes);
        }
        var title = Ui.Lbl(Title(w, d) + (place && w.Regions.TryGetValue(d.RegionId, out var r) ? $"  ·  {r.Name}" : ""), 18);
        title.AddThemeColorOverride("font_color", hon is null ? Ui.Text : tint);
        head.AddChild(Ui.Grow(title));
        float strength = MedalSystem.Bonus(w, d);
        if (strength > 0f)
        {
            var bonus = Ui.Lbl($"+{strength:P0} força", 16);
            bonus.AddThemeColorOverride("font_color", Ui.Good);
            head.AddChild(bonus);
        }

        body.AddChild(State(w, d));

        var sheet = Sheet(w, d);
        if (sheet.GetChildCount() > 0) body.AddChild(sheet);

        var xp = new HBoxContainer(); xp.AddThemeConstantOverride("separation", 8);
        xp.AddChild(Ui.Lbl($"XP {d.Xp:0}", 15));
        xp.AddChild(Ui.Bar(d.Xp / MathF.Max(1f, w.Rule("xp_max", 100f)), new Color(1f, 0.82f, 0.25f), 150f));
        xp.AddChild(Ui.Lbl($"{d.Battles} batalhas  ·  {d.Captures} regiões tomadas", 15));
        body.AddChild(xp);

        if (hon is not null)
        {
            var note = Ui.Lbl($"Honra de batalha: {hon.Description}  ·  recompõe-se +{DivisionHonourSystem.Bonus(w, d):P0} mais depressa", 14);
            note.AddThemeColorOverride("font_color", tint);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(note);
        }

        var ribbons = Ribbons(w, d);
        if (ribbons.GetChildCount() > 0) body.AddChild(ribbons);
        return card;
    }

    /// <summary>A ficha de combate: os números com que esta divisão bate e aguenta. É a tabela que o HoI4
    /// põe ao lado do desenho do modelo — ataque mole, ataque duro, defesa, rotura, blindagem, perfuração,
    /// dureza, efectivo — e que aqui não existia em sítio nenhum: o cartão dizia como estava a tropa (HP,
    /// organização, abastecimento) e nunca de que era feita a força dela.
    ///
    /// Os números são os do DivisionStatCache, os mesmos que o CombatSystem lê; o nome e a frase de cada um
    /// vêm da tabela unit_stat_def, por isso um stat novo em unit_stat entra na ficha com uma linha de SQL e
    /// zero linhas de C#. Um stat que ainda não pesa em conta nenhuma tem `shown` a 0 e fica de fora.</summary>
    public static HFlowContainer Sheet(World w, Division d)
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        StatBlock st;
        try { st = w.Stats.Get(d.TemplateId); } catch { return flow; }
        foreach (var def in w.UnitStatDefs.Values.Where(x => x.Shown).OrderBy(x => x.Sort))
        {
            float v = st[def.Key];
            var plate = Ui.Counter(Glyph.Make(def.Glyph, 17, Ui.Accent), out var value, out var note);
            value.Text = Value(def, v);
            note.Text = def.Name;
            plate.TooltipText = $"{def.Name}: {Value(def, v)}\n{def.Note}";
            if (v <= 0f) { value.AddThemeColorOverride("font_color", Ui.TextDim); }
            flow.AddChild(plate);
        }
        return flow;
    }

    /// <summary>O número como se lê: casas decimais da tabela, e fatia em percentagem para os que são fatia
    /// (a dureza de uma divisão é «que parte dela é blindada», não um valor).</summary>
    public static string Value(UnitStatDef def, float v)
        => def.Percent ? v.ToString("P0") : v.ToString("F" + Math.Clamp(def.Digits, 0, 3));

    /// <summary>--smoke: desenha a ficha de combate de uma divisão do país e diz o que lá ficou — quantos
    /// números, quantos a tabela tem ao todo, e o mais pesado deles. Desenha mesmo (e liberta) para o
    /// contador acusar uma chapa que não existe ou uma linha que não chega ao cartão.</summary>
    public static string SmokeSheet(World w, int countryId)
    {
        var d = w.Divisions.Values.FirstOrDefault(x => x.CountryId == countryId) ?? w.Divisions.Values.FirstOrDefault();
        if (d is null) return "sem divisões para a ficha de combate";
        var flow = Sheet(w, d);
        int rows = flow.GetChildCount();
        flow.QueueFree();
        int total = w.UnitStatDefs.Count;
        var st = w.Stats.Get(d.TemplateId);
        var top = w.UnitStatDefs.Values.Where(x => x.Shown && !x.Percent)
                   .OrderByDescending(x => st[x.Key]).FirstOrDefault();
        string best = top is null ? "nenhum" : $"{top.Name.ToLowerInvariant()} {Value(top, st[top.Key])}";
        return $"ficha de combate de {Title(w, d)} com {rows} de {total} números da tabela (o mais alto: {best})";
    }

    /// <summary>Efectivos, organização e abastecimento em três barras finas lado a lado.</summary>
    public static HBoxContainer State(World w, Division d)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 8);
        h.AddChild(Ui.Lbl($"HP {d.Hp:0}", 15));
        h.AddChild(Ui.Bar(d.Hp / 100f, Ui.Good, 90f));
        h.AddChild(Ui.Lbl($"Org {d.Org:0}", 15));
        h.AddChild(Ui.Bar(d.Org / 100f, Ui.Accent, 90f));
        var sup = Ui.Lbl($"Sup {d.Supply:0.0}", 15);
        if (d.Supply < 1f) sup.AddThemeColorOverride("font_color", Ui.Danger);
        h.AddChild(sup);
        // cerco: "Sup 0,5" dizia que se comia mal, nunca que não havia por onde sair. Isto diz.
        if (d.Cut)
        {
            int? left = PocketSystem.DaysToSurrender(w, d);
            var ring = Ui.Lbl($"⛓ cercada há {d.PocketDays} d", 15);
            ring.AddThemeColorOverride("font_color", Ui.Danger.Lightened(0.2f));
            ring.TooltipText = "sem cadeia por terra até casa: perde "
                + $"{w.Rule("pocket_attrition", 4f):0.#} de efectivo e {w.Rule("pocket_org", 8f):0.#} de organização por dia"
                + (left is int n ? $"\nbolsa fechada: rende-se dentro de {n} dias" : "\nainda há vizinha nossa por onde romper");
            h.AddChild(ring);
        }
        // trincheira: o que os dias de mãos quietas neste chão já valem a defender (EntrenchSystem)
        if (d.Entrench > 0f)
        {
            var dug = Ui.Lbl($"⛏ {d.Entrench:0.#}  (+{EntrenchSystem.Bonus(w, d) - 1f:P0} a defender)", 15);
            dug.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.45f));
            dug.TooltipText = $"cava-se {w.Rule("entrench_per_day", 0.5f):0.#} por dia parado, até {EntrenchSystem.Max(w, d):0.#} nesta região; marchar perde tudo";
            h.AddChild(dug);
        }
        return h;
    }

    /// <summary>Fitas das condecorações, uma por medalha, da mais baixa para a mais alta. A divisa é a do
    /// grau (MedalView.Insignia) e não um 🎖 igual para todas, e a fita de casa leva o selo ⚜.</summary>
    public static HFlowContainer Ribbons(World w, Division d)
    {
        var flow = new HFlowContainer();
        foreach (var m in d.Medals.Select(id => w.MedalDefs.GetValueOrDefault(id)).OfType<MedalDef>().OrderBy(m => m.Sort))
        {
            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", Ui.Box(RibbonColor(m.Sort), 6));
            var l = Ui.Lbl($"{MedalView.Insignia(m.Sort)} {m.Name}" + (m.CountryTag is null ? "" : " ⚜"), 15);
            string tip = m.Description + (m.CountryTag is null ? "" : $"\ncondecoração de {m.CountryTag}");
            l.TooltipText = tip;
            chip.AddChild(l);
            chip.TooltipText = tip;
            flow.AddChild(chip);
        }
        return flow;
    }

    /// <summary>Cor da fita: quanto mais alta a condecoração, mais quente.</summary>
    public static Color RibbonColor(int sort) => (sort % 5) switch
    {
        1 => new Color(0.35f, 0.45f, 0.60f, 0.75f),
        2 => new Color(0.30f, 0.55f, 0.40f, 0.75f),
        3 => new Color(0.55f, 0.45f, 0.25f, 0.75f),
        4 => new Color(0.60f, 0.35f, 0.25f, 0.75f),
        _ => new Color(0.62f, 0.28f, 0.42f, 0.80f),
    };
}
