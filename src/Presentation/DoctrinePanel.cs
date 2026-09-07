using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>As escolas de guerra do exército, desenhadas em árvore como as doutrinas de terra do HoI4: três
/// ramos lado a lado — Guerra de Movimento, Superioridade de Fogo, Assalto em Massa — cada um com os seus
/// degraus ligados por traços, e a experiência de campanha em cima a dizer o que se pode pagar.
///
/// O ponto do desenho é mostrar a escolha antes de ela ser feita: enquanto não se adopta nada, os três ramos
/// estão acesos; ao pagar o primeiro degrau os outros dois apagam-se de vez, e vê-se logo porquê. Um painel
/// de lista não dizia isto — dizia apenas que havia doutrinas.
///
/// Só lê o World e despacha AdoptDoctrineCommand; quem manda nas regras é o Core (World.DoctrineBlock).
/// A camada dos traços é a mesma da árvore de focos (FocusLinks).</summary>
public partial class DoctrinePanel : PanelContainer
{
    private const float NodeW = 250f, NodeH = 96f, GapX = 34f, GapY = 40f, HeadH = 44f;

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
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.12f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.070f, 0.078f, 0.090f, 0.97f)));

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

    /// <summary>Abre a árvore de doutrinas deste país (por omissão, a do jogador).</summary>
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
        if (!w.Countries.TryGetValue(_countryId, out var c) || w.ArmyDoctrines.Count == 0) return;

        // a experiência entra na chave em passos de 1: o painel tem de acender o botão no dia em que dá
        string key = $"{c.Id}|{(int)c.ArmyXp}|{string.Join(",", c.Doctrines.OrderBy(x => x))}";
        if (key == _lastKey) return;
        _lastKey = key;

        var branch = w.DoctrineBranchOf(c);
        _title.Text = $"⚔ Escolas de guerra de {c.Name}";
        _sub.Text = $"experiência de exército: {c.ArmyXp:0} (tecto {w.Rule("army_xp_max", 600f):0})   ·   "
                  + (branch is null
                        ? "ramo por escolher — o primeiro degrau fecha os outros dois"
                        : $"ramo: {w.DoctrineBranches[branch].Name} ({c.Doctrines.Count} degrau{(c.Doctrines.Count == 1 ? "" : "s")})");

        foreach (var child in _canvas.GetChildren()) if (child != _links) child.QueueFree();
        var lines = new List<(Vector2, Vector2, Color, bool)>();

        var branches = w.DoctrineBranches.Values.OrderBy(b => b.Sort).ThenBy(b => b.Id).ToList();
        int cols = 0, rows = 0;
        foreach (var b in branches)
        {
            var steps = w.ArmyDoctrines.Values.Where(d => d.Branch == b.Id)
                .OrderBy(d => d.Sort).ThenBy(d => d.Cost).ThenBy(d => d.Id).ToList();
            if (steps.Count == 0) continue;
            int col = cols++;
            float x = col * (NodeW + GapX);

            bool closed = branch is not null && branch != b.Id;
            var head = Head(b, steps.Count, closed);
            head.Position = new Vector2(x, 0f);
            head.Size = new Vector2(NodeW, HeadH);
            _canvas.AddChild(head);

            for (int i = 0; i < steps.Count; i++)
            {
                var d = steps[i];
                float y = HeadH + GapY / 2f + i * (NodeH + GapY);
                var card = Node(w, c, d);
                card.Position = new Vector2(x, y);
                card.Size = new Vector2(NodeW, NodeH);
                _canvas.AddChild(card);
                if (i > 0)
                {
                    var a = new Vector2(x + NodeW / 2f, y - GapY);
                    var bb = new Vector2(x + NodeW / 2f, y);
                    lines.Add((a, bb, c.Doctrines.Contains(steps[i - 1].Id) ? Ui.Accent : Ui.Frame, false));
                }
                rows = Math.Max(rows, i + 1);
            }
        }

        var size = new Vector2(Math.Max(1, cols) * (NodeW + GapX), HeadH + GapY + rows * (NodeH + GapY));
        _canvas.CustomMinimumSize = size;
        _links.Size = size;
        _links.Set(lines);
    }

    /// <summary>Cabeçalho do ramo: o nome com o ícone, apagado se a escolha já fechou este caminho.</summary>
    private PanelContainer Head(DoctrineBranch b, int steps, bool closed)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(closed ? Ui.Ink : Ui.SurfaceHi, 6));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 0); card.AddChild(v);
        var name = Ui.Lbl($"{b.Icon} {b.Name}", 17);
        if (closed) name.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(name);
        v.AddChild(Dim(closed ? "escola fechada" : $"{steps} degraus"));
        return card;
    }

    /// <summary>Um degrau: verde se já se sabe, aço com botão se dá para pagar hoje, apagado com o motivo se
    /// não dá. O que a escola vale vem dos efeitos da base de dados, escrito em claro.</summary>
    private PanelContainer Node(World w, Country c, ArmyDoctrine d)
    {
        bool known = c.Doctrines.Contains(d.Id);
        string? block = w.DoctrineBlock(c, d.Id);
        bool open = block is null;
        bool rich = open && c.ArmyXp >= d.Cost;
        var bg = known ? Ui.Good.Darkened(0.55f) : rich ? Ui.SurfaceHi : Ui.Ink;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(bg, 6));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2); card.AddChild(v);

        var name = Ui.Lbl(d.Name, 15);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (!known && !rich) name.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(name);
        v.AddChild(Dim(Gains(w, d)));

        if (known) v.AddChild(Dim("aprendida"));
        else if (!open)
            v.AddChild(Dim(block!.StartsWith('!')
                ? $"escola fechada por {Named(w, block[1..])}"
                : $"precisa de {Named(w, block)}"));
        else if (c.Id == _game.PlayerId)
        {
            string id = d.Id;
            if (rich) v.AddChild(Ui.Btn($"Adoptar ({d.Cost:0} xp)", () => Adopt(id), NodeW - 20f, Ui.Kind.Primary));
            else v.AddChild(Dim($"faltam {d.Cost - c.ArmyXp:0} de experiência"));
        }
        else v.AddChild(Dim($"{d.Cost:0} de experiência"));

        card.TooltipText = d.Description;
        return card;
    }

    /// <summary>O que o degrau dá, em texto curto: "ataque +8%, marcha +5%".</summary>
    private static string Gains(World w, ArmyDoctrine d)
    {
        if (!w.DoctrineEffects.TryGetValue(d.Id, out var effects) || effects.Count == 0) return "sem efeito";
        return string.Join(", ", effects.Select(e => $"{StatName(e.Key)} {(e.Mul >= 1f ? "+" : "")}{(e.Mul - 1f) * 100f:0.#}%"));
    }

    /// <summary>Nome em português da estatística que a escola melhora (ou piora: a guerra de movimento paga
    /// velocidade com couraça).</summary>
    private static string StatName(string key) => key switch
    {
        "attack" => "ataque",
        "defense" => "defesa",
        "org_regain" => "recuperação",
        "move_speed" => "marcha",
        "industry" => "indústria",
        "production_speed" => "produção",
        "conscription" => "recrutamento",
        _ => key,
    };

    private static string Named(World w, string doctrineId) =>
        w.ArmyDoctrines.TryGetValue(doctrineId, out var d) ? d.Name : doctrineId;

    private static Label Dim(string text)
    {
        var l = Ui.Lbl(text, 13);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.AddThemeColorOverride("font_color", Ui.TextDim);
        return l;
    }

    private void Adopt(string doctrineId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        _game.Dispatch(new AdoptDoctrineCommand(pid, doctrineId));
        _lastKey = ""; Fill();
    });

    /// <summary>--smoke: abre a árvore do jogador e diz quantos cartões desenhou (cabeçalhos + degraus).</summary>
    public int Smoke()
    {
        if (_game.PlayerId is not int pid) return 0;
        Open(pid); Refresh();
        int nodes = _canvas.GetChildCount() - 1;    // menos a camada dos traços
        Close();
        return nodes;
    }
}
