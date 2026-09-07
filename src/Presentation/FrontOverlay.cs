using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A linha da frente desenhada no mapa, como no HoI4: onde a nossa terra encosta à do inimigo aparece
/// uma barra grossa com dentes virados para o lado de lá, e a cor diz de longe se aquele troço está guarnecido.
///
/// Sem isto, saber onde estava a frente era ler o mapa região a região à procura de duas cores encostadas — e
/// pior, não havia maneira nenhuma de ver que o troço do sul não tinha uma única divisão. Agora vê-se: verde é
/// frente cheia, amarelo é frente a meio, vermelho é frente aberta, e um troço com buracos leva a caveira do
/// aviso na etiqueta. Cada teatro põe o nome e a percentagem de guarnição no seu centro.
///
/// Quem sabe onde estão os teatros e quanto valem é o TheatreSystem; isto só desenha. Vive dentro do MapView,
/// em coordenadas de mundo, por isso acompanha o pan e o zoom como as setas dos planos.</summary>
public partial class FrontOverlay : Node2D
{
    /// <summary>Um contacto já em coordenadas de mundo: da nossa região para a região deles.</summary>
    private readonly record struct Edge(Vector2 Mine, Vector2 Theirs, Color Tint, bool Hole);

    private const float Bar = 46f, Width = 7f, Tooth = 15f;   // barra, grossura e dentes, em unidades de mundo

    private Game _game = null!;
    private Node2D _tagRoot = null!;
    private readonly List<Edge> _edges = new();
    private string _painted = "";
    private float _tagScale = 1f;

    public void Setup(Game game)
    {
        _game = game;
        ZIndex = 1;                                   // por baixo das rotas e das setas: é o chão da guerra
        _tagRoot = new Node2D { Name = "FrontTags" };
        AddChild(_tagRoot);
    }

    /// <summary>Cor da guarnição: frente cheia em verde, vazia em vermelho, com o amarelo do meio pelo caminho.</summary>
    public static Color Tint(float coverage) =>
        coverage >= 0.5f ? Ui.Accent.Lerp(Ui.Good, (coverage - 0.5f) * 2f)
                         : Ui.Danger.Lerp(Ui.Accent, Mathf.Clamp(coverage, 0f, 0.5f) * 2f);

    /// <summary>As etiquetas mantêm-se legíveis ao afastar, como as dos planos.</summary>
    public void SetZoom(float zoom)
    {
        _tagScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 0.05f, 6f);
        foreach (var c in _tagRoot.GetChildren()) if (c is Node2D n) n.Scale = Vector2.One * _tagScale;
    }

    /// <summary>Recalcula os teatros (e só redesenha quando a frente mexeu).</summary>
    public void Refresh()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid) return;
            var fronts = TheatreSystem.Of(w, pid);

            string key = string.Join(";", fronts.Select(t =>
                $"{t.FoeId}:{string.Join(",", t.RegionIds)}:{(int)(t.Coverage * 20f)}:{t.Holes}"));
            if (key == _painted) return;
            _painted = key;

            _edges.Clear();
            Ui.Clear(_tagRoot);
            foreach (var t in fronts) Build(w, t);
            QueueRedraw();
        }
        catch (Exception ex) { GD.PushError("FrontOverlay: " + ex); }
    }

    private void Build(World w, Theatre t)
    {
        var tint = Tint(t.Coverage);
        var centre = Vector2.Zero;
        int seen = 0;

        foreach (int id in t.RegionIds)
        {
            if (!w.Regions.TryGetValue(id, out var mine)) continue;
            var here = new Vector2(mine.CenterX, mine.CenterY);
            bool hole = !w.Divisions.Values.Any(d => d.CountryId == mine.ControllerId && d.RegionId == id);
            foreach (int n in mine.Neighbours)
            {
                if (!w.Regions.TryGetValue(n, out var foe) || foe.ControllerId != t.FoeId) continue;
                var there = new Vector2(foe.CenterX, foe.CenterY);
                if (here.DistanceTo(there) < 1f) continue;
                _edges.Add(new Edge(here, there, tint, hole));
                centre += here.Lerp(there, 0.5f); seen++;
            }
        }
        if (seen == 0) return;

        // etiqueta no meio do troço: quem é a frente, como está guarnecida e quantos buracos tem
        string text = $"🛡 {t.Name} · {t.Coverage:P0}" + (t.Holes > 0 ? $" · ☠ {t.Holes}" : "");
        var lbl = Ui.Lbl(text, 15);
        lbl.AddThemeColorOverride("font_color", tint.Lightened(0.4f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        lbl.AddThemeConstantOverride("outline_size", 5);
        var box = new PanelContainer { Position = new Vector2(-70, -60) };
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.08f, 0.74f), 4));
        box.AddChild(lbl);
        var tag = new Node2D { Position = centre / seen, Scale = Vector2.One * _tagScale };
        tag.AddChild(box);
        _tagRoot.AddChild(tag);
    }

    public override void _Draw()
    {
        foreach (var e in _edges) DrawEdge(e);
    }

    /// <summary>Um contacto: barra atravessada a meio caminho entre as duas regiões, com dentes a apontar ao
    /// inimigo. Um troço sem tropa nenhuma leva a barra aos bocados, para o buraco se ver antes de custar caro.</summary>
    private void DrawEdge(Edge e)
    {
        var dir = (e.Theirs - e.Mine).Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        var mid = e.Mine.Lerp(e.Theirs, 0.5f);
        var a = mid - side * Bar * 0.5f;
        var b = mid + side * Bar * 0.5f;

        DrawLine(a, b, Ui.Ink with { A = 0.45f }, Width + 4f);        // rasto escuro: separa a linha do terreno
        if (e.Hole)
        {
            // frente aberta: a barra deixa de ser barra e passa a três tracinhos com folga entre eles
            for (int i = 0; i < 3; i++)
                DrawLine(a.Lerp(b, i * 0.36f), a.Lerp(b, i * 0.36f + 0.18f), e.Tint, Width);
        }
        else DrawLine(a, b, e.Tint, Width);

        // dentes virados ao inimigo: dizem de que lado está a terra deles sem ler o mapa
        foreach (float at in new[] { 0.25f, 0.75f })
        {
            var foot = a.Lerp(b, at);
            DrawColoredPolygon(new[] { foot + dir * Tooth, foot + side * 7f, foot - side * 7f },
                               e.Tint with { A = 0.95f });
        }
    }

    /// <summary>--smoke: quantos contactos ficaram desenhados e quantos deles são buraco na frente.</summary>
    public (int Edges, int Holes) Smoke() { Refresh(); return (_edges.Count, _edges.Count(e => e.Hole)); }
}
