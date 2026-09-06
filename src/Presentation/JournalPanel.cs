using Godot;

namespace WarGame.Presentation;

/// <summary>Painel "Jornal": histórico das notícias do jogador (tudo o que passou pelos toasts do Hud),
/// datado e do mais recente para o mais antigo. O Hud alimenta via Add; guarda as últimas 200 entradas
/// só em memória — o jornal recomeça a cada sessão.</summary>
public partial class JournalPanel : PanelContainer
{
    private const int Max = 200;
    private Game _game = null!;
    private VBoxContainer _body = null!;
    private readonly List<(int Day, string Text)> _entries = new();
    private int _lastCount = -1;

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.55f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 0.95f)));
        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        head.AddChild(Ui.Grow(Ui.Lbl("Jornal", 22)));
        head.AddChild(Ui.Btn("Fechar", Close));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Add(string text)
    {
        _entries.Add((_game.World.Clock.Day, text));
        if (_entries.Count > Max) _entries.RemoveAt(0);
        if (Visible) Fill();
    }

    public void Open() { Fill(); Visible = true; Ui.FadeIn(this); }
    public void Close() => Visible = false;

    private void Fill()
    {
        if (_entries.Count == _lastCount) return;
        _lastCount = _entries.Count;
        Ui.Clear(_body);
        if (_entries.Count == 0) { _body.AddChild(Ui.Lbl("Ainda sem notícias.", 18)); return; }
        var clock = _game.World.Clock;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var (day, text) = _entries[i];
            var row = new HBoxContainer(); _body.AddChild(row);
            var date = Ui.Lbl(clock.Date.AddDays(day - clock.Day).ToString("yyyy-MM-dd"), 16);
            date.Modulate = new Color(1, 1, 1, 0.6f); date.CustomMinimumSize = new Vector2(130, 0);
            row.AddChild(date);
            var lbl = Ui.Grow(Ui.Lbl(text, 18)); lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(lbl);
        }
    }
}
