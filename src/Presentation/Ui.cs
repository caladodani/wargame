using Godot;

namespace WarGame.Presentation;

/// <summary>Fábricas de controlos para toque: botões ≥ 48 px de altura, fonte ≥ 18. Handlers embrulhados em try/catch.
/// Tema global (Ui.Theme) aplicado à janela pelo Hud: dá cor, cantos redondos e estados aos botões, aos painéis,
/// aos diálogos e às barras de scroll sem cada painel ter de repetir styleboxes.</summary>
internal static class Ui
{
    public const int Font = 18;

    // Paleta de sala de operações: aço escuro esverdeado, latão nas molduras, letra cor de papel. É a
    // gramática dos jogos de grande estratégia da casa Paradox — chapas metálicas com moldura de latão e
    // números a dourado — e cai melhor num mapa de 1930 do que o azul de aplicação que estava aqui.
    public static readonly Color Ink = new(0.055f, 0.066f, 0.078f);
    public static readonly Color Surface = new(0.117f, 0.133f, 0.153f);
    public static readonly Color SurfaceHi = new(0.180f, 0.203f, 0.227f);
    /// <summary>Latão: molduras, sublinhados e o que o dedo deve encontrar primeiro.</summary>
    public static readonly Color Accent = new(0.784f, 0.647f, 0.298f);
    /// <summary>Latão escurecido das molduras (linha de 1 px à volta das chapas).</summary>
    public static readonly Color Frame = new(0.404f, 0.341f, 0.180f);
    public static readonly Color Danger = new(0.686f, 0.235f, 0.231f);
    public static readonly Color Good = new(0.373f, 0.561f, 0.310f);
    public static readonly Color Text = new(0.902f, 0.886f, 0.827f);
    public static readonly Color TextDim = new(0.596f, 0.612f, 0.596f);

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
            b.AddThemeStyleboxOverride("normal", Fill(c.Darkened(0.55f), border: c));
            b.AddThemeStyleboxOverride("hover", Fill(c.Darkened(0.35f), border: c.Lightened(0.3f)));
            b.AddThemeStyleboxOverride("pressed", Fill(c.Darkened(0.7f), border: c));
            b.AddThemeColorOverride("font_color", kind == Kind.Primary ? Accent.Lightened(0.55f) : Colors.White);
        }
        b.Pressed += () => { try { onPressed(); } catch (Exception ex) { GD.PushError($"Botão '{b.Text}': {ex}"); } };
        return b;
    }

    /// <summary>Faz o controlo ocupar a largura livre da linha.</summary>
    public static T Grow<T>(T c) where T : Control { c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; return c; }

    /// <summary>Chapa: fundo escuro, canto quase direito e moldura de latão de 1 px. Todas as caixas do
    /// jogo passam por aqui, por isso é aqui que se muda a cara do jogo inteiro.</summary>
    public static StyleBoxFlat Box(Color bg, int pad = 8) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        BorderColor = Frame,
        ContentMarginLeft = pad, ContentMarginRight = pad, ContentMarginTop = pad, ContentMarginBottom = pad,
    };

    /// <summary>Chapa de botão: canto direito, moldura de latão e um risco mais claro em cima — o relevo
    /// gasto das teclas de metal, sem uma única textura carregada.</summary>
    private static StyleBoxFlat Fill(Color bg, int radius = 2, int padX = 14, int padY = 8, Color? border = null) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 2, BorderWidthBottom = 1,
        BorderColor = border ?? Frame,
        ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
    };

    /// <summary>Contador da barra de topo: ícone, número e nota, numa chapa estreita com moldura. É a fila
    /// de mostradores que qualquer jogo do género tem por cima do mapa — dinheiro, homens, divisões — e que
    /// aqui vivia em duas frases de texto corrido que ninguém lia de relance.</summary>
    public static PanelContainer Counter(string icon, out Label value, out Label note, Color? tint = null)
    {
        var plate = new PanelContainer();
        plate.AddThemeStyleboxOverride("panel", Box(Ink with { A = 0.85f }, 6));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); plate.AddChild(row);
        var ic = Lbl(icon, 18); ic.AddThemeColorOverride("font_color", tint ?? Accent); row.AddChild(ic);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 0); row.AddChild(v);
        value = Lbl("—", 18); value.AddThemeColorOverride("font_color", Text); v.AddChild(value);
        note = Lbl("", 13); note.AddThemeColorOverride("font_color", TextDim); v.AddChild(note);
        return plate;
    }

    /// <summary>Faz de um mostrador (ou de qualquer cartão) um botão: o dedo carrega em cima e abre-se o
    /// painel de que ele fala. É a maneira dos jogos de grande estratégia — na barra de cima nada é só
    /// enfeite, tudo o que mostra um número leva ao sítio onde esse número se gasta.</summary>
    public static T Click<T>(T node, Action onClick, string? tip = null) where T : Control
    {
        node.MouseFilter = Control.MouseFilterEnum.Stop;
        node.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        if (tip is not null) node.TooltipText = tip;
        node.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } or InputEventScreenTouch { Pressed: true })
            {
                node.AcceptEvent();
                onClick();
            }
        };
        return node;
    }

    /// <summary>Escala de calor dos modos de mapa: do aço frio ao latão e do latão ao vermelho. É a mesma
    /// leitura de qualquer mapa temático — quanto mais quente, mais daquilo há — feita com as cores da casa
    /// em vez de um arco-íris que não pertence a esta pele.</summary>
    public static Color Heat(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < 0.5f ? Surface.Lerp(Accent.Darkened(0.15f), t * 2f)
                        : Accent.Darkened(0.15f).Lerp(Danger.Lightened(0.1f), (t - 0.5f) * 2f);
    }

    /// <summary>Fila de lâmpadas: quantas de um total estão acesas. É como os jogos do género mostram
    /// fábricas, ranhuras e cais — um número diz "3 de 5", mas uma fila de chapas acesas vê-se sem ler.
    /// Acima de `max` a fila pára e o resto vai num "+n" para não atravessar o ecrã.</summary>
    public static HBoxContainer Pips(int filled, int total, Color? on = null, int max = 12)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 3);
        int shown = Math.Min(total, max);
        for (int i = 0; i < shown; i++)
            row.AddChild(new ColorRect
            {
                CustomMinimumSize = new Vector2(9, 16),
                Color = i < filled ? (on ?? Accent) : SurfaceHi.Darkened(0.35f),
            });
        if (total > shown)
        {
            var more = Lbl("+" + (total - shown), 13);
            more.AddThemeColorOverride("font_color", TextDim);
            row.AddChild(more);
        }
        return row;
    }

    /// <summary>Fila de abas metálicas no topo de um painel: chapa de latão com o topo arredondado, a
    /// aberta levantada e acesa sobre a calha, as fechadas encostadas e escuras. Quem desenha é o
    /// MetalTabs — aqui só se lhe dão os nomes, para todo o jogo pedir abas da mesma maneira.</summary>
    public static Control Tabs(IReadOnlyList<string> labels, int active, Action<int> onPick)
    {
        var tabs = new MetalTabs();
        tabs.Set(labels, active, onPick);
        return tabs;
    }

    /// <summary>Brasão do painel: a bandeira do país numa moldura de latão com rebites, e o título do
    /// painel ao lado, com a legenda por baixo. É o escudo que os painéis dos jogos de grande estratégia
    /// têm no canto — sem ele um painel aberto podia ser de qualquer país, e com meia dúzia deles abertos
    /// ninguém sabia de quem era a folha que estava a ler.
    ///
    /// Devolve a linha inteira; quem a chama acrescenta ao lado o que lhe falta (botões, contadores).</summary>
    public static HBoxContainer Crest(string tag, string title, string sub = "", int size = 22)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);

        var shield = new PanelContainer();
        var plate = Box(Ink with { A = 0.9f }, 3);
        plate.BorderWidthLeft = plate.BorderWidthRight = plate.BorderWidthTop = plate.BorderWidthBottom = 2;
        plate.BorderColor = Accent;
        shield.AddThemeStyleboxOverride("panel", plate);
        var flag = Flags.Rect(size + 6);
        flag.Texture = Flags.Of(tag);
        if (flag.Texture is null)
        {
            // país sem bandeira: fica a sigla em latão, que é melhor do que um buraco na moldura
            var t = Lbl(tag, size - 4);
            t.AddThemeColorOverride("font_color", Accent);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.CustomMinimumSize = new Vector2((size + 6) * 1.5f, size + 6);
            shield.AddChild(t);
        }
        else shield.AddChild(flag);
        row.AddChild(shield);

        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 0);
        var name = Lbl(title, size);
        name.AddThemeColorOverride("font_color", Text);
        stack.AddChild(name);
        if (sub.Length > 0)
        {
            var s2 = Lbl(sub, 13);
            s2.AddThemeColorOverride("font_color", TextDim);
            stack.AddChild(s2);
        }
        row.AddChild(Grow(stack));
        return row;
    }

    /// <summary>Volta a encher uma linha de cabeçalho com o brasão. Existe porque os cabeçalhos dos painéis
    /// montam-se uma vez no Setup e o país do jogador só se sabe depois — e ainda muda quando ele escolhe.</summary>
    public static void CrestInto(HBoxContainer host, string tag, string title, string sub = "", int size = 22)
    {
        Clear(host);
        host.AddChild(Grow(Crest(tag, title, sub, size)));
    }

    /// <summary>Título de secção à maneira das folhas de estado-maior: versaletes (o Godot não tem a
    /// variante tipográfica, faz-se por maiúsculas com espaço entre letras) em latão, com o risco por
    /// baixo. Dá hierarquia aos painéis sem gastar altura nem tamanho de letra.</summary>
    public static VBoxContainer Head(string text, int size = 14)
    {
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2);
        var l = Lbl(string.Join(" ", text.ToUpperInvariant().ToCharArray()), size);
        l.AddThemeColorOverride("font_color", Accent);
        v.AddChild(l);
        v.AddChild(Rule());
        return v;
    }

    /// <summary>Risco de latão a toda a largura: separa secções sem gastar altura.</summary>
    public static ColorRect Rule(float height = 1f) => new()
    {
        Color = Frame, CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static Theme? _theme;

    /// <summary>Tema da janela inteira (o Hud põe-no na raiz): botões com estados, painéis, diálogos e scroll.</summary>
    public static Theme Theme()
    {
        if (_theme is not null) return _theme;
        var t = new Theme { DefaultFontSize = Font };

        t.SetStylebox("normal", "Button", Fill(Surface));
        t.SetStylebox("hover", "Button", Fill(SurfaceHi, border: Accent));
        t.SetStylebox("pressed", "Button", Fill(Accent.Darkened(0.55f), border: Accent));
        t.SetStylebox("focus", "Button", Fill(SurfaceHi, border: Accent));
        t.SetStylebox("disabled", "Button", Fill(Surface.Darkened(0.45f), border: Frame.Darkened(0.4f)));
        t.SetColor("font_color", "Button", Text);
        t.SetColor("font_hover_color", "Button", Accent.Lightened(0.4f));
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
    /// <summary>Nome de painel de uma chave de stat. As chaves são as da base de dados (uma palavra em
    /// inglês, que é a chave de country_stat e dos efeitos); os painéis é que sabem como se diz cá. Estava
    /// espalhado por quem precisava — o gabinete tinha a sua tabela, as leis iam ter outra — e duas tabelas
    /// da mesma coisa acabam sempre a discordar.</summary>
    public static string StatName(string key) => key switch
    {
        "industry" => "indústria",
        "production_speed" => "produção",
        "research_speed" => "investigação",
        "research_slots" => "ranhuras de investigação",
        "conscription" => "recruta",
        "counter_intel" => "contra-espionagem",
        "org_regain" => "recomposição",
        "move_speed" => "marcha",
        "attack" => "ataque",
        "defense" => "defesa",
        "occupied_yield" => "rendimento ocupado",
        "integration_speed" => "integração",
        "resistance_growth" => "resistência",
        "export_share" => "exportação",
        "export_price" => "preço de exportação",
        "port_capacity" => "cais",
        "aggression" => "agressividade",
        _ => key,
    };

    public static void Clear(Node n)
    {
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }
}
