using Godot;
using Timer = Godot.Timer;
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
    private Button _pause = null!;
    private PanelContainer _toastBox = null!;
    private Timer _toastTimer = null!;
    private ConfirmationDialog _confirmNew = null!;
    private RegionPanel _region = null!;
    private ProductionPanel _production = null!;
    private CountryPanel _countryPanel = null!;
    private readonly List<IDisposable> _subs = new();
    private bool _smoke, _smoked;
    private ulong _backAt;   // Time.GetTicksMsec do último "voltar" sem painel aberto

    public override void _Ready()
    {
        try
        {
            _game = GetNode<Game>("/root/Game");
            _map = GetNode<MapView>("../MapView");
            _smoke = OS.GetCmdlineUserArgs().Contains("--smoke");
            BuildTopBar(); BuildToast();
            _confirmNew = Ui.Dialog(this, () => _game.NewGame());
            _confirmNew.DialogText = "Começar um novo jogo? O jogo actual perde-se.";
            _production = new ProductionPanel(); AddChild(_production); _production.Setup(_game);
            _countryPanel = new CountryPanel(); AddChild(_countryPanel); _countryPanel.Setup(_game);
            _region = new RegionPanel(); AddChild(_region); _region.Setup(_game, _map, _production, _countryPanel);

            _map.RegionTapped += OnRegionTapped;
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
        if (_region.Visible) { _region.Close(); return; }
        if (_production.Visible) { _production.Close(); return; }
        if (_countryPanel.Visible) { _countryPanel.Close(); return; }
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
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); bar.AddChild(row);
        _date = Ui.Lbl("2030-01-01", 22); row.AddChild(_date);
        row.AddChild(Ui.Btn("<", () => Speed(-1), 56));
        _pause = Ui.Btn("||", () => Speed(0), 72); row.AddChild(_pause);
        row.AddChild(Ui.Btn(">", () => Speed(+1), 56));
        _country = Ui.Grow(Ui.Lbl("", 20)); row.AddChild(_country);
        _army = Ui.Lbl("", 20); row.AddChild(_army);
        row.AddChild(Ui.Btn("País", OpenCountry));
        row.AddChild(Ui.Btn("Guardar", () => { _game.Save(); Toast("Jogo guardado"); }));
        row.AddChild(Ui.Btn("Novo jogo", () => _confirmNew.PopupCentered()));
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
        center.OffsetTop = 70; center.OffsetBottom = 130;
        AddChild(center);
        _toastBox = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _toastBox.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0, 0, 0, 0.8f), 10));
        _toast = Ui.Lbl("", 20); _toastBox.AddChild(_toast); center.AddChild(_toastBox);
        _toastTimer = new Timer { WaitTime = 4, OneShot = true }; AddChild(_toastTimer);
        _toastTimer.Timeout += () => _toastBox.Visible = false;

        // Instrução enquanto não há jogador.
        var center2 = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center2.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        center2.OffsetTop = 140; center2.OffsetBottom = 200;
        AddChild(center2);
        _hint = Ui.Lbl("Toca num país e escolhe-o", 26); _hint.Visible = false; center2.AddChild(_hint);
    }

    /// <summary>Mensagem breve ao jogador (4 s). Seguro chamar de sinais; de outra thread usar CallDeferred.</summary>
    public void Toast(string msg)
    {
        try
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            _toast.Text = msg; _toastBox.Visible = true; _toastTimer.Start();
            if (_smoke) GD.Print("toast: " + msg);
        }
        catch (Exception ex) { GD.PushError("Toast: " + ex); }
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
        _subs.Add(w.Events.Subscribe<DivisionDestroyed>(e =>
        {
            if (!w.Divisions.TryGetValue(e.DivisionId, out var d) || !Player(d.CountryId)) return;
            string name; try { name = w.Units.GetTemplate(d.TemplateId).Name; } catch { name = "divisão"; }
            Later($"{name} destruída em {RegionName(d.RegionId)}");
        }));
        _subs.Add(w.Events.Subscribe<FactionJoinedWar>(e =>
        {
            if (Player(e.MemberCountryId) || Player(e.AgainstCountryId) || _game.PlayerId is int p2 && w.AreAtWar(p2, e.AgainstCountryId))
                Later($"{Country(e.MemberCountryId)} entrou na guerra contra {Country(e.AgainstCountryId)} (facção)");
        }));
        _subs.Add(w.Events.Subscribe<WarEnded>(e =>
        {
            if (Player(e.A) || Player(e.B)) Later($"Paz entre {Country(e.A)} e {Country(e.B)}");
        }));
        _subs.Add(w.Events.Subscribe<CountryCapitulated>(e =>
        {
            if (Player(e.CountryId)) Callable.From(ShowDefeat).CallDeferred();
            else if (Player(e.WinnerId)) Later($"Vitória! {Country(e.CountryId)} capitulou — as regiões dele são tuas");
            else Later($"{Country(e.CountryId)} capitulou! Regiões passam para {Country(e.WinnerId)}");
        }));
    }

    /// <summary>Fim de jogo do jogador: capitulou. Diálogo com novo jogo ou continuar a ver o mundo.</summary>
    private void ShowDefeat()
    {
        var dlg = new AcceptDialog
        {
            Title = "Derrota",
            DialogText = "O teu país capitulou. As tuas regiões foram ocupadas e o exército dissolvido.",
            OkButtonText = "Novo jogo",
        };
        dlg.AddButton("Continuar a ver", true, "watch");
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
        }
        catch (Exception ex) { GD.PushError("Hud.RefreshAll: " + ex); }
    }

    private static string FmtMen(float m) =>
        m < 0 ? "—" : m >= 1e6f ? $"{m / 1e6f:0.0}M" : m >= 1e3f ? $"{m / 1e3f:0}k" : $"{m:0}";

    private void RefreshTop()
    {
        var w = _game.World; var c = w.Clock;
        _date.Text = c.Date.ToString("yyyy-MM-dd") + (c.Paused ? "  (pausa)" : $"  ×{c.Speed}");
        _pause.Text = c.Paused ? "Play" : "||";
        if (_game.PlayerId is int pid && w.Countries.TryGetValue(pid, out var p))
        {
            _country.Text = $"{p.Tag}   {p.Money:0.0}  (+{EconomySystem.Income(w, pid):0.0}/dia)";
            _army.Text = $"Divisões {w.Divisions.Values.Count(d => d.CountryId == pid)}  ·  Fila {p.Queue.Count}  ·  Homens {FmtMen(p.Manpower)}";
            _hint.Visible = false;
        }
        else { _country.Text = ""; _army.Text = ""; _hint.Visible = true; }
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
        _production.Open();
        GD.Print($"smoke: painéis abertos na capital {cap.Name}");
    }
}
