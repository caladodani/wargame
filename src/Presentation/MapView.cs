using Godot;

namespace WarGame.Presentation;

/// <summary>Câmara 2D com pan/zoom por toque. Regiões desenhadas como Polygon2D (a gerar do import Natural Earth).</summary>
public partial class MapView : Node2D
{
    private Camera2D _cam = null!;
    private readonly Dictionary<int, Vector2> _touches = new();
    private float _lastPinch;

    [Signal] public delegate void RegionTappedEventHandler(int regionId);
    private RegionRenderer _regions = null!;

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
        switch (e)
        {
            case InputEventScreenTouch t:
                if (t.Pressed) _touches[t.Index] = t.Position;
                else
                {
                    _touches.Remove(t.Index);
                    if (_touches.Count == 0 && !t.DoubleTap && _regions.RegionAt(GetGlobalMousePosition()) is int rid)
                        EmitSignal(SignalName.RegionTapped, rid);
                }
                if (_touches.Count == 2) _lastPinch = Pinch();
                break;
            case InputEventScreenDrag d:
                _touches[d.Index] = d.Position;
                if (_touches.Count == 1) _cam.Position -= d.Relative / _cam.Zoom;
                else if (_touches.Count == 2)
                {
                    float p = Pinch();
                    if (_lastPinch > 0) SetZoom(_cam.Zoom.X * (p / _lastPinch));
                    _lastPinch = p;
                }
                break;
        }
    }

    private float Pinch() { var v = _touches.Values.ToArray(); return v[0].DistanceTo(v[1]); }
    private void SetZoom(float z) { z = Mathf.Clamp(z, 0.1f, 8f); _cam.Zoom = new Vector2(z, z); }
}
