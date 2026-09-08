using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A mobília do mapa desenhada: chapas de praça, âncoras de porto, fortes e as chapas de batalha.
///
/// No HoI4 estas coisas não são texto — são objectos com forma e cor, e é por isso que o mapa se lê de
/// relance. A forma da chapa diz o grau da praça (estrela na capital, pentágono, quadrado, círculo à medida
/// que vale menos) e a cor diz de quem ela é: verde a nossa, vermelha a de quem está em guerra connosco,
/// cinzenta a de terceiros e a que está por descobrir. A batalha leva um ponteiro com o número da vantagem
/// e a cor de quem a está a levar a melhor.
///
/// Um nó só para o mundo inteiro, como o chão e as cidades: são centenas de chapas e cada uma não pode ser
/// um nó. Quem decide o que se marca é o MapMarks e o BattleOdds (Core); aqui só se desenha.
///
/// A lista completa faz-se com o mundo parado (Refresh, chamado pelo RegionRenderer); o zoom só escolhe
/// quais das chapas entram, com contas puras sobre essa lista — durante um beliscão o zoom muda a cada
/// fotograma e ninguém pode andar a varrer o mundo a esse ritmo.</summary>
public partial class MapFurniture : Node2D
{
    private List<MapMarks.Prize> _all = new();
    private List<MapMarks.Prize> _shown = new();
    private List<MapMarks.Work> _works = new();
    private List<(float X, float Y, int Number, int Mood)> _battles = new();

    private float _zoom = -1f;
    private int _floor = 5, _max = 600;
    private float _prizeZoom = 0.16f, _nameZoom = 0.85f, _baseZoom = 0.45f;

    // As três cores do mapa de guerra. Não são as cores dos países: são a relação com quem está a olhar.
    private static readonly Color Ours = new(0.42f, 0.86f, 0.46f);
    private static readonly Color Theirs = new(0.93f, 0.32f, 0.29f);
    private static readonly Color Neutral = new(0.82f, 0.84f, 0.88f);
    private static readonly Color Ink = new(0.05f, 0.06f, 0.09f, 0.92f);
    private static readonly Color Paper = new(0.98f, 0.98f, 1f, 0.96f);
    private static readonly Color Even = new(0.97f, 0.82f, 0.32f);

    private static Color Of(int side) => side > 0 ? Ours : side < 0 ? Theirs : Neutral;

    /// <summary>O mundo mudou: refaz a lista toda. Só é chamado com o mundo parado.</summary>
    public void Refresh(World w, int viewerId)
    {
        _floor = (int)w.Rule("victory_marker_min", 5f);
        _max = (int)w.Rule("mark_draw_max", 600f);
        _prizeZoom = w.Rule("mark_prize_zoom", 0.16f);
        _nameZoom = w.Rule("mark_name_zoom", 0.85f);
        _baseZoom = w.Rule("mark_base_zoom", 0.45f);
        _all = MapMarks.All(w, viewerId);
        _works = MapMarks.Works(w, viewerId);
        _battles = BattleOdds.Shown(w, viewerId)
            .Where(b => w.Regions.ContainsKey(b.RegionId))
            .Select(b => (w.Regions[b.RegionId].CenterX, w.Regions[b.RegionId].CenterY,
                          BattleOdds.Number(w, b, viewerId), BattleOdds.Mood(w, b, viewerId)))
            .ToList();
        Pick(_zoom < 0f ? 1f : _zoom, force: true);
    }

    /// <summary>O zoom mudou: escolhe o que cabe. Entra arredondado a degraus de 0,05, como as cidades.</summary>
    public void SetZoom(float zoom)
    {
        float step = MathF.Round(zoom * 20f) / 20f;
        if (Mathf.IsEqualApprox(step, _zoom)) return;
        Pick(step, force: false);
    }

    private void Pick(float step, bool force)
    {
        _zoom = step;
        // sem tecto aqui: o tecto é do desenho e conta o que está no ecrã. Cortar a lista do mundo pelas
        // praças que mais valem punha as chapas todas na Europa e deixava sem chapa a terra onde se está.
        _shown = step < _prizeZoom ? new List<MapMarks.Prize>()
                                   : MapMarks.Shown(_all, _floor, step, int.MaxValue);
        QueueRedraw();
        _ = force;
    }

    /// <summary>Quantas chapas cabem nesta escala, para a prova headless.</summary>
    public int Drawn => _shown.Count;
    public int Named => _zoom >= _nameZoom ? _shown.Count : 0;
    public int Works => _zoom >= _baseZoom ? _works.Count : 0;
    public int Battles => _battles.Count;

    /// <summary>O pedaço de mundo que está no ecrã, com uma margem para as chapas de meia-entrada. Sem
    /// câmara (headless, ou antes do primeiro fotograma) devolve o mundo todo.</summary>
    private Rect2 Window()
    {
        var view = GetViewportRect();
        if (view.Size.X <= 1f || view.Size.Y <= 1f) return new Rect2(-1e9f, -1e9f, 2e9f, 2e9f);
        var inv = GetCanvasTransform().AffineInverse();
        var rect = new Rect2(inv * view.Position, Vector2.Zero)
            .Expand(inv * (view.Position + view.Size))
            .Expand(inv * new Vector2(view.Position.X, view.Position.Y + view.Size.Y))
            .Expand(inv * new Vector2(view.Position.X + view.Size.X, view.Position.Y));
        return rect.Grow(40f / MathF.Max(0.05f, _zoom));
    }

    public override void _Draw()
    {
        float k = 1f / MathF.Max(0.05f, _zoom);        // unidades de mundo que valem um pixel no ecrã
        var font = ThemeDB.FallbackFont;
        var win = Window();

        if (_zoom >= _baseZoom)
            foreach (var work in _works)
            {
                if (!win.HasPoint(new Vector2(work.X, work.Y))) continue;
                var at = new Vector2(work.X, work.Y) + new Vector2(0f, 11f * k);
                float r = 5.5f * k;
                DrawCircle(at, r + 1.2f * k, Ink);
                DrawCircle(at, r, new Color(Of(work.Side), 0.85f));
                Glyph.Draw(this, new Rect2(at - Vector2.One * r * 0.72f, Vector2.One * r * 1.44f),
                           work.Glyph, Ink, MathF.Max(1f, 1.4f * k));
                if (work.Level > 1)
                    DrawString(font, at + new Vector2(r + 1.5f * k, r * 0.6f), work.Level.ToString(),
                               HorizontalAlignment.Left, -1, Math.Clamp((int)(9f * k), 7, 40), Paper);
            }

        int size = Math.Clamp((int)MathF.Round(11f * k / 2f) * 2, 8, 64);
        int budget = Math.Max(1, _max);                // tecto do que se desenha de uma vez, contado no ecrã
        foreach (var p in _shown)
        {
            var at = new Vector2(p.X, p.Y);
            if (!win.HasPoint(at)) continue;
            if (budget-- <= 0) break;                  // a lista vem das que mais valem: o corte cai nas menores
            float r = (p.Capital ? 8.5f : p.Points >= 5 ? 7f : 6f) * k;
            var face = Of(p.Side);
            var pts = Shape(p.Shape, at, r);
            DrawColoredPolygon(Ring(pts, at, 1.3f * k), Ink);         // moldura escura: lê-se em cima de qualquer cor
            DrawColoredPolygon(pts, face);
            if (!p.Seen) DrawColoredPolygon(Ring(pts, at, -1.9f * k), new Color(Ink, 0.55f));   // por descobrir: chapa oca
            if (_zoom >= _prizeZoom * 2.5f)
                DrawString(font, at + new Vector2(-r, size * 0.36f), p.Points.ToString(),
                           HorizontalAlignment.Center, r * 2f, size, Ink);
            if (_zoom < _nameZoom) continue;
            var pen = at + new Vector2(r + 3.5f * k, size * 0.36f);
            DrawString(font, pen + new Vector2(k, k), p.Name, HorizontalAlignment.Left, -1, size, new Color(0, 0, 0, 0.75f));
            DrawString(font, pen, p.Name, HorizontalAlignment.Left, -1, size, Paper);
        }

        foreach (var (x, y, number, mood) in _battles)
        {
            if (!win.HasPoint(new Vector2(x, y))) continue;
            var at = new Vector2(x, y) + new Vector2(0f, -13f * k);
            var face = mood > 0 ? Ours : mood < 0 ? Theirs : Even;
            // o ponteiro: uma seta virada para a terra onde se combate, com a chapa do número por cima
            var tip = at + new Vector2(0f, 6.5f * k);
            DrawColoredPolygon(new[] { tip, at + new Vector2(-4.5f * k, -1f * k), at + new Vector2(4.5f * k, -1f * k) }, Ink);
            float w2 = 9f * k, h2 = 6f * k;
            DrawRect(new Rect2(at - new Vector2(w2, h2 + 1f * k), new Vector2(w2 * 2f, h2 * 2f)), Ink);
            DrawRect(new Rect2(at - new Vector2(w2 - 1.1f * k, h2 - 1.1f * k + 1f * k),
                               new Vector2((w2 - 1.1f * k) * 2f, (h2 - 1.1f * k) * 2f)), face);
            int bs = Math.Clamp((int)(10f * k), 7, 44);
            DrawString(font, at + new Vector2(-w2, bs * 0.34f - 1f * k), number.ToString(),
                       HorizontalAlignment.Center, w2 * 2f, bs, Ink);
        }
    }

    /// <summary>A forma da chapa. O nome vem da linha victory_tier — o que aqui não estiver sai redondo.</summary>
    private static Vector2[] Shape(string name, Vector2 at, float r) => name switch
    {
        "estrela" => Star(at, r, r * 0.44f, 5),
        "pentagono" => Regular(at, r, 5, -Mathf.Pi / 2f),
        "quadrado" => Regular(at, r * 0.95f, 4, Mathf.Pi / 4f),
        _ => Regular(at, r, 18, 0f),
    };

    private static Vector2[] Regular(Vector2 at, float r, int sides, float turn)
    {
        var pts = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = turn + Mathf.Tau * i / sides;
            pts[i] = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
        }
        return pts;
    }

    private static Vector2[] Star(Vector2 at, float outer, float inner, int points)
    {
        var pts = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float a = -Mathf.Pi / 2f + Mathf.Pi * i / points;
            float rad = i % 2 == 0 ? outer : inner;
            pts[i] = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rad;
        }
        return pts;
    }

    /// <summary>A mesma forma, crescida (ou encolhida) a partir do centro: é assim que se faz a moldura.</summary>
    private static Vector2[] Ring(Vector2[] pts, Vector2 at, float grow)
    {
        var outv = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            var d = pts[i] - at;
            float len = d.Length();
            outv[i] = len < 0.0001f ? pts[i] : at + d * ((len + grow) / len);
        }
        return outv;
    }

    /// <summary>O que está desenhado, em palavras, para a prova headless.</summary>
    public string Report()
    {
        int works = Works, battles = Battles;
        return $"{Drawn} chapas ({_shown.Count(p => p.Capital)} capitais, {_shown.Count(p => p.Side > 0)} nossas, "
             + $"{_shown.Count(p => p.Side < 0)} do inimigo, tecto de {_max} no ecrã), "
             + $"{works} obra{(works == 1 ? "" : "s")}, {battles} batalha{(battles == 1 ? "" : "s")}";
    }
}
