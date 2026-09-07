using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>As escolas de guerra, desenhadas em árvore como as doutrinas do HoI4: os ramos lado a lado —
/// Guerra de Movimento, Superioridade de Fogo, Assalto em Massa — cada um com os seus degraus ligados por
/// traços, e a experiência em cima a dizer o que se pode pagar.
///
/// Agora com as três armas em abas de metal, como o HoI4 as tem: Exército, Ar e Mar. Cada aba é uma árvore
/// sua, com a sua experiência (que se ganha a combater, a voar e a navegar) e a sua escolha — escolher a
/// escola de caça não fecha escola nenhuma de terra. Um país pode ser da Guerra de Movimento, do
/// Bombardeamento e do Corso ao mesmo tempo; o que não pode é ser de duas escolas da mesma arma.
///
/// O ponto do desenho é mostrar a escolha antes de ela ser feita: enquanto não se adopta nada, os ramos estão
/// todos acesos; ao pagar o primeiro degrau os outros apagam-se de vez, e vê-se logo porquê. Um painel de
/// lista não dizia isto — dizia apenas que havia doutrinas.
///
/// À direita das três escolas comuns vem a coluna que só este país tem: a escola nacional
/// (army_doctrine_branch.country_tag), emoldurada em latão, com a bandeira no cabeçalho e o selo "⚜ TAG" —
/// a maneira própria de fazer a guerra, que mais nenhum exército pode aprender. Cada cabeçalho leva agora a
/// fila de lâmpadas com os degraus já aprendidos, para se ver a altura da escola de relance.
///
/// Só lê o World e despacha AdoptDoctrineCommand; quem manda nas regras é o Core (World.DoctrineBlock).
/// A camada dos traços é a mesma da árvore de focos (FocusLinks).</summary>
public partial class DoctrinePanel : PanelContainer
{
    private const float NodeW = 250f, NodeH = 96f, GapX = 34f, GapY = 40f, HeadH = 44f;

    private Game _game = null!;
    private Label _title = null!, _sub = null!;
    private MetalTabs _tabs = null!;
    private string _domain = World.Land;
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
        _tabs = new MetalTabs();
        _tabs.Set(TabNames, 0, Pick);
        v.AddChild(_tabs);
        v.AddChild(Ui.Rule());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        v.AddChild(scroll);
        _canvas = new Control();
        scroll.AddChild(_canvas);
        _links = new FocusLinks { MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_links);
    }

    /// <summary>As três abas, pela ordem do World.Domains (as mesmas do estado-maior).</summary>
    private static readonly string[] TabNames = Ui.Arms;

    /// <summary>Troca de arma: outra árvore, outra experiência, outra escolha.</summary>
    private void Pick(int i)
    {
        _domain = World.Domains[Math.Clamp(i, 0, World.Domains.Length - 1)];
        _tabs.Set(TabNames, i, Pick);
        _lastKey = ""; Fill();
    }

    /// <summary>Abre a árvore de doutrinas deste país (por omissão, a do jogador). Com uma arma
    /// (World.Land/Air/Sea) abre logo na aba dela — é por aqui que entram as medalhas da barra de cima.</summary>
    public void Open(int countryId, string? domain = null)
    {
        _countryId = countryId; _lastKey = "";
        if (domain is not null && Array.IndexOf(World.Domains, domain) is int tab && tab >= 0)
        {
            _domain = domain;
            _tabs.Set(TabNames, tab, Pick);
        }
        _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); });
    }

    public void Close() => Visible = false;

    public void Refresh() { if (Visible) Fill(); }

    private void Fill()
    {
        var w = _game.World;
        if (!w.Countries.TryGetValue(_countryId, out var c) || w.ArmyDoctrines.Count == 0) return;

        // a experiência entra na chave em passos de 1: o painel tem de acender o botão no dia em que dá
        float xp = World.Xp(c, _domain);
        string key = $"{c.Id}|{_domain}|{(int)xp}|{string.Join(",", c.Doctrines.OrderBy(x => x))}";
        if (key == _lastKey) return;
        _lastKey = key;

        var branch = w.DoctrineBranchOf(c, _domain);
        _title.Text = $"{TabNames[Array.IndexOf(World.Domains, _domain)].Split(' ')[0]} Escolas de guerra de {c.Name}";
        int open = w.Branches(c, _domain).Count;
        int mine = c.Doctrines.Count(id => w.ArmyDoctrines.TryGetValue(id, out var d) && w.DomainOf(d) == _domain);
        _sub.Text = $"{World.XpName(_domain)}: {xp:0} (tecto {w.Rule(XpMaxRule(_domain), 600f):0})   ·   "
                  + (branch is null
                        ? $"{open} escolas abertas — o primeiro degrau fecha as outras desta arma"
                        : $"ramo: {(w.DoctrineBranches[branch].CountryTag == c.Tag ? "⚜ " : "")}{w.DoctrineBranches[branch].Name}"
                          + $" ({mine} degrau{(mine == 1 ? "" : "s")})");

        foreach (var child in _canvas.GetChildren()) if (child != _links) child.QueueFree();
        var lines = new List<(Vector2, Vector2, Color, bool)>();

        var branches = w.Branches(c, _domain);
        int cols = 0, rows = 0;
        foreach (var b in branches)
        {
            var steps = w.DoctrineSteps(c, b.Id);
            if (steps.Count == 0) continue;
            int col = cols++;
            float x = col * (NodeW + GapX);

            bool closed = branch is not null && branch != b.Id;
            bool own = b.CountryTag == c.Tag;
            var head = Head(c, b, steps, closed);
            head.Position = new Vector2(x, 0f);
            head.Size = new Vector2(NodeW, own ? HeadH + 14f : HeadH);
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

    /// <summary>Cabeçalho do ramo: o nome com o ícone, apagado se a escolha já fechou este caminho, e a fila
    /// de lâmpadas com os degraus aprendidos. A escola nacional entra de outra maneira — moldura de latão,
    /// bandeira do país e o selo "⚜ TAG" — porque não é uma escolha entre iguais: é a de casa.</summary>
    private PanelContainer Head(Country c, DoctrineBranch b, List<ArmyDoctrine> steps, bool closed)
    {
        bool own = b.CountryTag == c.Tag;
        var card = new PanelContainer();
        var plate = Ui.Box(closed ? Ui.Ink : own ? new Color(0.15f, 0.14f, 0.11f, 0.96f) : Ui.SurfaceHi, 6);
        if (own && !closed)
        {
            plate.SetBorderWidthAll(2);
            plate.BorderColor = Ui.Accent with { A = 0.8f };
        }
        card.AddThemeStyleboxOverride("panel", plate);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 0); card.AddChild(v);

        if (own)
        {
            var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 6);
            line.AddChild(Ui.Grow(Ui.Crest(c.Tag, $"{b.Icon} {b.Name}", $"escola de {c.Name}", 16)));
            line.AddChild(Seal(c));
            v.AddChild(line);
        }
        else
        {
            var name = Ui.Lbl($"{b.Icon} {b.Name}", 17);
            if (closed) name.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(name);
        }

        int done = steps.Count(d => c.Doctrines.Contains(d.Id));
        var foot = new HBoxContainer(); foot.AddThemeConstantOverride("separation", 8);
        foot.AddChild(Ui.Grow(Dim(closed ? "escola fechada"
                                 : own ? $"{steps.Count} degraus, só de {c.Tag}"
                                       : $"{steps.Count} degraus")));
        if (!closed) foot.AddChild(Ui.Pips(done, steps.Count, own ? Ui.Accent : null));
        v.AddChild(foot);
        return card;
    }

    /// <summary>O mesmo selo que o gabinete e as leis põem no que é só deste país, para as três coisas se
    /// lerem como a mesma ideia.</summary>
    private static PanelContainer Seal(Country c)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 4));
        var l = Ui.Lbl($"⚜ {c.Tag}", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"escola de guerra de {c.Name}: nenhum outro exército a aprende";
        chip.AddChild(l);
        return chip;
    }

    /// <summary>Um degrau: verde se já se sabe, aço com botão se dá para pagar hoje, apagado com o motivo se
    /// não dá. O que a escola vale vem dos efeitos da base de dados, escrito em claro.</summary>
    private PanelContainer Node(World w, Country c, ArmyDoctrine d)
    {
        bool known = c.Doctrines.Contains(d.Id);
        string? block = w.DoctrineBlock(c, d.Id);
        bool open = block is null;
        float xp = World.Xp(c, w.DomainOf(d));
        bool rich = open && xp >= d.Cost;
        var bg = known ? Ui.Good.Darkened(0.55f) : rich ? Ui.SurfaceHi : Ui.Ink;

        var card = new PanelContainer();
        var plate = Ui.Box(bg, 6);
        if (d.CountryTag == c.Tag)                             // degrau da escola de casa: moldura de latão
        {
            plate.SetBorderWidthAll(2);
            plate.BorderColor = Ui.Accent with { A = known || rich ? 0.75f : 0.35f };
        }
        card.AddThemeStyleboxOverride("panel", plate);
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
            else v.AddChild(Dim($"faltam {d.Cost - xp:0} de {World.XpName(w.DomainOf(d))}"));
        }
        else v.AddChild(Dim($"{d.Cost:0} de {World.XpName(w.DomainOf(d))}"));

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
        "air_losses" => "aviões perdidos",
        "air_bombing" => "bombardeamento",
        "air_upkeep" => "custo de esquadrão",
        "naval_losses" => "navios perdidos",
        "naval_upkeep" => "custo de esquadra",
        "naval_blockade" => "bloqueio",
        "naval_escort" => "escolta",
        "naval_patrol" => "patrulha",
        _ => key,
    };

    /// <summary>Cartões desenhados agora: os filhos da tela menos a camada dos traços e menos os que já
    /// estão marcados para morrer — o QueueFree só se cumpre no fim do frame, e o --smoke não tem frames.</summary>
    private int Drawn() => _canvas.GetChildren().Count(n => n != _links && !n.IsQueuedForDeletion());

    /// <summary>A regra do tecto da experiência desta arma.</summary>
    private static string XpMaxRule(string domain) => domain switch
    {
        World.Air => "air_xp_max",
        World.Sea => "navy_xp_max",
        _ => "army_xp_max",
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

    /// <summary>--smoke: abre a árvore do jogador, diz quantos cartões desenhou (cabeçalhos + degraus) e
    /// dá conta da coluna de casa — a escola nacional, quantos degraus tem e o que já se aprendeu dela.</summary>
    public string Smoke()
    {
        if (_game.PlayerId is not int pid) return "sem país";
        var w = _game.World;
        var c = w.Countries[pid];
        Open(pid); Refresh();
        int nodes = Drawn();
        Close();

        var own = w.Branches(c).FirstOrDefault(b => b.CountryTag == c.Tag);
        string mine = own is null
            ? "sem escola de casa"
            : $"⚜ {own.Name} com {w.DoctrineSteps(c, own.Id).Count} degraus "
              + $"({w.DoctrineSteps(c, own.Id).Count(d => c.Doctrines.Contains(d.Id))} aprendido"
              + $"{(w.DoctrineSteps(c, own.Id).Count(d => c.Doctrines.Contains(d.Id)) == 1 ? "" : "s")})";

        // as três armas: passa-se por cada aba, para nenhuma árvore ficar por desenhar, e diz-se o que
        // cada uma tem de escolas, de escolha feita e de experiência no bolso
        var arms = new List<string>();
        for (int i = 0; i < World.Domains.Length; i++)
        {
            Pick(i);
            Open(pid); Refresh();
            int cards = Drawn();
            Close();
            string dom = World.Domains[i];
            var home = w.Branches(c, dom).FirstOrDefault(b => b.CountryTag == c.Tag);
            arms.Add($"{TabNames[i]}: {cards} cartões em {w.Branches(c, dom).Count} escolas, "
                   + $"de casa {(home is null ? "nenhuma" : "⚜ " + home.Name)}, "
                   + $"{(w.DoctrineBranchOf(c, dom) is string b ? w.DoctrineBranches[b].Name : "sem ramo")}, "
                   + $"{World.Xp(c, dom):0} xp");
        }
        Pick(0);

        return $"{nodes} cartões em {w.Branches(c).Count} escolas, {mine}, ramo "
             + $"{(w.DoctrineBranchOf(c) is string br ? w.DoctrineBranches[br].Name : "por escolher")}, "
             + $"{c.ArmyXp:0} de experiência; abas — {string.Join(" | ", arms)}";
    }
}
