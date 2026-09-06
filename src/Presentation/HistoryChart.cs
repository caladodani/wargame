using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Gráfico de linhas da evolução (World.History): uma polilinha por país, cor da tabela
/// country (a mesma do mapa), métrica comutável (divisões/regiões/dinheiro/potência). Redesenha em Refresh.
///
/// Tem mira: um toque (ou arrastar) põe uma vertical no dia mais próximo e uma tabela flutuante com o valor
/// de cada país nesse dia, ordenada de cima para baixo. Um gráfico de oito linhas sem isto é bonito e não se
/// lê — e a nota de potência, que não é contável a olho, era ilegível de todo.</summary>
public partial class HistoryChart : Control
{
    private Game _game = null!;
    private string _metric = "divisions";
    private readonly Dictionary<int, Color> _colors = new();
    private readonly Dictionary<int, string> _tags = new();

    public void Setup(Game game)
    {
        _game = game;
        foreach (var r in game.StaticDb.Query("SELECT id,tag,color FROM country"))
        {
            int id = Convert.ToInt32(r["id"]);
            _colors[id] = new Color((string?)r["color"] ?? "#cccccc");
            _tags[id] = (string?)r["tag"] ?? "?";
        }
        CustomMinimumSize = new Vector2(0, 230);
    }

    public void SetMetric(string metric) { _metric = metric; QueueRedraw(); }
    /// <summary>Só para o --smoke: pousa a mira no último dia amostrado, para o caminho do desenho da
    /// tabela flutuante ser percorrido sem ninguém tocar no ecrã.</summary>
    public int SmokeCursor()
    {
        var hist = _game.World.History;
        if (hist.Count == 0) return 0;
        _cursorDay = hist[^1].Day;
        QueueRedraw();
        return _cursorDay.Value;
    }
    public string Metric => _metric;

    /// <summary>Dia em que a mira está pousada (null = sem mira).</summary>
    private int? _cursorDay;

    private float Value(HistorySample s) => _metric switch
    {
        "regions" => s.Regions,
        "money" => s.Money,
        "power" => s.Power,
        _ => s.Divisions,
    };

    private string Format(float v) => _metric switch
    {
        "power" => $"{v:0.0}",
        "money" => v >= 1000f ? $"{v / 1000f:0.#}k" : $"{v:0}",
        _ => $"{v:0}",
    };

    /// <summary>Mira: o toque escolhe o dia amostrado mais próximo do X do dedo. Fora do gráfico apaga-a,
    /// para o desenho não ficar com uma vertical que já ninguém pediu.</summary>
    public override void _GuiInput(InputEvent @event)
    {
        bool press = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
        bool drag = @event is InputEventMouseMotion m && (m.ButtonMask & MouseButtonMask.Left) != 0;
        if (!press && !drag) return;
        var hist = _game.World.History;
        if (hist.Count < 2) return;
        float x = ((InputEventMouse)@event).Position.X;
        const float padL = 44f, padR = 56f;
        float plotW = Size.X - padL - padR;
        if (plotW < 40f) return;
        int d0 = hist[0].Day, d1 = hist[^1].Day;
        if (d1 <= d0) return;
        float wanted = d0 + (d1 - d0) * Math.Clamp((x - padL) / plotW, 0f, 1f);
        int best = hist[0].Day;
        foreach (var sm in hist) if (MathF.Abs(sm.Day - wanted) < MathF.Abs(best - wanted)) best = sm.Day;
        _cursorDay = _cursorDay == best && press ? null : best;   // segundo toque no mesmo dia limpa a mira
        QueueRedraw();
        AcceptEvent();
    }

    public override void _Draw()
    {
        var hist = _game.World.History;
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0, 0, 0, 0.35f));
        if (hist.Count < 2) { DrawString(ThemeDB.FallbackFont, new Vector2(12, 24), "Ainda sem dados — as amostras chegam com os dias.", fontSize: 15); return; }

        int d0 = hist[0].Day, d1 = hist[^1].Day;
        if (d1 <= d0) return;
        float max = 1f;
        foreach (var s in hist) max = MathF.Max(max, Value(s));

        const float padL = 44f, padR = 56f, padT = 10f, padB = 22f;
        float plotW = size.X - padL - padR, plotH = size.Y - padT - padB;
        if (plotW < 40 || plotH < 40) return;
        Vector2 At(int day, float v) => new(padL + plotW * (day - d0) / (d1 - d0), padT + plotH * (1f - v / max));

        // grelha: 4 linhas horizontais com valor
        var grid = new Color(1, 1, 1, 0.12f);
        for (int i = 0; i <= 4; i++)
        {
            float v = max * i / 4f, y = padT + plotH * (1f - i / 4f);
            DrawLine(new Vector2(padL, y), new Vector2(padL + plotW, y), grid);
            DrawString(ThemeDB.FallbackFont, new Vector2(2, y + 4), Format(v), fontSize: 12);
        }
        DrawString(ThemeDB.FallbackFont, new Vector2(padL, size.Y - 6), $"dia {d0}", fontSize: 12);
        DrawString(ThemeDB.FallbackFont, new Vector2(padL + plotW - 40, size.Y - 6), $"dia {d1}", fontSize: 12);

        // uma polilinha por país; jogador mais grossa; legenda à direita no fim da linha
        var byCountry = new Dictionary<int, List<Vector2>>();
        foreach (var s in hist)
        {
            if (!byCountry.TryGetValue(s.CountryId, out var pts)) byCountry[s.CountryId] = pts = new();
            pts.Add(At(s.Day, Value(s)));
        }
        foreach (var (cid, pts) in byCountry)
        {
            var col = _colors.GetValueOrDefault(cid, Colors.Gray);
            col.A = 1f;
            bool me = _game.PlayerId == cid;
            // a linha do jogador leva mancha por baixo: entre oito cores, a nossa tem de saltar à vista
            if (me && pts.Count >= 2)
            {
                var area = new List<Vector2>(pts) { new(pts[^1].X, padT + plotH), new(pts[0].X, padT + plotH) };
                DrawColoredPolygon(area.ToArray(), new Color(col, 0.16f));
            }
            if (pts.Count >= 2) DrawPolyline(pts.ToArray(), col, me ? 3f : 1.5f, antialiased: true);
            var end = pts[^1];
            DrawString(ThemeDB.FallbackFont, new Vector2(padL + plotW + 4, end.Y + 4), _tags.GetValueOrDefault(cid, "?"), fontSize: 12, modulate: col);
        }

        if (_cursorDay is int day) Crosshair(hist, day, At, padL, padT, plotW, plotH);
    }

    /// <summary>Vertical no dia escolhido, um ponto por país e a tabela flutuante com os valores desse dia,
    /// do maior para o menor. A caixa salta para o lado esquerdo quando a mira anda perto da direita.</summary>
    private void Crosshair(List<HistorySample> hist, int day, Func<int, float, Vector2> at,
                           float padL, float padT, float plotW, float plotH)
    {
        var rows = hist.Where(s => s.Day == day).OrderByDescending(Value).ToList();
        if (rows.Count == 0) return;
        float x = at(day, 0f).X;
        DrawLine(new Vector2(x, padT), new Vector2(x, padT + plotH), new Color(1, 1, 1, 0.45f), 1.5f);
        foreach (var s in rows)
        {
            var col = _colors.GetValueOrDefault(s.CountryId, Colors.Gray); col.A = 1f;
            DrawCircle(at(s.Day, Value(s)), _game.PlayerId == s.CountryId ? 5f : 3.5f, col);
        }

        var font = ThemeDB.FallbackFont;
        int lines = Math.Min(rows.Count, 8);
        float boxW = 132f, boxH = 20f + lines * 16f;
        float bx = x + 10f + boxW > padL + plotW ? x - 10f - boxW : x + 10f;
        float by = Math.Clamp(padT + 6f, padT, padT + plotH - boxH);
        DrawRect(new Rect2(bx, by, boxW, boxH), new Color(0.06f, 0.07f, 0.09f, 0.92f));
        DrawRect(new Rect2(bx, by, boxW, boxH), new Color(1, 1, 1, 0.18f), filled: false);
        DrawString(font, new Vector2(bx + 8f, by + 15f), $"dia {day}", fontSize: 12, modulate: new Color(1, 1, 1, 0.75f));
        for (int i = 0; i < lines; i++)
        {
            var s = rows[i];
            var col = _colors.GetValueOrDefault(s.CountryId, Colors.Gray); col.A = 1f;
            float y = by + 31f + i * 16f;
            DrawString(font, new Vector2(bx + 8f, y), _tags.GetValueOrDefault(s.CountryId, "?"), fontSize: 12, modulate: col);
            DrawString(font, new Vector2(bx + 46f, y), Format(Value(s)), fontSize: 12,
                       modulate: _game.PlayerId == s.CountryId ? Colors.White : new Color(1, 1, 1, 0.8f));
        }
    }
}
