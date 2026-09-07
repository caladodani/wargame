using Godot;

namespace WarGame.Presentation;

/// <summary>Régua de latão: o controlo de arrastar do jogo, desenhado como uma calha de metal com entalhes
/// e um cursor rebitado. Até aqui o jogo não tinha maneira nenhuma de pedir um número numa gama — as
/// escolhas eram todas de sim/não (botões) ou de uma entre poucas (as chapas do MetalTabs) — e a primeira
/// coisa que precisou de uma fatia (o empréstimo de material: quanto do nosso rendimento parte por dia)
/// não se podia perguntar sem inventar um HSlider do Godot, que num painel de rebites e cantoneiras
/// parecia um formulário caído lá dentro.
///
/// É: calha embutida com sombra por dentro, entalhes de latão marcados de passo a passo, o troço já
/// escolhido cheio com degradê de luz em cima, e um cursor de chapa com dois rebites e um risco de luz na
/// aresta. Por cima da calha vai a legenda que quem chama souber escrever para cada valor — a fatia em
/// percentagem, os pontos por dia, o que se quiser.
///
/// Arrasta-se com o dedo ou com o rato. Enquanto se arrasta só se redesenha (e chama-se onSlide para a
/// legenda acompanhar); a ordem parte quando se larga (onDone), porque quem manda mudar a fatia é um
/// comando validado e não se despacha um por píxel percorrido.</summary>
public partial class BrassSlider : Control
{
    /// <summary>Altura total, altura da calha, largura e altura do cursor, e a folga do texto por cima.</summary>
    private const float Tall = 58f, Rail = 16f, KnobW = 24f, KnobH = 34f, Caption = 20f;

    private float _min, _max = 1f, _step = 0.01f, _value;
    private Func<float, string> _text = v => $"{v:0.##}";
    private Action<float>? _slide, _done;
    private bool _dragging, _hover;

    /// <summary>Valor actual, já preso à gama e ao passo.</summary>
    public float Value => _value;

    /// <summary>Enche a régua. `text` escreve a legenda de um valor; `onSlide` corre a cada movimento (para
    /// quem quiser acompanhar noutro sítio) e `onDone` corre quando o dedo se levanta — é aí que se manda
    /// a ordem.</summary>
    public void Set(float min, float max, float step, float value,
                    Func<float, string> text, Action<float>? onSlide = null, Action<float>? onDone = null)
    {
        _min = min; _max = MathF.Max(min + 1e-4f, max); _step = MathF.Max(1e-4f, step);
        _value = Snap(value);
        _text = text; _slide = onSlide; _done = onDone;
        CustomMinimumSize = new Vector2(160f, Tall);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        QueueRedraw();
    }

    /// <summary>Põe a régua num valor sem chamar ninguém (para quem a queira acertar de fora).</summary>
    public void Put(float value) { _value = Snap(value); QueueRedraw(); }

    private float Snap(float v) =>
        Math.Clamp(MathF.Round(Math.Clamp(v, _min, _max) / _step) * _step, _min, _max);

    /// <summary>Fracção percorrida (0 à esquerda, 1 à direita).</summary>
    private float T => (_value - _min) / (_max - _min);

    /// <summary>Onde a calha começa e acaba: o cursor tem de caber inteiro dentro dela nas duas pontas.</summary>
    private float Left => KnobW / 2f;
    private float Right => MathF.Max(Left + 1f, Size.X - KnobW / 2f);

    public override void _Draw()
    {
        float railY = Caption + (Tall - Caption - Rail) / 2f;
        var rail = new Rect2(Left, railY, Right - Left, Rail);

        // 1. calha embutida: fundo escuro com moldura de latão apagado e sombra por dentro no topo
        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = Ui.Ink,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            BorderColor = Ui.Frame.Darkened(0.2f),
        }, rail);
        DrawLine(new Vector2(rail.Position.X + 2f, rail.Position.Y + 2f),
                 new Vector2(rail.End.X - 2f, rail.Position.Y + 2f), Colors.Black with { A = 0.5f }, 2f);

        // 2. troço escolhido: latão com luz em cima e sombra em baixo, linha a linha
        float knobX = rail.Position.X + rail.Size.X * T;
        if (knobX > rail.Position.X + 2f)
        {
            int lines = (int)(rail.Size.Y - 4f);
            for (int k = 0; k < lines; k++)
            {
                float t = (float)k / MathF.Max(1, lines - 1);
                DrawRect(new Rect2(rail.Position.X + 2f, rail.Position.Y + 2f + k, knobX - rail.Position.X - 2f, 1f),
                         Ui.Accent.Lightened(0.25f).Lerp(Ui.Accent.Darkened(0.45f), t));
            }
        }

        // 3. entalhes: um risco por passo, para se ver que a fatia anda aos degraus e não a esmo
        int steps = (int)MathF.Round((_max - _min) / _step);
        if (steps is > 0 and <= 40)
            for (int i = 0; i <= steps; i++)
            {
                float x = rail.Position.X + rail.Size.X * i / steps;
                DrawLine(new Vector2(x, rail.End.Y + 1f), new Vector2(x, rail.End.Y + 4f), Ui.Frame with { A = 0.8f }, 1f);
            }

        // 4. cursor: chapa de latão com dois rebites e o fio de luz na aresta de cima
        var knob = new Rect2(knobX - KnobW / 2f, railY + (Rail - KnobH) / 2f, KnobW, KnobH);
        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = _dragging ? Ui.SurfaceHi.Lightened(0.12f) : _hover ? Ui.SurfaceHi : Ui.Surface,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 2, BorderWidthBottom = 1,
            BorderColor = _dragging || _hover ? Ui.Accent : Ui.Frame,
        }, knob);
        DrawLine(new Vector2(knob.Position.X + 3f, knob.Position.Y + 2f),
                 new Vector2(knob.End.X - 3f, knob.Position.Y + 2f), Ui.Accent with { A = 0.6f }, 1f);
        foreach (float ry in new[] { knob.Position.Y + 9f, knob.End.Y - 9f })
        {
            DrawCircle(new Vector2(knob.Position.X + KnobW / 2f, ry), 2.6f, Ui.Ink with { A = 0.75f });
            DrawCircle(new Vector2(knob.Position.X + KnobW / 2f - 0.6f, ry - 0.6f), 1.4f, Ui.Accent with { A = 0.85f });
        }

        // 5. legenda por cima da calha, em latão quando o dedo lá está
        var font = GetThemeDefaultFont();
        if (font is null) return;
        string label = _text(_value);
        var size = font.GetStringSize(label, HorizontalAlignment.Left, -1, 16);
        DrawString(font, new Vector2(rail.Position.X, size.Y * 0.8f), label, HorizontalAlignment.Left, -1, 16,
                   _dragging ? Ui.Accent.Lightened(0.3f) : Ui.Text);
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } b:
                AcceptEvent(); _dragging = true; Move(b.Position.X); break;
            case InputEventScreenTouch { Pressed: true } t:
                AcceptEvent(); _dragging = true; Move(t.Position.X); break;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } up when _dragging:
                AcceptEvent(); Move(up.Position.X); Release(); break;
            case InputEventScreenTouch { Pressed: false } tu when _dragging:
                AcceptEvent(); Move(tu.Position.X); Release(); break;
            case InputEventMouseMotion m:
                if (_dragging) { AcceptEvent(); Move(m.Position.X); }
                else if (!_hover) { _hover = true; QueueRedraw(); }
                break;
            case InputEventScreenDrag d when _dragging:
                AcceptEvent(); Move(d.Position.X); break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && _hover) { _hover = false; QueueRedraw(); }
    }

    private void Move(float x)
    {
        float was = _value;
        _value = Snap(_min + (_max - _min) * Math.Clamp((x - Left) / (Right - Left), 0f, 1f));
        if (_value == was) return;
        QueueRedraw();
        try { _slide?.Invoke(_value); } catch (Exception ex) { GD.PushError("Régua: " + ex); }
    }

    private void Release()
    {
        _dragging = false;
        QueueRedraw();
        try { _done?.Invoke(_value); } catch (Exception ex) { GD.PushError("Régua: " + ex); }
    }

    /// <summary>--smoke: arrasta a régua até uma fracção da gama e larga-a, como faria um dedo. Devolve o
    /// valor em que ficou, para o smoke provar que a régua prende ao passo e chama quem a ouve.</summary>
    public float SmokeDrag(float t)
    {
        Size = new Vector2(MathF.Max(Size.X, 200f), Tall);
        _dragging = true;
        Move(Left + (Right - Left) * Math.Clamp(t, 0f, 1f));
        Release();
        return _value;
    }
}
