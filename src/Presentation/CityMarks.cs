using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>As cidades escritas no mapa: um ponto por praça e o nome ao lado das que cabem na escala. É o
/// que faltava para o mapa ser um mapa e não um quebra-cabeças de fronteiras — no HoI4 lê-se o nome da terra
/// antes de se saber de que província é, e é por nomes que se decide para onde vai a seta.
///
/// Quem escolhe o que cabe é o Cities (Core, a partir da tabela city do Natural Earth); aqui só se desenha.
/// Um nó só para o mundo inteiro, como o chão: são centenas de pontos e cada um não pode ser um nó.
///
/// Ponto e letra ficam do mesmo tamanho no ecrã a qualquer zoom — dividem-se pelo zoom, que é o que a câmara
/// multiplica. Um nome que crescesse com o mapa tapava o país ao fim de duas aproximações.</summary>
public partial class CityMarks : Node2D
{
    private World _w = null!;
    private float _zoom = -1f;
    private List<CityDef> _shown = new();
    private int _named;

    private static readonly Color Halo = new(0.05f, 0.06f, 0.09f, 0.75f);
    private static readonly Color Dot = new(0.96f, 0.97f, 1f, 0.95f);
    private static readonly Color CapitalDot = new(1f, 0.86f, 0.42f, 0.98f);
    private static readonly Color Ink = new(0.98f, 0.98f, 1f, 0.95f);

    public void Setup(World w) => _w = w;

    /// <summary>O zoom mudou: refaz a lista do que cabe. O zoom entra arredondado a degraus de 0,05 — durante
    /// um beliscão o zoom muda a cada fotograma, e refazer a lista do mundo inteiro a esse ritmo era gastar
    /// um telemóvel a decidir o que já estava decidido.</summary>
    public void SetZoom(float zoom)
    {
        float step = MathF.Round(zoom * 20f) / 20f;
        if (Mathf.IsEqualApprox(step, _zoom)) return;
        _zoom = step;
        _shown = Cities.Shown(_w, step);
        _named = _shown.Count(c => Cities.Named(_w, c, step));
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_shown.Count == 0) return;
        float k = 1f / MathF.Max(0.05f, _zoom);          // unidades de mundo que valem um pixel no ecrã
        var font = ThemeDB.FallbackFont;
        int size = Math.Clamp((int)MathF.Round(12f * k / 2f) * 2, 8, 72);   // tamanhos aos pares: menos atlas
        foreach (var c in _shown)
        {
            var at = new Vector2(c.X, c.Y);
            float rad = (c.Capital ? 3.2f : c.Population >= 1_000_000 ? 2.6f : 1.9f) * k;
            DrawCircle(at, rad + 1.4f * k, Halo);        // o aro escuro é o que faz o ponto ler-se em cima de qualquer cor
            DrawCircle(at, rad, c.Capital ? CapitalDot : Dot);
            if (c.Capital) DrawArc(at, rad + 2.6f * k, 0f, Mathf.Tau, 20, CapitalDot, 1.1f * k);
            if (!Cities.Named(_w, c, _zoom)) continue;
            var pen = at + new Vector2(rad + 3f * k, size * 0.36f);
            DrawString(font, pen + new Vector2(k, k), c.Name, HorizontalAlignment.Left, -1, size, Halo);
            DrawString(font, pen, c.Name, HorizontalAlignment.Left, -1, size, Ink);
        }
    }

    /// <summary>--smoke: o que está desenhado agora.</summary>
    public string Report() => $"{_shown.Count} pontos ({_shown.Count(c => c.Capital)} capitais), {_named} com nome";

    public int Drawn => _shown.Count;
    public int Named => _named;
}
