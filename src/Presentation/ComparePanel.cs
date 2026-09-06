using Godot;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Folha de comparação directa: nós contra eles, conta a conta. É a pergunta que se faz antes de
/// declarar guerra e que o jogo só respondia a quem abrisse dois painéis e fizesse contas de cabeça.
///
/// Três secções em abas metálicas — Terra, Economia, Guerra — e em cada linha os dois números lado a lado,
/// com o lado que está à frente aceso e uma barra a marcar a proporção entre eles. O que exige espionagem
/// (quartéis, cofre, ogivas) aparece com um ponto de interrogação enquanto não houver rede montada: o
/// nevoeiro vale aqui como vale no mapa. Quem faz as contas é o Core (Compare); isto só desenha.</summary>
public partial class ComparePanel : PanelContainer
{
    private static readonly string[] Sections = { "Terra", "Economia", "Guerra" };

    private Game _game = null!;
    private VBoxContainer _body = null!, _tabsHolder = null!;
    private Label _title = null!, _verdict = null!;
    private TextureRect _flagL = null!, _flagR = null!;
    private int _other, _tab;
    private string _lastKey = "";

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.42f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.078f, 0.09f, 0.098f, 0.96f)));

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6); AddChild(v);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        _flagL = Flags.Rect(20); head.AddChild(_flagL);
        _title = Ui.Lbl("", 22); head.AddChild(Ui.Grow(_title));
        _flagR = Flags.Rect(20); head.AddChild(_flagR);
        head.AddChild(Ui.Btn("Fechar", Close));

        _verdict = Ui.Lbl("", 17); _verdict.AddThemeColorOverride("font_color", Ui.Accent); v.AddChild(_verdict);
        _tabsHolder = new VBoxContainer(); v.AddChild(_tabsHolder);
        v.AddChild(Ui.Rule());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); _body.AddThemeConstantOverride("separation", 3); scroll.AddChild(_body);
    }

    /// <summary>Abre a folha contra este país (o nosso fica sempre à esquerda).</summary>
    public void Open(int otherCountryId)
    {
        _other = otherCountryId; _lastKey = "";
        _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); });
    }

    public void Close() => Visible = false;

    private void Pick(int tab) { _tab = tab; _lastKey = ""; Fill(); }

    private void Fill()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(_other, out var them)
            || !w.Countries.TryGetValue(pid, out var us)) return;

        var rows = Compare.Sheet(w, pid, pid, _other);
        var (_, _, verdict) = Compare.Tally(rows, us.Name, them.Name);
        // a aba escolhida entra na chave: sem isso trocar de secção não redesenhava nada
        string key = _tab + "|" + verdict + "|" + string.Join("|", rows.Select(r => r.Label + r.Left + r.Right + r.Winner));
        if (key == _lastKey) return;
        _lastKey = key;

        _title.Text = $"{us.Name}   vs   {them.Name}";
        _flagL.Texture = Flags.Of(us.Tag); _flagR.Texture = Flags.Of(them.Tag);
        _verdict.Text = verdict;

        Ui.Clear(_tabsHolder);
        _tabsHolder.AddChild(Ui.Tabs(Sections, _tab, Pick));

        Ui.Clear(_body);
        foreach (var r in rows.Where(r => r.Group == Sections[_tab])) _body.AddChild(Line(r));
        if (rows.Any(r => r.Group == Sections[_tab] && r.Secret))
            _body.AddChild(Note("Os números com ? só se sabem com rede de informações montada — manda espiar."));
    }

    /// <summary>Uma conta: nome ao meio, número de cada lado, e a barra que mostra a proporção entre eles.
    /// O lado que ganha fica aceso; num segredo ficam os dois apagados, porque ninguém ganha o que não sabe.</summary>
    private Control Line(CompareRow r)
    {
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 1);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); box.AddChild(row);

        var left = Ui.Lbl(r.Left, 19);
        left.HorizontalAlignment = HorizontalAlignment.Right;
        left.CustomMinimumSize = new Vector2(110, 0);
        left.AddThemeColorOverride("font_color", r.Winner == 1 ? Ui.Good : r.Secret ? Ui.TextDim : Ui.Text);
        row.AddChild(left);

        var label = Ui.Lbl(r.Label, 16);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeColorOverride("font_color", Ui.TextDim);
        row.AddChild(Ui.Grow(label));

        var right = Ui.Lbl(r.Right, 19);
        right.CustomMinimumSize = new Vector2(110, 0);
        right.AddThemeColorOverride("font_color", r.Winner == 2 ? Ui.Danger : r.Secret ? Ui.TextDim : Ui.Text);
        row.AddChild(right);

        box.AddChild(Split(r));
        return box;
    }

    /// <summary>Barra de dois lados: o quanto de cada um, na mesma linha. Um segredo desenha-se cinzento e
    /// ao meio, para não sugerir uma vantagem que não se conhece.</summary>
    private static Control Split(CompareRow r)
    {
        float a = Parse(r.Left), b = Parse(r.Right);
        float share = r.Secret || a + b <= 0f ? 0.5f : a / (a + b);
        var bar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 6) };
        bar.AddThemeConstantOverride("separation", 2);
        var l = new ColorRect { Color = r.Secret ? Ui.SurfaceHi : Ui.Good.Darkened(r.Winner == 1 ? 0f : 0.45f), SizeFlagsStretchRatio = Mathf.Max(0.02f, share) };
        var rr = new ColorRect { Color = r.Secret ? Ui.SurfaceHi : Ui.Danger.Darkened(r.Winner == 2 ? 0f : 0.45f), SizeFlagsStretchRatio = Mathf.Max(0.02f, 1f - share) };
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill; rr.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bar.AddChild(l); bar.AddChild(rr);
        return bar;
    }

    private static float Parse(string s) => float.TryParse(s, out var v) ? MathF.Max(0f, v) : 0f;

    private static Label Note(string text)
    {
        var l = Ui.Lbl(text, 14);
        l.AddThemeColorOverride("font_color", Ui.TextDim);
        return l;
    }

    /// <summary>--smoke: abre a folha contra este país, passa pelas três abas e diz quantas linhas ficaram
    /// desenhadas na última.</summary>
    public int Smoke(int otherCountryId)
    {
        _other = otherCountryId;
        for (int i = 0; i < Sections.Length; i++) Pick(i);
        return _body.GetChildCount();
    }
}
