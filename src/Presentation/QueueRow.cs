using Godot;

namespace WarGame.Presentation;

/// <summary>Uma encomenda da fila de produção, que se pega e se larga noutro lugar — a fila arrastável do
/// HoI4. A ordem da fila sempre foi a prioridade (o cofre e as linhas de montagem servem-na de cima para
/// baixo), mas era a ordem de chegada e não havia como mexer nela: quem quisesse a coluna blindada esta
/// semana tinha de cancelar as cinco encomendas de infantaria à frente dela e voltar a encomendar tudo.
///
/// Agora arrasta-se: pega-se na chapa, arrasta-se para cima ou para baixo e larga-se na encomenda cujo
/// lugar se quer. Enquanto se arrasta, a linha por baixo do dedo acende-se em latão para se ver onde é que
/// aquilo vai cair, e o fantasma que segue o dedo diz o nome do que se está a mudar de sítio.
///
/// Só sabe de índices; quem despacha o comando é o painel.</summary>
public partial class QueueRow : PanelContainer
{
    private int _index;
    private string _label = "", _kind = "infantry", _spec = "";
    private Action<int, int>? _onMove;
    private StyleBoxFlat _rest = null!, _hot = null!;

    /// <summary>Índice desta encomenda na fila (o mesmo que o comando usa).</summary>
    public int Index => _index;

    public void Bind(int index, string label, string kind, string spec, Color tint, Action<int, int> onMove)
    {
        _index = index; _label = label; _kind = kind; _spec = spec; _onMove = onMove;
        _rest = Ui.Box(tint, 6);
        _hot = Ui.Box(Ui.SurfaceHi, 6);
        AddThemeStyleboxOverride("panel", _rest);
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "Arrasta para mudar a prioridade";
    }

    /// <summary>Pegar: o dado que viaja é o índice de onde a encomenda saiu. O fantasma leva o símbolo da
    /// tropa à frente do nome — a meio de um arrasto o dedo tapa a linha de origem, e é pelo fantasma que se
    /// sabe qual das encomendas vem agarrada.</summary>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        var ghost = new PanelContainer();
        ghost.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.9f }, 6));
        var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 8);
        line.AddChild(UnitSymbol.Of(_kind, 30f, 20f, _spec));
        var lbl = Ui.Lbl("⣿ " + _label, 15);
        lbl.AddThemeColorOverride("font_color", Ui.Accent);
        line.AddChild(lbl);
        ghost.AddChild(line);
        SetDragPreview(ghost);
        return _index;
    }

    /// <summary>Só se larga por cima de outra encomenda, e acende-se enquanto o dedo lá está.</summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool ok = data.VariantType == Variant.Type.Int && (int)data != _index;
        AddThemeStyleboxOverride("panel", ok ? _hot : _rest);
        return ok;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        AddThemeStyleboxOverride("panel", _rest);
        if (data.VariantType == Variant.Type.Int) _onMove?.Invoke((int)data, _index);
    }

    /// <summary>Largado o arrasto (aqui ou noutro sítio qualquer), a chapa apaga-se outra vez.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && _rest is not null) AddThemeStyleboxOverride("panel", _rest);
    }

    /// <summary>--smoke: o caminho do arrasto sem dedo nenhum — pega, valida e larga sobre esta linha.</summary>
    public bool Smoke(int fromIndex)
    {
        var data = Variant.From(fromIndex);
        if (!_CanDropData(Vector2.Zero, data)) return false;
        _DropData(Vector2.Zero, data);
        return true;
    }
}
