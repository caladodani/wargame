using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>As leis nacionais como escada, à maneira do HoI4. Antes eram uma lista corrida: o nome da lei
/// em vigor numa linha e, por baixo, uma fila de linhas iguais com um botão "Mudar" cada uma — não se via
/// que as leis de um grupo são degraus da mesma escada, nem para que lado é que se sobe, nem o que se ganha
/// e o que se perde ao subir.
///
/// Agora cada grupo é um cartão com a sua escada: um degrau por lei, ordenado do país desmobilizado para o
/// país em pé de guerra, com a fila de lâmpadas a dizer a que altura fica, o degrau em vigor aceso em latão
/// e os efeitos escritos em português por baixo do nome. Cada degrau por ocupar leva a chapa "Subir" com o
/// preço, apagada com a razão no tooltip quando o cofre não chega.
///
/// Só lê o World; mudar de lei é de quem sabe despachar comandos.</summary>
public static class LawsView
{
    /// <summary>Nome e chapa de cada grupo de leis. A base de dados guarda o id do grupo (uma palavra em
    /// inglês, que é a chave); o painel é que sabe como se diz cá.</summary>
    private static readonly Dictionary<string, (string Icon, string Name)> Groups = new()
    {
        ["conscription"] = ("🎖", "Conscrição"),
        ["economy"] = ("🏭", "Economia"),
        ["trade"] = ("⚓", "Comércio"),
        ["security"] = ("🕵", "Segurança"),
        ["occupation"] = ("🏴", "Ocupação"),
        ["doctrine"] = ("⚔", "Doutrina"),
    };

    /// <summary>Ordem por que os cartões aparecem: primeiro os grupos conhecidos, depois o que a base de
    /// dados trouxer de novo, por ordem alfabética.</summary>
    public static List<string> Order(World w)
    {
        var groups = w.Laws.Values.Select(l => l.Group).Distinct().ToList();
        return groups.OrderBy(g => Groups.Keys.ToList().IndexOf(g) is int i && i >= 0 ? i : 99).ThenBy(g => g).ToList();
    }

    public static string GroupName(string group) =>
        Groups.TryGetValue(group, out var meta) ? $"{meta.Icon} {meta.Name}" : group;

    /// <summary>A escada toda: um cartão por grupo de leis. Null quando a base de dados não traz leis.</summary>
    public static VBoxContainer? Cards(World w, Country c, bool mine, Action<string> onChange)
    {
        if (w.Laws.Count == 0) return null;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        foreach (var grp in Order(w)) box.AddChild(Card(w, c, grp, mine, onChange));
        return box;
    }

    /// <summary>Um grupo: cabeçalho com a lei em vigor e a escada dos degraus por baixo.</summary>
    private static PanelContainer Card(World w, Country c, string group, bool mine, Action<string> onChange)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.12f, 0.13f, 0.18f, 0.94f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        var steps = w.Laws.Values.Where(l => l.Group == group).OrderBy(l => l.Sort).ThenBy(l => l.Id).ToList();
        var active = w.ActiveLaw(c, group);
        float cost = w.Rule("law_change_cost", 30f);

        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        var title = Ui.Lbl($"{GroupName(group)}: {active?.Name ?? "—"}", 17);
        title.AddThemeColorOverride("font_color", Ui.Accent);
        head.AddChild(Ui.Grow(title));
        // a que altura da escada é que o país está: um país desmobilizado acende uma lâmpada, um em pé de guerra acende-as todas
        head.AddChild(Ui.Pips(steps.FindIndex(l => l.Id == active?.Id) + 1, steps.Count));

        foreach (var l in steps)
        {
            bool on = l.Id == active?.Id;
            var step = new PanelContainer();
            step.AddThemeStyleboxOverride("panel", Ui.Box(on ? Ui.SurfaceHi : Ui.Surface.Darkened(0.25f), 6));
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); step.AddChild(row);

            var text = new VBoxContainer(); text.AddThemeConstantOverride("separation", 1);
            var name = Ui.Lbl((on ? "▸ " : "   ") + l.Name, 16);
            name.AddThemeColorOverride("font_color", on ? Ui.Accent : Ui.Text);
            text.AddChild(name);
            var eff = Ui.Lbl("      " + Effects(w, l), 14);
            eff.AddThemeColorOverride("font_color", on ? Ui.Good : Ui.TextDim);
            text.AddChild(eff);
            if (l.Description.Length > 0)
            {
                var note = Ui.Lbl("      " + l.Description, 13);
                note.AddThemeColorOverride("font_color", Ui.TextDim);
                note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                text.AddChild(note);
            }
            row.AddChild(Ui.Grow(text));

            if (on)
            {
                var seal = Ui.Lbl("em vigor", 14);
                seal.AddThemeColorOverride("font_color", Ui.Accent);
                row.AddChild(seal);
            }
            else if (mine)
            {
                string lid = l.Id;
                var b = Ui.Btn($"Subir ({cost:0})", () => onChange(lid), 150,
                               l.Sort > (active?.Sort ?? 0) ? Ui.Kind.Primary : Ui.Kind.Normal);
                string? why = new ChangeLawCommand(c.Id, lid).Validate(w);
                b.Disabled = why is not null;
                b.TooltipText = why ?? l.Description;
                row.AddChild(b);
            }
            v.AddChild(step);
        }
        return card;
    }

    /// <summary>O que a lei muda, em texto curto: "indústria +10%, ciência −10%". A fatia de exportação não
    /// é um acréscimo — é um tecto — por isso escreve-se como tecto.</summary>
    public static string Effects(World w, Law law)
    {
        if (!w.LawEffects.TryGetValue(law.Id, out var effs) || effs.Count == 0) return "sem efeitos";
        return string.Join(", ", effs.OrderBy(e => e.Key).Select(e => e.Key switch
        {
            "export_share" => $"exporta até {e.Mul:P0} dos depósitos",
            "export_price" => $"o estrangeiro paga {(e.Mul - 1f) * 100f:+0;-0}%",
            _ => $"{Ui.StatName(e.Key)} {(e.Mul - 1f) * 100f:+0;-0}%",
        }));
    }
}
