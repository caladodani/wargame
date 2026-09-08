using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>O chão desenhado por cima do mapa político: uma serra na serra, um quarteirão na cidade, uma duna
/// no deserto, uma árvore na mata, gelo na tundra. É a convenção de qualquer carta militar — e é o que o HoI4
/// faz com o relevo — enquanto aqui o terreno só se via abrindo a ficha da região ou trocando o mapa todo para
/// o modo Terreno. O mapa que se olha o tempo inteiro não dizia por onde é que se anda.
///
/// Desenho nosso, feito à mão no Glyph.cs: nada disto é arte de ninguém. Quem escolhe o sinal e a cor é o
/// Relief (a partir da tabela terrain); aqui só se decide onde é que cabe dentro da província e quantos.
///
/// Um nó só para o mundo inteiro, e não um por marca: são milhares e o mapa já tem nós que cheguem. As marcas
/// contam-se uma vez no arranque (o chão não muda de dono) e ficam num _Draw() que o Godot guarda em canvas.</summary>
public partial class TerrainMarks : Node2D
{
    private readonly record struct Mark(Vector2 At, float Size, string Glyph, Color Ink);
    private readonly List<Mark> _marks = new();
    private readonly Dictionary<string, int> _regionsWith = new();

    /// <summary>Marcas desenhadas e regiões marcadas — a conta que a prova headless escreve.</summary>
    public int Count => _marks.Count;
    public int Regions => _regionsWith.Values.Sum();

    /// <summary>Escolhe o sítio das marcas região a região. Corre uma vez, no arranque do mapa.</summary>
    public void Build(World w, RegionRenderer map)
    {
        foreach (var r in w.Regions.Values)
        {
            string? glyph = Relief.Mark(w, r);
            if (glyph is null) continue;
            var rings = map.Rings(r.Id);
            if (rings.Count == 0) continue;

            var ring = rings.OrderByDescending(Area).First();
            var box = Box(ring);
            float span = MathF.Max(box.Size.X, box.Size.Y);
            // A cidade é uma província pequena por natureza — a maior parte das urbanas do mundo cabem numa
            // unha do mapa. O sinal encolhe com ela em vez de a saltar: só um ponto de terra que nem isso
            // chega a ser é que fica sem chão marcado.
            if (span < 3f) continue;
            float size = Math.Clamp(MathF.Sqrt(box.Size.X * box.Size.Y) * 0.34f, 6f, 46f);
            string colour = Relief.Colour(w, r);
            var ink = (colour.Length > 0 ? new Color(colour) : Colors.Gray).Darkened(0.45f) with { A = 0.85f };

            var spots = Spots(ring, box, Relief.Marks(span), size);
            foreach (var at in spots) _marks.Add(new Mark(at, size, glyph, ink));
            if (spots.Count > 0) _regionsWith[glyph] = _regionsWith.GetValueOrDefault(glyph) + 1;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var m in _marks)
            Glyph.Draw(this, new Rect2(m.At - new Vector2(m.Size, m.Size) / 2f, m.Size, m.Size),
                       m.Glyph, m.Ink, MathF.Max(1.1f, m.Size / 13f));
    }

    /// <summary>Onde é que o sinal cabe: peneira uma grelha dentro da caixa da província, fica com os pontos
    /// que estão mesmo dentro do polígono e escolhe os que ficam mais longe da fronteira — um sinal encostado
    /// à linha lê-se como sendo da província do lado. Marcas do mesmo sítio afastam-se entre si.</summary>
    private static List<Vector2> Spots(Vector2[] ring, Rect2 box, int want, float size)
    {
        var inside = new List<(Vector2 At, float Room)>();
        for (int gy = 1; gy <= 5; gy++)
            for (int gx = 1; gx <= 5; gx++)
            {
                var p = box.Position + new Vector2(box.Size.X * gx / 6f, box.Size.Y * gy / 6f);
                if (!Inside(p, ring)) continue;
                inside.Add((p, Room(p, ring)));
            }

        var picked = new List<Vector2>();
        // sem um único ponto da grelha lá dentro (província fina, uma língua de terra) vale o centro do
        // polígono: mais vale um sinal quase certo do que ficar sem chão nenhum marcado.
        if (inside.Count == 0)
        {
            var mid = Vector2.Zero;
            foreach (var v in ring) mid += v;
            picked.Add(mid / ring.Length);
            return picked;
        }
        foreach (var (at, _) in inside.OrderByDescending(s => s.Room))
        {
            if (picked.Count >= want) break;
            if (picked.Any(p => p.DistanceTo(at) < size * 1.3f)) continue;
            picked.Add(at);
        }
        return picked;
    }

    /// <summary>Quanto espaço livre há à volta deste ponto: a distância à fronteira mais próxima.</summary>
    private static float Room(Vector2 p, Vector2[] ring)
    {
        float best = float.MaxValue;
        for (int i = 0; i < ring.Length; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Length];
            var ab = b - a;
            float len = ab.LengthSquared();
            float t = len < 1e-6f ? 0f : Math.Clamp((p - a).Dot(ab) / len, 0f, 1f);
            best = MathF.Min(best, p.DistanceSquaredTo(a + ab * t));
        }
        return MathF.Sqrt(best);
    }

    private static bool Inside(Vector2 p, Vector2[] ring)
    {
        bool inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            if (ring[i].Y > p.Y == ring[j].Y > p.Y) continue;
            float x = (ring[j].X - ring[i].X) * (p.Y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X;
            if (p.X < x) inside = !inside;
        }
        return inside;
    }

    private static Rect2 Box(Vector2[] ring)
    {
        var min = ring[0]; var max = ring[0];
        foreach (var v in ring) { min = min.Min(v); max = max.Max(v); }
        return new Rect2(min, max - min);
    }

    private static float Area(Vector2[] v)
    {
        float a = 0f;
        for (int i = 0; i < v.Length; i++) { var b = v[(i + 1) % v.Length]; a += v[i].X * b.Y - b.X * v[i].Y; }
        return MathF.Abs(a) * 0.5f;
    }

    /// <summary>--smoke: o que ficou desenhado, chão a chão.</summary>
    public string Report(World w)
    {
        var census = Relief.Census(w);
        string spread = string.Join(", ", census.Select(c =>
            $"{c.Ground.Name} {_regionsWith.GetValueOrDefault(c.Ground.Glyph, 0)}/{c.Regions}"));
        return $"{_marks.Count} sinais em {Regions} de {w.Regions.Count} regiões ({spread})";
    }
}
