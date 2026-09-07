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
    private Control? _title;
    private Label _titleName = null!, _titleTag = null!;
    private LineEdit _search = null!;

    private enum Step { Country, Difficulty }
    private Step _step;
    private List<(int Id, string Tag, string Name)> _countries = new();
    private int? _chosenCountry;
    private string _chosenDifficulty = "normal";

    /// <summary>Em paisagem o cartão não passa disto: um menu com 1400 de largo e uma linha de país por
    /// linha ficava uma tira de texto perdida no meio do ecrã.</summary>
    private const float WideMax = 560f;

    private const int Margin = 20;   // respiro entre o cartão e a borda do ecrã
    private const int CardPad = 16;  // moldura do PanelContainer (Ui.Box), de cada lado

    /// <summary>Largura útil do conteúdo do cartão, tirada do ecrã e não de um número fixo — e lida na hora,
    /// que rodar o telemóvel muda-a. Em retrato leva a largura toda menos as margens e a moldura (com o 480
    /// fixo de antes, num viewport de 1152 sobravam 320 de vazio de cada lado e o menu parecia um cartão de
    /// visita); em paisagem trava no WideMax e fica centrado. Descontar a moldura é o que impede o cartão de
    /// ficar 32 mais largo do que o ecrã: o que se mede aqui é o miolo, não o painel.</summary>
    private float Wide
    {
        get
        {
            var v = GetViewportRect().Size;
            float cabe = MathF.Max(240f, v.X - 2 * (Margin + CardPad));
            return v.Y > v.X ? cabe : MathF.Min(WideMax, cabe);
        }
    }

    public override void _Ready()
    {
        try
        {
            _game = GetNode<Game>("/root/Game");
            // Adiada: o ChangeSceneToFile tira já a cena actual, e aqui o /root ainda está a metê-la —
            // dava "Parent node is busy adding/removing children, remove_child() can't be called at this time".
            if (_game.PlayerId is not null) { Callable.From(() => GetTree().ChangeSceneToFile(GameScene)).CallDeferred(); return; }

            Settings.Apply(GetWindow());
            GetTree().Root.Theme = Ui.Theme();

            var backdrop = new ColorRect { Color = Ui.Ink };
            backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(backdrop);

            // Tudo dentro de contentores, sem contas de posição à mão. Antes o título levava
            // Position = (-largura/2, 48) — e Position é o canto superior esquerdo em coordenadas do pai, não
            // um deslocamento a partir da âncora: ia parar a x negativo e via-se "AME". O cartão levava
            // SetAnchorsAndOffsetsPreset(Center) enquanto ainda media 0, o que lhe pregava o canto ao meio do
            // ecrã e o fazia crescer para fora pela direita. Num ecrã largo quase não se notava; em retrato era
            // metade do menu de fora.
            var margin = new MarginContainer();
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                margin.AddThemeConstantOverride(side, Margin);
            AddChild(margin);

            var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            column.AddThemeConstantOverride("separation", 24);
            margin.AddChild(column);

            var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            title.AddThemeConstantOverride("separation", 2);
            var name = Ui.Lbl("WARGAME"); name.AddThemeColorOverride("font_color", Ui.Accent);
            name.HorizontalAlignment = HorizontalAlignment.Center; title.AddChild(name);
            var tag = Ui.Lbl("uma campanha por escolher");
            tag.HorizontalAlignment = HorizontalAlignment.Center; title.AddChild(tag);
            _titleName = name; _titleTag = tag;
            column.AddChild(title);
            _title = title;

            var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.99f), CardPad));
            column.AddChild(card);

            // Rodar o ecrã muda a largura que cabe e a altura que sobra para a lista.
            GetViewport().SizeChanged += OnResize;

            _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            card.AddChild(_scroll);
            var v = new VBoxContainer();
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

    /// <summary>Altura livre para o cartão: o que sobra do ecrã depois do título e das margens. Sem isto a
    /// lista de 235 países empurrava o título para fora do ecrã em vez de rolar dentro do cartão.</summary>
    private void Fit()
    {
        float wide = Wide;
        // O título acompanha o cartão em vez de ficar preso a 34: num ecrã de telemóvel ao alto isso dava
        // um letreiro do tamanho de uma etiqueta em cima de um painel que ocupa o ecrã todo.
        int fs = (int)Math.Clamp(wide / 13f, 30f, 64f);
        _titleName.AddThemeFontSizeOverride("font_size", fs);
        _titleTag.AddThemeFontSizeOverride("font_size", (int)Math.Clamp(fs / 2.2f, 14f, 26f));

        _stack.CustomMinimumSize = new Vector2(wide, 0);
        float tall = _stack.GetCombinedMinimumSize().Y;
        float livre = GetViewportRect().Size.Y - (_title?.GetCombinedMinimumSize().Y ?? 0f) - 120f;
        _scroll.CustomMinimumSize = new Vector2(wide, MathF.Min(tall, MathF.Max(200f, livre)));
    }

    /// <summary>Ecrã rodado ou janela mudada: a largura que cabe é outra. Na lista chega ajustar (refazê-la
    /// perdia o que estivesse escrito na pesquisa); na dificuldade refaz-se, que o texto das linhas é
    /// quebrado à largura do cartão.</summary>
    private void OnResize()
    {
        if (_stack is null) return;
        if (_step == Step.Difficulty) Fill(); else Fit();
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
