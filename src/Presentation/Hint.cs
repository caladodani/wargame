using Godot;

namespace WarGame.Presentation;

/// <summary>A dica ao dedo parado: o que o rato mostra a quem passa por cima, aqui mostra-se a quem carrega
/// e espera.
///
/// Existe porque este jogo se joga num telemóvel e o Godot só mostra `TooltipText` com um rato parado em
/// cima. São perto de duzentos sítios com dica escrita — o que uma condecoração exige, porque é que um
/// general está cansado, o que uma lei custa, porque é que um botão de diplomacia está barrado — e num
/// telemóvel nenhuma delas aparecia. Não estavam escondidas: estavam escritas e inalcançáveis, que é pior,
/// porque o código diz que a informação lá está.
///
/// Apanha o toque no `_Input`, antes da interface, e não come nada: quem carrega e larga depressa continua
/// a carregar no botão que está por baixo. Só quando a dica chega a aparecer é que o largar é engolido —
/// senão carregar e esperar sobre "Justificar guerra" mostrava a dica E declarava a intenção.
///
/// O alvo é o controlo com dica mais fundo debaixo do dedo, entre os que estão à vista: um painel fechado
/// não responde por cima do que está aberto. Como só olha para a árvore do Hud, o mapa fica de fora — e o
/// toque demorado no mapa continua a ser o que já era, escolher tropas.
///
/// Com rato, o botão direito faz o mesmo. Não é para o jogo: é para se poder ver isto sem telemóvel.</summary>
public partial class Hint : Control
{
    /// <summary>Quanto tempo o dedo tem de ficar parado, e quanto pode escorregar sem cancelar. 0.4 s é o
    /// que separa um toque de uma demora; 18 px é o que um dedo mexe sem querer numa chapa de vidro.</summary>
    private const float Delay = 0.4f, Slip = 18f, Life = 6f;

    private PanelContainer _plate = null!;
    private Label _lbl = null!;
    private Vector2 _at, _down;
    private float _held, _shown;
    private bool _tracking, _fired;

    public override void _Ready()
    {
        Name = "Hint";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _plate = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _plate.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.97f }, 10));
        _lbl = Ui.Lbl("", 16);
        _lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _lbl.AddThemeColorOverride("font_color", Ui.Text);
        _plate.AddChild(_lbl);
        AddChild(_plate);
    }

    public override void _Input(InputEvent e)
    {
        switch (e)
        {
            case InputEventScreenTouch { Pressed: true } t:
                Close();
                _down = t.Position; _held = 0f; _tracking = true; _fired = false;
                break;
            case InputEventScreenTouch { Pressed: false }:
                // Engolir o largar só quando a dica chegou a aparecer: um botão do Godot dispara ao largar,
                // por isso é aqui que se decide se a demora foi uma pergunta ou um carregar.
                if (_fired) GetViewport().SetInputAsHandled();
                _tracking = false; _fired = false;
                break;
            case InputEventScreenDrag d when _tracking && d.Position.DistanceTo(_down) > Slip:
                _tracking = false;
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } m:
                Open(m.Position);
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (_tracking && !_fired)
        {
            _held += (float)delta;
            if (_held >= Delay) { _fired = Open(_down); _tracking = false; }
        }
        if (!_plate.Visible) return;
        Place();
        _shown += (float)delta;
        if (_shown >= Life) Close();
    }

    /// <summary>Abre a dica de quem estiver debaixo do ponto. Devolve false quando não há nada a dizer ali —
    /// e nesse caso o toque segue o seu caminho como se nada fosse.</summary>
    public bool Open(Vector2 at, bool blind = false)
    {
        if (GetParent() is not Node root || Pick(root, at, blind) is not Control target) return false;
        _lbl.Text = target.TooltipText;
        _lbl.CustomMinimumSize = new Vector2(Math.Min(GetViewportRect().Size.X * 0.72f, 520f), 0);
        _at = at;
        _shown = 0f;
        _plate.Visible = true;
        Place();
        return true;
    }

    public void Close() { _plate.Visible = false; _shown = 0f; }

    /// <summary>Por cima do dedo, que o dedo tapa o que está por baixo dele, e dentro do ecrã.</summary>
    private void Place()
    {
        var vp = GetViewportRect().Size;
        var s = _plate.Size;
        float x = Math.Clamp(_at.X - s.X / 2f, 8f, Math.Max(8f, vp.X - s.X - 8f));
        float y = _at.Y - s.Y - 28f;
        if (y < 8f) y = Math.Min(_at.Y + 36f, Math.Max(8f, vp.Y - s.Y - 8f));
        _plate.Position = new Vector2(x, y);
    }

    /// <summary>O controlo com dica mais fundo debaixo do ponto, e o último a ser desenhado quando há empate:
    /// é o que o dedo carrega.
    ///
    /// A regra de descida é a do Godot e não a intuitiva. Um filho pode desenhar-se fora do rectângulo do pai
    /// — uma etiqueta que transborda, uma linha de um contentor ainda por arrumar — e por isso não se pára a
    /// busca em quem não contém o ponto. Só se pára em quem recorta o que tem dentro (`ClipContents`), que é
    /// a única situação em que o de dentro deixa mesmo de aparecer. Quem está escondido não conta, e é isso
    /// que impede um painel fechado de responder por cima do que está aberto.</summary>
    private static Control? Pick(Node root, Vector2 at, bool blind)
    {
        Control? best = null;
        if (root is Control head && head.TooltipText.Length > 0 && head.GetGlobalRect().HasPoint(at)) best = head;
        Walk(root);
        return best;

        void Walk(Node n)
        {
            foreach (var child in n.GetChildren())
            {
                if (child is Hint) continue;
                if (child is Control c)
                {
                    if (!blind && !c.IsVisibleInTree()) continue;
                    bool inside = c.GetGlobalRect().HasPoint(at);
                    if (inside && c.TooltipText.Length > 0) best = c;
                    if (!inside && c.ClipContents) continue;
                }
                Walk(child);
            }
        }
    }

    /// <summary>O que o --smoke diz das dicas: quantas estão escritas, quantas estão à vista neste momento, e
    /// de entre essas quantas o dedo apanha mesmo — carregando no meio de cada uma e vendo se é a própria que
    /// responde.
    ///
    /// A terceira conta é a que interessa. A primeira diz só que alguém escreveu a dica, e foi por acreditar
    /// nesse género de conta que o 0.3.15 foi para o telemóvel com uma grelha por cima do mundo. Uma dica
    /// tapada por outra que fique por cima dela não aparece a ninguém, e só uma busca a sério dá por isso.
    ///
    /// Só se mede o que está à vista, e é de propósito. Um painel fechado nunca foi arrumado: os filhos dele
    /// estão todos empilhados no mesmo pixel, tapam-se uns aos outros e uma busca às cegas acusava 12 de 192
    /// — media a falta de layout e não o dedo. Aqui mede-se o ecrã tal como ele está, e por isso a conta do
    /// meio importa: se um dia "à vista" descer sem razão, é porque alguma coisa deixou de ser desenhada.</summary>
    public string Smoke()
    {
        // Arrumar antes de medir: o Godot só põe os filhos de um contentor no sítio no fim do quadro, e o
        // smoke mede no mesmo instante em que a faixa de alarmes acabou de nascer — sem isto os cinco avisos
        // estão todos empilhados no mesmo pixel e a conta acusa uma sombra que o jogador nunca vê.
        GetParent().PropagateNotification((int)Container.NotificationSortChildren);

        var tips = new List<Control>();
        Collect(GetParent(), tips);
        int shown = 0, caught = 0;
        Control? first = null;
        foreach (var c in tips)
        {
            var r = c.GetGlobalRect();
            if (!c.IsVisibleInTree() || r.Size.X < 1f || r.Size.Y < 1f) continue;
            shown++;
            // o exemplo é a dica mais comprida à vista: é a que prova que o papelinho quebra linha
            if (first is null || c.TooltipText.Length > first.TooltipText.Length) first = c;
            if (Pick(GetParent(), r.GetCenter(), blind: false) == c) caught++;
        }
        int letters = 0;
        if (first is not null && Open(first.GetGlobalRect().GetCenter())) { letters = _lbl.Text.Length; Close(); }
        return $"dicas ao toque: {tips.Count} escritas, {shown} à vista, {caught} que o dedo apanha"
             + (letters == 0 ? ", sem exemplo" : $" (exemplo de {letters} letras)");
    }

    private static void Collect(Node root, List<Control> into)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Hint) continue;
            if (child is Control { TooltipText.Length: > 0 } c) into.Add(c);
            Collect(child, into);
        }
    }
}
