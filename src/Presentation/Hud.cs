using Godot;
using Timer = Godot.Timer;
using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Stats;
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
    // Combustível: o depósito, o saldo do dia e a autonomia (FuelSystem).
    private Label _fuel = null!, _fuelNote = null!;
    private Label _xp = null!, _xpNote = null!, _airXp = null!, _airXpNote = null!, _seaXp = null!, _seaXpNote = null!;
    private Label _pocket = null!, _pocketNote = null!;
    // Poder político e tensão mundial: a moeda da política e o termómetro do mundo (PoliticsSystem/WorldTension).
    private Label _pp = null!, _ppNote = null!, _tension = null!, _tensionNote = null!;
    private PanelContainer _ppPlate = null!, _tensionPlate = null!;
    private PanelContainer _fuelPlate = null!;
    private PanelContainer _yardPlate = null!, _xpPlate = null!, _airXpPlate = null!, _seaXpPlate = null!, _pocketPlate = null!;
    // Os mostradores que sempre estiveram à vista também se guardam agora: cada um leva um tooltip com as
    // parcelas da conta dele, e um tooltip escreve-se no mostrador, não no número lá dentro.
    private PanelContainer _moneyPlate = null!, _menPlate = null!, _divsPlate = null!, _civPlate = null!, _milPlate = null!;
    private PanelContainer _season = null!;
    private PanelContainer _top = null!;                    // a barra inteira: quem está por baixo mede-se por ela
    private HFlowContainer _plates = null!, _nav = null!;   // mostradores e chapas de painel: filas que dobram
    private CenterContainer _toastRow = null!, _hintRow = null!;
    private int _worstPocketRegion;                         // a bolsa que a chapa ⛓ mostra (0 = nenhuma)
    private string _seasonPainted = "";
    private TextureRect _playerFlag = null!;
    private SpeedRibbon _speed = null!;
    private PanelContainer _toastBox = null!;
    private ColorRect _accent = null!;
    private Timer _toastTimer = null!;
    private Tween? _toastTween;   // animação de entrada/saída do toast (morre e recomeça a cada mensagem)
    private SlotsPanel _slots = null!;             // fichas dos jogos guardados (era um AcceptDialog do sistema)
    private ConfirmationDialog? _offerDialog;      // proposta do inimigo (troca ou paz)
    private int _offerFrom;
    private string _offerKind = "prisioneiros";
    private int _offerRegion;                     // região da cedência a que o diálogo está a responder
    private Button _offers = null!;                // distintivo das propostas em cima da mesa
    private int _offersShown = -1;
    private RegionPanel _region = null!;
    private ArmySelect _multiSel = null!;
    private RegionFooter _footer = null!;
    private Hint _tips = null!;
    private BuildBar _buildBar = null!;
    private bool _suppressTapSelect;   // duplo toque já tratado: o toque simples a seguir não mexe na selecção
    private GameMenu _menu = null!;
    private ProductionPanel _production = null!;
    private CountryPanel _countryPanel = null!;
    private WorldPanel _worldPanel = null!;
    private CountrySelect _countrySelect = null!;
    private WarPanel _warPanel = null!;
    private ArmyPanel _armyPanel = null!;
    private EndScreen _end = null!;
    private EventCard _card = null!;                // cartão de acontecimento (ecrã inteiro, pára o relógio)
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
            // Sem jogador (ex.: troca para um slot vazio) esta cena não é sítio para se estar — volta-se
            // ao ecrã inicial, simétrico do que ele faz quando chega aqui já com jogador escolhido.
            if (_game.PlayerId is null) { GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn"); return; }
            _map = GetNode<MapView>("../MapView");
            if (_game.World.Countries.TryGetValue(_game.PlayerId.Value, out var home)
                && _game.World.Regions.TryGetValue(home.CapitalRegionId, out var cap0))
                _map.Focus(new Vector2(cap0.CenterX, cap0.CenterY), 1f);   // arranque: câmara já na capital, à HoI4
            _smoke = OS.GetCmdlineUserArgs().Contains("--smoke");
            Settings.Apply(GetWindow());         // tamanho da interface antes de se desenhar o que quer que seja
            GetTree().Root.Theme = Ui.Theme();   // tema da janela inteira: painéis, botões e diálogos de uma vez
            // E outra vez em cada filho, porque o tema de uma janela só desce por Control e por Window —
            // e este Hud é uma CanvasLayer. A corrente partia-se aqui: 85 dos 114 botões do jogo estavam a
            // desenhar-se com o tema de fábrica do Godot e não com o desta casa, e ninguém dava por isso
            // porque os dois são cinzentos escuros. Ligado antes de se criar seja o que for, apanha também
            // os painéis que nascem mais tarde.
            ChildEnteredTree += n => { if (n is Control c && c.Theme is null) c.Theme = Ui.Theme(); };
            BuildTopBar(); BuildToast();
            _production = new ProductionPanel(); AddChild(_production); _production.Setup(_game);
            _countryPanel = new CountryPanel(); AddChild(_countryPanel); _countryPanel.Setup(_game);
            _worldPanel = new WorldPanel(); AddChild(_worldPanel); _worldPanel.Setup(_game, _countryPanel);
            _countrySelect = new CountrySelect(); AddChild(_countrySelect); _countrySelect.Setup(_game, _countryPanel);
            _warPanel = new WarPanel(); AddChild(_warPanel); _warPanel.Setup(_game);
            _warPanel.OnShowRegion = ShowRegion;                     // "Ver no mapa" das cedências
            _journal = new JournalPanel(); AddChild(_journal); _journal.Setup(_game);
            _region = new RegionPanel(); AddChild(_region); _region.Setup(_game, _map, _production, _countryPanel);
            _multiSel = new ArmySelect(); AddChild(_multiSel); _multiSel.Setup(_game, _map);
            _footer = new RegionFooter(); AddChild(_footer); _footer.Setup(_game);
            _buildBar = new BuildBar(); AddChild(_buildBar); _buildBar.Setup(_game);
            _armyPanel = new ArmyPanel(); AddChild(_armyPanel); _armyPanel.Setup(_game, _map, _multiSel);
            _map.Routes.Watch(_multiSel);   // o mapa nasce antes dos painéis: a selecção liga-se aqui
            _end = new EndScreen(); AddChild(_end); _end.Setup(_game);
            _card = new EventCard(); AddChild(_card); _card.Setup(_game, k => _sfx.Play(k));
            _slots = new SlotsPanel(); AddChild(_slots); _slots.Setup(_game);
            _menu = new GameMenu(); AddChild(_menu); _menu.Setup(_game, OpenSlots, () => _end.Show(CampaignReport.Ongoing));
            _modeBar = new MapModeBar(); AddChild(_modeBar); _modeBar.Setup(_game, _map.Regions);
            _compare = new ComparePanel(); AddChild(_compare); _compare.Setup(_game);
            _battle = new BattlePanel(); AddChild(_battle); _battle.Setup(_game);
            _region.OnBattle = id => _battle.Open(id);
            _focusTree = new FocusPanel(); AddChild(_focusTree); _focusTree.Setup(_game);
            _countryPanel.OnFocusTree = id => _focusTree.Open(id);
            _doctrines = new DoctrinePanel(); AddChild(_doctrines); _doctrines.Setup(_game);
            _countryPanel.OnDoctrines = id => _doctrines.Open(id);
            _countryPanel.OnWarTab = tab => { ClosePanels(); _warPanel.Open(tab); };

            // Véu por trás de cada painel flutuante: escurece o mapa e apanha o toque que lhe passava ao
            // lado — sem isto um dedo que falhasse o botão dava um pan no mundo por trás do menu aberto.
            foreach (var (p, close) in new (PanelContainer Panel, Action Close)[]
            {
                (_production, _production.Close), (_countryPanel, _countryPanel.Close), (_worldPanel, _worldPanel.Close),
                (_warPanel, _warPanel.Close), (_journal, _journal.Close), (_region, _region.Close),
                (_armyPanel, _armyPanel.Close), (_compare, _compare.Close), (_battle, _battle.Close),
                (_focusTree, _focusTree.Close), (_doctrines, _doctrines.Close),
            }) Ui.Scrim(p, close);

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
                    { ClosePanels(); _battle.Open(mine.RegionId); }
            };

            // Caixilharia de metal: todos os painéis flutuantes ganham cantoneiras e rebites de uma vez. Fica
            // de fora a barra de topo (o texto encosta às arestas) e a tira de avisos, que é fina de propósito.
            _frames = PanelFrame.DressAll(this, _alerts, GetNode<PanelContainer>("Top"));
            LayoutUnderTop();   // agora que a faixa de alarmes e a chapa da selecção existem, descem com a barra

            // Depois da caixilharia: o cartaz do klaxon é de alarme e não leva cantoneiras de painel.
            _klaxon = new DefeatKlaxon(); AddChild(_klaxon); _klaxon.Setup();
            _sfx = new Sfx { Name = "Sfx" }; AddChild(_sfx);   // as vozes fazem-se no _Ready dele

            // A dica ao dedo parado nasce em último: tem de ficar por cima de tudo o que explica, e não
            // leva caixilharia — é um papelinho, não um painel.
            _tips = new Hint(); AddChild(_tips);

            _map.RegionTapped += OnRegionTapped;
            _map.RegionLongPressed += rid => _multiSel.LongPress(rid);
            _map.RegionDoubleTapped += OnRegionDoubleTapped;
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
        if (_card.Visible) { _card.Close(FirstOption(_card.OpenId)); return; }   // recuar = a escolha da IA
        if (_end.Visible) { _end.Close(); return; }
        if (_slots.Visible) { _slots.Close(); return; }
        if (_menu.Visible) { _menu.Close(); return; }
        if (_buildBar.Visible) { _buildBar.Close(); return; }
        if (_multiSel.Active) { _game.RunWhenIdle(_multiSel.Clear); return; }
        if (_region.Visible) { _region.Close(); return; }
        if (_production.Visible) { _production.Close(); return; }
        if (_compare.Visible) { _compare.Close(); return; }
        if (_countryPanel.Visible) { _countryPanel.Close(); return; }
        if (_warPanel.Visible) { _warPanel.Close(); return; }
        if (_armyPanel.Visible) { _armyPanel.Close(); return; }
        var now = Time.GetTicksMsec();
        if (now - _backAt < 2000) { _game.QuitSafely(); return; }
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

    /// <summary>As chapas da barra de cima, pela ordem em que lá estão. Nenhuma delas é linha de tabela: a
    /// barra não é uma lista de nada, é o mesmo estado de sempre com um símbolo por cima. Por isso vão daqui
    /// ao Glyph.Asked como extras — assim o contador do smoke conta-as com as outras e um nome mal escrito
    /// aparece na conta em vez de sair calado como roda dentada.</summary>
    public static readonly string[] BarGlyphs =
        { "cofre", "gente", "espadas", "fabrica", "bigorna", "ancora", "barril", "medalha", "asa", "barco", "corrente" };

    private void BuildTopBar()
    {
        var bar = new PanelContainer { Name = "Top" };
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        bar.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.043f, 0.051f, 0.059f, 0.96f), 6));
        AddChild(bar);
        _top = bar;
        bar.Resized += LayoutUnderTop;      // a barra cresce com o número de linhas: o resto segue-a
        // A barra leva uma tira fina por baixo, pintada com a cor do país do jogador: dá identidade
        // ao ecrã inteiro e fica vermelha quando o país está em guerra.
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 6); bar.AddChild(stack);

        // Primeira linha: o relógio do jogo (bandeira, país, data, andamento, estação). É curta de
        // propósito — cabe num telemóvel em pé sem empurrar nada para fora.
        var deck = new VBoxContainer { Name = "Deck", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        deck.AddThemeConstantOverride("separation", 6);
        stack.AddChild(deck);
        // Também dobra: num telemóvel em pé a fita das cinco velocidades e a data já não cabem lado a lado
        // com a bandeira e a estação, e o que sobrava saía pela direita do ecrã.
        var row = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("h_separation", 10);
        row.AddThemeConstantOverride("v_separation", 4);
        deck.AddChild(row);
        _playerFlag = Flags.Rect(22); _playerFlag.Visible = false; row.AddChild(_playerFlag);
        _country = Ui.Lbl("", 20); _country.AddThemeColorOverride("font_color", Ui.Accent); row.AddChild(_country);
        _date = Ui.Lbl("2030-01-01", 22); row.AddChild(_date);
        _speed = new SpeedRibbon(); row.AddChild(_speed); _speed.Setup(_game);
        _season = SeasonView.Badge(_game.World); row.AddChild(_season);
        // Fila de mostradores, à maneira dos jogos de grande estratégia: dinheiro, homens e divisões, cada
        // um com a sua nota (rendimento, reserva por chegar, fila de produção). Antes isto eram duas frases
        // de texto corrido — "Divisões 12 · Fila 3 · Homens 1.2M" — que ninguém lia de relance.
        //
        // Os mostradores viviam num deslizador horizontal, e num telemóvel isso queria dizer que metade do
        // estado do país estava fora do ecrã, escondido atrás de um arrasto que ninguém adivinha: via-se o
        // cofre e os homens, e as fábricas, os estaleiros e as medalhas só apareciam a quem soubesse
        // puxar. Agora é uma fila que dobra — HFlowContainer — e desce para a linha de baixo o que não
        // couber. A barra fica mais alta no telemóvel, e é isso mesmo que se quer: o estado todo à vista.
        var plates = _plates = new HFlowContainer { Name = "Plates", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        plates.AddThemeConstantOverride("h_separation", 10);
        plates.AddThemeConstantOverride("v_separation", 6);
        deck.AddChild(plates);
        // Os símbolos da barra eram os últimos emoji do jogo à vista permanente — um cifrão, um peão de
        // xadrez, uma fábrica redonda e colorida da fonte do telemóvel. Agora são chapas desenhadas, como
        // tudo o resto: o cofre, a gente, as espadas, o telhado da fábrica, a bigorna, a âncora, o barril,
        // as medalhas e a corrente. Estas duas não vêm de tabela nenhuma (o cofre e o barril não são linha
        // de nada) e por isso passam pelo Glyph.Asked como extras, para o contador do smoke as ver.
        plates.AddChild(_moneyPlate = Ui.Counter(Glyph.Make("cofre", 19, Ui.Accent), out _money, out _moneyNote));
        plates.AddChild(_menPlate = Ui.Counter(Glyph.Make("gente", 19, Ui.Text), out _men, out _menNote));
        // Poder político ao lado do cofre: são as duas moedas do país e lêem-se juntas. Uma paga aço, a
        // outra paga leis, gabinete, pactos e pretextos — e nunca se trocam uma pela outra, que é
        // precisamente o que faz da política uma escolha e não uma compra.
        _ppPlate = Ui.Click(Ui.Counter(Glyph.Make("coluna", 19, Ui.Accent), out _pp, out _ppNote),
                            OpenCountry, "poder político: leis, gabinete e decisões");
        plates.AddChild(_ppPlate);
        // Tensão mundial: o termómetro do mundo. Não é nosso — é de toda a gente — mas é ele que abre e
        // fecha as portas do que se pode fazer hoje, e por isso vive na barra como o resto.
        _tensionPlate = Ui.Click(Ui.Counter(Glyph.Make("globo", 19, Ui.Text), out _tension, out _tensionNote),
                                 OpenWorld, "tensão mundial");
        plates.AddChild(_tensionPlate);
        plates.AddChild(_divsPlate = Ui.Counter(Glyph.Make("espadas", 19, Ui.Danger.Lightened(0.25f)), out _divs, out _divsNote));
        // Indústria: quantas fábricas estão ao serviço e quantas há. Sem isto o jogador só descobria o
        // tecto da economia quando uma obra ou uma encomenda era recusada.
        plates.AddChild(_civPlate = Ui.Counter(Glyph.Make("fabrica", 19, Ui.Good.Lightened(0.2f)), out _civ, out _civNote));
        plates.AddChild(_milPlate = Ui.Counter(Glyph.Make("bigorna", 19, Ui.Accent), out _mil, out _milNote));
        _yardPlate = Ui.Counter(Glyph.Make("ancora", 19, Ui.Text), out _yard, out _yardNote);
        plates.AddChild(_yardPlate);
        // Combustível: o barril fica ao lado das fábricas porque é a mesma pergunta que elas — o que é que
        // hoje dá para pôr a andar. Some-se em quem não tem máquinas nenhumas a beber.
        _fuelPlate = Ui.Counter(Glyph.Make("barril", 19, Ui.Accent), out _fuel, out _fuelNote);
        plates.AddChild(_fuelPlate);
        // Experiência: a moeda das escolas de guerra. Fica ao lado das fábricas porque é a mesma pergunta —
        // o que é que hoje já dá para comprar. São três medalhas, uma por arma, como o HoI4 as tem lado a
        // lado na barra de cima: o exército aprende a combater, o ar a voar, o mar a navegar, e cada bolso
        // é seu. Carregar numa abre a árvore de escolas daquela arma — na barra nada é só enfeite.
        _xpPlate = Ui.Click(Ui.Counter(Glyph.Make("medalha", 19, Ui.Good.Lightened(0.2f)), out _xp, out _xpNote),
                            () => Schools(World.Land), "escolas de guerra do exército");
        plates.AddChild(_xpPlate);
        _airXpPlate = Ui.Click(Ui.Counter(Glyph.Make("asa", 19, Ui.Text), out _airXp, out _airXpNote),
                               () => Schools(World.Air), "escolas de guerra do ar");
        plates.AddChild(_airXpPlate);
        _seaXpPlate = Ui.Click(Ui.Counter(Glyph.Make("barco", 19, Ui.Text), out _seaXp, out _seaXpNote),
                               () => Schools(World.Sea), "escolas de guerra do mar");
        plates.AddChild(_seaXpPlate);
        // Cerco: só aparece quando há tropa nossa cortada, e é a chapa mais cara de ignorar da barra —
        // dias de bolsa e o prazo até as armas baixarem. Carregar leva o mapa à pior das bolsas.
        _pocketPlate = Ui.Click(Ui.Counter(Glyph.Make("corrente", 19, Ui.Danger.Lightened(0.15f)), out _pocket, out _pocketNote),
                                ShowWorstPocket, "divisões cercadas");
        _pocketPlate.Visible = false;
        plates.AddChild(_pocketPlate);

        // Segunda linha: os painéis. Também estavam num deslizador horizontal, e um botão que só aparece
        // depois de se arrastar a fila é um botão que não existe — foi assim que a fila de produção passou
        // meses escondida. Agora dobram de linha como os mostradores: vê-se o jogo todo de uma vez.
        var tabs = _nav = new HFlowContainer { Name = "Nav", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tabs.AddThemeConstantOverride("h_separation", 8);
        tabs.AddThemeConstantOverride("v_separation", 6);
        stack.AddChild(tabs);
        tabs.AddChild(Ui.Btn("Frente", DefendBorders));
        tabs.AddChild(Ui.Btn("País", OpenCountry));
        // A produção só se alcançava por dentro do painel de uma região, no botão "Produzir": quem não
        // soubesse disso não tinha como chegar à fila — e a fila é onde se ganha a guerra antes de ela
        // começar. Passa a ter chapa própria na barra, como no HoI4.
        tabs.AddChild(Ui.Btn("Produção", OpenProduction));
        // Construir: escolhe-se o tipo aqui e depois toca-se no mapa, região a região — o menu não tapa o
        // jogo (ao contrário dos outros separadores) e fica aberto para se construir em várias regiões seguidas.
        tabs.AddChild(Ui.Btn("Construir", OpenBuild));
        tabs.AddChild(Ui.Btn("Mundo", OpenWorld));
        // Países: a lista de todos, com procura. Sem ela só se chegava a um país tocando-lhe no mapa
        // (e o toque no mapa é para escolher tropas) ou às quinze linhas da tabela de potências.
        tabs.AddChild(Ui.Btn("Países", OpenCountries));
        tabs.AddChild(Ui.Btn("Guerra", OpenWar));
        // Distintivo das propostas: só aparece quando o inimigo tem alguma coisa em cima da mesa, e
        // pisca quando chega uma nova. Sem ele a proposta vivia só na notificação, que passa.
        _offers = Ui.Btn("✉", OpenWar, 0, Ui.Kind.Primary);
        _offers.Visible = false;
        tabs.AddChild(_offers);
        tabs.AddChild(Ui.Btn("Exércitos", OpenArmies));
        tabs.AddChild(Ui.Btn("Crónica", OpenJournal));
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

    /// <summary>Fecha todos os painéis flutuantes — os principais e os de detalhe. Chamar sempre antes de
    /// abrir outro: dois abertos ao mesmo tempo tapavam-se um ao outro (e o de baixo continuava a roubar o
    /// toque de quem já achava estar a falar com o de cima). Cada aba da barra passa por aqui primeiro.</summary>
    private void ClosePanels()
    {
        _region.Close(); _production.Close(); _countryPanel.Close(); _worldPanel.Close();
        _warPanel.Close(); _armyPanel.Close(); _journal.Close();
        _compare.Close(); _battle.Close(); _focusTree.Close(); _doctrines.Close();
        _countrySelect.Close();
    }

    private void OpenWar()
    {
        if (_game.PlayerId is not int) { Toast("Toca num país e escolhe-o primeiro"); return; }
        ClosePanels();
        _warPanel.Open();
    }

    private void OpenProduction()
    {
        if (_game.PlayerId is not int) { Toast("Toca num país e escolhe-o primeiro"); return; }
        ClosePanels();
        _production.Open();
    }

    private void OpenCountry()
    {
        if (_game.PlayerId is not int pid) { Toast("Toca num país e escolhe-o primeiro"); return; }
        ClosePanels();
        _countryPanel.Open(pid);
    }

    private void OpenWorld() { ClosePanels(); _worldPanel.Open(); }
    private void OpenCountries() { ClosePanels(); _countrySelect.Open(); }
    private void OpenArmies() { ClosePanels(); _armyPanel.Open(); }
    private void OpenJournal() { ClosePanels(); _journal.Open(); }

    /// <summary>Não fecha os outros painéis nem o mapa: é uma chapa pequena no canto, não um véu a tapar o jogo.</summary>
    private void OpenBuild()
    {
        if (_game.PlayerId is not int) { Toast("Toca num país e escolhe-o primeiro"); return; }
        if (_buildBar.Visible) _buildBar.Close(); else _buildBar.Open();
    }

    private void BuildToast()
    {
        // Toast: caixa centrada por baixo da barra; Ignore no wrapper para o toque passar ao mapa.
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        AddChild(center);
        _toastRow = center;
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
        AddChild(center2);
        _hintRow = center2;
        _hint = Ui.Lbl("Toca num país e escolhe-o", 26); _hint.Visible = false; center2.AddChild(_hint);
        LayoutUnderTop();
    }

    /// <summary>Põe tudo o que vive logo abaixo da barra de topo à altura a que ela ficou. A barra deixou
    /// de ter um número fixo de linhas — os mostradores dobram para a linha de baixo quando o ecrã é
    /// estreito — e as posições cravadas a 132 px passaram a esconder-se por trás dela num telemóvel.
    /// Sem uma passagem de layout ainda não há Size: nesse caso vale o tamanho mínimo da barra, que é o
    /// mesmo número sem esperar por ecrã nenhum (é assim que o --smoke a mede).</summary>
    private void LayoutUnderTop()
    {
        if (_top is null) return;
        float y = MathF.Max(_top.Size.Y, _top.GetCombinedMinimumSize().Y) + 8f;
        if (_toastRow is not null) { _toastRow.OffsetTop = y; _toastRow.OffsetBottom = y + 60f; }
        if (_hintRow is not null) { _hintRow.OffsetTop = y + 70f; _hintRow.OffsetBottom = y + 130f; }
        // a faixa de alarmes começa por baixo das duas faixas que passam (aviso e dica) e não em cima delas:
        // ao centro e à mesma altura do aviso, era a notificação que ficava ilegível. Daqui para baixo é o
        // jogador que manda — arrastou-a, a AlertStrip para de ouvir esta linha.
        _alerts?.PlaceUnder(y + 140f);
        _multiSel?.PlaceUnder(y - 4f);
        _buildBar?.PlaceUnder(y + 4f);
    }

    private void OpenSlots() => _game.RunWhenIdle(_slots.Open);

    /// <summary>Mensagem breve ao jogador (4 s). Seguro chamar de sinais; de outra thread usar CallDeferred.</summary>
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
        // Um acontecimento que pede pausa abre o cartão de ecrã inteiro; os outros passam no aviso do canto.
        // Quem tem escolhas abre sempre (chega pelo NewsChoiceRequired), pause ou não.
        _subs.Add(w.Events.Subscribe<NewsFired>(e =>
        {
            if (!w.NewsEvents.TryGetValue(e.EventId, out var n) || !Mine(w, n)) return;
            if (!n.Pause) { Later($"📰 {n.Title} — {n.Body}", Sfx.Kind.Bell); return; }
            bool card = !w.NewsOptions.ContainsKey(n.Id);          // com escolhas o cartão vem pelo NewsChoiceRequired
            Callable.From(() =>
            {
                _journal?.Add($"📰 {n.Title}");                    // fica na crónica mesmo quando não passa no aviso
                if (card) _card.Show(e.EventId);
            }).CallDeferred();
        }));
        _subs.Add(w.Events.Subscribe<NewsChoiceRequired>(e =>
            Callable.From(() => _card.Show(e.EventId)).CallDeferred()));
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
        _subs.Add(w.Events.Subscribe<ParadropLanded>(e =>
        {
            if (!Player(e.CountryId)) return;
            Later(e.Captured ? $"🪂 Salto sobre {RegionName(e.RegionId)} — terreno tomado" : $"🪂 Salto sobre {RegionName(e.RegionId)}",
                e.Captured ? Sfx.Kind.Chime : Sfx.Kind.Drum);
        }));
        _subs.Add(w.Events.Subscribe<ParadropAborted>(e =>
        {
            if (Player(e.CountryId)) Later($"🪂 Salto sobre {RegionName(e.RegionId)} desfeito — {e.Why}", Sfx.Kind.Siren);
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
            if (w.SeasonDefs.TryGetValue(e.SeasonId, out var sd)) Later($"Entrou o {sd.Name}. {sd.Note}");
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
        // Governos no exílio: o jogador só ouve o que lhe diz respeito — a casa que dá, a bandeira que
        // volta e, sobretudo, as regiões que a sua tropa libertou e vai devolver ao dono.
        _subs.Add(w.Events.Subscribe<GovernmentExiled>(e =>
        {
            if (Player(e.HostId)) Later($"O governo de {Country(e.CountryId)} pede asilo: passa a governar daqui");
            else if (Ally(e.CountryId)) Later($"O governo de {Country(e.CountryId)} parte para o exílio em {Country(e.HostId)}");
        }));
        _subs.Add(w.Events.Subscribe<ExileEnded>(e =>
        {
            if (Ally(e.CountryId)) Later($"O governo de {Country(e.CountryId)} no exílio dissolve-se: ninguém o reconhece já");
        }));
        _subs.Add(w.Events.Subscribe<GovernmentReturned>(e =>
        {
            if (Player(e.LiberatorId))
                Later($"{Country(e.CountryId)} volta do exílio: {e.Regions} regiões libertadas passam para o dono", Sfx.Kind.Fanfare);
            else if (Ally(e.CountryId) || Ally(e.LiberatorId))
                Later($"{Country(e.CountryId)} volta do exílio pela mão de {Country(e.LiberatorId)}");
        }));
        _subs.Add(w.Events.Subscribe<WorldDominated>(e => Callable.From(() =>
        {
            if (Player(e.CountryId)) _end.Show(CampaignReport.Domination);
            else { ShowDomination(e.CountryId); _end.Show(CampaignReport.Defeat); }
        }).CallDeferred()));
    }

    /// <summary>A primeira opção de um evento (a que a IA escolheria), ou null se não tem escolhas.</summary>
    private string? FirstOption(string eventId) =>
        _game.World.NewsOptions.TryGetValue(eventId, out var opts) && opts.Count > 0 ? opts[0].Id : null;

    /// <summary>A notícia é nossa: caiu no nosso país ou no mundo inteiro. Nos eventos de estado quem diz
    /// isso é o registo do dia em que caíram (World.NewsFired) — antes de acontecerem não têm dono.</summary>
    private bool Mine(World w, NewsEvent n)
    {
        int? on = w.NewsFired.TryGetValue(n.Id, out var hit) ? (hit.CountryId == 0 ? null : hit.CountryId) : n.CountryId;
        return on is null || Player(on.Value);
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
    /// <summary>Aliado de facção do jogador: o que se passa com ele é notícia nossa.</summary>
    private bool Ally(int countryId) => _game.PlayerId is int pid && _game.World.SameFaction(pid, countryId);
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
            _map.Pockets.Refresh();             // os caldeirões: a terra fechada dentro de um anel
            _map.WarMarks.Refresh();            // as asas e as esquadras: os contadores da guerra no mapa
            _region.Refresh();
            _map.Routes.Refresh();              // rotas da tropa escolhida (depois do painel: lê a selecção dele)
            _production.Refresh();
            _countryPanel.Refresh();
            _worldPanel.Refresh();
            _warPanel.Refresh();
            _armyPanel.Refresh();
            _alerts.Refresh();
            _battle.Refresh();
            _focusTree.Refresh();
            _doctrines.Refresh();
            bool covered = _compare.Visible || _region.Visible || _production.Visible || _countryPanel.Visible
                           || _worldPanel.Visible || _warPanel.Visible || _journal.Visible || _armyPanel.Visible
                           || _battle.Visible || _focusTree.Visible || _doctrines.Visible;
            _alerts.SetCovered(covered);   // a faixa é do mapa: com um painel aberto sai da frente (e dos toques dele)
            _modeBar.SetCovered(covered);
            if (_modeBar.Visible) _modeBar.Refresh();
            _footer.SetCovered(covered);   // já relê o World sozinho quando não está tapado
            _buildBar.SetCovered(covered);
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
            // Um varrimento só às regiões, e dele saem o rendimento, o pool de homens e as três filas de
            // fábricas — e as parcelas com que cada mostrador se explica ao dedo que lhe pousa em cima.
            var parts = Breakdown.Scan(w, pid);
            float income = parts.Income;
            _money.Text = $"{p.Money:0.0}";
            _money.AddThemeColorOverride("font_color", income < 0f ? Ui.Danger : Ui.Text);
            _moneyNote.Text = $"{(income < 0 ? "" : "+")}{income:0.0}/dia";
            _men.Text = FmtMen(p.Manpower);
            _menNote.Text = "recrutas";
            _moneyPlate.TooltipText = Breakdown.Money(p, parts);
            Politics(w, p);
            _menPlate.TooltipText = Breakdown.Men(w, p, parts);
            _divs.Text = w.Divisions.Values.Count(d => d.CountryId == pid).ToString();
            _divsNote.Text = p.Queue.Count > 0 ? $"fila {p.Queue.Count}" : "divisões";
            _divsPlate.TooltipText = Breakdown.Divisions(w, p);
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
            _civPlate.TooltipText = Breakdown.Factories(w, parts, "civil", yards.CivilBusy, yards.Civil,
                                                        "Fábricas civis", "em obra");
            _milPlate.TooltipText = Breakdown.Factories(w, parts, "militar", yards.MilitaryBusy, yards.Military,
                                                        "Fábricas militares", "na fila de produção");
            _yardPlate.TooltipText = Breakdown.Factories(w, parts, "naval", yards.NavalBusy, yards.Naval,
                                                         "Estaleiros", "a levar abastecimento por mar");
            Fuel(w, p);
            Medal(w, p, World.Land, _xpPlate, _xp, _xpNote);
            Medal(w, p, World.Air, _airXpPlate, _airXp, _airXpNote);
            Medal(w, p, World.Sea, _seaXpPlate, _seaXp, _seaXpNote);
            Encircled(w, pid);
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
        else { _ppPlate.Visible = false; _tensionPlate.Visible = false; _fuelPlate.Visible = false; _pocketPlate.Visible = false; _airXpPlate.Visible = false; _seaXpPlate.Visible = false; _country.Text = ""; _money.Text = "—"; _moneyNote.Text = ""; _men.Text = "—"; _menNote.Text = ""; _divs.Text = "—"; _divsNote.Text = ""; _civ.Text = "—"; _civNote.Text = ""; _mil.Text = "—"; _milNote.Text = ""; _yardPlate.Visible = false; _xpPlate.Visible = false; _hint.Visible = true; _playerFlag.Visible = false; _playerFlag.Texture = null; _accent.Color = Ui.SurfaceHi; }
    }

    /// <summary>--smoke: o combustível medido onde ele se vê e onde ele dói. O depósito e o saldo do dia
    /// vêm do FuelSystem; o mostrador é lido da barra tal como está desenhado (some-se quando não há nada a
    /// beber); e a última conta é a que interessa a sério — pergunta-se à tabela modifier quanto perde uma
    /// divisão de blindados quando o país fica a seco, porque é essa linha da BD que faz da mecânica uma
    /// mecânica e não um número na barra.</summary>
    private string SmokeFuel(int pid)
    {
        var w = _game.World; var p = w.Countries[pid];
        int drinkers = w.Divisions.Values.Count(d => d.CountryId == pid && w.Stats.Get(d.TemplateId)["fuel_use"] > 0f);
        // O multiplicador da seca sobre uma divisão que beba: a mesma pergunta que o combate faz. Se o
        // jogador não tiver máquinas nenhumas — ao dia 79 Portugal não tem — mede-se numa do mundo, porque o
        // que interessa provar é que a linha da BD existe e morde, não de quem é a divisão.
        string bite = "sem máquinas no mundo para medir";
        var thirsty = w.Divisions.Values.FirstOrDefault(d => d.CountryId == pid && w.Stats.Get(d.TemplateId)["fuel_use"] > 0f)
                   ?? w.Divisions.Values.FirstOrDefault(d => w.Stats.Get(d.TemplateId)["fuel_use"] > 0f);
        if (thirsty is not null)
        {
            var st = w.Stats.Get(thirsty.TemplateId);
            var (wetF, wetM) = w.Modifiers.Evaluate("str", st, new ModContext());
            var (dryF, dryM) = w.Modifiers.Evaluate("str", st, new ModContext().With("fuel_out", "true"));
            bite = $"a seco {(thirsty.CountryId == pid ? "os nossos" : "os de " + (w.Countries.TryGetValue(thirsty.CountryId, out var tc) ? tc.Tag : "?"))}"
                 + $" batem a {(dryM + dryF) / MathF.Max(0.0001f, wetM + wetF):P0} (bebem {w.Stats.Get(thirsty.TemplateId)["fuel_use"]:0.0}/dia)";
        }
        string plate = _fuelPlate.Visible ? $"mostrador {_fuel.Text} ({_fuelNote.Text})" : "mostrador escondido";
        return $"{p.Fuel:0} de {p.FuelCap:0} no depósito, +{p.FuelIn:0.0}/dia refinado e −{p.FuelUse:0.0}/dia bebido"
             + $" por {drinkers} divis{(drinkers == 1 ? "ão" : "ões")}, {plate}, {bite}";
    }

    /// <summary>Voluntários: quantas divisões nossas se batem na guerra de outro, quantas cabem ainda, e o
    /// mesmo para o mundo todo — a mecânica não é do jogador, é de toda a gente, e ao dia 79 Portugal
    /// costuma não ter nada lá fora. Mede-se também a mordida da linha da BD, que é o que prova que ser
    /// voluntário custa alguma coisa mesmo quando ninguém ainda mandou ninguém.</summary>
    private string SmokeVolunteers(int pid)
    {
        var w = _game.World;
        int away = VolunteerSystem.Away(w, pid), cap = VolunteerSystem.Cap(w, pid);
        int world = w.Divisions.Values.Count(d => d.IsVolunteer);
        int senders = w.Divisions.Values.Where(d => d.IsVolunteer).Select(d => d.VolunteerFrom).Distinct().Count();
        int able = w.Countries.Values.Count(c => !c.Capitulated && c.AtWarWith.Count == 0
                                              && VolunteerSystem.Pick(w, c.Id) is not null);

        var st = w.Stats.Get(w.Divisions.Values.First(d => d.CountryId == pid).TemplateId);
        var (homeF, homeM) = w.Modifiers.Evaluate("str", st, new ModContext());
        var (awayF, awayM) = w.Modifiers.Evaluate("str", st, new ModContext().With("volunteer", "true"));
        string bite = $"longe de casa batem a {(awayM + awayF) / MathF.Max(0.0001f, homeM + homeF):P0}";

        return $"{away} de {cap} nossas lá fora, {world} no mundo de {senders} país{(senders == 1 ? "" : "es")}"
             + $" ({able} em condições de mandar hoje), {bite}";
    }

    /// <summary>--smoke: as duas moedas e o termómetro. Diz o poder político que temos e o que ele rende
    /// por dia com as parcelas todas, diz a tensão do mundo e quem a está a puxar, e depois prova a coisa
    /// que interessa: que a tensão TRANCA de verdade. Escolhe uma lei com min_tension, tenta aprová-la com
    /// o cofre político cheio, e diz se passou ou o que a barrou — se um dia a porta deixar de existir, esta
    /// linha diz "aberta" com o mundo em paz e o smoke denuncia-o.</summary>
    private string SmokePolitics(int pid)
    {
        var w = _game.World;
        var c = w.Countries[pid];
        float t = WorldTension.Of(w);
        var parts = WorldTension.Parts(w);
        var mine = PoliticsSystem.Parts(w, pid);

        // o cofre político do dia 73 não paga nada: o smoke adianta-o, como faz com o cofre de produção
        float before = c.Political;
        c.Political = MathF.Max(c.Political, w.Rule("law_change_cost", 30f) + w.Rule("justify_cost", 25f));

        var locked = w.Laws.Values.Where(l => l.MinTension > 0f && World.LawIsFor(l, c))
                          .OrderBy(l => l.MinTension).ThenBy(l => l.Id).FirstOrDefault();
        string gate = locked is null ? "nenhuma lei pede tensão"
            : new ChangeLawCommand(pid, locked.Id).Validate(w) is string why
                ? $"\"{locked.Name}\" barrada: {why}"
                : $"\"{locked.Name}\" já passa (tensão {locked.MinTension:0} alcançada)";

        // e a porta do longe: justificar guerra a quem não faz fronteira connosco
        var far = w.Countries.Values.FirstOrDefault(o => o.Id != pid && !o.Capitulated
                                                     && !w.AreAtWar(pid, o.Id) && !w.SharesBorder(pid, o.Id));
        string reach = far is null ? "sem alvo distante"
            : new JustifyWarCommand(pid, far.Id).Validate(w) is string no
                ? $"guerra a {far.Tag} barrada: {no}"
                : $"guerra a {far.Tag} ao nosso alcance";
        c.Political = before;

        int laws = w.Laws.Values.Count(l => l.MinTension > 0f);
        return $"{c.Political:0} de poder político (+{PoliticsSystem.Gain(w, pid):0.0}/dia em {mine.Count} parcela{(mine.Count == 1 ? "" : "s")}"
             + $": {string.Join(", ", mine.Select(x => $"{x.Label} {(x.Points < 0 ? "" : "+")}{x.Points:0.0}"))})"
             + $", tensão mundial {t:0}/100 — {WorldTension.Mood(w)} ({parts.Count} parcela{(parts.Count == 1 ? "" : "s")}"
             + (parts.Count > 0 ? $", a maior: {parts[0].Label} +{parts[0].Points:0.0}" : "") + ")"
             + $", {laws} lei{(laws == 1 ? "" : "s")} presa{(laws == 1 ? "" : "s")} à tensão, {gate}, {reach}"
             + $", volta ao cofre: lei {w.Rule("law_change_cost", 30f):0} pp, pacto {w.Rule("nap_cost", 20f):0} pp, pretexto {w.Rule("justify_cost", 25f):0} pp";
    }

    /// <summary>--smoke: os governos no exílio. Conta o mundo e não só a nossa casa, porque um exílio é
    /// coisa que quase sempre acontece a outros: quantos governos andam fora, com que legitimidade média e
    /// quantos deles já podiam voltar hoje. Num mundo em paz isto dá tudo zero — e é por isso que se mede
    /// também o direito de asilo (ExileSystem.Host de cada país de pé), que é a peça que decide se um país
    /// que caia amanhã continua a existir ou acaba ali. Do nosso lado conta-se o que nos custa: os governos
    /// que acolhemos e as regiões que a nossa tropa tem na mão e devolveria ao dono no dia do regresso.</summary>
    private string SmokeExile(int pid)
    {
        var w = _game.World;
        var exiled = w.Countries.Values.Where(c => c.InExile).ToList();
        int dead = w.Countries.Values.Count(c => c.Capitulated && c.ExileHostId is null);
        float legit = exiled.Count == 0 ? 0f : exiled.Average(c => c.ExileLegitimacy);
        int back = exiled.Count(c => c.ExileLegitimacy >= w.Rule("exile_return_legitimacy", 0.6f)
                                     && ExileSystem.Occupier(w, c) is int o
                                     && (o == c.ExileHostId || w.SameFaction(c.ExileHostId!.Value, o)));

        var standing = w.Countries.Values.Where(c => !c.Capitulated).ToList();
        int asylum = standing.Count(c => ExileSystem.Host(w, c) is not null);
        int hosted = exiled.Count(c => c.ExileHostId == pid);
        int owed = exiled.Count == 0 ? 0
            : w.Regions.Values.Count(r => r.ControllerId == pid && exiled.Any(c => c.Id == r.InitialOwnerId));

        return $"{exiled.Count} governo{(exiled.Count == 1 ? "" : "s")} fora de casa"
             + $" (legitimidade média {legit:P0}, {back} em condições de voltar hoje), {dead} capitulado{(dead == 1 ? "" : "s")} sem governo,"
             + $" {asylum} de {standing.Count} países com asilo se caírem hoje; acolhemos {hosted}, devolveríamos {owed} regiões";
    }

    /// <summary>As duas chapas da política: o que temos de poder político (e o que ele rende por dia) e a
    /// que temperatura está o mundo. A tensão pinta-se: em paz é texto normal, à beira é âmbar, em chamas
    /// é vermelho — o jogador tem de ver o mundo aquecer sem ir ler número nenhum.</summary>
    private void Politics(World w, Country p)
    {
        _ppPlate.Visible = _tensionPlate.Visible = true;
        float gain = PoliticsSystem.Gain(w, p.Id);
        _pp.Text = $"{p.Political:0}";
        _pp.AddThemeColorOverride("font_color", gain <= 0f ? Ui.Danger : Ui.Text);
        _ppNote.Text = $"{(gain < 0 ? "" : "+")}{gain:0.0}/dia";
        _ppPlate.TooltipText = Breakdown.Political(w, p);

        float t = WorldTension.Of(w);
        _tension.Text = $"{t:0}";
        _tension.AddThemeColorOverride("font_color",
            t >= w.Rule("tension_grave", 70f) ? Ui.Danger
            : t >= w.Rule("tension_uneasy", 40f) ? Ui.Accent : Ui.Text);
        _tensionNote.Text = WorldTension.Mood(w);
        _tensionPlate.TooltipText = Breakdown.Tension(w);
    }

    /// <summary>O barril da barra: quanto combustível está no depósito, o saldo do dia e — quando o dia não
    /// paga o dia — quantos dias faltam até parar. Um país sem uma máquina a beber e sem petróleo nenhum não
    /// mostra mostrador: é barra ocupada a dizer zero.</summary>
    private void Fuel(World w, Country p)
    {
        _fuelPlate.Visible = p.FuelUse > 0f || p.FuelIn > 0f || p.Fuel > 0f;
        if (!_fuelPlate.Visible) return;
        _fuel.Text = $"{p.Fuel:0}/{p.FuelCap:0}";
        float net = p.FuelIn - p.FuelUse;
        _fuel.AddThemeColorOverride("font_color", p.FuelOut ? Ui.Danger : net < 0f ? Ui.Accent : Ui.Text);
        float days = FuelSystem.DaysLeft(p);
        _fuelNote.Text = p.FuelOut ? "a seco"
            : days >= 0f ? $"{days:0} dias"
            : $"{(net < 0 ? "" : "+")}{net:0.0}/dia";
        _fuelPlate.TooltipText = $"Combustível: {p.Fuel:0.0} de {p.FuelCap:0} no depósito.\n"
            + $"Refina {p.FuelIn:0.0}/dia do petróleo que controlamos e bebe {p.FuelUse:0.0}/dia"
            + (p.AtWarWith.Count > 0 ? " (em guerra as máquinas gastam mais)." : ".")
            + (p.FuelOut ? "\nSem combustível os blindados batem a metade." : "");
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
        plate.TooltipText = Breakdown.Medal(w, p, domain);
    }

    /// <summary>--smoke: a barra de topo medida — quantos mostradores estão à vista, quanta largura pedem
    /// e em quantas linhas isso cai no ecrã de agora. Sem passagem de layout o Size é 0, por isso mede-se
    /// pelo tamanho mínimo das chapas contra a largura do viewport.</summary>
    private string SmokeTopBar()
    {
        float wide = MathF.Max(1f, _top.GetViewportRect().Size.X);
        float tall = MathF.Max(_top.Size.Y, _top.GetCombinedMinimumSize().Y);
        // As chapas da barra e quantos mostradores se explicam ao dedo: um mostrador com uma conta por
        // trás tem um tooltip de várias linhas (as parcelas), e é isso que aqui se conta — um número sem
        // parcelas na barra de cima é um oráculo, e era o que o jogo tinha antes.
        var (drawn, fell) = Glyph.Count(_top);
        int counters = 0, explains = 0;
        foreach (var child in _plates.GetChildren())
            if (child is PanelContainer pc)
            {
                counters++;
                if (pc.TooltipText.Contains('\n')) explains++;
            }
        return $"{Rows(_plates, "mostradores", wide)}, {Rows(_nav, "chapas", wide)}"
             + $", barra de {tall:0}px (faixa de alarmes a {_alerts.OffsetTop:0})"
             + $", {drawn} chapas desenhadas na barra ({fell} na roda)"
             + $", {explains} de {counters} mostradores explicam a conta";
    }

    /// <summary>Quantas linhas é que uma fila que dobra ocupa na largura de agora (o layout headless não a
    /// mediu ainda, por isso soma-se o tamanho mínimo de cada chapa mais a separação entre elas).</summary>
    private static string Rows(HFlowContainer flow, string what, float wide)
    {
        var shown = flow.GetChildren().OfType<Control>().Where(c => c.Visible).ToList();
        float sep = flow.GetThemeConstant("h_separation");
        float need = shown.Sum(c => c.GetCombinedMinimumSize().X) + sep * Math.Max(0, shown.Count - 1);
        int rows = (int)MathF.Ceiling(need / wide);
        return $"{shown.Count} {what} em {rows} linha{(rows == 1 ? "" : "s")} de {need:0}px por {wide:0}px";
    }

    /// <summary>--smoke: manda saltar a primeira divisão de pára-quedistas do país e diz para onde. A ordem
    /// é a mesma que a barra de selecção dá — e como o voo leva dias, é o tick do jogo que a faz aterrar
    /// antes de a prova acabar.</summary>
    private string SmokeParadrop(int pid)
    {
        var w = _game.World;
        int world = w.Divisions.Values.Count(d => ParadropSystem.IsAirborne(w, d));
        var para = w.Divisions.Values.Where(d => d.CountryId == pid && ParadropSystem.IsAirborne(w, d))
                                     .OrderBy(d => d.Id).FirstOrDefault();
        if (para is null) return $"sem pára-quedistas nossos ({world} no mundo)";
        // --smoke: o salto passou a pedir transportes a sério (um céu de caças não larga ninguém); se o
        // hangar não os tem, põem-se lá à mão — a prova é a divisão aterrar, não o cofre
        float lift = w.Rule("paradrop_wings", 3f);
        if (AirMissionSystem.Free(w, pid, "transport") < lift
            && Air.Choose(w, float.MaxValue, "transport") is string carga && carga.Length > 0)
            w.Countries[pid].Planes[carga] = w.Countries[pid].Planes.GetValueOrDefault(carga) + lift;
        int range = (int)w.Rule("paradrop_range_hops", 4f);
        var reach = ParadropSystem.Reach(w, para.RegionId, range);
        string name = DivisionView.Title(w, para);
        var spot = reach.Keys.OrderBy(id => id)
            .FirstOrDefault(id => ParadropSystem.Block(w, pid, para.Id, id) is null);
        if (spot == 0)
            return $"{name} sem sítio de salto em {reach.Count} regiões ao alcance"
                 + $" ({(reach.Count == 0 ? "sem alcance" : ParadropSystem.Block(w, pid, para.Id, reach.Keys.Min())!)}, {world} no mundo)";
        var err = _game.Dispatch(new ParadropCommand(pid, para.Id, spot));
        return err ?? $"{name} salta sobre {w.Regions[spot].Name} a {reach[spot]} região{(reach[spot] == 1 ? "" : "es")}"
                    + $" ({para.DropDays:0} dias no ar, {ParadropSystem.InFlight(w, pid):0.#} asas presas,"
                    + $" {reach.Count} regiões ao alcance, {world} divisões de salto no mundo)";
    }

    /// <summary>--smoke: operação anfíbia. Prova as três coisas que a mecânica trouxe e que antes não
    /// existiam: que uma ordem de marcha já não assalta uma praia inimiga, que a operação se marca e leva
    /// semanas a preparar-se, e que no dia em que está pronta larga sozinha com a tropa embarcada a caminho
    /// da praia. A preparação adianta-se à mão — esperar doze dias de jogo dentro da prova não provava mais
    /// nada, e o que se quer ver é o dia da largada.</summary>
    private string SmokeInvasion(int pid)
    {
        var w = _game.World;
        Region? port = null, beach = null;
        foreach (var r in w.Regions.Values.Where(x => x.ControllerId == pid && x.SeaNeighbours.Count > 0).OrderBy(x => x.Id))
            if (NavalInvasionSystem.Beaches(w, pid, r.Id).FirstOrDefault() is Region b) { port = r; beach = b; break; }
        if (port is null || beach is null)
        {
            int coasts = w.Regions.Values.Count(r => r.ControllerId == pid && r.SeaNeighbours.Count > 0);
            return $"sem praia inimiga do outro lado do mar ({coasts} costas nossas com rota, "
                 + $"{NavalInvasionSystem.FreeConvoys(w, pid):0} mercantes livres)";
        }

        // tropa no cais: o mundo do smoke não tem forçosamente uma divisão ali parada, e a prova é a
        // operação, não a marcha até ao porto
        var d = w.Divisions.Values.Where(x => x.CountryId == pid && x.RegionId == port.Id && x.CanFight
                                              && !w.InBattle(x.Id) && !x.InFlight).OrderBy(x => x.Id).FirstOrDefault()
                ?? w.Divisions.Values.Where(x => x.CountryId == pid && x.CanFight && !w.InBattle(x.Id) && !x.InFlight)
                    .OrderBy(x => x.Id).FirstOrDefault();
        if (d is null) return $"sem divisões nossas para embarcar em {port.Name}";
        if (d.RegionId != port.Id) { d.ClearPath(); w.PlaceDivision(d, port.Id); }

        // a porta nova: marchar para a praia deixou de ser uma ordem legítima
        string refused = new MoveDivisionCommand(pid, d.Id, beach.Id).Validate(w) ?? "aceite (não devia)";

        if (_game.Dispatch(new PlanNavalInvasionCommand(pid, beach.Id, new List<int> { d.Id })) is string no)
            return $"{port.Name} → {beach.Name} recusada: {no}";
        var inv = w.NavalInvasions.First(i => i.CountryId == pid && i.TargetId == beach.Id);
        inv.Prep = 1f;                                   // o dia da largada, sem esperar as semanas todas
        string resumo = NavalInvasionSystem.Short(w, pid);

        new NavalInvasionSystem().Tick(w);
        bool sailed = d.Seaborne && d.Path.Count > 0 && d.Path[0] == beach.Id;
        return $"{resumo} ({DivisionView.Title(w, d)} embarcada; marcha directa recusada: «{refused}»;"
             + $" largou: {(sailed ? "sim, tropa a caminho da praia" : "não")},"
             + $" {w.NavalInvasions.Count} operaç{(w.NavalInvasions.Count == 1 ? "ão" : "ões")} por largar no mundo)";
    }

    /// <summary>--smoke: o chão do céu (AirBases). Prova as três coisas que a mecânica trouxe: que uma asa
    /// dorme num campo nosso, que levantar um campo de aviação acrescenta camas e alcance, e que um céu do
    /// outro lado do mundo é recusado por distância e não por fronteira — que era a mentira antiga, em que
    /// bastava fazer fronteira para a força aérea inteira aparecer em qualquer céu. O campo levanta-se à
    /// mão: a obra leva semanas e o que se quer ver é a lotação, não o calendário.</summary>
    private string SmokeAirBases(int pid)
    {
        var w = _game.World;
        if (w.BuildingDefs.Values.FirstOrDefault(d => d.IsAirfield) is not BuildingDef field)
            return "sem campo de aviação na tabela";
        if (AirMissionSystem.Front(w, pid) is not int sky) return "sem céu da frente para medir";
        var (near, km) = AirBases.Nearest(w, pid, sky);
        if (near is null) return "sem terra nossa de onde levantar";

        float before = AirBases.Slots(w, near), reachBefore = AirBases.Extra(w, near);
        near.Buildings[field.Id] = near.Buildings.GetValueOrDefault(field.Id) + 1;
        float after = AirBases.Slots(w, near), reachAfter = AirBases.Extra(w, near);

        // o céu inimigo mais longe deste campo, e o que as asas de hoje alcançam: é aqui que a geografia
        // manda — a mesma conta em km reais que o comando faz antes de deixar destacar
        float reach = AirBases.Range(w, Air.Pick(w, pid, "superiority", 1f));
        var far = w.Regions.Values.Where(r => w.AreAtWar(pid, r.ControllerId))
                   .OrderByDescending(r => w.Km(near.Id, r.Id)).ThenBy(r => r.Id).FirstOrDefault();
        string longe = far is null ? "sem céu deles para medir"
            : $"{far.Name} a {w.Km(near.Id, far.Id):0} km, {(AirBases.Covers(w, pid, far.Id, reach) ? "ao alcance" : "fora do alcance")} de asas de {reach:0} km";
        return $"{AirBases.Short(w, pid)}; {near.Name} a {km:0} km da frente, {field.Name.ToLowerInvariant()} nível "
             + $"{near.Buildings[field.Id]} leva as camas de {before:0} a {after:0} e o alcance de +{reachBefore:0} a "
             + $"+{reachAfter:0} km; céu deles mais longe: {longe}; destacar sobre {w.Regions[sky].Name}: "
             + $"«{AirMissionSystem.Block(w, pid, sky, "superioridade", 1f) ?? "aceite"}»";
    }

    /// <summary>--smoke: o campo de aviação que flutua (porta-aviões) e o aço que o céu deita ao fundo. Prova
    /// as duas coisas que a mecânica trouxe: que um casco leva asas a mar onde não há terra nossa nenhuma —
    /// um céu recusado por distância passa a aceite assim que o convés lá chega — e que há uma missão de
    /// ataque naval para lhes mandar. O casco põe-se ao mar à mão: o que se quer ver é o alcance, não o
    /// estaleiro.</summary>
    private string SmokeCarriers(int pid)
    {
        var w = _game.World;
        if (w.ShipClasses.Values.FirstOrDefault(d => d.IsCarrier) is not ShipClassDef carrier)
            return "sem porta-aviões na tabela";
        if (w.AirMissionDefs.Values.FirstOrDefault(d => d.Effect == "naval") is not AirMissionDef strike)
            return "sem ataque naval na tabela";
        int modelos = w.PlaneClasses.Values.Count(d => d.Deck);
        string folha = $"{carrier.Name}: {carrier.Deck:0.#} asas por casco, {modelos} model{(modelos == 1 ? "o" : "os")} "
                     + $"de convés, alcance de bordo {w.Rule("air_carrier_range", 600f):0} km";

        // o mar mais perto de casa onde a aviação de terra não chega: é exactamente o que o convés existe
        // para abrir, e é o único sítio onde a prova se vê a mudar de não para sim
        var c = w.Countries[pid];
        float reach = AirBases.Range(w, Air.Pick(w, pid, strike.Effect, 1f));
        // as asas que cabem num convés são as do nosso hangar que a tabela deixa embarcar: um país que só
        // tenha caças de superioridade tem porta-aviões e não tem quem lá durma, e isso é para se ler
        float cabem = AirBases.DeckWings(w, c.Planes);
        var sea = w.Regions.Values.Where(r => r.Coastal && !AirBases.Covers(w, pid, r.Id, reach))
                   .OrderBy(r => w.Km(r.Id, c.CapitalRegionId)).ThenBy(r => r.Id).FirstOrDefault();
        if (sea is null) return folha + "; sem mar fora do alcance para medir";
        float roomBefore = AirBases.Room(w, pid, sea.Id, reach, carrier.Deck);

        // e agora com o casco lá: a esquadra leva o campo com ela
        c.Ships[carrier.Id] = c.Ships.GetValueOrDefault(carrier.Id) + 1f;
        var fleet = w.NavalMissions.FirstOrDefault(m => m.CountryId == pid && m.RegionId == sea.Id);
        if (fleet is null)
        {
            fleet = new NavalMission
            {
                CountryId = pid, RegionId = sea.Id, SinceDay = w.Clock.Day,
                MissionId = w.NavalMissionDefs.Values.OrderBy(d => d.Sort).First().Id,
                Name = w.NextFormationName(pid, World.Sea, sea.Id),
            };
            w.NavalMissions.Add(fleet);
        }
        fleet.Squadron[carrier.Id] = fleet.Squadron.GetValueOrDefault(carrier.Id) + 1f;
        var alvo = w.Regions.Values.Where(r => r.Coastal && w.AreAtWar(pid, r.ControllerId))
                    .OrderBy(r => w.Km(r.Id, c.CapitalRegionId)).ThenBy(r => r.Id).FirstOrDefault() ?? sea;
        return $"{folha}; {sea.Name} a {w.Km(sea.Id, c.CapitalRegionId):0} km da capital, fora do alcance de "
             + $"asas de {reach:0} km ({roomBefore:0.#} camas); com {fleet.Name} lá: "
             + $"{(AirBases.Covers(w, pid, sea.Id, reach) ? "ao alcance" : "fora do alcance")}, "
             + $"{AirBases.Room(w, pid, sea.Id, reach, carrier.Deck):0.#} camas, das quais {cabem:0.#} asas "
             + $"nossas lá cabem; {strike.Name.ToLowerInvariant()} sobre {alvo.Name}: "
             + $"«{AirMissionSystem.Block(w, pid, alvo.Id, strike.Id, 1f) ?? "aceite"}»";
    }

    /// <summary>--smoke: a guerra submarina. Prova a única coisa que a mecânica tem para provar — que o que
    /// se esconde não leva tiro, e que se vai lá buscar com sonar e não com peso. Põe-se um bando de
    /// submarinos deles no nosso mar (sem busca nenhuma, ficam todos escondidos) e manda-se a caça: o
    /// escondido cai, o que se vê passa a apanhável e a conta do dia diz quantos vão ao fundo.</summary>
    private string SmokeSubs(int pid)
    {
        var w = _game.World;
        if (w.ShipClasses.Values.FirstOrDefault(d => d.IsSub) is not ShipClassDef boat) return "sem submarino na tabela";
        var dog = w.ShipClasses.Values.Where(d => d.IsHunter).OrderByDescending(d => d.Asw).ThenBy(d => d.Sort).FirstOrDefault();
        string mid = Subs.MissionId(w);
        if (dog is null || mid.Length == 0) return "sem caça anti-submarina na tabela";
        var def = w.NavalMissionDefs[mid];
        string folha = $"{boat.Name} esconde-se {boat.Stealth:0%}, {dog.Name} tem sonar {dog.Asw:0.#}, "
                     + $"{def.Name.ToLowerInvariant()} afunda {w.Rule("asw_kill", 0.06f):0.##}/ponto ao dia";

        var c = w.Countries[pid];
        var foe = w.Countries.Values.Where(x => w.AreAtWar(pid, x.Id)).OrderBy(x => x.Id).FirstOrDefault();
        var coast = w.Regions.Values.Where(r => r.ControllerId == pid && r.SeaNeighbours.Count > 0)
                     .OrderBy(r => w.Km(r.Id, c.CapitalRegionId)).ThenBy(r => r.Id).FirstOrDefault();
        if (foe is null || coast is null) return folha + "; sem costa nossa ou sem guerra para medir";

        // o bando deles debaixo do nosso mar, posto à mão: o que se quer ver é a busca, não o estaleiro
        string zone = Zones.Sea(w, coast.Id);
        float pack = 6f;
        var raid = new NavalMission
        {
            CountryId = foe.Id, RegionId = coast.Id, SinceDay = w.Clock.Day,
            MissionId = w.NavalMissionDefs.Values.OrderBy(d => d.Sort).First().Id,
            Name = w.NextFormationName(foe.Id, World.Sea, coast.Id),
        };
        raid.Squadron[boat.Id] = pack;
        w.NavalMissions.Add(raid);
        float before = Subs.HiddenShare(w, foe.Id, zone);

        // e a nossa caça em cima deles: os cascos saem do porto pelo sonar (Navy.Value com efeito 'asw')
        float hulls = 10f;
        c.Ships[dog.Id] = c.Ships.GetValueOrDefault(dog.Id) + hulls;
        // o cofre da prova já vai gasto de fragmentos anteriores e a estadia do dia tapava a verdadeira
        // resposta da porta: paga-se a estadia só para a perguntar, e devolve-se o cofre como estava
        float cofre = c.Money;
        c.Money = MathF.Max(cofre, hulls * w.Rule("naval_mission_upkeep", 0.8f) + 1f);
        string no = NavalMissionSystem.Block(w, pid, coast.Id, mid, hulls) ?? "aceite";
        c.Money = cofre;
        NavalMissionSystem.Assign(w, pid, coast.Id, mid, hulls);
        var hunt = w.NavalMissions.FirstOrDefault(m => m.CountryId == pid && m.RegionId == coast.Id);
        float power = hunt is null ? 0f : hunt.Squadron.Sum(kv => kv.Value * Subs.Asw(w, kv.Key)) * def.Value;
        float after = Subs.HiddenShare(w, foe.Id, zone), seen = Subs.Visible(w, foe.Id, zone);
        return $"{folha}; {pack:0.#} submarinos deles no mar de {coast.Name} ({zone}): {before:P0} escondidos; "
             + $"com {hulls:0.#} cascos nossos em {def.Name.ToLowerInvariant()} ({Navy.Describe(w, hunt?.Squadron ?? new())}, "
             + $"sonar {power:0.#}): {after:P0} escondidos, {seen:0.#} à vista, "
             + $"{MathF.Min(seen, power * w.Rule("asw_kill", 0.06f)):0.##} ao fundo hoje; destacar: «{no}»";
    }

    /// <summary>--smoke: até onde a rede de abastecimento chega. Diz a regra, a divisão nossa mais afastada
    /// do depósito e quantas já vão com a linha fina — e, para o preço das estradas aparecer mesmo num
    /// mundo em paz, o que custaria atravessar as regiões à volta da capital se fossem terra tomada.</summary>
    private string SmokeSupplyReach(int pid)
    {
        var w = _game.World;
        var ours = w.Divisions.Values.Where(d => d.CountryId == pid && !d.Cut).ToList();
        float deep = ours.Count == 0 ? 0f : ours.Max(d => d.SupplyDepth);
        int thin = ours.Count(d => SupplySystem.Reach(w, d.SupplyDepth) < 1f);
        int free = (int)w.Rule("supply_reach_free", 3f);
        var cap = w.Regions[w.Countries[pid].CapitalRegionId];
        var round = cap.Neighbours.Select(id => w.Regions[id]).ToList();
        string roads = round.Count == 0
            ? "capital sem vizinhas"
            : $"à volta da capital {round.Min(r => SupplySystem.StepCost(w, r)):0.##}–{round.Max(r => SupplySystem.StepCost(w, r)):0.##} por região";
        return $"{free} regiões de graça, depois −{w.Rule("supply_reach_decay", 0.12f):P0}/região até {w.Rule("supply_reach_min", 0.5f):P0}"
             + $" ({ours.Count} divisões ligadas, a mais afastada a {deep:0.#}, {thin} com a linha fina; {roads})";
    }

    /// <summary>--smoke: redespacho estratégico. Manda mesmo uma divisão nossa para o comboio, mede o que
    /// os carris lhe pouparam em dias e o que lhe custaram em organização, e volta a pô-la como estava —
    /// num mundo em paz não há redespacho nenhum a acontecer e sem isto a ordem não se provava.</summary>
    private string SmokeRedeploy(int pid)
    {
        var w = _game.World;
        var d = w.Divisions.Values.Where(x => x.CountryId == pid && !w.InBattle(x.Id)).OrderBy(x => x.Id).FirstOrDefault();
        if (d is null) return "sem divisões nossas";
        // destino: a região nossa mais longe desta a que os carris cheguem, que é para o que serve o comboio
        var reach = Redeploy.Reach(w, d.RegionId, pid);
        if (reach.Count == 0) return "sem retaguarda ligada por carris";
        int end = reach.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        var far = (r: w.Regions[end], path: Redeploy.Path(w, d.RegionId, end, pid)!);
        var origin = w.Regions[d.RegionId];
        var next = w.Regions[far.path[0]];
        float onFoot = MovementSystem.HopDays(w, d, origin, next);
        var (path, org, riding) = (d.Path.ToList(), d.Org, d.Redeploying);
        var err = _game.Dispatch(new RedeployCommand(pid, d.Id, far.r.Id));
        string line = err ?? $"{DivisionView.Title(w, d)} de {origin.Name} para {far.r.Name} em {far.path.Count} saltos"
            + $" ({onFoot:0.#} → {MovementSystem.HopDays(w, d, origin, next):0.#} dias por salto,"
            + $" organização {org:0} → {d.Org:0}, recomposição a {w.Rule("redeploy_org_regain", 0.25f):P0})";
        d.SetPath(path); d.Org = org; d.Redeploying = riding;      // o smoke não deixa tropa em viagem
        int world = w.Divisions.Values.Count(x => x.Redeploying);
        return $"{line}; {world} em viagem no mundo";
    }

    /// <summary>--smoke: o tempo local. Conta o céu do mundo inteiro região a região (é o mesmo sorteio que
    /// o mapa pinta no modo "Tempo"), diz o que está por cima da nossa capital e o que isso lhe custa, e
    /// prova que o céu é notícia para o combate perguntando à tabela de modificadores — não a uma segunda
    /// conta escrita aqui.</summary>
    private string SmokeWeather(int pid)
    {
        var w = _game.World;
        if (w.WeatherDefs.Count == 0) return "sem tempo carregado";
        var census = new Dictionary<string, int>();
        foreach (var r in w.Regions.Values)
            if (Weather.Of(w, r) is WeatherDef d) census[d.Id] = census.GetValueOrDefault(d.Id) + 1;
        if (census.Count == 0) return "nenhuma região com céu";
        string spread = string.Join(", ", census.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
            .Select(kv => $"{w.WeatherDefs[kv.Key].Icon} {w.WeatherDefs[kv.Key].Name} {kv.Value}"));

        var cap = w.Regions[w.Countries[pid].CapitalRegionId];
        var sky = Weather.Of(w, cap)!;
        // o assalto debaixo deste céu contra o mesmo assalto sem ele: a diferença sai da tabela modifier
        float bite = GroundSystem.Terrain(w, cap.Terrain, cap.River, attacking: true, sky.Id)
                     / MathF.Max(0.01f, GroundSystem.Terrain(w, cap.Terrain, cap.River, attacking: true));
        // o pior céu do mundo hoje: é o que o modo de mapa pinta mais carregado
        var worst = w.Regions.Values.OrderBy(r => Weather.Of(w, r)?.MoveMult ?? 1f).ThenBy(r => r.Id).First();
        var worstSky = Weather.Of(w, worst)!;
        int painted = MapModes.Shades(w, pid, "weather").Count;
        int days = (int)MathF.Ceiling(MathF.Max(1f, w.Rule("weather_days", 5f)) - w.Clock.Day % MathF.Max(1f, w.Rule("weather_days", 5f)));
        return $"{spread}; capital {cap.Name} com {sky.Name} (frio {Weather.Cold(w, cap):0.00}, marcha ×{sky.MoveMult:0.00},"
             + $" assalto ×{bite:0.00}, aviação ×{sky.AirMult:0.00}, muda em {days} dia{(days == 1 ? "" : "s")});"
             + $" pior em {worst.Name} com {worstSky.Name} (marcha ×{worstSky.MoveMult:0.00});"
             + $" {painted} regiões pintadas no modo tempo; chapa {sky.Glyph}"
             + $" {(Glyph.Knows(sky.Glyph) ? "desenhada" : "na roda")}";
    }

    /// <summary>--smoke: tácticas de combate. Conta o que cada lado escolheria hoje em cada região do mundo,
    /// mostra as duas tácticas frente a frente onde se combate (ou na capital, se o mundo estiver em paz),
    /// prova que a leitura do inimigo desconta mesmo o que a tabela manda e conta as chapas desenhadas.</summary>
    private string SmokeTactics(int pid)
    {
        var w = _game.World;
        if (w.TacticDefs.Count == 0) return "sem tácticas carregadas";
        var census = new Dictionary<string, int>();
        int read = 0;
        foreach (var r in w.Regions.Values)
        {
            if (Tactics.Of(w, r, attacking: true) is TacticDef a) census[a.Id] = census.GetValueOrDefault(a.Id) + 1;
            if (Tactics.Of(w, r, attacking: false) is TacticDef d) census[d.Id] = census.GetValueOrDefault(d.Id) + 1;
            if (Tactics.Countered(w, r, attacking: true) || Tactics.Countered(w, r, attacking: false)) read++;
        }
        if (census.Count == 0) return "nenhuma região com táctica";
        string spread = string.Join(", ", census.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
            .Select(kv => $"{w.TacticDefs[kv.Key].Icon} {w.TacticDefs[kv.Key].Name} {kv.Value}"));

        // onde se combate hoje, e na falta de guerra a nossa capital: as duas tácticas frente a frente
        int stageId = w.ActiveBattles.FirstOrDefault()?.RegionId ?? w.Countries[pid].CapitalRegionId;
        var stage = w.Regions[stageId];
        int drawn = w.TacticDefs.Values.Count(t => Glyph.Knows(t.Glyph));
        return $"{spread} ({w.TacticDefs.Count} na tabela, {read} regiões com uma lida);"
             + $" em {stage.Name}: {Tactics.Line(w, stage)}"
             + $" (chapa {Tactics.Plate(w, stage, attacking: true)?.Glyph ?? "—"}, mudam em {Tactics.DaysLeft(w)}"
             + $" dia{(Tactics.DaysLeft(w) == 1 ? "" : "s")});"
             + $" {drawn} de {w.TacticDefs.Count} chapas desenhadas";
    }

    /// <summary>--smoke: pontos de vitória. Diz os graus da tabela, quanto vale o mundo e quanto dele está
    /// na nossa mão, as praças de quem nos faz guerra, e prova que o mapa e a pastilha dizem o mesmo número
    /// que a conta da paz — é o mesmo VictoryPoints.Of nos três sítios.</summary>
    private string SmokeVictory(int pid)
    {
        var w = _game.World;
        if (w.VictoryTiers.Count == 0) return "sem pontos de vitória carregados";
        var census = new Dictionary<string, int>();
        int world = 0;
        foreach (var r in w.Regions.Values)
            if (VictoryPoints.Tier(w, r) is VictoryTierDef t) { census[t.Id] = census.GetValueOrDefault(t.Id) + 1; world += t.Points; }
        string spread = string.Join(", ", w.VictoryTiers.Values.OrderBy(t => t.Sort)
            .Select(t => $"{t.Icon} {t.Name} {census.GetValueOrDefault(t.Id)}×{t.Points}"));

        var foe = w.Countries.Values.FirstOrDefault(o => o.Id != pid && w.AreAtWar(pid, o.Id))
               ?? w.Countries.Values.Where(o => o.Id != pid).OrderByDescending(o => VictoryPoints.Total(w, o.Id)).FirstOrDefault();
        string front = "sem ninguém do outro lado";
        if (foe is not null)
            front = $"{foe.Name} vale {VictoryPoints.Total(w, foe.Id)}, maiores praças "
                  + string.Join(", ", VictoryPoints.Prizes(w, foe.Id, 3).Select(pr => $"{pr.Name} {VictoryPoints.Of(w, pr)}"))
                  + $", temos-lhe {VictoryPoints.Taken(w, pid, foe.Id):P0}";
        var seat = w.Regions[w.Countries[pid].CapitalRegionId];
        int drawn = w.VictoryTiers.Values.Count(t => Glyph.Knows(t.Glyph));
        return $"{w.VictoryTiers.Count} graus ({spread}) somam {world} no mundo, {VictoryPoints.Held(w, pid)} na nossa mão;"
             + $" {front}; a capital: {VictoryPoints.Line(w, seat)} (mapa: {MapModes.Text(w, pid, seat, "victory")});"
             + $" {_map.Regions.Prizes()} praças marcadas no mapa; {drawn} de {w.VictoryTiers.Count} chapas desenhadas";
    }

    /// <summary>--smoke: a conta da rendição. Diz a dose da regra, onde vamos nós e onde vai quem nos faz
    /// guerra, prova que a conta é mesmo a mistura das duas medidas, e diz o caminho mais curto para o
    /// derrubar — as praças que faltam tomar-lhe. Sem tocar em ninguém: é tudo lido do mapa.</summary>
    private string SmokeCapitulation(int pid)
    {
        var w = _game.World;
        var us = w.Countries[pid];
        float weight = w.Rule("capitulate_weight_vp", 0f);
        var (myPop, myVp, myW) = Capitulation.Parts(w, us);
        string mine = $"nós {Capitulation.Progress(w, us):P0} de {Capitulation.Limit(w, us):P0}"
                    + $" (gente {myPop:P0} · praças {myVp:P0}, peso {myW:0.00})";

        var foe = w.Countries.Values.FirstOrDefault(o => o.Id != pid && w.AreAtWar(pid, o.Id));
        string theirs = "sem ninguém do outro lado";
        if (foe is not null)
        {
            var need = Capitulation.Needed(w, foe);
            string road = need.Count == 0 ? "cai já" : string.Join(", ", need.Take(3)
                .Select(r => $"{r.Name} {VictoryPoints.Of(w, r)}")) + (need.Count > 3 ? ", …" : "");
            var (fp, fv, fw) = Capitulation.Parts(w, foe);
            // a conta tem de ser a mistura das duas medidas, e não uma delas
            float mix = (1f - fw) * fp + fw * fv;
            theirs = $"{foe.Name} {Capitulation.Progress(w, foe):P0} de {Capitulation.Limit(w, foe):P0}"
                   + $" (gente {fp:P0} · praças {fv:P0} → {mix:P0}), capital {(Capitulation.CapitalLost(w, foe) ? "tomada" : "de pé")};"
                   + $" faltam {need.Count} regi{(need.Count == 1 ? "ão" : "ões")}: {road}";
        }
        return $"peso das praças {weight:0.00}; {mine}; {theirs}";
    }

    /// <summary>--smoke: os rios desenhados no mapa. Conta as regiões de rio do mundo, aproxima até a água
    /// acender e mede o que ficou desenhado — e afasta outra vez para provar que ao longe ela se apaga, que
    /// é a convenção do HoI4 (de longe o mapa é político). Diz por fim, numa margem a sério, que o traço
    /// azul e a conta do combate saem da mesma linha: rio nas duas margens, assalto mais caro nos dois
    /// sentidos e a frente mais apertada.</summary>
    private string SmokeRivers()
    {
        var w = _game.World;
        int total = w.Regions.Values.Count(r => r.River);
        if (total == 0) return "mundo sem rios";
        var seat = w.Regions.Values.FirstOrDefault(r => r.River && Rivers.Banks(w, r).Any(n => Rivers.Between(w, r.Id, n)))
                ?? w.Regions.Values.First(r => r.River);
        var eye = new Vector2(seat.CenterX, seat.CenterY);
        _map.Focus(eye, 1f);
        string near = _map.Regions.RiverReport();
        bool onNear = _map.Regions.RiversVisible;
        _map.Focus(eye, RegionRenderer.RiverZoomLimit / 2f);
        bool onFar = _map.Regions.RiversVisible;
        _map.Focus(eye, 0.2f);

        string pair = $"{seat.Name} sem outra margem de rio ao lado";
        if (Rivers.Banks(w, seat).FirstOrDefault(n => Rivers.Between(w, seat.Id, n)) is int other && other > 0
            && w.Regions.TryGetValue(other, out var o))
            pair = $"{seat.Name} ↔ {o.Name}: água no traço comum, assalto ×{BattleField.RiverBite(w, seat):0.00}"
                 + $" para lá e ×{BattleField.RiverBite(w, o):0.00} para cá, frente −{w.Rule("front_width_river", 1f):0}";
        return $"{total} de {w.Regions.Count} regiões com rio; {near}; de perto {(onNear ? "à vista" : "escondida")}"
             + $" e ao longe {(onFar ? "à vista" : "apagada")} (acende a {RegionRenderer.RiverZoomLimit:0.00} de zoom);"
             + $" {pair}; ficha: {Rivers.Line(w, seat)}";
    }

    /// <summary>--smoke: o chão desenhado no mapa político. Conta os sinais que ficaram e em que regiões,
    /// prova que o desenho de uma região é mesmo o da linha do terreno dela (o mapa não pode inventar chão
    /// nenhum) e que de longe se apaga, como a água. Volta ao zoom de sempre.</summary>
    private string SmokeGround()
    {
        var w = _game.World;
        var census = Relief.Census(w);
        if (census.Count == 0) return "mundo sem chão marcado";
        var seat = w.Regions.Values.First(r => Relief.Mark(w, r) == census[^1].Ground.Glyph);
        var eye = new Vector2(seat.CenterX, seat.CenterY);
        _map.Focus(eye, 1f);
        string near = _map.Regions.TerrainReport();
        bool onNear = _map.Regions.TerrainVisible;
        _map.Focus(eye, RegionRenderer.TerrainZoomLimit / 2f);
        bool onFar = _map.Regions.TerrainVisible;
        _map.Focus(eye, 0.2f);

        var flat = w.Regions.Values.FirstOrDefault(r => Relief.Mark(w, r) is null);
        return $"{near}; {seat.Name}: {census[^1].Ground.Name} com o sinal «{Relief.Mark(w, seat)}»"
             + $"; {(flat is null ? "sem chão liso" : $"{flat.Name} sem sinal (passo de sempre)")}"
             + $"; de perto {(onNear ? "à vista" : "escondido")} e ao longe {(onFar ? "à vista" : "apagado")}"
             + $" (acende a {RegionRenderer.TerrainZoomLimit:0.00} de zoom)";
    }

    /// <summary>--smoke: as cidades escritas no mapa. Conta o que a tabela traz, prova que de longe só se
    /// veem as capitais e as metrópoles e que de perto entra a terra pequena (sem nunca perder o que já se
    /// via), e mostra a linha que a ficha da região põe. Volta ao zoom de sempre.</summary>
    private string SmokeCities()
    {
        var w = _game.World;
        if (w.Cities.Count == 0) return "mundo sem cidades na tabela";
        var big = w.Cities[0];
        var eye = new Vector2(big.X, big.Y);

        _map.Focus(eye, 1f);
        string near = _map.Regions.CityReport();
        int atNear = Cities.Shown(w, 1f).Count;
        _map.Focus(eye, RegionRenderer.CityZoomLimit / 2f);
        bool onFar = _map.Regions.CitiesVisible;
        int atFar = Cities.Shown(w, RegionRenderer.CityZoomLimit).Count;
        _map.Focus(eye, 0.2f);

        var seat = w.Regions.GetValueOrDefault(big.RegionId);
        return $"{w.Cities.Count} na tabela ({w.Cities.Count(c => c.Capital)} capitais); a 1.0 de zoom {near}"
             + $"; a {RegionRenderer.CityZoomLimit:0.00} só {atFar} e ao longe {(onFar ? "à vista" : "apagadas")}"
             + $" ({atNear} de perto, corte de {Cities.Size((int)Cities.Cut(w, 1f))} gente)"
             + $"; maior: {big.Name} {Cities.Size(big.Population)} em {seat?.Name ?? "?"}"
             + $"; ficha: {(seat is null ? "sem região" : Cities.Line(w, seat))}";
    }

    /// <summary>--smoke: a mobília do mapa. Conta as chapas desenhadas de perto e de longe (o corte sobe ao
    /// afastar, mas a capital nunca cai), diz a forma de cada grau, prova o verde/cinzento/vermelho da
    /// relação e mostra a chapa de uma batalha se houver. Volta ao zoom de sempre.</summary>
    private string SmokeFurniture()
    {
        var w = _game.World;
        int pid = _game.PlayerId ?? 0;
        if (w.VictoryTiers.Count == 0) return "mundo sem graus de praça";
        var seat = w.Regions.GetValueOrDefault(w.Countries.TryGetValue(pid, out var me) ? me.CapitalRegionId : 0);
        var eye = seat is null ? Vector2.Zero : new Vector2(seat.CenterX, seat.CenterY);

        _map.Focus(eye, 1f);
        string near = _map.Regions.FurnitureReport();
        int atNear = MapMarks.Prizes(w, pid, 1f).Count;
        _map.Focus(eye, 0.2f);
        int atFar = MapMarks.Prizes(w, pid, 0.2f).Count;
        int atNone = MapMarks.Prizes(w, pid, w.Rule("mark_prize_zoom", 0.16f) / 2f).Count;

        string shapes = string.Join(", ", w.VictoryTiers.Values.OrderBy(t => t.Sort)
            .Select(t => $"{t.Name} {t.Shape}"));
        var mine = MapMarks.Prizes(w, pid, 1f).FirstOrDefault(p => p.Side > 0);
        var battle = BattleOdds.Shown(w, pid).FirstOrDefault();
        string fight = battle is null ? "sem batalha no mapa"
            : $"{w.Regions[battle.RegionId].Name} {BattleOdds.Line(w, battle, pid)}";

        return $"a 1.0 de zoom {near}; a 0.20 só {atFar} e ao longe nenhuma ({atNone}), de perto {atNear}"
             + $" (corte de {MapMarks.Cut(w, 0.2f)} para {MapMarks.Cut(w, 1f)} pontos)"
             + $"; formas: {shapes}"
             + $"; {(mine.RegionId == 0 ? "sem praça nossa" : $"{mine.Name}: {MapMarks.Line(w, pid, w.Regions[mine.RegionId])}")}"
             + $"; batalha: {fight}";
    }

    /// <summary>--smoke: o mundo a acontecer. Conta os eventos de estado e as sondas que os esperam, diz
    /// quais estão a dar hoje para quem joga, e prova o cartão: abre um à mão, lê o que ele mostra, mete-lhe
    /// outro em cima para provar a fila, e fecha os dois sem escolher nada.</summary>
    private string SmokeEvents()
    {
        var w = _game.World;
        int pid = _game.PlayerId ?? 0;
        if (!w.Countries.TryGetValue(pid, out var c)) return "sem país a jogar";
        var watch = w.NewsEvents.Values.Where(n => n.IsWatch).OrderBy(n => n.Id).ToList();
        if (watch.Count == 0) return "mundo sem eventos de estado";

        int probes = watch.Select(n => n.Watch).Distinct().Count();
        int unknown = watch.Count(n => !WorldWatch.Known(n.Watch));
        var live = watch.Where(n => WorldWatch.Fires(w, c, n.Watch, n.Arg)).ToList();
        string acesas = live.Count == 0 ? "nenhuma"
            : string.Join(", ", live.Take(3).Select(n => $"{n.Watch} ({WorldWatch.Line(w, c, n.Watch, n.Arg)})"));

        // e provar que caem mesmo: passa-se a semana de descanso à frente e corre-se o sistema uma vez
        float rest = w.Rule("event_watch_day", 7f);
        w.Rules["event_watch_day"] = 0f;
        new NewsSystem().Tick(w);
        w.Rules["event_watch_day"] = rest;
        string caidos = w.NewsFired.Count == 0 ? "nenhum"
            : string.Join(" · ", w.NewsFired.Keys.Take(3).Select(id => w.NewsEvents.TryGetValue(id, out var f) ? f.Title : id));

        var pick = watch.FirstOrDefault(n => w.NewsOptions.ContainsKey(n.Id)) ?? watch[0];
        var other = watch.FirstOrDefault(n => n.Id != pick.Id) ?? pick;
        _card.Show(pick.Id);
        int opts = w.NewsOptions.TryGetValue(pick.Id, out var list) ? list.Count : 0;
        string held = _card.Held ? "pára o relógio" : "não pára o relógio";
        _card.Show(other.Id);
        int queued = _card.Waiting;
        _card.Close(null);
        string then = _card.OpenId.Length > 0 && w.NewsEvents.TryGetValue(_card.OpenId, out var nx) ? nx.Title : "nada";
        _card.Close(null);

        return $"{watch.Count} eventos de estado em {probes} sondas ({WorldWatch.Keys.Length} conhecidas, "
             + $"{unknown} por conhecer), a dar hoje: {acesas}; {w.NewsFired.Count} caíram ({caidos}) "
             + $"depois da semana de descanso de {rest:0} dias; "
             + $"cartão «{pick.Title}» com {opts} escolha{(opts == 1 ? "" : "s")} e estampa {(pick.Glyph.Length > 0 ? pick.Glyph : "nenhuma")}, "
             + $"{held}, {queued} à espera, a seguir entra «{then}» e fecha em {(_card.Visible ? "aberto" : "nada")}";
    }

    /// <summary>--smoke: o armazém de material. Diz a folha das prateleiras, e depois prova o ciclo inteiro
    /// numa divisão nossa: leva-se uma pilha a meio material, enche-se o armazém do que o modelo dela pede,
    /// corre-se o EquipmentSystem uma vez e vê-se o material subir e as prateleiras descer. Prova também o
    /// estrangulamento (com um tipo a zero não sobe nada), o tecto que o material põe na resistência e o
    /// caixote de "por armar" desenhado no contador do mapa. No fim repõe-se tudo como estava.</summary>
    private string SmokeWarehouse(int pid)
    {
        var w = _game.World;
        if (!w.Countries.TryGetValue(pid, out var c)) return "sem país";
        string folha = StockView.Smoke(w, c);
        var mine = w.Divisions.Values.FirstOrDefault(d => d.HomeId == pid);
        if (mine is null) return folha + "; sem divisões nossas para armar";

        float keptKit = mine.Kit;
        var keptStock = new Dictionary<int, float>(c.Stock);
        var need = w.KitNeed(mine.TemplateId);
        mine.Kit = 0.5f;

        // estrangulamento: com o armazém vazio não se repõe nada, por muitos homens que a divisão tenha
        foreach (var (type, _) in need) c.Stock[type] = 0f;
        float vazio = EquipmentSystem.Refill(w, c, mine, 0.1f);

        // e com o armazém cheio do que aquele modelo pede, um dia de reposição
        foreach (var (type, qty) in need) c.Stock[type] = qty;
        float antes = need.Sum(n => c.Stocked(n.UnitTypeId));
        new EquipmentSystem().Tick(w);
        float depois = need.Sum(n => c.Stocked(n.UnitTypeId));
        float tecto = 100f * mine.Kit, forca = EquipmentSystem.PowerMult(w, mine);

        // o caixote no contador: aproxima-se o mapa da região onde a pilha está e conta-se quantas caixas o
        // desenham, ainda com o material em baixo
        int crates = 0; float worstDrawn = 1f;
        var where = w.Regions.Values.FirstOrDefault(r => r.DivisionIds.Contains(mine.Id));
        if (where is not null)
        {
            var eye = new Vector2(where.CenterX, where.CenterY);
            _map.Focus(eye, 1.2f);
            _map.Regions.Refresh();
            (crates, worstDrawn) = _map.Regions.PorArmar();
            _map.Focus(eye, 0.2f);
        }

        float reposto = mine.Kit - 0.5f;
        mine.Kit = keptKit;
        c.Stock.Clear();
        foreach (var (type, qty) in keptStock) c.Stock[type] = qty;
        if (where is not null) _map.Regions.Refresh();

        string depot = "sem fábricas livres";
        if (Warehouse.Depot(w, c) is (int dt, float dout))
        {
            string dn = dt.ToString();
            try { dn = w.Units.GetUnitType(dt).Name; } catch { /* tipo sem ficha */ }
            depot = $"{dn} +{dout:0.00}/dia";
        }

        return $"{folha}; {need.Count} tipos no modelo da pilha de prova, armazém vazio repõe {vazio:0.000},"
             + $" cheio repõe {reposto:0.000} e gasta {antes - depois:0.00} conjuntos"
             + $" (tecto da resistência {tecto:0}%, força ×{forca:0.00}, chão {w.Rule("kit_power_floor", 0.45f):0.00});"
             + $" {crates} contador{(crates == 1 ? "" : "es")} com caixote (pior {worstDrawn:P0});"
             + $" depósito: {depot}; marcas: {SmokeMarks(w, c, mine)}";
    }

    /// <summary>--smoke: as marcas do material. Conta as gerações da tabela, mostra a que o país tem aberta,
    /// abre-lhe a última à força e prova a cadeia inteira: a linha de produção reafina-se (e paga em ritmo),
    /// o conjunto passa a custar mais, e uma divisão com material novo bate-se com mais força do que a mesma
    /// divisão com material de origem. No fim repõe as tecnologias e a marca como estavam.</summary>
    private static string SmokeMarks(World w, Country c, Division mine)
    {
        int type = w.KitNeed(mine.TemplateId).FirstOrDefault().UnitTypeId;
        var list = Marks.All(w, type);
        if (list.Count == 0) return "sem tabela de marcas";
        string nome = type.ToString();
        try { nome = w.Units.GetUnitType(type).Name; } catch { /* tipo sem ficha */ }

        float abertaAntes = Marks.Open(w, c, type);
        var keptTechs = new HashSet<string>(c.Techs);
        float keptMark = mine.Mark;

        var linha = new ProductionOrder { UnitTypeId = type, Mark = abertaAntes, Efficiency = 1.4f };
        float custoAntes = w.OrderCost(linha);

        foreach (var m in list) if (m.TechId.Length > 0) c.Techs.Add(m.TechId);
        float abertaDepois = Marks.Open(w, c, type);
        var (marcaNova, ritmo) = Marks.Retool(w, c, type, linha.Mark, linha.Efficiency);
        linha.Mark = marcaNova;
        float custoDepois = w.OrderCost(linha);

        mine.Mark = list[0].Mark;  float forcaVelha = Marks.PowerMult(w, mine);
        mine.Mark = abertaDepois;  float forcaNova = Marks.PowerMult(w, mine);
        float desgaste = Marks.WearMult(w, mine);

        mine.Mark = keptMark;
        c.Techs.Clear(); foreach (var t in keptTechs) c.Techs.Add(t);

        return $"{w.EquipmentMarks.Count} na tabela, {list.Count} para {nome} "
             + $"({string.Join(" → ", list.Select(m => $"{Marks.Roman(m.Mark)} {m.Name}"))}); "
             + $"aberta {Marks.Describe(w, type, abertaAntes)}, com a investigação toda {Marks.Describe(w, type, abertaDepois)}; "
             + $"a linha reafina-se de {Marks.Roman((int)abertaAntes)} para {Marks.Roman((int)marcaNova)} "
             + $"(ritmo {linha.Efficiency:0.00} → {ritmo:0.00}, conjunto {custoAntes:0.00} → {custoDepois:0.00}); "
             + $"a divisão bate-se ×{forcaVelha:0.00} com a de origem e ×{forcaNova:0.00} com a última "
             + $"(desgaste ×{desgaste:0.00})";
    }

    /// <summary>--smoke: a rede — carris e depósitos. Diz quantas regiões do mundo têm via e de que nível,
    /// quantos troços o mapa desenha e a que aproximação aparecem, e depois prova as duas metades: assenta
    /// carril numa região nossa e vê-se o salto da rede ficar mais barato; levanta um depósito na região
    /// nossa mais funda e vê-se essa distância encolher pelo crédito que ele dá. No fim repõe-se tudo.</summary>
    private string SmokeRails(int pid)
    {
        var w = _game.World;
        if (!w.Countries.TryGetValue(pid, out _)) return "sem país";
        int max = (int)w.Rule("rail_max", 4f);
        var byLevel = new int[max + 1];
        int nossos = 0;
        foreach (var r in w.Regions.Values)
        {
            byLevel[Math.Clamp(r.Rail, 0, max)]++;
            if (r.ControllerId == pid && r.Rail > 0) nossos++;
        }

        // o mapa: os troços só se desenham a partir de RegionRenderer.RailZoomLimit, como os rios e o chão
        var eye = _map.VisibleWorldRect().GetCenter();
        _map.Focus(eye, RegionRenderer.RailZoomLimit + 0.05f);
        _map.Regions.Refresh();
        var rails = _map.Regions.Rails();
        _map.Focus(eye, 0.2f);
        _map.Regions.Refresh();

        // o carril: o mesmo salto da rede antes e depois de a via lá estar
        var mine = w.Regions.Values.FirstOrDefault(r => r.ControllerId == pid && r.OwnerId == pid && r.Rail < max);
        string via = "sem região nossa para assentar carril";
        if (mine is not null)
        {
            int kept = mine.Rail;
            float antes = SupplySystem.StepCost(w, mine);
            mine.Rail = max;
            float depois = SupplySystem.StepCost(w, mine);
            mine.Rail = kept;
            via = $"em {mine.Name} o salto da rede passava de {antes:0.00} a {depois:0.00} com carril {max}";
        }

        // o depósito: o crédito de rede que ele dá encurta a distância da região nossa mais funda
        var net = SupplySystem.Network(w, pid);
        string dep = "sem depósito na tabela";
        var hubDef = w.BuildingDefs.Values.FirstOrDefault(d => d.IsHub);
        var deepest = w.Regions.Values.Where(r => r.ControllerId == pid)
                       .OrderByDescending(r => SupplySystem.StepCost(w, r)).FirstOrDefault();
        if (hubDef is not null && deepest is not null)
        {
            int kept = deepest.Buildings.GetValueOrDefault(hubDef.Id);
            deepest.Buildings[hubDef.Id] = 1;
            var comHub = SupplySystem.Network(w, pid);
            if (kept > 0) deepest.Buildings[hubDef.Id] = kept; else deepest.Buildings.Remove(hubDef.Id);
            dep = $"{hubDef.Name} em {deepest.Name} levava os depósitos ligados de {net.Hubs} a {comHub.Hubs}"
                + $" e o crédito de {net.Credit:0.#} a {comHub.Credit:0.#} saltos";
        }

        return $"{nossos} regiões nossas com via (mundo por nível: {string.Join("/", byLevel)}),"
             + $" {rails.Tracks} troços no mapa (melhor nível {rails.Best}, visíveis {(rails.Visible ? "sim" : "não")}"
             + $" a partir de ×{RegionRenderer.RailZoomLimit:0.00}); {via}; rede de {net.Linked} regiões ligadas"
             + $" (a mais funda a {net.Deepest:0.0} saltos); {dep}";
    }

    /// <summary>--smoke: graus de veterania. Diz os degraus da tabela e o que cada um dá, conta as divisões
    /// do mundo por grau, e prova que o bónus que o combate soma é o do grau — a mesma Veterancy.Bonus nos
    /// dois sítios. Mede também os galões desenhados nos contadores do mapa.</summary>
    private string SmokeVeterancy(int pid)
    {
        var w = _game.World;
        if (w.VeterancyTiers.Count == 0) return "sem graus de veterania carregados";
        string spread = string.Join(", ", w.VeterancyTiers.Values.OrderBy(t => t.Sort)
            .Select(t => $"{t.Icon} {t.Name} ≥{t.MinXp:0} +{t.Bonus:P0} {t.Chevrons} {(t.Chevrons == 1 ? "galão" : "galões")}"));
        var census = new Dictionary<string, int>();
        foreach (var d in w.Divisions.Values)
            if (Veterancy.Tier(w, d) is VeterancyDef t) census[t.Id] = census.GetValueOrDefault(t.Id) + 1;
        string world = string.Join(", ", w.VeterancyTiers.Values.OrderBy(t => t.Sort)
            .Select(t => $"{t.Name} {census.GetValueOrDefault(t.Id)}"));

        // uma divisão nossa levada a cada degrau: o grau muda e o bónus é o do grau, sem nada de guardado
        var mine = w.Divisions.Values.FirstOrDefault(d => d.CountryId == pid);
        string ladder = "sem divisões nossas";
        if (mine is not null)
        {
            float keep = mine.Xp;
            ladder = string.Join(" → ", w.VeterancyTiers.Values.OrderBy(t => t.Sort).Select(t =>
            {
                mine.Xp = t.MinXp;
                return $"{t.MinXp:0} XP {Veterancy.Tier(w, mine)!.Name} +{Veterancy.Bonus(w, mine):P0}";
            }));
            mine.Xp = keep;
        }
        int drawn = w.VeterancyTiers.Values.Count(t => Glyph.Knows(t.Glyph));

        // os galões no mapa: num mundo em paz a tropa é toda verde e não havia riscos nenhuns para contar.
        // Sobe-se a experiência de uma pilha nossa ao grau do topo, aproxima-se o mapa até as caixas serem
        // desenhadas, contam-se os riscos — e repõe-se a experiência e a vista como estavam.
        int marked = 0, chevrons = 0;
        var stackRegion = w.Regions.Values.FirstOrDefault(r => r.DivisionIds
            .Any(id => w.Divisions.TryGetValue(id, out var sd) && sd.CountryId == pid));
        if (stackRegion is not null)
        {
            var stack = stackRegion.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>()
                                   .Where(sd => sd.CountryId == pid).ToList();
            var kept = stack.Select(sd => sd.Xp).ToList();
            var top = w.VeterancyTiers.Values.OrderByDescending(t => t.MinXp).First();
            foreach (var sd in stack) sd.Xp = top.MinXp;
            var eye = new Vector2(stackRegion.CenterX, stackRegion.CenterY);
            _map.Focus(eye, 1.2f);
            _map.Regions.Refresh();
            (marked, chevrons) = _map.Regions.Galoes();
            for (int i = 0; i < stack.Count; i++) stack[i].Xp = kept[i];
            _map.Regions.Refresh();
            _map.Focus(eye, 0.2f);
        }
        return $"{w.VeterancyTiers.Count} graus ({spread}); no mundo {world};"
             + $" a nossa tropa a subir: {ladder}; ficha: {(mine is null ? "—" : Veterancy.Line(w, mine))};"
             + $" {marked} contador{(marked == 1 ? "" : "es")} com galões ({chevrons} riscos) com a pilha no topo;"
             + $" {drawn} de {w.VeterancyTiers.Count} chapas desenhadas";
    }

    /// <summary>--smoke: estados-fantoche. Diz os degraus que a tabela traz e o que cada um cobra, quantos
    /// países já obedecem a alguém, e — sem tocar em ninguém — o que renderia curvar hoje o país com quem
    /// estamos em guerra: em que degrau entraria, quanto pagaria por dia e se a pressão de hoje já chega.</summary>
    private string SmokeSubjects(int pid)
    {
        var w = _game.World;
        if (w.SubjectTypeDefs.Count == 0) return "sem vassalagem carregada";
        string steps = string.Join(", ", w.SubjectTypeDefs.Values.OrderBy(t => t.AutonomyMin).ThenBy(t => t.Sort)
            .Select(t => $"{t.Icon} {t.Name} {t.YieldShare * 100f:0}%/{t.ManpowerShare * 100f:0}%"));
        int bound = w.Countries.Values.Count(c => c.IsSubject);
        int drawn = w.SubjectTypeDefs.Values.Count(t => Glyph.Knows(t.Glyph));

        // um alvo de verdade para a conta: quem nos faz guerra, e na falta de guerra o vizinho do lado
        var mark = w.Countries.Values.FirstOrDefault(o => o.Id != pid && w.AreAtWar(pid, o.Id))
                ?? w.Countries.Values.Where(o => o.Id != pid && !o.IsSubject).OrderBy(o => o.Id).FirstOrDefault();
        string hypothesis = "sem país a quem propor";
        if (mark is not null && Subjects.Deepest(w) is SubjectTypeDef step)
        {
            var v = PeaceTerms.PuppetVerdict(w, pid, mark.Id);
            hypothesis = $"{mark.Name} entraria como {step.Name} por {Subjects.Tribute(w, mark, step):0.0}/dia"
                       + $" e {Subjects.Levy(w, mark, step):0} homens/dia"
                       + $" (pressão {v.Pressure:0.00} contra preço {v.Price:0.00}: {(v.Accepted ? "curva-se" : "ainda não")})";
        }
        var seat = w.Regions[w.Countries[pid].CapitalRegionId];
        return $"{w.SubjectTypeDefs.Count} degraus ({steps}), {bound} vassalo{(bound == 1 ? "" : "s")} no mundo;"
             + $" {hypothesis}; mapa: {MapModes.Text(w, pid, seat, "subject")}; {drawn} de {w.SubjectTypeDefs.Count} chapas desenhadas";
    }

    /// <summary>--smoke: tropas especiais de terreno. Conta as marcas da tabela unit_tag no mundo inteiro,
    /// diz quantos modelos nossos as têm e prova, com o motor de modificadores e não com uma segunda conta,
    /// o que a marca vale no chão dela — mais o símbolo que se desenha por cima do NATO.</summary>
    private string SmokeSpecialForces(int pid)
    {
        var w = _game.World;
        // quantas divisões do mundo têm cada marca: é a tabela que manda, o código só as conta
        var world = new Dictionary<string, int>();
        foreach (var d in w.Divisions.Values)
        {
            string s = UnitSymbol.SpecFor(w, d.TemplateId);
            if (s.Length > 0) world[s] = world.GetValueOrDefault(s) + 1;
        }
        IReadOnlyList<DivisionTemplate> mine;
        try { mine = w.Units.GetTemplates(pid); } catch { mine = Array.Empty<DivisionTemplate>(); }
        var ours = mine.Select(t => (t, spec: UnitSymbol.SpecFor(w, t.Id))).Where(x => x.spec.Length > 0).ToList();
        string marks = world.Count == 0 ? "nenhuma no mundo"
            : string.Join(", ", world.OrderBy(kv => kv.Key)
                .Select(kv => $"{NatoSymbol.SpecMark(kv.Key)} {NatoSymbol.SpecName(kv.Key)} {kv.Value}"));

        // o que a marca vale onde ela sabe bater-se: a mesma pergunta que o combate faz à tabela. O par sai
        // dos nossos modelos se os tivermos, e do mundo se não — o país do jogador pode não ter alpinos e a
        // prova continua a fazer-se com tropa a sério, não com uma inventada aqui
        var hill = w.Regions.Values.FirstOrDefault(r => r.Terrain == "mountain") ?? w.Regions.Values.First();
        var ctx = CombatSystem.BuildContext(w, hill, pid);
        float Str(int tid) { var (f, m) = w.Modifiers.Evaluate("str_attacker", w.Stats.Get(tid), ctx); return m + f; }
        var seen = w.Divisions.Values.Select(d => d.TemplateId).Concat(mine.Select(t => t.Id)).Distinct().ToList();
        int Pick(bool special) => seen.FirstOrDefault(t => (UnitSymbol.SpecFor(w, t) == "montanha") == special);
        string TName(int tid) { try { return w.Units.GetTemplate(tid).Name; } catch { return "T" + tid; } }
        int alp = Pick(true), plain = Pick(false);
        string worth = alp == 0 || plain == 0
            ? $"sem par para comparar em {hill.Terrain}"
            : $"em {hill.Terrain} {TName(alp)} bate a {Str(alp):P0} e {TName(plain)} a {Str(plain):P0}";

        // e o desenho: o símbolo NATO com a marca por cima, como no contador do mapa
        var sym = UnitSymbol.Of("infantry", 30f, 22f, ours.Count > 0 ? ours[0].spec : "montanha");
        AddChild(sym); sym.QueueRedraw();
        string drawn = $"símbolo {sym.Kind}+{sym.Spec} ({sym.TooltipText})";
        sym.QueueFree();
        return $"{marks}; {ours.Count} modelo{(ours.Count == 1 ? "" : "s")} nosso{(ours.Count == 1 ? "" : "s")} com marca; {worth}; {drawn}";
    }

    /// <summary>--smoke: força uma bolsa numa divisão nossa para a chapa ⛓ e o aviso do cerco serem
    /// desenhados mesmo num mundo em paz, e repõe tudo como estava.</summary>
    private string SmokePocket(int pid)
    {
        var w = _game.World;
        var d = w.Divisions.Values.FirstOrDefault(x => x.CountryId == pid);
        if (d is null) return "sem divisões";
        bool cut = d.Cut; int had = d.PocketDays;
        d.Cut = true; d.PocketDays = (int)w.Rule("pocket_grace", 3f) + 1;
        RefreshTop();
        string plate = _pocketPlate.Visible ? $"{_pocket.Text} ({_pocketNote.Text})" : "chapa escondida";
        int warned = Alerts.For(w, pid).Count(a => a.Id == "pocket");
        // e o caldeirão no mapa: com a bolsa fingida em pé, as riscas e o anel têm de aparecer mesmo
        var drawn = _map.Pockets.Smoke();
        var pots = Pockets.All(w);
        string caldeirao = pots.Count == 0 ? "nenhum caldeirão" : Pockets.Short(w, pots[0]);
        d.Cut = cut; d.PocketDays = had;
        RefreshTop();
        _map.Pockets.Refresh();
        return $"{plate}, {warned} aviso na faixa, chapa depois de desfeito o cerco: {(_pocketPlate.Visible ? "à vista" : "escondida")}"
             + $"; no mapa {drawn.Pockets} bolsa{(drawn.Pockets == 1 ? "" : "s")} com {drawn.Divisions} divis{(drawn.Divisions == 1 ? "ão" : "ões")}"
             + $" ({drawn.Bands} riscas, {drawn.Ring} troços de anel, {drawn.Tags} chapa{(drawn.Tags == 1 ? "" : "s")}), a pior: {caldeirao}"
             + $"; depois de desfeito: {_map.Pockets.Smoke().Pockets} no mapa";
    }

    /// <summary>A chapa do cerco: quantas divisões nossas estão em bolsa e quanto tempo falta à pior delas.
    /// Some-se quando não há nenhuma — a barra não guarda espaço para desgraças que não existem.</summary>
    private void Encircled(World w, int pid)
    {
        var pocketed = PocketSystem.Of(w, pid);
        _pocketPlate.Visible = pocketed.Count > 0;
        if (pocketed.Count == 0)
        {
            _worstPocketRegion = 0;
            // A chapa está escondida, mas a explicação fica escrita: quem a vir aparecer a meio de uma
            // campanha tem de saber ao primeiro toque o que é um cerco e o que lhe acontece se o ignorar.
            _pocketPlate.TooltipText = "Divisões cercadas: nenhuma.\n"
                + $"· uma divisão sem ligação a casa rende-se ao fim de {w.Rule("pocket_surrender", 10f):0} dias fechada"
                + "\nA chapa aparece sozinha no dia em que houver tropa nossa cortada.";
            return;
        }
        var worst = pocketed[0];
        // a bolsa a que a pior divisão pertence: é ela que o mapa desenha e é o coração dela que o toque
        // procura — a região com mais tropa, e não a divisão solta que por acaso leva mais dias
        var pots = Pockets.Of(w, pid);
        var pot = pots.FirstOrDefault(x => x.DivisionIds.Contains(worst.Id));
        _worstPocketRegion = pot.Regions > 0 ? pot.HeartId : worst.RegionId;
        _pocket.Text = pocketed.Count.ToString();
        _pocketNote.Text = PocketSystem.DaysToSurrender(w, worst) is int days
            ? days <= 0 ? "rende-se hoje" : days == 1 ? "rende-se amanhã" : $"rende-se em {days} d"
            : pocketed.Count == 1 ? "cercada" : "cercadas";
        _pocketPlate.TooltipText = $"Divisões cercadas: {pocketed.Count}"
            + (pots.Count > 0 ? $" em {pots.Count} bolsa{(pots.Count == 1 ? "" : "s")}" : "") + ".\n"
            + $"· a pior está em {w.Regions[worst.RegionId].Name}, fechada há {worst.PocketDays} "
            + (worst.PocketDays == 1 ? "dia" : "dias")
            + (pot.Regions > 0 ? $"\n· o caldeirão fechou {pot.Regions} regi{(pot.Regions == 1 ? "ão" : "ões")}"
                               + $" e {pot.Vp} ponto{(pot.Vp == 1 ? "" : "s")} de vitória lá dentro" : "")
            + $"\n· rende-se ao fim de {w.Rule("pocket_surrender", 10f):0} dias fechada"
            + "\nSem ligação a casa não chega abastecimento: as armas baixam e a organização não recupera."
            + "\nO mapa desenha a bolsa às riscas, com o anel por fora. Toque para lá ir.";
    }

    /// <summary>Toque na chapa do cerco: o mapa vai à bolsa pior e abre-lhe a ficha.</summary>
    private void ShowWorstPocket()
    {
        if (_worstPocketRegion > 0) ShowRegion(_worstPocketRegion);
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

    /// <summary>Toque simples: nunca tapa o jogo. Mostra a ficha breve no rodapé, e ou entrega o toque à
    /// obra armada (menu Construir), ou troca a selecção (a ArmySelect ignora-o se a região não é nossa) —
    /// salvo se o mesmo gesto já foi tratado como duplo toque (_suppressTapSelect, posto pelo MapView, que
    /// manda primeiro o sinal de duplo toque e só depois o simples).</summary>
    private void OnRegionTapped(int regionId)
    {
        try
        {
            _footer.Show(regionId);
            if (_buildBar.Armed) { _buildBar.HandleTap(regionId); return; }
            if (_suppressTapSelect) { _suppressTapSelect = false; return; }
            _multiSel.Tap(regionId);
        }
        catch (Exception ex) { GD.PushError("Hud.OnRegionTapped: " + ex); }
    }

    /// <summary>Duplo toque: com selecção activa, é o destino da marcha (a ordem parte, a marca e a rota
    /// ficam); sem selecção, abre a ficha completa da região. Marca _suppressTapSelect porque o MapView
    /// emite este sinal antes do toque simples do mesmo gesto — sem a marca, o toque simples a seguir trocava
    /// a selecção antes de MoveTo a poder usar.</summary>
    private void OnRegionDoubleTapped(int regionId)
    {
        try
        {
            _suppressTapSelect = true;
            if (_multiSel.Active) _multiSel.DoubleTap(regionId);
            else { _footer.Show(regionId); _region.Open(regionId); }
        }
        catch (Exception ex) { GD.PushError("Hud.OnRegionDoubleTapped: " + ex); }
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
        _multiSel.Tap(from.Id);                                           // marca-se aqui: a rota lê a ArmySelect
        return _map.Routes.Smoke();
    }

    // --smoke: abre os painéis e dá uma ordem de movimento para os caminhos de código correrem sem ecrã.
    private void Smoke()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c) || !w.Regions.TryGetValue(c.CapitalRegionId, out var cap)) return;
        OnRegionTapped(cap.Id);   // toque simples: rodapé pintado, capital marcada na ArmySelect (tem divisões)
        GD.Print($"smoke: {cap.DivisionIds.Count} divisões na capital, {cap.Neighbours.Count} vizinhos, "
               + $"{_map.Regions.FrontierLines()} tiras de fronteira");
        if (cap.Neighbours.FirstOrDefault(n => w.Regions.TryGetValue(n, out var nr) && nr.ControllerId == pid) is int own && own != 0) _multiSel.MoveTo(own);
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
        int worldTabs = _worldPanel.SmokeTabs();                       // e as abas de metal, secção a secção
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
        string combatSheet = DivisionView.SmokeSheet(w, pid);           // os números com que a tropa bate, na ficha da divisão
        int landTabs = _countryPanel.SmokeTabs();                       // e as quatro abas de metal, secção a secção
        int chartDay = _countryPanel.SmokeChart(); _countryPanel.Close();  // e o gráfico na nota de potência, com mira
        // --smoke: ao dia 6 a campanha ainda não tem história nenhuma, e uma crónica vazia não desenha
        // chapa nenhuma. Escreve-se uma linha de cada género que pese o bastante para entrar, e assim o
        // carril e os filtros têm mesmo de desenhar as chapas todas que a tabela pede.
        foreach (var kind in w.ChronicleKinds.Values.OrderBy(k => k.Id))
            ChronicleSystem.Write(w, kind.Id, $"{kind.Name}: linha de prova do --smoke.", pid);
        int cron = _journal.Smoke();                                    // painel Crónica: linha do tempo e filtros
        var cronPlates = Glyph.Count(_journal); _journal.Close();       // e as chapas dos géneros, na marca e nos filtros
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
            _region.Open(rear.Id);                                      // painel da região inimiga, com o cartão novo
            _region.Open(cap.Id);                                       // e de volta à capital: cartão da nossa retaguarda
        }
        // ficha do chão: abre-se de propósito numa região de terreno castigador (ou com rio), que é onde os
        // números do terreno deixam de ser todos 1,00 — na planície a ficha não prova nada
        var hard = w.Regions.Values.FirstOrDefault(r => r.ControllerId == pid && (r.River || r.Terrain != "plain"))
                ?? w.Regions.Values.FirstOrDefault(r => r.River || r.Terrain != "plain") ?? cap;
        string groundSheet = GroundView.Smoke(w, hard, pid);
        // ficha do que a terra dá: escolhe-se uma região com depósitos, que é onde as parcelas passam das
        // duas de sempre (dinheiro e homens) e a fila de chapas tem alguma coisa a provar
        var rich = w.Regions.Values.FirstOrDefault(r => r.ControllerId == pid && r.Resources.Count > 0)
                ?? w.Regions.Values.FirstOrDefault(r => r.Resources.Count > 0) ?? cap;
        string yieldSheet = YieldView.Smoke(w, rich);
        // ficha de como a terra está: escolhe-se uma região com forte, cais, obra a andar ou estrada partida
        // — numa terra em paz e inteira a ficha são duas parcelas e não prova a grelha
        var busyLand = w.Regions.Values.FirstOrDefault(r => r.ControllerId == pid
                            && (r.Fort > 0 || r.Building || r.FortBuilding || r.Project is not null
                                || r.Infrastructure < r.BaseInfrastructure - 1e-4f || RegionState.PortLevels(w, r) > 0))
                    ?? cap;
        string stateSheet = StateView.Smoke(w, busyLand, pid, _map.Regions.Mode);
        // ficha de como o país está: a mesma grelha, um degrau acima
        string nationSheet = NationView.Smoke(w, c);
        // saldo da guerra: as chapas dos dois lados na aba das Guerras do painel Mundo
        string warSheet = WarLedgerView.Smoke(w);
        _region.Open(busyLand.Id);
        _region.Open(rich.Id);
        _region.Open(hard.Id);
        _region.Open(cap.Id);
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
        var techPlates = Glyph.Count(techTree);                          // chapas do ramo, desenhadas em vez de emoji
        techTree.Free();                                                 // órfão: nunca entrou na árvore de cena
        // folha de comparação: nós contra o vizinho, pelas três abas
        int rival = smokeFoe != 0 ? smokeFoe : w.Countries.Values.FirstOrDefault(x => x.Id != pid)?.Id ?? pid;
        int cmp = _compare.Smoke(rival); _compare.Close();
        var ind = Industry.Of(w, pid);                                   // fábricas: os mostradores e a bancada
        _production.Open();
        string queue = _production.Smoke();                              // fila de produção: chapas arrastáveis
        string plan = PlanView.Smoke(w, c);                              // porque é que a encomenda da frente demora
        _production.Close();
        string armazem = SmokeWarehouse(pid);                            // armazém: as prateleiras, a reposição e o caixote do mapa
        string carris = SmokeRails(pid);                                 // rede: os carris do mapa e os depósitos que a levam atrás da ofensiva
        int modes = _modeBar.Smoke();                                    // modos de mapa: pinta o mundo por cada conta e volta ao político
        var modePlates = Glyph.Count(_modeBar);                          // e as chapas da fita, desenhadas em vez de emoji
        string classes = _modeBar.SmokeClasses();                        // e o modo que pinta por classe: cor de tabela e chave desenhada
        string fight = _battle.Smoke(cap.Id);                               // ecrã de batalha: os dois lados, linha e reserva
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
            if (NavalMissionSystem.Free(w, pid) < 1f) Navy.Buy(w, c, Navy.Basic(w));
            float ships = MathF.Max(w.Rule("naval_mission_min_ships", 1f), MathF.Floor(NavalMissionSystem.Free(w, pid)));
            var sail = new AssignNavalMissionCommand(pid, seaTarget, "bloqueio", ships);
            sea = sail.Validate(w) is string no ? no
                : _game.Dispatch(sail) ?? $"{ships:0.#} navio{(ships == 1f ? "" : "s")} ao largo de {w.Regions[seaTarget].Name}";
        }
        // zonas estratégicas: o quadro do céu e do mar do mundo, que é onde a guerra do ar e a do mar se
        // decidem desde que deixaram de ser província a província
        string zonas = Zones.Short(w, pid);
        string marinha = Navy.Short(w, pid);                              // a marinha por classes: cascos, composição e peso
        // aviação por modelos: encomenda-se o melhor caça do hangar para a folha provar a escolha do modelo e
        // não só a contagem de asas
        string fighter = Air.Choose(w, float.MaxValue, "superiority");
        // --smoke: se o cofre não deu a compra, dá-se o caça à mão — a prova é o modelo, não o preço
        if (fighter.Length > 0 && _game.Dispatch(new BuyPlaneCommand(pid, fighter)) is not null) Air.Buy(w, c, fighter);
        string aviacao = Air.Short(w, pid);                               // a aviação por modelos: asas, composição e peso
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
        // empréstimo de material: arrasta-se a régua de latão até meio e assina-se a torneira ao
        // primeiro país que não seja inimigo, para a secção nova do painel da Guerra correr inteira
        string lend = _warPanel.SmokeMaterial();
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
            _countryPanel.Open(people);                                   // cartão da ocupação desenhado
            var occPlates = _countryPanel.SmokePlates();                  // com as chapas das políticas por onde escolher
            _countryPanel.Close();
            occ += $", {occPlates.Drawn} chapas no cartão ({occPlates.FellBack} na roda)";
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
        string bar = SmokeTopBar();                                       // barra de topo medida, chapa a chapa
        string pocket = SmokePocket(pid);                                 // e o cerco: chapa ⛓ e aviso, com bolsa fingida
        string footer = _footer.Smoke(cap.Id);                            // rodapé passivo: nome, tempo e obra da capital
        // Um estrangeiro qualquer com quem já haja alguma coisa em cima da mesa: prefere-se um vizinho, e
        // na falta dele o país com mais divisões. Sobre a nossa própria ficha a folha de acções não aparece.
        int foreignId = w.Regions.Values
            .Where(rg => rg.ControllerId == pid)
            .SelectMany(rg => rg.Neighbours)
            .Select(n => w.Regions.TryGetValue(n, out var o) ? o.ControllerId : 0)
            .FirstOrDefault(id => id > 0 && id != pid);
        if (foreignId == 0)
            foreignId = w.Divisions.Values.Where(d => d.CountryId != pid)
                .GroupBy(d => d.CountryId).OrderByDescending(g => g.Count())
                .Select(g => g.Key).FirstOrDefault();
        string sheet = foreignId > 0 ? _countryPanel.SmokeDiplomacy(foreignId) : "sem estrangeiro para a folha";
                string picker = _countrySelect.Smoke();                            // lista de países: filtros e procura
        string build = _buildBar.Smoke();                                 // menu Construir: tipos armados e desarmados
        string obra = BuildView.Smoke(w, pid);                            // a conta de uma obra, parcela a parcela
        string fuel = SmokeFuel(pid);                                     // combustível: depósito, mostrador e o que a seca custa
        string volunteers = SmokeVolunteers(pid);                         // voluntários: quantos lá fora, no mundo, e o que custa ir
        string exile = SmokeExile(pid);                                      // governos no exílio: os do mundo, não só o nosso
        string politica = SmokePolitics(pid);                             // poder político e tensão mundial: as duas moedas e o termómetro
        string jump = SmokeParadrop(pid);                                 // salto de pára-quedas: quem salta, para onde e a que custo
        string invasao = SmokeInvasion(pid);                              // operação anfíbia: marcar a praia, preparar e largar
        string campos = SmokeAirBases(pid);                               // o chão do céu: campos, camas e alcance em km reais
        string conves = SmokeCarriers(pid);                               // e o chão que flutua: porta-aviões e ataque naval
        string mergulho = SmokeSubs(pid);                                 // guerra submarina: o que se esconde e quem o vai buscar
        string oficina = _warPanel.SmokeShop();                           // oficina de aviões: fuselagem, ranhuras, peças e o desenho assinado
        string guerraMapa = _map.WarMarks.Smoke();                        // contadores do ar e do mar por cima do mapa
        string reach = SmokeSupplyReach(pid);                             // alcance da rede: até onde a linha aguenta sem afinar
        string stripes = _map.Regions.SmokeStripes(pid);                  // riscas de ocupação: a cor do dono por cima da do ocupante
        string rios = SmokeRivers();                                      // rios: a água desenhada no mapa e o que ela cobra a quem assalta
        string chao = SmokeGround();                                      // chão: serras, cidades, dunas e mata desenhadas por cima do mapa político
        string cidades = SmokeCities();                                   // cidades: os nomes de terra escritos no mapa, por escala
        string chapas = SmokeFurniture();                                 // chapas: a mobília do mapa — praças, obras e batalhas
        string eventos = SmokeEvents();                                   // acontecimentos: as sondas do mundo e o cartão de ecrã inteiro
        string special = SmokeSpecialForces(pid);                         // tropas especiais: a marca de terreno é da unidade
        string rail = SmokeRedeploy(pid);                                 // redespacho: a retaguarda atravessa-se de comboio
        string tempo = SmokeWeather(pid);                                 // tempo local: o céu muda de região para região
        string tacticas = SmokeTactics(pid);                              // tácticas: o que cada lado tenta, e quem lê quem
        string vassalos = SmokeSubjects(pid);                             // vassalagem: os degraus, quem obedece e o que renderia curvar alguém
        string vitoria = SmokeVictory(pid);                               // pontos de vitória: o que cada praça vale e quanto do inimigo já é nosso
        string veterania = SmokeVeterancy(pid);                           // veterania: os degraus, quem está em cada um e os galões do mapa
        string rendicao = SmokeCapitulation(pid);                         // rendição: quanto falta a cada lado para cair, e por onde
        GD.Print($"smoke: painéis abertos na capital {cap.Name}, {world} linhas no painel Mundo em {worldTabs} abas, {served} na folha de serviço ({combatSheet}), medalheiro {caseWho} com {ribbons} fitas em {plates} chapas ({decorated} divis{(decorated == 1 ? "ão" : "ões")} condecorada{(decorated == 1 ? "" : "s")}), estação {w.Season?.Name ?? "nenhuma"}, {cron} na crónica ({cronPlates.Drawn} chapas desenhadas das quais {cronPlates.FellBack} na roda), {hurt} na enfermaria em {hurtArms} arma{(hurtArms == 1 ? "" : "s")} (gravidades por arma: {wounds}), estado-maior de {c.Generals.Count} em {staffArms} por arma (de casa: {ourGeneral}; postos {staffRanks}; quadro de {rungs} degraus, {ownArms} escada{(ownArms == 1 ? "" : "s")} de casa), {PrisonerView.Short(pris)} prisioneiros, cais para {c.PortCapacity:0} divisões, {sab} alvo{(sab == 1 ? "" : "s")} de sabotagem, retaguarda da capital {CounterIntelSystem.Chance(w, pid, cap):P0}/dia, troca de {PrisonerView.Short(swap)} prisioneiros, {posted} proposta{(posted == 1 ? "" : "s")} do inimigo, cedência de {ceded}, potência ao dia {chartDay}, {fogged} regiões no nevoeiro ({fogWhy}), {alarms} alarme{(alarms == 1 ? "" : "s")} na faixa, investigação em {busy}/{labs} ranhuras ({techCards} fichas em {techBranches} ramos, {techHome} de casa, {techPlates.Drawn} chapas desenhadas das quais {techPlates.FellBack} na roda), folha de comparação com {cmp} linhas, fábricas {ind.CivilBusy}/{ind.Civil} civis e {ind.MilitaryBusy}/{ind.Military} militares, {modes} modos de mapa (agora {_map.Regions.Mode}, {modePlates.Drawn} chapas desenhadas das quais {modePlates.FellBack} na roda; {classes}), ecrã de batalha: {fight}, {tree} focos na árvore, {plans} seta{(plans == 1 ? "" : "s")} de plano no mapa, rota da tropa escolhida: {route}, escolas de guerra: {schools}, medalhas na barra: {medals}, adido {attache} ({hosts} anfitri{(hosts == 1 ? "ão" : "ões")} possíve{(hosts == 1 ? "l" : "is")}), fita de velocidade em {speed} (andamentos {string.Join("/", Game.SpeedPace.Skip(1))}), interface {screen}, actualização: {update}, {counters} contadores no mapa (trincheira média {dug:0.0}), fundo do mapa: {_map.Regions.BackdropReport()}, riscas: {stripes}, rios: {rios}, chão: {chao}, cidades: {cidades}, chapas: {chapas}, acontecimentos: {eventos}, tropas especiais: {special}, redespacho: {rail}, tempo: {tempo}, tácticas: {tacticas}, vassalagem: {vassalos}, vitória: {vitoria}, veterania: {veterania}, rendição: {rendicao}, tratado de {trade}, {_frames} painéis com moldura de metal ({PanelGrain.Report(this)}), guerra aérea: {air} ({w.AirMissions.Count} miss{(w.AirMissions.Count == 1 ? "ão" : "ões")} no mundo, ficha de {wingName}), guerra naval: {sea} ({w.NavalMissions.Count} esquadra{(w.NavalMissions.Count == 1 ? "" : "s")} no mundo, ficha de {fleetName}; fundo de {homePool} nomes), zonas: {zonas}, marinha: {marinha}, aviação: {aviacao}, campos: {campos}, porta-aviões: {conves}, submarinos: {mergulho}, oficina: {oficina}, guerra no mapa: {guerraMapa}, {names} nomes de país curvados no mapa ({glyphs} letras), comboios: {convoy} ({ConvoySystem.Available(w, pid):0} mercantes, {ConvoySystem.SupplyNeed(w, pid) + ConvoySystem.TradeNeed(w, pid):0} ocupados, {ConvoySystem.GroundedCount(w, pid)} parados), invasão: {invasao}, {metalTabs} abas de metal no painel da Guerra, ocupação: {occ}, {lanes.Lanes} rota{(lanes.Lanes == 1 ? "" : "s")} de comboio no mapa ({lanes.Cut} cortada{(lanes.Cut == 1 ? "" : "s")}), painel do País em {landTabs} abas, {spoils}, {gov}, {laws}, {queue}, {plan}, armazém: {armazem}, carris: {carris}, klaxon: {klaxon}, som: {sound}, {theatres.Count} teatro{(theatres.Count == 1 ? "" : "s")} de operações ({line.Edges} contactos na linha da frente cosidos em {line.Strands} fio{(line.Strands == 1 ? "" : "s")}, {line.Holes} troço{(line.Holes == 1 ? "" : "s")} sem tropa, guarnição {(theatres.Count == 0 ? 0f : theatres.Average(t => t.Coverage)):P0}), barra de topo: {bar}, {groundSheet}, {yieldSheet}, {stateSheet}, {nationSheet}, {warSheet}, política: {politica}, cerco: {pocket}, alcance: {reach}, combustível: {fuel}, voluntários: {volunteers}, exílio: {exile}, salto: {jump}, material: {lend}, rodapé: {footer}, construir: {build}, {obra}, {picker}, {sheet}, {MetalButton.Report(this)}, {Skin.Report(this, Ui.Theme())}, {_tips.Smoke()}");
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
        // O gesto que estava partido: com tropas marcadas num sítio, o duplo toque numa região que TAMBÉM tem
        // tropas nossas tem de mandar marchar. O primeiro toque do par rouba a marca para o destino (o Tap
        // troca-a em qualquer região com divisões nossas) e, sem a reposição da ArmySelect, a ordem morria em
        // silêncio. Prova-se com duas regiões nossas ligadas por caminho.
        var mineRegions = w.Regions.Values.Where(r => r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var md) && md.CountryId == pid)).ToList();
        var src = mineRegions.FirstOrDefault();
        var dst = src is null ? null : mineRegions.FirstOrDefault(r => r.Id != src.Id && MoveDivisionCommand.FindPath(w, src.Id, r.Id, pid) is not null);
        if (src is not null && dst is not null)
        {
            var movers = src.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var md) && md.CountryId == pid && !w.InBattle(id)).ToList();
            foreach (var id in movers) _game.Dispatch(new StopDivisionCommand(pid, id));   // apaga rotas de ordens anteriores do smoke
            _multiSel.Clear();
            _multiSel.Tap(src.Id);                  // marca a origem
            _multiSel.Tap(dst.Id);                  // 1.º toque do duplo: rouba a marca, que o destino tem tropas nossas
            _multiSel.DoubleTap(dst.Id);            // 2.º toque: repõe a origem e manda marchar
            int marching = movers.Count(id => w.Divisions.TryGetValue(id, out var md) && md.Path.Count > 0);
            GD.Print($"smoke: duplo toque em {dst.Name} (já com tropas nossas) → {marching}/{movers.Count} divisões de {src.Name} a marchar");
            // e o mesmo destino com origem E destino marcados por toque longo — juntar tropas a uma região que
            // já está na selecção. É o caso que a guarda "destino não pode estar marcado" recusava.
            foreach (var id in movers) _game.Dispatch(new StopDivisionCommand(pid, id));
            _multiSel.Clear();
            _multiSel.LongPress(src.Id); _multiSel.LongPress(dst.Id);
            _multiSel.Tap(dst.Id); _multiSel.DoubleTap(dst.Id);
            int joined = movers.Count(id => w.Divisions.TryGetValue(id, out var md) && md.Path.Count > 0);
            GD.Print($"smoke: duplo toque em {dst.Name} com origem e destino marcados → {joined}/{movers.Count} divisões a juntar-se");
            _multiSel.Clear();
        }
        else GD.Print("smoke: duplo toque sobre tropas por provar — sem duas regiões nossas ligadas");
        string menu = _menu.Smoke();                        // menu de jogo: secções, botões e filas de chapas medidas
        int saves = _slots.Smoke();                          // e as fichas dos jogos guardados
        GD.Print($"smoke: menu de jogo com {menu}, dificuldade {(_game.World.Difficulty ?? "por escolher")}, "
               + $"{saves} fichas de jogo guardado (slot {_game.Slot} em curso)");
    }
}
