using Godot;
using WarGame.Core.Data;
using WarGame.Core.Events;

namespace WarGame.Presentation;

/// <summary>Um Polygon2D por anel; cor pelo controlador. Reage a RegionCaptured via EventBus.</summary>
public partial class RegionRenderer : Node2D
{
    private readonly Dictionary<int, List<Polygon2D>> _byRegion = new();
    private readonly Dictionary<int, Color> _countryColor = new();

    public void Build(SqlWorldRepository repo, IDatabase staticDb, Game game)
    {
        foreach (var r in staticDb.Query("SELECT id,color FROM country"))
            _countryColor[Convert.ToInt32(r["id"])] = new Color((string?)r["color"] ?? "#cccccc");

        foreach (var (regionId, pts) in repo.ReadPolygons())
        {
            var v = new Vector2[pts.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(pts[2 * i], pts[2 * i + 1]);
            var poly = new Polygon2D { Polygon = v, Color = ColorFor(game, regionId) };
            var outline = new Line2D { Points = v.Append(v[0]).ToArray(), Width = 1.2f, DefaultColor = new Color(0, 0, 0, 0.45f) };
            AddChild(poly); poly.AddChild(outline);
            if (!_byRegion.TryGetValue(regionId, out var list)) _byRegion[regionId] = list = new();
            list.Add(poly);
        }
        game.World.Events.Subscribe<RegionCaptured>(e => CallDeferred(nameof(Recolor), e.RegionId));
    }

    private Color ColorFor(Game game, int regionId) =>
        _countryColor.GetValueOrDefault(game.World.Regions[regionId].ControllerId, Colors.Gray);

    private void Recolor(int regionId)
    {
        var game = GetNode<Game>("/root/Game");
        foreach (var p in _byRegion[regionId]) p.Color = ColorFor(game, regionId);
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
