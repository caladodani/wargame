using Godot;
using WarGame.Core.Commands;

namespace WarGame.Presentation;

/// <summary>Selecção de regiões — a única do jogo: um toque simples troca a marca para a região tocada (se
/// tiver divisões minhas); um toque longo (ou botão direito no PC) acrescenta/tira uma marca sem apagar as
/// outras, para mover vários exércitos de uma vez; duplo toque num destino sem marcas move as divisões todas
/// para lá SEM apagar a selecção — a seta da rota e as marcas ficam, como pede o jogo original. Só um toque
/// simples noutra região com divisões minhas é que troca a selecção. Barra no topo mostra o total e dá
/// Parar/Dissolver/Avanço automático/Limpar sem abrir painel nenhum.
/// Lê o World só via RunWhenIdle e muta só por Dispatch.</summary>
public partial class ArmySelect : PanelContainer
{
    private Game _game = null!;
    private MapView _map = null!;
    private Label _label = null!;
    private Button _stop = null!, _disband = null!, _auto = null!;
    private readonly HashSet<int> _sel = new();

    public bool Active => _sel.Count > 0;
    /// <summary>Regiões marcadas — o painel Exércitos usa a mesma marcação para recrutar divisões para um grupo.</summary>
    public IReadOnlyCollection<int> RegionIds => _sel;
    public int RegionCount => _sel.Count;

    /// <summary>Divisões do país `countryId` que estão nas regiões marcadas.</summary>
    public int DivisionCount(WarGame.Core.Model.World w, int countryId) =>
        _sel.Sum(rid => w.Regions.TryGetValue(rid, out var r)
            ? r.DivisionIds.Count(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == countryId) : 0);

    public void Setup(Game game, MapView map)
    {
        _game = game; _map = map;
        Visible = false;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        OffsetTop = 128;   // altura de partida; o Hud acerta-a pela altura real da barra de topo
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.16f, 0.14f, 0.04f, 0.92f)));
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 8); AddChild(h);
        _label = Ui.Lbl("", 18); h.AddChild(_label);
        _stop = Ui.Btn("Parar", () => _game.RunWhenIdle(StopAll)); h.AddChild(_stop);
        _disband = Ui.Btn("Dissolver", () => _game.RunWhenIdle(DisbandAll), 0f, Ui.Kind.Danger); h.AddChild(_disband);
        _auto = Ui.Btn("⚑ Avanço auto", () => _game.RunWhenIdle(ToggleAutoAdvance)); h.AddChild(_auto);
        h.AddChild(Ui.Btn("Limpar", () => _game.RunWhenIdle(Clear)));
    }

    /// <summary>A barra de topo cresceu (mostradores dobrados numa linha nova): a chapa da selecção
    /// acompanha, senão nasce por trás dela.</summary>
    public void PlaceUnder(float y) => OffsetTop = y;

    /// <summary>Toque simples: região com divisões minhas troca a selecção por só ela; sem divisões minhas
    /// não mexe em nada (é a ficha da região que responde por essa, no rodapé).</summary>
    public void Tap(int regionId) => _game.RunWhenIdle(() =>
    {
        if (!Mine(regionId)) return;
        _sel.Clear(); _sel.Add(regionId);
        Refresh();
    });

    /// <summary>Toque longo: marca/desmarca uma região com divisões minhas, sem apagar as outras marcas.</summary>
    public void LongPress(int regionId) => _game.RunWhenIdle(() =>
    {
        if (!Mine(regionId)) { _game.Notify("Sem divisões tuas aí — o toque longo marca regiões de origem"); return; }
        if (!_sel.Add(regionId)) _sel.Remove(regionId);
        Refresh();
    });

    /// <summary>Duplo toque: com regiões marcadas, é o destino; sem marcas não faz nada (o duplo toque sem
    /// selecção abre a ficha da região, decisão do Hud).</summary>
    public void DoubleTap(int regionId) => _game.RunWhenIdle(() =>
    {
        if (Active && !_sel.Contains(regionId)) MoveTo(regionId);
    });

    private bool Mine(int regionId)
    {
        if (_game.PlayerId is not int pid) return false;
        var w = _game.World;
        return w.Regions.TryGetValue(regionId, out var r)
            && r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid);
    }

    /// <summary>Uma ordem de movimento por divisão do jogador em cada região marcada. A selecção fica: a
    /// tropa continua marcada e a rota desenha-se por cima da marcha (regra do jogo original).</summary>
    public void MoveTo(int targetRegionId)
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        string? first = null; int n = 0;
        foreach (var rid in _sel.ToList())
        {
            if (rid == targetRegionId || !w.Regions.TryGetValue(rid, out var r)) continue;
            foreach (var id in r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid).ToList())
            {
                var err = _game.Dispatch(new MoveDivisionCommand(pid, id, targetRegionId));
                if (err is null) n++; else first ??= err;
            }
        }
        string dest = w.Regions.TryGetValue(targetRegionId, out var t) ? t.Name : "R" + targetRegionId;
        _game.Notify(first ?? $"{n} divisões a caminho de {dest}");
        Refresh();
    }

    private void StopAll()
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        string? first = null;
        foreach (var rid in _sel.ToList())
        {
            if (!w.Regions.TryGetValue(rid, out var r)) continue;
            foreach (var id in r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid).ToList())
                first ??= _game.Dispatch(new StopDivisionCommand(pid, id));
        }
        if (first is not null) _game.Notify(first);
        Refresh();
    }

    private void DisbandAll()
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        string? first = null; int n = 0;
        foreach (var rid in _sel.ToList())
        {
            if (!w.Regions.TryGetValue(rid, out var r)) continue;
            foreach (var id in r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid).ToList())
            {
                var err = _game.Dispatch(new DisbandDivisionCommand(pid, id));
                if (err is null) n++; else first ??= err;
            }
        }
        _game.Notify(first ?? $"{n} divisões dissolvidas");
        Refresh();
    }

    /// <summary>Liga/desliga o avanço automático nas divisões marcadas: se alguma ainda não o tem, liga em
    /// todas; se já o têm todas, desliga.</summary>
    private void ToggleAutoAdvance()
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        var ids = _sel.SelectMany(rid => w.Regions.TryGetValue(rid, out var r)
            ? r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid) : Enumerable.Empty<int>()).ToList();
        bool on = ids.Any(id => w.Divisions.TryGetValue(id, out var d) && !d.AutoAdvance);
        string? first = null;
        foreach (var id in ids) first ??= _game.Dispatch(new SetAutoAdvanceCommand(pid, id, on));
        if (first is not null) _game.Notify(first);
        Refresh();
    }

    public void Clear() { _sel.Clear(); Refresh(); }

    private void Refresh()
    {
        _map.Regions.HighlightMulti(_sel);
        if (!Active) { Visible = false; return; }
        var w = _game.World; int pid = _game.PlayerId ?? -1;
        int divs = _sel.Sum(rid => w.Regions.TryGetValue(rid, out var r)
            ? r.DivisionIds.Count(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid) : 0);
        _label.Text = $"⚔ {_sel.Count} regiões · {divs} divisões — duplo toque no destino move";
        var ids = _sel.SelectMany(rid => w.Regions.TryGetValue(rid, out var r)
            ? r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid) : Enumerable.Empty<int>()).ToList();
        _auto.Text = ids.Count == 0 || ids.Any(id => w.Divisions.TryGetValue(id, out var d) && !d.AutoAdvance)
            ? "⚑ Avanço auto" : "⚑ Parar avanço";
        Visible = true;
    }
}
