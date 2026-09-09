using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A cara da ruptura: o cartão que diz que o país está a um passo de se partir em dois, ou que já
/// se partiu e como estão as duas metades.
///
/// É a leitura que faltava ao golpe. Antes, um país instável era uma barra de estabilidade a descer e mais
/// nada; no dia do golpe trocava-se o nome do governo e o mapa nem piscava. Agora o painel avisa ANTES —
/// quantas províncias se levantam, quais (as mais longe da capital), com que partido e com que bandeira —
/// e depois do estoiro mostra as duas metades lado a lado: terra e tropa de cada uma.
///
/// Só desenha; a conta é toda do CivilWar (WarGame.Core).</summary>
public static class CivilWarView
{
    /// <summary>O cartão, ou null quando não há ruptura nenhuma à vista neste país.</summary>
    public static PanelContainer? Card(World w, Country c)
    {
        var live = CivilWar.Of(w, c.Id);
        var coming = live is null ? CivilWar.Coming(w, c) : null;
        if (live is null && coming is null) return null;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.20f, 0.11f, 0.11f, 0.92f), 10));
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", 4);
        card.AddChild(v);

        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        head.AddChild(Glyph.Make("brecha", 22, Ui.Danger, "guerra civil"));
        var title = Ui.Lbl(live is not null ? "Guerra civil" : "O país está a rachar", 17);
        title.AddThemeColorOverride("font_color", Ui.Danger);
        head.AddChild(Ui.Grow(title));

        if (live is (Country rebel, Country parent))
        {
            int days = Math.Max(0, w.Clock.Day - rebel.BornDay);
            head.AddChild(Ui.Lbl($"há {days} dia{(days == 1 ? "" : "s")}", 14));
            v.AddChild(Side(w, parent, "o governo"));
            v.AddChild(Side(w, rebel, "o levantamento"));
            v.AddChild(Note(rebel.Id == c.Id
                ? $"Esta bandeira nasceu no dia {rebel.BornDay} da metade de {parent.Name} que se recusou a obedecer."
                : $"{rebel.Name} governa a terra que se levantou. Enquanto a guerra durar, o país é dois países."));
        }
        else if (coming is CivilWar.Forecast f)
        {
            var line = Ui.Lbl($"{f.Regions} das {f.Held} províncias · {f.Share:P0}", 15);
            line.AddThemeColorOverride("font_color", Ui.Danger);
            head.AddChild(line);
            v.AddChild(Note($"Se o golpe pegar, quem se levanta são {f.PartyName}: as {f.Regions} províncias mais "
                          + "longe da capital passam para o outro lado com a tropa que lá estiver, e as duas metades "
                          + "ficam em guerra no mesmo dia. O governo fica com a capital e com o que estiver à volta dela."));
            if (w.RebelStyles.GetValueOrDefault(f.Party) is RebelStyleDef style)
            {
                var born = new HBoxContainer(); born.AddThemeConstantOverride("separation", 8); v.AddChild(born);
                if (style.Glyph.Length > 0) born.AddChild(Glyph.Make(style.Glyph, 18, Ui.TextDim));
                var name = Ui.Lbl("nasceria: " + style.Name.Replace("{pais}", c.Name), 14);
                name.AddThemeColorOverride("font_color", Colour(style.Colour));
                born.AddChild(Ui.Grow(name));
            }
        }
        return card;
    }

    /// <summary>Uma metade: bandeira, nome, terra e tropa. As duas linhas juntas são o placar da guerra.</summary>
    private static Control Side(World w, Country c, string what)
    {
        var (regions, divisions) = CivilWar.Strength(w, c.Id);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        var dot = new ColorRect { Color = Colour(c.Colour), CustomMinimumSize = new Vector2(12, 12) };
        row.AddChild(dot);
        var name = Ui.Lbl(c.Name, 15);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(name);
        var side = Ui.Lbl(what, 13); side.AddThemeColorOverride("font_color", Ui.TextDim);
        row.AddChild(side);
        row.AddChild(Ui.Lbl($"{regions} prov · {divisions} div", 14));
        return row;
    }

    private static Control Note(string text)
    {
        var l = Ui.Lbl(text, 13);
        l.AddThemeColorOverride("font_color", Ui.TextDim);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return l;
    }

    private static Color Colour(string hex)
    {
        if (hex.Length == 0) return Ui.TextDim;
        try { return new Color(hex); } catch { return Ui.TextDim; }
    }
}
