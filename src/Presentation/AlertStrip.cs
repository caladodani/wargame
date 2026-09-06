using Godot;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Faixa de avisos encostada à direita, por baixo da barra de topo — o sítio onde os jogos de
/// grande estratégia põem as bandeirinhas de alarme. Cada aviso é uma chapa estreita com ícone, moldura de
/// latão e uma barra de cor à esquerda pela gravidade: vermelho arde já, âmbar pede atenção, cinzento é
/// nota de rodapé.
///
/// Até aqui o jogo só falava quando alguma coisa acontecia, numa notificação que passava. O que estava mal
/// *agora* — cofre a secar, tropa a beber areia, fronteira aberta, terra ocupada a ferver, fábricas
/// paradas — só se descobria indo a cada painel ver os números. Isto levanta a mão sozinho.
///
/// Tocar num aviso leva ao problema: os que têm sítio no mapa levam a câmara lá e abrem a ficha da região,
/// os outros abrem o painel que resolve o assunto. Quem decide o que é aviso é o Core (Alerts); aqui só se
/// desenha, e só quando a chave do conjunto muda.</summary>
public partial class AlertStrip : PanelContainer
{
    private Game _game = null!;
    private VBoxContainer _list = null!;
    private string _key = "";

    /// <summary>Aviso com sítio no mapa: leva a câmara lá e abre a ficha da região.</summary>
    public Action<int>? OnGoTo;
    /// <summary>Aviso sem sítio: o id diz que painel resolve o assunto ("offers", "queue", "research").</summary>
    public Action<string>? OnOpen;

    public void Setup(Game game)
    {
        _game = game;
        AnchorLeft = 1; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 0;
        GrowHorizontal = GrowDirection.Begin; GrowVertical = GrowDirection.End;
        OffsetRight = -12; OffsetTop = 132;                  // a barra de topo tem duas linhas
        AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.9f }, 6));
        MouseFilter = MouseFilterEnum.Stop;                  // o toque na faixa não é pan do mapa
        Visible = false;

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); AddChild(v);
        v.AddChild(Ui.Head("Alarmes"));
        _list = new VBoxContainer(); _list.AddThemeConstantOverride("separation", 3); v.AddChild(_list);
    }

    /// <summary>Redesenha se o conjunto de avisos mudou. Corre a cada tick da UI, por isso a chave é o
    /// que a defende: sem mudanças não se toca em nada.</summary>
    public void Refresh()
    {
        if (_game.PlayerId is not int pid) { Visible = false; _key = ""; return; }
        var alerts = Alerts.For(_game.World, pid);
        string key = Alerts.Key(alerts);
        if (key == _key) return;
        _key = key;

        Ui.Clear(_list);
        Visible = alerts.Count > 0;
        foreach (var a in alerts) _list.AddChild(Row(a));
    }

    private Control Row(Alert a)
    {
        var color = a.Level switch
        {
            AlertLevel.Danger => Ui.Danger,
            AlertLevel.Warn => Ui.Accent,
            _ => Ui.TextDim,
        };
        var b = new Button { TooltipText = a.Text, CustomMinimumSize = new Vector2(300, 0) };
        b.AddThemeStyleboxOverride("normal", Ui.Box(Ui.Surface, 6));
        b.AddThemeStyleboxOverride("hover", Ui.Box(Ui.SurfaceHi, 6));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(Ui.SurfaceHi, 6));
        b.Pressed += () => { if (a.RegionId > 0) OnGoTo?.Invoke(a.RegionId); else OnOpen?.Invoke(a.Id); };

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        // barra de gravidade à esquerda: lê-se a cor antes de se ler a frase
        row.AddChild(new ColorRect { Color = color, CustomMinimumSize = new Vector2(4, 0), MouseFilter = MouseFilterEnum.Ignore });
        var icon = Ui.Lbl(a.Icon, 18); icon.AddThemeColorOverride("font_color", color); row.AddChild(icon);
        var text = Ui.Lbl(a.Text, 15);
        text.AddThemeColorOverride("font_color", a.Level == AlertLevel.Info ? Ui.TextDim : Ui.Text);
        row.AddChild(Ui.Grow(text));
        b.AddChild(row);
        // o Button não faz o layout dos filhos: a linha acompanha-o à mão
        b.Resized += () => row.Size = b.Size;
        b.CustomMinimumSize = new Vector2(300, 30);
        return b;
    }

    /// <summary>--smoke: quantos avisos estão na faixa depois de a desenhar.</summary>
    public int Smoke()
    {
        Refresh();
        return _list.GetChildCount();
    }
}
