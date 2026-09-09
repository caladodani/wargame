using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>As leis nacionais como escada, à maneira do HoI4. Antes eram uma lista corrida: o nome da lei
/// em vigor numa linha e, por baixo, uma fila de linhas iguais com um botão "Mudar" cada uma — não se via
/// que as leis de um grupo são degraus da mesma escada, nem para que lado é que se sobe, nem o que se ganha
/// e o que se perde ao subir.
///
/// Subir custa poder político e não pontos de produção — uma lei não se compra com fábricas — e os degraus
/// que mexem fundo na vida da gente só se destrancam com o mundo já inquieto (law.min_tension): esses
/// aparecem com cadeado e a tensão que lhes falta, em vez de um botão que ninguém percebia por que é que
/// estava apagado.
///
/// Agora cada grupo é um cartão com a sua escada: um degrau por lei, ordenado do país desmobilizado para o
/// país em pé de guerra, com a fila de lâmpadas a dizer a que altura fica, o degrau em vigor aceso em latão
/// e os efeitos escritos em português por baixo do nome. Cada degrau por ocupar leva a chapa "Subir" com o
/// preço, apagada com a razão no tooltip quando o cofre não chega.
///
/// A escada que é só de um país (law.country_tag) vem emoldurada de outra maneira: cabeçalho com a bandeira
/// e o selo "⚜ TAG", moldura em latão e o aviso de que mais nenhum país tem aquela escada. O nome e a chapa
/// de cada grupo já não estão escritos aqui — vêm da tabela law_group, como manda a arquitectura.
///
/// Só lê o World; mudar de lei é de quem sabe despachar comandos.</summary>
public static class LawsView
{
    /// <summary>Ordem por que os cartões deste país aparecem (o World já ordena por law_group.sort e já
    /// deixa de fora as escadas que são de outro país).</summary>
    public static List<string> Order(World w, Country c) => w.LawGroups(c);

    /// <summary>Nome de painel do grupo: chapa e nome vêm da tabela law_group; um grupo que a base de dados
    /// não descreva fica com a chave à vista, que é melhor do que uma linha vazia.</summary>
    public static string GroupName(World w, string group) =>
        w.LawGroupDefs.TryGetValue(group, out var d)
            ? (d.Icon.Length > 0 ? $"{d.Icon} {d.Name}" : d.Name)
            : group;

    /// <summary>A escada toda: um cartão por grupo de leis do país. Null quando não há leis nenhumas.</summary>
    public static VBoxContainer? Cards(World w, Country c, bool mine, Action<string> onChange)
    {
        var groups = Order(w, c);
        if (groups.Count == 0) return null;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        foreach (var grp in groups) box.AddChild(Card(w, c, grp, mine, onChange));
        return box;
    }

    /// <summary>Um grupo: cabeçalho com a lei em vigor e a escada dos degraus por baixo. A escada própria do
    /// país troca o cabeçalho por um brasão e ganha moldura de latão.</summary>
    private static PanelContainer Card(World w, Country c, string group, bool mine, Action<string> onChange)
    {
        var steps = w.Laws.Values.Where(l => l.Group == group && World.LawIsFor(l, c))
                          .OrderBy(l => l.Sort).ThenBy(l => l.Id).ToList();
        bool national = w.LawGroupDefs.TryGetValue(group, out var def) && def.CountryTag == c.Tag
                        || steps.Any(l => l.CountryTag == c.Tag);
        var active = w.ActiveLaw(c, group);
        float cost = w.Rule("law_change_cost", 30f);

        var card = new PanelContainer();
        var plate = Ui.Box(national ? new Color(0.15f, 0.14f, 0.11f, 0.96f) : new Color(0.12f, 0.13f, 0.18f, 0.94f), 10);
        if (national)
        {
            plate.SetBorderWidthAll(2);
            plate.BorderColor = Ui.Accent with { A = 0.75f };
        }
        card.AddThemeStyleboxOverride("panel", plate);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        if (national)
        {
            // escada que é só deste país: entra com bandeira, como as folhas de estado-maior
            head.AddChild(Ui.Grow(Ui.Crest(c.Tag, $"{GroupName(w, group)}: {active?.Name ?? "—"}",
                                           $"escada própria de {c.Name} — mais nenhum país a tem", 17)));
            head.AddChild(Seal(c));
        }
        else
        {
            var title = Ui.Lbl($"{GroupName(w, group)}: {active?.Name ?? "—"}", 17);
            title.AddThemeColorOverride("font_color", Ui.Accent);
            head.AddChild(Ui.Grow(title));
        }
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
            else if (l.MinTension > 0f && WorldTension.Of(w) < l.MinTension)
            {
                // trancada pelo mundo, e não pelo cofre: um país em sossego não vota a mobilização geral.
                // Isto mostra-se a toda a gente e não só a quem joga — é a peça que explica por que é que
                // meia escada está fora de alcance no dia 1 e passa a estar ao alcance quando o mundo arde.
                var lockChip = Ui.Lbl($"🔒 tensão {l.MinTension:0}", 14);
                lockChip.AddThemeColorOverride("font_color", Ui.TextDim);
                lockChip.TooltipText = $"o mundo está a {WorldTension.Of(w):0} de 100 e esta lei só passa a partir de {l.MinTension:0}";
                row.AddChild(lockChip);
            }
            else if (mine)
            {
                string lid = l.Id;
                var b = Ui.Btn($"Subir ({cost:0} pp)", () => onChange(lid), 150,
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

    /// <summary>Selo da escada que é do país e de mais nenhum (o mesmo que o gabinete põe nos conselheiros
    /// próprios, para as duas coisas se lerem como a mesma ideia).</summary>
    private static PanelContainer Seal(Country c)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 4));
        var l = Ui.Lbl($"⚜ {c.Tag}", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"leis próprias de {c.Name}: nenhum outro país as pode votar";
        chip.AddChild(l);
        return chip;
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
