using Godot;
using WarGame.Core.Commands;

namespace WarGame.Presentation;

/// <summary>Selecção múltipla de regiões: toque longo (ou botão direito no PC) numa região com divisões
/// do jogador alterna a marca (contorno amarelo); duplo toque noutra região, com marcas activas, move
/// TODAS as divisões marcadas para lá (estilo RTS). Barra no topo mostra o total e deixa limpar.
/// Lê o World só via RunWhenIdle e muta só por Dispatch.</summary>
public partial class ArmySelect : PanelContainer
{
    private Game _game = null!;
    private MapView _map = null!;
    private Label _label = null!;
    private readonly HashSet<int> _sel = new();

    public bool Active => _sel.Count > 0;

    public void Setup(Game game, MapView map)
    {
        _game = game; _map = map;
        Visible = false;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        OffsetTop = 64;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.16f, 0.14f, 0.04f, 0.92f)));
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 12); AddChild(h);
        _label = Ui.Lbl("", 18); h.AddChild(_label);
        h.AddChild(Ui.Btn("Limpar", () => _game.RunWhenIdle(Clear)));
    }

    /// <summary>Toque longo: marca/desmarca uma região com divisões minhas.</summary>
    public void LongPress(int regionId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        bool mine = w.Regions.TryGetValue(regionId, out var r)
            && r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid);
        if (!mine) { _game.Notify("Sem divisões tuas aí — o toque longo marca regiões de origem"); return; }
        if (!_sel.Add(regionId)) _sel.Remove(regionId);
        Refresh();
    });

    /// <summary>Duplo toque: com regiões marcadas, é o destino; sem marcas não faz nada (o toque normal
    /// já abriu o painel da região).</summary>
    public void DoubleTap(int regionId) => _game.RunWhenIdle(() =>
    {
        if (Active && !_sel.Contains(regionId)) MoveTo(regionId);
    });

    /// <summary>Uma ordem de movimento por divisão do jogador em cada região marcada; limpa no fim.</summary>
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
        Clear();
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
        Visible = true;
    }
}
