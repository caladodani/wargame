using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara da ocupação: o que fazemos ao povo da terra que lhe tomámos. Enquanto o
/// OccupationSystem só existia por dentro, tomar terra era um número calado no rendimento — agora tem cartão
/// próprio no painel do país ocupado, com a rua a ferver numa barra e uma chapa por política.
///
/// Cada chapa diz de uma vez o que a política custa e o que rende (resistência, rendimento, recrutas), e a
/// que está desligada leva a razão no tooltip — a mesma que o OccupationSystem.Block devolve ao comando, para
/// o botão nunca mentir sobre porque não se pode assinar.
///
/// Só lê o World e devolve o id da política escolhida a quem sabe despachar comandos.</summary>
public static class OccupationView
{
    /// <summary>Cor da rua: calma em verde, a ferver em vermelho.</summary>
    public static Color Heat(float heat) => Ui.Good.Lerp(Ui.Danger, Mathf.Clamp(heat, 0f, 1f));

    /// <summary>Cartão da ocupação que este país faz ao povo daquele. Null quando não lhe ocupamos terra
    /// nenhuma — um país com quem nunca cruzámos a fronteira não tem ocupação que mostrar.</summary>
    public static PanelContainer? Card(World w, int occupierId, int ownerId, Action<string> onPick)
    {
        int regions = OccupationSystem.Regions(w, occupierId, ownerId);
        if (regions == 0 || w.OccupationPolicyDefs.Count == 0) return null;

        var now = OccupationSystem.Policy(w, occupierId, ownerId);
        float heat = OccupationSystem.Heat(w, occupierId, ownerId);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.16f, 0.12f, 0.09f, 0.92f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        var headRow = new HBoxContainer(); headRow.AddThemeConstantOverride("separation", 8);
        headRow.AddChild(Glyph.Make(now.Glyph, 17, Ui.Accent, now.Name));
        var head = Ui.Lbl($"{regions} região{(regions == 1 ? "" : "ões")} sob ocupação · {now.Name}", 17);
        head.AddThemeColorOverride("font_color", Ui.Accent);
        headRow.AddChild(Ui.Grow(head));
        v.AddChild(headRow);

        var bar = Ui.Bar(Mathf.Clamp(heat, 0f, 1f), Heat(heat), 0f);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        v.AddChild(bar);

        var note = Ui.Lbl($"rua a {heat:P0} · {now.Note}", 14);
        note.AddThemeColorOverride("font_color", heat > 0.5f ? Ui.Danger : Ui.TextDim);
        v.AddChild(note);

        foreach (var def in w.OccupationPolicyDefs.Values.OrderBy(d => d.Sort))
        {
            string id = def.Id;
            string? no = OccupationSystem.Block(w, occupierId, ownerId, id);
            bool on = def.Id == now.Id;
            var b = Ui.Btn($"      {def.Name}   —   revolta ×{def.Resistance:0.00} · rende ×{def.Yield:0.00} · homens ×{def.Manpower:0.00}",
                           () => onPick(id), 0, on ? Ui.Kind.Primary : Ui.Kind.Normal);
            b.Alignment = HorizontalAlignment.Left;
            b.Disabled = no is not null;
            b.TooltipText = on ? def.Note : no ?? def.Note;
            b.AddThemeFontSizeOverride("font_size", 15);
            Glyph.Stamp(b, def.Glyph, on ? Ui.Ink : Ui.Accent, 15f);
            v.AddChild(Ui.Grow(b));
        }
        return card;
    }
}
