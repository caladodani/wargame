using Godot;
using Timer = Godot.Timer;
using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Barra de topo (data, velocidade, país, dinheiro, exército, Guardar/Novo jogo), toast de eventos e
/// os painéis de região e produção. Lê o World só em TickCompleted/StateChanged ou via RunWhenIdle.</summary>
public partial class Hud : CanvasLayer
{
    private Game _game = null!;
    private MapView _map = null!;
    private Label _date = null!, _country = null!, _toast = null!, _hint = null!;
    // Mostradores da barra de topo: cada um é um número grande com a sua nota por baixo.
    private Label _money = null!, _moneyNote = null!, _men = null!, _menNote = null!, _divs = null!, _divsNote = null!;
    // Fábricas civis, militares e estaleiros: a fila de mostradores industriais do HoI4.
    private Label _civ = null!, _civNote = null!, _mil = null!, _milNote = null!, _yard = null!, _yardNote = null!;
    private Label _xp = null!, _xpNote = null!, _airXp = null!, _airXpNote = null!, _seaXp = null!, _seaXpNote = null!;
    private PanelContainer _yardPlate = null!, _xpPlate = null!, _airXpPlate = null!, _seaXpPlate = null!;
    private PanelContainer _season = null!;
    private string _seasonPainted = "";
    private TextureRect _playerFlag = null!;
    private SpeedRibbon _speed = null!;
    private PanelContainer _toastBox = null!;
    private ColorRect _accent = null!;
    private Timer _toastTimer = null!;
    private Tween? _toastTween;   // animação de entrada/saída do toast (morre e recomeça a cada mensagem)
    private AcceptDialog _slots = null!;
    private ConfirmationDialog? _offerDialog;      // proposta do inimigo (troca ou paz)
    private int _offerFrom;
    private string _offerKind = "prisioneiros";
    private int _offerRegion;                     // região da cedência a que o diálogo está a responder
    private Button _offers = null!;                // distintivo das propostas em cima da mesa
    private int _offersShown = -1;
    private RegionPanel _region = null!;
    private ArmySelect _multiSel = null!;
    private GameMenu _menu = null!;
    private ProductionPanel _production = null!;
    private CountryPanel _countryPanel = null!;
    private WorldPanel _worldPanel = null!;
    private WarPanel _warPanel = null!;
    private ArmyPanel _armyPanel = null!;
    private EndScreen _end = null!;
    private MiniMap _mini = null!;
    private MapModeBar _modeBar = null!;
    private BattlePanel _battle = null!;
    private FocusPanel _focusTree = null!;
    private DoctrinePanel _doctrines = null!;
    private AlertStrip _alerts = null!;
    private DefeatKlaxon _klaxon = null!;
    private Sfx _sfx = null!;                      // banco de sons gerado em código
    private Button _mute = null!;                  // altifalante da barra de topo
    private PanelContainer _toastPlate = null!;    // chapa do timbre à esquerda do cartão do aviso
    private Label _toastIcon = null!;
    private ColorRect _toastClock = null!;         // barra que escoa os segundos do aviso
    private StyleBoxFlat _toastSkin = null!;
    private Sfx.Kind _toastKind = Sfx.Kind.None;
    private ComparePanel _compare = null!;
    private UpdateChip _update = null!;
    private JournalPanel _journal = null!;
    private readonly List<IDisposable> _subs = new();
    private readonly HashSet<(int, int)> _whitePeace = new();   // guerras fechadas por paz branca (o WarEnded seguinte muda o toast)
    private readonly HashSet<(int, int)> _negotiated = new();   // idem para a paz negociada: a notícia sai no PeaceSigned
    private bool _smoke, _smoked;
    private int _frames;                                        // painéis vestidos com a moldura de metal
    private ulong _backAt;   // Time.GetTicksMsec do último "voltar" sem painel aberto

    public override void _Ready()
    {
        try
        {
            _game = GetNode<Game>("/root/Game");
            _map = GetNode<MapView>("../MapView");
            _smoke = OS.GetCmdlineUserArgs().Contains("--smoke");
            Settings.Apply(GetWindow());         // tamanho da interface antes de se desenhar o que quer que seja
            GetTree().Root.Theme = Ui.Theme();   // tema da janela inteira: painéis, botões e diálogos de uma vez
            BuildTopBar(); BuildToast();
            _production = new ProductionPanel(); AddChild(_production); _production.Setup(_game);
            _countryPanel = new CountryPanel(); AddChild(_countryPanel); _countryPanel.Setup(_game);
            _worldPanel = new WorldPanel(); AddChild(_worldPanel); _worldPanel.Setup(_game, _countryPanel);
            _warPanel = new WarPanel(); AddChild(_warPanel); _warPanel.Setup(_game);
            _warPanel.OnShowRegion = ShowRegion;                     // "Ver no mapa" das cedências
            _journal = new JournalPanel(); AddChild(_journal); _journal.Setup(_game);
            _region = new RegionPanel(); AddChild(_region); _region.Setup(_game, _map, _production, _countryPanel);
            _multiSel = new ArmySelect(); AddChild(_multiSel); _multiSel.Setup(_game, _map);
            _armyPanel = new ArmyPanel(); AddChild(_armyPanel); _armyPanel.Setup(_game, _map, _multiSel);
            _map.Routes.Watch(_region, _multiSel);   // o mapa nasce antes dos painéis: a selecção liga-se aqui
            _end = new EndScreen(); AddChild(_end); _end.Setup(_game);
            _menu = new GameMenu(); AddChild(_menu); _menu.Setup(_game, OpenSlots, () => _end.Show(CampaignReport.Ongoing));
            _mini = new MiniMap(); AddChild(_mini); _mini.Setup(_map);
            _modeBar = new MapModeBar(); AddChild(_modeBar); _modeBar.Setup(_game, _map.Regions);
            _compare = new ComparePanel(); AddChild(_compare); _compare.Setup(_game);
            _battle = new BattlePanel(); AddChild(_battle); _battle.Setup(_game);
            _region.OnBattle = id => _battle.Open(id);
            _focusTree = new FocusPanel(); AddChild(_focusTree); _focusTree.Setup(_game);
            _countryPanel.OnFocusTree = id => _focusTree.Open(id);
            _doctrines = new DoctrinePanel(); AddChild(_doctrines); _doctrines.Setup(_game);
            _countryPanel.OnDoctrines = id => _doctrines.Open(id);
            _alerts = new AlertStrip(); AddChild(_alerts); _alerts.Setup(_game);
            _alerts.OnGoTo = ShowRegion;
            _alerts.OnOpen = id =>
            {
                if (id == "offers") OpenWar();
                else if (id == "queue") OpenProduction();      // fila parada: abre-se a fila, não o país
                else if (id == "research") OpenCountry();
                else if (id == "battle" && _game.World.ActiveBattles.FirstOrDefault(b =>
                             b.AttackerCountryId == _game.PlayerId
                             || _game.World.Regions.GetValueOrDefault(b.RegionId)?.ControllerId == _game.PlayerId) is Battle mine)
                    _battle.Open(mine.RegionId);
            };

            // Caixilharia de metal: todos os painéis flutuantes ganham cantoneiras e rebites de uma vez. Fica
            // de fora a barra de topo (o texto encosta às arestas) e a tira de avisos, que é fina de propósito.
            _frames = PanelFrame.DressAll(this, _alerts, GetNode<PanelContainer>("Top"));

            // Depois da caixilharia: o cartaz do klaxon é de alarme e não leva cantoneiras de painel.
            _klaxon = new DefeatKlaxon(); AddChild(_klaxon); _klaxon.Setup();
            _sfx = new Sfx { Name = "Sfx" }; AddChild(_sfx);   // as vozes fazem-se no _Ready dele

            _map.RegionTapped += OnRegionTapped;
            _map.RegionLongPressed += rid => _multiSel.LongPress(rid);
            _map.RegionDoubleTapped += rid => _multiSel.DoubleTap(rid);
            _game.TickCompleted += OnTick;
            _game.StateChanged += RefreshAll;
            _game.CommandFailed += Toast;
            SubscribeEvents();
            _game.RunWhenIdle(RefreshAll);
            _update.Check();                     // e, se houver versão nova, ela começa a vir já
        }
        catch (Exception ex) { GD.PushError("Hud._Ready: " + ex); }
    }

    // O Game é autoload e sobrevive ao ReloadCurrentScene: sem isto o Hud antigo continuava a receber sinais.
    /// <summary>Botão voltar do Android (quit_on_go_back=false no project.godot) e Escape:
    /// fecha o painel aberto; sem painel, segundo toque em 2 s grava e sai.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationWMGoBackRequest) Back();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) { Back(); GetViewport().SetInputAsHandled(); }
    }

    private void Back()
    {
        if (_game is null) return;
        if (_end.Visible) { _end.Close(); return; }
        if (_menu.Visible) { _menu.Close(); return; }
        if (_multiSel.Active) { _game.RunWhenIdle(_multiSel.Clear); return; }
        if (_region.Visible) { _region.Close(); return; }
        if (_production.Visible) { _production.Close(); return; }
        if (_compare.Visible) { _compare.Close(); return; }
        if (_countryPanel.Visible) { _countryPanel.Close(); return; }
        if (_warPanel.Visible) { _warPanel.Close(); return; }
        if (_armyPanel.Visible) { _armyPanel.Close(); return; }
        var now = Time.GetTicksMsec();
        if (now - _backAt < 2000) { _game.Save(); GetTree().Quit(); return; }
        _backAt = now;
        Toast("Prime outra vez para gravar e sair");
    }

    public override void _ExitTree()
    {
        if (_game is null) return;
        _game.TickCompleted -= OnTick; _game.StateChanged -= RefreshAll; _game.CommandFailed -= Toast;
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
    }

    private void BuildTopBar()
    {
        var bar = new PanelContainer { Name = "Top" };
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        bar.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.043f, 0.051f, 0.059f, 0.96f), 6));
        AddChild(bar);
        // A barra leva uma tira fina por baixo, pintada com a cor do país do jogador: dá identidade
        // ao ecrã inteiro e fica vermelha quando o país está em guerra.
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 6); bar.AddChild(stack);

        // Primeira linha: o estado do jogo (data, velocidade, país, exército). Num telemóvel isto sozinho
        // já enche a largura — por isso a navegação desceu para a linha de baixo.
        var deck = new ScrollContainer
        {
            Name = "Deck",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        stack.AddChild(deck);
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10); deck.AddChild(row);
        _playerFlag = Flags.Rect(22); _playerFlag.Visible = false; row.AddChild(_playerFlag);
        _country = Ui.Lbl("", 20); _country.AddThemeColorOverride("font_color", Ui.Accent); row.AddChild(_country);
        _date = Ui.Lbl("2030-01-01", 22); row.AddChild(_date);
        _speed = new SpeedRibbon(); row.AddChild(_speed); _speed.Setup(_game);
        _season = SeasonView.Badge(_game.World); row.AddChild(_season);
        row.AddChild(Ui.Grow(new Control()));
        // Fila de mostradores à direita, à maneira dos jogos de grande estratégia: dinheiro, homens e
        // divisões, cada um com a sua nota (rendimento, reserva por chegar, fila de produção). Antes isto
        // eram duas frases de texto corrido — "Divisões 12 · Fila 3 · Homens 1.2M" — que ninguém lia de
        // relance nem encontrava outra vez quando queria.
        row.AddChild(Ui.Counter("₵", out _money, out _moneyNote, Ui.Accent));
        row.AddChild(Ui.Counter("♟", out _men, out _menNote, Ui.Text));
        row.AddChild(Ui.Counter("⚔", out _divs, out _divsNote, Ui.Danger.Lightened(0.25f)));
        // Indústria: quantas fábricas estão ao serviço e quantas há. Sem isto o jogador só descobria o
        // tecto da economia quando uma obra ou uma encomenda era recusada.
        row.AddChild(Ui.Counter("🏭", out _civ, out _civNote, Ui.Good.Lightened(0.2f)));
        row.AddChild(Ui.Counter("⚙", out _mil, out _milNote, Ui.Accent));
        _yardPlate = Ui.Counter("⚓", out _yard, out _yardNote, Ui.Text);
        row.AddChild(_yardPlate);
        // Experiência: a moeda das escolas de guerra. Fica ao lado das fábricas porque é a mesma pergunta —
        // o que é que hoje já dá para comprar. São três medalhas, uma por arma, como o HoI4 as tem lado a
        // lado na barra de cima: o exército aprende a combater, o ar a voar, o mar a navegar, e cada bolso
        // é seu. Carregar numa abre a árvore de escolas daquela arma — na barra nada é só enfeite.
        _xpPlate = Ui.Click(Ui.Counter("🎖", out _xp, out _xpNote, Ui.Good.Lightened(0.2f)),
                            () => Schools(World.Land), "escolas de guerra do exército");
        row.AddChild(_xpPlate);
        _airXpPlate = Ui.Click(Ui.Counter("✈", out _airXp, out _airXpNote, Ui.Text),
                               () => Schools(World.Air), "escolas de guerra do ar");
        row.AddChild(_airXpPlate);
        _seaXpPlate = Ui.Click(Ui.Counter("🚢", out _seaXp, out _seaXpNote, Ui.Text),
                               () => Schools(World.Sea), "escolas de guerra do mar");
        row.AddChild(_seaXpPlate);

        // Segunda linha: os painéis, dentro de um deslizador horizontal. Os botões nunca são cortados —
        // no ecrã largo cabem todos, no estreito arrasta-se a fila para o lado.
        var nav = new ScrollContainer
        {
            Name = "Nav",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        stack.AddChild(nav);
        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 8); nav.AddChild(tabs);
        tabs.AddChild(Ui.Btn("Frente", DefendBorders));
        tabs.AddChild(Ui.Btn("País", OpenCountry));
        // A produção só se alcançava por dentro do painel de uma região, no botão "Produzir": quem não
        // soubesse disso não tinha como chegar à fila — e a fila é onde se ganha a guerra antes de ela
        // começar. Passa a ter chapa própria na barra, como no HoI4.
        tabs.AddChild(Ui.Btn("Produção", OpenProduction));
        tabs.AddChild(Ui.Btn("Mundo", () => _worldPanel.Open()));
        tabs.AddChild(Ui.Btn("Guerra", OpenWar));
        // Distintivo das propostas: só aparece quando o inimigo tem alguma coisa em cima da mesa, e
        // pisca quando chega uma nova. Sem ele a proposta vivia só na notificação, que passa.
        _offers = Ui.Btn("✉", OpenWar, 0, Ui.Kind.Primary);
        _offers.Visible = false;
        tabs.AddChild(_offers);
        tabs.AddChild(Ui.Btn("Exércitos", () => _armyPanel.Open()));
        tabs.AddChild(Ui.Btn("Crónica", () => _journal.Open()));
        // altifalante: o som é do jogo, não do telefone — desliga-se aqui e a chapa diz em que estado está
        _mute = Ui.Btn("🔊", ToggleSound, 0, Ui.Kind.Normal);
        _mute.TooltipText = "som dos avisos";
        tabs.AddChild(_mute);
        _update = new UpdateChip(); tabs.AddChild(_update); _update.Setup(_game);
        tabs.AddChild(Ui.Btn("☰ Menu", () => _menu.Toggle()));

        stack.AddChild(Ui.Rule());                                    // risco de latão a fechar a chapa
        _accent = new ColorRect { CustomMinimumSize = new Vector2(0, 3), Color = Ui.SurfaceHi, MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddChild(_accent);
    }

    /// <summary>Abre a folha de comparação directa contra este país (o painel do País chama-a).</summary>
    public void OpenCompare(int otherCountryId)
    {
        if (_game.PlayerId is null) { Toast("Toca num país e escolhe-o primeiro"); return; }
        _countryPanel.Close();
        _compare.Open(otherCountryId);
    }

    /// <summary>Plano de batalha simplificado: manda as divisões paradas guardar a fronteira com o inimigo.</summary>
    private void DefendBorders()
    {
        if (_game.PlayerId is not int pid) { Toast("Toca num país e escolhe-o primeiro"); return; }
        var err = _game.Dispatch(new DefendBordersCommand(pid));
        Toast(err ?? "Divisões a caminho da frente");
    }

    private void OpenWar()
    {
        if (_game.PlayerId is not int) { Toast("Toca num país e escolhe-o primeiro"); return; }
        _region.Close(); _production.Close(); _countryPanel.Close(); _worldPanel.Close();
        _warPanel.Open();
    }

    private void OpenProduction()
    {
        if (_game.PlayerId is not int) { Toast("Toca num país e escolhe-o primeiro"); return; }
        _region.Close(); _warPanel.Close(); _countryPanel.Close(); _worldPanel.Close();
        _production.Open();
    }

    private void OpenCountry()
    {
        if (_game.PlayerId is not int pid) { Toast("Toca num país e escolhe-o primeiro"); return; }
        _region.Close(); _production.Close(); _countryPanel.Open(pid);
    }

    private void BuildToast()
    {
        // Toast: caixa centrada por baixo da barra; Ignore no wrapper para o toque passar ao mapa.
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        center.OffsetTop = 132; center.OffsetBottom = 192;   // a barra de topo tem duas linhas
        AddChild(center);
        _toastBox = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _toastSkin = Ui.Box(Ui.Ink with { A = 0.94f }, 12);
        _toastSkin.SetBorderWidthAll(2);
        _toastSkin.BorderColor = Ui.Frame;
        _toastBox.AddThemeStyleboxOverride("panel", _toastSkin);
        // cartão de aviso à maneira do HoI4: chapa do timbre à esquerda, texto ao meio, e por baixo a barra
        // que escoa os quatro segundos — quem olha de relance sabe o que é sem ler
        var card = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeConstantOverride("separation", 4);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 10);
        _toastPlate = new PanelContainer { CustomMinimumSize = new Vector2(44, 44) };
        _toastPlate.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface, 6));
        _toastIcon = Ui.Lbl("•", 22);
        _toastIcon.HorizontalAlignment = HorizontalAlignment.Center;
        _toastIcon.VerticalAlignment = VerticalAlignment.Center;
        _toastPlate.AddChild(_toastIcon);
        line.AddChild(_toastPlate);
        _toast = Ui.Lbl("", 20);
        _toast.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(Ui.Grow(_toast));
        card.AddChild(line);
        _toastClock = new ColorRect { CustomMinimumSize = new Vector2(0, 3), Color = Ui.Frame, MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddChild(_toastClock);
        _toastBox.AddChild(card); center.AddChild(_toastBox);
        _toastTimer = new Timer { WaitTime = 4, OneShot = true }; AddChild(_toastTimer);
        _toastTimer.Timeout += FadeOutToast;

        // Instrução enquanto não há jogador.
        var center2 = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center2.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        center2.OffsetTop = 202; center2.OffsetBottom = 262;
        AddChild(center2);
        _hint = Ui.Lbl("Toca num país e escolhe-o", 26); _hint.Visible = false; center2.AddChild(_hint);
    }

    /// <summary>Mensagem breve ao jogador (4 s). Seguro chamar de sinais; de outra thread usar CallDeferred.</summary>
    private void OpenSlots()
    {
        if (_slots is null)
        {
            _slots = new AcceptDialog { Title = "Jogos guardados" };
            _slots.GetOkButton().Text = "Fechar";
            AddChild(_slots);
        }
        foreach (var c in _slots.GetChildren()) if (c is VBoxContainer old) { _slots.RemoveChild(old); old.QueueFree(); }
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        for (int i = 1; i <= Game.SlotCount; i++)
        {
            int slot = i;
            var day = _game.SlotDay(slot);
            string txt = $"Slot {slot} — " + (slot == _game.Slot ? $"actual (dia {_game.World.Clock.Day})"
                        : day is int d ? $"dia {d}" : "vazio");
            var b = Ui.Btn(txt, () => { _slots.Hide(); _game.SwitchSlot(slot); });
            b.Disabled = slot == _game.Slot;
            v.AddChild(b);
        }
        _slots.AddChild(v);
        _slots.PopupCentered();
    }

    public void Toast(string msg) => Toast(msg, Sfx.Kind.Blip);

    /// <summary>Aviso no ecrã, com a voz que lhe pertence: a chapa, a cor da moldura e o som saem todos do
    /// mesmo Kind, para o olho e o ouvido dizerem a mesma coisa.</summary>
    public void Toast(string msg, Sfx.Kind kind)
    {
        try
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            _toastKind = kind;
            _toast.Text = msg;
            _toastIcon.Text = Sfx.Icon(kind);
            var tint = Sfx.Tint(kind);
            _toastIcon.AddThemeColorOverride("font_color", tint);
            _toastSkin.BorderColor = tint with { A = 0.85f };
            _toastClock.Color = tint;
            ShowToastBox();
            _toastTimer.Start();
            _sfx?.Play(kind);
            _journal?.Add(msg);
            if (_smoke) GD.Print("toast: " + msg);
        }
        catch (Exception ex) { GD.PushError("Toast: " + ex); }
    }

    /// <summary>Liga e desliga o som dos avisos. O botão fica com a chapa do estado em que está.</summary>
    private void ToggleSound()
    {
        _sfx.Muted = !_sfx.Muted;
        _mute.Text = _sfx.Muted ? "🔇" : "🔊";
        Toast(_sfx.Muted ? "Som desligado" : "Som ligado", _sfx.Muted ? Sfx.Kind.None : Sfx.Kind.Chime);
    }

    /// <summary>Entrada do toast: aparece a subir e a ganhar opacidade (0,18 s).</summary>
    private void ShowToastBox()
    {
        _toastTween?.Kill();
        _toastBox.Visible = true;
        _toastBox.Modulate = new Color(1, 1, 1, 0);
        _toastBox.Position = new Vector2(_toastBox.Position.X, 16);
        _toastTween = CreateTween().SetParallel();
        _toastTween.TweenProperty(_toastBox, "modulate:a", 1f, 0.18);
        _toastTween.TweenProperty(_toastBox, "position:y", 0f, 0.18).SetTrans(Tween.TransitionType.Cubic);
        // a barra de tempo escoa da esquerda para a direita ao ritmo do temporizador do aviso
        _toastClock.PivotOffset = Vector2.Zero;
        _toastClock.Scale = Vector2.One;
        _toastTween.TweenProperty(_toastClock, "scale:x", 0f, _toastTimer.WaitTime).SetTrans(Tween.TransitionType.Linear);
    }

    /// <summary>Saída do toast: desvanece antes de desaparecer, para não piscar.</summary>
    private void FadeOutToast()
    {
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenProperty(_toastBox, "modulate:a", 0f, 0.25);
        _toastTween.TweenCallback(Callable.From(() => _toastBox.Visible = false));
    }

    // Os eventos chegam na thread do tick; aí o World é coerente (é o tick que o está a mutar), mas a UI
    // só se toca na main thread → texto decidido já, entrega por CallDeferred.
    private void SubscribeEvents()
    {
        var w = _game.World;
        _subs.Add(w.Events.Subscribe<WarDeclared>(e =>
        {
            if (Player(e.Aggressor) || Player(e.Target)) Later($"{Country(e.Aggressor)} declarou guerra a {Country(e.Target)}", Sfx.Kind.Drum);
        }));
        _subs.Add(w.Events.Subscribe<RegionCaptured>(e =>
        {
            if (Player(e.NewController)) Later($"Capturaste {RegionName(e.RegionId)}", Sfx.Kind.Chime);
            else if (Player(e.OldController)) Later($"Perdeste {RegionName(e.RegionId)} para {Country(e.NewController)}", Sfx.Kind.Siren);
        }));
        _subs.Add(w.Events.Subscribe<BattleStarted>(e => { if (Mine(e.RegionId)) Later($"Batalha em {RegionName(e.RegionId)}", Sfx.Kind.Drum); }));
        _subs.Add(w.Events.Subscribe<BattleEnded>(e =>
        {
            if (Mine(e.RegionId)) Later($"Batalha em {RegionName(e.RegionId)}: {(e.AttackerWon ? "atacante venceu" : "defesa aguentou")}", Sfx.Kind.Drum);
        }));
        _subs.Add(w.Events.Subscribe<BattleLost>(e =>
        {
            if (!Player(e.CountryId)) return;
            string place = RegionName(e.RegionId);
            int streak = e.Streak; bool ground = e.GroundLost, alarm = e.Alarm;
            Callable.From(() => _klaxon.Raise(place, streak, ground, alarm)).CallDeferred();
        }));
        _subs.Add(w.Events.Subscribe<TechResearched>(e =>
        {
            if (Player(e.CountryId)) Later($"Investigação concluída: {(w.Techs.TryGetValue(e.TechId, out var t) ? t.Name : e.TechId)}", Sfx.Kind.Bell);
        }));
        _subs.Add(w.Events.Subscribe<NukeStruck>(e =>
            Later($"☢ {Country(e.AttackerId)} lançou uma ogiva sobre {RegionName(e.RegionId)} ({Country(e.TargetCountryId)})", Sfx.Kind.Siren)));
        _subs.Add(w.Events.Subscribe<DivisionDestroyed>(e =>
        {
            if (!w.Divisions.TryGetValue(e.DivisionId, out var d) || !Player(d.CountryId)) return;
            string name; try { name = w.Units.GetTemplate(d.TemplateId).Name; } catch { name = "divisão"; }
            Later($"{name} destruída em {RegionName(d.RegionId)}", Sfx.Kind.Siren);
        }));
        _subs.Add(w.Events.Subscribe<NewsFired>(e =>
        {
            if (w.NewsEvents.TryGetValue(e.EventId, out var n) && (n.CountryId is null || Player(n.CountryId.Value)))
                Later($"📰 {n.Title} — {n.Body}", Sfx.Kind.Bell);
        }));
        _subs.Add(w.Events.Subscribe<NewsChoiceRequired>(e =>
            Callable.From(() => ShowNewsChoice(e.EventId)).CallDeferred()));
        _subs.Add(w.Events.Subscribe<WarJustifyStarted>(e =>
        {
            if (Player(e.TargetCountryId)) Later($"{Country(e.CountryId)} está a justificar guerra contra ti!", Sfx.Kind.Siren);
            else if (Player(e.CountryId)) Later($"A justificar guerra contra {Country(e.TargetCountryId)}");
        }));
        _subs.Add(w.Events.Subscribe<FocusCompleted>(e =>
        {
            if (Player(e.CountryId)) Later($"Foco concluído: {(w.Focuses.TryGetValue(e.FocusId, out var f) ? f.Name : e.FocusId)}", Sfx.Kind.Chime);
        }));
        _subs.Add(w.Events.Subscribe<FactionJoinedWar>(e =>
        {
            if (Player(e.MemberCountryId) || Player(e.AgainstCountryId) || _game.PlayerId is int p2 && w.AreAtWar(p2, e.AgainstCountryId))
                Later($"{Country(e.MemberCountryId)} entrou na guerra contra {Country(e.AgainstCountryId)} (facção)", Sfx.Kind.Drum);
        }));
        _subs.Add(w.Events.Subscribe<InfrastructureBuilt>(e =>
        {
            if (_game.PlayerId is int p && _game.World.Regions.TryGetValue(e.RegionId, out var r) && r.OwnerId == p)
                Later($"Infraestrutura melhorada em {r.Name} (×{r.Infrastructure:0.00})", Sfx.Kind.Coin);
        }));
        _subs.Add(w.Events.Subscribe<DecisionExpired>(e =>
        {
            if (_game.PlayerId == e.CountryId && _game.World.DecisionDefs.TryGetValue(e.DecisionId, out var dd))
                Later($"Decisão terminou: {dd.Name}");
        }));
        _subs.Add(w.Events.Subscribe<RegionIntegrated>(e =>
        {
            if (_game.PlayerId is int p && _game.World.Regions.TryGetValue(e.RegionId, out var r)
                && (e.NewOwner == p || e.OldOwner == p))
                Later(e.NewOwner == p ? $"{r.Name} integrada no nosso país" : $"Perdemos {r.Name}: integrada por {Country(e.NewOwner)}");
        }));
        _subs.Add(w.Events.Subscribe<BuildingBuilt>(e =>
        {
            if (_game.PlayerId is int p && _game.World.Regions.TryGetValue(e.RegionId, out var r) && r.OwnerId == p
                && _game.World.BuildingDefs.TryGetValue(e.BuildingId, out var bd))
                Later($"{bd.Name} nível {e.Level} em {r.Name}", Sfx.Kind.Coin);
        }));
        _subs.Add(w.Events.Subscribe<PeaceOfferRejected>(e =>
        {
            if (Player(e.FromCountryId)) Later($"{Country(e.ToCountryId)} recusou a paz — ainda acha que ganha", Sfx.Kind.Siren);
        }));
        _subs.Add(w.Events.Subscribe<MoneyTransferred>(e =>
        {
            if (Player(e.ToCountryId)) Later($"{Country(e.FromCountryId)} enviou-te {e.Amount:0} pontos de produção");
            else if (Player(e.FromCountryId)) Later($"Apoio de {e.Amount:0} pts enviado a {Country(e.ToCountryId)}");
        }));
        _subs.Add(w.Events.Subscribe<PactSigned>(e =>
        {
            if (Player(e.A) || Player(e.B)) Later($"Pacto de não-agressão com {Country(Player(e.A) ? e.B : e.A)} até ao dia {e.UntilDay}", Sfx.Kind.Chime);
        }));
        _subs.Add(w.Events.Subscribe<PactRejected>(e =>
        {
            if (Player(e.FromCountryId)) Later($"{Country(e.ToCountryId)} recusou o pacto de não-agressão");
        }));
        _subs.Add(w.Events.Subscribe<BattleRetreat>(e =>
        {
            if (Player(e.CountryId) && _game.World.Regions.TryGetValue(e.RegionId, out var r))
                Later($"{e.Divisions} divisões retiraram de {r.Name}", Sfx.Kind.Siren);
        }));
        _subs.Add(w.Events.Subscribe<SpyOpStarted>(e =>
        {
            if (Player(e.CountryId) && _game.World.SpyOps.TryGetValue(e.OpId, out var op))
                Later($"Operação \"{op.Name}\" lançada contra {Country(e.TargetCountryId)}");
        }));
        _subs.Add(w.Events.Subscribe<SpyOpCompleted>(e =>
        {
            var op = _game.World.SpyOps.GetValueOrDefault(e.OpId);
            if (Player(e.CountryId)) Later($"Operação \"{op?.Name ?? e.OpId}\" concluída contra {Country(e.TargetCountryId)}");
            else if (Player(e.TargetCountryId)) Later($"Fomos alvo de espionagem: {op?.Name ?? e.OpId} ({Country(e.CountryId)})", Sfx.Kind.Siren);
        }));
        _subs.Add(w.Events.Subscribe<FortBuilt>(e =>
        {
            if (_game.PlayerId is int p && _game.World.Regions.TryGetValue(e.RegionId, out var r) && r.OwnerId == p)
                Later($"Fortificação nível {e.Level} em {r.Name}", Sfx.Kind.Coin);
        }));
        _subs.Add(w.Events.Subscribe<TradeDealCreated>(e =>
        {
            if (!Player(e.BuyerId) && !Player(e.SellerId)) return;
            var name = _game.World.ResourceDefs.TryGetValue(e.ResourceId, out var rd) ? rd.Name : e.ResourceId;
            Later(Player(e.BuyerId) ? $"Compramos {name} {e.Units:0} a {Country(e.SellerId)}"
                                    : $"{Country(e.BuyerId)} compra-nos {name} {e.Units:0}");
        }));
        _subs.Add(w.Events.Subscribe<TradeDealEnded>(e =>
        {
            if (!Player(e.BuyerId) && !Player(e.SellerId)) return;
            var name = _game.World.ResourceDefs.TryGetValue(e.ResourceId, out var rd) ? rd.Name : e.ResourceId;
            Later($"Acordo de {name} com {Country(Player(e.BuyerId) ? e.SellerId : e.BuyerId)} terminou");
        }));
        _subs.Add(w.Events.Subscribe<RegionRevolted>(e =>
        {
            if (_game.PlayerId is not int p || !w.Regions.TryGetValue(e.RegionId, out var r)) return;
            if (r.OwnerId == p) Later($"{r.Name} revoltou-se e voltou para nós");
            else if (e.OldController == p) Later($"Perdemos {r.Name} para uma revolta popular", Sfx.Kind.Siren);
        }));
        _subs.Add(w.Events.Subscribe<LawChanged>(e =>
        {
            if (Player(e.CountryId) && w.Laws.TryGetValue(e.LawId, out var l)) Later($"Nova lei: {l.Name}", Sfx.Kind.Chime);
        }));
        _subs.Add(w.Events.Subscribe<FactionCreated>(e =>
        {
            if (Player(e.CountryId)) Later($"Facção fundada: {(w.Factions.TryGetValue(e.FactionId, out var f) ? f.Name : e.FactionId)}");
        }));
        _subs.Add(w.Events.Subscribe<FactionJoined>(e =>
        {
            string fname = w.Factions.TryGetValue(e.FactionId, out var f) ? f.Name : e.FactionId;
            if (Player(e.CountryId)) Later($"Aderiste à facção {fname}");
            else if (_game.PlayerId is int p && f is not null && f.Members.Contains(p)) Later($"{Country(e.CountryId)} entrou na {fname}");
        }));
        _subs.Add(w.Events.Subscribe<FactionInviteRejected>(e =>
        {
            if (_game.PlayerId is int p && w.Factions.TryGetValue(e.FactionId, out var f) && f.Members.Contains(p))
                Later($"{Country(e.CountryId)} recusou o convite (sem inimigo comum)");
        }));
        _subs.Add(w.Events.Subscribe<FactionLeft>(e =>
        {
            string fname = w.Factions.TryGetValue(e.FactionId, out var f) ? f.Name : e.FactionId;
            if (Player(e.CountryId)) Later($"Saíste da facção {fname}");
            else if (_game.PlayerId is int p && f is not null && f.Members.Contains(p)) Later($"{Country(e.CountryId)} saiu da {fname}");
        }));
        _subs.Add(w.Events.Subscribe<LandingAborted>(e =>
        {
            if (_game.World.Divisions.TryGetValue(e.DivisionId, out var d) && Player(d.CountryId))
                Later($"Desembarque adiado em {RegionName(e.RegionId)} — tropa sem organização para assaltar a praia");
        }));
        _subs.Add(w.Events.Subscribe<WhitePeaceSigned>(e => _whitePeace.Add((e.A, e.B))));
        _subs.Add(w.Events.Subscribe<PeaceSigned>(e =>
        {
            _negotiated.Add((e.Winner, e.Loser));
            if (Player(e.Winner)) Later($"🕊 Paz com {Country(e.Loser)} — ficas com {e.Regions} regiões", Sfx.Kind.Fanfare);
            else if (Player(e.Loser)) Later($"🕊 Paz com {Country(e.Winner)} — cedes-lhe {e.Regions} regiões", Sfx.Kind.Siren);
        }));
        _subs.Add(w.Events.Subscribe<WarEnded>(e =>
        {
            bool white = _whitePeace.Remove((e.A, e.B));
            if (_negotiated.Remove((e.A, e.B)) | _negotiated.Remove((e.B, e.A))) return;   // já noticiada acima
            if (Player(e.A) || Player(e.B))
                Later(white ? $"🕊 Paz branca entre {Country(e.A)} e {Country(e.B)} — cada um fica com o que controla"
                            : $"Paz entre {Country(e.A)} e {Country(e.B)}");
        }));
        _subs.Add(w.Events.Subscribe<WarGoalDeclared>(e =>
        {
            if (Player(e.CountryId)) Later($"🎯 Objectivo de guerra contra {Country(e.TargetCountryId)}: {Regions(e.RegionIds)}");
            else if (Player(e.TargetCountryId)) Later($"🎯 {Country(e.CountryId)} quer tirar-nos {Regions(e.RegionIds)}");
        }));
        _subs.Add(w.Events.Subscribe<WarGoalAchieved>(e =>
        {
            if (Player(e.CountryId)) Later($"🎯 Objectivo cumprido contra {Country(e.TargetCountryId)} — dá para exigir a paz", Sfx.Kind.Chime);
            else if (Player(e.TargetCountryId)) Later($"⚠ {Country(e.CountryId)} já tem o que veio buscar");
        }));
        // Tabela mundial: só o lugar do jogador, e só quando ele muda mesmo (a contagem é de dias a dias).
        _subs.Add(w.Events.Subscribe<PowerRankChanged>(e =>
        {
            if (!Player(e.CountryId) || e.From == 0) return;
            string tier = e.Tier.Length > 0 ? $" ({e.Tier})" : "";
            Later(e.To < e.From ? $"🌍 Subimos ao {e.To}.º lugar mundial{tier}" : $"🌍 Descemos ao {e.To}.º lugar mundial{tier}");
        }));
        // Promoção de comandante: acontece poucas vezes por campanha, por isso vai toda para as notícias.
        _subs.Add(w.Events.Subscribe<GeneralPromoted>(e =>
        {
            if (!Player(e.CountryId) || !w.GeneralDefs.TryGetValue(e.GeneralId, out var gdef)) return;
            Later($"🎖 {gdef.Name} promovido a {e.RankName}", Sfx.Kind.Fanfare);
        }));
        // Condecoração: só as do jogador, e só as de peso (as primeiras chegam às centenas num exército grande).
        _subs.Add(w.Events.Subscribe<MedalAwarded>(e =>
        {
            if (!Player(e.CountryId) || !w.MedalDefs.TryGetValue(e.MedalId, out var m) || m.Sort < 3) return;
            string unit = w.Divisions.TryGetValue(e.DivisionId, out var d)
                ? d.Name ?? SafeTemplate(w, d) : "Divisão " + e.DivisionId;
            Later($"🎖 {unit}: {m.Name}", Sfx.Kind.Fanfare);
        }));
        // Nome de guerra: uma divisão só ganha honra umas poucas vezes por campanha — vai toda para as notícias.
        _subs.Add(w.Events.Subscribe<DivisionHonoured>(e =>
        {
            if (!Player(e.CountryId)) return;
            string unit = w.Divisions.TryGetValue(e.DivisionId, out var d)
                ? d.Name ?? SafeTemplate(w, d) : "Divisão " + e.DivisionId;
            Later($"▮ {unit} passa a chamar-se «{e.Title}»", Sfx.Kind.Fanfare);
        }));
        // Prisioneiros: só as levas grandes (uma divisão desfeita por dia numa guerra grande enche o ecrã).
        _subs.Add(w.Events.Subscribe<PrisonersTaken>(e =>
        {
            if (e.Men < (int)w.Rule("prisoner_news_men", 20000f)) return;
            string foe = w.Countries.TryGetValue(e.FromCountryId, out var fc) ? fc.Name : "o inimigo";
            if (Player(e.CaptorId)) Later($"⛓ {e.Men:N0} prisioneiros de {foe} nas nossas mãos");
            else if (Player(e.FromCountryId)) Later($"⛓ {e.Men:N0} dos nossos caem prisioneiros", Sfx.Kind.Siren);
        }));
        // Propostas do outro lado: a IA bate à porta e o jogador tem de responder alguma coisa.
        _subs.Add(w.Events.Subscribe<OfferMade>(e =>
        {
            if (!Player(e.ToId)) return;
            int from = e.FromId;
            string kind = e.Kind;
            Later(kind == "regiao" ? $"🏳 {Country(from)} oferece {RegionName(e.RegionId)} para acabar a guerra"
                : kind == "paz" ? $"🕊 {Country(from)} propõe paz branca"
                                : $"✉ {Country(from)} propõe trocar {e.Men:N0} prisioneiros de cada lado");
            Callable.From(() => PopOffer(from, kind)).CallDeferred();
        }));
        _subs.Add(w.Events.Subscribe<OfferExpired>(e =>
        {
            if (Player(e.ToId)) Later($"✉ A proposta de {Country(e.FromId)} caiu da mesa");
        }));
        _subs.Add(w.Events.Subscribe<PrisonersExchanged>(e =>
        {
            if (!Player(e.CountryId) && !Player(e.OtherId)) return;
            int otherId = Player(e.CountryId) ? e.OtherId : e.CountryId;
            string other = w.Countries.TryGetValue(otherId, out var oc) ? oc.Name : "o inimigo";
            Later($"🤝 Troca com {other}: {e.Home:N0} dos nossos voltam a casa");
        }));
        _subs.Add(w.Events.Subscribe<PrisonersReturned>(e =>
        {
            if (Player(e.HomeCountryId)) Later($"⛓ {e.Men:N0} prisioneiros nossos voltam a casa");
            else if (Player(e.HolderId)) Later($"⛓ Abrimos os campos: {e.Men:N0} prisioneiros repatriados");
        }));
        // Sabotagem na retaguarda: interessa quando é nossa ou quando é contra nós.
        _subs.Add(w.Events.Subscribe<RegionSabotaged>(e =>
        {
            if (Player(e.CountryId)) Later($"💥 Sabotagem nossa: {e.Damage}", Sfx.Kind.Coin);
            else if (Player(e.TargetCountryId)) Later($"💥 Sabotagem inimiga na retaguarda: {e.Damage}");
        }));
        _subs.Add(w.Events.Subscribe<SabotageFoiled>(e =>
        {
            string place = w.Regions.TryGetValue(e.RegionId, out var sr) ? sr.Name : "na retaguarda";
            if (Player(e.TargetCountryId)) Later($"🛡 Equipa inimiga apanhada em {place}");
            else if (Player(e.CountryId)) Later($"🛡 A nossa equipa foi apanhada em {place}");
        }));
        // Baixas no comando: perder um marechal é dos acontecimentos mais caros da campanha.
        _subs.Add(w.Events.Subscribe<GeneralKilled>(e =>
        {
            if (!Player(e.CountryId)) return;
            string who = w.GeneralDefs.TryGetValue(e.GeneralId, out var kd) ? kd.Name : e.GeneralId;
            string place = w.Regions.TryGetValue(e.RegionId, out var kr) ? $" em {kr.Name}" : "";
            Later($"⚰ {who} morre em combate{place}");
        }));
        _subs.Add(w.Events.Subscribe<GeneralWounded>(e =>
        {
            if (!Player(e.CountryId)) return;
            string who = w.GeneralDefs.TryGetValue(e.GeneralId, out var wd) ? wd.Name : e.GeneralId;
            string kind = w.WoundKinds.TryGetValue(e.KindId, out var k) ? $"{k.Icon} {k.Name}" : "🩸 Ferido";
            Later($"{kind}: {who} sai do campo por {e.Days} dias");
        }));
        _subs.Add(w.Events.Subscribe<GeneralRecovered>(e =>
        {
            if (!Player(e.CountryId)) return;
            string who = w.GeneralDefs.TryGetValue(e.GeneralId, out var rd) ? rd.Name : e.GeneralId;
            Later($"🩹 {who} volta ao serviço");
        }));
        _subs.Add(w.Events.Subscribe<CommandHandedOver>(e =>
        {
            if (!Player(e.CountryId) || !w.ArmyGroups.TryGetValue(e.GroupId, out var g)) return;
            string sub = e.NewGeneralId is string nid && w.GeneralDefs.TryGetValue(nid, out var sd) ? sd.Name : null!;
            Later(sub is null ? $"🎖 {g.Name} fica sem comandante" : $"🎖 {g.Name} passa às mãos de {sub}");
        }));
        // Estação nova: muda a marcha, a recomposição e o desgaste de toda a gente — é notícia de primeira.
        _subs.Add(w.Events.Subscribe<SeasonChanged>(e =>
        {
            if (w.SeasonDefs.TryGetValue(e.SeasonId, out var sd)) Later($"{sd.Icon} Entrou o {sd.Name}. {sd.Note}");
        }));
        // Saldo da guerra que acabou: sai como notícia e fica no painel Guerra para consulta.
        _subs.Add(w.Events.Subscribe<WarSummary>(e =>
        {
            if (_game.PlayerId is not int pid || !e.Record.Involves(pid)) return;
            var r = e.Record;
            int foe = r.A == pid ? r.B : r.A;
            string verdict = r.Winner is null ? "sem vencedor" : r.Winner == pid ? "vitória nossa" : "derrota";
            Later($"📜 Guerra com {Country(foe)} ({r.Days} dias): {verdict} — regiões {r.Regions(pid)}–{r.Regions(foe)}, divisões perdidas {r.Losses(pid)}");
        }));
        _subs.Add(w.Events.Subscribe<CountryCapitulated>(e =>
        {
            if (Player(e.CountryId)) Callable.From(() => _end.Show(CampaignReport.Defeat)).CallDeferred();
            else if (Player(e.WinnerId)) Later($"Vitória! {Country(e.CountryId)} capitulou — as regiões dele são tuas", Sfx.Kind.Fanfare);
            else Later($"{Country(e.CountryId)} capitulou! Regiões passam para {Country(e.WinnerId)}", Sfx.Kind.Siren);
        }));
        _subs.Add(w.Events.Subscribe<WorldDominated>(e => Callable.From(() =>
        {
            if (Player(e.CountryId)) _end.Show(CampaignReport.Domination);
            else { ShowDomination(e.CountryId); _end.Show(CampaignReport.Defeat); }
        }).CallDeferred()));
    }

    /// <summary>Evento noticioso do jogador com escolhas: diálogo modal, um botão por opção.
    /// Fechar sem escolher fica com a primeira (a IA faria o mesmo).</summary>
    private void ShowNewsChoice(string eventId)
    {
        var w = _game.World;
        if (!w.NewsEvents.TryGetValue(eventId, out var n) || !w.NewsOptions.TryGetValue(eventId, out var opts) || opts.Count == 0) return;

        var dlg = new AcceptDialog { Title = n.Title, DialogText = n.Body, OkButtonText = opts[0].Title };
        for (int i = 1; i < opts.Count; i++) dlg.AddButton(opts[i].Title, false, opts[i].Id);
        void Choose(string optionId) => _game.RunWhenIdle(() =>
        {
            if (_game.PlayerId is not int pid) return;
            var err = _game.Dispatch(new ChooseNewsOptionCommand(pid, eventId, optionId));
            if (err is not null) _game.Notify(err);
        });
        dlg.Confirmed += () => Choose(opts[0].Id);
        dlg.CustomAction += action => { Choose((string)action); dlg.Hide(); };
        dlg.Canceled += () => Choose(opts[0].Id);   // fechar = primeira opção, nunca fica por escolher
        AddChild(dlg);
        dlg.PopupCentered();
    }

    /// <summary>Fim de jogo por domínio mundial: vitória do jogador ou de uma IA.</summary>
    private void ShowDomination(int countryId)
    {
        bool me = _game.PlayerId == countryId;
        var dlg = new AcceptDialog
        {
            Title = me ? "Vitória mundial!" : "O mundo caiu",
            DialogText = me
                ? "Controlas a maior parte da população do planeta. O mundo é teu."
                : $"{Country(countryId)} controla a maior parte da população do planeta.",
            OkButtonText = "Novo jogo",
        };
        dlg.AddButton("Continuar", true, "watch");
        dlg.Confirmed += () => _game.NewGame();
        AddChild(dlg);
        dlg.PopupCentered();
    }



    private void Later(string msg) => Later(msg, Sfx.Kind.Blip);
    private void Later(string msg, Sfx.Kind kind) => Callable.From(() => Toast(msg, kind)).CallDeferred();
    private bool Player(int countryId) => _game.PlayerId == countryId;
    /// <summary>Põe a proposta do inimigo à frente do jogador: um Sim/Não com os números do dia. Recusar
    /// não é castigo nenhum — a proposta sai da mesa e eles voltam a insistir mais tarde. Quem quiser
    /// pensar melhor fecha o diálogo e vai buscá-la ao painel Guerra, onde o cartão fica.</summary>
    private void PopOffer(int fromId, string kind = "")
    {
        if (_game.PlayerId is not int pid) return;
        var offer = kind.Length > 0 ? OfferView.Pending(_game.World, pid, fromId, kind) : OfferView.First(_game.World, pid, fromId);
        if (offer is null) return;
        _offerFrom = fromId; _offerKind = offer.Kind; _offerRegion = offer.RegionId;
        if (_offerDialog is null)
        {
            _offerDialog = Ui.Dialog(this, () => AnswerOffer(true));
            _offerDialog.Canceled += () => AnswerOffer(false);
            _offerDialog.Title = "Proposta do inimigo";
        }
        _offerDialog.Title = offer.Kind == "regiao" ? "Cedência de território"
                           : offer.Kind == "paz" ? "Proposta de paz" : "Proposta do inimigo";
        _offerDialog.DialogText = OfferView.Line(_game.World, offer);
        _offerDialog.PopupCentered();
    }

    /// <summary>Resposta ao diálogo da proposta. Vai pelo mesmo comando do painel Guerra.</summary>
    private void AnswerOffer(bool accept) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var deal = PrisonerExchange.Evaluate(_game.World, _offerFrom, pid);
        var err = _game.Dispatch(new AnswerOfferCommand(pid, _offerFrom, accept, _offerKind));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify(!accept ? "Proposta recusada"
                    : _offerKind == "regiao" ? $"Paz assinada: {RegionName(_offerRegion)} passa a ser nossa"
                    : _offerKind == "paz" ? "Paz assinada: a guerra acabou onde estava"
                    : $"Troca aceite: {PrisonerView.Short(deal.Home)} dos nossos a caminho de casa");
        _warPanel.Refresh();
    });

    private string Country(int id) => _game.World.Countries.TryGetValue(id, out var c) ? c.Name : "?";
    private string RegionName(int id) => _game.World.Regions.TryGetValue(id, out var r) ? r.Name : "R" + id;
    // Região controlada pelo jogador ou com divisões dele.
    private bool Mine(int regionId)
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Regions.TryGetValue(regionId, out var r)) return false;
        return r.ControllerId == pid || r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid);
    }

    private void OnTick(int day)
    {
        RefreshAll();
        if (_smoke && !_smoked) { _smoked = true; Smoke(); }
    }

    private void RefreshAll()
    {
        try
        {
            RefreshTop();
            _map.Regions.Refresh();
            _map.Plans.Refresh();               // setas dos planos de batalha, por cima do mapa
            _map.Convoys.Refresh();             // rotas dos comboios mercantes, tracejadas no mar
            _map.Fronts.Refresh();              // e a linha da frente, com os dentes virados ao inimigo
            _region.Refresh();
            _map.Routes.Refresh();              // rotas da tropa escolhida (depois do painel: lê a selecção dele)
            _production.Refresh();
            _countryPanel.Refresh();
            _worldPanel.Refresh();
            _warPanel.Refresh();
            _armyPanel.Refresh();
            // O mini-mapa não serve de nada por baixo de um painel que ocupa metade do ecrã.
            _alerts.Refresh();
            _battle.Refresh();
            _focusTree.Refresh();
            _doctrines.Refresh();
            bool covered = _compare.Visible || _region.Visible || _production.Visible || _countryPanel.Visible
                           || _worldPanel.Visible || _warPanel.Visible || _journal.Visible || _armyPanel.Visible
                           || _battle.Visible || _focusTree.Visible || _doctrines.Visible;
            _mini.SetCovered(covered);
            _modeBar.SetCovered(covered);
            if (_modeBar.Visible) _modeBar.Refresh();
            if (!_mini.Visible) return;
            _mini.Refresh();
        }
        catch (Exception ex) { GD.PushError("Hud.RefreshAll: " + ex); }
    }

    /// <summary>Nome do modelo da divisão, sem deixar rebentar o toast se o template já não existir.</summary>
    private static string SafeTemplate(World w, Division d)
    {
        try { return w.Units.GetTemplate(d.TemplateId).Name; } catch { return "Divisão " + d.Id; }
    }

    /// <summary>Nomes das regiões de um objectivo, cortados para o toast não virar parágrafo.</summary>
    private string Regions(IReadOnlyList<int> ids)
    {
        var names = ids.Take(3).Select(RegionName).ToList();
        return string.Join(", ", names) + (ids.Count > names.Count ? $" (+{ids.Count - names.Count})" : "");
    }

    private static string FmtMen(float m) =>
        m < 0 ? "—" : m >= 1e6f ? $"{m / 1e6f:0.0}M" : m >= 1e3f ? $"{m / 1e3f:0}k" : $"{m:0}";

    private void RefreshTop()
    {
        var w = _game.World; var c = w.Clock;
        _date.Text = c.Date.ToString("yyyy-MM-dd");   // o andamento vive na fita, não em setas atrás da data
        _speed.Refresh();
        // a estação muda quatro vezes por ano: só se redesenha a chapa (e se relava o mapa) quando muda
        string season = w.Season?.Id ?? "";
        if (season != _seasonPainted)
        {
            _seasonPainted = season;
            SeasonView.Paint(_season, w);
            _map.Regions.Modulate = SeasonView.MapTint(w);
        }
        if (_game.PlayerId is int pid && w.Countries.TryGetValue(pid, out var p))
        {
            if (_playerFlag.Texture is null) { _playerFlag.Texture = Flags.Of(p.Tag); _playerFlag.Visible = _playerFlag.Texture is not null; }
            _country.Text = p.Tag;
            float income = EconomySystem.Income(w, pid);
            _money.Text = $"{p.Money:0.0}";
            _money.AddThemeColorOverride("font_color", income < 0f ? Ui.Danger : Ui.Text);
            _moneyNote.Text = $"{(income < 0 ? "" : "+")}{income:0.0}/dia";
            _men.Text = FmtMen(p.Manpower);
            _menNote.Text = "recrutas";
            _divs.Text = w.Divisions.Values.Count(d => d.CountryId == pid).ToString();
            _divsNote.Text = p.Queue.Count > 0 ? $"fila {p.Queue.Count}" : "divisões";
            var yards = Industry.Of(w, pid);
            _civ.Text = $"{yards.CivilBusy}/{yards.Civil}";
            _civ.AddThemeColorOverride("font_color", yards.FreeCivil > 0 ? Ui.TextDim : Ui.Text);
            _civNote.Text = yards.CivilBusy == 1 ? "obra" : "obras";
            _mil.Text = $"{yards.MilitaryBusy}/{yards.Military}";
            _mil.AddThemeColorOverride("font_color", yards.MilitaryBusy < yards.Military ? Ui.TextDim : Ui.Text);
            _milNote.Text = yards.MilitaryBusy == 1 ? "linha" : "linhas";
            _yardPlate.Visible = yards.Naval > 0;                 // país sem costa não tem cais nenhum a mostrar
            _yard.Text = $"{yards.NavalBusy}/{yards.Naval}";
            _yardNote.Text = yards.Naval == 1 ? "estaleiro" : "estaleiros";
            Medal(w, p, World.Land, _xpPlate, _xp, _xpNote);
            Medal(w, p, World.Air, _airXpPlate, _airXp, _airXpNote);
            Medal(w, p, World.Sea, _seaXpPlate, _seaXp, _seaXpNote);
            _hint.Visible = false;
            int posted = OfferView.Count(w, pid);
            if (posted != _offersShown)
            {
                bool arrived = posted > _offersShown && _offersShown >= 0;
                _offersShown = posted;
                _offers.Text = posted == 1 ? "✉ 1 proposta" : $"✉ {posted} propostas";
                _offers.Visible = posted > 0;
                if (arrived && posted > 0)
                {
                    var pulse = _offers.CreateTween().SetLoops(3);   // três piscadelas: dá nas vistas sem prender o olho
                    pulse.TweenProperty(_offers, "modulate:a", 0.35f, 0.25);
                    pulse.TweenProperty(_offers, "modulate:a", 1f, 0.25);
                }
            }
            bool atWar = p.AtWarWith.Count > 0;
            _accent.Color = atWar ? Ui.Danger : _map.Regions.CountryColor(pid);
        }
        else { _airXpPlate.Visible = false; _seaXpPlate.Visible = false; _country.Text = ""; _money.Text = "—"; _moneyNote.Text = ""; _men.Text = "—"; _menNote.Text = ""; _divs.Text = "—"; _divsNote.Text = ""; _civ.Text = "—"; _civNote.Text = ""; _mil.Text = "—"; _milNote.Text = ""; _yardPlate.Visible = false; _xpPlate.Visible = false; _hint.Visible = true; _playerFlag.Visible = false; _playerFlag.Texture = null; _accent.Color = Ui.SurfaceHi; }
    }

    /// <summary>Uma das três medalhas da barra: o que esta arma tem no bolso e o que isso já dá para
    /// comprar. Some-se quando o mundo não tem escolas dessa arma — um jogo sem doutrinas navais não põe
    /// um mostrador vazio a ocupar barra.</summary>
    private void Medal(World w, Country p, string domain, PanelContainer plate, Label value, Label note)
    {
        plate.Visible = w.DoctrineBranches.Values.Any(b => b.Domain == domain);
        value.Text = $"{World.Xp(p, domain):0}";
        string? next = ArmyXpSystem.Next(w, p, domain);
        value.AddThemeColorOverride("font_color", next is null ? Ui.Text : Ui.Good.Lightened(0.35f));
        note.Text = next is string id && w.ArmyDoctrines.TryGetValue(id, out var nd)
            ? "dá para " + nd.Name.ToLowerInvariant()
            : domain switch { World.Air => "experiência do ar", World.Sea => "experiência do mar", _ => "experiência" };
    }

    /// <summary>Carregar numa medalha abre a árvore de escolas daquela arma, já na aba certa.</summary>
    private void Schools(string domain)
    {
        if (_game.PlayerId is int pid) _doctrines.Open(pid, domain);
    }

    /// <summary>Leva o mapa a uma região e abre-lhe a ficha: o "Ver no mapa" da cedência aterra aqui, e
    /// como o painel Guerra se fecha antes, o jogador fica com a terra à frente dos olhos.</summary>
    private void ShowRegion(int regionId)
    {
        if (!_game.World.Regions.TryGetValue(regionId, out var r)) return;
        _map.Focus(new Vector2(r.CenterX, r.CenterY), 1.6f);
        OnRegionTapped(regionId);
    }

    private void OnRegionTapped(int regionId)
    {
        try
        {
            if (_region.MoveMode) { _region.MoveTo(regionId); return; }
            _production.Close(); _countryPanel.Close();
            _map.Regions.Highlight(regionId);
            _region.Open(regionId);
        }
        catch (Exception ex) { GD.PushError("Hud.OnRegionTapped: " + ex); }
    }

    /// <summary>--smoke: monta um exército com frente e plano a meio, só para as setas do mapa serem
    /// desenhadas mesmo quando a campanha ainda não tem plano nenhum em curso. Desfaz o que criou.</summary>
    private int SmokePlans()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid) return 0;
        if (w.Wars.Values.FirstOrDefault(x => x.Involves(pid)) is not WarInfo war) return 0;
        if (_game.Dispatch(new CreateArmyGroupCommand(pid, "Grupo do plano")) is not null) return 0;
        var g = w.ArmyGroups.Values.LastOrDefault(x => x.CountryId == pid);
        if (g is null) return 0;
        foreach (var d in w.Divisions.Values.Where(d => d.CountryId == pid).Take(3))
            _game.Dispatch(new AssignDivisionCommand(pid, d.Id, g.Id));
        _game.Dispatch(new SetArmyGroupFrontCommand(pid, g.Id, war.EnemyOf(pid)));
        _game.Dispatch(new SetArmyGroupStanceCommand(pid, g.Id, GroupStance.Defend));
        g.Planning = 0.6f;                     // o plano leva 20 dias a fazer-se: o smoke adianta-o
        int drawn = _map.Plans.Smoke();
        _game.Dispatch(new DisbandArmyGroupCommand(pid, g.Id));
        _map.Plans.Refresh();
        return drawn;
    }

    /// <summary>--smoke: manda uma divisão marchar e devolve o que a rota desenhada diz. A ordem dá-se à mão
    /// (e não pelo gesto) porque um destino sem caminho seria recusado em silêncio e a prova imprimia zero
    /// sem se perceber porquê.</summary>
    private string SmokeRoute(Region from)
    {
        if (_game.PlayerId is not int pid) return "sem jogador";
        var w = _game.World;
        var mine = w.Divisions.Values.FirstOrDefault(d => d.CountryId == pid && d.RegionId == from.Id);
        if (mine is null) return "sem divisões na capital";
        int to = from.Neighbours.FirstOrDefault(n => w.Regions.TryGetValue(n, out var r) && r.ControllerId == pid);
        if (to == 0) return "capital sem vizinha nossa";
        if (mine.Path.Count == 0 && _game.Dispatch(new MoveDivisionCommand(pid, mine.Id, to)) is string err) return "ordem recusada: " + err;
        if (mine.Path.Count == 0) return "sem caminho";
        _region.Open(from.Id);                                            // painel aberto: a rota lê a selecção dele
        return _map.Routes.Smoke();
    }

    // --smoke: abre os painéis e dá uma ordem de movimento para os caminhos de código correrem sem ecrã.
    private void Smoke()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c) || !w.Regions.TryGetValue(c.CapitalRegionId, out var cap)) return;
        OnRegionTapped(cap.Id); _region.SelectAll(); _region.BeginMove();
        GD.Print($"smoke: {cap.DivisionIds.Count} divisões na capital, {cap.Neighbours.Count} vizinhos, "
               + $"{_map.Regions.FrontierLines()} tiras de fronteira");
        if (cap.Neighbours.FirstOrDefault(n => w.Regions.TryGetValue(n, out var nr) && nr.ControllerId == pid) is int own && own != 0) _region.MoveTo(own);
        // uma leva de prisioneiros, para os campos e a balança do painel Guerra terem o que desenhar
        if (c.Prisoners.Count == 0 && w.Countries.Values.FirstOrDefault(x => x.Id != pid) is Country foe)
        {
            c.Prisoners[foe.Id] = (int)(w.Rule("prisoner_work_men", 400000f) * 0.5f);
            new PrisonerSystem().Tick(w);                              // põe-nos a trabalhar já neste dia
        }
        _production.Open();
        _warPanel.Open(); _warPanel.SmokeDeal(); _warPanel.Close();   // painel Guerra e mesa de negociação enchem sem rebentar
        _armyPanel.Open(); _armyPanel.Smoke(); _armyPanel.Close();     // painel Exércitos: grupo criado, frente atribuída e dissolvido
        _end.Show(CampaignReport.Ongoing); _end.Close();               // ecrã de fim de campanha, com o relatório todo
        int world = _worldPanel.Smoke();                               // painel Mundo: tabela de potências desenhada
        int worldTabs = _worldPanel.SmokeTabs();                       // e as quatro abas de metal, secção a secção
        // uma divisão nossa condecorada e com nome próprio, para a folha de serviço ter cartões a desenhar
        if (w.Divisions.Values.FirstOrDefault(d => d.CountryId == pid) is Division hero)
        {
            hero.Battles = Math.Max(hero.Battles, 12); hero.Captures = Math.Max(hero.Captures, 6); hero.Xp = MathF.Max(hero.Xp, 95f);
            new MedalSystem().Tick(w); new DivisionHonourSystem().Tick(w);
        }
        // estado-maior: contrata-se primeiro o comandante de casa (é o que leva selo e retrato próprios) e
        // depois um mercenário, para a folha ter os dois blocos; um deles fica ferido para a enfermaria desenhar
        int hurt = 0, hurtArms = 0;
        var staffPool = w.GeneralPool(c);
        foreach (var pick in new[] { staffPool.FirstOrDefault(g => g.CountryTag is not null),
                                     staffPool.FirstOrDefault(g => g.CountryTag is null),
                                     staffPool.FirstOrDefault(g => g.Domain == World.Air),
                                     staffPool.FirstOrDefault(g => g.Domain == World.Sea) })
        {
            if (pick is null || c.Generals.Contains(pick.Id)) continue;
            c.Money = MathF.Max(c.Money, pick.Cost);                 // ao dia 73 ninguém tem troco: o smoke adianta-o
            if (pick.Xp > 0f) { c.AirXp = MathF.Max(c.AirXp, pick.Xp); c.NavyXp = MathF.Max(c.NavyXp, pick.Xp); }
            _game.Dispatch(new HireGeneralCommand(pid, pick.Id));    // sem estado-maior não há enfermaria para desenhar
        }
        // carreira de cada arma: dá-se ao contratado a experiência do segundo degrau da escada DELE, para a
        // folha de postos aparecer com degraus feitos e degraus por fazer (e para se ver que a do ar e a do
        // mar não são a da infantaria)
        foreach (string id in c.Generals)
        {
            var rankLadder = w.Ranks(w.DomainOfGeneral(id), pid);
            if (rankLadder.Count > 1) c.GeneralXp[id] = MathF.Max(c.GeneralXp.GetValueOrDefault(id), rankLadder[1].Xp);
        }
        string staffRanks = string.Join("/", World.Domains.Select(d =>
            c.Generals.Where(id => w.DomainOfGeneral(id) == d).Select(id => CommanderView.RankName(w, pid, id))
                      .FirstOrDefault(n => n.Length > 0) ?? "—"));
        string staffArms = string.Join("/", World.Domains.Select(d => $"{w.GeneralsInService(c, d)}"));
        // quadro de postos: as três escadas do país desenhadas de uma vez, e quantas são de casa
        var rankBoard = CommanderView.Board(w, c);
        int rungs = World.Domains.Sum(d => w.Ranks(d, c).Count);
        int ownArms = World.Domains.Count(d => w.HasOwnRanks(c, d));
        rankBoard.Free();                                                // órfão: nunca entrou na árvore de cena
        string ourGeneral = c.Generals.Select(id => w.GeneralDefs[id]).FirstOrDefault(g => g.CountryTag is not null)
                            is GeneralDef mineGen
                            ? $"{mineGen.Icon} {mineGen.Name} ({Ui.StatName(mineGen.StatKey)} ×{mineGen.Mult:0.00})"
                            : "nenhum de casa";
        string wounds = "nenhuma";
        if (c.Generals.FirstOrDefault() is string gen && w.WoundKinds.Values.FirstOrDefault(k => !k.Fatal) is WoundKind wk)
        {
            c.GeneralWound[gen] = w.Clock.Day + wk.Days;
            c.GeneralWoundKind[gen] = wk.Id;
            World.ApplyGenerals(w, c);
            hurt = c.GeneralWound.Count;
            // enfermaria por arma: quantas armas têm gente no hospital e que gravidades cada arma sorteia
            var beds = CommanderView.Infirmary(w, pid);
            hurtArms = World.Domains.Count(d => c.GeneralWound.Keys.Any(id => w.DomainOfGeneral(id) == d));
            beds?.Free();                                                // órfã: nunca entrou na árvore de cena
            wounds = string.Join("/", World.Domains.Select(d => w.WoundKinds.Values.Count(k => World.WoundIsFor(k, d))));
        }
        // um cais no litoral, para o cartão dos portos e a linha da região terem números
        if (PortView.Ports(w, pid).Count == 0
            && w.BuildingDefs.Values.FirstOrDefault(b => b.SupplyRange > 0f) is BuildingDef quay
            && w.Regions.Values.FirstOrDefault(r => r.ControllerId == pid && r.Coastal) is Region coast)
        {
            coast.Buildings[quay.Id] = 1;
            new SupplySystem().Tick(w);                                // recalcula capacidade e carga do cais
        }
        int smokeFoe = 0;                                               // inimigo com quem o smoke negoceia
        int served = _countryPanel.Smoke(pid);                          // painel País: folha de serviço com os cartões
        int landTabs = _countryPanel.SmokeTabs();                       // e as quatro abas de metal, secção a secção
        int chartDay = _countryPanel.SmokeChart(); _countryPanel.Close();  // e o gráfico na nota de potência, com mira
        int cron = _journal.Smoke(); _journal.Close();                  // painel Crónica: linha do tempo e filtros
        // vitrine do medalheiro: uma divisão do país leva a folha de serviço a sério, para as chapas terem
        // quem as traga, e a vitrine monta-se à parte só para se contarem as fitas (no --smoke o QueueFree
        // não chega a correr: fecha-se com Free)
        if (w.Divisions.Values.FirstOrDefault(d => d.CountryId == pid) is Division decorate)
        {
            decorate.Battles = Math.Max(decorate.Battles, 12); decorate.Captures = Math.Max(decorate.Captures, 5);
            decorate.Xp = MathF.Max(decorate.Xp, 95f);
            new MedalSystem().Tick(w);
        }
        int ribbons = w.Medals(c).Count;
        string caseWho = w.HasOwnMedals(c) ? $"de {c.Tag}" : "comuns";
        int decorated = w.Divisions.Values.Count(d => d.CountryId == pid && d.Medals.Count > 0);
        var showcase = MedalView.Case(w, c);
        int plates = showcase.GetChild(0).GetChildren().OfType<PanelContainer>().Count();
        showcase.Free();
        // uma equipa de sabotagem a caminho da retaguarda inimiga, para o cartão ter barra e botões
        int sab = 0;
        // ao dia 79 o jogador pode ainda não ter guerra nenhuma: o smoke arranja-lhe uma com o vizinho do lado,
        // senão a secção de sabotagem nunca chegava a ser desenhada
        // e há-de ser com um vizinho de verdade: a cedência de território exige fronteira comum
        if (w.Regions.Values.FirstOrDefault(r => r.ControllerId != pid && r.OwnerId == r.ControllerId
                    && w.Countries.TryGetValue(r.ControllerId, out var rc) && !rc.IsPlayer && !rc.Capitulated
                    && r.Neighbours.Any(n => w.Regions.TryGetValue(n, out var nb) && nb.ControllerId == pid)) is Region foeLand
            && w.Countries.TryGetValue(foeLand.ControllerId, out var nbc))
        {
            // guerra registada e já parada: assim a mesa leva paz branca e cedência, não só a troca
            int since = w.Clock.Day - (int)w.Rule("peace_stale_days", 60f) - 1;
            w.StartWar(pid, nbc.Id, since);
            var info = w.Wars[World.WarKey(pid, nbc.Id)];
            info.StartDay = System.Math.Min(info.StartDay, since);
            info.LastProgressDay = since;                            // guerra já existente: envelhece-se a frente
            smokeFoe = nbc.Id;
        }
        if (w.Regions.Values.FirstOrDefault(r => r.ControllerId != pid && w.AreAtWar(pid, r.ControllerId)) is Region rear)
        {
            sab = SabotageView.Options(w, rear).Count;
            if (SabotageView.Options(w, rear).FirstOrDefault() is SpyOp raid)
            {
                c.Money = MathF.Max(c.Money, raid.Cost);                 // o smoke adianta o custo da operação
                _game.Dispatch(new StartSpyOpCommand(pid, rear.ControllerId, raid.Id, rear.Id));
            }
            OnRegionTapped(rear.Id);                                    // painel da região inimiga, com o cartão novo
            OnRegionTapped(cap.Id);                                     // e de volta à capital: cartão da nossa retaguarda
        }
        // campos cheios dos dois lados, para a mesa da troca ter números, veredicto e botão
        int swap = 0;
        if (w.Countries.Values.FirstOrDefault(x => x.Id != pid && w.AreAtWar(pid, x.Id)) is Country prey)
        {
            float full = w.Rule("prisoner_work_men", 400000f);
            if (c.Prisoners.GetValueOrDefault(prey.Id) == 0) c.Prisoners[prey.Id] = (int)(full * 0.5f);
            if (prey.Prisoners.GetValueOrDefault(pid) == 0) prey.Prisoners[pid] = (int)(full * 0.3f);
            swap = PrisonerExchange.Evaluate(w, pid, prey.Id).Men;
            _warPanel.Open(); _warPanel.SmokeDeal(); _warPanel.Close();  // agora com guerra a sério: balança e troca desenhadas
        }
        // uma proposta do inimigo em cima da mesa, para o cartão do painel Guerra e o diálogo serem desenhados
        int posted = 0; string ceded = "nenhuma";
        if ((w.Countries.GetValueOrDefault(smokeFoe)
             ?? w.Countries.Values.FirstOrDefault(x => x.Id != pid && !x.IsPlayer && w.AreAtWar(pid, x.Id))) is Country caller)
        {
            float period = w.Rule("offer_period_days", 10f);
            float ratio = w.Rule("cede_ratio", 0.6f);
            w.Rules["offer_period_days"] = 1f;                          // ao dia 86 a ronda das propostas não calhava
            w.Rules["cede_ratio"] = 999f;                               // e o vizinho ainda não está derrotado que chegue
            new OfferSystem().Tick(w);
            w.Rules["offer_period_days"] = period;
            w.Rules["cede_ratio"] = ratio;
            posted = w.Offers.Count(o => o.ToId == pid);
            ceded = w.Offers.FirstOrDefault(o => o.ToId == pid && o.Kind == "regiao") is PendingOffer land
                  ? RegionName(land.RegionId) : "nenhuma";
            _warPanel.Open(); _warPanel.Close();                        // cartão da proposta desenhado
            PopOffer(caller.Id);                                        // e o diálogo Sim/Não em cima dele
            _offerDialog?.Hide();
            AnswerOffer(false);                                         // recusa: os campos ficam como estavam
        }
        int pris = PrisonerView.Held(w, pid);                           // campos de prisioneiros do jogador
        // nevoeiro: quantas regiões alheias estão tapadas e o que a ficha diz sobre a primeira delas
        int fogged = 0; string fogWhy = "mapa aberto";
        foreach (var fr in w.Regions.Values.Where(x => x.ControllerId != pid).OrderBy(x => x.Id))
            if (!Vision.Sees(w, pid, fr)) { if (fogged++ == 0) { fogWhy = Vision.Why(w, pid, fr); _region.Open(fr.Id); _region.Close(); } }
        int alarms = _alerts.Smoke();                                   // faixa de alarmes desenhada
        int labs = ResearchSystem.Slots(w, c), busy = c.Research.Count;  // ranhuras de investigação
        // fichas de investigação (TechView): quantos cartões se desenham, em quantos ramos, e quantos são de casa
        var techTree = TechView.Tree(w, c, true, _ => { });
        int techBranches = techTree.GetChildren().OfType<PanelContainer>().Count();
        int techCards = techTree.GetChildren().OfType<GridContainer>().Sum(g => g.GetChildCount());
        int techHome = w.Techs.Values.Count(t => t.CountryTag == c.Tag && w.CanResearch(c, t.Id));
        techTree.Free();                                                 // órfão: nunca entrou na árvore de cena
        // folha de comparação: nós contra o vizinho, pelas três abas
        int rival = smokeFoe != 0 ? smokeFoe : w.Countries.Values.FirstOrDefault(x => x.Id != pid)?.Id ?? pid;
        int cmp = _compare.Smoke(rival); _compare.Close();
        var ind = Industry.Of(w, pid);                                   // fábricas: os mostradores e a bancada
        _production.Open();
        string queue = _production.Smoke();                              // fila de produção: chapas arrastáveis
        _production.Close();
        int modes = _modeBar.Smoke();                                    // modos de mapa: pinta o mundo por cada conta e volta ao político
        int fight = _battle.Smoke(cap.Id);                               // ecrã de batalha: os dois lados, linha e reserva
        int tree = _focusTree.Smoke();                                   // árvore de focos: grelha, traços e ramos rivais
        int plans = SmokePlans();                                        // planos de batalha: setas desenhadas no mapa
        // escolas de guerra: dá-se experiência à mão e aprende-se o primeiro degrau da escola de casa, para a
        // coluna de latão ficar com um degrau aceso e a árvore provar que fecha as escolas comuns
        c.ArmyXp = MathF.Max(c.ArmyXp, 260f);
        if (w.Branches(c).FirstOrDefault(b => b.CountryTag == c.Tag) is DoctrineBranch homeSchool
            && w.DoctrineSteps(c, homeSchool.Id).FirstOrDefault() is ArmyDoctrine homeStep)
            _game.Dispatch(new AdoptDoctrineCommand(pid, homeStep.Id));
        string schools = _doctrines.Smoke();                              // escolas de guerra: ramos e degraus da árvore
        // as três medalhas da barra de cima, uma por arma: o que está no bolso e o que isso já dá para comprar
        string medals = string.Join(" · ", new[] { (World.Land, "🎖"), (World.Air, "✈"), (World.Sea, "🚢") }
            .Select(medal => $"{medal.Item2} {World.Xp(c, medal.Item1):0}"
                           + (ArmyXpSystem.Next(w, c, medal.Item1) is string ready
                                ? $" dá para {w.ArmyDoctrines[ready].Name}" : " sem compra")));
        // adido militar: quantos anfitriões há, e a missão despachada quando houver guerra alheia para ver
        string attache = "sem guerra alheia";
        if (AttacheSystem.Pick(w, pid) is int hostId)
        {
            _game.Dispatch(new SendAttacheCommand(pid, hostId));
            attache = w.Attaches.TryGetValue(pid, out var mission)
                    ? $"junto de {w.Countries[mission.HostId].Name}" : "cofre curto";
        }
        else if (w.AtWar(pid)) attache = "a nossa guerra vê-se de dentro";
        // guerra aérea: compra-se uma asa se o cofre a der e destaca-se para o céu da frente, para a secção
        // do painel da Guerra desenhar missões a sério em vez de uma linha vazia
        string air = "sem céu ao alcance";
        if (AirMissionSystem.Front(w, pid) is int sky)
        {
            if (AirMissionSystem.Free(w, pid) < 1f) _game.Dispatch(new BuyAirWingCommand(pid));
            // --smoke: se o cofre não deu a compra, dá-se o esquadrão à mão — a prova é a missão voar, não o preço
            if (AirMissionSystem.Free(w, pid) < 1f) c.AirPower += 1f;
            float lot = MathF.Max(w.Rule("air_mission_min_wings", 1f), MathF.Floor(AirMissionSystem.Free(w, pid)));
            var order = new AssignAirMissionCommand(pid, sky, "superioridade", lot);
            air = order.Validate(w) is string why ? why
                : _game.Dispatch(order) ?? $"{lot:0.#} asa{(lot == 1f ? "" : "s")} sobre {w.Regions[sky].Name}";
        }
        // guerra naval: compra-se um navio, destaca-se para o mar da melhor costa deles e vê-se a secção
        string sea = "sem mar ao alcance";
        if (NavalMissionSystem.Target(w, pid) is int seaTarget)
        {
            if (NavalMissionSystem.Free(w, pid) < 1f) _game.Dispatch(new BuyWarshipCommand(pid));
            // --smoke: se o cofre não deu a compra, dá-se o navio à mão — a prova é a esquadra zarpar
            if (NavalMissionSystem.Free(w, pid) < 1f) c.Warships += 1f;
            float ships = MathF.Max(w.Rule("naval_mission_min_ships", 1f), MathF.Floor(NavalMissionSystem.Free(w, pid)));
            var sail = new AssignNavalMissionCommand(pid, seaTarget, "bloqueio", ships);
            sea = sail.Validate(w) is string no ? no
                : _game.Dispatch(sail) ?? $"{ships:0.#} navio{(ships == 1f ? "" : "s")} ao largo de {w.Regions[seaTarget].Name}";
        }
        // nomes próprios: a asa e a esquadra que acabaram de sair já não são "4 asas em Braga" — têm nome do
        // fundo do país (formation_name) e o selo ⚜ quando é um nome de casa
        string wingName = w.AirMissions.FirstOrDefault(m => m.CountryId == pid) is AirMission ourWing
            ? ourWing.Name + (FormationView.IsHome(w, pid, World.Air, ourWing.Name) ? " ⚜" : "") : "sem asa";
        string fleetName = w.NavalMissions.FirstOrDefault(m => m.CountryId == pid) is NavalMission ourFleet
            ? ourFleet.Name + (FormationView.IsHome(w, pid, World.Sea, ourFleet.Name) ? " ⚜" : "") : "sem esquadra";
        int homePool = w.Countries.TryGetValue(pid, out var flag)
            ? w.Formations(World.Air, flag).Count + w.Formations(World.Sea, flag).Count : 0;
        // comboios mercantes: manda-se um para o estaleiro e mede-se a marinha que carrega a frente e as compras
        string convoy = new BuyConvoyCommand(pid).Validate(w) is string noHold ? noHold
            : _game.Dispatch(new BuyConvoyCommand(pid)) ?? "mais um comboio no estaleiro";
        _warPanel.Open();
        int hosts = _warPanel.SmokeAttache();                             // cartão do adido e lista de anfitriões
        int metalTabs = _warPanel.SmokeTabs();                            // as abas de metal, secção a secção
        _warPanel.Close();
        string speed = _speed.Smoke();                                    // fita das velocidades: as cinco casas acesas à vez
        string update = _update.Smoke();                                  // chapa da actualização, sem tocar na rede
        string screen = $"{Settings.ScaleName} ×{Settings.Scale:0.00}";   // tamanho da interface
        var (names, glyphs) = _map.Regions.CountryNames();                // nomes escritos sobre a curva do território
        // contadores NATO: aproxima-se o mapa até ao limiar, desenham-se as caixas e volta-se à vista de longe
        var eye = new Vector2(cap.CenterX, cap.CenterY);
        _map.Focus(eye, 1.2f);
        _map.Regions.Refresh();
        int counters = _map.Regions.Counters();
        float dug = w.Divisions.Values.Where(d => d.CountryId == pid).Select(d => d.Entrench).DefaultIfEmpty(0f).Average();
        _map.Focus(eye, 0.2f);
        // mercado: assina-se um tratado com o primeiro país que tenha alguma coisa por vender, para o preço
        // travado, o depósito e o prazo passarem todos pelo ecrã do painel do País
        string trade = "ninguém vende";
        int term = (int)w.Rule("trade_deal_days", 180f);
        foreach (var seller in w.Countries.Values.Where(x => x.Id != pid && !w.AreAtWar(pid, x.Id)).OrderBy(x => x.Id))
        {
            var rd = w.ResourceDefs.Values.OrderBy(d => d.Id).FirstOrDefault(d =>
                ResourceSystem.Controlled(w, seller.Id, d.Id) - TradeSystem.Sold(w, seller.Id, d.Id) >= 1f
                && !w.TradeDeals.Any(t => t.BuyerId == pid && t.SellerId == seller.Id && t.ResourceId == d.Id));
            if (rd is null) continue;
            var buy = new CreateTradeDealCommand(pid, seller.Id, rd.Id, 1f, term);
            if (buy.Validate(w) is string why) { trade = $"{rd.Name} de {seller.Name} recusado: {why}"; continue; }
            _game.Dispatch(buy);
            var signed = w.TradeDeals.FirstOrDefault(t => t.BuyerId == pid && t.SellerId == seller.Id && t.ResourceId == rd.Id);
            trade = signed is null ? $"{rd.Name} de {seller.Name} por assinar"
                  : $"{rd.Name} de {seller.Name} a {signed.PricePerUnit:0.0}/un por {TradeSystem.DaysLeft(w, signed)} dias";
            break;
        }
        _countryPanel.Open(pid); _countryPanel.Close();                   // o mercado desenhado no painel do País
        // ocupação: toma-se uma terra do vizinho em guerra e assina-se-lhe a lei mais dura, para o cartão do
        // painel do País ter povo ocupado, barra de revolta e chapas por onde escolher
        string occ = "sem terra alheia debaixo de nós";
        if (w.Regions.Values.FirstOrDefault(r => r.ControllerId != pid && r.OwnerId == r.ControllerId
                && w.AreAtWar(pid, r.ControllerId)) is Region seized
            && w.OccupationPolicyDefs.Values.OrderByDescending(d => d.Yield).FirstOrDefault() is OccupationPolicyDef harsh)
        {
            seized.ControllerId = pid;                                    // --smoke: a conquista que a campanha ainda não fez
            int people = seized.OwnerId;
            occ = _game.Dispatch(new SetOccupationPolicyCommand(pid, people, harsh.Id))
                ?? $"{harsh.Name} sobre {w.Countries[people].Name} ({OccupationSystem.Regions(w, pid, people)} região, revolta {OccupationSystem.Heat(w, pid, people):P0})";
            _countryPanel.Open(people); _countryPanel.Close();            // cartão da ocupação desenhado
        }
        // conferência de paz: com terra ocupada na mão, contam-se os pontos de espólio com que chegaríamos à
        // mesa deste inimigo, quantos vencedores se sentam nela e quanto lhe custa a capital
        string spoils = "sem mesa de paz para nos sentarmos";
        if (w.Countries.Values.Where(x => x.Id != pid && !x.Capitulated && w.AreAtWar(pid, x.Id))
                .OrderByDescending(x => PeaceSpoils.Points(w, pid, x.Id)).FirstOrDefault() is Country loser)
        {
            var seats = PeaceSpoils.Table(w, loser);
            float crown = w.Regions.TryGetValue(loser.CapitalRegionId, out var throne)
                ? PeaceSpoils.Cost(w, throne, true) : 0f;
            spoils = $"{PeaceSpoils.Points(w, pid, loser.Id):0} pontos de espólio sobre {loser.Name}, "
                   + $"mesa de {seats.Count} vencedor{(seats.Count == 1 ? "" : "es")}, capital dele a {crown:0}";
        }
        // gabinete civil: nomeia-se o conselheiro mais barato de cada pasta que o cofre pague, para o cartão
        // do painel do País ter cadeiras ocupadas, folha de salários e chapas de candidatos
        string gov = "gabinete por formar";
        foreach (var slot in w.CabinetSlots)
        {
            // primeiro o conselheiro próprio do país, que é o que se quer ver com o selo no cartão
            var pool = CabinetSystem.Candidates(w, c, slot.Id).Where(a => a.Cost <= c.Money).ToList();
            if ((pool.FirstOrDefault(a => a.CountryTag is not null) ?? pool.FirstOrDefault()) is AdvisorDef pick)
                _game.Dispatch(new AppointAdvisorCommand(pid, pick.Id));
        }
        if (c.Cabinet.Count > 0)
        {
            // --smoke: um ano de casa na primeira pasta, para a barra de rodagem ter o que mostrar
            string first = c.Cabinet.Keys.OrderBy(k => k).First();
            c.CabinetSince[first] = w.Clock.Day - (int)w.Rule("advisor_tenure_days", 365f);
            World.ApplyCabinet(w, c);
            int nossos = c.Cabinet.Values.Count(id => w.AdvisorDefs[id].CountryTag is not null);
            gov = $"{c.Cabinet.Count} pasta{(c.Cabinet.Count == 1 ? "" : "s")} do gabinete ({string.Join(", ", c.Cabinet.Values.Select(id => w.AdvisorDefs[id].Name))}), "
                + $"{nossos} de casa, folha de {CabinetSystem.Wages(w, c):0.0}/dia, rodagem de {World.CabinetTenure(w, c, first):P0} na pasta mais antiga";
        }
        // leis nacionais: sobe-se um degrau na primeira escada que o cofre pague, para o cartão do painel do
        // País mostrar o degrau em vigor a mudar de sítio, e diz-se o que a lei de comércio deixa sair do país
        string laws = "sem leis na base de dados";
        var ladders = LawsView.Order(w, c);
        if (ladders.Count > 0)
        {
            foreach (var grp in ladders)
            {
                var cur = w.ActiveLaw(c, grp);
                var next = w.Laws.Values.FirstOrDefault(l => l.Group == grp && World.LawIsFor(l, c)
                                                          && l.Sort == (cur?.Sort ?? 0) + 1);
                if (next is not null && _game.Dispatch(new ChangeLawCommand(pid, next.Id)) is null) break;
            }
            // a escada que é só deste país tem de aparecer no meio das outras: é o que o cartão de latão mostra
            string ownLadder = ladders.FirstOrDefault(g => w.LawGroupDefs.TryGetValue(g, out var d) && d.CountryTag == c.Tag)
                               is string mineGrp
                               ? $"escada própria \"{LawsView.GroupName(w, mineGrp)}\" em {w.ActiveLaw(c, mineGrp)?.Name ?? "—"}"
                               : "sem escada própria";
            laws = $"{ladders.Count} escadas de leis ({string.Join(", ", ladders.Select(g => w.ActiveLaw(c, g)?.Name ?? "—"))}), "
                 + $"{ownLadder}, exporta até {c.Stat("export_share", 1f):P0} dos depósitos";
        }
        // alarme de derrota: perdem-se de propósito as batalhas seguidas que a regra exige, para a série
        // subir, o desgaste de guerra pagar a conta, a faixa acender o aviso e o klaxon tocar
        int need = (int)w.Rule("defeat_streak_alarm", 3f);
        int beater = w.Countries.Values.Where(x => x.Id != pid).OrderByDescending(x => w.AreAtWar(pid, x.Id)).First().Id;
        float worn = c.WarExhaustion;
        for (int i = 0; i < need; i++) w.Events.Publish(new BattleEnded(cap.Id, true, beater, pid));
        string klaxon = _klaxon.Smoke(cap.Name, c.DefeatStreak, true, true)
                      + $", desgaste de guerra {worn:0.0}→{c.WarExhaustion:0.0}, faixa: "
                      + (Alerts.For(w, pid).FirstOrDefault(a => a.Id == "defeat")?.Text ?? "sem aviso");
        _alerts.Smoke();                                                  // a faixa redesenhada já com a derrota
        var lanes = _map.Convoys.Smoke();                                 // rotas de comboio tracejadas no mapa
        var line = _map.Fronts.Smoke();                                   // e a linha da frente, com dentes e buracos
        var theatres = TheatreSystem.Of(w, pid);                          // os teatros que o painel da Guerra mostra
        string route = SmokeRoute(cap);                                   // e a rota da tropa escolhida, seta e tudo
        // sonoplastia: um aviso com voz de fanfarra (para o cartão apanhar chapa, moldura e barra de tempo),
        // depois a caixa toda de vozes e o botão do altifalante a calar e a voltar
        Toast("Prova de sonoplastia", Sfx.Kind.Fanfare);
        string plate = $"{_toastIcon.Text} {Sfx.Voice(_toastKind)}";
        string bank = _sfx.Smoke();
        ToggleSound(); bool hushed = _sfx.Muted; ToggleSound();
        string sound = $"{bank}, cartão com a chapa {plate}, "
                     + $"altifalante {(hushed && !_sfx.Muted ? "cala e volta" : "preso")}";
        GD.Print($"smoke: painéis abertos na capital {cap.Name}, {world} linhas no painel Mundo em {worldTabs} abas, {served} na folha de serviço, medalheiro {caseWho} com {ribbons} fitas em {plates} chapas ({decorated} divis{(decorated == 1 ? "ão" : "ões")} condecorada{(decorated == 1 ? "" : "s")}), estação {w.Season?.Name ?? "nenhuma"}, {cron} na crónica, {hurt} na enfermaria em {hurtArms} arma{(hurtArms == 1 ? "" : "s")} (gravidades por arma: {wounds}), estado-maior de {c.Generals.Count} em {staffArms} por arma (de casa: {ourGeneral}; postos {staffRanks}; quadro de {rungs} degraus, {ownArms} escada{(ownArms == 1 ? "" : "s")} de casa), {PrisonerView.Short(pris)} prisioneiros, cais para {c.PortCapacity:0} divisões, {sab} alvo{(sab == 1 ? "" : "s")} de sabotagem, retaguarda da capital {CounterIntelSystem.Chance(w, pid, cap):P0}/dia, troca de {PrisonerView.Short(swap)} prisioneiros, {posted} proposta{(posted == 1 ? "" : "s")} do inimigo, cedência de {ceded}, potência ao dia {chartDay}, {fogged} regiões no nevoeiro ({fogWhy}), {alarms} alarme{(alarms == 1 ? "" : "s")} na faixa, investigação em {busy}/{labs} ranhuras ({techCards} fichas em {techBranches} ramos, {techHome} de casa), folha de comparação com {cmp} linhas, fábricas {ind.CivilBusy}/{ind.Civil} civis e {ind.MilitaryBusy}/{ind.Military} militares, {modes} modos de mapa (agora {_map.Regions.Mode}), ecrã de batalha com {fight} linhas, {tree} focos na árvore, {plans} seta{(plans == 1 ? "" : "s")} de plano no mapa, rota da tropa escolhida: {route}, escolas de guerra: {schools}, medalhas na barra: {medals}, adido {attache} ({hosts} anfitri{(hosts == 1 ? "ão" : "ões")} possíve{(hosts == 1 ? "l" : "is")}), fita de velocidade em {speed} (andamentos {string.Join("/", Game.SpeedPace.Skip(1))}), interface {screen}, actualização: {update}, {counters} contadores no mapa (trincheira média {dug:0.0}), tratado de {trade}, {_frames} painéis com moldura de metal, guerra aérea: {air} ({w.AirMissions.Count} miss{(w.AirMissions.Count == 1 ? "ão" : "ões")} no mundo, ficha de {wingName}), guerra naval: {sea} ({w.NavalMissions.Count} esquadra{(w.NavalMissions.Count == 1 ? "" : "s")} no mundo, ficha de {fleetName}; fundo de {homePool} nomes), {names} nomes de país curvados no mapa ({glyphs} letras), comboios: {convoy} ({ConvoySystem.Available(w, pid):0} mercantes, {ConvoySystem.SupplyNeed(w, pid) + ConvoySystem.TradeNeed(w, pid):0} ocupados, {ConvoySystem.GroundedCount(w, pid)} parados), {metalTabs} abas de metal no painel da Guerra, ocupação: {occ}, {lanes.Lanes} rota{(lanes.Lanes == 1 ? "" : "s")} de comboio no mapa ({lanes.Cut} cortada{(lanes.Cut == 1 ? "" : "s")}), painel do País em {landTabs} abas, {spoils}, {gov}, {laws}, {queue}, klaxon: {klaxon}, som: {sound}, {theatres.Count} teatro{(theatres.Count == 1 ? "" : "s")} de operações ({line.Edges} contactos na linha da frente cosidos em {line.Strands} fio{(line.Strands == 1 ? "" : "s")}, {line.Holes} troço{(line.Holes == 1 ? "" : "s")} sem tropa, guarnição {(theatres.Count == 0 ? 0f : theatres.Average(t => t.Coverage)):P0})");
        // uma região minha com divisões, para o toque longo ter o que marcar
        var withDivs = w.Regions.Values.FirstOrDefault(r => r.ControllerId == pid
            && r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid));
        if (withDivs is not null)
        {
            _multiSel.LongPress(withDivs.Id);
            GD.Print($"smoke: toque longo em {withDivs.Name} → multi-selecção {(_multiSel.Active ? "activa" : "limpa")}");
            if (withDivs.Neighbours.FirstOrDefault() is int nb && nb != 0) _multiSel.DoubleTap(nb);
            GD.Print($"smoke: duplo toque → selecção {(_multiSel.Active ? "por usar" : "consumida")}");
        }
        _mini.Toggle(); _mini.Toggle(); _mini.Refresh();   // mini-mapa: encolher, abrir e pintar sem rebentar
        _menu.Open(); _menu.Close();
        GD.Print($"smoke: menu de jogo abre, dificuldade {(_game.World.Difficulty ?? "por escolher")}");
    }
}
