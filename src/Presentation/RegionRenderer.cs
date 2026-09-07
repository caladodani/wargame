using Godot;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;

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
    private readonly Dictionary<int, List<Vector2[]>> _rings = new();  // anéis crus, para as contas da fronteira
    // por anel, e aresta a aresta: região vizinha que aquela aresta acompanha (-1 = costa ou terra de ninguém)
    private readonly Dictionary<int, List<int[]>> _edgeOf = new();
    private readonly Dictionary<int, Node2D> _frontier = new();        // região → linhas da fronteira nacional
    private readonly Dictionary<int, Color> _countryColor = new();
    private readonly Dictionary<int, LabelSettings> _labelStyle = new();
    private readonly Dictionary<int, StyleBoxFlat> _pillStyle = new();
    private readonly Dictionary<int, Node2D> _markers = new();      // região → Node2D (escala 1/zoom) com um Label
    private readonly Dictionary<int, Tween> _pulses = new();        // região em batalha → animação do marcador
    private readonly Dictionary<int, float> _area = new();          // região → área do polígono (peso do centróide do país)
    private readonly Dictionary<int, CountryLabel> _countryNames = new();  // país → nome escrito sobre o território
    private readonly Dictionary<int, UnitCounter> _counters = new();  // região → contador NATO (só de perto)
    /// <summary>Zoom a partir do qual os contadores substituem o número da pastilha: de longe o mapa é
    /// político e não militar, e cem caixas ao mesmo tempo não se leem.</summary>
    private const float CounterZoom = 0.7f;
    private bool _countersOn;
    private Node2D _highlightRoot = null!, _multiRoot = null!, _markerRoot = null!, _goalRoot = null!, _nameRoot = null!;
    private Node2D _counterRoot = null!;
    private Node2D _frontierRoot = null!;
    private float _zoom = 1f;
    // Modo de mapa (MapModes): o político pinta pelo controlador, os outros pela conta escolhida.
    private string _mode = MapModes.Political, _metric = "owner";
    private Dictionary<int, float> _shades = new();
    private readonly Dictionary<int, int> _shadePainted = new();   // tom já pintado (0..20), para não repintar à toa
    private string _goalKey = "";                                   // objectivos desenhados (evita refazer o contorno todos os dias)
    private Tween? _goalPulse;
    private Game _game = null!;
    private float _markerScale = 1f;

    public void Build(SqlWorldRepository repo, IDatabase staticDb, Game game)
    {
        _game = game;
        foreach (var r in staticDb.Query("SELECT id,color FROM country"))
            _countryColor[Convert.ToInt32(r["id"])] = new Color((string?)r["color"] ?? "#cccccc");
        SeparateNeighbourColours(game.World);

        foreach (var (regionId, pts) in repo.ReadPolygons())
        {
            var v = new Vector2[pts.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(pts[2 * i], pts[2 * i + 1]);
            _area[regionId] = _area.GetValueOrDefault(regionId) + Area(v);
            var poly = new Polygon2D { Polygon = v, Color = ColorFor(regionId) };
            var ring = v.Append(v[0]).ToArray();
            // duas linhas por anel: uma grossa na cor do controlador (dá relevo à fronteira nacional)
            // e uma fina preta por cima, que separa regiões do mesmo país.
            var border = new Line2D { Points = ring, Width = BorderWidth(regionId), DefaultColor = BorderFor(regionId) };
            var outline = new Line2D { Points = ring, Width = 1.2f, DefaultColor = new Color(0, 0, 0, 0.45f) };
            AddChild(poly); poly.AddChild(border); poly.AddChild(outline);
            if (!_byRegion.TryGetValue(regionId, out var list)) _byRegion[regionId] = list = new();
            if (!_borders.TryGetValue(regionId, out var blist)) _borders[regionId] = blist = new();
            if (!_rings.TryGetValue(regionId, out var rlist)) _rings[regionId] = rlist = new();
            list.Add(poly); blist.Add(border); rlist.Add(v);
        }
        // Por cima dos polígonos: primeiro a fronteira nacional, depois o realce e os marcadores.
        _frontierRoot = new Node2D { Name = "Frontiers" }; AddChild(_frontierRoot);
        MapEdges(game.World);
        foreach (int id in _rings.Keys) PaintFrontier(id);
        _highlightRoot = new Node2D { Name = "Highlight" }; AddChild(_highlightRoot);
        _multiRoot = new Node2D { Name = "MultiHighlight" }; AddChild(_multiRoot);
        _goalRoot = new Node2D { Name = "Goals" }; AddChild(_goalRoot);
        _nameRoot = new Node2D { Name = "CountryNames", Modulate = new Color(1, 1, 1, 0.9f) }; AddChild(_nameRoot);
        _markerRoot = new Node2D { Name = "Markers" }; AddChild(_markerRoot);   // números por cima dos nomes
        _counterRoot = new Node2D { Name = "Counters", Visible = false }; AddChild(_counterRoot);   // e os contadores por cima de tudo
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

    /// <summary>Cor actual de uma região (controlador, escurecida quando é ocupação; noutro modo de mapa,
    /// o tom da escala de calor).</summary>
    public Color ColorOf(int regionId) => ColorFor(regionId);

    /// <summary>Modo de mapa escolhido (id da tabela map_mode).</summary>
    public string Mode => _mode;

    /// <summary>Troca o modo de mapa e repinta o mundo. Um id desconhecido volta ao mapa político.</summary>
    public void SetMode(string modeId)
    {
        var def = _game.World.MapModeDefs.GetValueOrDefault(modeId);
        _mode = def?.Id ?? MapModes.Political;
        _metric = def?.Metric ?? "owner";
        _shades = _game.PlayerId is int pid ? MapModes.Shades(_game.World, pid, _metric)
                                            : MapModes.Shades(_game.World, 0, _metric);
        _shadePainted.Clear();
        RepaintAll();
    }

    /// <summary>Cor do país, tal como sai da base de dados (o Hud usa-a na barra de topo).</summary>
    public Color CountryColor(int countryId) => _countryColor.GetValueOrDefault(countryId, Colors.Gray);

    /// <summary>Cor da moldura da região: um risco discreto na cor do controlador, que separa regiões do
    /// mesmo país. A fronteira nacional não se desenha aqui — ver PaintFrontier.</summary>
    private Color BorderFor(int regionId) => ColorFor(regionId).Lightened(0.3f) with { A = 0.35f };

    /// <summary>Espessura da moldura interna (a fronteira nacional tem linha própria, FrontierWidth).</summary>
    private static float BorderWidth(int regionId) => 1.5f;

    /// <summary>Cor e grossura da linha de fronteira nacional: quase branca, para se ver onde acaba um país
    /// e começa o outro mesmo que os dois tenham cores parecidas (Polónia e Rússia saíam do mesmo branco).</summary>
    private static readonly Color FrontierColor = new(0.96f, 0.97f, 1f, 0.95f);
    private const float FrontierWidth = 5f;
    /// <summary>Distância (unidades de mundo) a que uma aresta ainda conta como colada à terra do vizinho.
    /// Os polígonos vêm simplificados região a região e quase nunca casam vértice a vértice: sem folga não
    /// haveria fronteira nenhuma, com folga a mais um risco interior passava por fronteira.</summary>
    private const float EdgeSnap = 4f;

    /// <summary>Descobre, uma vez só e para sempre, que vizinha é que cada aresta de cada anel acompanha.
    ///
    /// Isto é o que faltava para desenhar a fronteira como no HoI4: antes, uma região com um vizinho
    /// estrangeiro levava o anel INTEIRO a branco grosso, e o mapa ficava com os estados de fronteira todos
    /// marcados à volta em vez da linha que os separa. A geometria não traz topologia (cada região foi
    /// simplificada por si), por isso a vizinhança de uma aresta mede-se por distância: se as duas pontas
    /// caem a menos de EdgeSnap da orla da vizinha, aquela aresta é o traço comum das duas.
    ///
    /// Guardado o id da vizinha por aresta, quem manda na cor é o controlador do dia — a mesma linha serve
    /// de fronteira internacional ou de linha da frente conforme quem manda de cada lado.</summary>
    private void MapEdges(World w)
    {
        foreach (var (id, rings) in _rings)
        {
            var list = new List<int[]>();
            var foes = new List<(Vector2[][] Rings, Rect2 Box, int Id)>();
            if (w.Regions.TryGetValue(id, out var reg))
                foreach (int n in reg.Neighbours)
                    if (_rings.TryGetValue(n, out var nr)) foes.Add((nr.ToArray(), Box(nr).Grow(EdgeSnap), n));

            foreach (var ring in rings)
            {
                var edge = new int[ring.Length];
                for (int i = 0; i < ring.Length; i++)
                {
                    edge[i] = -1;
                    var a = ring[i]; var b = ring[(i + 1) % ring.Length];
                    foreach (var f in foes)
                        if (f.Box.HasPoint(a) && f.Box.HasPoint(b) && Hugs(a, f.Rings) && Hugs(b, f.Rings)) { edge[i] = f.Id; break; }
                }
                list.Add(edge);
            }
            _edgeOf[id] = list;
        }
    }

    private static Rect2 Box(List<Vector2[]> rings)
    {
        var box = new Rect2(rings[0][0], Vector2.Zero);
        foreach (var ring in rings) foreach (var p in ring) box = box.Expand(p);
        return box;
    }

    /// <summary>Ponto colado à orla de outra região (a menos de EdgeSnap de uma das arestas dela).</summary>
    private static bool Hugs(Vector2 p, Vector2[][] rings)
    {
        float limit = EdgeSnap * EdgeSnap;
        foreach (var ring in rings)
            for (int i = 0; i < ring.Length; i++)
                if (DistSq(p, ring[i], ring[(i + 1) % ring.Length]) < limit) return true;
        return false;
    }

    private static float DistSq(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float len = ab.LengthSquared();
        float t = len <= 0f ? 0f : Mathf.Clamp((p - a).Dot(ab) / len, 0f, 1f);
        return p.DistanceSquaredTo(a + ab * t);
    }

    /// <summary>Redesenha a linha de fronteira desta região: só as arestas que acompanham terra de outro
    /// controlador, cosidas em tiras seguidas (uma Line2D por tira, não uma por aresta).</summary>
    private void PaintFrontier(int regionId)
    {
        if (!_frontier.TryGetValue(regionId, out var node))
        {
            node = new Node2D { Name = "F" + regionId };
            _frontierRoot.AddChild(node);
            _frontier[regionId] = node;
        }
        foreach (var c in node.GetChildren()) { node.RemoveChild(c); c.QueueFree(); }
        if (!_rings.TryGetValue(regionId, out var rings) || !_edgeOf.TryGetValue(regionId, out var edges)) return;
        var w = _game.World;
        if (!w.Regions.TryGetValue(regionId, out var r)) return;

        for (int k = 0; k < rings.Count; k++)
        {
            var ring = rings[k]; var edge = edges[k];
            int n = ring.Length;
            var split = new bool[n];
            int marked = 0;
            for (int i = 0; i < n; i++)
                if (edge[i] >= 0 && w.Regions.TryGetValue(edge[i], out var o) && o.ControllerId != r.ControllerId)
                { split[i] = true; marked++; }
            if (marked == 0) continue;

            if (marked == n) { node.AddChild(Frontier(ring.Append(ring[0]).ToArray())); continue; }
            for (int i = 0; i < n; i++)
            {
                if (!split[i] || split[(i - 1 + n) % n]) continue;      // só começa tira onde a anterior não é fronteira
                var pts = new List<Vector2> { ring[i] };
                for (int j = i; j < i + n && split[j % n]; j++) pts.Add(ring[(j + 1) % n]);
                node.AddChild(Frontier(pts.ToArray()));
            }
        }
    }

    private static Line2D Frontier(Vector2[] pts) => new()
    {
        Points = pts, Width = FrontierWidth, DefaultColor = FrontierColor,
        JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
    };

    /// <summary>Área do anel (fórmula do sapateiro), para o nome do país cair no meio do território que conta.</summary>
    private static float Area(Vector2[] v)
    {
        float a = 0f;
        for (int i = 0; i < v.Length; i++) { var p = v[i]; var q = v[(i + 1) % v.Length]; a += p.X * q.Y - q.X * p.Y; }
        return MathF.Abs(a) * 0.5f;
    }

    /// <summary>As cores vêm das bandeiras e repetem-se: 247 países para 84 cores, 27 deles brancos — a
    /// Polónia e a Rússia saíam do mesmo branco e não havia como distingui-las no mapa. Aqui, e só para
    /// desenhar, quem colide com um vizinho recebe uma variação da sua própria cor (matiz rodado por
    /// passos irregulares; aos cinzentos dá-se matiz a partir do id). Os países maiores são servidos
    /// primeiro, para que sejam os pequenos a ceder a cor de bandeira. A base de dados fica intacta.</summary>
    private void SeparateNeighbourColours(World w)
    {
        var neighbours = new Dictionary<int, HashSet<int>>();
        var size = new Dictionary<int, int>();
        foreach (var r in w.Regions.Values)
        {
            size[r.OwnerId] = size.GetValueOrDefault(r.OwnerId) + 1;
            foreach (int n in r.Neighbours)
                if (w.Regions.TryGetValue(n, out var o) && o.OwnerId != r.OwnerId)
                {
                    if (!neighbours.TryGetValue(r.OwnerId, out var set)) neighbours[r.OwnerId] = set = new();
                    set.Add(o.OwnerId);
                }
        }

        foreach (int id in neighbours.Keys.OrderByDescending(x => size.GetValueOrDefault(x)).ThenBy(x => x))
        {
            if (!_countryColor.TryGetValue(id, out var original)) continue;
            var mine = original;
            for (int step = 0; step < 24 && Collides(id, mine, neighbours[id]); step++)
                mine = Vary(original, id, step);
            _countryColor[id] = mine;
        }
    }

    private bool Collides(int id, Color mine, HashSet<int> neighbours) =>
        neighbours.Any(n => n != id && _countryColor.TryGetValue(n, out var other) && TooClose(mine, other));

    /// <summary>Variação nº `step` da cor de um país: o matiz roda em passos que não fecham o círculo, por
    /// isso duas tentativas nunca caem na mesma família; o cinzento ganha um matiz próprio do id.</summary>
    private static Color Vary(Color baseColor, int countryId, int step)
    {
        float h = baseColor.S < 0.18f
            ? Mathf.PosMod(countryId * 0.13f + step * 0.191f, 1f)
            : Mathf.PosMod(baseColor.H + (step + 1) * 0.191f, 1f);
        float sat = Mathf.Clamp(MathF.Max(baseColor.S, 0.45f) + step % 3 * 0.12f, 0.35f, 0.95f);
        float val = Mathf.Clamp(MathF.Max(baseColor.V, 0.55f) - step % 4 * 0.1f, 0.4f, 1f);
        return Color.FromHsv(h, sat, val);
    }

    /// <summary>Duas cores que ninguém distingue lado a lado num mapa. Cinzentos comparam-se pelo brilho
    /// (preto e branco convivem bem); as coloridas, pelo matiz.</summary>
    private static bool TooClose(Color a, Color b)
    {
        bool greyA = a.S < 0.18f, greyB = b.S < 0.18f;
        if (greyA != greyB) return false;
        if (greyA) return MathF.Abs(a.V - b.V) < 0.25f;
        float dh = MathF.Abs(a.H - b.H); dh = MathF.Min(dh, 1f - dh);
        return dh < 0.08f && MathF.Abs(a.V - b.V) < 0.22f && MathF.Abs(a.S - b.S) < 0.3f;
    }

    private Color ColorFor(int regionId)
    {
        if (!_game.World.Regions.TryGetValue(regionId, out var r)) return Colors.Gray;
        // Fora do mapa político manda a conta: quente onde há muito, aço frio onde não há resposta.
        if (_metric != "owner")
            return _shades.TryGetValue(regionId, out float t) ? Ui.Heat(t) : Ui.Surface.Darkened(0.45f);
        var c = _countryColor.GetValueOrDefault(r.ControllerId, Colors.Gray);
        return r.ControllerId == r.OwnerId ? c : c.Darkened(0.28f);   // ocupada: tom escuro do ocupante
    }

    /// <summary>Repinta o mundo inteiro já (o RecolorAll espera pelo fim do tick; a troca de modo é do
    /// jogador e não pode ficar um segundo à espera).</summary>
    private void RepaintAll()
    {
        foreach (var (id, polys) in _byRegion)
        {
            var col = ColorFor(id); foreach (var p in polys) p.Color = col;
            PaintBorder(id);
        }
    }

    /// <summary>Refaz as contas do modo actual e repinta só as regiões cujo tom mudou. Os números mexem-se
    /// todos os dias (abastecimento, resistência): sem isto o mapa temático era uma fotografia velha.</summary>
    private void RefreshShades()
    {
        if (_metric == "owner") return;
        _shades = MapModes.Shades(_game.World, _game.PlayerId ?? 0, _metric);
        foreach (var (id, polys) in _byRegion)
        {
            int step = _shades.TryGetValue(id, out float t) ? (int)MathF.Round(t * 20f) : -1;
            if (_shadePainted.TryGetValue(id, out int before) && before == step) continue;
            _shadePainted[id] = step;
            var col = ColorFor(id); foreach (var p in polys) p.Color = col;
            PaintBorder(id);
        }
    }

    private void RecolorAll() => _game.RunWhenIdle(() =>
    {
        foreach (var (id, polys) in _byRegion)
        {
            var col = ColorFor(id); foreach (var p in polys) p.Color = col;
            PaintBorder(id);
            PaintFrontier(id);
        }
    });

    /// <summary>Moldura da região: cor e espessura conforme seja fronteira nacional ou risco interno.</summary>
    private void PaintBorder(int regionId)
    {
        if (!_borders.TryGetValue(regionId, out var borders)) return;
        var bc = BorderFor(regionId); float bw = BorderWidth(regionId);
        foreach (var b in borders) { b.DefaultColor = bc; b.Width = bw; }
    }

    // Chega na main thread (deferred); a leitura do controlador espera pelo fim do tick.
    private void Recolor(int regionId) => _game.RunWhenIdle(() =>
    {
        if (_byRegion.TryGetValue(regionId, out var polys)) foreach (var p in polys) p.Color = ColorFor(regionId);
        PaintBorder(regionId);
        PaintFrontier(regionId);
        // mudar de dono muda a fronteira dos dois lados: os vizinhos têm de ser repintados também
        if (_game.World.Regions.TryGetValue(regionId, out var reg))
            foreach (int n in reg.Neighbours) { PaintBorder(n); PaintFrontier(n); }
    });

    /// <summary>Marcadores e realce à escala inversa do zoom (clamp 0.05..6): tamanho constante no ecrã —
    /// ao aproximar encolhem no mapa em vez de crescerem no ecrã.</summary>
    public void SetZoom(float zoom)
    {
        _zoom = zoom;
        _markerScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 0.05f, 6f);
        foreach (var m in _markers.Values) m.Scale = Vector2.One * _markerScale;
        foreach (var c in _counters.Values) c.Scale = Vector2.One * _markerScale;
        // cruzar o limiar troca a leitura do mapa: os contadores acendem e a pastilha larga o número
        bool on = zoom >= CounterZoom;
        _counterRoot.Visible = on;
        if (on != _countersOn) { _countersOn = on; Refresh(); }
        foreach (var n in _countryNames.Values)
        {
            n.Scale = Vector2.One * _markerScale;
            n.Visible = (bool)n.GetMeta("alive", false) && ShowName(n);   // ao afastar, ficam só os grandes
        }
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
            RefreshShades();
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
                // o marcador conta o que o jogador tem como ver: tropa do outro lado do nevoeiro não aparece
                int shown = _game.PlayerId is int viewer ? Vision.CountIn(w, viewer, r) : r.DivisionIds.Count;
                if (shown == 0 && r.Fort == 0 && !resisting && !capital && !goal && !port) continue;
                seen.Add(r.Id);
                if (!_markers.TryGetValue(r.Id, out var m)) _markers[r.Id] = m = NewMarker(r);
                var pill = m.GetNode<PanelContainer>("Center/Pill");
                var label = pill.GetNode<Label>("Text");
                bool fighting = battles.Contains(r.Id);
                label.Text = (goal ? GoalMark : "")
                           + (capital ? CapitalMark : "")
                           + (fighting ? BattleMark : "")
                           + (shown > 0 && !_countersOn ? shown.ToString() : "")   // de perto, quem conta é o contador
                           + (r.Fort > 0 ? FortMark : "")
                           + (port ? PortMark : "")
                           + (resisting ? ResistMark : "");
                label.LabelSettings = StyleFor(r.ControllerId);
                pill.AddThemeStyleboxOverride("panel", PillFor(r.ControllerId));
                Pulse(r.Id, pill, fighting);
                m.Visible = true;
                if (_countersOn && shown > 0) FillCounter(w, r, shown);
                else if (_counters.TryGetValue(r.Id, out var off)) off.Visible = false;
            }
            foreach (var (id, m) in _markers)
                if (!seen.Contains(id)) { m.Visible = false; Pulse(id, null, false); }
            foreach (var (id, ct) in _counters)
                if (!seen.Contains(id)) ct.Visible = false;
            RefreshCountryNames(w);
        }
        catch (Exception ex) { GD.PushError("RegionRenderer.Refresh: " + ex); }
    }

    /// <summary>Enche o contador desta região com a maior força que lá está: cor e bandeira do dono, símbolo
    /// do tipo de tropa, quantas divisões, e — só se a tropa for nossa ou de aliado — o estado em que ela
    /// está. Do inimigo vê-se o que se vê de fora: quantos são e de que género, nunca as barras.</summary>
    private void FillCounter(World w, Region r, int shown)
    {
        var divs = r.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>().ToList();
        if (divs.Count == 0) { if (_counters.TryGetValue(r.Id, out var none)) none.Visible = false; return; }
        // a maior pilha manda no contador; empate desempata pelo id, para a caixa não andar aos saltos
        var group = divs.GroupBy(d => d.CountryId).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().ToList();
        int owner = group[0].CountryId;
        bool known = _game.PlayerId is int pid && (owner == pid || w.SameFaction(pid, owner));

        var tags = new HashSet<string>();
        try { foreach (string t in w.Stats.Get(group[0].TemplateId).Tags) tags.Add(t); } catch { /* modelo sem etiquetas */ }

        if (!_counters.TryGetValue(r.Id, out var counter))
        {
            _counters[r.Id] = counter = new UnitCounter { Scale = Vector2.One * _markerScale };
            _counterRoot.AddChild(counter);
        }
        counter.Position = new Vector2(r.CenterX, r.CenterY);
        counter.Set(_countryColor.GetValueOrDefault(owner, Colors.Gray),
                    w.Countries.TryGetValue(owner, out var oc) ? Flags.Of(oc.Tag) : null,
                    UnitCounter.KindOf(tags),
                    known ? group.Count : shown,
                    group.Average(d => d.Org) / 100f,
                    group.Average(d => d.Hp) / 100f,
                    known ? group.Average(d => d.Entrench) : 0f,
                    known);
        counter.Visible = true;
    }

    /// <summary>--smoke: quantos contadores estão desenhados no mapa (o zoom de perto tem de estar ligado).</summary>
    public int Counters() => _counters.Values.Count(c => c.Visible);

    /// <summary>Nome de cada país escrito por cima do território que controla, no centro de gravidade das
    /// regiões dele (pesadas pela área, para o nome cair na massa principal e não num arquipélago).
    ///
    /// Sem isto o mapa é uma manta de cores anónima: dois vizinhos de tom parecido só se distinguem quando
    /// se toca em cada um. O tamanho da letra cresce com o país e a etiqueta esconde-se quando o território
    /// é pequeno demais no ecrã para caber lá o nome (ver ShowNames).</summary>
    private void RefreshCountryNames(World w)
    {
        // um ponto por região, pesado pela área: é a nuvem que dá o eixo e a curva do nome (CountryLabel)
        var cloud = new Dictionary<int, List<(Vector2 At, float Weight)>>();
        foreach (var r in w.Regions.Values)
        {
            float a = MathF.Max(1f, _area.GetValueOrDefault(r.Id));
            if (!cloud.TryGetValue(r.ControllerId, out var list)) cloud[r.ControllerId] = list = new();
            list.Add((new Vector2(r.CenterX, r.CenterY), a));
        }

        foreach (var (id, points) in cloud)
        {
            if (!w.Countries.TryGetValue(id, out var c)) continue;
            float area = points.Sum(p => p.Weight);
            if (area <= 0f) continue;
            if (!_countryNames.TryGetValue(id, out var node)) _countryNames[id] = node = NewCountryName();
            // países grandes levam letra maior; a cor é a do país, clareada para se ler por cima do mapa
            int size = (int)Mathf.Clamp(MathF.Sqrt(area) * 0.16f, 16f, 64f);
            node.Set(c.Name, points, _countryColor.GetValueOrDefault(id, Colors.White).Lightened(0.55f), size);
        }
        foreach (var (id, node) in _countryNames)
        {
            node.SetMeta("alive", cloud.ContainsKey(id));   // país sem território não tem nome no mapa
            node.Visible = cloud.ContainsKey(id) && ShowName(node);
        }
    }

    /// <summary>Etiqueta com o nome do país, escrita letra a letra sobre a espinha do território: letra clara
    /// com contorno preto, para se ler por cima de qualquer cor de mapa. Fica na sua própria camada, por
    /// baixo dos marcadores das divisões.</summary>
    private CountryLabel NewCountryName()
    {
        var node = new CountryLabel { Scale = Vector2.One * _markerScale };
        _nameRoot.AddChild(node);
        return node;
    }

    /// <summary>O nome só aparece quando o país ocupa espaço que chegue no ecrã (largura aparente em píxeis):
    /// afastado vêem-se os impérios, ao aproximar aparecem os pequenos.</summary>
    private bool ShowName(CountryLabel node) => node.Span * _zoom > 90f;

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

    /// <summary>--smoke: tiras de fronteira nacional desenhadas (uma por troço seguido de arestas).</summary>
    /// <summary>Nomes de país no mapa e as letras que eles somam: o --smoke prova por aqui que os nomes
    /// deixaram de ser um Label direito e passaram a ser letra a letra sobre a curva do território.</summary>
    public (int Names, int Glyphs) CountryNames() =>
        (_countryNames.Values.Count(n => (bool)n.GetMeta("alive", false)), _countryNames.Values.Sum(n => n.GetChildCount()));

    public int FrontierLines() => _frontier.Values.Sum(n => n.GetChildCount());

    /// <summary>Hit-test para toque: região cujo polígono contém o ponto (mundo).</summary>
    public int? RegionAt(Vector2 worldPos)
    {
        foreach (var (id, polys) in _byRegion)
            foreach (var p in polys)
                if (Geometry2D.IsPointInPolygon(worldPos, p.Polygon)) return id;
        return null;
    }
}
