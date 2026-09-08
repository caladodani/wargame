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
/// Ao centro passou a tapar outra coisa: a fita de notificações, que nasce à mesma altura. Não há sítio no
/// ecrã de um telemóvel que esteja sempre livre — o que muda é quem está a jogar e o que quer ver. Por isso
/// a faixa deixou de ter um sítio: arrasta-se pela pega, guarda-se onde ficou (Settings, sobrevive ao jogo
/// seguinte) e o Hud só a arruma enquanto ninguém lhe tocou. Um toque na pega, sem arrastar, dobra-a até ao
/// título — a maneira mais rápida de a tirar da frente sem a perder de vista. A tela é o limite: por muito
/// que se puxe, a faixa não sai do ecrã.
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
    /// <summary>Folga entre a faixa e a borda do ecrã: arrastada até ao fim, ainda se lhe vê a moldura.</summary>
    private const float Margin = 4f;
    /// <summary>Menos do que isto de dedo andado e não foi arrasto nenhum: foi um toque, e um toque dobra.</summary>
    private const float TapSlop = 8f;

    private Game _game = null!;
    private VBoxContainer _list = null!;
    private Label _chevron = null!;
    private string _key = "";
    private int _count;          // avisos desenhados agora (a chave guarda o redesenho, isto guarda o visível)
    private bool _covered;       // um painel está aberto por cima do mapa

    private float _under = 132f; // altura que o Hud pediu; só vale enquanto o jogador não a arrumar
    private Vector2 _spot;       // canto superior esquerdo escolhido, em pixéis da tela
    private bool _placed;        // o jogador já a pôs onde quer: o Hud deixa de mandar
    private bool _folded;        // dobrada até ao título
    private bool _dragging;      // dedo em baixo na pega
    private bool _dragged;       // e já passou a folga: é arrasto, não toque
    private Vector2 _grab;       // onde o dedo pegou, em coordenadas da faixa

    /// <summary>Aviso com sítio no mapa: leva a câmara lá e abre a ficha da região.</summary>
    public Action<int>? OnGoTo;
    /// <summary>Aviso sem sítio: o id diz que painel resolve o assunto ("offers", "queue", "research").</summary>
    public Action<string>? OnOpen;

    public void Setup(Game game)
    {
        _game = game;
        AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.9f }, 6));
        MouseFilter = MouseFilterEnum.Stop;                  // o toque na faixa não é pan do mapa
        Visible = false;

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        v.MouseFilter = MouseFilterEnum.Ignore;              // a caixa não come o arrasto: ele é da faixa
        AddChild(v);
        v.AddChild(Head());
        v.AddChild(Ui.Rule());
        _list = new VBoxContainer(); _list.AddThemeConstantOverride("separation", 3); v.AddChild(_list);

        if (Settings.AlertSpot is Vector2 spot) { _spot = spot; _placed = true; }
        Fold(Settings.AlertFolded);
        Settle();
    }

    /// <summary>A pega: três pontos, o título e a seta que diz se está aberta ou dobrada. Nada aqui apanha
    /// o rato — o arrasto e o toque são tratados pela faixa inteira, em _GuiInput, e os únicos filhos que
    /// ficam com o toque para si são os botões dos avisos (senão tocar num aviso arrastava a faixa).</summary>
    private HBoxContainer Head()
    {
        var bar = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 26),          // altura de dedo para pegar nela
        };
        bar.AddThemeConstantOverride("separation", 8);
        bar.AddChild(Glyph("· · ·", Ui.TextDim));
        var title = Glyph("A L A R M E S", Ui.Accent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bar.AddChild(title);
        _chevron = Glyph("▼", Ui.TextDim);
        bar.AddChild(_chevron);
        return bar;
    }

    private static Label Glyph(string text, Color color)
    {
        var l = Ui.Lbl(text, 14);
        l.AddThemeColorOverride("font_color", color);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.MouseFilter = MouseFilterEnum.Ignore;
        return l;
    }

    /// <summary>A barra de topo mudou de altura (os mostradores dobraram para outra linha): a faixa desce
    /// com ela em vez de ficar meia escondida por trás. Depois de o jogador a arrumar à mão, cala-se: a
    /// posição escolhida por quem joga ganha sempre à do layout.</summary>
    public void PlaceUnder(float y)
    {
        _under = y;
        if (!_placed) Settle();
    }

    /// <summary>Põe a faixa no sítio: ao centro por baixo da barra enquanto ninguém lhe tocou, no canto
    /// escolhido depois disso — e sempre dentro da tela, que é o que a salva de uma rotação de ecrã ou de
    /// um degrau de tamanho que a deixasse meia de fora.</summary>
    private void Settle()
    {
        if (_placed)
        {
            var screen = GetViewportRect().Size;
            _spot = new Vector2(
                Mathf.Clamp(_spot.X, Margin, MathF.Max(Margin, screen.X - Size.X - Margin)),
                Mathf.Clamp(_spot.Y, Margin, MathF.Max(Margin, screen.Y - Size.Y - Margin)));
            AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = 0;
            GrowHorizontal = GrowDirection.End; GrowVertical = GrowDirection.End;
            OffsetLeft = OffsetRight = _spot.X;
            OffsetTop = OffsetBottom = _spot.Y;
            return;
        }
        AnchorLeft = AnchorRight = 0.5f; AnchorTop = AnchorBottom = 0;
        GrowHorizontal = GrowDirection.Both; GrowVertical = GrowDirection.End;
        OffsetLeft = OffsetRight = 0;                        // cresce para os dois lados: fica centrada
        OffsetTop = OffsetBottom = _under;
    }

    /// <summary>Dobra até ao título, ou abre. Guarda-se: quem a dobrou não a quer de volta aberta no jogo
    /// seguinte.</summary>
    private void Fold(bool folded)
    {
        _folded = folded;
        _list.Visible = !folded;
        _chevron.Text = folded ? "▶" : "▼";
        ResetSize();                                         // encolher já, senão fica com o tamanho de antes
        Settle();
    }

    /// <summary>Arrastar pela pega e dobrar com um toque. O dedo pousado numa chapa de aviso nunca chega
    /// aqui — o botão fica-lhe com o toque — por isso a pega é o título e as folgas entre as chapas.</summary>
    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton b && b.ButtonIndex == MouseButton.Left)
        {
            if (b.Pressed) { _dragging = true; _dragged = false; _grab = b.Position; }
            else if (_dragging)
            {
                _dragging = false;
                if (_dragged) Settings.SetAlertSpot(_spot);
                else { Fold(!_folded); Settings.SetAlertFolded(_folded); }
            }
            AcceptEvent();
        }
        else if (e is InputEventMouseMotion m && _dragging)
        {
            var moved = m.Position - _grab;
            if (!_dragged && moved.Length() < TapSlop) return;   // ainda pode ser um toque: não mexer nada
            _dragged = true;
            _placed = true;
            _spot = GlobalPosition + moved;
            Settle();
            AcceptEvent();
        }
    }

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
            Settle();                    // mais avisos, faixa mais alta: pode já não caber onde estava
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
