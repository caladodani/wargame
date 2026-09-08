using Godot;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Faixa de avisos ao centro do ecrã, por baixo da barra de topo — o sítio onde os jogos de
/// grande estratégia põem as bandeirinhas de alarme. Cada aviso é uma chapa estreita com ícone, moldura de
/// latão e uma barra de cor à esquerda pela gravidade: vermelho arde já, âmbar pede atenção, cinzento é
/// nota de rodapé.
///
/// Estava encostada à direita e ficava por cima dos painéis: além de tapar o que eles diziam, comia os
/// toques que iam para os botões deles — o "Mudar frente" do painel Exércitos nascia debaixo dela e não
/// havia maneira de lhe tocar. A faixa é do mapa: com um painel aberto sai da frente (SetCovered), como já
/// faziam a fita dos modos, o rodapé e o menu de construir.
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
    private int _count;          // avisos desenhados agora (a chave guarda o redesenho, isto guarda o visível)
    private bool _covered;       // um painel está aberto por cima do mapa

    /// <summary>Aviso com sítio no mapa: leva a câmara lá e abre a ficha da região.</summary>
    public Action<int>? OnGoTo;
    /// <summary>Aviso sem sítio: o id diz que painel resolve o assunto ("offers", "queue", "research").</summary>
    public Action<string>? OnOpen;

    public void Setup(Game game)
    {
        _game = game;
        AnchorLeft = 0.5f; AnchorRight = 0.5f; AnchorTop = 0; AnchorBottom = 0;
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.End;
        OffsetLeft = 0; OffsetRight = 0;                     // cresce para os dois lados: fica centrada
        OffsetTop = 132;                                     // altura de partida; o Hud acerta-a pela barra real
        AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.9f }, 6));
        MouseFilter = MouseFilterEnum.Stop;                  // o toque na faixa não é pan do mapa
        Visible = false;

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); AddChild(v);
        v.AddChild(Ui.Head("Alarmes"));
        _list = new VBoxContainer(); _list.AddThemeConstantOverride("separation", 3); v.AddChild(_list);
    }

    /// <summary>A barra de topo mudou de altura (os mostradores dobraram para outra linha): a faixa desce
    /// com ela em vez de ficar meia escondida por trás.</summary>
    public void PlaceUnder(float y) => OffsetTop = y;

    /// <summary>Redesenha se o conjunto de avisos mudou. Corre a cada tick da UI, por isso a chave é o
    /// que a defende: sem mudanças não se toca em nada.</summary>
    public void Refresh()
    {
        if (_game.PlayerId is not int pid) { _count = 0; _key = ""; Reveal(); return; }
        var alerts = Alerts.For(_game.World, pid);
        string key = Alerts.Key(alerts);
        if (key != _key)
        {
            _key = key;
            Ui.Clear(_list);
            foreach (var a in alerts) _list.AddChild(Row(a));
            _count = alerts.Count;
        }
        Reveal();
    }

    /// <summary>Um painel aberto tapa o mapa e a faixa é do mapa: sai da frente enquanto ele lá estiver.
    /// Sem isto não era só feio — era um botão do painel que não pegava, porque a faixa lhe apanhava o
    /// toque primeiro.</summary>
    public void SetCovered(bool covered) { _covered = covered; Reveal(); }

    private void Reveal() => Visible = _count > 0 && !_covered;

    private Control Row(Alert a)
    {
        var color = a.Level switch
        {
            AlertLevel.Danger => Ui.Danger,
            AlertLevel.Warn => Ui.Accent,
            _ => Ui.TextDim,
        };
        // a frase vai no texto do próprio botão (e não numa etiqueta filha): um Button não faz o layout dos
        // filhos, e enquanto a frase era filha o botão media 300px fixos e cortava os avisos compridos ao
        // meio. Assim é ele que se mede pela frase; os espaços à esquerda abrem o sítio da chapa e do ícone,
        // que são os únicos filhos e vão ancorados à mão.
        var b = new Button
        {
            Text = "      " + a.Text,
            TooltipText = a.Text,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(300, 34),
        };
        b.AddThemeFontSizeOverride("font_size", 15);
        b.AddThemeColorOverride("font_color", a.Level == AlertLevel.Info ? Ui.TextDim : Ui.Text);
        b.AddThemeStyleboxOverride("normal", Ui.Box(Ui.Surface, 6));
        b.AddThemeStyleboxOverride("hover", Ui.Box(Ui.SurfaceHi, 6));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(Ui.SurfaceHi, 6));
        b.Pressed += () => { if (a.RegionId > 0) OnGoTo?.Invoke(a.RegionId); else OnOpen?.Invoke(a.Id); };

        // barra de gravidade encostada à esquerda: lê-se a cor antes de se ler a frase
        var bar = new ColorRect { Color = color, MouseFilter = MouseFilterEnum.Ignore };
        bar.AnchorTop = 0; bar.AnchorBottom = 1;
        bar.OffsetLeft = 5; bar.OffsetRight = 9; bar.OffsetTop = 5; bar.OffsetBottom = -5;
        b.AddChild(bar);

        var icon = Ui.Lbl(a.Icon, 16);
        icon.AddThemeColorOverride("font_color", color);
        icon.MouseFilter = MouseFilterEnum.Ignore;
        icon.AnchorTop = icon.AnchorBottom = 0.5f;
        icon.OffsetLeft = 14; icon.OffsetRight = 34; icon.OffsetTop = -11; icon.OffsetBottom = 11;
        b.AddChild(icon);
        return b;
    }

    /// <summary>--smoke: quantos avisos estão na faixa depois de a desenhar.</summary>
    public int Smoke()
    {
        Refresh();
        return _list.GetChildCount();
    }
}
