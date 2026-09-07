using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Camada de traços da árvore de focos: as linhas que ligam um foco ao que vem depois dele. Vive por
/// baixo dos cartões e desenha-se à mão porque nenhum contentor do Godot faz ligações entre filhos.</summary>
public partial class FocusLinks : Control
{
    private readonly List<(Vector2 A, Vector2 B, Color Tint, bool Dashed)> _lines = new();

    public void Set(List<(Vector2 A, Vector2 B, Color Tint, bool Dashed)> lines)
    {
        _lines.Clear(); _lines.AddRange(lines); QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var (a, b, tint, dashed) in _lines)
        {
            if (dashed)
            {
                // rivalidade: traço a tracejado, que não é caminho nenhum — é uma porta que se fecha
                var dir = (b - a).Normalized(); float len = a.DistanceTo(b);
                for (float t = 0; t < len; t += 14f)
                    DrawLine(a + dir * t, a + dir * MathF.Min(len, t + 7f), tint, 2f);
                continue;
            }
            // cotovelo: desce do pai, atravessa a meio e desce até ao filho
            float mid = (a.Y + b.Y) / 2f;
            DrawLine(a, new Vector2(a.X, mid), tint, 2f);
            DrawLine(new Vector2(a.X, mid), new Vector2(b.X, mid), tint, 2f);
            DrawLine(new Vector2(b.X, mid), b, tint, 2f);
            DrawCircle(b, 3f, tint);
        }
    }
}

/// <summary>Árvore de focos nacionais desenhada como no HoI4: os focos em grelha, ligados por traços, em vez
/// da lista de linhas de texto que o painel do País mostrava. Assim vê-se de relance o que abre o quê, onde a
/// árvore se parte em dois ramos que se excluem (traço vermelho tracejado) e onde volta a juntar-se.
///
/// Cada cartão diz o estado pela cor: feito (verde), em curso (âmbar, com barra e dias que faltam), à mão
/// (aço, com botão) ou fechado (apagado, a dizer o que falta ou quem o fechou). Só lê o World e despacha
/// SelectFocusCommand; as regras de quem pode o quê são do Core (World.FocusBlock).</summary>
public partial class FocusPanel : PanelContainer
{
    private const float NodeW = 210f, NodeH = 78f, GapX = 26f, GapY = 44f;

    private Game _game = null!;
    private Label _title = null!, _sub = null!;
    private Control _canvas = null!;
    private FocusLinks _links = null!;
    private int _countryId;
    private string _lastKey = "";

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.070f, 0.078f, 0.090f, 1f)));

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6); AddChild(v);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        _title = Ui.Lbl("", 22); head.AddChild(Ui.Grow(_title));
        head.AddChild(Ui.Btn("Fechar", Close));
        _sub = Ui.Lbl("", 16); _sub.AddThemeColorOverride("font_color", Ui.TextDim); v.AddChild(_sub);
        v.AddChild(Ui.Rule());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        v.AddChild(scroll);
        _canvas = new Control();
        scroll.AddChild(_canvas);
        _links = new FocusLinks { MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_links);
    }

    /// <summary>Abre a árvore deste país (por omissão, a do jogador).</summary>
    public void Open(int countryId)
    {
        _countryId = countryId; _lastKey = "";
        _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); });
    }

    public void Close() => Visible = false;

    public void Refresh() { if (Visible) Fill(); }

    private void Fill()
    {
        var w = _game.World;
        if (!w.Countries.TryGetValue(_countryId, out var c)) return;
        var mine = w.Focuses.Values.Where(f => f.CountryId == c.Id).OrderBy(f => f.Sort).ThenBy(f => f.Id).ToList();

        string key = $"{c.Id}|{mine.Count}|{c.CurrentFocus}|{(int)c.FocusProgress}|{string.Join(",", c.FocusesDone.OrderBy(x => x))}";
        if (key == _lastKey) return;
        _lastKey = key;

        _title.Text = $"⚑ Focos de {c.Name}";
        _sub.Text = c.CurrentFocus is string cur && w.Focuses.TryGetValue(cur, out var cf)
            ? $"em curso: {cf.Name} — faltam {(int)MathF.Ceiling(cf.Days - c.FocusProgress)} dias"
            : $"{c.FocusesDone.Count(w.Focuses.ContainsKey)} de {mine.Count} concluídos";

        foreach (var child in _canvas.GetChildren()) if (child != _links) child.QueueFree();
        var pos = Layout(w, mine);
        var lines = new List<(Vector2, Vector2, Color, bool)>();

        foreach (var f in mine)
        {
            if (!pos.TryGetValue(f.Id, out var cell)) continue;
            var node = Node(w, c, f);
            node.Position = new Vector2(cell.X * (NodeW + GapX), cell.Y * (NodeH + GapY));
            node.Size = new Vector2(NodeW, NodeH);
            _canvas.AddChild(node);

            foreach (var need in Parents(w, f))
            {
                if (!pos.TryGetValue(need, out var from)) continue;
                var a = new Vector2(from.X * (NodeW + GapX) + NodeW / 2f, from.Y * (NodeH + GapY) + NodeH);
                var b = new Vector2(cell.X * (NodeW + GapX) + NodeW / 2f, cell.Y * (NodeH + GapY));
                lines.Add((a, b, c.FocusesDone.Contains(need) ? Ui.Accent : Ui.Frame, false));
            }
            // rivais: só uma vez por par (o da esquerda desenha)
            if (!w.FocusRivals.TryGetValue(f.Id, out var rivals)) continue;
            foreach (var other in rivals)
            {
                if (!pos.TryGetValue(other, out var to) || to.X < cell.X || (to.X == cell.X && to.Y <= cell.Y)) continue;
                var a = new Vector2(cell.X * (NodeW + GapX) + NodeW, cell.Y * (NodeH + GapY) + NodeH / 2f);
                var b = new Vector2(to.X * (NodeW + GapX), to.Y * (NodeH + GapY) + NodeH / 2f);
                lines.Add((a, b, Ui.Danger, true));
            }
        }

        int cols = pos.Count == 0 ? 1 : pos.Values.Max(p => p.X) + 1;
        int rows = pos.Count == 0 ? 1 : pos.Values.Max(p => p.Y) + 1;
        var size = new Vector2(cols * (NodeW + GapX), rows * (NodeH + GapY));
        _canvas.CustomMinimumSize = size;
        _links.Size = size;
        _links.Set(lines);
    }

    /// <summary>Todos os focos que este exige: o `requires` da tabela mais os de focus_link.</summary>
    private static IEnumerable<string> Parents(World w, Focus f)
    {
        if (f.Requires is string r) yield return r;
        if (w.FocusLinks.TryGetValue(f.Id, out var extra)) foreach (var e in extra) yield return e;
    }

    /// <summary>Coloca cada foco numa célula: a linha é a profundidade na árvore, a coluna vem de uma travessia
    /// em profundidade — o primeiro filho fica debaixo do pai e os ramos seguintes abrem para a direita.</summary>
    private static Dictionary<string, Vector2I> Layout(World w, List<Focus> all)
    {
        var byId = all.ToDictionary(f => f.Id);
        var depth = new Dictionary<string, int>();
        int Depth(string id, int guard)
        {
            if (depth.TryGetValue(id, out int d)) return d;
            if (guard > 32 || !byId.TryGetValue(id, out var f)) return 0;
            int best = 0;
            foreach (var p in Parents(w, f)) if (byId.ContainsKey(p)) best = Math.Max(best, Depth(p, guard + 1) + 1);
            return depth[id] = best;
        }
        foreach (var f in all) Depth(f.Id, 0);

        var kids = all.ToDictionary(f => f.Id, _ => new List<Focus>());
        foreach (var f in all)
            foreach (var p in Parents(w, f))
                if (kids.TryGetValue(p, out var list) && !list.Contains(f)) list.Add(f);

        var cell = new Dictionary<string, Vector2I>();
        int next = 0;
        void Place(Focus f)
        {
            if (cell.ContainsKey(f.Id)) return;
            cell[f.Id] = new Vector2I(next, depth[f.Id]);
            bool first = true;
            foreach (var k in kids[f.Id].OrderBy(x => x.Sort).ThenBy(x => x.Id))
            {
                if (cell.ContainsKey(k.Id)) continue;
                if (!first) next++;                 // ramo novo: abre coluna à direita
                first = false;
                Place(k);
            }
        }
        foreach (var f in all.Where(x => !Parents(w, x).Any(byId.ContainsKey)))
        { Place(f); next++; }
        foreach (var f in all) if (!cell.ContainsKey(f.Id)) { cell[f.Id] = new Vector2I(next++, depth[f.Id]); }
        return cell;
    }

    private PanelContainer Node(World w, Country c, Focus f)
    {
        bool done = c.FocusesDone.Contains(f.Id), current = c.CurrentFocus == f.Id;
        string? block = w.FocusBlock(c, f.Id);
        bool open = block is null;
        var bg = done ? Ui.Good.Darkened(0.55f) : current ? Ui.Accent.Darkened(0.6f) : open ? Ui.SurfaceHi : Ui.Ink;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(bg, 6));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2); card.AddChild(v);

        var name = Ui.Lbl(f.Name, 15);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (!open && !done && !current) name.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(name);

        if (current)
        {
            v.AddChild(Ui.Bar(c.FocusProgress / MathF.Max(1f, f.Days), Ui.Accent, NodeW - 20f));
            v.AddChild(Dim($"faltam {(int)MathF.Ceiling(f.Days - c.FocusProgress)} dias"));
        }
        else if (done) v.AddChild(Dim("concluído"));
        else if (open)
        {
            bool mine = c.Id == _game.PlayerId;
            if (mine)
            {
                string id = f.Id;
                v.AddChild(Ui.Btn($"Escolher ({f.Days} d)", () => Pick(id), NodeW - 20f, Ui.Kind.Primary));
            }
            else v.AddChild(Dim($"{f.Days} dias"));
        }
        else if (block!.StartsWith('!'))
            v.AddChild(Dim($"fechado por {Named(w, block[1..])}"));
        else v.AddChild(Dim($"precisa de {Named(w, block)}"));

        card.TooltipText = f.Description;
        return card;
    }

    private static string Named(World w, string focusId) =>
        w.Focuses.TryGetValue(focusId, out var f) ? f.Name : focusId;

    private static Label Dim(string text)
    {
        var l = Ui.Lbl(text, 13);
        l.AddThemeColorOverride("font_color", Ui.TextDim);
        return l;
    }

    private void Pick(string focusId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        _game.Dispatch(new SelectFocusCommand(pid, focusId));
        _lastKey = ""; Fill();
    });

    /// <summary>--smoke: abre a árvore do jogador e diz quantos cartões desenhou.</summary>
    public int Smoke()
    {
        if (_game.PlayerId is not int pid) return 0;
        Open(pid); Refresh();
        int nodes = _canvas.GetChildCount() - 1;    // menos a camada dos traços
        Close();
        return nodes;
    }
}
