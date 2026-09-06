using Godot;

namespace WarGame.Presentation;

/// <summary>Mini-mapa no canto inferior esquerdo: o mundo inteiro em ponto pequeno, com a cor de cada
/// controlador, um rectângulo a marcar o que está no ecrã e toque para saltar para lá. Num mapa de 8000
/// unidades a câmara perde-se depressa; isto dá o mundo todo de relance sem largar o zoom onde se está.
///
/// São ~3400 polígonos: desenhá-los a cada frame comia o telemóvel. Vivem por isso dentro de um SubViewport
/// que só re-desenha quando uma cor muda (conquista, capitulação) — no ecrã fica uma textura e uma linha.
/// Os polígonos são Polygon2D (o Godot triangula-os bem, ao contrário de um DrawColoredPolygon à mão
/// em regiões côncavas).</summary>
public partial class MiniMap : PanelContainer
{
    private const float W = 280f, H = 190f;

    private MapView _map = null!;
    private Control _canvas = null!;
    private SubViewport _vp = null!;
    private Node2D _root = null!, _overlay = null!;
    private Line2D _view = null!;
    private Button _toggle = null!;
    private readonly Dictionary<int, List<Polygon2D>> _shapes = new();
    private Vector2 _offset;      // canto do mundo → canto do mini-mapa
    private float _scale = 1f;
    private Rect2 _lastView;

    public void Setup(MapView map)
    {
        _map = map;
        AnchorLeft = 0; AnchorRight = 0; AnchorTop = 1; AnchorBottom = 1;
        GrowHorizontal = GrowDirection.End; GrowVertical = GrowDirection.Begin;
        OffsetLeft = 12; OffsetBottom = -12;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.92f), 6));
        MouseFilter = MouseFilterEnum.Stop;   // o toque no mini-mapa não é pan do mapa grande

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); AddChild(v);
        var head = new HBoxContainer();
        head.AddChild(Ui.Grow(Ui.Lbl("Mapa", 15)));
        _toggle = Ui.Btn("–", Toggle, 44); head.AddChild(_toggle);
        v.AddChild(head);

        _canvas = new Control { CustomMinimumSize = new Vector2(W, H), ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        _canvas.GuiInput += OnCanvasInput;
        v.AddChild(_canvas);

        _vp = new SubViewport
        {
            Size = new Vector2I((int)W, (int)H),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
        };
        _canvas.AddChild(_vp);
        _root = new Node2D(); _vp.AddChild(_root);
        _canvas.AddChild(new TextureRect { Texture = _vp.GetTexture(), Size = new Vector2(W, H), MouseFilter = MouseFilterEnum.Ignore });

        _overlay = new Node2D { Name = "Overlay" }; _canvas.AddChild(_overlay);
        _view = new Line2D { Width = 3f, DefaultColor = Colors.White, Closed = true };
        _overlay.AddChild(_view);
        SetProcess(true);
    }

    /// <summary>Copia a forma de cada região do renderizador do mapa grande e ajusta a escala para o mundo
    /// inteiro caber na caixa. Só corre uma vez, quando os polígonos do mapa grande já existem.</summary>
    public void Build()
    {
        if (_shapes.Count > 0) return;
        var shapes = _map.Regions.Shapes().ToList();
        if (shapes.Count == 0) return;

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var (_, pts) in shapes)
            foreach (var p in pts)
            {
                minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X);
                minY = MathF.Min(minY, p.Y); maxY = MathF.Max(maxY, p.Y);
            }
        _scale = MathF.Min(W / MathF.Max(1f, maxX - minX), H / MathF.Max(1f, maxY - minY));
        _offset = new Vector2(-minX * _scale + (W - (maxX - minX) * _scale) / 2f,
                              -minY * _scale + (H - (maxY - minY) * _scale) / 2f);
        _root.Scale = _overlay.Scale = Vector2.One * _scale;
        _root.Position = _overlay.Position = _offset;
        _view.Width = 3f / _scale;

        foreach (var (id, pts) in shapes)
        {
            var poly = new Polygon2D { Polygon = pts, Color = _map.Regions.ColorOf(id) };
            _root.AddChild(poly);
            if (!_shapes.TryGetValue(id, out var list)) _shapes[id] = list = new();
            list.Add(poly);
        }
        Redraw();
    }

    /// <summary>Cores em dia depois de conquistas e capitulações. Se nada mudou, não custa nada:
    /// o SubViewport só volta a desenhar quando há mesmo cor nova.</summary>
    public void Refresh()
    {
        Build();
        bool dirty = false;
        foreach (var (id, polys) in _shapes)
        {
            var c = _map.Regions.ColorOf(id);
            foreach (var p in polys) if (p.Color != c) { p.Color = c; dirty = true; }
        }
        if (dirty) Redraw();
    }

    /// <summary>Manda o SubViewport desenhar uma vez (e só uma).</summary>
    private void Redraw() => _vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

    /// <summary>O rectângulo segue a câmara sem esperar por um tick — o pan é contínuo.</summary>
    public override void _Process(double delta)
    {
        if (_view is null || !Visible || !_canvas.Visible) return;
        var r = _map.VisibleWorldRect();
        if (r.Position.DistanceSquaredTo(_lastView.Position) < 1f && r.Size.DistanceSquaredTo(_lastView.Size) < 1f) return;
        _lastView = r;
        _view.Points = new[] { r.Position, r.Position + new Vector2(r.Size.X, 0), r.End, r.Position + new Vector2(0, r.Size.Y) };
    }

    /// <summary>Toque no mini-mapa: a câmara salta para lá, com o zoom que já estava.</summary>
    private void OnCanvasInput(InputEvent e)
    {
        Vector2? at = e switch
        {
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb => mb.Position,
            InputEventScreenTouch { Pressed: true } t => t.Position,
            _ => null,
        };
        if (at is not Vector2 pos) return;
        _map.MoveTo((pos - _offset) / _scale);   // inverso da transformação do Node2D à escala
        _canvas.AcceptEvent();
    }

    /// <summary>Encolhe/expande: no telemóvel o mapa pequeno também tapa mundo.</summary>
    public void Toggle()
    {
        _canvas.Visible = !_canvas.Visible;
        _toggle.Text = _canvas.Visible ? "–" : "+";
        if (_canvas.Visible) Redraw();
    }

    /// <summary>Esconde-se enquanto um painel grande está aberto (não serve de nada por baixo dele).</summary>
    public void SetCovered(bool covered) => Visible = !covered;
}
