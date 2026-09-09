using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>As asas e as esquadras desenhadas em cima do mapa, como no HoI4.
///
/// O céu e o mar já tinham tudo — modelos, campos onde dormir, porta-aviões, submarinos que se escondem e
/// caça anti-submarina — e não tinham a única coisa que faz um mapa de guerra parecer um mapa de guerra:
/// estarem lá. Para saber onde andava a nossa aviação abria-se um painel e lia-se uma lista; a esquadra que
/// fechava o nosso cais era uma linha de texto. Agora vê-se: cada missão de ar é um contador pendurado por
/// cima da província, cada esquadra é um contador pousado no mar ao largo da costa, e um mar sem contador
/// nenhum quer dizer o que parece — não há lá nada que nós vejamos.
///
/// Um nó só para o mundo inteiro, como as chapas de praça e as cidades: são dezenas de contadores e cada um
/// não pode ser um nó. Quem decide o que se vê é o WarMarks (Core), com o nevoeiro e o esconderijo dos
/// submarinos já aplicados; aqui só se desenha, com as três cores do mapa de guerra — verde nosso, vermelho
/// de quem está em guerra connosco, cinzento de terceiros.</summary>
public partial class WarMapMarks : Node2D
{
    /// <summary>Um contador já colocado: onde ele fica no mundo e de onde ele pende.</summary>
    private readonly record struct Plate(WarMarks.Mark Mark, Vector2 At, Vector2 Anchor);

    private static readonly Color Ours = new(0.42f, 0.86f, 0.46f);
    private static readonly Color Theirs = new(0.93f, 0.32f, 0.29f);
    private static readonly Color Neutral = new(0.82f, 0.84f, 0.88f);
    private static readonly Color Ink = new(0.05f, 0.06f, 0.09f, 0.94f);
    private static readonly Color Paper = new(0.98f, 0.98f, 1f, 0.96f);

    private static Color Of(int side) => side > 0 ? Ours : side < 0 ? Theirs : Neutral;

    private Game _game = null!;
    private List<Plate> _plates = new();
    private float _zoom = 1f, _cut = 0.12f;
    private int _max = 200;

    public void Setup(Game game)
    {
        _game = game;
        ZIndex = 3;            // por cima das rotas de comboio, por baixo das setas dos planos
    }

    public void SetZoom(float zoom) { _zoom = zoom; QueueRedraw(); }

    /// <summary>Refaz a lista dos contadores. Só é chamado com o mundo parado.</summary>
    public void Refresh()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid) { _plates = new List<Plate>(); QueueRedraw(); return; }
            _cut = w.Rule("war_mark_zoom", 0.12f);
            _max = (int)w.Rule("war_mark_max", 200f);

            var plates = new List<Plate>();
            var used = new Dictionary<int, int>();          // contadores já pousados por região: empilham-se
            foreach (var m in WarMarks.All(w, pid))
            {
                var anchor = new Vector2(m.X, m.Y);
                int floor = used.GetValueOrDefault(m.RegionId);
                used[m.RegionId] = floor + 1;
                plates.Add(new Plate(m, anchor + Offset(w, m, floor), anchor));
            }
            _plates = plates;
            QueueRedraw();
        }
        catch (Exception ex) { GD.PushError("WarMapMarks: " + ex); }
    }

    /// <summary>Onde é que o contador pousa em relação à província. A asa fica por cima da terra (é onde ela
    /// está); a esquadra sai para o largo, na direcção do mar que aquela costa tem à frente — um contador de
    /// esquadra em cima de terra firme era o desenho a mentir. Missões a mais na mesma província empilham-se
    /// em vez de se taparem.</summary>
    private static Vector2 Offset(World w, WarMarks.Mark m, int floor)
    {
        float step = 20f * floor;
        if (!m.Sea) return new Vector2(0f, -26f - step);
        if (!w.Regions.TryGetValue(m.RegionId, out var r) || r.SeaNeighbours.Count == 0)
            return new Vector2(0f, 26f + step);

        var here = new Vector2(r.CenterX, r.CenterY);
        var sum = Vector2.Zero;
        int n = 0;
        foreach (var (id, _) in r.SeaNeighbours.OrderBy(kv => kv.Key))
            if (w.Regions.TryGetValue(id, out var sea))
            {
                var d = new Vector2(sea.CenterX, sea.CenterY) - here;
                if (d.LengthSquared() > 1f) { sum += d.Normalized(); n++; }
            }
        var dir = n == 0 || sum.LengthSquared() < 0.0001f ? Vector2.Down : sum.Normalized();
        return dir * (34f + step);
    }

    public override void _Draw()
    {
        if (_zoom < _cut || _plates.Count == 0) return;
        float k = 1f / MathF.Max(0.05f, _zoom);          // unidades de mundo que valem um pixel no ecrã
        var font = ThemeDB.FallbackFont;
        int budget = Math.Max(1, _max);
        foreach (var p in _plates)
        {
            if (budget-- <= 0) break;
            Counter(p, k, font);
        }
    }

    /// <summary>Um contador: o fio que o prende à província, a caixa escura com a faixa da relação em cima,
    /// a chapa do modelo (ou do casco) à esquerda e a conta à direita. O que anda escondido por baixo de
    /// água sai à parte, ao lado, com a chapa do submarino apagada — é nosso e mais ninguém o vê.</summary>
    private void Counter(Plate p, float k, Font font)
    {
        var face = Of(p.Mark.Side);
        var at = p.Anchor + (p.At - p.Anchor) * k;        // o desvio conta-se em pixéis, não em léguas
        float w = 30f * k, h = 15f * k;
        var box = new Rect2(at - new Vector2(w / 2f, h / 2f), new Vector2(w, h));

        // o fio até à província: sabe-se de onde é que aquilo está a voar (ou onde é aquele mar)
        DrawLine(p.Anchor, at, Ink, 2.6f * k);
        DrawLine(p.Anchor, at, face with { A = 0.55f }, 1.2f * k);
        DrawCircle(p.Anchor, 1.9f * k, face);

        DrawRect(box.Grow(1.4f * k), Ink);
        DrawRect(box, new Color(0.10f, 0.11f, 0.14f, 0.94f));
        DrawRect(new Rect2(box.Position + new Vector2(1f * k, 1f * k), new Vector2(w - 2f * k, 2.6f * k)), face);
        DrawRect(box, face with { A = 0.85f }, false, 1.2f * k);

        // a chapa do que ali está: o modelo com mais asas, o casco mais numeroso do que se vê
        var plate = new Rect2(box.Position + new Vector2(2.2f * k, 4.6f * k), new Vector2(9.5f * k, 9.5f * k));
        Glyph.Draw(this, plate, p.Mark.Glyph, face.Lightened(0.2f), MathF.Max(1f, 1.3f * k));

        // e a tarefa, pequena, colada ao canto: superioridade, bombardeamento, bloqueio, caça
        var task = new Rect2(box.Position + new Vector2(w - 8f * k, h - 8f * k), new Vector2(6.4f * k, 6.4f * k));
        Glyph.Draw(this, task, p.Mark.MissionGlyph, Paper with { A = 0.75f }, MathF.Max(1f, 1.1f * k));

        int size = Math.Clamp((int)(11f * k), 7, 44);
        DrawString(font, box.Position + new Vector2(12f * k, h * 0.5f + size * 0.34f),
                   $"{p.Mark.Count:0.#}", HorizontalAlignment.Left, w - 18f * k, size, Paper);

        if (p.Mark.Hidden <= 0.05f) return;
        // o que vai por baixo: chapa apagada e a conta em cinza, encostada ao contador
        var deep = new Rect2(box.End.X + 2.5f * k, box.Position.Y + 3.5f * k, 8f * k, 8f * k);
        Glyph.Draw(this, deep, "submarino", face with { A = 0.45f }, MathF.Max(1f, 1.1f * k));
        DrawString(font, new Vector2(deep.End.X + 1.5f * k, deep.End.Y - 0.5f * k),
                   $"{p.Mark.Hidden:0.#}", HorizontalAlignment.Left, -1, Math.Clamp((int)(8f * k), 6, 32),
                   Paper with { A = 0.7f });
    }

    /// <summary>--smoke: o que o mapa está a mostrar da guerra do ar e do mar.</summary>
    public string Report()
    {
        int air = _plates.Count(p => !p.Mark.Sea), sea = _plates.Count - air;
        int shown = _zoom < _cut ? 0 : Math.Min(_plates.Count, _max);
        float theirs = _plates.Where(p => p.Mark.Side < 0).Sum(p => p.Mark.Count);
        return $"{shown} contador{(shown == 1 ? "" : "es")} no mapa ({air} de ar, {sea} de mar, "
             + $"{theirs:0.#} asas e cascos deles à vista; corte a {_cut:0.00} de zoom, tecto de {_max})";
    }

    /// <summary>--smoke: refaz e conta, para a prova headless não depender de um fotograma ter corrido.</summary>
    public string Smoke() { Refresh(); return Report(); }
}
