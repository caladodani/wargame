using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Os planos de batalha desenhados no mapa, como no HoI4: uma seta gorda por cada exército com
/// frente atribuída, a apontar do sítio onde as tropas estão para a primeira terra do inimigo.
///
/// Até aqui a ordem dada a um grupo — "3.º Exército, frente contra a Espanha, avançar" — só existia dentro
/// do painel Exércitos, em texto. No mapa não se via nada: nem para onde ia cada exército, nem se ainda
/// estava a preparar-se. Agora a seta diz as duas coisas ao mesmo tempo. A forma dá a missão (cheia a
/// avançar, com o corpo aberto a defender) e o enchimento da seta é a preparação do plano: uma seta vazia
/// é um estado-maior que acabou de receber ordens, uma seta cheia é uma ofensiva pronta a partir.
///
/// A operação anfíbia desenha-se com a mesma seta, do cais para a praia, e pela mesma razão: é uma ordem que
/// leva semanas a amadurecer e o enchimento diz quanto lhe falta. É a seta que o HoI4 põe no mar quando se
/// marca uma invasão — e sem ela a maior ordem do jogo só existia dentro de um painel.
///
/// Só lê o World (BattlePlanSystem faz as contas) e vive dentro do MapView, em coordenadas de mundo, por
/// isso acompanha o pan e o zoom sem contas nossas. As etiquetas é que se encolhem ao contrário do zoom,
/// como os marcadores das regiões.</summary>
public partial class PlanOverlay : Node2D
{
    /// <summary>Uma seta pronta a desenhar, já em coordenadas de mundo.</summary>
    private readonly record struct Arrow(Vector2 From, Vector2 To, float Planning, Color Tint, bool Advance, string Text);

    private const float ShaftWidth = 18f;      // largura do corpo da seta, em unidades de mundo
    private const float HeadWidth = 44f;
    private const float HeadLength = 58f;

    private Game _game = null!;
    private Node2D _tagRoot = null!;
    private readonly List<Arrow> _arrows = new();
    private string _painted = "";
    private float _tagScale = 1f, _thick = 1f;

    public void Setup(Game game)
    {
        _game = game;
        ZIndex = 3;                                   // por cima dos polígonos e dos contornos
        _tagRoot = new Node2D { Name = "PlanTags" };
        AddChild(_tagRoot);
    }

    /// <summary>As etiquetas mantêm-se legíveis ao afastar, como os marcadores do RegionRenderer — e as
    /// setas encolhem ao aproximar. Uma seta desenhada em unidades de mundo cresce com o mapa: ao aproximar
    /// para ver uma frente, a seta do plano tapava as regiões que ela atravessa. Com a grossura dividida
    /// pelo zoom fica do mesmo tamanho no ecrã, e ao afastar não engorda para lá do tamanho de origem, senão
    /// no mapa inteiro eram manchas.</summary>
    public void SetZoom(float zoom)
    {
        _tagScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 0.05f, 6f);
        foreach (var c in _tagRoot.GetChildren()) if (c is Node2D n) n.Scale = Vector2.One * _tagScale;
        float thick = Mathf.Clamp(_tagScale, 0.3f, 1f);
        if (Mathf.IsEqualApprox(thick, _thick)) return;
        _thick = thick;
        QueueRedraw();
    }

    /// <summary>Recalcula as setas (e só redesenha se alguma coisa mudou).</summary>
    public void Refresh()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid) return;
            var plans = w.ArmyGroups.Values
                .Where(g => g.CountryId == pid && BattlePlanSystem.Plans(g) && g.Divisions.Count > 0)
                .OrderBy(g => g.Id).ToList();

            var landings = NavalInvasionSystem.Of(w, pid);

            string key = string.Join(";", plans.Select(g =>
                $"{g.Id}:{(int)g.Stance}:{g.FrontCountryId}:{(int)(g.Planning * 20f)}:{string.Join(",", Regions(w, g).OrderBy(x => x))}"))
                + "|" + string.Join(";", landings.Select(i => $"{i.FromId}>{i.TargetId}:{(int)(i.Prep * 20f)}:{i.DivisionIds.Count}"));
            if (key == _painted) return;
            _painted = key;

            _arrows.Clear();
            Ui.Clear(_tagRoot);
            foreach (var g in plans) Build(w, g);
            foreach (var inv in landings) Build(w, inv);
            QueueRedraw();
        }
        catch (Exception ex) { GD.PushError("PlanOverlay: " + ex); }
    }

    /// <summary>Regiões onde este grupo tem tropa (a origem da seta é o meio delas).</summary>
    private static List<int> Regions(World w, ArmyGroup g) =>
        g.Divisions.Where(id => w.Divisions.ContainsKey(id)).Select(id => w.Divisions[id].RegionId).Distinct().ToList();

    private void Build(World w, ArmyGroup g)
    {
        var here = Regions(w, g).Where(w.Regions.ContainsKey).ToList();
        if (here.Count == 0 || g.FrontCountryId is not int foe) return;

        var from = Vector2.Zero;
        foreach (int id in here) from += Center(w.Regions[id]);
        from /= here.Count;

        // alvo: a terra do inimigo mais perto do centro de gravidade do exército — é para lá que aponta
        Vector2? to = null; float best = float.MaxValue;
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId != foe) continue;
            var p = Center(r);
            float d = p.DistanceSquaredTo(from);
            if (d < best) { best = d; to = p; }
        }
        if (to is not Vector2 target || target.DistanceTo(from) < 1f) return;

        var tint = g.Stance == GroupStance.Advance ? Ui.Accent : Ui.Good;
        int days = BattlePlanSystem.DaysToReady(w, g);
        string text = g.Planning >= w.Rule("planning_max", 1f)
            ? $"{g.Name} · plano pronto"
            : $"{g.Name} · plano {g.Planning:P0}" + (days > 0 ? $" (~{days} d)" : "");
        _arrows.Add(new Arrow(from, target, g.Planning, tint, g.Stance == GroupStance.Advance, text));

        var tag = new Node2D { Position = from.Lerp(target, 0.5f), Scale = Vector2.One * _tagScale };
        var lbl = Ui.Lbl(text, 15);
        lbl.AddThemeColorOverride("font_color", tint.Lightened(0.5f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        lbl.AddThemeConstantOverride("outline_size", 5);
        var box = new PanelContainer { Position = new Vector2(-90, -34) };
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.08f, 0.72f), 4));
        box.AddChild(lbl);
        tag.AddChild(box);
        _tagRoot.AddChild(tag);
    }

    /// <summary>A seta da operação anfíbia: do cais onde a tropa espera para a praia que vai assaltar, com o
    /// enchimento a dar a preparação — a mesma leitura da seta de plano, que é o que o HoI4 desenha quando
    /// se marca uma invasão. Sem ela, a maior ordem do jogo só existia dentro de um painel: no mapa não se
    /// via nem de onde partia nem para onde ia, e uma operação esquecida ficava semanas a prender tropa sem
    /// ninguém dar por ela.</summary>
    private void Build(World w, NavalInvasion inv)
    {
        if (!w.Regions.TryGetValue(inv.FromId, out var port) || !w.Regions.TryGetValue(inv.TargetId, out var beach)) return;
        var from = Center(port); var target = Center(beach);
        if (target.DistanceTo(from) < 1f) return;

        float days = NavalInvasionSystem.Days(w, inv.DivisionIds.Count);
        int left = (int)MathF.Ceiling((1f - inv.Prep) * days);
        string text = inv.Prep >= 1f
            ? $"{inv.Name} · {NavalInvasionSystem.Hold(w, inv) ?? "larga hoje"}"
            : $"{inv.Name} · preparação {inv.Prep:P0}" + (left > 0 ? $" (~{left} d)" : "");
        _arrows.Add(new Arrow(from, target, inv.Prep, Ui.Danger, true, text));

        var tag = new Node2D { Position = from.Lerp(target, 0.5f), Scale = Vector2.One * _tagScale };
        var lbl = Ui.Lbl(text, 15);
        lbl.AddThemeColorOverride("font_color", Ui.Danger.Lightened(0.5f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        lbl.AddThemeConstantOverride("outline_size", 5);
        var box = new PanelContainer { Position = new Vector2(-90, -34) };
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.08f, 0.72f), 4));
        box.AddChild(lbl);
        tag.AddChild(box);
        _tagRoot.AddChild(tag);
    }

    private static Vector2 Center(Region r) => new(r.CenterX, r.CenterY);

    public override void _Draw()
    {
        foreach (var a in _arrows) DrawArrow(a);
    }

    /// <summary>Uma seta: corpo rectangular até ao início da ponta, ponta triangular, e por dentro o mesmo
    /// desenho encolhido à fracção que o plano já tem preparada. A defender, o corpo fica vazado — é uma
    /// linha a segurar, não uma investida.</summary>
    private void DrawArrow(Arrow a)
    {
        var dir = (a.To - a.From).Normalized();
        float len = a.From.DistanceTo(a.To);
        float head = MathF.Min(HeadLength * _thick, len * 0.45f);
        var body = Body(a.From, dir, len, head, _thick);
        var tip = Head(a.From + dir * (len - head), dir, head, _thick);

        var faint = a.Tint with { A = a.Advance ? 0.28f : 0.18f };
        DrawColoredPolygon(body, faint);
        DrawColoredPolygon(tip, a.Tint with { A = 0.35f });

        // enchimento: quanto do caminho já está estudado
        float ready = Mathf.Clamp(a.Planning, 0f, 1f);
        if (ready > 0.01f)
        {
            float fill = (len - head) * ready;
            DrawColoredPolygon(Body(a.From, dir, fill + head, head, _thick), a.Tint with { A = 0.7f });
        }

        var edge = a.Tint.Lightened(0.35f) with { A = 0.9f };
        DrawPolyline(body.Append(body[0]).ToArray(), edge, 3f * _thick);
        DrawPolyline(tip.Append(tip[0]).ToArray(), edge, 3f * _thick);
    }

    private static Vector2[] Body(Vector2 from, Vector2 dir, float len, float head, float thick)
    {
        var side = new Vector2(-dir.Y, dir.X) * (ShaftWidth * thick / 2f);
        var end = from + dir * MathF.Max(0f, len - head);
        return new[] { from + side, end + side, end - side, from - side };
    }

    private static Vector2[] Head(Vector2 at, Vector2 dir, float head, float thick)
    {
        var side = new Vector2(-dir.Y, dir.X) * (HeadWidth * thick / 2f);
        return new[] { at + side, at + dir * head, at - side };
    }

    /// <summary>--smoke: quantas setas de plano ficaram desenhadas no mapa.</summary>
    public int Smoke() { Refresh(); return _arrows.Count; }
}
