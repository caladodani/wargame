using Godot;

namespace WarGame.Presentation;

/// <summary>Câmara 2D por toque: toque curto = selecção (RegionTapped), um dedo a arrastar = pan, dois = zoom.
/// Emite ZoomChanged e avisa o RegionRenderer para os marcadores manterem o tamanho.</summary>
public partial class MapView : Node2D
{
    [Signal] public delegate void RegionTappedEventHandler(int regionId);
    [Signal] public delegate void ZoomChangedEventHandler(float zoom);

    public RegionRenderer Regions => _regions;

    private const float TapMaxDrag = 14f;     // arrasto acumulado (px) a partir do qual deixa de ser toque curto
    private Camera2D _cam = null!;
    private RegionRenderer _regions = null!;
    private readonly Dictionary<int, Vector2> _touches = new();
    private float _lastPinch, _dragDist;
    private bool _multi;

    public override void _Ready()
    {
        _cam = GetNode<Camera2D>("Camera2D");
        _regions = GetNode<RegionRenderer>("Regions");
        var game = GetNode<Game>("/root/Game");
        _regions.Build(game.WorldRepo, game.StaticDb, game);
        SetZoom(GetViewportRect().Size.X / 8400f);   // arranque: mapa inteiro (8000 un. de largura) visível
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
                        if (_touches.Count == 0) { _dragDist = 0; _multi = false; }
                        _touches[t.Index] = t.Position;
                        if (_touches.Count >= 2) { _multi = true; _lastPinch = Pinch(); }
                    }
                    else
                    {
                        _touches.Remove(t.Index);
                        if (_touches.Count == 0 && !_multi && _dragDist < TapMaxDrag && _regions.RegionAt(ToWorld(t.Position)) is int rid)
                            EmitSignal(SignalName.RegionTapped, rid);
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
            }
        }
        catch (Exception ex) { GD.PushError("MapView input: " + ex); }
    }

    /// <summary>Centra a câmara em `worldPos` com o zoom dado (ex.: capital ao escolher país).</summary>
    public void Focus(Vector2 worldPos, float zoom) { _cam.Position = worldPos; SetZoom(zoom); }

    private Vector2 ToWorld(Vector2 screen) => GetCanvasTransform().AffineInverse() * screen;

    private float Pinch() { var v = _touches.Values.ToArray(); return v[0].DistanceTo(v[1]); }

    private void SetZoom(float z)
    {
        z = Mathf.Clamp(z, 0.1f, 8f); _cam.Zoom = new Vector2(z, z);
        _regions.SetZoom(z);
        EmitSignal(SignalName.ZoomChanged, z);
    }
}
