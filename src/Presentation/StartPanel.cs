using Godot;
using WarGame.Core.Commands;

namespace WarGame.Presentation;

/// <summary>Porta de entrada do jogo: sem jogador escolhido, é a única coisa que se vê — antes disto o jogo
/// caía a meio do mapa com as unidades todas por explicar, e a única forma de escolher país era um "Jogar
/// como X" escondido dentro da ficha de região, que só abre por duplo toque em terra vazia (um gesto que
/// ninguém descobre sozinho a abrir o jogo pela 1ª vez).
///
/// Três passos, um por cima do outro: país (lista pesquisável por nome), dificuldade, "Começar". Não se
/// fecha por fora — nem toque no véu, nem o botão voltar — porque não é um painel qualquer que se consulta e
/// se dispensa, é o que falta para o resto do jogo fazer sentido. Só "Começar" o fecha.
///
/// Lê o World só em Open/Fill (mundo parado: sem jogador o relógio está a 0) e muta só em Começar, por
/// Game.Dispatch + World.ApplyDifficulty, tal como o "Jogar como X" que substitui.</summary>
public partial class StartPanel : PanelContainer
{
    private Game _game = null!;
    private MapView _map = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _body = null!, _stack = null!, _rowsBox = null!;
    private LineEdit _search = null!;

    private enum Step { Country, Difficulty }
    private Step _step;
    private List<(int Id, string Tag, string Name)> _countries = new();
    private int? _chosenCountry;
    private string _chosenDifficulty = "normal";
    private int _rows;

    /// <summary>Largura útil da lista e das fichas.</summary>
    private const float Wide = 480f;

    public void Setup(Game game, MapView map)
    {
        _game = game; _map = map;
        Visible = false;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.99f), 16));
        Ui.Scrim(this, () => { });   // sem saída por fora: só "Começar" fecha isto

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_scroll);
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(Wide, 0) };
        v.AddThemeConstantOverride("separation", 10);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(v);
        _stack = v;
        _body = new VBoxContainer(); _body.AddThemeConstantOverride("separation", 8); v.AddChild(_body);
    }

    public void Open()
    {
        var w = _game.World;
        _step = Step.Country; _chosenCountry = null;
        _countries = w.Countries.Values.OrderBy(c => c.Name).Select(c => (c.Id, c.Tag, c.Name)).ToList();
        _chosenDifficulty = w.Difficulty ?? "normal";
        Fill(); Fit(); Visible = true; Ui.FadeIn(this); Ui.SlideIn(this);
    }

    private void Fit()
    {
        float tall = _stack.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(Wide, MathF.Min(tall, GetViewportRect().Size.Y * 0.86f));
    }

    public void Close() => Visible = false;

    /// <summary>--smoke: abre à força (mesmo já havendo jogador), conta as linhas de país desenhadas e fecha
    /// sem tocar no jogador escolhido de verdade.</summary>
    public int Smoke()
    {
        var w = _game.World;
        _step = Step.Country; _chosenCountry = null;
        _countries = w.Countries.Values.OrderBy(c => c.Name).Select(c => (c.Id, c.Tag, c.Name)).ToList();
        _chosenDifficulty = w.Difficulty ?? "normal";
        Fill(); Fit(); Visible = true;
        int n = _rows;
        Visible = false;
        return n;
    }

    private void Fill()
    {
        try
        {
            Ui.Clear(_body);
            if (_step == Step.Country) FillCountry(); else FillDifficulty();
            Fit();
        }
        catch (Exception ex) { GD.PushError("StartPanel: " + ex); }
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
        _rows = 0;
        foreach (var c in shown) { _rowsBox.AddChild(CountryRow(c)); _rows++; }
        if (_rows == 0) _rowsBox.AddChild(Ui.Lbl("Nenhum país com esse nome", 14));
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
            // o que o nível faz, em números — como no menu de jogo, para não se escolher às cegas.
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

    /// <summary>"Jogar como <país>": ChoosePlayerCommand, dificuldade escolhida, relógio a 1×, câmara na
    /// capital. O mesmo desfecho que o antigo "Jogar como X" da ficha de região.</summary>
    private void Begin() => _game.RunWhenIdle(() =>
    {
        if (_chosenCountry is not int id) return;
        var w = _game.World;
        var err = _game.Dispatch(new ChoosePlayerCommand(id));
        if (err is not null) { _game.Notify(err); return; }
        w.ApplyDifficulty(_chosenDifficulty);
        w.Clock.Speed = 1;
        var c = w.Countries[id];
        if (w.Regions.TryGetValue(c.CapitalRegionId, out var cap)) _map.Focus(new Vector2(cap.CenterX, cap.CenterY), 1f);
        _game.Notify($"Jogas com {c.Name}");
        Close();
    });
}
