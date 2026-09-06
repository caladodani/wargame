using Godot;

namespace WarGame.Presentation;

/// <summary>Fábricas de controlos para toque: botões ≥ 48 px de altura, fonte ≥ 18. Handlers embrulhados em try/catch.</summary>
internal static class Ui
{
    public const int Font = 18;

    public static Label Lbl(string text, int size = Font)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        return l;
    }

    public static Button Btn(string text, Action onPressed, float minWidth = 0f)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 48) };
        b.AddThemeFontSizeOverride("font_size", 20);
        b.Pressed += () => { try { onPressed(); } catch (Exception ex) { GD.PushError($"Botão '{b.Text}': {ex}"); } };
        return b;
    }

    /// <summary>Faz o controlo ocupar a largura livre da linha.</summary>
    public static T Grow<T>(T c) where T : Control { c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; return c; }

    public static StyleBoxFlat Box(Color bg, int pad = 8) => new()
    {
        BgColor = bg, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = pad, ContentMarginRight = pad, ContentMarginTop = pad, ContentMarginBottom = pad,
    };

    /// <summary>ConfirmationDialog Sim/Não com fontes de toque, já adicionado a `parent`. Texto definido por uso (DialogText).</summary>
    public static ConfirmationDialog Dialog(Node parent, Action onConfirm)
    {
        var d = new ConfirmationDialog { OkButtonText = "Sim", CancelButtonText = "Não", MinSize = new Vector2I(560, 200) };
        d.GetLabel().AddThemeFontSizeOverride("font_size", 20);
        foreach (var b in new[] { d.GetOkButton(), d.GetCancelButton() })
        { b.CustomMinimumSize = new Vector2(140, 48); b.AddThemeFontSizeOverride("font_size", 20); }
        d.Confirmed += () => { try { onConfirm(); } catch (Exception ex) { GD.PushError("Diálogo: " + ex); } };
        parent.AddChild(d);
        return d;
    }

    /// <summary>Remove e liberta todos os filhos já (QueueFree sozinho deixa-os no layout até ao fim do frame).</summary>
    public static void Clear(Node n)
    {
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }
}
