using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>O cartão de um acontecimento: ecrã inteiro, estampa desenhada, o texto do que se passou e os
/// botões da decisão.
///
/// No HoI4 um acontecimento não é um aviso que passa no canto — é o jogo a parar, o ecrã a escurecer e uma
/// folha a ser posta em cima da mesa com duas ou três saídas, cada uma com o seu preço escrito. É metade
/// do que faz o mundo parecer vivo, e era exactamente o que aqui faltava: os eventos com escolhas abriam
/// num AcceptDialog do sistema, cinzento, com os botões todos iguais e sem dizer o que cada um custava.
///
/// A estampa não é arte importada de lado nenhum: é um desenho do Glyph.cs (o mesmo alfabeto das chapas do
/// mapa) posto em grande sobre um fundo com raios, tingido pelo tom da notícia — dourado no que é bom,
/// vermelho no que é mau, ferro no que ainda não se sabe.
///
/// O relógio pára enquanto o cartão está aberto (só se o evento pedir e se o jogador não tiver desligado a
/// pausa nas preferências) e volta ao andamento em que estava quando o cartão fecha. Cartões que cheguem
/// com um aberto ficam em fila — o mundo não espera pela nossa atenção, mas o ecrã só mostra um de cada
/// vez.</summary>
public partial class EventCard : Control
{
    private Game _game = null!;
    private Action<Sfx.Kind>? _sound;
    private PanelContainer _card = null!, _band = null!;
    private Stamp _stamp = null!;
    private Label _title = null!, _kicker = null!;
    private VBoxContainer _text = null!, _buttons = null!;

    private readonly List<string> _queue = new();
    private string _open = "";
    private int _wasSpeed = -1;

    /// <summary>Evento em cima da mesa ("" = nenhum), e quantos esperam a vez.</summary>
    public string OpenId => _open;
    public int Waiting => _queue.Count;
    /// <summary>O cartão parou o relógio (para a prova headless: no smoke o jogo volta a acelerar sozinho).</summary>
    public bool Held { get; private set; }

    private const float Wide = 460f;

    public void Setup(Game game, Action<Sfx.Kind>? sound = null)
    {
        _game = game;
        _sound = sound;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;                  // o cartão é modal: nada passa para o mapa
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var veil = new ColorRect { Color = Ui.Ink with { A = 0.78f } };
        veil.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centre);

        _card = new PanelContainer { CustomMinimumSize = new Vector2(Wide, 0) };
        centre.AddChild(_card);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 0);
        _card.AddChild(v);

        // faixa de cima: a estampa à esquerda, o título e a data à direita
        _band = new PanelContainer();
        v.AddChild(_band);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        _band.AddChild(head);
        _stamp = new Stamp { CustomMinimumSize = new Vector2(84, 84) };
        head.AddChild(_stamp);
        var heading = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        heading.AddThemeConstantOverride("separation", 2);
        head.AddChild(Ui.Grow(heading));
        _kicker = Ui.Lbl("", 13);
        heading.AddChild(_kicker);
        _title = Ui.Wrapped("", Wide - 84f - 40f, 22);
        heading.AddChild(_title);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        v.AddChild(body);
        _text = new VBoxContainer();
        _text.AddThemeConstantOverride("separation", 6);
        body.AddChild(_text);
        _buttons = new VBoxContainer();
        _buttons.AddThemeConstantOverride("separation", 6);
        body.AddChild(_buttons);
    }

    /// <summary>Põe o acontecimento à frente de quem joga. Com um cartão já aberto, este espera a vez.</summary>
    public void Show(string eventId)
    {
        if (eventId.Length == 0 || eventId == _open || _queue.Contains(eventId)) return;
        if (_open.Length > 0) { _queue.Add(eventId); return; }
        Open(eventId);
    }

    private void Open(string eventId)
    {
        var w = _game.World;
        if (!w.NewsEvents.TryGetValue(eventId, out var n)) { Next(); return; }
        _open = eventId;
        Fill(w, n);
        Visible = true;
        Ui.FadeIn(this, 0.18);
        _sound?.Invoke(n.Tone == "bom" ? Sfx.Kind.Fanfare : n.Tone == "mau" ? Sfx.Kind.Siren : Sfx.Kind.Bell);
        if (OS.HasFeature("mobile")) Input.VibrateHandheld(n.Tone == "mau" ? 60 : 30);

        // parar o relógio: só se o evento o pedir e o jogador não tiver desligado a pausa
        Held = false;
        if (n.Pause && Settings.PauseOnEvent && _wasSpeed < 0)
        {
            _wasSpeed = w.Clock.Speed;
            w.Clock.Speed = 0;
            Held = true;
        }
    }

    private void Fill(World w, NewsEvent n)
    {
        var tint = n.Tone switch { "bom" => Ui.Good, "mau" => Ui.Danger, _ => Ui.Accent };
        _card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface, 0));
        _band.AddThemeStyleboxOverride("panel", Ui.Box(tint.Darkened(0.62f), 12));
        _stamp.Paint(n.Glyph, tint);
        _title.Text = n.Title;
        _title.AddThemeColorOverride("font_color", tint.Lightened(0.45f));
        _kicker.Text = $"Dia {w.Clock.Day} · {Where(w, n)}";
        _kicker.AddThemeColorOverride("font_color", Ui.TextDim);

        Ui.Clear(_text);
        _text.AddChild(Ui.Wrapped(n.Body, Wide - 24f, 15));
        if (w.NewsEffects.TryGetValue(n.Id, out var effs) && effs.Count > 0)
            _text.AddChild(Ui.Wrapped("Já se sente: " + Effects(effs), Wide - 24f, 14));

        Ui.Clear(_buttons);
        var opts = w.NewsOptions.TryGetValue(n.Id, out var list) ? list : new List<NewsOption>();
        if (opts.Count == 0 || w.NewsChoices.ContainsKey(n.Id))
        {
            _buttons.AddChild(Ui.Btn("Entendido", () => Close(null), 0, Ui.Kind.Primary));
            return;
        }
        foreach (var o in opts)
        {
            var line = new VBoxContainer();
            line.AddThemeConstantOverride("separation", 0);
            var opt = o;                                    // a captura tem de ser da volta desta iteração
            line.AddChild(Ui.Btn(o.Title, () => Close(opt.Id), 0, Ui.Kind.Primary));
            if (w.NewsOptionEffects.TryGetValue(o.Id, out var oeffs) && oeffs.Count > 0)
            {
                var note = Ui.Wrapped(Effects(oeffs), Wide - 24f, 13);
                note.AddThemeColorOverride("font_color", Ui.TextDim);
                line.AddChild(note);
            }
            _buttons.AddChild(line);
        }
    }

    /// <summary>Fecha o cartão. Com escolha por fazer, escolhe-se o que se carregou (ou a primeira, que é
    /// o que a IA faria) — um evento não fica em cima da mesa para sempre.</summary>
    public void Close(string? optionId)
    {
        var w = _game.World;
        string id = _open;
        _open = "";
        Visible = false;
        if (_wasSpeed >= 0)
        {
            if (w.Clock.Speed == 0) w.Clock.Speed = _wasSpeed;    // se o jogador mexeu no andamento, manda ele
            _wasSpeed = -1;
        }
        Held = false;
        if (id.Length > 0 && optionId is not null && !w.NewsChoices.ContainsKey(id))
            _game.RunWhenIdle(() =>
            {
                if (_game.PlayerId is not int pid) return;
                if (_game.Dispatch(new ChooseNewsOptionCommand(pid, id, optionId)) is string err) _game.Notify(err);
            });
        Next();
    }

    private void Next()
    {
        if (_queue.Count == 0) return;
        var id = _queue[0];
        _queue.RemoveAt(0);
        Open(id);
    }

    /// <summary>Onde é que a notícia se passou: o país em que o evento caiu, ou o mundo.</summary>
    private static string Where(World w, NewsEvent n)
    {
        int on = w.NewsFired.TryGetValue(n.Id, out var hit) ? hit.CountryId : n.CountryId ?? 0;
        return on != 0 && w.Countries.TryGetValue(on, out var c) ? c.Name : "mundo";
    }

    /// <summary>Os efeitos em português e em percentagem: "indústria +8%, investigação −5%".</summary>
    private static string Effects(IEnumerable<(string Key, float Mul)> effs) =>
        string.Join(", ", effs.Select(e =>
            $"{Ui.StatName(e.Key)} {(e.Mul >= 1f ? "+" : "−")}{MathF.Abs(e.Mul - 1f) * 100f:0.#}%"));

    /// <summary>A estampa: o desenho do Glyph em grande, sobre raios que saem do meio. É o lugar da
    /// gravura do HoI4 — sem lhe copiar um pixel, porque isto desenha-se aqui.</summary>
    private partial class Stamp : Control
    {
        private string _glyph = "";
        private Color _tint = Ui.Accent;

        public void Paint(string glyph, Color tint) { _glyph = glyph; _tint = tint; QueueRedraw(); }

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            var mid = r.Size / 2f;
            float rad = MathF.Min(r.Size.X, r.Size.Y) / 2f;
            DrawCircle(mid, rad, _tint.Darkened(0.72f));
            for (int i = 0; i < 16; i++)                       // raios: a luz da notícia a sair do meio
            {
                float a = Mathf.Tau * i / 16f;
                var to = mid + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rad;
                DrawLine(mid + (to - mid) * 0.42f, to, _tint with { A = 0.16f }, 3f);
            }
            DrawArc(mid, rad - 2f, 0f, Mathf.Tau, 48, _tint with { A = 0.8f }, 2f);
            if (_glyph.Length > 0)
                Glyph.Draw(this, new Rect2(mid - Vector2.One * rad * 0.52f, Vector2.One * rad * 1.04f),
                           _glyph, _tint.Lightened(0.45f), 2.4f);
        }
    }
}
