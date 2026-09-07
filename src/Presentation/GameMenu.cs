using Godot;

namespace WarGame.Presentation;

/// <summary>Menu de jogo (botão ☰ da barra): guardar, trocar de slot, escolher dificuldade, o tamanho da
/// interface, recomeçar e sair. A dificuldade vem da tabela difficulty e reescreve regras por
/// World.ApplyDifficulty — muda a meio do jogo e vale já no dia seguinte. Recomeçar e sair passam por
/// confirmação.
///
/// Era uma pilha de botões todos iguais numa caixa sem cabeçalho: nove filas de 48 px onde nada dizia o que
/// era ordem, o que era escolha e o que era irreversível, e onde as duas escolhas exclusivas (dificuldade e
/// tamanho) gastavam oito linhas com ○ e ● à frente do nome. Agora tem o brasão de quem se joga em cima,
/// secções em versaletes como as folhas do estado-maior, as escolhas em filas de chapas de metal (as mesmas
/// abas do resto do jogo) e o que se paga caro — recomeçar, sair — separado no fundo. Por trás corre um véu
/// que escurece o mapa e apanha o dedo que passa ao lado.</summary>
public partial class GameMenu : PanelContainer
{
    private Game _game = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _body = null!;
    private ConfirmationDialog _confirmNew = null!, _confirmQuit = null!;
    private Action _openSlots = null!;
    private Action _openReport = () => { };

    /// <summary>Largura útil do menu: a caixa menos a folga da chapa. É por ela que as notas dobram.</summary>
    private const float Wide = 428f;

    public void Setup(Game game, Action openSlots, Action openReport)
    {
        _game = game; _openSlots = openSlots; _openReport = openReport;
        Visible = false;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.97f), 16));
        Ui.Scrim(this, Close);

        _confirmNew = Ui.Dialog(this, () => { Close(); _game.NewGame(); });
        _confirmNew.DialogText = "Começar um jogo novo? O jogo actual perde-se.";
        _confirmQuit = Ui.Dialog(this, () => { _game.Save(); GetTree().Quit(); });
        _confirmQuit.DialogText = "Guardar e sair do jogo?";

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_scroll);
        _body = new VBoxContainer { CustomMinimumSize = new Vector2(Wide, 0) };
        _body.AddThemeConstantOverride("separation", 8);
        _body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(_body);
    }

    public void Open() { Fill(); Fit(); Visible = true; Ui.FadeIn(this); }

    /// <summary>O menu cresce com o que lá está dentro, mas nunca para lá do ecrã: num telemóvel em pé, com a
    /// interface no degrau "Grande", uma caixa de mil píxeis saía por cima e por baixo da tela e as ordens do
    /// fundo — recomeçar, sair — ficavam fora de alcance. Cabendo, não há barra de scroll nenhuma.</summary>
    private void Fit()
    {
        float tall = _body.GetCombinedMinimumSize().Y;
        float room = GetViewportRect().Size.Y * 0.86f;
        _scroll.CustomMinimumSize = new Vector2(Wide, MathF.Min(tall, room));
    }
    public void Close() => Visible = false;
    public void Toggle() { if (Visible) Close(); else Open(); }

    /// <summary>--smoke: abre o menu já cheio e diz o que ficou lá dentro — secções, botões e as duas filas
    /// de chapas. Sem isto o menu era a única coisa da interface que ninguém media.</summary>
    public string Smoke()
    {
        Open();
        int rows = _body.GetChildCount();
        int buttons = _body.GetChildren().OfType<Control>().Sum(Buttons);
        int plates = _body.GetChildren().OfType<MetalTabs>().Count();
        var size = GetCombinedMinimumSize();
        bool rolls = _body.GetCombinedMinimumSize().Y > _scroll.CustomMinimumSize.Y + 1f;
        Close();
        return $"{rows} filas, {buttons} botões, {plates} filas de chapas, {size.X:0}×{size.Y:0}px"
             + (rolls ? " (com rolo)" : " (inteiro no ecrã)");
    }

    private static int Buttons(Control c) =>
        (c is Button ? 1 : 0) + c.GetChildren().OfType<Control>().Sum(Buttons);

    private void Fill()
    {
        Ui.Clear(_body);
        var w = _game.World;
        int? pid = _game.PlayerId;
        string tag = pid is int id && w.Countries.TryGetValue(id, out var me) ? me.Tag : "—";
        string who = pid is int id2 && w.Countries.TryGetValue(id2, out var me2) ? me2.Name : "sem país escolhido";
        string version = ProjectSettings.GetSetting("application/config/version").AsString();
        _body.AddChild(Ui.Crest(tag, "Menu", $"{who}  ·  dia {w.Clock.Day}  ·  slot {_game.Slot}  ·  v{version}", 26));

        _body.AddChild(Ui.Head("Jogo"));
        _body.AddChild(Ui.Btn("▶  Continuar", Close, 0, Ui.Kind.Primary));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        row.AddChild(Ui.Grow(Ui.Btn("Guardar", () => { _game.Save(); _game.Notify("Jogo guardado"); Close(); })));
        row.AddChild(Ui.Grow(Ui.Btn("Jogos guardados", () => { Close(); _openSlots(); })));
        _body.AddChild(row);
        _body.AddChild(Ui.Btn("Resumo da campanha", () => { Close(); _openReport(); }));

        if (w.DifficultyDefs.Count > 0)
        {
            _body.AddChild(Ui.Head("Dificuldade"));
            string current = w.Difficulty ?? "normal";
            var defs = w.DifficultyDefs.Values.OrderBy(d => d.Sort).ToList();
            int active = Math.Max(0, defs.FindIndex(d => d.Id == current));
            _body.AddChild(Ui.Tabs(defs.Select(d => d.Name).ToList(), active, i => SetDifficulty(defs[i].Id)));
            // o que o nível faz, em números: "muda a produção e o rendimento" não deixava escolher nada.
            // Em duas colunas, como uma folha de estado-maior — em texto corrido eram quatro linhas de parágrafo.
            var sheet = new GridContainer { Columns = 2 };
            sheet.AddThemeConstantOverride("h_separation", 14);
            sheet.AddThemeConstantOverride("v_separation", 2);
            foreach (var (key, value) in defs[active].Effects.OrderBy(e => e.Key))
                sheet.AddChild(Ui.Wrapped("· " + Effect(key, value), Wide / 2f - 14f, 14));
            _body.AddChild(sheet);
        }

        // Tamanho da interface: o jogo desenha-se numa tela esticada para o ecrã e num telemóvel em pé a
        // letra saía pequena de mais para painéis de quinze linhas. Aqui muda-se tudo de uma vez — letra,
        // chapas e a altura do dedo — e fica guardado fora do save, porque é do ecrã e não do jogo.
        _body.AddChild(Ui.Head("Interface"));
        _body.AddChild(Ui.Tabs(Settings.ScaleNames, Settings.ScaleIndex, SetScale));
        _body.AddChild(Ui.Lbl($"Letra, chapas e altura do dedo a ×{Settings.Scale:0.00}.", 14));

        _body.AddChild(Ui.Head("Sessão"));
        var danger = new HBoxContainer(); danger.AddThemeConstantOverride("separation", 6);
        danger.AddChild(Ui.Grow(Ui.Btn("Recomeçar", () => _confirmNew.PopupCentered(), 0, Ui.Kind.Danger)));
        danger.AddChild(Ui.Grow(Ui.Btn("Sair do jogo", () => _confirmQuit.PopupCentered(), 0, Ui.Kind.Danger)));
        _body.AddChild(danger);
    }

    /// <summary>O que uma regra de dificuldade quer dizer em português. As chaves são as do
    /// difficulty_effect; uma que ainda não esteja aqui sai com a chave e o valor, que é melhor do que
    /// desaparecer da nota.</summary>
    internal static string Effect(string key, float value) => key switch
    {
        "build_min_days" => $"obras em {value:0} dias no mínimo",
        "new_division_org" => $"divisões novas com {value:0} de organização",
        "points_per_million" => $"{value:0.00} de produção por milhão de habitantes",
        "manpower_per_million_daily" => $"{value:0} homens por dia por milhão",
        "ai_general_reserve" => $"IA guarda {value:0} antes de contratar",
        _ => $"{key} {value:0.##}",
    };

    /// <summary>Muda o tamanho de tudo e redesenha o menu já no tamanho novo, para se ver a escolha.</summary>
    private void SetScale(int step)
    {
        Settings.SetScale(step, GetWindow());
        _game.Notify($"Interface: {Settings.ScaleName}");
        Fill();
    }

    /// <summary>Troca de nível a meio do jogo: aplica as regras novas e guarda para não se perder.</summary>
    private void SetDifficulty(string id) => _game.RunWhenIdle(() =>
    {
        _game.World.ApplyDifficulty(id);
        _game.Save();
        _game.Notify($"Dificuldade: {_game.World.DifficultyDefs[id].Name}");
        Fill();
    });
}
