using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Painel da região tocada (45% inferior do ecrã): estado, divisões presentes e ordens do jogador
/// (Mover, Parar, Declarar guerra, Produzir) ou "Jogar como" enquanto não há jogador.
/// Lê o World só em Fill (mundo parado) e muta só por Game.Dispatch.</summary>
public partial class RegionPanel : PanelContainer
{
    /// <summary>O próximo toque numa região é o destino das divisões seleccionadas.</summary>
    public bool MoveMode { get; private set; }

    private Game _game = null!;
    private MapView _map = null!;
    private ProductionPanel _production = null!;
    private CountryPanel _countryPanel = null!;
    private Label _title = null!, _info = null!;
    private VBoxContainer _rows = null!;
    private Button _play = null!, _all = null!, _move = null!, _stop = null!, _war = null!, _produce = null!;
    private ConfirmationDialog _warDialog = null!;
    private readonly Dictionary<string, string> _terrainNames = new();
    private readonly HashSet<int> _selected = new();
    private readonly Dictionary<int, CheckBox> _boxes = new();
    private readonly List<int> _mine = new();
    private int _regionId, _warTarget;
    private string _lastKey = "";

    public void Setup(Game game, MapView map, ProductionPanel production, CountryPanel countryPanel)
    {
        _game = game; _map = map; _production = production; _countryPanel = countryPanel;
        try { foreach (var r in game.StaticDb.Query("SELECT id,name FROM terrain")) _terrainNames[(string)r["id"]!] = (string)r["name"]!; }
        catch (Exception ex) { GD.PushError("terrain: " + ex.Message); }

        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.55f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 0.95f)));
        var v = new VBoxContainer(); AddChild(v);
        _title = Ui.Lbl("", 22); v.AddChild(_title);
        _info = Ui.Lbl("", 18); v.AddChild(_info);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _rows = Ui.Grow(new VBoxContainer()); scroll.AddChild(_rows);

        var actions = new HFlowContainer(); v.AddChild(actions);
        _play = Ui.Btn("", () => _game.RunWhenIdle(OnPlay)); actions.AddChild(_play);
        _all = Ui.Btn("Todas", SelectAll); actions.AddChild(_all);
        _move = Ui.Btn("Mover", BeginMove); actions.AddChild(_move);
        _stop = Ui.Btn("Parar", () => _game.RunWhenIdle(OnStop)); actions.AddChild(_stop);
        _war = Ui.Btn("", () => _warDialog.PopupCentered()); actions.AddChild(_war);
        _produce = Ui.Btn("Produzir", () => { Close(); _production.Open(); }); actions.AddChild(_produce);
        actions.AddChild(Ui.Btn("País", () => _game.RunWhenIdle(() =>
        {
            if (!_game.World.Regions.TryGetValue(_regionId, out var r)) return;
            Close(); _countryPanel.Open(r.ControllerId);
        })));
        actions.AddChild(Ui.Btn("Fechar", Close));
        _warDialog = Ui.Dialog(this, () => _game.RunWhenIdle(OnWar));
    }

    public void Open(int regionId)
    {
        _regionId = regionId; _selected.Clear(); MoveMode = false; _lastKey = "";
        _game.RunWhenIdle(() => { Fill(); Visible = true; });
    }

    /// <summary>Relê o estado (chamado pelo Hud com o mundo parado). Sem efeito se fechado.</summary>
    public void Refresh() { if (Visible) Fill(); }

    public void Close()
    {
        Visible = false; MoveMode = false; _selected.Clear();
        _map.Regions.Highlight(null);
    }

    /// <summary>Alterna: todas as divisões do jogador seleccionadas ↔ nenhuma.</summary>
    public void SelectAll()
    {
        bool all = _mine.Count > 0 && _mine.All(_selected.Contains);
        _selected.Clear();
        if (!all) _selected.UnionWith(_mine);
        foreach (var (id, cb) in _boxes) cb.SetPressedNoSignal(_selected.Contains(id));
        UpdateButtons();
    }

    public void BeginMove()
    {
        if (_selected.Count == 0) return;
        MoveMode = true; UpdateButtons();
        _game.Notify("Toca na região de destino");
    }

    /// <summary>Destino escolhido: uma ordem por divisão seleccionada; o primeiro erro vai ao toast.</summary>
    public void MoveTo(int targetRegionId) => _game.RunWhenIdle(() =>
    {
        MoveMode = false;
        if (_game.PlayerId is not int pid) return;
        string? first = null;
        foreach (var id in _selected.ToList())
            first ??= _game.Dispatch(new MoveDivisionCommand(pid, id, targetRegionId));
        if (first is not null) _game.Notify(first);
        Refresh();
    });

    private void OnStop()
    {
        if (_game.PlayerId is not int pid) return;
        string? first = null;
        foreach (var id in _selected.ToList()) first ??= _game.Dispatch(new StopDivisionCommand(pid, id));
        if (first is not null) _game.Notify(first);
        Refresh();
    }

    private void OnWar()
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new JustifyWarCommand(pid, _warTarget));
        if (err is not null) _game.Notify(err);
        Refresh();
    }

    /// <summary>"Jogar como <controlador>": ChoosePlayerCommand, relógio a 1×, câmara na capital.</summary>
    private void OnPlay()
    {
        var w = _game.World;
        if (!w.Regions.TryGetValue(_regionId, out var r) || !w.Countries.TryGetValue(r.ControllerId, out var c)) return;
        var err = _game.Dispatch(new ChoosePlayerCommand(c.Id));
        if (err is not null) { _game.Notify(err); return; }
        w.Clock.Speed = 1;
        var cap = w.Regions.GetValueOrDefault(c.CapitalRegionId) ?? r;
        _map.Focus(new Vector2(cap.CenterX, cap.CenterY), 1f);
        _game.Notify($"Jogas com {c.Name}");
        Close();
    }

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (!w.Regions.TryGetValue(_regionId, out var r)) { Close(); return; }
            int? pid = _game.PlayerId;
            var ctrl = w.Countries.GetValueOrDefault(r.ControllerId);

            _title.Text = $"{r.Name}  ·  {_terrainNames.GetValueOrDefault(r.Terrain, r.Terrain)}{(r.Coastal ? " ⚓" : "")}";
            var info = $"{ctrl?.Name ?? "—"}{(r.ControllerId != r.OwnerId ? " (ocupada)" : "")}  ·  {r.Population / 1e6f:0.0} M hab.";
            var battle = w.ActiveBattles.FirstOrDefault(b => b.RegionId == r.Id);
            if (battle is not null) info += $"  ·  {RegionRenderer.BattleMark}batalha ({battle.Days} dias)";
            if (MoveMode) info += "\nToca na região de destino";
            _info.Text = info;

            // Divisões: as do jogador primeiro (com caixa), depois as outras.
            var divs = r.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>()
                        .OrderByDescending(d => d.CountryId == pid).ThenBy(d => d.Id).ToList();
            _mine.Clear(); _mine.AddRange(divs.Where(d => d.CountryId == pid).Select(d => d.Id));
            _selected.IntersectWith(_mine);
            var lines = divs.Select(d => (d.Id, mine: d.CountryId == pid, text: Line(w, d))).ToList();
            var key = string.Join("|", lines.Select(l => l.Id + ":" + l.text));
            if (key != _lastKey)   // só reconstrói as linhas quando algo mudou (evita saltos de scroll a 4×)
            {
                _lastKey = key;
                Ui.Clear(_rows); _boxes.Clear();
                foreach (var (id, mine, text) in lines) _rows.AddChild(Row(id, mine, text));
                if (lines.Count == 0) _rows.AddChild(Ui.Lbl("Sem divisões"));
            }

            bool hasPlayer = pid is not null, anyMine = _mine.Count > 0;
            _play.Visible = !hasPlayer && ctrl is not null;
            if (_play.Visible) _play.Text = $"Jogar como {ctrl!.Name}";
            _all.Visible = _move.Visible = _stop.Visible = hasPlayer && anyMine;
            bool canWar = hasPlayer && ctrl is not null && ctrl.Id != pid && !w.AreAtWar(pid!.Value, ctrl.Id);
            _war.Visible = canWar;
            if (canWar) { _war.Text = $"Justificar guerra a {ctrl!.Name}"; _warTarget = ctrl.Id; _warDialog.DialogText = $"Justificar objectivo de guerra contra {ctrl.Name}? A guerra declara-se sozinha ao fim da justificação."; }
            _produce.Visible = hasPlayer;
            UpdateButtons();
        }
        catch (Exception ex) { GD.PushError("RegionPanel.Fill: " + ex); }
    }

    private static string Line(World w, Division d)
    {
        string name; try { name = w.Units.GetTemplate(d.TemplateId).Name; } catch { name = "T" + d.TemplateId; }
        var tag = w.Countries.TryGetValue(d.CountryId, out var c) ? c.Tag : "?";
        var s = $"{tag} {name}   HP {d.Hp:0}  Org {d.Org:0}  Sup {d.Supply:0.0}";
        if (d.DestinationRegionId is int dest) s += $"   → {(w.Regions.TryGetValue(dest, out var rr) ? rr.Name : "R" + dest)}";
        if (w.InBattle(d.Id)) s += "   " + RegionRenderer.BattleMark.Trim();
        return s;
    }

    // Divisão do jogador = CheckBox com o texto (linha inteira é alvo de toque); outras = Label.
    private Control Row(int id, bool mine, string text)
    {
        if (!mine) { var l = Ui.Grow(Ui.Lbl(text)); l.CustomMinimumSize = new Vector2(0, 40); return l; }
        var cb = Ui.Grow(new CheckBox { Text = text, ButtonPressed = _selected.Contains(id), CustomMinimumSize = new Vector2(0, 48) });
        cb.AddThemeFontSizeOverride("font_size", Ui.Font);
        cb.Toggled += on => { try { if (on) _selected.Add(id); else _selected.Remove(id); UpdateButtons(); } catch (Exception ex) { GD.PushError(ex.ToString()); } };
        _boxes[id] = cb;
        return cb;
    }

    private void UpdateButtons()
    {
        _move.Disabled = _selected.Count == 0 || MoveMode;
        _stop.Disabled = _selected.Count == 0;
    }
}
