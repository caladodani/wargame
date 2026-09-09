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
/// Desde 0.3.87 o ministro traz também a COR do partido dele: uma chapa com o glifo e o puxão diário que
/// dá à opinião do país, e o aviso vermelho quando é gente da oposição sentada à mesa do governo. O
/// cabeçalho conta o gabinete em política — quantos são do governo, quantos são rivais, e o que isso
/// custa em estabilidade por dia (CabinetSystem.StabilityShift).
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

        // a política do gabinete: quantos são do governo, quantos são rivais, e o que a mesa custa por dia
        int rivals = CabinetSystem.Rivals(w, c), coloured = CabinetSystem.Ministers(w, c).Count();
        float shift = CabinetSystem.StabilityShift(w, c);
        if (coloured > 0)
        {
            var pol = Ui.Lbl($"política da mesa: {coloured - rivals} do governo, {rivals} da oposição"
                             + $" · estabilidade {shift:+0.00;-0.00;0}/dia", 14);
            pol.AddThemeColorOverride("font_color", rivals > 0 ? Ui.Danger : Ui.Good);
            pol.TooltipText = "Cada ministro do partido do governo segura o país; cada um da oposição abana-o "
                            + "e faz campanha de dentro do Estado (advisor_loyal_stability / advisor_rival_stability).";
            v.AddChild(pol);
        }

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
                if (Colour(w, c, sat) is Control chip) title.AddChild(chip);
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
                    var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 6);
                    var b = Ui.Btn($"{a.Icon} {a.Name}   —   {Effects(a, 1f)}   ·   {a.Cost:0} pp + {a.Cost * w.Rule("advisor_wage_share", 0.01f):0.0}/dia",
                                   () => onAppoint(id), 0, group ? Ui.Kind.Primary : Ui.Kind.Normal);
                    b.Disabled = c.Money < a.Cost;
                    b.TooltipText = b.Disabled ? $"faltam {a.Cost - c.Money:0} pontos de produção"
                                  : a.Note + (Party(w, a) is PartyDef pd ? $"\n{Pull(c, a, pd)}" : "");
                    b.AddThemeFontSizeOverride("font_size", 14);
                    line.AddChild(Ui.Grow(b));
                    // a cor política ao lado do preço: contratar é escolher para onde o país deriva
                    if (Colour(w, c, a) is Control chip) line.AddChild(chip);
                    v.AddChild(line);
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

    /// <summary>O partido deste conselheiro, ou null quando é técnico (ou quando a tabela não conhece a
    /// cor que ele traz — save antigo com partidos que já não existem).</summary>
    public static PartyDef? Party(World w, AdvisorDef a) =>
        string.IsNullOrEmpty(a.Party) ? null : w.PartyDefs.GetValueOrDefault(a.Party);

    /// <summary>A frase do puxão: o que este homem faz à opinião do país por dia, e de que lado está.</summary>
    private static string Pull(Country c, AdvisorDef a, PartyDef p) =>
        $"{p.Name}: {a.Drift:+0.000;-0.000;0} pontos de opinião por dia"
        + (p.Id == c.Party ? " · é do governo, segura a casa" : " · é da oposição, abana a casa");

    /// <summary>Chapa da cor política: o glifo do partido, o puxão diário, e a moldura vermelha quando é
    /// gente da oposição. Null para um técnico — quem não tem partido não leva chapa nenhuma.</summary>
    private static Control? Colour(World w, Country c, AdvisorDef a)
    {
        if (Party(w, a) is not PartyDef p) return null;
        bool rival = p.Id != c.Party;
        var tint = PartyView.Of(p);
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(tint with { A = rival ? 0.28f : 0.16f }, 4));
        var box = new HBoxContainer(); box.AddThemeConstantOverride("separation", 4); chip.AddChild(box);
        box.AddChild(Glyph.Make(p.Glyph, 14, tint, p.Name));
        var l = Ui.Lbl($"{(rival ? "⚑ " : "")}{a.Drift:+0.00;-0.00;0}", 13);
        l.AddThemeColorOverride("font_color", rival ? Ui.Danger : tint);
        box.AddChild(l);
        chip.TooltipText = Pull(c, a, p);
        return chip;
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
