using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Ficha da região, aberta por duplo toque quando não há nada seleccionado (a ArmySelect trata do
/// toque simples e do movimento). Mostra o estado e as divisões presentes, e dá as ordens que não cabem no
/// toque no mapa: declarar guerra, atacar com a bomba, jogar como este país, ver a batalha, retirar e
/// sabotar a retaguarda inimiga. Mover, parar, dissolver e construir saem daqui — vivem na barra de selecção
/// e no menu Construir, sem tapar o mapa.
/// Lê o World só em Fill (mundo parado) e muta só por Game.Dispatch.</summary>
public partial class RegionPanel : PanelContainer
{
    private Game _game = null!;
    private MapView _map = null!;
    private ProductionPanel _production = null!;
    private CountryPanel _countryPanel = null!;
    private Label _title = null!, _info = null!;
    private TextureRect _flag = null!;
    private VBoxContainer _rows = null!;
    /// <summary>O Hud liga isto ao ecrã de batalha (o painel não conhece os outros painéis todos).</summary>
    public Action<int>? OnBattle;

    private Button _play = null!, _war = null!, _produce = null!, _retreat = null!;
    private Button _battle = null!;
    private VBoxContainer _sab = null!;       // sabotagem na retaguarda (operações spy_op de scope region)
    private string _sabKey = "";
    private ConfirmationDialog _warDialog = null!;
    private Button _nuke = null!;
    private ConfirmationDialog _nukeDialog = null!;
    private int _nukeTarget;
    private int _regionId, _warTarget;
    private string _lastKey = "";

    public void Setup(Game game, MapView map, ProductionPanel production, CountryPanel countryPanel)
    {
        _game = game; _map = map; _production = production; _countryPanel = countryPanel;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 1f)));
        var v = new VBoxContainer(); AddChild(v);
        var titleRow = new HBoxContainer(); v.AddChild(titleRow);
        _flag = Flags.Rect(26); titleRow.AddChild(_flag);
        _title = Ui.Lbl("", 22); titleRow.AddChild(Ui.Grow(_title));
        titleRow.AddChild(Ui.Btn("Fechar", Close));   // sempre no canto superior direito, como nos outros painéis
        _info = Ui.Lbl("", 18); v.AddChild(_info);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _rows = Ui.Grow(new VBoxContainer()); scroll.AddChild(_rows);

        var actions = new HFlowContainer(); v.AddChild(actions);
        _sab = new VBoxContainer(); v.AddChild(_sab);          // sabotagem: só aparece em região inimiga
        _play = Ui.Btn("", () => _game.RunWhenIdle(OnPlay)); actions.AddChild(_play);
        _war = Ui.Btn("", () => _warDialog.PopupCentered()); actions.AddChild(_war);
        _produce = Ui.Btn("Produzir", () => { Close(); _production.Open(); }); actions.AddChild(_produce);
        _retreat = Ui.Btn("Retirar", () => _game.RunWhenIdle(OnRetreat)); actions.AddChild(_retreat);
        _battle = Ui.Btn("⚔ Ver batalha", () => { int id = _regionId; Close(); OnBattle?.Invoke(id); }, 0f, Ui.Kind.Primary);
        actions.AddChild(_battle);
        _nuke = Ui.Btn("☢ Ataque nuclear", () => _nukeDialog.PopupCentered()); actions.AddChild(_nuke);
        actions.AddChild(Ui.Btn("País", () => _game.RunWhenIdle(() =>
        {
            if (!_game.World.Regions.TryGetValue(_regionId, out var r)) return;
            Close(); _countryPanel.Open(r.ControllerId);
        })));
        _warDialog = Ui.Dialog(this, () => _game.RunWhenIdle(OnWar));
        _nukeDialog = Ui.Dialog(this, () => _game.RunWhenIdle(OnNuke));
    }

    public void Open(int regionId)
    {
        _regionId = regionId; _lastKey = "";
        _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); });
    }

    /// <summary>Relê o estado (chamado pelo Hud com o mundo parado). Sem efeito se fechado.</summary>
    public void Refresh() { if (Visible) Fill(); }

    public void Close()
    {
        Visible = false;
        _map.Regions.Highlight(null);
    }

    /// <summary>Manda uma equipa rebentar alguma coisa nesta região inimiga.</summary>
    private void OnSabotage(string opId)
    {
        if (_game.PlayerId is not int pid || !_game.World.Regions.TryGetValue(_regionId, out var r)) return;
        var err = _game.Dispatch(new StartSpyOpCommand(pid, r.ControllerId, opId, r.Id));
        _game.Notify(err ?? "Equipa a caminho.");
        _sabKey = "";
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
            _title.Text = $"{r.Name}  ·  {GroundView.Name(w, r.Terrain)}{(r.Coastal ? " ⚓" : "")}";
            var info = $"{ctrl?.Name ?? "—"}{(r.ControllerId != r.OwnerId ? " (ocupada)" : "")}  ·  {r.Population / 1e6f:0.0} M hab.  ·  Infra ×{r.Infrastructure:0.00}  ·  💰 {EconomySystem.RegionIncome(w, r):0.00}/dia";
            if (r.Infrastructure < r.BaseInfrastructure - 1e-4f) info += $"  ·  🔧 danificada (repõe até ×{r.BaseInfrastructure:0.00})";
            if (r.Fort > 0) info += $"  ·  🏰 Forte {r.Fort}";
            foreach (var (res, amount) in r.Resources.OrderBy(kv => kv.Key))
                if (w.ResourceDefs.TryGetValue(res, out var rd)) info += $"  ·  {rd.Name} {amount:0}";
            foreach (var (bid, lvl) in r.Buildings.OrderBy(kv => kv.Key))
                if (lvl > 0 && w.BuildingDefs.TryGetValue(bid, out var bd)) info += $"  ·  {bd.Name} {lvl}";
            if (r.Project is string proj && w.BuildingDefs.TryGetValue(proj, out var pd))
                info += $"  🏗 {pd.Name}: {(int)MathF.Ceiling(pd.Days - r.ProjectProgress)} dias";
            if (PortView.RegionLine(w, r) is string quay && quay.Length > 0) info += "  ·  " + quay;
            if (SeasonView.RegionLine(w, r) is string season && season.Length > 0) info += "  ·  " + season;
            if (r.Resistance > 0.005f) info += $"  ·  ✊ resistência {r.Resistance:P0}";
            if (r.Integration > 0.5f) info += $"  ·  🤝 integração {r.Integration / MathF.Max(1f, w.Rule("integration_days", 150f)):P0}";
            // fábricas civis: a obra pode ser recusada com o cofre cheio, e sem isto não se percebia porquê
            if (pid is int owner && r.OwnerId == owner && r.ControllerId == owner)
            {
                var yards = Industry.Of(w, owner);
                info += $"  ·  🏭 {yards.FreeCivil} de {yards.Civil} fábricas civis livres";
            }
            // com o mapa pintado por uma conta (abastecimento, resistência...), a ficha diz o número exacto
            if (w.MapModeDefs.GetValueOrDefault(_map.Regions.Mode) is MapModeDef mode && mode.Metric != "owner"
                && MapModes.Text(w, pid ?? 0, r, mode.Metric) is string mText && mText.Length > 0)
                info += $"  ·  {mode.Name.ToLowerInvariant()} {mText}";
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
            _info.Text = info;

            // Divisões presentes: as do jogador primeiro, depois as outras — sem caixa de selecção, é
            // a ArmySelect quem escolhe agora (toque simples no mapa).
            // nevoeiro: fora do que temos como ver, a guarnição alheia não se conta
            bool fogged = pid is int viewer && !Vision.Sees(w, viewer, r);
            var divs = (pid is int seer ? Vision.DivisionsIn(w, seer, r)
                                       : r.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>())
                        .OrderByDescending(d => d.CountryId == pid).ThenBy(d => d.Id).ToList();
            var lines = divs.Select(d => (d.Id, text: Line(w, d), d.Hp, d.Org)).ToList();
            // a ficha do chão: muda com o terreno, o rio, o forte, a estrada, a estação e quem olha
            int ground = pid ?? r.ControllerId;
            var groundKey = $"g{ground}:{r.Terrain}:{r.River}:{r.Fort}:{r.Infrastructure:0.00}:{w.SeasonMove:0.00}|";
            var key = groundKey + (fogged ? "fog|" : "") + string.Join("|", lines.Select(l => l.Id + ":" + l.text));
            if (key != _lastKey)   // só reconstrói as linhas quando algo mudou (evita saltos de scroll a 4×)
            {
                _lastKey = key;
                Ui.Clear(_rows);
                _rows.AddChild(GroundView.Card(w, r, ground));
                foreach (var (_, text, hp, org) in lines) _rows.AddChild(Row(text, hp, org));
                if (fogged)
                {
                    var fog = Ui.Lbl($"🌫 {Vision.Why(w, pid!.Value, r)}", 16);
                    fog.AddThemeColorOverride("font_color", Ui.TextDim);
                    _rows.AddChild(fog);
                    _rows.AddChild(Ui.Lbl("Espia o país ou chega à fronteira para veres a guarnição", 14));
                }
                else if (lines.Count == 0) _rows.AddChild(Ui.Lbl("Sem divisões"));
            }

            bool hasPlayer = pid is not null;
            _play.Visible = !hasPlayer && ctrl is not null;
            if (_play.Visible) _play.Text = $"Jogar como {ctrl!.Name}";
            bool canWar = hasPlayer && ctrl is not null && ctrl.Id != pid && !w.AreAtWar(pid!.Value, ctrl.Id);
            _war.Visible = canWar;
            if (canWar) { _war.Text = $"Justificar guerra a {ctrl!.Name}"; _warTarget = ctrl.Id; _warDialog.DialogText = $"Justificar objectivo de guerra contra {ctrl.Name}? A guerra declara-se sozinha ao fim da justificação."; }
            bool canNuke = hasPlayer && ctrl is not null && ctrl.Id != pid && w.AreAtWar(pid!.Value, ctrl.Id)
                           && w.Countries.TryGetValue(pid.Value, out var meN) && meN.Nukes > 0;
            _nuke.Visible = canNuke;
            if (canNuke) { _nukeTarget = r.Id; _nukeDialog.DialogText = $"Lançar uma ogiva nuclear sobre {r.Name}? As divisões e a infra-estrutura de lá ficam arrasadas; a tua estabilidade também sofre (opinião mundial)."; }
            _produce.Visible = hasPlayer;
            _battle.Visible = battle is not null;     // o ecrã dos dois lados só faz sentido com batalha a decorrer
            _retreat.Visible = hasPlayer && battle is not null
                && r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid);
            // sabotagem na retaguarda: o que se pode mandar rebentar aqui, e a equipa que já vai a caminho
            var running = pid is int me ? SabotageView.Running(w, me, r.Id) : null;
            var sabKey = pid is int p2
                ? $"{r.Id}|{r.ControllerId}|{r.Fort}|{r.Infrastructure:0.00}|{string.Join(",", r.Buildings.Select(kv => kv.Key + ":" + kv.Value))}|{running?.OpId}:{(int)(running?.DaysLeft ?? 0f)}|{(int)(w.Countries.TryGetValue(p2, out var mc) ? mc.Money : 0f)}|g{CounterIntelSystem.Guards(w, r.ControllerId, r)}"
                : "";
            if (sabKey != _sabKey)
            {
                _sabKey = sabKey;
                Ui.Clear(_sab);
                if (pid is int p3 && SabotageView.Card(w, p3, r, OnSabotage) is VBoxContainer sab) _sab.AddChild(sab);
                // a nossa própria retaguarda: quem guarda isto e o que apanhamos aqui
                if (pid is int p4 && SabotageView.Rear(w, p4, r) is VBoxContainer rear) _sab.AddChild(rear);
            }
        }
        catch (Exception ex) { GD.PushError("RegionPanel.Fill: " + ex); }
    }

    private static string Line(World w, Division d)
    {
        string name = DivisionView.Title(w, d);      // com o nome de guerra, se já o ganhou
        var tag = w.Countries.TryGetValue(d.CountryId, out var c) ? c.Tag : "?";
        var s = $"{tag} {name}   HP {d.Hp:0}  Org {d.Org:0}  Sup {d.Supply:0.0}";
        if (d.DestinationRegionId is int dest) s += $"   → {(w.Regions.TryGetValue(dest, out var rr) ? rr.Name : "R" + dest)}";
        // travessia marítima em curso: quem vai no barco desembarca com menos organização
        if (d.TargetRegionId is int hop && w.IsSeaHop(d.RegionId, hop)) s += "   🌊";
        if (d.Xp >= 1f) s += $"   XP {d.Xp:0}";
        if (d.Medals.Count > 0) s += "   🎖" + (d.Medals.Count > 1 ? "×" + d.Medals.Count : "");
        if (d.Honour is string hon && w.HonourDefs.TryGetValue(hon, out var hd)) s += "   " + DivisionView.Chevrons(hd.Sort);
        if (w.GroupOf(d.Id) is ArmyGroup g)   // às ordens de um grupo de exércitos: o símbolo diz a postura
            s += (g.Stance switch
            {
                GroupStance.Advance => "   ▶ ",
                GroupStance.Defend => "   ⛨ ",
                GroupStance.Reserve => "   ⏸ ",
                _ => "   ■ ",
            }) + g.Name;
        if (d.AutoAdvance) s += "   ⚑";
        if (w.InBattle(d.Id)) s += "   " + RegionRenderer.BattleMark.Trim();
        return s;
    }

    /// <summary>Linha da divisão: o texto e, por baixo, duas barras finas — verde para os efectivos, azul
    /// para a organização. Ver o estado da tropa sem ler números.</summary>
    private static Control Row(string text, float hp, float org)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        var l = Ui.Grow(Ui.Lbl(text)); l.CustomMinimumSize = new Vector2(0, 40);
        v.AddChild(l);
        var bars = new HBoxContainer();
        bars.AddThemeConstantOverride("separation", 8);
        bars.AddChild(Ui.Grow(Ui.Bar(hp / 100f, Ui.Good)));
        bars.AddChild(Ui.Grow(Ui.Bar(org / 100f, Ui.Accent)));
        v.AddChild(bars);
        return v;
    }
}
