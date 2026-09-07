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

    // Os edifícios trazem o desenho da tabela `building`; estes dois não são linhas de lá — são regras
    // (infra_build_cost, fort_build_cost) e já tinham o nome escrito aqui. O desenho fica ao pé do nome.
    private const string InfraIcon = "🛣", FortIcon = "🛡";

    private Game _game = null!;
    private VBoxContainer _list = null!;
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
        _status = Ui.Lbl("Escolhe o tipo e toca no mapa", 14);
        _status.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(_status);
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
            _list.AddChild(Pick(d.Name, d.Id, $"{d.Cost:0}, {d.Days:0} d", d.Icon));
        _list.AddChild(Pick("Infra-estrutura", Infra, $"{w.Rule("infra_build_cost", 40f):0}, {w.Rule("infra_build_days", 30f):0} d", InfraIcon));
        _list.AddChild(Pick("Fortificação", Fort, $"{w.Rule("fort_build_cost", 30f):0}, {w.Rule("fort_build_days", 20f):0} d", FortIcon));
    }

    /// <summary>Uma chapa da lista. O desenho vai à cabeça do nome, como no menu de construção do HoI4: numa
    /// lista de seis obras todas escritas do mesmo tamanho, o que se procura encontra-se pela figura e não
    /// pela leitura. Edifício sem desenho na tabela leva um espaço no lugar dele, para as colunas de nomes
    /// não ficarem em degrau.</summary>
    private Button Pick(string name, string id, string cost, string icon)
    {
        var b = Ui.Btn($"{(icon.Length == 0 ? " " : icon)}  {name} ({cost})", () => Arm(id), 0f,
                       id == _armed ? Ui.Kind.Primary : Ui.Kind.Normal);
        b.Alignment = HorizontalAlignment.Left;
        return b;
    }

    private void Arm(string id)
    {
        _armed = id; _painted = "";
        Fill();
        (string name, string icon) = id == Infra ? ("Infra-estrutura", InfraIcon)
            : id == Fort ? ("Fortificação", FortIcon)
            : _game.World.BuildingDefs.TryGetValue(id, out var d) ? (d.Name, d.Icon) : (id, "");
        _status.Text = $"Toca no mapa onde construir — {icon} {name}".Replace("—  ", "— ");
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

    /// <summary>--smoke: abre o menu, arma o primeiro tipo e devolve o que ficou escolhido. Conta também as
    /// obras com desenho: a coluna `building.icon` vazia não dá erro nenhum, apenas uma lista sem figuras.</summary>
    public string Smoke()
    {
        Open();
        var defs = _game.World.BuildingDefs.Values.OrderBy(d => d.Id).ToList();
        var first = defs.FirstOrDefault();
        if (first is not null) Arm(first.Id);
        string status = _status.Text;
        int com = defs.Count(d => d.Icon.Length > 0) + 2;   // +2: infra e fortificação, que não são linhas de `building`
        Close();
        return $"{status} ({com} de {defs.Count + 2} obras com desenho)";
    }
}
