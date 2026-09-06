using Godot;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Um Polygon2D por anel; cor pelo controlador (RegionCaptured via EventBus). Marcadores com o nº de
/// divisões por região e contorno da região seleccionada. Só lê o World em Refresh — chamado com o mundo parado.</summary>
public partial class RegionRenderer : Node2D
{
    public const string BattleMark = "⚔ ";

    private readonly Dictionary<int, List<Polygon2D>> _byRegion = new();
    private readonly Dictionary<int, Color> _countryColor = new();
    private readonly Dictionary<int, LabelSettings> _labelStyle = new();
    private readonly Dictionary<int, Node2D> _markers = new();      // região → Node2D (escala 1/zoom) com um Label
    private Node2D _highlightRoot = null!, _markerRoot = null!;
    private Game _game = null!;
    private float _markerScale = 1f;

    public void Build(SqlWorldRepository repo, IDatabase staticDb, Game game)
    {
        _game = game;
        foreach (var r in staticDb.Query("SELECT id,color FROM country"))
            _countryColor[Convert.ToInt32(r["id"])] = new Color((string?)r["color"] ?? "#cccccc");

        foreach (var (regionId, pts) in repo.ReadPolygons())
        {
            var v = new Vector2[pts.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(pts[2 * i], pts[2 * i + 1]);
            var poly = new Polygon2D { Polygon = v, Color = ColorFor(regionId) };
            var outline = new Line2D { Points = v.Append(v[0]).ToArray(), Width = 1.2f, DefaultColor = new Color(0, 0, 0, 0.45f) };
            AddChild(poly); poly.AddChild(outline);
            if (!_byRegion.TryGetValue(regionId, out var list)) _byRegion[regionId] = list = new();
            list.Add(poly);
        }
        // Por cima dos polígonos: primeiro o realce, depois os marcadores.
        _highlightRoot = new Node2D { Name = "Highlight" }; AddChild(_highlightRoot);
        _markerRoot = new Node2D { Name = "Markers" }; AddChild(_markerRoot);
        game.World.Events.Subscribe<RegionCaptured>(e => { int id = e.RegionId; Callable.From(() => Recolor(id)).CallDeferred(); });
    }

    private Color ColorFor(int regionId) =>
        _game.World.Regions.TryGetValue(regionId, out var r) ? _countryColor.GetValueOrDefault(r.ControllerId, Colors.Gray) : Colors.Gray;

    // Chega na main thread (deferred); a leitura do controlador espera pelo fim do tick.
    private void Recolor(int regionId) => _game.RunWhenIdle(() =>
    {
        if (_byRegion.TryGetValue(regionId, out var polys)) foreach (var p in polys) p.Color = ColorFor(regionId);
    });

    /// <summary>Marcadores e realce à escala inversa do zoom (clamp 1..6) para se lerem em qualquer zoom.</summary>
    public void SetZoom(float zoom)
    {
        _markerScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 1f, 6f);
        foreach (var m in _markers.Values) m.Scale = Vector2.One * _markerScale;
        foreach (var c in _highlightRoot.GetChildren()) if (c is Line2D l) l.Width = 4f * _markerScale;
    }

    /// <summary>Um Label por região com divisões: "N" ou "⚔ N" se há batalha, cor do controlador. Esconde os vazios.</summary>
    public void Refresh()
    {
        try
        {
            var w = _game.World;
            var battles = new HashSet<int>(w.ActiveBattles.Select(b => b.RegionId));
            var seen = new HashSet<int>();
            foreach (var r in w.Regions.Values)
            {
                if (r.DivisionIds.Count == 0) continue;
                seen.Add(r.Id);
                if (!_markers.TryGetValue(r.Id, out var m)) _markers[r.Id] = m = NewMarker(r);
                var label = (Label)m.GetChild(0);
                label.Text = (battles.Contains(r.Id) ? BattleMark : "") + r.DivisionIds.Count;
                label.LabelSettings = StyleFor(r.ControllerId);
                m.Visible = true;
            }
            foreach (var (id, m) in _markers) if (!seen.Contains(id)) m.Visible = false;
        }
        catch (Exception ex) { GD.PushError("RegionRenderer.Refresh: " + ex); }
    }

    private Node2D NewMarker(Region r)
    {
        var m = new Node2D { Position = new Vector2(r.CenterX, r.CenterY), Scale = Vector2.One * _markerScale };
        m.AddChild(new Label
        {
            Size = new Vector2(160, 48), Position = new Vector2(-80, -24),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _markerRoot.AddChild(m);
        return m;
    }

    private LabelSettings StyleFor(int countryId)
    {
        if (_labelStyle.TryGetValue(countryId, out var s)) return s;
        return _labelStyle[countryId] = new LabelSettings
        {
            FontSize = 28, FontColor = _countryColor.GetValueOrDefault(countryId, Colors.White),
            OutlineSize = 6, OutlineColor = Colors.Black,
        };
    }

    /// <summary>Contorno branco na região seleccionada (null = nenhuma); a anterior volta ao normal.</summary>
    public void Highlight(int? regionId)
    {
        foreach (var c in _highlightRoot.GetChildren()) { _highlightRoot.RemoveChild(c); c.QueueFree(); }
        if (regionId is null || !_byRegion.TryGetValue(regionId.Value, out var polys)) return;
        foreach (var p in polys)
            _highlightRoot.AddChild(new Line2D { Points = p.Polygon.Append(p.Polygon[0]).ToArray(), Width = 4f * _markerScale, DefaultColor = Colors.White });
    }

    /// <summary>Hit-test para toque: região cujo polígono contém o ponto (mundo).</summary>
    public int? RegionAt(Vector2 worldPos)
    {
        foreach (var (id, polys) in _byRegion)
            foreach (var p in polys)
                if (Geometry2D.IsPointInPolygon(worldPos, p.Polygon)) return id;
        return null;
    }
}
