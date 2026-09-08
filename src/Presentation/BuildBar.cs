using Godot;
using WarGame.Core.Commands;

namespace WarGame.Presentation;

/// <summary>Menu "Construir": escolhe-se primeiro o tipo (Arsenal, Fábrica, Laboratório, Porto,
/// Infra-estrutura ou Fortificação) e depois toca-se directamente no mapa, região a região, sem o menu
/// tapar o jogo — fica armado até se escolher outro tipo ou fechar, para se construir em vários estados
/// seguidos sem reabrir o menu. É o Hud quem manda os toques do mapa para cá enquanto está armado, antes
/// de a ArmySelect os ver.
/// Lê o World só via RunWhenIdle e muta só por Game.Dispatch.</summary>
public partial class BuildBar : PanelContainer
{
    private const string Infra = "@infra", Fort = "@fort";

    // Os edifícios trazem o desenho da tabela `building` (coluna glyph); estes dois não são linhas de lá —
    // são regras (infra_build_cost, fort_build_cost) e já tinham o nome escrito aqui. A chapa também.
    private const string InfraGlyph = "estrada", FortGlyph = "escudo";

    private Game _game = null!;
    private VBoxContainer _list = null!;
    private HBoxContainer _stamp = null!;
    private Label _status = null!;
    private string? _armed;
    private string _painted = "";

    public bool Armed => _armed is not null;

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;   // o toque no menu não é toque no mapa por trás
        AnchorLeft = 1; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 0;
        GrowHorizontal = GrowDirection.Begin; GrowVertical = GrowDirection.End;
        OffsetRight = -12;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.09f, 0.03f, 0.94f), 6));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); AddChild(v);
        var head = new HBoxContainer();
        head.AddChild(Ui.Grow(Ui.Head("Construir", 14)));
        head.AddChild(Ui.Btn("Fechar", Close, 44));
        v.AddChild(head);
        var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 6);
        _stamp = new HBoxContainer();                     // a chapa do tipo armado, ao lado da frase
        line.AddChild(_stamp);
        _status = Ui.Lbl("Escolhe o tipo e toca no mapa", 14);
        _status.AddThemeColorOverride("font_color", Ui.TextDim);
        line.AddChild(Ui.Grow(_status));
        v.AddChild(line);
        _list = new VBoxContainer(); _list.AddThemeConstantOverride("separation", 2); v.AddChild(_list);
    }

    /// <summary>A chapa da selecção acompanha a altura da barra de topo — este menu também.</summary>
    public void PlaceUnder(float y) => OffsetTop = y;

    /// <summary>Abre o menu e desenha os tipos (a tabela `building` só existe depois de o mundo carregar,
    /// por isso a lista faz-se aqui e não no Setup).</summary>
    public void Open() => _game.RunWhenIdle(() => { Visible = true; Fill(); });

    public void Close() { Visible = false; _armed = null; }

    /// <summary>Um painel a tapar o ecrã esconde o menu — reabre-se pela chapa "Construir" na barra de topo.</summary>
    public void SetCovered(bool covered) { if (covered) Visible = false; }

    private void Fill()
    {
        var w = _game.World;
        string key = string.Join(",", w.BuildingDefs.Keys.OrderBy(k => k)) + "|" + _armed;
        if (key == _painted) return;
        _painted = key;
        Ui.Clear(_list);
        foreach (var d in w.BuildingDefs.Values.OrderBy(d => d.Id))
            _list.AddChild(Pick(d.Name, d.Id, $"{d.Cost:0}, {d.Days:0} d", d.Glyph));
        _list.AddChild(Pick("Infra-estrutura", Infra, $"{w.Rule("infra_build_cost", 40f):0}, {w.Rule("infra_build_days", 30f):0} d", InfraGlyph));
        _list.AddChild(Pick("Fortificação", Fort, $"{w.Rule("fort_build_cost", 30f):0}, {w.Rule("fort_build_days", 20f):0} d", FortGlyph));
    }

    /// <summary>Uma chapa da lista. O desenho vai à cabeça do nome, como no menu de construção do HoI4: numa
    /// lista de seis obras todas escritas do mesmo tamanho, o que se procura encontra-se pela figura e não
    /// pela leitura. A chapa é desenhada por cima do botão, ancorada à esquerda e ao meio da altura, porque
    /// um Button não arruma filhos — e o texto começa com um recuo do tamanho dela para não lhe ir por cima.</summary>
    private Button Pick(string name, string id, string cost, string glyph)
    {
        var b = Ui.Btn($"        {name} ({cost})", () => Arm(id), 0f,
                       id == _armed ? Ui.Kind.Primary : Ui.Kind.Normal);
        b.Alignment = HorizontalAlignment.Left;
        Glyph.Stamp(b, glyph, id == _armed ? Ui.Ink : Ui.Accent, PlateSize);
        return b;
    }

    private const float PlateSize = 18f;

    private void Arm(string id)
    {
        _armed = id; _painted = "";
        Fill();
        (string name, string glyph) = id == Infra ? ("Infra-estrutura", InfraGlyph)
            : id == Fort ? ("Fortificação", FortGlyph)
            : _game.World.BuildingDefs.TryGetValue(id, out var d) ? (d.Name, d.Glyph) : (id, "roda");
        Ui.Clear(_stamp);
        _stamp.AddChild(Glyph.Make(glyph, 16, Ui.TextDim));
        _status.Text = $"Toca no mapa onde construir — {name}";
    }

    /// <summary>Toque no mapa enquanto armado: uma ordem de construção nessa região; o menu fica armado
    /// para se tocar noutra a seguir.</summary>
    public void HandleTap(int regionId) => _game.RunWhenIdle(() =>
    {
        if (_armed is not string id || _game.PlayerId is not int pid) return;
        var err = id == Infra ? _game.Dispatch(new BuildInfrastructureCommand(pid, regionId))
                 : id == Fort ? _game.Dispatch(new BuildFortCommand(pid, regionId))
                 : _game.Dispatch(new BuildBuildingCommand(pid, regionId, id));
        string name = _game.World.Regions.TryGetValue(regionId, out var r) ? r.Name : "R" + regionId;
        _game.Notify(err ?? $"Obra iniciada em {name}");
    });

    /// <summary>--smoke: abre o menu, arma o primeiro tipo e devolve o que ficou escolhido, com a conta das
    /// chapas. São três números diferentes de propósito, porque é fácil enganar-se com um só: quantos nomes
    /// a base de dados pede em todas as tabelas que têm coluna glyph (obras, ramos, estações, modos de mapa,
    /// missões de ar e mar, géneros da crónica, pastas do gabinete, políticas de ocupação e gravidades de
    /// baixa, mais as duas obras que são regras e não linhas), quantos desses o Glyph sabe
    /// mesmo desenhar, e quantas chapas ficaram desenhadas neste menu — destas, quantas caíram na roda
    /// dentada por o nome não existir. Um nome mal escrito na tabela não dá erro nenhum: dá uma roda
    /// calada, e é isso que este contador faz aparecer.</summary>
    public string Smoke()
    {
        Open();
        var w = _game.World;
        var defs = w.BuildingDefs.Values.OrderBy(d => d.Id).ToList();
        var first = defs.FirstOrDefault();
        if (first is not null) Arm(first.Id);
        string status = _status.Text;

        // As chapas da barra de cima entram na conta pelo mesmo caminho: não são linha de tabela nenhuma,
        // mas são pedidas pelo nome e partiriam caladas na mesma.
        var extra = new[] { InfraGlyph, FortGlyph }.Concat(Hud.BarGlyphs).ToArray();
        var asked = Glyph.Asked(w, extra);
        int known = asked.Count(Glyph.Knows);
        var (drawn, fell) = Glyph.Count(this);
        Close();
        return $"{status} ({drawn} chapas desenhadas no menu, {fell} na roda; "
             + $"{known} de {asked.Count} nomes pedidos pela base de dados têm desenho)";
    }
}
