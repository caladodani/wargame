using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Gráfico de linhas da evolução (World.History): uma polilinha por país, cor da tabela
/// country (a mesma do mapa), métrica comutável (divisões/regiões/dinheiro). Redesenha em Refresh.</summary>
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
    public string Metric => _metric;

    private float Value(HistorySample s) => _metric switch
    {
        "regions" => s.Regions,
        "money" => s.Money,
        _ => s.Divisions,
    };

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
            DrawString(ThemeDB.FallbackFont, new Vector2(2, y + 4), v >= 1000 ? $"{v / 1000f:0.#}k" : $"{v:0}", fontSize: 12);
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
            if (pts.Count >= 2) DrawPolyline(pts.ToArray(), col, me ? 3f : 1.5f, antialiased: true);
            var end = pts[^1];
            DrawString(ThemeDB.FallbackFont, new Vector2(padL + plotW + 4, end.Y + 4), _tags.GetValueOrDefault(cid, "?"), fontSize: 12, modulate: col);
        }
    }
}
