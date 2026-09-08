using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara do gabinete civil (HoI4: political advisors). O país tinha estado-maior e não tinha governo:
/// não havia uma única cadeira civil para preencher, nem ecrã onde se visse que a indústria rende mais porque
/// há um homem sentado a tratar dela.
///
/// Agora cada pasta é um retrato emoldurado como no HoI4: a chapa do homem à esquerda, o nome e o que ele
/// multiplica ao lado, e por baixo a barra de rodagem — os dias de casa que fazem o mesmo conselheiro valer
/// mais (World.CabinetTenure). Os que são do país levam o selo "⚜ nosso" e aparecem no cimo da lista de
/// candidatos, separados dos que se contratam em qualquer lado.
///
/// Só lê o World; nomear e demitir é de quem sabe despachar comandos.</summary>
public static class CabinetView
{
    /// <summary>Cartão do gabinete deste país. Null quando a BD não traz pastas nenhumas.</summary>
    public static PanelContainer? Card(World w, Country c, int day, Action<string> onAppoint, Action<string> onDismiss)
    {
        if (w.CabinetSlots.Count == 0) return null;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.12f, 0.13f, 0.18f, 0.94f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 5); card.AddChild(v);

        float wages = CabinetSystem.Wages(w, c);
        int own = c.Cabinet.Values.Count(id => w.AdvisorDefs.TryGetValue(id, out var a) && a.CountryTag is not null);
        var headRow = new HBoxContainer(); headRow.AddThemeConstantOverride("separation", 8);
        headRow.AddChild(Glyph.Make("pasta", 17, Ui.Accent));
        var head = Ui.Lbl($"Gabinete: {c.Cabinet.Count}/{w.CabinetSlots.Count} pastas ocupadas · folha de {wages:0.0}/dia"
                          + (own > 0 ? $" · {own} de casa" : ""), 17);
        head.AddThemeColorOverride("font_color", Ui.Accent);
        headRow.AddChild(Ui.Grow(head));
        v.AddChild(headRow);

        float bonus = w.Rule("advisor_tenure_bonus", 0.5f);
        foreach (var slot in w.CabinetSlots)
        {
            v.AddChild(Ui.Rule());
            string? seated = c.Cabinet.GetValueOrDefault(slot.Id);
            float tenure = World.CabinetTenure(w, c, slot.Id);

            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
            // Cadeira vazia mostra a chapa da pasta; cadeira ocupada mostra a cara do homem que lá está —
            // essa continua a vir da tabela advisor, que é sabor nacional e não se desenha uma a uma.
            row.AddChild(Portrait(seated is not null && w.AdvisorDefs.TryGetValue(seated, out var sit) ? sit.Icon : null,
                                  slot.Glyph, seated is not null));
            var cell = Ui.Grow(new VBoxContainer()); cell.AddThemeConstantOverride("separation", 2);

            var title = new HBoxContainer(); title.AddThemeConstantOverride("separation", 8);
            title.AddChild(Glyph.Make(slot.Glyph, 15, Ui.TextDim, slot.Name));
            var pasta = Ui.Lbl(slot.Name, 16);
            pasta.AddThemeColorOverride("font_color", Ui.TextDim);
            title.AddChild(pasta);
            if (seated is not null && w.AdvisorDefs.TryGetValue(seated, out var sat))
            {
                var who = Ui.Lbl(sat.Name, 17);
                who.AddThemeColorOverride("font_color", Ui.Good);
                title.AddChild(Ui.Grow(who));
                if (sat.CountryTag is not null) title.AddChild(Seal(c));
                string id = slot.Id;
                title.AddChild(Ui.Btn("Demitir", () => onDismiss(id), 0, Ui.Kind.Danger));
                cell.AddChild(title);

                var what = Ui.Lbl(Effects(sat, 1f + bonus * tenure), 15);
                what.AddThemeColorOverride("font_color", Ui.Text);
                cell.AddChild(what);

                // rodagem: os dias de casa que fazem o mesmo homem valer mais
                int since = day - c.CabinetSince.GetValueOrDefault(slot.Id);
                var wear = new HBoxContainer(); wear.AddThemeConstantOverride("separation", 8);
                wear.AddChild(Ui.Bar(tenure, tenure >= 0.999f ? Ui.Good : Ui.Accent, 160f));
                var note = Ui.Lbl(tenure >= 0.999f
                                  ? $"{since} dias de casa · rodado de todo (+{bonus:P0} do que faz)"
                                  : $"{since} dias de casa · rodagem {tenure:P0}", 14);
                note.AddThemeColorOverride("font_color", Ui.TextDim);
                wear.AddChild(Ui.Grow(note));
                cell.AddChild(wear);
            }
            else
            {
                var empty = Ui.Lbl("cadeira vazia", 16);
                empty.AddThemeColorOverride("font_color", Ui.TextDim);
                title.AddChild(Ui.Grow(empty));
                cell.AddChild(title);
            }
            row.AddChild(cell);
            v.AddChild(row);

            // candidatos: primeiro os do país, depois os que se contratam em qualquer lado
            var pool = CabinetSystem.Candidates(w, c, slot.Id).Where(a => a.Id != seated).ToList();
            foreach (var group in new[] { true, false })
            {
                var men = pool.Where(a => (a.CountryTag is not null) == group).ToList();
                if (men.Count == 0) continue;
                var label = Ui.Lbl(group ? "      ⚜ do país" : "      de qualquer lado", 13);
                label.AddThemeColorOverride("font_color", group ? Ui.Accent : Ui.TextDim);
                v.AddChild(label);
                foreach (var a in men)
                {
                    string id = a.Id;
                    var b = Ui.Btn($"{a.Icon} {a.Name}   —   {Effects(a, 1f)}   ·   {a.Cost:0} pp + {a.Cost * w.Rule("advisor_wage_share", 0.01f):0.0}/dia",
                                   () => onAppoint(id), 0, group ? Ui.Kind.Primary : Ui.Kind.Normal);
                    b.Disabled = c.Money < a.Cost;
                    b.TooltipText = b.Disabled ? $"faltam {a.Cost - c.Money:0} pontos de produção" : a.Note;
                    b.AddThemeFontSizeOverride("font_size", 14);
                    v.AddChild(Ui.Grow(b));
                }
            }
        }
        return card;
    }

    /// <summary>Retrato: a chapa emoldurada do homem que está na pasta (ou o ícone da pasta, apagado, quando
    /// a cadeira está vazia).</summary>
    private static PanelContainer Portrait(string? icon, string glyph, bool seated)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(54, 54) };
        frame.AddThemeStyleboxOverride("panel", Ui.Box(seated ? Ui.Ink : Ui.Surface.Darkened(0.3f), 6));
        if (icon is null)
        {
            var plate = Glyph.Make(glyph, 26, Ui.Frame);
            plate.SizeFlagsHorizontal = plate.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            frame.AddChild(plate);
            return frame;
        }
        var face = Ui.Lbl(icon, 26);
        face.HorizontalAlignment = HorizontalAlignment.Center;
        face.VerticalAlignment = VerticalAlignment.Center;
        face.AddThemeColorOverride("font_color", Ui.Accent);
        frame.AddChild(face);
        return frame;
    }

    /// <summary>Selo do conselheiro que é do país e de mais nenhum.</summary>
    private static PanelContainer Seal(Country c)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 4));
        var l = Ui.Lbl($"⚜ {c.Tag}", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"conselheiro próprio de {c.Name}: mais nenhum país o pode chamar";
        chip.AddChild(l);
        return chip;
    }

    /// <summary>O que o homem vale, em texto curto: "indústria +10%, recruta +15%". O factor é a rodagem já
    /// contada (1 = acabado de chegar), para o cartão dizer o que ele rende hoje e não o que dizia a tabela.</summary>
    public static string Effects(AdvisorDef a, float factor) =>
        a.Effects.Count == 0 ? "sem efeito"
        : string.Join(", ", a.Effects.OrderBy(kv => kv.Key)
            .Select(kv => $"{Pretty(kv.Key)} {(kv.Value - 1f) * factor * 100f:+0;-0}%"));

    /// <summary>Nome de cozinha do stat em português de painel (a tabela vive no Ui, que é de todos).</summary>
    private static string Pretty(string key) => Ui.StatName(key);
}
