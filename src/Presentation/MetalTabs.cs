using Godot;

namespace WarGame.Presentation;

/// <summary>Abas de metal: a fila de separadores que arruma um painel em secções, desenhada como chapa de
/// latão em vez de rectângulos lisos. Até aqui o Ui.Tabs punha Buttons com um StyleBoxFlat — funcionava, mas
/// num painel já cheio de rebites e cantoneiras (PanelFrame) as abas eram a única coisa que parecia um
/// formulário. Os jogos de grande estratégia arrumam tudo assim: chapas gastas encaixadas numa calha, a que
/// está aberta levantada e acesa, as outras encostadas e escuras.
///
/// Uma aba é: chapa com o topo arredondado, degradê de cima (clara, luz rasante) para baixo (escura), fio de
/// luz na aresta de cima, sombra por baixo da que está aberta, dois rebites na aberta e uma calha de latão a
/// correr por baixo da fila inteira — acesa por baixo da aba aberta, apagada no resto. A aberta é mais alta
/// (as fechadas descem Sunk pixéis), que é o que dá o encaixe sem precisar de recorte nenhum.
///
/// Só desenha e diz qual foi carregada: não sabe nada do mundo. Quem lhe dá os nomes é o Ui.Tabs.</summary>
public partial class MetalTabs : Control
{
    /// <summary>Altura da chapa, quanto descem as fechadas, folga do texto e canto de cima.</summary>
    private const float Plate = 36f, Sunk = 5f, PadX = 18f, Corner = 7f, Rail = 3f;

    private string[] _labels = System.Array.Empty<string>();
    private float[] _x = System.Array.Empty<float>();      // aresta esquerda de cada aba
    private float[] _w = System.Array.Empty<float>();      // largura de cada aba
    private int _active, _hover = -1;
    private System.Action<int>? _pick;

    /// <summary>Enche a fila. Mede o texto com a fonte do tema, para uma aba com um nome comprido não cortar
    /// a letra — as abas não têm todas a mesma largura, como as etiquetas de uma pasta.</summary>
    public void Set(IReadOnlyList<string> labels, int active, System.Action<int> onPick)
    {
        _labels = labels.ToArray();
        _active = active; _pick = onPick;
        _x = new float[_labels.Length]; _w = new float[_labels.Length];

        var font = GetThemeDefaultFont();
        int size = 17;
        float at = 0f;
        for (int i = 0; i < _labels.Length; i++)
        {
            float text = font?.GetStringSize(_labels[i], HorizontalAlignment.Left, -1, size).X ?? _labels[i].Length * 9f;
            _x[i] = at; _w[i] = text + PadX * 2f;
            at += _w[i] + 2f;
        }
        CustomMinimumSize = new Vector2(at, Plate + Rail);
        MouseFilter = MouseFilterEnum.Stop;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_labels.Length == 0) return;
        var font = GetThemeDefaultFont();
        int size = 17;
        float bottom = Plate;

        // calha de latão por baixo da fila toda: é nela que as chapas encaixam
        DrawRect(new Rect2(0f, bottom, Size.X, Rail), Ui.Frame.Darkened(0.35f));

        for (int i = 0; i < _labels.Length; i++)
        {
            bool on = i == _active, hot = i == _hover;
            float top = on ? 0f : Sunk;
            var plate = new Rect2(_x[i], top, _w[i], bottom - top);

            // 1. chapa: base escura com o canto de cima redondo e a moldura de latão
            var box = new StyleBoxFlat
            {
                BgColor = on ? Ui.SurfaceHi : Ui.Surface.Darkened(hot ? 0.10f : 0.28f),
                CornerRadiusTopLeft = (int)Corner, CornerRadiusTopRight = (int)Corner,
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 0,
                BorderColor = on ? Ui.Accent : Ui.Frame.Darkened(0.25f),
            };
            DrawStyleBox(box, plate);

            // 2. degradê da chapa: luz rasante em cima, sombra no fundo (linha a linha, recuado dos cantos)
            var lit = (on ? Ui.Accent : Ui.Frame) with { A = on ? 0.20f : 0.10f };
            var dark = Ui.Ink with { A = on ? 0.28f : 0.45f };
            int lines = (int)(plate.Size.Y - 2f);
            for (int k = 0; k < lines; k++)
            {
                float t = (float)k / MathF.Max(1, lines - 1);
                DrawRect(new Rect2(plate.Position.X + 2f, plate.Position.Y + 1f + k, plate.Size.X - 4f, 1f),
                         lit.Lerp(dark, t));
            }

            // 3. fio de luz na aresta de cima: é o que faz a chapa parecer metal e não papel pintado
            DrawLine(new Vector2(plate.Position.X + Corner, plate.Position.Y + 1.5f),
                     new Vector2(plate.End.X - Corner, plate.Position.Y + 1.5f),
                     (on ? Ui.Accent : Ui.Frame) with { A = on ? 0.75f : 0.35f }, 1.5f);

            // 4. a aba aberta leva rebites e a sua fatia da calha acesa; as outras ficam com a sombra
            if (on)
            {
                foreach (float rx in new[] { plate.Position.X + 9f, plate.End.X - 9f })
                {
                    DrawCircle(new Vector2(rx, plate.Position.Y + 11f), 2.6f, Ui.Ink with { A = 0.7f });
                    DrawCircle(new Vector2(rx - 0.6f, plate.Position.Y + 10.4f), 1.5f, Ui.Accent with { A = 0.85f });
                }
                DrawRect(new Rect2(plate.Position.X, bottom, plate.Size.X, Rail), Ui.Accent);
            }
            else
                DrawRect(new Rect2(plate.Position.X, bottom - 1f, plate.Size.X, 1f), Ui.Ink with { A = 0.6f });

            // 5. o nome, centrado na chapa
            if (font is null) continue;
            var text = font.GetStringSize(_labels[i], HorizontalAlignment.Left, -1, size);
            var at = new Vector2(plate.Position.X + (plate.Size.X - text.X) / 2f,
                                 plate.Position.Y + (plate.Size.Y + text.Y * 0.62f) / 2f);
            if (on) DrawString(font, at + new Vector2(0f, 1f), _labels[i], HorizontalAlignment.Left, -1, size,
                               Ui.Ink with { A = 0.8f });
            DrawString(font, at, _labels[i], HorizontalAlignment.Left, -1, size, on ? Ui.Text : Ui.TextDim);
        }
    }

    /// <summary>Carregar escolhe a aba; passar por cima acende-a. A aba escolhida é decidida por quem manda
    /// (o painel guarda o índice e volta a encher), como no Ui.Tabs de antes.</summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion m)
        {
            int was = _hover;
            _hover = At(m.Position);
            if (was != _hover) QueueRedraw();
            return;
        }
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } b
            && At(b.Position) is int i and >= 0)
        {
            AcceptEvent();
            _pick?.Invoke(i);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && _hover != -1) { _hover = -1; QueueRedraw(); }
    }

    /// <summary>Que aba está debaixo deste ponto (−1 = nenhuma).</summary>
    private int At(Vector2 p)
    {
        for (int i = 0; i < _labels.Length; i++)
            if (p.X >= _x[i] && p.X <= _x[i] + _w[i] && p.Y <= Plate + Rail) return i;
        return -1;
    }
}
