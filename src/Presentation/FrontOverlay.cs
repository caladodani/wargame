using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A linha da frente desenhada no mapa, como no HoI4: um traço corrido que acompanha a fronteira,
/// com dentes virados para o lado de lá, e a cor a dizer de longe se aquele troço está guarnecido.
///
/// Antes desenhava-se um contacto de cada vez: por cada par (região minha, região dele) uma barra
/// atravessada a meio caminho entre os dois centros. No mapa isso não era uma frente, era um tracejado de
/// traços soltos — não se via onde começava, onde acabava, nem que aquilo era uma linha só. Agora os
/// contactos encadeiam-se (FrontLine, no Core: dois contactos são seguidos quando as regiões de um lado se
/// tocam e as do outro também) e cada um contribui com o troço de fronteira verdadeiro que o RegionRenderer
/// já mediu para desenhar a fronteira nacional. O resultado é uma linha contínua sempre que a geometria dá,
/// que só parte onde a frente parte mesmo: um enclave, uma ilha, uma bifurcação.
///
/// Verde é frente cheia, amarelo é frente a meio, vermelho é frente aberta; um troço sem uma única divisão
/// desenha-se aos bocados, para o buraco se ver antes de custar caro. Cada teatro põe o nome e a percentagem
/// de guarnição no seu centro.
///
/// Quem sabe onde estão os teatros e quanto valem é o TheatreSystem, quem os encadeia é o FrontLine; isto só
/// desenha. Vive dentro do MapView, em coordenadas de mundo, por isso acompanha o pan e o zoom.</summary>
public partial class FrontOverlay : Node2D
{
    /// <summary>Um fio de frente já em coordenadas de mundo: os pontos por onde a linha passa, a normal em
    /// cada um (para onde ficam os dentes) e se aquele troço está sem tropa.</summary>
    private readonly record struct Strand(Vector2[] Points, Vector2[] Normals, bool[] Hole, Color Tint);

    private const float Bar = 46f, Width = 7f, Tooth = 15f;   // barra do contacto solto, grossura e dentes
    private const float ToothStep = 130f;                     // distância entre dentes ao longo do fio
    private const float Probe = 6f;                           // sonda que decide de que lado está a terra dele

    private Game _game = null!;
    private RegionRenderer _shapes = null!;
    private Node2D _tagRoot = null!;
    private readonly List<Strand> _strands = new();
    private int _contacts;
    private string _painted = "";
    private float _tagScale = 1f, _thick = 1f;

    public void Setup(Game game, RegionRenderer shapes)
    {
        _game = game; _shapes = shapes;
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
        // a linha e os dentes são desenhados em unidades de mundo: sem isto engrossavam com o mapa e ao
        // aproximar a frente ficava uma faixa por cima das regiões em vez de um traço na fronteira
        float thick = Mathf.Clamp(_tagScale, 0.3f, 1f);
        if (Mathf.IsEqualApprox(thick, _thick)) return;
        _thick = thick;
        QueueRedraw();
    }

    /// <summary>Recalcula os teatros (e só redesenha quando a frente mexeu). A chave só se grava depois de o
    /// desenho estar feito: uma excepção a meio deixaria a frente congelada até os teatros mudarem por outro
    /// motivo, e o erro só apareceria no log.</summary>
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

            _strands.Clear();
            _contacts = 0;
            Ui.Clear(_tagRoot);
            foreach (var t in fronts) Build(w, t);
            _painted = key;
            QueueRedraw();
        }
        catch (Exception ex) { _painted = ""; GD.PushError("FrontOverlay: " + ex); }
    }

    private void Build(World w, Theatre t)
    {
        var raw = FrontLine.Contacts(w, t);
        if (raw.Count == 0) return;
        var tint = Tint(t.Coverage);

        // cada contacto ganha o seu troço de fronteira verdadeiro (o mesmo que pinta a fronteira nacional) e,
        // com ele, um ponto e uma normal fiáveis; sem troço fica o ponto de aproximação máxima entre os anéis
        var arcs = new Dictionary<(int, int), Vector2[]>();
        var contacts = new List<FrontContact>(raw.Count);
        foreach (var c in raw)
        {
            var arc = LongestArc(c.MineId, c.FoeId);
            Vector2 at = arc is null ? Approach(c) : arc[arc.Length / 2];
            if (arc is not null) arcs[(c.MineId, c.FoeId)] = arc;
            var n = Normal(c, at);
            contacts.Add(c with { X = at.X, Y = at.Y, NormalX = n.X, NormalY = n.Y });
        }
        _contacts += contacts.Count;

        var centre = Vector2.Zero;
        foreach (var c in contacts) centre += new Vector2(c.X, c.Y);
        centre /= contacts.Count;

        foreach (var strand in FrontLine.Chain(w, contacts)) Weave(strand, arcs, tint);

        // etiqueta no meio do troço: quem é a frente, como está guarnecida e quantos buracos tem
        string text = $"🛡 {t.Name} · {t.Coverage:P0}" + (t.Holes > 0 ? $" · ☠ {t.Holes}" : "");
        var lbl = Ui.Lbl(text, 15);
        lbl.AddThemeColorOverride("font_color", tint.Lightened(0.4f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        lbl.AddThemeConstantOverride("outline_size", 5);
        var box = new PanelContainer { Position = new Vector2(-70, -60) };
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.08f, 0.74f), 4));
        box.AddChild(lbl);
        var tag = new Node2D { Position = centre, Scale = Vector2.One * _tagScale };
        tag.AddChild(box);
        _tagRoot.AddChild(tag);
    }

    /// <summary>Cose os contactos de um fio numa polilinha só: cada um entra com o seu troço de fronteira, na
    /// ponta que continua o traço anterior, e o salto entre troços é uma recta — é onde a simplificação dos
    /// polígonos deixou buraco, e a fronteira verdadeira passa mesmo por ali.</summary>
    private void Weave(List<FrontContact> strand, Dictionary<(int, int), Vector2[]> arcs, Color tint)
    {
        var pts = new List<Vector2>(); var nor = new List<Vector2>(); var hole = new List<bool>();
        for (int i = 0; i < strand.Count; i++)
        {
            var c = strand[i];
            var here = new Vector2(c.X, c.Y);
            var piece = arcs.TryGetValue((c.MineId, c.FoeId), out var a) ? a : new[] { here };
            if (piece.Length > 1)
            {
                // a ponta que fica para trás é a que está mais perto do que já está desenhado (ou, no
                // princípio do fio, a que está mais longe do contacto seguinte)
                var anchor = pts.Count > 0 ? pts[^1]
                           : i + 1 < strand.Count ? new Vector2(strand[i + 1].X, strand[i + 1].Y) : here;
                bool flip = pts.Count > 0
                    ? anchor.DistanceSquaredTo(piece[^1]) < anchor.DistanceSquaredTo(piece[0])
                    : anchor.DistanceSquaredTo(piece[0]) < anchor.DistanceSquaredTo(piece[^1]);
                if (flip) piece = piece.Reverse().ToArray();
            }
            var normal = new Vector2(c.NormalX, c.NormalY);
            foreach (var p in piece) { pts.Add(p); nor.Add(normal); hole.Add(c.Hole); }
        }
        if (pts.Count > 0) _strands.Add(new Strand(pts.ToArray(), nor.ToArray(), hole.ToArray(), tint));
    }

    /// <summary>O maior troço de fronteira entre estas duas regiões, ou nada se a geometria não deu nenhum.</summary>
    private Vector2[]? LongestArc(int mine, int foe)
    {
        Vector2[]? best = null; float bestLen = 0f;
        foreach (var arc in _shapes.BorderWith(mine, foe))
        {
            float len = 0f;
            for (int i = 0; i + 1 < arc.Length; i++) len += arc[i].DistanceTo(arc[i + 1]);
            if (best is null || len > bestLen) { best = arc; bestLen = len; }
        }
        return best is { Length: > 1 } ? best : null;
    }

    /// <summary>Onde as duas regiões mais se aproximam, quando não há aresta comum: o meio do par de vértices
    /// mais próximo. Sem anéis, fica o meio dos dois centros (que é o que o FrontLine já pôs no contacto).</summary>
    private Vector2 Approach(FrontContact c)
    {
        var mine = _shapes.Rings(c.MineId); var foe = _shapes.Rings(c.FoeId);
        Vector2 best = new(c.X, c.Y); float bestD = float.MaxValue;
        foreach (var ra in mine)
            foreach (var pa in ra)
                foreach (var rb in foe)
                    foreach (var pb in rb)
                    {
                        float d = pa.DistanceSquaredTo(pb);
                        if (d < bestD) { bestD = d; best = pa.Lerp(pb, 0.5f); }
                    }
        return best;
    }

    /// <summary>Para que lado ficam os dentes. O centro da região do inimigo engana (o centróide de uma
    /// região comprida cai fora dela), por isso pergunta-se ao polígono: dá-se um passo curto para cada lado
    /// e fica o que aterra dentro da terra dele. Sem resposta, vale o centro.</summary>
    private Vector2 Normal(FrontContact c, Vector2 at)
    {
        var fallback = new Vector2(c.NormalX, c.NormalY);
        var foe = _shapes.Rings(c.FoeId);
        if (foe.Count == 0) return fallback;
        bool Inside(Vector2 p) { foreach (var ring in foe) if (Geometry2D.IsPointInPolygon(p, ring)) return true; return false; }
        bool plus = Inside(at + fallback * Probe), minus = Inside(at - fallback * Probe);
        return plus == minus ? fallback : plus ? fallback : -fallback;
    }

    public override void _Draw()
    {
        foreach (var s in _strands) DrawStrand(s);
    }

    /// <summary>Um fio: rasto escuro por baixo (separa a linha do terreno), o traço da cor da guarnição por
    /// cima — aos bocados onde a frente está vazia — e dentes de espaço a espaço virados ao inimigo.</summary>
    private void DrawStrand(Strand s)
    {
        var p = s.Points;
        float w = Width * _thick;
        if (p.Length == 1)
        {
            // contacto solto (uma ilha, um enclave): não há fio nenhum, fica a barra atravessada de dantes
            var dir = s.Normals[0];
            var side = new Vector2(-dir.Y, dir.X);
            var a = p[0] - side * Bar * _thick * 0.5f; var b = p[0] + side * Bar * _thick * 0.5f;
            DrawLine(a, b, Ui.Ink with { A = 0.45f }, w + 4f * _thick);
            if (s.Hole[0]) for (int i = 0; i < 3; i++) DrawLine(a.Lerp(b, i * 0.36f), a.Lerp(b, i * 0.36f + 0.18f), s.Tint, w);
            else DrawLine(a, b, s.Tint, w);
            DrawTooth(p[0], dir, s.Tint);
            return;
        }

        DrawPolyline(p, Ui.Ink with { A = 0.45f }, w + 4f * _thick);
        for (int i = 0; i + 1 < p.Length; i++)
        {
            if (!s.Hole[i]) DrawLine(p[i], p[i + 1], s.Tint, w);
            else DrawLine(p[i].Lerp(p[i + 1], 0.15f), p[i].Lerp(p[i + 1], 0.55f), s.Tint, w);
        }

        // dentes: um a cada ToothStep de linha andada, e sempre pelo menos um, senão um fio curto ficava
        // liso e não dizia de que lado está a terra deles
        float walked = ToothStep * 0.5f;
        bool any = false;
        for (int i = 0; i + 1 < p.Length; i++)
        {
            float seg = p[i].DistanceTo(p[i + 1]);
            walked += seg;
            if (walked < ToothStep) continue;
            walked = 0f; any = true;
            DrawTooth(p[i].Lerp(p[i + 1], 0.5f), s.Normals[i], s.Tint);
        }
        if (!any) DrawTooth(p[p.Length / 2], s.Normals[p.Length / 2], s.Tint);
    }

    private void DrawTooth(Vector2 at, Vector2 dir, Color tint)
    {
        var side = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[] { at + dir * Tooth * _thick, at + side * 7f * _thick, at - side * 7f * _thick },
                           tint with { A = 0.95f });
    }

    /// <summary>--smoke: quantos contactos ficaram desenhados, quantos deles são buraco na frente e em
    /// quantos fios contínuos é que a linha ficou (menos fios do que contactos = frente cosida).</summary>
    public (int Edges, int Holes, int Strands) Smoke()
    {
        Refresh();
        return (_contacts, _strands.Sum(CountHoles), _strands.Count);
    }

    /// <summary>Troços sem tropa dentro de um fio (contam-se as passagens de guarnecido para vazio).</summary>
    private static int CountHoles(Strand s)
    {
        int n = 0;
        for (int i = 0; i < s.Hole.Length; i++) if (s.Hole[i] && (i == 0 || !s.Hole[i - 1])) n++;
        return n;
    }
}
