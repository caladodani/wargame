using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>O caldeirão desenhado no mapa: a terra fechada dentro do anel às riscas, o anel por cima em
/// traço grosso com os ganchos virados para dentro, e uma chapa ao meio com o que lá está preso e quantos
/// dias faltam.
///
/// O cerco já era a manobra mais cara do jogo — quem o fecha ganha os homens todos, quem o apanha perde-os
/// — mas no mapa não se via nada: as divisões cortadas eram contadores iguais aos outros. No HoI4 o
/// caldeirão é a imagem da campanha: a bolsa fecha-se à vista, e do lado de dentro conta-se o tempo. É essa
/// leitura que aqui se recria, com desenho nosso (riscas a 135°, ao contrário das riscas de ocupação, para
/// as duas coisas nunca se confundirem no mesmo sítio).
///
/// Quem sabe o que é uma bolsa é o Pockets, no Core; isto só desenha. Vive dentro do MapView, em
/// coordenadas de mundo, por isso acompanha o pan e o zoom.</summary>
public partial class PocketOverlay : Node2D
{
    /// <summary>Uma bolsa já em coordenadas de mundo: as riscas do recheio, os troços do anel e a cor.</summary>
    private readonly record struct Cauldron(Vector2[][] Bands, Vector2[][] Ring, Color Tint, bool Sealed);

    private const float BandGap = 34f, BandWidth = 9f;   // riscas do recheio, mais largas que as da ocupação
    private const int BandMax = 40;                      // tecto por região: o mapa mundial tem de aguentar
    private const float RingWidth = 9f, Hook = 16f, HookStep = 150f;

    private Game _game = null!;
    private RegionRenderer _shapes = null!;
    private Node2D _tagRoot = null!;
    private readonly List<Cauldron> _pots = new();
    private string _painted = "";
    private float _tagScale = 1f, _thick = 1f;
    private int _pockets, _divisions;

    public void Setup(Game game, RegionRenderer shapes)
    {
        _game = game; _shapes = shapes;
        ZIndex = 2;                                   // por cima da frente, por baixo das setas e das rotas
        _tagRoot = new Node2D { Name = "PocketTags" };
        AddChild(_tagRoot);
    }

    /// <summary>Vermelho para as nossas bolsas (é o nosso exército a morrer lá dentro), dourado para as que
    /// nós fechámos (é o prémio da manobra) e cinzento para as guerras dos outros.</summary>
    private Color Tint(Pocket p, int pid) =>
        p.CountryId == pid ? new Color(0.86f, 0.18f, 0.16f)
        : p.RingCountryId == pid ? new Color(0.92f, 0.74f, 0.24f)
        : new Color(0.62f, 0.6f, 0.58f);

    /// <summary>As chapas mantêm-se legíveis ao afastar, como as da linha da frente; o traço do anel é em
    /// unidades de mundo e por isso encolhe com o zoom em vez de engrossar por cima do mapa.</summary>
    public void SetZoom(float zoom)
    {
        _tagScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 0.05f, 6f);
        foreach (var c in _tagRoot.GetChildren()) if (c is Node2D n) n.Scale = Vector2.One * _tagScale;
        float thick = Mathf.Clamp(_tagScale, 0.3f, 1.2f);
        if (Mathf.IsEqualApprox(thick, _thick)) return;
        _thick = thick;
        QueueRedraw();
    }

    /// <summary>Refaz as bolsas do mundo (e só redesenha quando alguma mexeu — uma bolsa muda de dia a dia,
    /// não de fotograma a fotograma). A chave só se grava depois do desenho feito, como na linha da frente:
    /// uma excepção a meio deixaria o mapa congelado com uma bolsa que já se desfez.</summary>
    public void Refresh()
    {
        try
        {
            var w = _game.World;
            int pid = _game.PlayerId ?? 0;
            var pots = Pockets.All(w);
            string key = string.Join(";", pots.Select(p =>
                $"{p.CountryId}:{p.RingCountryId}:{string.Join(",", p.RegionIds)}:{p.Divisions}:{p.DaysLeft}:{p.Sealed}")) + "|" + pid;
            if (key == _painted) return;

            _pots.Clear();
            _pockets = pots.Count; _divisions = pots.Sum(p => p.Divisions);
            Ui.Clear(_tagRoot);
            foreach (var p in pots) Build(w, p, pid);
            _painted = key;
            QueueRedraw();
        }
        catch (Exception ex) { _painted = ""; GD.PushError("PocketOverlay: " + ex); }
    }

    private void Build(World w, Pocket p, int pid)
    {
        var tint = Tint(p, pid);
        var bands = new List<Vector2[]>();
        var ring = new List<Vector2[]>();
        var centre = Vector2.Zero; int counted = 0;
        var inside = p.RegionIds.ToHashSet();

        foreach (int id in p.RegionIds)
        {
            if (!w.Regions.TryGetValue(id, out var r)) continue;
            centre += new Vector2(r.CenterX, r.CenterY); counted++;
            foreach (var ringPts in _shapes.Rings(id)) Bands(ringPts, bands);
            // o anel: só o lado da bolsa que encosta a terra que não é dela — por dentro não se desenha nada
            foreach (int nb in r.Neighbours)
            {
                if (inside.Contains(nb)) continue;
                foreach (var arc in _shapes.BorderWith(id, nb)) if (arc.Length >= 2) ring.Add(arc);
            }
        }
        if (counted == 0) return;
        _pots.Add(new Cauldron(bands.ToArray(), ring.ToArray(), tint, p.Sealed));

        // a chapa: o que está preso lá dentro e o tempo que lhe resta, no coração da bolsa
        var heart = w.Regions.TryGetValue(p.HeartId, out var h) ? new Vector2(h.CenterX, h.CenterY) : centre / counted;
        string prazo = p.DaysLeft is int n ? $"⏳ {n} d" : p.Sealed ? "fechada" : "com saída";
        string quem = w.Countries.TryGetValue(p.CountryId, out var c) ? c.Tag : "?";
        var lbl = Ui.Lbl($"⛓ CERCO · {quem} · {p.Divisions} div · {prazo}", 15);
        lbl.AddThemeColorOverride("font_color", tint.Lightened(0.45f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        lbl.AddThemeConstantOverride("outline_size", 5);
        var box = new PanelContainer { Position = new Vector2(-80, -46) };
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.03f, 0.03f, 0.8f), 4));
        box.AddChild(lbl);
        var tag = new Node2D { Position = heart, Scale = Vector2.One * _tagScale };
        tag.AddChild(box);
        _tagRoot.AddChild(tag);
    }

    /// <summary>As riscas do recheio de um anel de região: faixas de x−y constante (135°, ao contrário das
    /// riscas de ocupação) cortadas pelo Geometry2D à forma da terra.</summary>
    private static void Bands(Vector2[] ring, List<Vector2[]> into)
    {
        if (ring.Length < 3) return;
        float minX = ring[0].X, maxX = ring[0].X, minY = ring[0].Y, maxY = ring[0].Y;
        foreach (var pt in ring)
        {
            minX = MathF.Min(minX, pt.X); maxX = MathF.Max(maxX, pt.X);
            minY = MathF.Min(minY, pt.Y); maxY = MathF.Max(maxY, pt.Y);
        }
        float span = maxY - minY + 8f;
        float x0 = minX - span, x1 = maxX + span;
        int made = 0;
        for (float t = MathF.Floor((minX - maxY) / BandGap) * BandGap; t <= maxX - minY && made < BandMax; t += BandGap)
        {
            var band = new[]
            {
                new Vector2(x0, x0 - t), new Vector2(x1, x1 - t),
                new Vector2(x1, x1 - t - BandWidth), new Vector2(x0, x0 - t - BandWidth),
            };
            foreach (var piece in Geometry2D.IntersectPolygons(band, ring))
            {
                if (piece.Length < 3 || made >= BandMax) continue;
                into.Add(piece); made++;
            }
        }
    }

    public override void _Draw()
    {
        foreach (var pot in _pots)
        {
            foreach (var band in pot.Bands) DrawColoredPolygon(band, pot.Tint with { A = 0.28f });
            foreach (var arc in pot.Ring) DrawRing(arc, pot);
        }
    }

    /// <summary>Um troço do anel: rasto escuro por baixo, o traço da bolsa por cima (contínuo quando está
    /// fechada, aos bocados quando ainda há saída) e ganchos virados para dentro de espaço a espaço — é o
    /// desenho de um saco a apertar, e lê-se de longe sem se confundir com a linha da frente.</summary>
    private void DrawRing(Vector2[] arc, Cauldron pot)
    {
        float w = RingWidth * _thick;
        DrawPolyline(arc, Ui.Ink with { A = 0.5f }, w + 4f * _thick);
        if (pot.Sealed) DrawPolyline(arc, pot.Tint, w);
        else for (int i = 0; i + 1 < arc.Length; i++)
            DrawLine(arc[i], arc[i].Lerp(arc[i + 1], 0.55f), pot.Tint, w);

        float walked = HookStep * 0.5f;
        for (int i = 0; i + 1 < arc.Length; i++)
        {
            float seg = arc[i].DistanceTo(arc[i + 1]);
            walked += seg;
            if (walked < HookStep || seg < 1f) continue;
            walked = 0f;
            var dir = (arc[i + 1] - arc[i]) / seg;
            var inward = new Vector2(dir.Y, -dir.X);     // para dentro da bolsa: o gancho aponta ao recheio
            var at = arc[i].Lerp(arc[i + 1], 0.5f);
            DrawColoredPolygon(new[] { at + inward * Hook * _thick, at + dir * 7f * _thick, at - dir * 7f * _thick },
                               pot.Tint with { A = 0.95f });
        }
    }

    /// <summary>--smoke: quantas bolsas ficaram desenhadas, quanta tropa lá está presa, quantas faixas de
    /// risca e quantos troços de anel — sem isto, um cerco podia deixar de aparecer no mapa sem nada dar
    /// erro.</summary>
    public (int Pockets, int Divisions, int Bands, int Ring, int Tags) Smoke()
    {
        Refresh();
        return (_pockets, _divisions, _pots.Sum(p => p.Bands.Length), _pots.Sum(p => p.Ring.Length),
                _tagRoot.GetChildCount());
    }
}
