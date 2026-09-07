using Godot;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A rota das divisões escolhidas desenhada no mapa, como no HoI4: escolhe-se uma tropa que vai a
/// caminho e vê-se por onde vai — a linha sai de onde ela está, passa pelas terras do caminho e acaba numa
/// seta em cima do destino.
///
/// O jogo sempre soube para onde cada divisão ia (Division.Path guarda os saltos que faltam), mas isso só
/// existia dentro do painel da região, em texto: no mapa uma coluna a marchar era igual a uma coluna parada,
/// e mandar três divisões para sítios diferentes era mandar às cegas. Agora a ordem vê-se no sítio onde foi
/// dada. O troço já andado do primeiro salto fica esbatido e a ponta cheia marca onde vai a coluna neste
/// momento; as paragens do meio levam um losango; a etiqueta diz o destino e os dias que faltam, contados
/// pelo mesmo cálculo que faz o mundo andar (MovementSystem.HopDays). Uma travessia por mar desenha-se
/// tracejada, que aquilo não é uma estrada. Uma divisão presa numa batalha diz que está presa, em vez de
/// mentir com uma marcha que não avança.
///
/// A escolha vem de dois sítios — as divisões marcadas no painel da região e as regiões marcadas por toque
/// longo — e nenhum deles avisa ninguém quando muda. Por isso isto pergunta sozinho, umas vezes por segundo
/// e sempre com o mundo parado (Game.RunWhenIdle), e só redesenha quando a resposta é outra.</summary>
public partial class RouteOverlay : Node2D
{
    /// <summary>Uma rota já em coordenadas de mundo, pronta a desenhar.</summary>
    private readonly record struct Lane(Vector2[] Points, Vector2 Head, bool BySea, bool Fighting, string Text);

    private const float Width = 6f;          // grossura da linha, em unidades de mundo
    private const float HeadWidth = 46f;     // ponta da seta no destino
    private const float HeadLength = 62f;
    private const float Stop = 9f;           // losango das paragens do meio
    private const float Ask = 0.15f;         // de quanto em quanto tempo se volta a perguntar quem está escolhido

    private Game _game = null!;
    private RegionPanel _region = null!;
    private ArmySelect _select = null!;
    private Node2D _tagRoot = null!;
    private readonly List<Lane> _lanes = new();
    private string _painted = "";
    private float _tagScale = 1f, _since;

    public void Setup(Game game)
    {
        _game = game;
        ZIndex = 4;                                   // por cima das setas dos planos: é a ordem de agora
        _tagRoot = new Node2D { Name = "RouteTags" };
        AddChild(_tagRoot);
        SetProcess(true);
    }

    /// <summary>O Hud liga a selecção depois de a criar (os overlays nascem primeiro, no MapView).</summary>
    public void Watch(RegionPanel region, ArmySelect select) { _region = region; _select = select; }

    /// <summary>As etiquetas mantêm-se legíveis ao afastar, como as dos planos.</summary>
    public void SetZoom(float zoom)
    {
        _tagScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 0.05f, 6f);
        foreach (var c in _tagRoot.GetChildren()) if (c is Node2D n) n.Scale = Vector2.One * _tagScale;
    }

    /// <summary>Ninguém avisa quando a selecção muda — pergunta-se. Com o mundo parado, que o tick corre
    /// noutra linha de execução e o World não se lê a meio.</summary>
    public override void _Process(double delta)
    {
        _since += (float)delta;
        if (_since < Ask) return;
        _since = 0f;
        if (_game is not null) _game.RunWhenIdle(Refresh);
    }

    /// <summary>Divisões escolhidas: as marcadas no painel da região aberta (ou todas as minhas que lá estão,
    /// se o painel está aberto sem nenhuma marcada) mais as das regiões marcadas por toque longo.</summary>
    private List<int> Chosen()
    {
        var ids = new List<int>();
        if (_game.PlayerId is not int pid) return ids;
        var w = _game.World;

        void FromRegion(int rid)
        {
            if (!w.Regions.TryGetValue(rid, out var r)) return;
            foreach (int id in r.DivisionIds)
                if (w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid) ids.Add(id);
        }

        if (_region is not null && _region.Visible)
        {
            if (_region.SelectedDivisions.Count > 0) ids.AddRange(_region.SelectedDivisions);
            else FromRegion(_region.OpenRegionId);
        }
        if (_select is not null) foreach (int rid in _select.RegionIds) FromRegion(rid);
        return ids;
    }

    /// <summary>Recalcula as rotas (e só redesenha quando alguma mudou).</summary>
    public void Refresh()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is null) return;
            var routes = RouteLine.For(w, Chosen());

            string key = string.Join(";", routes.Select(r =>
                $"{r.DivisionId}>{string.Join(",", r.Stops.Select(s => s.RegionId))}:{(int)(r.Progress * 20f)}:{r.Days}:{(r.Fighting ? 1 : 0)}"));
            if (key == _painted) return;

            _lanes.Clear();
            Ui.Clear(_tagRoot);
            foreach (var r in routes) Build(w, r);
            _painted = key;
            QueueRedraw();
        }
        catch (Exception ex) { _painted = ""; GD.PushError("RouteOverlay: " + ex); }
    }

    private void Build(WarGame.Core.Model.World w, DivisionRoute r)
    {
        var pts = r.Stops.Select(s => new Vector2(s.X, s.Y)).ToArray();
        if (pts.Length < 2) return;
        var head = new Vector2(r.Head.X, r.Head.Y);
        string dest = w.Regions.TryGetValue(r.To.RegionId, out var to) ? to.Name : "R" + r.To.RegionId;
        int hops = r.Stops.Count - 1;
        string text = r.Fighting
            ? $"{r.Name} · presa em combate → {dest}"
            : $"{r.Name} → {dest} · {r.Days} d" + (hops > 1 ? $" ({hops} saltos)" : "");
        _lanes.Add(new Lane(pts, head, r.BySea, r.Fighting, text));

        // etiqueta a meio do que falta andar: no destino ficava por cima do contador de unidades da região
        var mid = pts[pts.Length / 2].Lerp(pts[^1], 0.5f);
        var tint = r.Fighting ? Ui.Danger : Ui.Accent;
        var lbl = Ui.Lbl(text, 14);
        lbl.AddThemeColorOverride("font_color", tint.Lightened(0.5f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        lbl.AddThemeConstantOverride("outline_size", 5);
        var box = new PanelContainer { Position = new Vector2(-80, -30) };
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.08f, 0.72f), 4));
        box.AddChild(lbl);
        var tag = new Node2D { Position = mid, Scale = Vector2.One * _tagScale };
        tag.AddChild(box);
        _tagRoot.AddChild(tag);
    }

    public override void _Draw()
    {
        foreach (var l in _lanes) DrawLane(l);
    }

    /// <summary>Uma rota: rasto escuro por baixo, o troço já andado esbatido, o que falta cheio, um losango em
    /// cada paragem do meio, a ponta da coluna marcada e uma seta no destino. Por mar vai tracejada.</summary>
    private void DrawLane(Lane l)
    {
        var tint = l.Fighting ? Ui.Danger : Ui.Accent;
        DrawPolyline(l.Points, Ui.Ink with { A = 0.5f }, Width + 4f);

        // primeiro salto: o que já foi andado fica pálido, o resto vai cheio como os outros
        DrawSeg(l.Points[0], l.Head, tint with { A = 0.25f }, l.BySea);
        DrawSeg(l.Head, l.Points[1], tint, l.BySea);
        for (int i = 1; i + 1 < l.Points.Length; i++) DrawSeg(l.Points[i], l.Points[i + 1], tint, l.BySea);

        for (int i = 1; i + 1 < l.Points.Length; i++)
        {
            var c = l.Points[i];
            DrawColoredPolygon(new[] { c + Vector2.Up * Stop, c + Vector2.Right * Stop, c + Vector2.Down * Stop, c + Vector2.Left * Stop },
                               tint with { A = 0.85f });
        }

        // onde vai a coluna neste momento
        DrawCircle(l.Head, Width * 1.4f, tint.Lightened(0.4f));

        var last = l.Points[^2]; var end = l.Points[^1];
        var dir = (end - last).Normalized();
        if (dir.LengthSquared() < 0.5f) return;
        float len = MathF.Min(HeadLength, last.DistanceTo(end) * 0.6f);
        var at = end - dir * len;
        var side = new Vector2(-dir.Y, dir.X) * (HeadWidth / 2f);
        DrawColoredPolygon(new[] { at + side, end, at - side }, tint with { A = 0.9f });
    }

    /// <summary>Um troço de rota: cheio por terra, aos tracinhos por mar.</summary>
    private void DrawSeg(Vector2 a, Vector2 b, Color tint, bool sea)
    {
        if (a.DistanceSquaredTo(b) < 1f) return;
        if (!sea) { DrawLine(a, b, tint, Width); return; }
        float len = a.DistanceTo(b), step = 26f;
        for (float at = 0; at < len; at += step)
            DrawLine(a.Lerp(b, at / len), a.Lerp(b, MathF.Min(1f, (at + step * 0.55f) / len)), tint, Width);
    }

    /// <summary>--smoke: quantas rotas ficaram desenhadas e o que diz a primeira.</summary>
    public string Smoke()
    {
        _painted = "";
        Refresh();
        return _lanes.Count == 0 ? "nenhuma" : $"{_lanes.Count} ({_lanes[0].Text})";
    }
}
