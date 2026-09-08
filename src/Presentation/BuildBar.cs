using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Menu "Construir": escolhe-se primeiro o tipo (Arsenal, Fábrica, Laboratório, Porto,
/// Infra-estrutura ou Fortificação) e depois toca-se directamente no mapa, região a região, sem o menu
/// tapar o jogo — fica armado até se escolher outro tipo ou fechar, para se construir em vários estados
/// seguidos sem reabrir o menu. É o Hud quem manda os toques do mapa para cá enquanto está armado, antes
/// de a ArmySelect os ver.
/// Lê o World só via RunWhenIdle e muta só por Game.Dispatch.</summary>
public partial class BuildBar : PanelContainer
{
    // As duas obras que não são linhas da tabela `building` (são regras: infra_*, fort_*). Os ids e as
    // chapas vivem agora no BuildPlan, com o resto da lista — o menu não decide o que se pode construir.
    private const string Infra = BuildPlan.Infra, Fort = BuildPlan.Fort, Rail = BuildPlan.Rail;
    private const string InfraGlyph = "estrada", FortGlyph = "escudo", RailGlyph = "carril";

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
        int pid = _game.PlayerId ?? 0;
        var yards = Industry.Of(w, pid);
        float money = w.Countries.TryGetValue(pid, out var me) ? me.Money : 0f;
        // a lista repinta-se quando o que ela diz muda: o cofre e as fábricas livres decidem o que está
        // acinzentado, e uma lista que não os visse mentia até se fechar e abrir o menu
        string key = string.Join(",", w.BuildingDefs.Keys.OrderBy(k => k)) + "|" + _armed
                   + "|" + (int)money + "|" + yards.FreeCivil + "/" + yards.Civil;
        if (key == _painted) return;
        _painted = key;
        Ui.Clear(_list);
        foreach (var o in BuildPlan.Offers(w))
            _list.AddChild(Pick(o, pid));
        // a conta da obra armada, em chapas: o que custa, quanto demora, quantas fábricas civis sobram e
        // até que nível vai. Sem nada armado não se desenha cartão nenhum — o menu é estreito.
        if (_armed is string armed && BuildView.Card(w, pid, null, armed) is PanelContainer card)
            _list.AddChild(card);
    }

    /// <summary>Uma chapa da lista. O desenho vai à cabeça do nome, como no menu de construção do HoI4: numa
    /// lista de seis obras todas escritas do mesmo tamanho, o que se procura encontra-se pela figura e não
    /// pela leitura. A chapa é desenhada por cima do botão, ancorada à esquerda e ao meio da altura, porque
    /// um Button não arruma filhos — e o texto começa com um recuo do tamanho dela para não lhe ir por cima.</summary>
    private Button Pick(BuildOffer o, int pid)
    {
        string travao = BuildPlan.Blocked(_game.World, pid, null, o.Id) ?? "";
        var b = Ui.Btn($"        {o.Name} ({o.Cost:0}, {o.Days:0} d)", () => Arm(o.Id), 0f,
                       o.Id == _armed ? Ui.Kind.Primary : Ui.Kind.Normal);
        b.Alignment = HorizontalAlignment.Left;
        // o que o país não pode pagar (ou não tem fábrica para começar) fica apagado, como no menu de
        // construção do HoI4 — mas continua a carregar-se, para se ver a conta e perceber o que falta
        if (travao.Length > 0 && o.Id != _armed) b.AddThemeColorOverride("font_color", Ui.TextDim);
        b.TooltipText = BuildPlan.Why(_game.World, pid, null, o.Id);
        Glyph.Stamp(b, o.Glyph, o.Id == _armed ? Ui.Ink : Ui.Accent, PlateSize);
        return b;
    }

    private const float PlateSize = 18f;

    private void Arm(string id)
    {
        _armed = id; _painted = "";
        Fill();
        var offer = BuildPlan.Find(_game.World, id);
        (string name, string glyph) = offer is BuildOffer o ? (o.Name, o.Glyph) : (id, "roda");
        Ui.Clear(_stamp);
        _stamp.AddChild(Glyph.Make(glyph, 16, Ui.TextDim));
        // a razão pela qual o toque no mapa vai ser recusado diz-se ANTES do toque, não depois
        string travao = BuildPlan.Blocked(_game.World, _game.PlayerId ?? 0, null, id) ?? "";
        _status.Text = travao.Length > 0 ? $"{name}: {travao}" : $"Toca no mapa onde construir — {name}";
        _status.AddThemeColorOverride("font_color", travao.Length > 0 ? Ui.Danger : Ui.TextDim);
    }

    /// <summary>Toque no mapa enquanto armado: uma ordem de construção nessa região; o menu fica armado
    /// para se tocar noutra a seguir.</summary>
    public void HandleTap(int regionId) => _game.RunWhenIdle(() =>
    {
        if (_armed is not string id || _game.PlayerId is not int pid) return;
        var err = id == Infra ? _game.Dispatch(new BuildInfrastructureCommand(pid, regionId))
                 : id == Fort ? _game.Dispatch(new BuildFortCommand(pid, regionId))
                 : id == Rail ? _game.Dispatch(new BuildRailCommand(pid, regionId))
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
        var extra = new[] { InfraGlyph, FortGlyph, RailGlyph }.Concat(Hud.BarGlyphs).ToArray();
        var asked = Glyph.Asked(w, extra);
        int known = asked.Count(Glyph.Knows);
        var (drawn, fell) = Glyph.Count(this);
        Close();
        return $"{status} ({drawn} chapas desenhadas no menu, {fell} na roda; "
             + $"{known} de {asked.Count} nomes pedidos pela base de dados têm desenho)";
    }
}
