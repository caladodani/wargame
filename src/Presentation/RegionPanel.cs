using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

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
    private TextureRect _flag = null!;
    private VBoxContainer _rows = null!;
    private Button _play = null!, _all = null!, _move = null!, _stop = null!, _disband = null!, _war = null!, _produce = null!, _build = null!, _fort = null!, _retreat = null!, _auto = null!;
    private HFlowContainer _bld = null!;      // botões de edifícios (tabela building)
    private string _bldKey = "";
    private ConfirmationDialog _warDialog = null!;
    private Button _nuke = null!;
    private ConfirmationDialog _nukeDialog = null!;
    private int _nukeTarget;
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
        var titleRow = new HBoxContainer(); v.AddChild(titleRow);
        _flag = Flags.Rect(26); titleRow.AddChild(_flag);
        _title = Ui.Lbl("", 22); titleRow.AddChild(_title);
        _info = Ui.Lbl("", 18); v.AddChild(_info);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _rows = Ui.Grow(new VBoxContainer()); scroll.AddChild(_rows);

        var actions = new HFlowContainer(); v.AddChild(actions);
        _bld = new HFlowContainer(); v.AddChild(_bld);
        _play = Ui.Btn("", () => _game.RunWhenIdle(OnPlay)); actions.AddChild(_play);
        _all = Ui.Btn("Todas", SelectAll); actions.AddChild(_all);
        _move = Ui.Btn("Mover", BeginMove); actions.AddChild(_move);
        _stop = Ui.Btn("Parar", () => _game.RunWhenIdle(OnStop)); actions.AddChild(_stop);
        _disband = Ui.Btn("Dissolver", () => _game.RunWhenIdle(OnDisband)); actions.AddChild(_disband);
        _war = Ui.Btn("", () => _warDialog.PopupCentered()); actions.AddChild(_war);
        _produce = Ui.Btn("Produzir", () => { Close(); _production.Open(); }); actions.AddChild(_produce);
        _build = Ui.Btn("", () => _game.RunWhenIdle(OnBuild)); actions.AddChild(_build);
        _fort = Ui.Btn("", () => _game.RunWhenIdle(OnFort)); actions.AddChild(_fort);
        _retreat = Ui.Btn("Retirar", () => _game.RunWhenIdle(OnRetreat)); actions.AddChild(_retreat);
        _auto = Ui.Btn("⚑ Avanço auto", () => _game.RunWhenIdle(OnAutoAdvance)); actions.AddChild(_auto);
        _nuke = Ui.Btn("☢ Ataque nuclear", () => _nukeDialog.PopupCentered()); actions.AddChild(_nuke);
        actions.AddChild(Ui.Btn("País", () => _game.RunWhenIdle(() =>
        {
            if (!_game.World.Regions.TryGetValue(_regionId, out var r)) return;
            Close(); _countryPanel.Open(r.ControllerId);
        })));
        actions.AddChild(Ui.Btn("Fechar", Close));
        _warDialog = Ui.Dialog(this, () => _game.RunWhenIdle(OnWar));
        _nukeDialog = Ui.Dialog(this, () => _game.RunWhenIdle(OnNuke));
    }

    public void Open(int regionId)
    {
        _regionId = regionId; _selected.Clear(); MoveMode = false; _lastKey = "";
        _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); });
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

    /// <summary>Liga/desliga o avanço automático nas divisões escolhidas: se alguma ainda não o tem,
    /// liga em todas; se já o têm todas, desliga.</summary>
    private void OnAutoAdvance()
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        bool on = _selected.Any(id => w.Divisions.TryGetValue(id, out var d) && !d.AutoAdvance);
        string? first = null;
        foreach (var id in _selected.ToList()) first ??= _game.Dispatch(new SetAutoAdvanceCommand(pid, id, on));
        if (first is not null) _game.Notify(first);
        Refresh();
    }

    private void OnStop()
    {
        if (_game.PlayerId is not int pid) return;
        string? first = null;
        foreach (var id in _selected.ToList()) first ??= _game.Dispatch(new StopDivisionCommand(pid, id));
        if (first is not null) _game.Notify(first);
        Refresh();
    }

    private void OnDisband()
    {
        if (_game.PlayerId is not int pid) return;
        string? first = null;
        foreach (var id in _selected.ToList()) first ??= _game.Dispatch(new DisbandDivisionCommand(pid, id));
        if (first is not null) _game.Notify(first);
        _selected.Clear();
        Refresh();
    }

    private void OnBuilding(string buildingId)
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new BuildBuildingCommand(pid, _regionId, buildingId));
        if (err is not null) _game.Notify(err);
        Refresh();
    }

    private void OnBuild()
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new BuildInfrastructureCommand(pid, _regionId));
        if (err is not null) _game.Notify(err);
        Refresh();
    }

    private void OnFort()
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new BuildFortCommand(pid, _regionId));
        if (err is not null) _game.Notify(err);
        Refresh();
    }

    private void OnRetreat()
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new RetreatFromBattleCommand(pid, _regionId));
        if (err is not null) _game.Notify(err);
        Refresh();
    }

    private void OnNuke()
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new NuclearStrikeCommand(pid, _nukeTarget));
        if (err is not null) _game.Notify(err);
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

            _flag.Texture = ctrl is not null ? Flags.Of(ctrl.Tag) : null;
            _title.Text = $"{r.Name}  ·  {_terrainNames.GetValueOrDefault(r.Terrain, r.Terrain)}{(r.Coastal ? " ⚓" : "")}";
            var info = $"{ctrl?.Name ?? "—"}{(r.ControllerId != r.OwnerId ? " (ocupada)" : "")}  ·  {r.Population / 1e6f:0.0} M hab.  ·  Infra ×{r.Infrastructure:0.00}  ·  💰 {EconomySystem.RegionIncome(w, r):0.00}/dia";
            if (r.Infrastructure < r.BaseInfrastructure - 1e-4f) info += $"  ·  🔧 danificada (repõe até ×{r.BaseInfrastructure:0.00})";
            if (r.Fort > 0) info += $"  ·  🏰 Forte {r.Fort}";
            foreach (var (res, amount) in r.Resources.OrderBy(kv => kv.Key))
                if (w.ResourceDefs.TryGetValue(res, out var rd)) info += $"  ·  {rd.Name} {amount:0}";
            foreach (var (bid, lvl) in r.Buildings.OrderBy(kv => kv.Key))
                if (lvl > 0 && w.BuildingDefs.TryGetValue(bid, out var bd)) info += $"  ·  {bd.Name} {lvl}";
            if (r.Project is string proj && w.BuildingDefs.TryGetValue(proj, out var pd))
                info += $"  🏗 {pd.Name}: {(int)MathF.Ceiling(pd.Days - r.ProjectProgress)} dias";
            if (r.Resistance > 0.005f) info += $"  ·  ✊ resistência {r.Resistance:P0}";
            if (r.Integration > 0.5f) info += $"  ·  🤝 integração {r.Integration / MathF.Max(1f, w.Rule("integration_days", 150f)):P0}";
            if (r.Building) info += $"  🏗 obra: {(int)MathF.Ceiling(w.Rule("infra_build_days", 30f) - r.BuildProgress)} dias";
            if (r.FortBuilding) info += $"  🏰 obra: {(int)MathF.Ceiling(w.Rule("fort_build_days", 20f) - r.FortProgress)} dias";
            var battle = w.ActiveBattles.FirstOrDefault(b => b.RegionId == r.Id);
            if (battle is not null)
            {
                float attOrg = battle.Attackers.Sum(id => w.Divisions.TryGetValue(id, out var d) ? d.Org : 0f);
                float defOrg = battle.Defenders.Sum(id => w.Divisions.TryGetValue(id, out var d) ? d.Org : 0f);
                string attTag = w.Countries.TryGetValue(battle.AttackerCountryId, out var ac) ? ac.Tag : "?";
                info += $"\n{RegionRenderer.BattleMark}batalha ({battle.Days} dias): {attTag} ataca — org {attOrg:0} vs {defOrg:0}"
                      + (r.Fort > 0 ? $" (forte {r.Fort})" : "");
            }
            if (MoveMode) info += "\nToca na região de destino";
            _info.Text = info;

            // Divisões: as do jogador primeiro (com caixa), depois as outras.
            var divs = r.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>()
                        .OrderByDescending(d => d.CountryId == pid).ThenBy(d => d.Id).ToList();
            _mine.Clear(); _mine.AddRange(divs.Where(d => d.CountryId == pid).Select(d => d.Id));
            _selected.IntersectWith(_mine);
            var lines = divs.Select(d => (d.Id, mine: d.CountryId == pid, text: Line(w, d), d.Hp, d.Org)).ToList();
            var key = string.Join("|", lines.Select(l => l.Id + ":" + l.text));
            if (key != _lastKey)   // só reconstrói as linhas quando algo mudou (evita saltos de scroll a 4×)
            {
                _lastKey = key;
                Ui.Clear(_rows); _boxes.Clear();
                foreach (var (id, mine, text, hp, org) in lines) _rows.AddChild(Row(id, mine, text, hp, org));
                if (lines.Count == 0) _rows.AddChild(Ui.Lbl("Sem divisões"));
            }

            bool hasPlayer = pid is not null, anyMine = _mine.Count > 0;
            _play.Visible = !hasPlayer && ctrl is not null;
            if (_play.Visible) _play.Text = $"Jogar como {ctrl!.Name}";
            _all.Visible = _move.Visible = _stop.Visible = _disband.Visible = hasPlayer && anyMine;
            bool canWar = hasPlayer && ctrl is not null && ctrl.Id != pid && !w.AreAtWar(pid!.Value, ctrl.Id);
            _war.Visible = canWar;
            if (canWar) { _war.Text = $"Justificar guerra a {ctrl!.Name}"; _warTarget = ctrl.Id; _warDialog.DialogText = $"Justificar objectivo de guerra contra {ctrl.Name}? A guerra declara-se sozinha ao fim da justificação."; }
            bool canNuke = hasPlayer && ctrl is not null && ctrl.Id != pid && w.AreAtWar(pid!.Value, ctrl.Id)
                           && w.Countries.TryGetValue(pid.Value, out var meN) && meN.Nukes > 0;
            _nuke.Visible = canNuke;
            if (canNuke) { _nukeTarget = r.Id; _nukeDialog.DialogText = $"Lançar uma ogiva nuclear sobre {r.Name}? As divisões e a infra-estrutura de lá ficam arrasadas; a tua estabilidade também sofre (opinião mundial)."; }
            _produce.Visible = hasPlayer;
            bool canBuild = hasPlayer && r.OwnerId == pid && r.ControllerId == pid && !r.Building
                            && r.Infrastructure < w.Rule("infra_max", 2f) - 1e-4f;
            _build.Visible = canBuild;
            if (canBuild) _build.Text = $"Melhorar infra ({w.Rule("infra_build_cost", 40f):0})";
            _retreat.Visible = hasPlayer && battle is not null
                && r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid);
            _auto.Visible = hasPlayer && _selected.Count > 0;
            if (_auto.Visible)
                _auto.Text = _selected.Any(id => w.Divisions.TryGetValue(id, out var d) && !d.AutoAdvance)
                    ? "⚑ Avanço auto" : "⚑ Parar avanço";
            bool canFort = hasPlayer && r.OwnerId == pid && r.ControllerId == pid && !r.FortBuilding
                           && r.Fort < (int)w.Rule("fort_max", 5f);
            _fort.Visible = canFort;
            if (canFort) _fort.Text = $"Fortificar ({w.Rule("fort_build_cost", 30f):0})";
            bool canBld = hasPlayer && r.OwnerId == pid && r.ControllerId == pid && r.Project is null;
            var bldKey = !canBld ? "" : r.Id + "|" + string.Join(",", w.BuildingDefs.Values
                .Where(d => (!d.Coastal || r.Coastal) && r.Buildings.GetValueOrDefault(d.Id) < d.MaxLevel).Select(d => d.Id + ":" + r.Buildings.GetValueOrDefault(d.Id)));
            if (bldKey != _bldKey)
            {
                _bldKey = bldKey;
                Ui.Clear(_bld);
                if (canBld)
                    foreach (var d in w.BuildingDefs.Values.OrderBy(d => d.Id))
                    {
                        if (d.Coastal && !r.Coastal) continue;      // porto só na costa: nem se oferece o botão
                        if (r.Buildings.GetValueOrDefault(d.Id) >= d.MaxLevel) continue;
                        var bid = d.Id;
                        _bld.AddChild(Ui.Btn($"{d.Name} {r.Buildings.GetValueOrDefault(bid) + 1} ({d.Cost:0}, {d.Days:0} d)",
                            () => _game.RunWhenIdle(() => OnBuilding(bid))));
                    }
            }
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
        // travessia marítima em curso: quem vai no barco desembarca com menos organização
        if (d.TargetRegionId is int hop && w.IsSeaHop(d.RegionId, hop)) s += "   🌊";
        if (d.Xp >= 1f) s += $"   XP {d.Xp:0}";
        if (d.Medals.Count > 0) s += "   🎖" + (d.Medals.Count > 1 ? "×" + d.Medals.Count : "");
        if (w.GroupOf(d.Id) is ArmyGroup g)   // às ordens de um grupo de exércitos: o símbolo diz a postura
            s += (g.Stance == GroupStance.Advance ? "   ▶ " : g.Stance == GroupStance.Defend ? "   ⛨ " : "   ■ ") + g.Name;
        if (d.AutoAdvance) s += "   ⚑";
        if (w.InBattle(d.Id)) s += "   " + RegionRenderer.BattleMark.Trim();
        return s;
    }

    // Divisão do jogador = CheckBox com o texto (linha inteira é alvo de toque); outras = Label.
    /// <summary>Linha da divisão: o texto (caixa de selecção se for nossa) e, por baixo, duas barras finas —
    /// verde para os efectivos, azul para a organização. Ver o estado da tropa sem ler números.</summary>
    private Control Row(int id, bool mine, string text, float hp, float org)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        if (mine)
        {
            var cb = Ui.Grow(new CheckBox { Text = text, ButtonPressed = _selected.Contains(id), CustomMinimumSize = new Vector2(0, 48) });
            cb.AddThemeFontSizeOverride("font_size", Ui.Font);
            cb.Toggled += on => { try { if (on) _selected.Add(id); else _selected.Remove(id); UpdateButtons(); } catch (Exception ex) { GD.PushError(ex.ToString()); } };
            _boxes[id] = cb;
            v.AddChild(cb);
        }
        else
        {
            var l = Ui.Grow(Ui.Lbl(text)); l.CustomMinimumSize = new Vector2(0, 40);
            v.AddChild(l);
        }
        var bars = new HBoxContainer();
        bars.AddThemeConstantOverride("separation", 8);
        bars.AddChild(Ui.Grow(Ui.Bar(hp / 100f, Ui.Good)));
        bars.AddChild(Ui.Grow(Ui.Bar(org / 100f, Ui.Accent)));
        v.AddChild(bars);
        return v;
    }

    private void UpdateButtons()
    {
        _move.Disabled = _selected.Count == 0 || MoveMode;
        _stop.Disabled = _disband.Disabled = _selected.Count == 0;
    }
}
