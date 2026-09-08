using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>A rede ferroviária desenhada no mapa: traço escuro com travessas entre o centro de duas
/// regiões vizinhas que têm carril, mais grosso onde a linha é melhor.
///
/// Existe porque a logística não se via. O abastecimento era uma percentagem na ficha da divisão e mais
/// nada: não havia como olhar para o mapa e perceber por onde é que a rede chega, nem porque é que uma
/// ofensiva secou a 400 km de casa. No HoI4 a via férrea é a primeira coisa que se lê num mapa de
/// logística — e é ela que se repara e se sobe antes de uma ofensiva. Aqui é a mesma leitura: onde há
/// linha, o salto da rede custa menos (SupplySystem.StepCost); onde não há, a tropa vai a pé.
///
/// Desenho nosso: duas linhas e uns riscos atravessados, que é como qualquer carta desenha um caminho de
/// ferro. Um nó só para o mundo inteiro (são milhares de troços) e um _Draw() que o Godot guarda em
/// canvas — refeito só quando alguém assenta carril.</summary>
public partial class RailMarks : Node2D
{
    private readonly record struct Track(Vector2 A, Vector2 B, int Level);
    private readonly List<Track> _tracks = new();

    private static readonly Color Iron = new(0.14f, 0.12f, 0.11f, 0.92f);
    private static readonly Color Sleeper = new(0.72f, 0.68f, 0.6f, 0.85f);

    /// <summary>Troços desenhados e o melhor nível de linha que lá está — a conta da prova headless.</summary>
    public int Count => _tracks.Count;
    public int Best => _tracks.Count == 0 ? 0 : _tracks.Max(t => t.Level);

    /// <summary>Refaz a rede toda. Corre no arranque e sempre que alguém acaba de assentar carril: são
    /// milhares de troços, mas isto acontece uma vez de vinte dias e não todos os dias.</summary>
    public void Build(World w)
    {
        _tracks.Clear();
        int max = (int)w.Rule("rail_draw_max", 6000f);
        foreach (var r in w.Regions.Values)
        {
            if (r.Rail < 1) continue;
            foreach (int n in r.Neighbours)
            {
                if (n < r.Id || !w.Regions.TryGetValue(n, out var o) || o.Rail < 1) continue;   // uma vez por par
                _tracks.Add(new Track(new Vector2(r.CenterX, r.CenterY), new Vector2(o.CenterX, o.CenterY),
                                      Math.Min(r.Rail, o.Rail)));
            }
        }
        // tecto: numa rede mundial inteira o que se corta é a linha pior, nunca a melhor
        if (_tracks.Count > max)
        {
            var best = _tracks.OrderByDescending(t => t.Level).Take(max).ToList();
            _tracks.Clear(); _tracks.AddRange(best);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var t in _tracks)
        {
            var dir = t.B - t.A;
            float len = dir.Length();
            if (len < 1f) continue;
            dir /= len;
            var side = new Vector2(-dir.Y, dir.X);
            float rail = 1.1f + t.Level * 0.5f;                 // uma linha boa desenha-se mais cheia
            DrawLine(t.A, t.B, Iron, rail);
            // travessas: uma a cada tanto, com tecto por troço — de longe são um risco, de perto são a via
            float step = MathF.Max(7f, len / 26f);
            float half = 1.6f + t.Level * 0.6f;
            for (float d = step / 2f; d < len; d += step)
            {
                var at = t.A + dir * d;
                DrawLine(at - side * half, at + side * half, Sleeper, 1.1f);
            }
        }
    }
}
