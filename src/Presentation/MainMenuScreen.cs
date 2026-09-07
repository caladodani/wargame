using Godot;
using WarGame.Core.Commands;

namespace WarGame.Presentation;

/// <summary>Ecrã inicial de verdade — o run/main_scene do projecto — não um modal por cima do mapa já
/// carregado. Sem jogador escolhido é a única coisa no ecrã: escolher país (lista pesquisável por nome),
/// escolher dificuldade, "Começar". Só depois disso é que a cena troca para Main.tscn, e aí sim o mapa e as
/// unidades todas se desenham.
///
/// Antes disto a única porta de entrada era o "Jogar como X" escondido na ficha de região — e depois de o
/// toque simples deixar de a abrir, só se chegava lá por duplo toque em terra vazia, gesto que ninguém
/// descobre sozinho a abrir o jogo pela 1ª vez. E mesmo antes disso, o jogo caía sempre a meio do mapa com
/// as unidades todas por explicar: esta cena existe para nunca mais isso acontecer.
///
/// Simétrico com Hud: se chegar aqui já com jogador escolhido (o Game é autoload e sobrevive à troca de
/// cena — uma campanha em curso, ou um slot que não estava vazio), salta logo para o mapa sem mostrar nada.
/// Hud faz o oposto: se Main.tscn for recarregado sem jogador (troca para um slot vazio), volta para aqui.</summary>
public partial class MainMenuScreen : Control
{
    private const string GameScene = "res://scenes/Main.tscn";

    private Game _game = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _body = null!, _stack = null!, _rowsBox = null!;
    private LineEdit _search = null!;

    private enum Step { Country, Difficulty }
    private Step _step;
    private List<(int Id, string Tag, string Name)> _countries = new();
    private int? _chosenCountry;
    private string _chosenDifficulty = "normal";

    private const float Wide = 480f;

    public override void _Ready()
    {
        try
        {
            _game = GetNode<Game>("/root/Game");
            if (_game.PlayerId is not null) { GetTree().ChangeSceneToFile(GameScene); return; }

            Settings.Apply(GetWindow());
            GetTree().Root.Theme = Ui.Theme();

            var backdrop = new ColorRect { Color = Ui.Ink };
            backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(backdrop);

            var title = new VBoxContainer();
            title.SetAnchorsPreset(LayoutPreset.CenterTop);
            title.AddThemeConstantOverride("separation", 2);
            var name = Ui.Lbl("WARGAME", 34); name.AddThemeColorOverride("font_color", Ui.Accent); title.AddChild(name);
            var tag = Ui.Lbl("uma campanha por escolher", 15); title.AddChild(tag);
            AddChild(title);
            title.Position = new Vector2(-title.GetCombinedMinimumSize().X / 2f, 48);

            var card = new PanelContainer();
            card.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.99f), 16));
            AddChild(card);

            _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            card.AddChild(_scroll);
            var v = new VBoxContainer { CustomMinimumSize = new Vector2(Wide, 0) };
            v.AddThemeConstantOverride("separation", 10);
            v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _scroll.AddChild(v);
            _stack = v;
            _body = new VBoxContainer(); _body.AddThemeConstantOverride("separation", 8); v.AddChild(_body);

            var w = _game.World;
            _countries = w.Countries.Values.OrderBy(c => c.Name).Select(c => (c.Id, c.Tag, c.Name)).ToList();
            _chosenDifficulty = w.Difficulty ?? "normal";
            Fill();
            Ui.FadeIn(card);
        }
        catch (Exception ex) { GD.PushError("MainMenuScreen._Ready: " + ex); }
    }

    private void Fit()
    {
        float tall = _stack.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(Wide, MathF.Min(tall, GetViewportRect().Size.Y * 0.8f));
    }

    private void Fill()
    {
        try
        {
            Ui.Clear(_body);
            if (_step == Step.Country) FillCountry(); else FillDifficulty();
            Fit();
        }
        catch (Exception ex) { GD.PushError("MainMenuScreen: " + ex); }
    }

    private void FillCountry()
    {
        _body.AddChild(Ui.Head("Escolher país"));
        _search = new LineEdit { PlaceholderText = "Procurar país…", CustomMinimumSize = new Vector2(0, 48) };
        _search.AddThemeFontSizeOverride("font_size", 20);
        _search.TextChanged += _ => RefreshRows();
        _body.AddChild(_search);
        _rowsBox = new VBoxContainer(); _rowsBox.AddThemeConstantOverride("separation", 6);
        _body.AddChild(_rowsBox);
        RefreshRows();
        _search.GrabFocus();
    }

    /// <summary>Refaz só as linhas de país (a caixa de pesquisa fica — refazê-la a cada letra perdia o foco).</summary>
    private void RefreshRows()
    {
        Ui.Clear(_rowsBox);
        string q = _search.Text.Trim();
        var shown = q.Length == 0 ? _countries
            : _countries.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                                  || c.Tag.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var c in shown) _rowsBox.AddChild(CountryRow(c));
        if (shown.Count == 0) _rowsBox.AddChild(Ui.Lbl("Nenhum país com esse nome", 14));
        Fit();
    }

    private Control CountryRow((int Id, string Tag, string Name) c)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(0, 48) };
        card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface with { A = 0.85f }, 6));
        card.AddChild(Ui.Grow(Ui.Crest(c.Tag, c.Name, "", 20)));
        return Ui.Click(card, () => { _chosenCountry = c.Id; _step = Step.Difficulty; Fill(); });
    }

    private void FillDifficulty()
    {
        var w = _game.World;
        if (_chosenCountry is not int id || !w.Countries.TryGetValue(id, out var c))
        { _step = Step.Country; Fill(); return; }

        _body.AddChild(Ui.Head("Dificuldade"));
        _body.AddChild(Ui.Crest(c.Tag, c.Name, "país escolhido", 22));

        if (w.DifficultyDefs.Count > 0)
        {
            var defs = w.DifficultyDefs.Values.OrderBy(d => d.Sort).ToList();
            int active = Math.Max(0, defs.FindIndex(d => d.Id == _chosenDifficulty));
            _body.AddChild(Ui.Tabs(defs.Select(d => d.Name).ToList(), active, i => { _chosenDifficulty = defs[i].Id; Fill(); }));
            // o que o nível faz, em números — como no menu ☰, para não se escolher às cegas.
            var sheet = new GridContainer { Columns = 2 };
            sheet.AddThemeConstantOverride("h_separation", 14);
            sheet.AddThemeConstantOverride("v_separation", 2);
            foreach (var (key, value) in defs[active].Effects.OrderBy(e => e.Key))
                sheet.AddChild(Ui.Wrapped("· " + GameMenu.Effect(key, value), Wide / 2f - 14f, 14));
            _body.AddChild(sheet);
        }

        _body.AddChild(Ui.Rule());
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        row.AddChild(Ui.Grow(Ui.Btn("‹ Mudar país", () => { _step = Step.Country; Fill(); })));
        row.AddChild(Ui.Grow(Ui.Btn("Começar", Begin, 0, Ui.Kind.Primary)));
        _body.AddChild(row);
    }

    /// <summary>ChoosePlayerCommand + dificuldade escolhida + relógio a 1×, e só então troca para o mapa —
    /// o mesmo desfecho que o antigo "Jogar como X" da ficha de região, mas antes de haver mapa para focar
    /// (isso faz o Hud, ao abrir Main.tscn já com jogador escolhido).</summary>
    private void Begin() => _game.RunWhenIdle(() =>
    {
        if (_chosenCountry is not int id) return;
        var w = _game.World;
        var err = _game.Dispatch(new ChoosePlayerCommand(id));
        if (err is not null) { _game.Notify(err); return; }
        w.ApplyDifficulty(_chosenDifficulty);
        w.Clock.Speed = 1;
        GetTree().ChangeSceneToFile(GameScene);
    });
}
