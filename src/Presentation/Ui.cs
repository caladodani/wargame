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

    /// <summary>A largura mais estreita que a tela do jogo alguma vez tem, em unidades de desenho.
    ///
    /// O jogo estica por "canvas_items/expand" a partir de 1152×648: num telefone ao alto (um S24 Ultra é
    /// 1440×3120) a escala é a do lado mais apertado, e a tela fica com 1152 de largura e mais altura. Ao
    /// baixo a largura só cresce. Logo: o que couber em 1152 cabe em todo o lado, e o que não couber sai
    /// pela direita do ecrã do telefone — que foi exactamente o que se viu.
    ///
    /// Não é um palpite para desenhar contra: é a régua com que o --smoke mede os painéis. Quem escreve um
    /// painel continua a deixar o texto embrulhar e as caixas crescerem; esta constante é só o juiz.</summary>
    public const float Phone = 1152f;

    /// <summary>Um controlo que não cabe: onde está, o que é e quanto pede a mais.</summary>
    public readonly record struct TooWide(string Path, string What, float Wants);

    /// <summary>Mede uma árvore de controlos contra uma largura e devolve quem não cabe.
    ///
    /// Só denuncia o culpado MAIS FUNDO: se uma linha é larga porque tem lá dentro uma etiqueta de 2000 px,
    /// a culpa é da etiqueta e não da linha — senão o relatório vinha com a árvore toda e não se via nada.
    /// Salta o que está dentro de um ScrollContainer que rola na horizontal (aí sair da vista é a intenção)
    /// e o que está escondido, que ninguém vê.
    ///
    /// A medida é a largura MÍNIMA combinada, não a largura de agora: é ela que empurra o pai e que faz o
    /// conteúdo passar a fronteira do ecrã, e é a única que se pode medir sem ecrã nenhum.</summary>
    public static List<TooWide> Overflow(Control root, float width, string path = "")
    {
        var found = new List<TooWide>();
        Walk(root, width, path.Length > 0 ? path : root.Name.ToString(), found);
        return found;
    }

    private static bool Walk(Node n, float width, string path, List<TooWide> found)
    {
        if (n is Control c)
        {
            if (!c.Visible) return false;
            if (c is ScrollContainer sc && sc.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled) return false;
            float wants = c.GetCombinedMinimumSize().X;
            if (wants <= width) return false;

            bool blamed = false;
            foreach (var kid in n.GetChildren()) blamed |= Walk(kid, width, $"{path}/{kid.Name}", found);
            if (!blamed) found.Add(new TooWide(path, Describe(c), wants));
            return true;
        }
        bool any = false;
        foreach (var kid in n.GetChildren()) any |= Walk(kid, width, $"{path}/{kid.Name}", found);
        return any;
    }

    /// <summary>O que o controlo é, com o texto que o faz crescer — é por ele que se percebe qual é a linha.</summary>
    private static string Describe(Control c)
    {
        string text = c switch
        {
            Label l => l.Text,
            Button b => b.Text,
            RichTextLabel r => r.Text,
            _ => "",
        };
        text = text.Replace('\n', ' ');
        if (text.Length > 40) text = text[..40] + "…";
        return text.Length > 0 ? $"{c.GetType().Name} \"{text}\"" : c.GetType().Name;
    }

    /// <summary>A régua ligada: enquanto não for nula, cada painel que passa pelas suas abas no --smoke
    /// mede-se sozinho por Ui.Measure. Assim a prova cobre TODAS as secções e não só a que ficou aberta —
    /// o que estava a passar a margem estava na aba da Ciência, não na de entrada.</summary>
    public static List<TooWide>? Watch;

    /// <summary>O painel mais largo que a régua viu e quanto pediu. Cabendo tudo, é isto que diz quanta
    /// folga sobra até à margem — um painel a 1140 cabe hoje e parte-se com a próxima linha de texto.</summary>
    public static (string Path, float Wants) Widest;

    /// <summary>Mede este painel se a régua estiver ligada. Fora do --smoke não faz nada.</summary>
    public static void Measure(Control c, string path)
    {
        if (Watch is null) return;
        Watch.AddRange(Overflow(c, Phone, path));
        float wants = Body(c);
        if (wants > Widest.Wants) Widest = (path, wants);
    }

    /// <summary>A largura que uma árvore de controlos pede, ignorando quem rola na horizontal.</summary>
    private static float Body(Node n)
    {
        if (n is ScrollContainer sc && sc.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled) return 0f;
        float wants = n is Control c && c.Visible ? c.GetCombinedMinimumSize().X : 0f;
        foreach (var kid in n.GetChildren()) wants = MathF.Max(wants, Body(kid));
        return wants;
    }

    /// <summary>O relatório do --smoke: quantos não cabem e os três piores. Cabe tudo → "cabe tudo".</summary>
    public static string OverflowReport(Control root, float width, string path = "")
    {
        var bad = Overflow(root, width, path);
        if (bad.Count == 0) return "cabe tudo";
        var worst = bad.OrderByDescending(b => b.Wants).Take(3)
                       .Select(b => $"{b.Path} {b.What} pede {b.Wants:0}");
        return $"{bad.Count} a passar a margem: {string.Join(" | ", worst)}";
    }

    public static Label Lbl(string text, int size = Font)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (size <= 15) l.AddThemeColorOverride("font_color", TextDim);   // legendas em tom mais apagado
        return l;
    }

    /// <summary>Altura mínima de um botão de dedo: o mínimo que uma mão acerta sem falhar.</summary>
    public const float TouchHeight = 48f;
    /// <summary>Altura da chapa grande (Ui.Big). Onde sobra altura — a lista de modelos da produção, a
    /// pesquisa, o construir — a chapa cresce em vez de deixar o painel meio vazio: foi o que o utilizador
    /// pediu, e é a chapa gorda dos jogos da casa Paradox. Não muda a cor nem o papel do botão, só o tamanho.</summary>
    public const float TallHeight = 72f;

    public static Button Btn(string text, Action onPressed, float minWidth = 0f, Kind kind = Kind.Normal,
                             float minHeight = 0f)
    {
        float h = minHeight > 0f ? minHeight : TouchHeight;
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, h) };
        b.AddThemeFontSizeOverride("font_size", h >= TallHeight ? 26 : 20);
        if (kind != Kind.Normal)
        {
            var c = kind == Kind.Primary ? Accent : Danger;
            if (!Metal(kind, (state, box) => b.AddThemeStyleboxOverride(state, box)))
            {
                b.AddThemeStyleboxOverride("normal", Fill(c.Darkened(0.55f), border: c));
                b.AddThemeStyleboxOverride("hover", Fill(c.Darkened(0.35f), border: c.Lightened(0.3f)));
                b.AddThemeStyleboxOverride("pressed", Fill(c.Darkened(0.7f), border: c));
            }
            b.AddThemeColorOverride("font_color", kind == Kind.Primary ? Accent.Lightened(0.55f) : Colors.White);
        }
        b.Pressed += () => { try { onPressed(); } catch (Exception ex) { GD.PushError($"Botão '{b.Text}': {ex}"); } };
        return b;
    }

    /// <summary>A mesma chapa, na altura grande (TallHeight). Usa-se onde o painel tem altura a sobrar e a
    /// chapa pequena ficava perdida no meio do vazio — é a mesma função de sempre, com outro tamanho.</summary>
    public static Button Big(string text, Action onPressed, float minWidth = 0f, Kind kind = Kind.Normal) =>
        Btn(text, onPressed, minWidth, kind, TallHeight);

    /// <summary>O tom por que a chapa da tecla é multiplicada, em repouso, sob o dedo e premida.
    ///
    /// Vem mais claro do que a cor lisa que estava aqui, e é de propósito: o relevo da imagem é feito de
    /// escurecimentos — contorno a 0.16, base a 0.38 — e uma cor já escura não deixa lá ficar aresta
    /// nenhuma. Uma tecla de metal é mais clara do que o painel onde está montada; era o painel que
    /// estava a ser rectângulo, não o botão que estava escuro de mais.</summary>
    private static (Color Idle, Color Hover, Color Down) Tone(Kind k) => k switch
    {
        Kind.Primary => (new(0.520f, 0.430f, 0.220f), new(0.680f, 0.570f, 0.300f), new(0.360f, 0.300f, 0.150f)),
        Kind.Danger => (new(0.480f, 0.200f, 0.190f), new(0.620f, 0.270f, 0.260f), new(0.330f, 0.140f, 0.130f)),
        _ => (new(0.320f, 0.345f, 0.370f), new(0.440f, 0.470f, 0.500f), new(0.250f, 0.270f, 0.290f)),
    };

    /// <summary>Veste de metal os cinco estados de um botão. Devolve false quando a imagem falta, e aí
    /// quem chamou fica com os StyleBoxFlat de sempre — o jogo continua, só sem relevo.</summary>
    private static bool Metal(Kind kind, Action<string, StyleBox> set)
    {
        var (idle, hover, down) = Tone(kind);
        if (MetalButton.Style(idle) is not StyleBox n) return false;
        set("normal", n);
        if (MetalButton.Style(hover) is StyleBox h) { set("hover", h); set("focus", h); }
        if (MetalButton.Style(down, pressed: true) is StyleBox p) set("pressed", p);
        if (MetalButton.Style(idle.Darkened(0.45f)) is StyleBox d) set("disabled", d);
        return true;
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
        var ic = Lbl(icon, 18); ic.AddThemeColorOverride("font_color", tint ?? Accent);
        return Counter(ic, out value, out note);
    }

    /// <summary>O mesmo mostrador com uma chapa desenhada por símbolo (Glyph.Make) em vez de um emoji. A
    /// chapa não estica com a linha: um mostrador tem duas linhas de texto e uma chapa esticada à altura
    /// delas fica um desenho alto e magro em cima de um número.</summary>
    public static PanelContainer Counter(Control icon, out Label value, out Label note)
    {
        var plate = new PanelContainer();
        plate.AddThemeStyleboxOverride("panel", Box(Ink with { A = 0.85f }, 6));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); plate.AddChild(row);
        icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(icon);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 0); row.AddChild(v);
        value = Lbl("—", 18); value.AddThemeColorOverride("font_color", Text); v.AddChild(value);
        note = Lbl("", 13); note.AddThemeColorOverride("font_color", TextDim); v.AddChild(note);
        return plate;
    }

    /// <summary>As três armas pela ordem do World.Domains, com a chapa de cada uma. É a mesma fila de
    /// abas nas escolas de guerra e no estado-maior — o jogo tem de chamar as armas sempre o mesmo.</summary>
    public static readonly string[] Arms = { "⚔ Exército", "✈ Ar", "⚓ Mar" };

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

        if (!Metal(Kind.Normal, (state, box) => t.SetStylebox(state, "Button", box)))
        {
            t.SetStylebox("normal", "Button", Fill(Surface));
            t.SetStylebox("hover", "Button", Fill(SurfaceHi, border: Accent));
            t.SetStylebox("pressed", "Button", Fill(Accent.Darkened(0.55f), border: Accent));
            t.SetStylebox("focus", "Button", Fill(SurfaceHi, border: Accent));
            t.SetStylebox("disabled", "Button", Fill(Surface.Darkened(0.45f), border: Frame.Darkened(0.4f)));
        }
        t.SetColor("font_color", "Button", Text);
        t.SetColor("font_hover_color", "Button", Accent.Lightened(0.4f));
        t.SetColor("font_pressed_color", "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", TextDim.Darkened(0.3f));

        t.SetColor("font_focus_color", "Button", Text);
        t.SetColor("font_hover_pressed_color", "Button", Colors.White);

        t.SetColor("font_color", "Label", Text);
        t.SetStylebox("panel", "PanelContainer", Box(Surface.Darkened(0.25f) with { A = 0.96f }, 10));
        t.SetStylebox("panel", "AcceptDialog", Box(Ink with { A = 0.99f }, 14));
        // O fundo de um diálogo não é o AcceptDialog: é um `Panel` que ele cria por dentro, e esse
        // procurava-se a si próprio pelo nome "Panel". Quatro dos cinco diálogos do jogo abriam com o
        // cinzento 0.25 de fábrica por trás do texto — claro, liso e do outro jogo.
        t.SetStylebox("panel", "Panel", Box(Ink with { A = 0.99f }, 14));
        // O ScrollContainer não pinta nada de propósito: quem pinta é a chapa do painel por baixo dele.
        t.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty());
        // Caixa de escrita. Só a moldura é que estava vestida: o cursor, o realce da selecção e o texto de
        // sugestão vinham de fábrica, e o realce de fábrica é azul — a única coisa azul que ficava num jogo
        // todo em aço e latão, e logo no sítio onde o jogador escreve.
        t.SetColor("font_color", "LineEdit", Text);
        t.SetColor("font_placeholder_color", "LineEdit", TextDim.Darkened(0.2f));
        t.SetColor("font_selected_color", "LineEdit", Ink);
        t.SetColor("font_uneditable_color", "LineEdit", TextDim);
        t.SetColor("caret_color", "LineEdit", Accent);
        t.SetColor("selection_color", "LineEdit", Accent with { A = 0.45f });
        t.SetStylebox("normal", "LineEdit", Fill(Ink, 6));
        t.SetStylebox("focus", "LineEdit", Fill(Ink.Lightened(0.08f), 6, border: Accent));
        t.SetStylebox("read_only", "LineEdit", Fill(Ink.Darkened(0.35f), 6, border: Frame.Darkened(0.4f)));

        // Barras de scroll finas e discretas — o dedo arrasta, a barra só mostra onde estamos. A deitada
        // faltava: quem tem uma tabela mais larga do que o ecrã via a barra de fábrica por baixo dela.
        foreach (string bar in new[] { "VScrollBar", "HScrollBar" })
        {
            t.SetStylebox("scroll", bar, Fill(Ink with { A = 0.5f }, 4, 0, 0));
            t.SetStylebox("grabber", bar, Fill(SurfaceHi, 4, 0, 0));
            t.SetStylebox("grabber_highlight", bar, Fill(Accent, 4, 0, 0));
            t.SetStylebox("grabber_pressed", bar, Fill(Accent, 4, 0, 0));
        }

        t.SetStylebox("panel", "ProgressBar", Fill(Ink, 4, 0, 0));
        t.SetStylebox("fill", "ProgressBar", Fill(Accent, 4, 0, 0));
        t.SetColor("font_color", "ProgressBar", Text);

        // A dica que aparece ao lado do dedo (ou do rato). Vinha de fábrica: chapa clara e letra escura,
        // o contrário de tudo o resto. É por aqui que a folha de diplomacia diz porque é que um botão está
        // barrado, por isso não é enfeite.
        t.SetStylebox("panel", "TooltipPanel", Box(Ink with { A = 0.97f }, 8));
        t.SetColor("font_color", "TooltipLabel", Text);
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

    /// <summary>Etiqueta que dobra de linha dentro de uma largura dada. A largura não é enfeite: um Label com
    /// autowrap e sem largura mínima é medido pelo Godot como se o texto tivesse de caber numa coluna de um
    /// carácter, e o painel que o contém nasce com milhares de píxeis de altura (o menu de jogo media
    /// 460×9033 por causa de duas notas destas). Dentro de um ScrollContainer isso só dá scroll a mais;
    /// num painel centrado deforma o painel inteiro.</summary>
    public static Label Wrapped(string text, float width, int size = Font)
    {
        var l = Lbl(text, size);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(width, 0);
        return l;
    }

    /// <summary>Véu por trás de um painel modal: escurece o mapa e apanha o toque que passa ao lado. Um menu
    /// aberto por cima de um mapa a mexer não se lê — e sem véu o dedo que falha o botão dá um pan no mundo
    /// por trás, que é o pior sítio para se descobrir que o menu ainda estava aberto. Tocar fora fecha,
    /// como em qualquer caixa modal.
    ///
    /// Nasce como irmão do painel, logo por trás dele, e acende-se e apaga-se com ele sozinho — quem o pede
    /// não tem de se lembrar do véu em cada Open/Close.</summary>
    public static ColorRect Scrim(Control panel, Action onOutside)
    {
        var veil = new ColorRect { Name = panel.Name + "Scrim", Color = Ink with { A = 0.62f }, Visible = panel.Visible };
        veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var host = panel.GetParent();
        host.AddChild(veil);
        host.MoveChild(veil, panel.GetIndex());       // por trás do painel, à frente de tudo o resto
        panel.VisibilityChanged += () => veil.Visible = panel.Visible;
        return Click(veil, onOutside);
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
        "fuel_gain" => "refinação",
        "fuel_capacity" => "depósito",
        "naval_upkeep" => "manutenção naval",
        "air_upkeep" => "manutenção aérea",
        "volunteer_cap" => "voluntários",
        "aggression" => "agressividade",
        _ => key,
    };

    public static void Clear(Node n)
    {
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }
}
