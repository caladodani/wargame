using Godot;

namespace WarGame.Presentation;

/// <summary>Fábricas de controlos para toque: botões ≥ 48 px de altura, fonte ≥ 18. Handlers embrulhados em try/catch.
/// Tema global (Ui.Theme) aplicado à janela pelo Hud: dá cor, cantos redondos e estados aos botões, aos painéis,
/// aos diálogos e às barras de scroll sem cada painel ter de repetir styleboxes.</summary>
internal static class Ui
{
    public const int Font = 18;

    // Paleta: fundo escuro azulado, azul de destaque para acções, vermelho para o que destrói.
    public static readonly Color Ink = new(0.07f, 0.08f, 0.11f);
    public static readonly Color Surface = new(0.13f, 0.15f, 0.20f);
    public static readonly Color SurfaceHi = new(0.19f, 0.22f, 0.29f);
    public static readonly Color Accent = new(0.25f, 0.55f, 0.95f);
    public static readonly Color Danger = new(0.80f, 0.28f, 0.28f);
    public static readonly Color Good = new(0.25f, 0.70f, 0.45f);
    public static readonly Color Text = new(0.91f, 0.93f, 0.97f);
    public static readonly Color TextDim = new(0.62f, 0.66f, 0.74f);

    /// <summary>Papel do botão: muda a cor, não o tamanho.</summary>
    public enum Kind { Normal, Primary, Danger }

    public static Label Lbl(string text, int size = Font)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (size <= 15) l.AddThemeColorOverride("font_color", TextDim);   // legendas em tom mais apagado
        return l;
    }

    public static Button Btn(string text, Action onPressed, float minWidth = 0f, Kind kind = Kind.Normal)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 48) };
        b.AddThemeFontSizeOverride("font_size", 20);
        if (kind != Kind.Normal)
        {
            var c = kind == Kind.Primary ? Accent : Danger;
            b.AddThemeStyleboxOverride("normal", Fill(c.Darkened(0.15f)));
            b.AddThemeStyleboxOverride("hover", Fill(c));
            b.AddThemeStyleboxOverride("pressed", Fill(c.Darkened(0.35f)));
            b.AddThemeColorOverride("font_color", Colors.White);
        }
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

    /// <summary>Stylebox lisa de botão: cantos redondos, margens de toque, sem borda.</summary>
    private static StyleBoxFlat Fill(Color bg, int radius = 8, int padX = 14, int padY = 8) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
    };

    private static Theme? _theme;

    /// <summary>Tema da janela inteira (o Hud põe-no na raiz): botões com estados, painéis, diálogos e scroll.</summary>
    public static Theme Theme()
    {
        if (_theme is not null) return _theme;
        var t = new Theme { DefaultFontSize = Font };

        t.SetStylebox("normal", "Button", Fill(Surface));
        t.SetStylebox("hover", "Button", Fill(SurfaceHi));
        t.SetStylebox("pressed", "Button", Fill(Accent.Darkened(0.25f)));
        t.SetStylebox("focus", "Button", Fill(SurfaceHi));
        t.SetStylebox("disabled", "Button", Fill(Surface.Darkened(0.35f)));
        t.SetColor("font_color", "Button", Text);
        t.SetColor("font_hover_color", "Button", Colors.White);
        t.SetColor("font_pressed_color", "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", TextDim.Darkened(0.3f));

        t.SetColor("font_color", "Label", Text);
        t.SetStylebox("panel", "PanelContainer", Box(Surface.Darkened(0.25f) with { A = 0.96f }, 10));
        t.SetStylebox("panel", "AcceptDialog", Box(Ink with { A = 0.99f }, 14));
        t.SetColor("font_color", "LineEdit", Text);
        t.SetStylebox("normal", "LineEdit", Fill(Ink, 6));
        t.SetStylebox("focus", "LineEdit", Fill(Ink.Lightened(0.08f), 6));

        // Barras de scroll finas e discretas — o dedo arrasta, a barra só mostra onde estamos.
        t.SetStylebox("scroll", "VScrollBar", Fill(Ink with { A = 0.5f }, 4, 0, 0));
        t.SetStylebox("grabber", "VScrollBar", Fill(SurfaceHi, 4, 0, 0));
        t.SetStylebox("grabber_highlight", "VScrollBar", Fill(Accent, 4, 0, 0));
        t.SetStylebox("grabber_pressed", "VScrollBar", Fill(Accent, 4, 0, 0));

        t.SetStylebox("panel", "ProgressBar", Fill(Ink, 4, 0, 0));
        t.SetStylebox("fill", "ProgressBar", Fill(Accent, 4, 0, 0));
        return _theme = t;
    }

    /// <summary>Barra de progresso fina (produção, investigação): valor 0..1, cor à escolha.</summary>
    public static ProgressBar Bar(float value01, Color? color = null, float width = 120f)
    {
        var p = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Value = Mathf.Clamp(value01, 0f, 1f), ShowPercentage = false,
            CustomMinimumSize = new Vector2(width, 10), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        if (color is Color c) p.AddThemeStyleboxOverride("fill", Fill(c, 4, 0, 0));
        return p;
    }

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

    /// <summary>Abertura de painel ancorado: só opacidade (mexer na posição briga com as âncoras).
    /// Chamar depois de pôr Visible = true.</summary>
    public static void FadeIn(Control panel, double secs = 0.14)
    {
        panel.Modulate = new Color(1, 1, 1, 0);
        panel.CreateTween().TweenProperty(panel, "modulate:a", 1f, secs);
    }

    /// <summary>Abertura de painel: entra a subir e a ganhar opacidade. Chamar depois de pôr Visible = true.</summary>
    public static void SlideIn(Control panel, float from = 40f, double secs = 0.16)
    {
        panel.Modulate = new Color(1, 1, 1, 0);
        float y = panel.Position.Y;
        panel.Position = new Vector2(panel.Position.X, y + from);
        var t = panel.CreateTween().SetParallel();
        t.TweenProperty(panel, "modulate:a", 1f, secs);
        t.TweenProperty(panel, "position:y", y, secs).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Remove e liberta todos os filhos já (QueueFree sozinho deixa-os no layout até ao fim do frame).</summary>
    public static void Clear(Node n)
    {
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }
}
