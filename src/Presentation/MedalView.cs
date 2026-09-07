using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>O medalheiro do país: a vitrine com as fitas que aquele exército prega, do primeiro combate à
/// ordem mais alta, e quantas divisões já as trazem.
///
/// As condecorações estavam nos cartões das divisões e em mais nada: via-se o que uma divisão tinha
/// ganho, nunca o que havia para ganhar. E desde que cada bandeira passou a ter as suas
/// (medal.country_tag), o jogador não tinha onde ver que a divisão portuguesa recebe a Cruz de Guerra
/// onde a alemã recebe a Gefechtsmedaille. A vitrine mostra o medalheiro inteiro — o que se pede a cada
/// grau, o que ele vale em campo e quantas divisões o têm — com o selo ⚜ quando as fitas são de casa.</summary>
public static class MedalView
{
    /// <summary>A vitrine de um país: cabeçalho com a contagem e uma chapa por condecoração.</summary>
    public static PanelContainer Case(World w, Country c)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.12f, 0.12f, 0.14f, 0.94f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 5); card.AddChild(v);

        var set = w.Medals(c);
        bool national = w.HasOwnMedals(c);
        var divs = w.Divisions.Values.Where(d => d.CountryId == c.Id).ToList();
        int decorated = divs.Count(d => d.Medals.Count > 0);

        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8);
        var title = Ui.Lbl("Medalheiro", 17);
        title.AddThemeColorOverride("font_color", Ui.Accent);
        head.AddChild(Ui.Grow(title));
        head.AddChild(Tally($"{decorated}/{divs.Count} divisões condecoradas"));
        if (national) head.AddChild(Seal(c));
        v.AddChild(head);

        if (set.Count == 0)
        {
            var none = Ui.Lbl("sem condecorações na base de dados", 14);
            none.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(none);
            return card;
        }

        foreach (var m in set) v.AddChild(Plate(w, c, m, divs, national));

        var foot = Ui.Lbl(national
            ? $"as fitas de {c.Name} substituem as comuns: pedem a mesma guerra e valem o mesmo"
            : "fitas comuns: quem não traz as suas condecora por estas", 13);
        foot.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(foot);
        return card;
    }

    /// <summary>Uma chapa da vitrine: a divisa do grau, o nome, o que se pede, o que vale e quem a traz.</summary>
    private static PanelContainer Plate(World w, Country c, MedalDef m, List<Division> divs, bool national)
    {
        var tint = DivisionView.RibbonColor(m.Sort);
        var wearers = divs.Where(d => d.Medals.Contains(m.Id)).ToList();

        var plate = new PanelContainer();
        plate.AddThemeStyleboxOverride("panel", Ui.Box(new Color(tint, wearers.Count > 0 ? 0.30f : 0.12f), 6));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); plate.AddChild(row);

        var badge = Ui.Lbl(Insignia(m.Sort), 22);
        badge.AddThemeColorOverride("font_color", wearers.Count > 0 ? Ui.Text : Ui.TextDim);
        row.AddChild(badge);

        var cell = new VBoxContainer(); cell.AddThemeConstantOverride("separation", 1); row.AddChild(Ui.Grow(cell));
        var name = Ui.Lbl(national ? $"{m.Name} ⚜" : m.Name, 15);
        name.AddThemeColorOverride("font_color", wearers.Count > 0 ? Ui.Text : Ui.TextDim);
        cell.AddChild(name);
        var ask = Ui.Lbl($"grau {m.Sort} · {Demand(m)} · +{m.Bonus:P0} de força", 13);
        ask.AddThemeColorOverride("font_color", Ui.TextDim);
        cell.AddChild(ask);
        if (wearers.Count > 0)
        {
            var who = wearers.OrderByDescending(d => d.Xp).Take(3).Select(d => DivisionView.Title(w, d)).ToList();
            var line = Ui.Lbl(string.Join(", ", who) + (wearers.Count > who.Count ? $" e mais {wearers.Count - who.Count}" : ""), 13);
            line.AddThemeColorOverride("font_color", tint.Lightened(0.35f));
            cell.AddChild(line);
        }

        var count = new VBoxContainer(); count.AddThemeConstantOverride("separation", 1); row.AddChild(count);
        var n = Ui.Lbl($"{wearers.Count}", 17);
        n.AddThemeColorOverride("font_color", wearers.Count > 0 ? Ui.Accent : Ui.TextDim);
        n.HorizontalAlignment = HorizontalAlignment.Right;
        count.AddChild(n);
        count.AddChild(Ui.Bar(divs.Count == 0 ? 0f : (float)wearers.Count / divs.Count, tint.Lightened(0.25f), 90));

        plate.TooltipText = $"{m.Name} — {m.Description}\n{Demand(m)}; soma {m.Bonus:P0} à força da divisão, "
                          + $"até ao tecto de {w.Rule("medal_bonus_max", 0.12f):P0} de todas juntas"
                          + (national ? $"\ncondecoração de {c.Name}: nenhuma outra bandeira a prega" : "");
        return plate;
    }

    /// <summary>O que a fita pede, em palavras — a coluna metric/threshold da tabela medal.</summary>
    private static string Demand(MedalDef m) => m.Metric switch
    {
        "battles" => $"{m.Threshold:0} batalha{(m.Threshold == 1 ? "" : "s")} travada{(m.Threshold == 1 ? "" : "s")}",
        "captures" => $"{m.Threshold:0} região{(m.Threshold == 1 ? "" : "s")} tomada{(m.Threshold == 1 ? "" : "s")}",
        "xp" => $"{m.Threshold:0} de experiência",
        _ => $"{m.Metric} {m.Threshold:0}",
    };

    /// <summary>A divisa do grau. É a mesma para a fita comum e para a nacional do mesmo grau, de propósito:
    /// o que muda entre bandeiras é o nome, não o que a divisão fez para a ganhar.</summary>
    public static string Insignia(int sort) => (sort % 5) switch
    {
        1 => "🎗",
        2 => "🎖",
        3 => "🏅",
        4 => "🛡",
        _ => "⭐",
    };

    private static PanelContainer Tally(string text)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface, 4));
        var l = Ui.Lbl(text, 13);
        l.AddThemeColorOverride("font_color", Ui.TextDim);
        chip.AddChild(l);
        return chip;
    }

    private static PanelContainer Seal(Country c)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 4));
        var l = Ui.Lbl($"⚜ fitas de {c.Tag}", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"condecorações de {c.Name}: nenhum outro exército prega estas";
        chip.AddChild(l);
        return chip;
    }
}
