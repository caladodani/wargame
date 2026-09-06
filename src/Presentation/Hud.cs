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
    private Label _date = null!, _country = null!, _army = null!, _toast = null!, _hint = null!;
    private PanelContainer _season = null!;
    private string _seasonPainted = "";
    private TextureRect _playerFlag = null!;
    private Button _pause = null!;
    private PanelContainer _toastBox = null!;
    private ColorRect _accent = null!;
    private Timer _toastTimer = null!;
    private Tween? _toastTween;   // animação de entrada/saída do toast (morre e recomeça a cada mensagem)
    private AcceptDialog _slots = null!;
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
    private JournalPanel _journal = null!;
    private readonly List<IDisposable> _subs = new();
    private readonly HashSet<(int, int)> _whitePeace = new();   // guerras fechadas por paz branca (o WarEnded seguinte muda o toast)
    private readonly HashSet<(int, int)> _negotiated = new();   // idem para a paz negociada: a notícia sai no PeaceSigned
    private bool _smoke, _smoked;
    private ulong _backAt;   // Time.GetTicksMsec do último "voltar" sem painel aberto

    public override void _Ready()
    {
        try
        {
            _game = GetNode<Game>("/root/Game");
            _map = GetNode<MapView>("../MapView");
            _smoke = OS.GetCmdlineUserArgs().Contains("--smoke");
            GetTree().Root.Theme = Ui.Theme();   // tema da janela inteira: painéis, botões e diálogos de uma vez
            BuildTopBar(); BuildToast();
            _production = new ProductionPanel(); AddChild(_production); _production.Setup(_game);
            _countryPanel = new CountryPanel(); AddChild(_countryPanel); _countryPanel.Setup(_game);
            _worldPanel = new WorldPanel(); AddChild(_worldPanel); _worldPanel.Setup(_game, _countryPanel);
            _warPanel = new WarPanel(); AddChild(_warPanel); _warPanel.Setup(_game);
            _journal = new JournalPanel(); AddChild(_journal); _journal.Setup(_game);
            _region = new RegionPanel(); AddChild(_region); _region.Setup(_game, _map, _production, _countryPanel);
            _multiSel = new ArmySelect(); AddChild(_multiSel); _multiSel.Setup(_game, _map);
            _armyPanel = new ArmyPanel(); AddChild(_armyPanel); _armyPanel.Setup(_game, _map, _multiSel);
            _end = new EndScreen(); AddChild(_end); _end.Setup(_game);
            _menu = new GameMenu(); AddChild(_menu); _menu.Setup(_game, OpenSlots, () => _end.Show(CampaignReport.Ongoing));
            _mini = new MiniMap(); AddChild(_mini); _mini.Setup(_map);

            _map.RegionTapped += OnRegionTapped;
            _map.RegionLongPressed += rid => _multiSel.LongPress(rid);
            _map.RegionDoubleTapped += rid => _multiSel.DoubleTap(rid);
            _game.TickCompleted += OnTick;
            _game.StateChanged += RefreshAll;
            _game.CommandFailed += Toast;
            SubscribeEvents();
            _game.RunWhenIdle(RefreshAll);
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
        bar.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.08f, 0.09f, 0.12f, 0.92f), 6));
        AddChild(bar);
        // A barra leva uma tira fina por baixo, pintada com a cor do país do jogador: dá identidade
        // ao ecrã inteiro e fica vermelha quando o país está em guerra.
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 6); bar.AddChild(stack);

        // Primeira linha: o estado do jogo (data, velocidade, país, exército). Num telemóvel isto sozinho
        // já enche a largura — por isso a navegação desceu para a linha de baixo.
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); stack.AddChild(row);
        _date = Ui.Lbl("2030-01-01", 22); row.AddChild(_date);
        row.AddChild(Ui.Btn("<", () => Speed(-1), 56));
        _pause = Ui.Btn("||", () => Speed(0), 72); row.AddChild(_pause);
        row.AddChild(Ui.Btn(">", () => Speed(+1), 56));
        _season = SeasonView.Badge(_game.World); row.AddChild(_season);
        _playerFlag = Flags.Rect(22); _playerFlag.Visible = false; row.AddChild(_playerFlag);
        _country = Ui.Grow(Ui.Lbl("", 20)); row.AddChild(_country);
        _army = Ui.Lbl("", 20); row.AddChild(_army);

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
        tabs.AddChild(Ui.Btn("Mundo", () => _worldPanel.Open()));
        tabs.AddChild(Ui.Btn("Guerra", OpenWar));
        tabs.AddChild(Ui.Btn("Exércitos", () => _armyPanel.Open()));
        tabs.AddChild(Ui.Btn("Crónica", () => _journal.Open()));
        tabs.AddChild(Ui.Btn("☰ Menu", () => _menu.Toggle()));

        _accent = new ColorRect { CustomMinimumSize = new Vector2(0, 3), Color = Ui.SurfaceHi, MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddChild(_accent);
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

    private void OpenCountry()
    {
        if (_game.PlayerId is not int pid) { Toast("Toca num país e escolhe-o primeiro"); return; }
        _region.Close(); _production.Close(); _countryPanel.Open(pid);
    }

    // delta 0 = alternar pausa. Sem jogador o relógio fica parado (Speed 0 é o Game que o põe).
    private void Speed(int delta)
    {
        if (_game.PlayerId is null) { Toast("Toca num país e escolhe-o primeiro"); return; }
        var c = _game.World.Clock;
        c.Speed = delta == 0 ? (c.Speed == 0 ? 1 : 0) : Mathf.Clamp(c.Speed + delta, 0, 4);
        _game.RunWhenIdle(RefreshTop);
    }

    private void BuildToast()
    {
        // Toast: caixa centrada por baixo da barra; Ignore no wrapper para o toque passar ao mapa.
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        center.OffsetTop = 132; center.OffsetBottom = 192;   // a barra de topo tem duas linhas
        AddChild(center);
        _toastBox = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _toastBox.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.92f }, 12));
        _toast = Ui.Lbl("", 20); _toastBox.AddChild(_toast); center.AddChild(_toastBox);
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

    public void Toast(string msg)
    {
        try
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            _toast.Text = msg;
            ShowToastBox();
            _toastTimer.Start();
            _journal?.Add(msg);
            if (_smoke) GD.Print("toast: " + msg);
        }
        catch (Exception ex) { GD.PushError("Toast: " + ex); }
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
            if (Player(e.Aggressor) || Player(e.Target)) Later($"{Country(e.Aggressor)} declarou guerra a {Country(e.Target)}");
        }));
        _subs.Add(w.Events.Subscribe<RegionCaptured>(e =>
        {
            if (Player(e.NewController)) Later($"Capturaste {RegionName(e.RegionId)}");
            else if (Player(e.OldController)) Later($"Perdeste {RegionName(e.RegionId)} para {Country(e.NewController)}");
        }));
        _subs.Add(w.Events.Subscribe<BattleStarted>(e => { if (Mine(e.RegionId)) Later($"Batalha em {RegionName(e.RegionId)}"); }));
        _subs.Add(w.Events.Subscribe<BattleEnded>(e =>
        {
            if (Mine(e.RegionId)) Later($"Batalha em {RegionName(e.RegionId)}: {(e.AttackerWon ? "atacante venceu" : "defesa aguentou")}");
        }));
        _subs.Add(w.Events.Subscribe<TechResearched>(e =>
        {
            if (Player(e.CountryId)) Later($"Investigação concluída: {(w.Techs.TryGetValue(e.TechId, out var t) ? t.Name : e.TechId)}");
        }));
        _subs.Add(w.Events.Subscribe<NukeStruck>(e =>
            Later($"☢ {Country(e.AttackerId)} lançou uma ogiva sobre {RegionName(e.RegionId)} ({Country(e.TargetCountryId)})")));
        _subs.Add(w.Events.Subscribe<DivisionDestroyed>(e =>
        {
            if (!w.Divisions.TryGetValue(e.DivisionId, out var d) || !Player(d.CountryId)) return;
            string name; try { name = w.Units.GetTemplate(d.TemplateId).Name; } catch { name = "divisão"; }
            Later($"{name} destruída em {RegionName(d.RegionId)}");
        }));
        _subs.Add(w.Events.Subscribe<NewsFired>(e =>
        {
            if (w.NewsEvents.TryGetValue(e.EventId, out var n) && (n.CountryId is null || Player(n.CountryId.Value)))
                Later($"📰 {n.Title} — {n.Body}");
        }));
        _subs.Add(w.Events.Subscribe<NewsChoiceRequired>(e =>
            Callable.From(() => ShowNewsChoice(e.EventId)).CallDeferred()));
        _subs.Add(w.Events.Subscribe<WarJustifyStarted>(e =>
        {
            if (Player(e.TargetCountryId)) Later($"{Country(e.CountryId)} está a justificar guerra contra ti!");
            else if (Player(e.CountryId)) Later($"A justificar guerra contra {Country(e.TargetCountryId)}");
        }));
        _subs.Add(w.Events.Subscribe<FocusCompleted>(e =>
        {
            if (Player(e.CountryId)) Later($"Foco concluído: {(w.Focuses.TryGetValue(e.FocusId, out var f) ? f.Name : e.FocusId)}");
        }));
        _subs.Add(w.Events.Subscribe<FactionJoinedWar>(e =>
        {
            if (Player(e.MemberCountryId) || Player(e.AgainstCountryId) || _game.PlayerId is int p2 && w.AreAtWar(p2, e.AgainstCountryId))
                Later($"{Country(e.MemberCountryId)} entrou na guerra contra {Country(e.AgainstCountryId)} (facção)");
        }));
        _subs.Add(w.Events.Subscribe<InfrastructureBuilt>(e =>
        {
            if (_game.PlayerId is int p && _game.World.Regions.TryGetValue(e.RegionId, out var r) && r.OwnerId == p)
                Later($"Infraestrutura melhorada em {r.Name} (×{r.Infrastructure:0.00})");
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
                Later($"{bd.Name} nível {e.Level} em {r.Name}");
        }));
        _subs.Add(w.Events.Subscribe<PeaceOfferRejected>(e =>
        {
            if (Player(e.FromCountryId)) Later($"{Country(e.ToCountryId)} recusou a paz — ainda acha que ganha");
        }));
        _subs.Add(w.Events.Subscribe<MoneyTransferred>(e =>
        {
            if (Player(e.ToCountryId)) Later($"{Country(e.FromCountryId)} enviou-te {e.Amount:0} pontos de produção");
            else if (Player(e.FromCountryId)) Later($"Apoio de {e.Amount:0} pts enviado a {Country(e.ToCountryId)}");
        }));
        _subs.Add(w.Events.Subscribe<PactSigned>(e =>
        {
            if (Player(e.A) || Player(e.B)) Later($"Pacto de não-agressão com {Country(Player(e.A) ? e.B : e.A)} até ao dia {e.UntilDay}");
        }));
        _subs.Add(w.Events.Subscribe<PactRejected>(e =>
        {
            if (Player(e.FromCountryId)) Later($"{Country(e.ToCountryId)} recusou o pacto de não-agressão");
        }));
        _subs.Add(w.Events.Subscribe<BattleRetreat>(e =>
        {
            if (Player(e.CountryId) && _game.World.Regions.TryGetValue(e.RegionId, out var r))
                Later($"{e.Divisions} divisões retiraram de {r.Name}");
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
            else if (Player(e.TargetCountryId)) Later($"Fomos alvo de espionagem: {op?.Name ?? e.OpId} ({Country(e.CountryId)})");
        }));
        _subs.Add(w.Events.Subscribe<FortBuilt>(e =>
        {
            if (_game.PlayerId is int p && _game.World.Regions.TryGetValue(e.RegionId, out var r) && r.OwnerId == p)
                Later($"Fortificação nível {e.Level} em {r.Name}");
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
            else if (e.OldController == p) Later($"Perdemos {r.Name} para uma revolta popular");
        }));
        _subs.Add(w.Events.Subscribe<LawChanged>(e =>
        {
            if (Player(e.CountryId) && w.Laws.TryGetValue(e.LawId, out var l)) Later($"Nova lei: {l.Name}");
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
            if (Player(e.Winner)) Later($"🕊 Paz com {Country(e.Loser)} — ficas com {e.Regions} regiões");
            else if (Player(e.Loser)) Later($"🕊 Paz com {Country(e.Winner)} — cedes-lhe {e.Regions} regiões");
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
            if (Player(e.CountryId)) Later($"🎯 Objectivo cumprido contra {Country(e.TargetCountryId)} — dá para exigir a paz");
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
            Later($"🎖 {gdef.Name} promovido a {e.RankName}");
        }));
        // Condecoração: só as do jogador, e só as de peso (as primeiras chegam às centenas num exército grande).
        _subs.Add(w.Events.Subscribe<MedalAwarded>(e =>
        {
            if (!Player(e.CountryId) || !w.MedalDefs.TryGetValue(e.MedalId, out var m) || m.Sort < 3) return;
            string unit = w.Divisions.TryGetValue(e.DivisionId, out var d)
                ? d.Name ?? SafeTemplate(w, d) : "Divisão " + e.DivisionId;
            Later($"🎖 {unit}: {m.Name}");
        }));
        // Nome de guerra: uma divisão só ganha honra umas poucas vezes por campanha — vai toda para as notícias.
        _subs.Add(w.Events.Subscribe<DivisionHonoured>(e =>
        {
            if (!Player(e.CountryId)) return;
            string unit = w.Divisions.TryGetValue(e.DivisionId, out var d)
                ? d.Name ?? SafeTemplate(w, d) : "Divisão " + e.DivisionId;
            Later($"▮ {unit} passa a chamar-se «{e.Title}»");
        }));
        // Prisioneiros: só as levas grandes (uma divisão desfeita por dia numa guerra grande enche o ecrã).
        _subs.Add(w.Events.Subscribe<PrisonersTaken>(e =>
        {
            if (e.Men < (int)w.Rule("prisoner_news_men", 20000f)) return;
            string foe = w.Countries.TryGetValue(e.FromCountryId, out var fc) ? fc.Name : "o inimigo";
            if (Player(e.CaptorId)) Later($"⛓ {e.Men:N0} prisioneiros de {foe} nas nossas mãos");
            else if (Player(e.FromCountryId)) Later($"⛓ {e.Men:N0} dos nossos caem prisioneiros");
        }));
        _subs.Add(w.Events.Subscribe<PrisonersReturned>(e =>
        {
            if (Player(e.HomeCountryId)) Later($"⛓ {e.Men:N0} prisioneiros nossos voltam a casa");
            else if (Player(e.HolderId)) Later($"⛓ Abrimos os campos: {e.Men:N0} prisioneiros repatriados");
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
            else if (Player(e.WinnerId)) Later($"Vitória! {Country(e.CountryId)} capitulou — as regiões dele são tuas");
            else Later($"{Country(e.CountryId)} capitulou! Regiões passam para {Country(e.WinnerId)}");
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



    private void Later(string msg) => Callable.From(() => Toast(msg)).CallDeferred();
    private bool Player(int countryId) => _game.PlayerId == countryId;
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
            _region.Refresh();
            _production.Refresh();
            _countryPanel.Refresh();
            _worldPanel.Refresh();
            _warPanel.Refresh();
            _armyPanel.Refresh();
            // O mini-mapa não serve de nada por baixo de um painel que ocupa metade do ecrã.
            _mini.SetCovered(_region.Visible || _production.Visible || _countryPanel.Visible
                             || _worldPanel.Visible || _warPanel.Visible || _journal.Visible || _armyPanel.Visible);
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
        _date.Text = c.Date.ToString("yyyy-MM-dd") + (c.Paused ? "  ⏸" : "  " + new string('\u25b6', Math.Max(1, c.Speed)));
        _pause.Text = c.Paused ? "Play" : "||";
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
            _country.Text = $"{p.Tag}   {p.Money:0.0}  (+{EconomySystem.Income(w, pid):0.0}/dia)";
            _army.Text = $"Divisões {w.Divisions.Values.Count(d => d.CountryId == pid)}  ·  Fila {p.Queue.Count}  ·  Homens {FmtMen(p.Manpower)}";
            _hint.Visible = false;
            bool atWar = p.AtWarWith.Count > 0;
            _accent.Color = atWar ? Ui.Danger : _map.Regions.CountryColor(pid);
        }
        else { _country.Text = ""; _army.Text = ""; _hint.Visible = true; _playerFlag.Visible = false; _playerFlag.Texture = null; _accent.Color = Ui.SurfaceHi; }
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

    // --smoke: abre os painéis e dá uma ordem de movimento para os caminhos de código correrem sem ecrã.
    private void Smoke()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c) || !w.Regions.TryGetValue(c.CapitalRegionId, out var cap)) return;
        OnRegionTapped(cap.Id); _region.SelectAll(); _region.BeginMove();
        GD.Print($"smoke: {cap.DivisionIds.Count} divisões na capital, {cap.Neighbours.Count} vizinhos");
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
        // uma divisão nossa condecorada e com nome próprio, para a folha de serviço ter cartões a desenhar
        if (w.Divisions.Values.FirstOrDefault(d => d.CountryId == pid) is Division hero)
        {
            hero.Battles = Math.Max(hero.Battles, 12); hero.Captures = Math.Max(hero.Captures, 6); hero.Xp = MathF.Max(hero.Xp, 95f);
            new MedalSystem().Tick(w); new DivisionHonourSystem().Tick(w);
        }
        // um comandante do jogador ferido, para a enfermaria e as barras de convalescença desenharem
        int hurt = 0;
        if (c.Generals.Count == 0 && w.GeneralDefs.Values.OrderBy(g => g.Cost).FirstOrDefault() is GeneralDef cheap)
        {
            c.Money = MathF.Max(c.Money, cheap.Cost);                // ao dia 73 ninguém tem troco: o smoke adianta-o
            _game.Dispatch(new HireGeneralCommand(pid, cheap.Id));   // sem estado-maior não há enfermaria para desenhar
        }
        if (c.Generals.FirstOrDefault() is string gen && w.WoundKinds.Values.FirstOrDefault(k => !k.Fatal) is WoundKind wk)
        {
            c.GeneralWound[gen] = w.Clock.Day + wk.Days;
            World.ApplyGenerals(w, c);
            hurt = c.GeneralWound.Count;
        }
        // um cais no litoral, para o cartão dos portos e a linha da região terem números
        if (PortView.Ports(w, pid).Count == 0
            && w.BuildingDefs.Values.FirstOrDefault(b => b.SupplyRange > 0f) is BuildingDef quay
            && w.Regions.Values.FirstOrDefault(r => r.ControllerId == pid && r.Coastal) is Region coast)
        {
            coast.Buildings[quay.Id] = 1;
            new SupplySystem().Tick(w);                                // recalcula capacidade e carga do cais
        }
        int served = _countryPanel.Smoke(pid); _countryPanel.Close();   // painel País: folha de serviço com os cartões
        int cron = _journal.Smoke(); _journal.Close();                  // painel Crónica: linha do tempo e filtros
        int pris = PrisonerView.Held(w, pid);                           // campos de prisioneiros do jogador
        GD.Print($"smoke: painéis abertos na capital {cap.Name}, {world} linhas no painel Mundo, {served} na folha de serviço, estação {w.Season?.Name ?? "nenhuma"}, {cron} na crónica, {hurt} na enfermaria, {PrisonerView.Short(pris)} prisioneiros, cais para {c.PortCapacity:0} divisões");
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
