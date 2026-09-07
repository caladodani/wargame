using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>As rotas dos comboios mercantes desenhadas no mapa, como no HoI4: um tracejado por cada travessia
/// que os nossos navios fazem hoje — o cais que alimenta o exército do outro lado do mar e a costa de quem nos
/// vende o aço.
///
/// Até aqui a marinha mercante era um número no painel do país: dizia-se ao jogador que tinha 20 comboios e
/// que três contratos estavam parados, mas não se via por onde é que eles andavam nem onde é que o inimigo
/// lhes estava a bater. Agora vê-se: cada rota é uma linha tracejada com um losango a meio a dizer quantos
/// mercantes ocupa, e uma rota cortada — bloqueio na costa ou navios que já foram ao fundo — fica vermelha,
/// aos tracinhos curtos, com o corte marcado por cima.
///
/// Comércio e abastecimento distinguem-se pela cor: o que compramos vem em latão (Ui.Accent), o que alimenta
/// as divisões vem no azul do cais (PortView.Quay). Quem sabe que travessias existem é o ConvoySystem.Routes;
/// isto só desenha. Vive dentro do MapView, em coordenadas de mundo, por isso acompanha o pan e o zoom.</summary>
public partial class ConvoyRoutes : Node2D
{
    /// <summary>Uma travessia já em coordenadas de mundo, pronta a desenhar.</summary>
    private readonly record struct Lane(Vector2 From, Vector2 To, Color Tint, bool Blocked, float Holds);

    private const float Dash = 34f, Gap = 22f, Width = 5f;   // tracejado, em unidades de mundo

    private Game _game = null!;
    private Node2D _tagRoot = null!;
    private readonly List<Lane> _lanes = new();
    private string _painted = "";
    private float _tagScale = 1f;

    public void Setup(Game game)
    {
        _game = game;
        ZIndex = 2;                                   // por cima do mapa, por baixo das setas dos planos
        _tagRoot = new Node2D { Name = "ConvoyTags" };
        AddChild(_tagRoot);
    }

    /// <summary>As etiquetas mantêm-se legíveis ao afastar, como as dos planos de batalha.</summary>
    public void SetZoom(float zoom)
    {
        _tagScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 0.05f, 6f);
        foreach (var c in _tagRoot.GetChildren()) if (c is Node2D n) n.Scale = Vector2.One * _tagScale;
    }

    /// <summary>Recalcula as rotas (e só redesenha quando alguma coisa mudou).</summary>
    public void Refresh()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid) return;
            var routes = ConvoySystem.Routes(w, pid);

            string key = string.Join(";", routes.Select(r => $"{r.FromId}>{r.ToId}:{(r.Trade ? "t" : "s")}:{r.Holds:0}:{(r.Blocked ? 1 : 0)}"));
            if (key == _painted) return;
            _painted = key;

            _lanes.Clear();
            Ui.Clear(_tagRoot);
            foreach (var r in routes) Build(w, r);
            QueueRedraw();
        }
        catch (Exception ex) { GD.PushError("ConvoyRoutes: " + ex); }
    }

    private void Build(World w, ConvoyRoute route)
    {
        if (!w.Regions.TryGetValue(route.FromId, out var a) || !w.Regions.TryGetValue(route.ToId, out var b)) return;
        var from = new Vector2(a.CenterX, a.CenterY);
        var to = new Vector2(b.CenterX, b.CenterY);
        if (from.DistanceTo(to) < 1f) return;

        var tint = route.Blocked ? Ui.Danger : route.Trade ? Ui.Accent : PortView.Quay;
        _lanes.Add(new Lane(from, to, tint, route.Blocked, route.Holds));

        // etiqueta a meio da travessia: quantos mercantes leva, ou o aviso de que hoje não passa nada
        string text = route.Blocked
            ? (route.Trade ? "✖ carga parada" : "✖ travessia cortada")
            : $"⛵ {route.Holds:0}";
        var lbl = Ui.Lbl(text, 14);
        lbl.AddThemeColorOverride("font_color", tint.Lightened(0.45f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        lbl.AddThemeConstantOverride("outline_size", 5);
        var box = new PanelContainer { Position = new Vector2(-52, -22) };
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.08f, 0.72f), 4));
        box.AddChild(lbl);
        var tag = new Node2D { Position = from.Lerp(to, 0.5f), Scale = Vector2.One * _tagScale };
        tag.AddChild(box);
        _tagRoot.AddChild(tag);
    }

    public override void _Draw()
    {
        foreach (var l in _lanes) DrawLane(l);
    }

    /// <summary>Uma travessia: tracejado do cais à costa, com um losango a meio. Cortada, os tracinhos
    /// encurtam para metade (a linha fica esfarrapada) e o meio leva a cruz do bloqueio.</summary>
    private void DrawLane(Lane l)
    {
        var dir = (l.To - l.From).Normalized();
        float len = l.From.DistanceTo(l.To);
        float dash = l.Blocked ? Dash * 0.4f : Dash;
        float step = dash + Gap;
        var faint = l.Tint with { A = l.Blocked ? 0.85f : 0.7f };

        // rasto escuro por baixo: separa a rota do mar sem lhe roubar a cor
        DrawLine(l.From, l.To, Ui.Ink with { A = 0.35f }, Width + 3f);
        for (float at = 0f; at < len; at += step)
            DrawLine(l.From + dir * at, l.From + dir * MathF.Min(len, at + dash), faint, Width);

        var mid = l.From.Lerp(l.To, 0.5f);
        var side = new Vector2(-dir.Y, dir.X);
        if (l.Blocked)
        {
            // o corte: duas barras cruzadas em cima da travessia, do tamanho da linha
            foreach (var v in new[] { (dir + side).Normalized(), (dir - side).Normalized() })
                DrawLine(mid - v * 26f, mid + v * 26f, Ui.Danger, Width + 1f);
        }
        else
        {
            // losango: o comboio a caminho, com quilha para o lado onde vai
            var nose = mid + dir * 20f;
            DrawColoredPolygon(new[] { nose, mid + side * 11f, mid - dir * 20f, mid - side * 11f },
                               l.Tint with { A = 0.9f });
        }
    }

    /// <summary>--smoke: quantas rotas de comboio ficaram desenhadas e quantas delas estão cortadas.</summary>
    public (int Lanes, int Cut) Smoke() { Refresh(); return (_lanes.Count, _lanes.Count(l => l.Blocked)); }
}
