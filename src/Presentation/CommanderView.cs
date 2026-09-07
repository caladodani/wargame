using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Cara dos comandantes: divisa do posto, cor e o cartão com a barra de experiência até à
/// promoção seguinte. Vive à parte porque dois painéis mostram o mesmo homem — o exército que ele
/// comanda (ArmyPanel) e o estado-maior do país (CountryPanel) — e a divisa tem de ser a mesma nos dois.
///
/// Só lê o World: quem dá a experiência é o GeneralXpSystem.</summary>
public static class CommanderView
{
    /// <summary>Divisa do posto: uma estrela por nível, como nas platinas.</summary>
    public static string Insignia(int level) => new string('★', Math.Clamp(level, 1, 6));

    /// <summary>Prata nos postos baixos, ouro no topo — dá para ver a folha de serviço de relance.</summary>
    public static Color Tint(int level, int top) => top <= 1
        ? new Color(1f, 0.82f, 0.25f)
        : new Color(0.78f, 0.80f, 0.86f).Lerp(new Color(1f, 0.78f, 0.20f), Math.Clamp((level - 1f) / (top - 1f), 0f, 1f));

    public static int TopLevel(World w) => w.GeneralRanks.Count == 0 ? 1 : w.GeneralRanks.Max(r => r.Level);

    /// <summary>Nome do posto ("Marechal") ou vazio se a tabela de postos não estiver carregada.</summary>
    public static string RankName(World w, int countryId, string generalId) =>
        w.RankOf(countryId, generalId)?.Name ?? "";

    /// <summary>Cartão do comandante destacado: posto, efeito amplificado e a barra do que falta para
    /// subir. A experiência era um número invisível; posta assim, o jogador percebe porque é que vale a
    /// pena deixar o mesmo homem à frente do mesmo exército campanha fora.</summary>
    public static PanelContainer Card(World w, int countryId, GeneralDef def, string effect)
    {
        var rank = w.RankOf(countryId, def.Id);
        int level = rank?.Level ?? 1;
        var tint = Tint(level, TopLevel(w));

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.16f, 0.14f, 0.09f, 0.92f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); v.AddChild(top);
        var badge = Ui.Lbl(Insignia(level), 17);
        badge.AddThemeColorOverride("font_color", tint);
        top.AddChild(badge);
        var title = Ui.Lbl(rank is null ? def.Name : $"{def.Name} · {rank.Name}", 17);
        title.AddThemeColorOverride("font_color", tint);
        top.AddChild(Ui.Grow(title));

        bool hurt = w.IsWounded(countryId, def.Id);
        if (hurt) card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.20f, 0.10f, 0.11f, 0.94f), 10));

        var eff = Ui.Lbl(hurt ? "Fora de serviço — o exército não leva nada do que ele vale" : effect, 15);
        eff.AddThemeColorOverride("font_color", hurt ? Ui.Danger : Ui.Text);
        v.AddChild(eff);

        v.AddChild(hurt ? Recovery(w, countryId, def.Id) : Progress(w, countryId, def.Id, tint));
        return card;
    }

    /// <summary>Barra de experiência até ao posto seguinte, com a legenda por baixo. No topo da carreira
    /// fica cheia e diz que já não há mais nada a ganhar.</summary>
    public static VBoxContainer Progress(World w, int countryId, string generalId, Color tint)
    {
        float xp = w.Countries.TryGetValue(countryId, out var c) ? c.GeneralXp.GetValueOrDefault(generalId) : 0f;
        var rank = w.RankOf(countryId, generalId);
        var next = w.NextRank(countryId, generalId);

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2);
        float from = rank?.Xp ?? 0f;
        float fill = next is null ? 1f : Math.Clamp((xp - from) / MathF.Max(1f, next.Xp - from), 0f, 1f);
        v.AddChild(Ui.Bar(fill, tint, 0f));

        string lead = next is not null && fill >= 0.75f ? "quase" : "a caminho de";
        var note = Ui.Lbl(next is null
            ? $"{xp:0} de experiência de campanha — no topo da carreira"
            : $"{xp:0}/{next.Xp:0} de experiência — {lead} {next.Name}", 14);
        note.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(note);
        return v;
    }

    /// <summary>Cor da enfermaria: vermelho apagado, para o comandante ferido não se confundir com o que
    /// está de pé.</summary>
    public static readonly Color Hurt = new(0.92f, 0.45f, 0.42f);

    /// <summary>Marca curta do estado de baixa ("🩸 12 d") ou vazio se o homem está de pé. É o que vai à
    /// frente do nome nas listas do estado-maior.</summary>
    public static string WoundMark(World w, int countryId, string generalId)
    {
        int left = w.WoundDaysLeft(countryId, generalId);
        return left <= 0 ? "" : $"🩸 {left} d ";
    }

    /// <summary>Barra da convalescença: enche à medida que os dias passam, medida contra a baixa mais
    /// longa da tabela. Substitui a barra de carreira enquanto o comandante está no hospital — a carreira
    /// dele está parada, e quem olha para o painel tem de ver isso.</summary>
    public static VBoxContainer Recovery(World w, int countryId, string generalId)
    {
        int left = w.WoundDaysLeft(countryId, generalId);
        float longest = w.WoundKinds.Count == 0 ? 60f : MathF.Max(1f, w.WoundKinds.Values.Max(k => k.Days));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2);
        v.AddChild(Ui.Bar(Math.Clamp(1f - left / longest, 0f, 1f), Hurt, 0f));
        var note = Ui.Lbl(left <= 1 ? "volta ao serviço amanhã" : $"volta ao serviço daqui a {left} dias", 14);
        note.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(note);
        return v;
    }

    /// <summary>O estado-maior inteiro à maneira do HoI4: uma folha de comandantes com retrato emoldurado
    /// em vez da lista corrida de linhas de texto que aqui estava. Cada homem ao serviço leva a chapa dele
    /// numa placa, a divisa do posto, o que multiplica, a linha da folha de serviço e a barra de carreira;
    /// os que faltam contratar aparecem por baixo, separados em dois blocos — os de casa (com o selo ⚜ e a
    /// bandeira do país) e os mercenários, que qualquer estado-maior pode chamar.
    ///
    /// Só lê o World; contratar e dispensar é de quem sabe despachar comandos.</summary>
    public static PanelContainer? Roster(World w, Country c, bool mine, Action<string> onHire, Action<string> onDismiss)
    {
        var pool = w.GeneralPool(c);
        if (pool.Count == 0) return null;
        int slots = (int)w.Rule("general_slots", 3f);
        int top = TopLevel(w);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.14f, 0.13f, 0.10f, 0.94f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 5); card.AddChild(v);

        int home = c.Generals.Count(id => w.GeneralDefs.TryGetValue(id, out var g) && g.CountryTag is not null);
        var head = Ui.Lbl($"🎖 Estado-maior: {c.Generals.Count}/{slots} ao serviço"
                          + (home > 0 ? $" · {home} de casa" : ""), 17);
        head.AddThemeColorOverride("font_color", Ui.Accent);
        v.AddChild(head);

        foreach (var def in pool.Where(g => c.Generals.Contains(g.Id))
                                .OrderByDescending(g => w.RankOf(c.Id, g.Id)?.Level ?? 1).ThenBy(g => g.Name))
        {
            v.AddChild(Ui.Rule());
            bool hurt = w.IsWounded(c.Id, def.Id);
            int level = w.RankOf(c.Id, def.Id)?.Level ?? 1;
            var tint = hurt ? Hurt : Tint(level, top);
            // destacado a um grupo: o bónus sai do país e vale, amplificado, só nesse exército
            var posted = w.ArmyGroups.Values.FirstOrDefault(g => g.CountryId == c.Id && g.GeneralId == def.Id);

            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
            row.AddChild(Portrait(def.Icon, tint, hurt));
            var cell = Ui.Grow(new VBoxContainer()); cell.AddThemeConstantOverride("separation", 2);

            var title = new HBoxContainer(); title.AddThemeConstantOverride("separation", 8);
            var badge = Ui.Lbl(Insignia(level), 15);
            badge.AddThemeColorOverride("font_color", tint);
            title.AddChild(badge);
            string rank = RankName(w, c.Id, def.Id) is string rn && rn.Length > 0 ? $" · {rn}" : "";
            var who = Ui.Lbl(def.Name + rank, 17);
            who.AddThemeColorOverride("font_color", tint);
            title.AddChild(Ui.Grow(who));
            if (def.CountryTag is not null) title.AddChild(Seal(c));
            if (mine)
            {
                string id = def.Id;
                title.AddChild(Ui.Btn("Dispensar", () => onDismiss(id), 0, Ui.Kind.Danger));
            }
            cell.AddChild(title);

            var eff = Ui.Lbl(hurt
                ? "🩸 no hospital — o exército não leva nada do que ele vale"
                : posted is null
                    ? $"{Ui.StatName(def.StatKey)} ×{def.Mult:0.00} em todo o país"
                    : $"⚔ {Ui.StatName(def.StatKey)} ×{1f + (def.Mult - 1f) * (w.Rule("general_command_bonus", 2f) + w.RankBonus(c.Id, def.Id)):0.00} no {posted.Name}", 15);
            eff.AddThemeColorOverride("font_color", hurt ? Hurt : Ui.Good);
            cell.AddChild(eff);
            if (def.Note.Length > 0 && !hurt)
            {
                var note = Ui.Lbl(def.Note, 13);
                note.AddThemeColorOverride("font_color", Ui.TextDim);
                note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                cell.AddChild(note);
            }
            cell.AddChild(hurt ? Recovery(w, c.Id, def.Id) : Progress(w, c.Id, def.Id, tint));
            row.AddChild(cell);
            v.AddChild(row);
        }

        // por contratar: primeiro os de casa, depois os que se contratam em qualquer lado
        var free = pool.Where(g => !c.Generals.Contains(g.Id)).ToList();
        if (mine && free.Count > 0)
        {
            v.AddChild(Ui.Rule());
            foreach (var group in new[] { true, false })
            {
                var men = free.Where(g => (g.CountryTag is not null) == group).ToList();
                if (men.Count == 0) continue;
                var label = Ui.Lbl(group ? $"      ⚜ de casa ({c.Name})" : "      mercenários, de qualquer lado", 13);
                label.AddThemeColorOverride("font_color", group ? Ui.Accent : Ui.TextDim);
                v.AddChild(label);
                foreach (var g in men)
                {
                    string id = g.Id;
                    var b = Ui.Btn($"{g.Icon} {g.Name}   —   {Ui.StatName(g.StatKey)} ×{g.Mult:0.00}   ·   {g.Cost:0} pp",
                                   () => onHire(id), 0, group ? Ui.Kind.Primary : Ui.Kind.Normal);
                    string? why = new HireGeneralCommand(c.Id, id).Validate(w);
                    b.Disabled = why is not null;
                    b.TooltipText = why ?? g.Note;
                    b.AddThemeFontSizeOverride("font_size", 14);
                    v.AddChild(Ui.Grow(b));
                }
            }
        }
        return card;
    }

    /// <summary>Retrato do comandante: a chapa dele numa placa emoldurada, apagada quando está no hospital.</summary>
    private static PanelContainer Portrait(string icon, Color tint, bool hurt)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(54, 54) };
        frame.AddThemeStyleboxOverride("panel", Ui.Box(hurt ? new Color(0.20f, 0.10f, 0.11f, 0.94f) : Ui.Ink, 6));
        var face = Ui.Lbl(icon, 26);
        face.HorizontalAlignment = HorizontalAlignment.Center;
        face.VerticalAlignment = VerticalAlignment.Center;
        face.AddThemeColorOverride("font_color", tint);
        frame.AddChild(face);
        return frame;
    }

    /// <summary>Selo do comandante que é de casa (o mesmo do gabinete e das leis nacionais).</summary>
    private static PanelContainer Seal(Country c)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 4));
        var l = Ui.Lbl($"⚜ {c.Tag}", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"comandante de {c.Name}: nenhum outro estado-maior o chama";
        chip.AddChild(l);
        return chip;
    }

    /// <summary>Enfermaria do estado-maior: quem está fora, há quanto tempo falta e o aviso de que os
    /// exércitos deles andam entregues a interinos. Vazia (null) quando não há baixas — o painel não
    /// mostra secções vazias.</summary>
    public static PanelContainer? Infirmary(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return null;
        var hurt = c.GeneralWound.Where(kv => kv.Value > w.Clock.Day)
                                 .OrderBy(kv => kv.Value).ToList();
        if (hurt.Count == 0) return null;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.20f, 0.10f, 0.11f, 0.90f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);
        var head = Ui.Lbl($"🏥 Enfermaria · {hurt.Count} fora de serviço", 17);
        head.AddThemeColorOverride("font_color", Hurt);
        v.AddChild(head);

        foreach (var (id, until) in hurt)
        {
            string name = w.GeneralDefs.TryGetValue(id, out var def) ? def.Name : id;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
            var who = Ui.Lbl(name, 16);
            who.AddThemeColorOverride("font_color", Ui.Text);
            row.AddChild(Ui.Grow(who));
            var when = Ui.Lbl($"{Math.Max(0, until - w.Clock.Day)} dias", 15);
            when.AddThemeColorOverride("font_color", Hurt);
            row.AddChild(when);
            v.AddChild(Recovery(w, countryId, id));
        }
        return card;
    }
}
