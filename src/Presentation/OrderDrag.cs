using Godot;

namespace WarGame.Presentation;

/// <summary>A SETA DA ORDEM: o dedo pousa no contador da pilha, arrasta até à província e larga — e a tropa
/// marcha para lá. É o gesto do HoI4 (agarrar a unidade e puxar até ao destino), e era o que faltava depois
/// de o contador ter passado a ser a unidade na 0.3.85: dava para lhe tocar, não dava para lhe mandar nada
/// sem passar pelo duplo toque no destino, que é um gesto de mapa e não de comando.
///
/// Enquanto o dedo anda, a seta desenha-se com ele: haste grossa com rasto escuro por baixo, ponta cheia no
/// sítio onde o dedo está, punho no contador de onde a ordem sai e uma etiqueta a dizer para onde vai. A cor
/// é a convenção das outras setas do jogo — dourada quando é marcha para terra nossa ou vazia, vermelha
/// quando o destino é do inimigo (aquilo é um assalto, e deve ver-se antes de se largar o dedo), cinzenta
/// com uma cruz quando não se pode largar ali.
///
/// Só desenha. Quem decide o que a ordem faz é a ArmySelect, que já sabe marchar, saltar de pára-quedas e
/// meter a tropa no comboio — arrastar com o salto armado larga pára-quedistas, exactamente como o duplo
/// toque.</summary>
public partial class OrderDrag : Node2D
{
    private const float Width = 7f;          // grossura da haste, em unidades de mundo
    private const float HeadWidth = 44f;     // ponta da seta
    private const float HeadLength = 56f;

    private Node2D _tagRoot = null!;
    private Vector2 _from, _to;
    private bool _live, _hostile, _legal;
    private string _text = "";
    private float _tagScale = 1f, _thick = 1f;

    /// <summary>Há uma ordem em curso debaixo do dedo.</summary>
    public bool Live => _live;
    /// <summary>--smoke: o que a etiqueta da seta está a dizer.</summary>
    public string Text => _text;

    public void Setup()
    {
        ZIndex = 6;                                   // por cima das rotas: é a ordem que ainda está na mão
        _tagRoot = new Node2D { Name = "OrderTag" };
        AddChild(_tagRoot);
    }

    /// <summary>Como nas rotas: a etiqueta mantém-se legível ao afastar e a seta encolhe ao aproximar.</summary>
    public void SetZoom(float zoom)
    {
        _tagScale = Mathf.Clamp(1f / Mathf.Max(zoom, 0.001f), 0.05f, 6f);
        foreach (var c in _tagRoot.GetChildren()) if (c is Node2D n) n.Scale = Vector2.One * _tagScale;
        _thick = Mathf.Clamp(_tagScale, 0.3f, 1.6f);
        QueueRedraw();
    }

    /// <summary>O dedo pousou no contador: a seta nasce ali, ainda sem destino.</summary>
    public void Begin(Vector2 from)
    {
        _from = _to = from; _live = true; _legal = false; _hostile = false; _text = "";
        Ui.Clear(_tagRoot);
        QueueRedraw();
    }

    /// <summary>O dedo anda: a ponta segue-o e a etiqueta diz o que acontece se largar aqui.</summary>
    public void To(Vector2 to, string text, bool hostile, bool legal)
    {
        if (!_live) return;
        _to = to; _hostile = hostile; _legal = legal;
        if (text != _text) { _text = text; Tag(); }
        QueueRedraw();
    }

    /// <summary>O dedo levantou: a seta desaparece (a ordem, se foi dada, passa a ser rota).</summary>
    public void End()
    {
        _live = false; _text = "";
        Ui.Clear(_tagRoot);
        QueueRedraw();
    }

    private void Tag()
    {
        Ui.Clear(_tagRoot);
        if (_text.Length == 0) return;
        var lbl = Ui.Lbl(_text, 15);
        lbl.AddThemeColorOverride("font_color", Tint().Lightened(0.55f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        lbl.AddThemeConstantOverride("outline_size", 5);
        var box = new PanelContainer { Position = new Vector2(-70, -46) };
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.08f, 0.78f), 4));
        box.AddChild(lbl);
        var tag = new Node2D { Scale = Vector2.One * _tagScale };
        tag.AddChild(box);
        _tagRoot.AddChild(tag);
    }

    /// <summary>A cor da ordem: dourada a marchar, vermelha a assaltar, cinzenta quando não se pode largar
    /// ali. É a mesma convenção das setas de rota — a cor diz o que a ordem é antes de ela ser dada.</summary>
    private Color Tint() => !_legal ? new Color(0.62f, 0.62f, 0.66f) : _hostile ? Ui.Danger : Ui.Accent;

    public override void _Process(double delta)
    {
        if (_live && _tagRoot.GetChildCount() > 0 && _tagRoot.GetChild(0) is Node2D tag) tag.Position = _to;
    }

    public override void _Draw()
    {
        if (!_live) return;
        var tint = Tint();
        float w = Width * _thick;
        var span = _to - _from;
        float len = span.Length();

        // punho no contador: o sítio de onde a ordem sai fica marcado mesmo antes de a seta ter comprimento
        DrawCircle(_from, w * 1.9f, Ui.Ink with { A = 0.55f });
        DrawCircle(_from, w * 1.35f, tint.Lightened(0.35f));
        if (len < 4f) return;

        var dir = span / len;
        float head = MathF.Min(HeadLength * _thick, len * 0.55f);
        var at = _to - dir * head;
        var side = new Vector2(-dir.Y, dir.X) * (HeadWidth * _thick / 2f);
        DrawLine(_from, at, Ui.Ink with { A = 0.55f }, w + 5f * _thick);
        DrawLine(_from, at, tint, w);
        DrawColoredPolygon(new[] { at + side, _to, at - side }, tint with { A = 0.92f });
        DrawPolyline(new[] { at + side, _to, at - side, at + side }, Ui.Ink with { A = 0.6f }, 2f * _thick);

        if (!_legal)      // cruz na ponta: largar aqui não dá ordem nenhuma
        {
            float s = 13f * _thick;
            DrawLine(_to + new Vector2(-s, -s), _to + new Vector2(s, s), Ui.Danger, 3f * _thick);
            DrawLine(_to + new Vector2(s, -s), _to + new Vector2(-s, s), Ui.Danger, 3f * _thick);
        }
    }
}
