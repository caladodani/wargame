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
    public const string FortMark = "■";
    /// <summary>Região ocupada com resistência relevante (≥ metade do caminho para a revolta).</summary>
    public const string ResistMark = "✊";
    /// <summary>Capital de um país vivo.</summary>
    public const string CapitalMark = "★";
    /// <summary>Região que o jogador exigiu numa guerra (objectivo de guerra).</summary>
    public const string GoalMark = "🎯";
    /// <summary>Região com porto: é por aqui que o abastecimento salta o mar.</summary>
    public const string PortMark = "⚓";

    private readonly Dictionary<int, List<Polygon2D>> _byRegion = new();
    private readonly Dictionary<int, List<Line2D>> _borders = new();   // moldura colorida por anel
    private readonly Dictionary<int, Color> _countryColor = new();
    private readonly Dictionary<int, LabelSettings> _labelStyle = new();
    private readonly Dictionary<int, StyleBoxFlat> _pillStyle = new();
    private readonly Dictionary<int, Node2D> _markers = new();      // região → Node2D (escala 1/zoom) com um Label
    private readonly Dictionary<int, Tween> _pulses = new();        // região em batalha → animação do marcador
    private Node2D _highlightRoot = null!, _multiRoot = null!, _markerRoot = null!, _goalRoot = null!;
    private string _goalKey = "";                                   // objectivos desenhados (evita refazer o contorno todos os dias)
    private Tween? _goalPulse;
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
            var ring = v.Append(v[0]).ToArray();
            // duas linhas por anel: uma grossa na cor do controlador (dá relevo à fronteira nacional)
            // e uma fina preta por cima, que separa regiões do mesmo país.
            var border = new Line2D { Points = ring, Width = 3.5f, DefaultColor = BorderFor(regionId) };
            var outline = new Line2D { Points = ring, Width = 1.2f, DefaultColor = new Color(0, 0, 0, 0.45f) };
            AddChild(poly); poly.AddChild(border); poly.AddChild(outline);
            if (!_byRegion.TryGetValue(regionId, out var list)) _byRegion[regionId] = list = new();
            if (!_borders.TryGetValue(regionId, out var blist)) _borders[regionId] = blist = new();
            list.Add(poly); blist.Add(border);
        }
        // Por cima dos polígonos: primeiro o realce, depois os marcadores.
        _highlightRoot = new Node2D { Name = "Highlight" }; AddChild(_highlightRoot);
        _multiRoot = new Node2D { Name = "MultiHighlight" }; AddChild(_multiRoot);
        _goalRoot = new Node2D { Name = "Goals" }; AddChild(_goalRoot);
        _markerRoot = new Node2D { Name = "Markers" }; AddChild(_markerRoot);
        game.World.Events.Subscribe<RegionCaptured>(e => { int id = e.RegionId; Callable.From(() => Recolor(id)).CallDeferred(); });
        // Capitulação transfere regiões em bloco sem RegionCaptured — pinta tudo de novo.
        game.World.Events.Subscribe<CountryCapitulated>(_ => Callable.From(RecolorAll).CallDeferred());
        game.World.Events.Subscribe<WhitePeaceSigned>(_ => Callable.From(RecolorAll).CallDeferred());
        game.World.Events.Subscribe<PeaceSigned>(_ => Callable.From(RecolorAll).CallDeferred());
    }

    /// <summary>Forma de cada região (um par por anel): o mini-mapa desenha as mesmas em ponto pequeno.</summary>
    public IEnumerable<(int RegionId, Vector2[] Points)> Shapes()
    {
        foreach (var (id, polys) in _byRegion)
            foreach (var p in polys) yield return (id, p.Polygon);
    }

    /// <summary>Cor actual de uma região (controlador, escurecida quando é ocupação).</summary>
    public Color ColorOf(int regionId) => ColorFor(regionId);

    /// <summary>Cor do país, tal como sai da base de dados (o Hud usa-a na barra de topo).</summary>
    public Color CountryColor(int countryId) => _countryColor.GetValueOrDefault(countryId, Colors.Gray);

    /// <summary>Cor da moldura da região: a do controlador, clareada, para a fronteira saltar à vista.</summary>
    private Color BorderFor(int regionId) => ColorFor(regionId).Lightened(0.45f) with { A = 0.75f };

    private Color ColorFor(int regionId)
    {
        if (!_game.World.Regions.TryGetValue(regionId, out var r)) return Colors.Gray;
        var c = _countryColor.GetValueOrDefault(r.ControllerId, Colors.Gray);
        return r.ControllerId == r.OwnerId ? c : c.Darkened(0.28f);   // ocupada: tom escuro do ocupante
    }

    private void RecolorAll() => _game.RunWhenIdle(() =>
    {
        foreach (var (id, polys) in _byRegion)
        {
            var col = ColorFor(id); foreach (var p in polys) p.Color = col;
            var bc = BorderFor(id); foreach (var b in _borders[id]) b.DefaultColor = bc;
        }
    });

    // Chega na main thread (deferred); a leitura do controlador espera pelo fim do tick.
    private void Recolor(int regionId) => _game.RunWhenIdle(() =>
    {
        if (_byRegion.TryGetValue(regionId, out var polys)) foreach (var p in polys) p.Color = ColorFor(regionId);
        if (_borders.TryGetValue(regionId, out var borders))
        { var bc = BorderFor(regionId); foreach (var b in borders) b.DefaultColor = bc; }
    });

    /// <summary>Marcadores e realce à escala inversa do zoom (clamp 0.05..6): tamanho constante no ecrã —
    /// ao aproximar encolhem no mapa em vez de crescerem no ecrã.</summary>
    public void SetZoom(float zoom)
    {
        _markerScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 0.05f, 6f);
        foreach (var m in _markers.Values) m.Scale = Vector2.One * _markerScale;
        foreach (var c in _highlightRoot.GetChildren()) if (c is Line2D l) l.Width = 4f * _markerScale;
        foreach (var c in _multiRoot.GetChildren()) if (c is Line2D l2) l2.Width = 4f * _markerScale;
        foreach (var c in _goalRoot.GetChildren()) if (c is Line2D l3) l3.Width = 5f * _markerScale;
    }

    /// <summary>Um Label por região com divisões: "N" ou "⚔ N" se há batalha, "■" se há forte, cor do controlador. Esconde os vazios.</summary>
    public void Refresh()
    {
        try
        {
            var w = _game.World;
            var battles = new HashSet<int>(w.ActiveBattles.Select(b => b.RegionId));
            var portIds = w.BuildingDefs.Values.Where(d => d.SupplyRange > 0f).Select(d => d.Id).ToHashSet();
            var goals = PlayerGoals(w);
            DrawGoals(goals);
            var capitals = new HashSet<int>(w.Countries.Values.Where(c => !c.Capitulated).Select(c => c.CapitalRegionId));
            var seen = new HashSet<int>();
            foreach (var r in w.Regions.Values)
            {
                bool resisting = r.Resistance >= 0.5f;
                bool capital = capitals.Contains(r.Id);
                bool goal = goals.Contains(r.Id);
                bool port = r.Buildings.Any(b => b.Value > 0 && portIds.Contains(b.Key));
                if (r.DivisionIds.Count == 0 && r.Fort == 0 && !resisting && !capital && !goal && !port) continue;
                seen.Add(r.Id);
                if (!_markers.TryGetValue(r.Id, out var m)) _markers[r.Id] = m = NewMarker(r);
                var pill = m.GetNode<PanelContainer>("Center/Pill");
                var label = pill.GetNode<Label>("Text");
                bool fighting = battles.Contains(r.Id);
                label.Text = (goal ? GoalMark : "")
                           + (capital ? CapitalMark : "")
                           + (fighting ? BattleMark : "")
                           + (r.DivisionIds.Count > 0 ? r.DivisionIds.Count.ToString() : "")
                           + (r.Fort > 0 ? FortMark : "")
                           + (port ? PortMark : "")
                           + (resisting ? ResistMark : "");
                label.LabelSettings = StyleFor(r.ControllerId);
                pill.AddThemeStyleboxOverride("panel", PillFor(r.ControllerId));
                Pulse(r.Id, pill, fighting);
                m.Visible = true;
            }
            foreach (var (id, m) in _markers)
                if (!seen.Contains(id)) { m.Visible = false; Pulse(id, null, false); }
        }
        catch (Exception ex) { GD.PushError("RegionRenderer.Refresh: " + ex); }
    }

    /// <summary>Regiões que o jogador exigiu nas guerras em curso (objectivos de guerra) e ainda não controla.</summary>
    private HashSet<int> PlayerGoals(World w)
    {
        var set = new HashSet<int>();
        if (_game.PlayerId is not int pid) return set;
        foreach (var war in w.Wars.Values)
            if (war.Involves(pid))
                foreach (int id in war.Side(pid).Goals)
                    if (w.Regions.TryGetValue(id, out var r) && r.ControllerId != pid) set.Add(id);
        return set;
    }

    /// <summary>Contorno dourado a pulsar nas regiões do objectivo — a guerra tem um destino e vê-se no mapa.
    /// Só refaz o desenho quando o conjunto muda; a animação é uma só, na camada inteira.</summary>
    private void DrawGoals(HashSet<int> goals)
    {
        string key = string.Join(",", goals.OrderBy(x => x));
        if (key == _goalKey) return;
        _goalKey = key;
        if (_goalPulse is not null && _goalPulse.IsValid()) _goalPulse.Kill();
        _goalPulse = null;
        foreach (var c in _goalRoot.GetChildren()) { _goalRoot.RemoveChild(c); c.QueueFree(); }
        var gold = new Color(1f, 0.82f, 0.25f);
        foreach (int id in goals)
            if (_byRegion.TryGetValue(id, out var polys))
                foreach (var p in polys)
                    _goalRoot.AddChild(new Line2D { Points = p.Polygon.Append(p.Polygon[0]).ToArray(), Width = 5f * _markerScale, DefaultColor = gold });
        if (goals.Count == 0) { _goalRoot.Modulate = Colors.White; return; }
        _goalPulse = _goalRoot.CreateTween().SetLoops();
        _goalPulse.TweenProperty(_goalRoot, "modulate:a", 0.35f, 0.9).SetTrans(Tween.TransitionType.Sine);
        _goalPulse.TweenProperty(_goalRoot, "modulate:a", 1f, 0.9).SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>Marcador: uma "pastilha" com a cor do controlador por trás do número, centrada na região.
    /// O CenterContainer de tamanho fixo é que centra a pastilha, que encolhe ao texto que leva dentro.</summary>
    private Node2D NewMarker(Region r)
    {
        var m = new Node2D { Position = new Vector2(r.CenterX, r.CenterY), Scale = Vector2.One * _markerScale };
        var center = new CenterContainer
        {
            Name = "Center", Size = new Vector2(220, 56), Position = new Vector2(-110, -28),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var pill = new PanelContainer { Name = "Pill", MouseFilter = Control.MouseFilterEnum.Ignore };
        pill.AddChild(new Label
        {
            Name = "Text",
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        center.AddChild(pill); m.AddChild(center);
        _markerRoot.AddChild(m);
        return m;
    }

    /// <summary>Fundo do marcador: tom escuro da cor do controlador, com a própria cor como borda.</summary>
    private StyleBoxFlat PillFor(int countryId)
    {
        if (_pillStyle.TryGetValue(countryId, out var box)) return box;
        var c = _countryColor.GetValueOrDefault(countryId, Colors.Gray);
        return _pillStyle[countryId] = new StyleBoxFlat
        {
            BgColor = c.Darkened(0.72f) with { A = 0.82f },
            BorderColor = c, BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 2, ContentMarginBottom = 2,
        };
    }

    /// <summary>Região em batalha pisca devagar (opacidade, não escala — a escala é do zoom).
    /// Deixar de haver batalha mata a animação e devolve o marcador ao normal.</summary>
    private void Pulse(int regionId, PanelContainer? pill, bool fighting)
    {
        if (fighting && pill is not null)
        {
            if (_pulses.ContainsKey(regionId)) return;
            var t = pill.CreateTween().SetLoops();
            t.TweenProperty(pill, "modulate:a", 0.45f, 0.6).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(pill, "modulate:a", 1f, 0.6).SetTrans(Tween.TransitionType.Sine);
            _pulses[regionId] = t;
            return;
        }
        if (!_pulses.Remove(regionId, out var old)) return;
        if (old.IsValid()) old.Kill();
        if (pill is not null) pill.Modulate = Colors.White;
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

    /// <summary>Contorno amarelo nas regiões da selecção múltipla — camada própria, não mexe no Highlight.</summary>
    public void HighlightMulti(IReadOnlyCollection<int> regionIds)
    {
        foreach (var c in _multiRoot.GetChildren()) { _multiRoot.RemoveChild(c); c.QueueFree(); }
        foreach (var id in regionIds)
            if (_byRegion.TryGetValue(id, out var polys))
                foreach (var p in polys)
                    _multiRoot.AddChild(new Line2D { Points = p.Polygon.Append(p.Polygon[0]).ToArray(), Width = 4f * _markerScale, DefaultColor = Colors.Yellow });
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
