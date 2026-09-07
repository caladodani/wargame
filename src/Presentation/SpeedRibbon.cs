using Godot;

namespace WarGame.Presentation;

/// <summary>A fita das velocidades, como a do HoI4: cinco casas de metal encostadas umas às outras — pausa,
/// e depois quatro andamentos — com a casa em uso acesa a latão.
///
/// Antes eram três botões soltos ("&lt;", "||", "&gt;") e um punhado de setas a seguir à data: para saltar da
/// pausa para a velocidade máxima carregava-se quatro vezes, e para saber em que andamento se estava era
/// preciso contar setinhas. A fita diz o estado sem se ler nada e leva a qualquer andamento num toque.
///
/// Clock.Speed é a excepção conhecida à regra de que a UI só despacha comandos (ver Game): o relógio é do
/// jogador, não do mundo.</summary>
public partial class SpeedRibbon : PanelContainer
{
    /// <summary>Nome de cada andamento, na ordem do Clock.Speed (0..4). O nome traz o ritmo atrás porque é
    /// isso que se quer saber ao carregar: "Depressa" não diz nada, "1 dia/s" diz.</summary>
    public static readonly string[] Names = { "Pausa", "Devagar", "Normal", "Depressa", "A correr" };

    /// <summary>O que cada casa promete, em tempo de jogo por segundo real (ver Game.SpeedSeconds).</summary>
    public static string Pace(int speed) => Game.SpeedPace[Mathf.Clamp(speed, 0, Game.SpeedPace.Length - 1)];
    private static readonly string[] Faces = { "❚❚", "▶", "▶▶", "▶▶▶", "▶▶▶▶" };

    private Game _game = null!;
    private readonly Button[] _cells = new Button[5];
    private Label _name = null!;
    private int _lit = -1;

    public void Setup(Game game)
    {
        _game = game;
        AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.85f }, 4));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 2); AddChild(row);
        for (int i = 0; i < _cells.Length; i++)
        {
            int level = i;
            var b = new Button
            {
                Text = Faces[i], CustomMinimumSize = new Vector2(i == 0 ? 54 : 44, 40),
                TooltipText = i == 0 ? "Pausa" : $"{Names[i]} — {Pace(i)} de jogo",
            };
            b.AddThemeFontSizeOverride("font_size", 15);
            b.Pressed += () => Set(level);
            _cells[i] = b;
            row.AddChild(b);
        }
        _name = Ui.Lbl("", 14);
        _name.CustomMinimumSize = new Vector2(96, 0);
        _name.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_name);
        Paint(_game.World.Clock.Speed);
    }

    /// <summary>Põe o mundo neste andamento (0 = pausa). Um toque na casa acesa volta à pausa, que é o gesto
    /// que toda a gente faz quando alguma coisa corre mal na frente.</summary>
    public void Set(int level)
    {
        var c = _game.World.Clock;
        if (_game.PlayerId is null) { c.Speed = 0; Refresh(); return; }
        c.Speed = c.Speed == level && level != 0 ? 0 : Mathf.Clamp(level, 0, _cells.Length - 1);
        Refresh();
    }

    /// <summary>Empurra a velocidade um degrau para cima ou para baixo (as teclas antigas continuam a servir).</summary>
    public void Step(int delta)
    {
        var c = _game.World.Clock;
        if (_game.PlayerId is null) { c.Speed = 0; Refresh(); return; }
        c.Speed = delta == 0 ? (c.Speed == 0 ? 1 : 0) : Mathf.Clamp(c.Speed + delta, 0, _cells.Length - 1);
        Refresh();
    }

    public void Refresh() => Paint(_game.World.Clock.Speed);

    private void Paint(int speed)
    {
        speed = Mathf.Clamp(speed, 0, _cells.Length - 1);
        if (speed == _lit) return;
        _lit = speed;
        for (int i = 0; i < _cells.Length; i++)
        {
            // a fita enche-se até ao andamento em uso: as casas passadas ficam mornas, as de diante apagadas
            bool on = i == speed, under = i > 0 && i < speed;
            var tint = speed == 0 ? Ui.Danger : Ui.Accent;
            var bg = on ? tint.Darkened(0.35f) : under ? tint.Darkened(0.72f) : Ui.Surface.Darkened(0.25f);
            var box = Ui.Box(bg, 2);
            box.BorderColor = on ? tint.Lightened(0.35f) : Ui.Frame;
            _cells[i].AddThemeStyleboxOverride("normal", box);
            _cells[i].AddThemeStyleboxOverride("hover", Ui.Box(bg.Lightened(0.12f), 2));
            _cells[i].AddThemeStyleboxOverride("pressed", Ui.Box(bg.Darkened(0.25f), 2));
            _cells[i].AddThemeColorOverride("font_color", on ? tint.Lightened(0.6f) : Ui.TextDim);
        }
        _name.Text = speed == 0 ? Names[0] : $"{Names[speed]}\n{Pace(speed)}";
        _name.AddThemeColorOverride("font_color", speed == 0 ? Ui.Danger.Lightened(0.3f) : Ui.TextDim);
    }

    /// <summary>--smoke: percorre a fita toda e diz em que andamento ficou (volta à velocidade de partida).</summary>
    public string Smoke()
    {
        int was = _game.World.Clock.Speed;
        for (int i = 0; i < _cells.Length; i++) Set(i);
        Set(was);
        int at = Mathf.Clamp(_game.World.Clock.Speed, 0, _cells.Length - 1);
        return $"{Names[at]} ({Pace(at)})";
    }
}
