using Godot;

namespace WarGame.Presentation;

/// <summary>Câmara 2D por toque: toque curto = selecção (RegionTapped), toque longo = marca a região para
/// a selecção múltipla (RegionLongPressed), duplo toque = destino do movimento (RegionDoubleTapped),
/// um dedo a arrastar = pan, dois = zoom. No PC o botão direito faz de toque longo.
/// Emite ZoomChanged e avisa o RegionRenderer para os marcadores manterem o tamanho.</summary>
public partial class MapView : Node2D
{
    [Signal] public delegate void RegionTappedEventHandler(int regionId);
    /// <summary>Toque longo (ou botão direito do rato): marca/desmarca a região na selecção múltipla.</summary>
    [Signal] public delegate void RegionLongPressedEventHandler(int regionId);
    /// <summary>Duplo toque: com regiões marcadas, é o destino para onde as divisões marcham.</summary>
    [Signal] public delegate void RegionDoubleTappedEventHandler(int regionId);
    [Signal] public delegate void ZoomChangedEventHandler(float zoom);

    public RegionRenderer Regions => _regions;

    /// <summary>Camada dos planos de batalha: as setas dos exércitos, por cima do mapa.</summary>
    public PlanOverlay Plans => _plans;

    private const float TapMaxDrag = 14f;      // arrasto acumulado (px) a partir do qual deixa de ser toque curto
    private const ulong LongPressMs = 450;     // dedo parado neste tempo = toque longo
    private const ulong DoubleTapMs = 320;     // segundo toque dentro desta janela = duplo toque
    private const float DoubleTapMaxDist = 60f;

    private Camera2D _cam = null!;
    private RegionRenderer _regions = null!;
    private PlanOverlay _plans = null!;
    private readonly Dictionary<int, Vector2> _touches = new();
    private float _lastPinch, _dragDist;
    private bool _multi, _longFired;
    private ulong _pressAt, _lastTapAt;
    private Vector2 _pressPos, _lastTapPos;

    public override void _Ready()
    {
        _cam = GetNode<Camera2D>("Camera2D");
        _regions = GetNode<RegionRenderer>("Regions");
        var game = GetNode<Game>("/root/Game");
        _regions.Build(game.WorldRepo, game.StaticDb, game);
        // as setas dos planos entram depois das regiões: desenham-se por cima do mapa
        _plans = new PlanOverlay { Name = "Plans" }; AddChild(_plans); _plans.Setup(game);
        SetZoom(GetViewportRect().Size.X / 8400f);   // arranque: mapa inteiro (8000 un. de largura) visível
        SetProcess(true);
    }

    /// <summary>O toque longo dispara com o dedo ainda pousado (como no Android), não à largada.</summary>
    public override void _Process(double delta)
    {
        if (_longFired || _multi || _touches.Count != 1 || _dragDist >= TapMaxDrag) return;
        if (Time.GetTicksMsec() - _pressAt < LongPressMs) return;
        _longFired = true;
        if (_regions.RegionAt(ToWorld(_pressPos)) is int rid) EmitSignal(SignalName.RegionLongPressed, rid);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        try
        {
            switch (e)
            {
                case InputEventScreenTouch t:
                    if (t.Pressed)
                    {
                        if (t.Index == 0) _touches.Clear();     // 1º dedo: limpa toques presos (release engolido pela GUI)
                        if (_touches.Count == 0)
                        {
                            _dragDist = 0; _multi = false; _longFired = false;
                            _pressAt = Time.GetTicksMsec(); _pressPos = t.Position;
                        }
                        _touches[t.Index] = t.Position;
                        if (_touches.Count >= 2) { _multi = true; _lastPinch = Pinch(); }
                    }
                    else
                    {
                        _touches.Remove(t.Index);
                        if (_touches.Count == 0 && !_multi && !_longFired && _dragDist < TapMaxDrag)
                            Tap(t.Position);
                    }
                    break;
                case InputEventScreenDrag d:
                    _touches[d.Index] = d.Position;
                    _dragDist += d.Relative.Length();
                    if (_touches.Count == 1) _cam.Position -= d.Relative / _cam.Zoom;
                    else if (_touches.Count == 2)
                    {
                        float p = Pinch();
                        if (_lastPinch > 0) SetZoom(_cam.Zoom.X * (p / _lastPinch));
                        _lastPinch = p;
                    }
                    break;
                case InputEventMouseButton { Pressed: true } mb when mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown:
                    SetZoom(_cam.Zoom.X * (mb.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1f / 1.15f));   // roda do rato no PC
                    break;
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } mbr:
                    // botão direito no PC = toque longo (a emulação de toque só cobre o esquerdo)
                    if (_regions.RegionAt(ToWorld(mbr.Position)) is int mrid) EmitSignal(SignalName.RegionLongPressed, mrid);
                    break;
            }
        }
        catch (Exception ex) { GD.PushError("MapView input: " + ex); }
    }

    /// <summary>Toque curto: emite sempre RegionTapped e, se for o segundo em cima do primeiro dentro da
    /// janela, também RegionDoubleTapped — o painel abre à mesma e o duplo toque manda marchar.</summary>
    private void Tap(Vector2 pos)
    {
        if (_regions.RegionAt(ToWorld(pos)) is not int rid) return;
        ulong now = Time.GetTicksMsec();
        bool doubled = now - _lastTapAt < DoubleTapMs && pos.DistanceTo(_lastTapPos) < DoubleTapMaxDist;
        _lastTapAt = doubled ? 0 : now;    // um duplo toque não encadeia com o toque seguinte
        _lastTapPos = pos;
        EmitSignal(SignalName.RegionTapped, rid);
        if (doubled) EmitSignal(SignalName.RegionDoubleTapped, rid);
    }

    /// <summary>Centra a câmara em `worldPos` com o zoom dado (ex.: capital ao escolher país).</summary>
    public void Focus(Vector2 worldPos, float zoom) { _cam.Position = worldPos; SetZoom(zoom); }

    /// <summary>Centra a câmara sem mexer no zoom (o mini-mapa salta assim para onde se tocou).</summary>
    public void MoveTo(Vector2 worldPos) => _cam.Position = worldPos;

    /// <summary>Pedaço do mundo que cabe no ecrã, em coordenadas de mundo (o mini-mapa desenha-o).</summary>
    public Rect2 VisibleWorldRect()
    {
        var size = GetViewportRect().Size / _cam.Zoom;
        return new Rect2(_cam.Position - size / 2f, size);
    }

    private Vector2 ToWorld(Vector2 screen) => GetCanvasTransform().AffineInverse() * screen;

    private float Pinch() { var v = _touches.Values.ToArray(); return v[0].DistanceTo(v[1]); }

    private void SetZoom(float z)
    {
        z = Mathf.Clamp(z, 0.1f, 8f); _cam.Zoom = new Vector2(z, z);
        _regions.SetZoom(z);
        _plans.SetZoom(z);
        EmitSignal(SignalName.ZoomChanged, z);
    }
}
