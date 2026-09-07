using Godot;
using Timer = Godot.Timer;

namespace WarGame.Presentation;

/// <summary>O klaxon de derrota — o aviso de batalha perdida do HoI4, com sirene. Perder uma batalha era
/// uma linha de toast igual à de uma fábrica acabada: quem estivesse a olhar para a fila de produção não
/// dava por nada, e a terceira derrota seguida passava tão calada como a primeira.
///
/// Agora o ecrã inteiro responde: as quatro arestas acendem-se a vermelho e apagam-se como uma luz de
/// alarme, um cartaz cai no meio do ecrã a dizer onde foi e quantas derrotas seguidas já vão, e — só
/// quando o país entra em alarme (DefeatAlarmSystem) — toca um klaxon de duas notas, gerado aqui em
/// código, que é o primeiro som que este jogo teve.
///
/// Não lê o World nem despacha nada: recebe o texto já feito de quem ouve o barramento.</summary>
public partial class DefeatKlaxon : Control
{
    private Control _glow = null!;             // as quatro bandas das arestas, que acendem e apagam juntas
    private PanelContainer _banner = null!;
    private Label _title = null!, _where = null!, _note = null!;
    private AudioStreamPlayer _horn = null!;
    private Timer _hold = null!;
    private Tween? _tween;
    private StyleBoxFlat _plate = null!;

    /// <summary>O último aviso que foi ao ecrã (para o --smoke ter o que contar).</summary>
    public string Last { get; private set; } = "nenhum";
    /// <summary>A sirene chegou a tocar no último aviso.</summary>
    public bool LastHorn { get; private set; }

    public void Setup()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;   // o dedo passa por aqui direito ao mapa

        _glow = new Control { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        _glow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_glow);
        // duas camadas por aresta: a de fora mais carregada, a de dentro a esbater-se para o meio do ecrã
        foreach (var preset in new[] { LayoutPreset.TopWide, LayoutPreset.BottomWide, LayoutPreset.LeftWide, LayoutPreset.RightWide })
        {
            _glow.AddChild(Band(preset, 96f, 0.20f));
            _glow.AddChild(Band(preset, 40f, 0.45f));
        }

        var centre = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        centre.OffsetBottom = -200;             // um pouco acima do meio, longe dos painéis de baixo
        AddChild(centre);

        _banner = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _plate = Ui.Box(Ui.Ink with { A = 0.95f }, 12);
        _plate.BorderColor = Ui.Danger; _plate.SetBorderWidthAll(3);
        _banner.AddThemeStyleboxOverride("panel", _plate);
        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 4);
        _title = Ui.Lbl("", 30); _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.AddThemeColorOverride("font_color", Ui.Danger);
        _where = Ui.Lbl("", 20); _where.HorizontalAlignment = HorizontalAlignment.Center;
        _note = Ui.Lbl("", 16); _note.HorizontalAlignment = HorizontalAlignment.Center;
        _note.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(_title); v.AddChild(_where); v.AddChild(_note);
        _banner.AddChild(v); centre.AddChild(_banner);

        _horn = new AudioStreamPlayer { Stream = Horn(), VolumeDb = -6f };
        AddChild(_horn);
        _hold = new Timer { WaitTime = 3.4, OneShot = true }; AddChild(_hold);
        _hold.Timeout += Fade;
    }

    /// <summary>Uma banda de aresta: quanto mais grossa, mais apagada — juntas fazem o halo do alarme.</summary>
    private static ColorRect Band(LayoutPreset preset, float thick, float alpha)
    {
        var r = new ColorRect { Color = Ui.Danger with { A = alpha }, MouseFilter = MouseFilterEnum.Ignore };
        r.SetAnchorsAndOffsetsPreset(preset);
        switch (preset)
        {
            case LayoutPreset.TopWide: r.OffsetBottom = thick; break;
            case LayoutPreset.BottomWide: r.OffsetTop = -thick; break;
            case LayoutPreset.LeftWide: r.OffsetRight = thick; break;
            default: r.OffsetLeft = -thick; break;
        }
        return r;
    }

    /// <summary>Levanta o alarme. <paramref name="alarm"/> é a série de derrotas já em cima da regra: só aí
    /// toca a sirene e o cartaz fica em letra cheia de vermelho.</summary>
    public void Raise(string place, int streak, bool groundLost, bool alarm)
    {
        _title.Text = alarm ? "☠  BATALHA PERDIDA" : "batalha perdida";
        _title.AddThemeFontSizeOverride("font_size", alarm ? 30 : 22);
        _title.AddThemeColorOverride("font_color", alarm ? Ui.Danger : Ui.Danger.Darkened(0.15f));
        _where.Text = place;
        _note.Text = (streak <= 1 ? "" : $"{streak}.ª derrota seguida · ")
                   + (groundLost ? "perdemos o terreno" : "o assalto foi travado");
        _plate.BorderColor = alarm ? Ui.Danger : Ui.Danger.Darkened(0.35f);

        Last = $"{_title.Text.Trim()} em {place}" + (_note.Text.Length > 0 ? $" ({_note.Text})" : "");
        LastHorn = alarm;
        if (alarm && _horn.Stream is not null) _horn.Play();

        _tween?.Kill();
        _banner.Visible = true;
        _banner.Modulate = new Color(1, 1, 1, 0);
        _banner.Position = new Vector2(_banner.Position.X, -22);
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(_banner, "modulate:a", 1f, 0.16);
        _tween.TweenProperty(_banner, "position:y", 0f, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        // o halo pisca duas vezes quando é alarme, uma só quando é um empurrão
        var pulse = CreateTween();
        for (int i = 0; i < (alarm ? 2 : 1); i++)
        {
            pulse.TweenProperty(_glow, "modulate:a", alarm ? 1f : 0.55f, 0.12);
            pulse.TweenProperty(_glow, "modulate:a", 0f, 0.34);
        }
        _hold.Start();
    }

    /// <summary>Saída do cartaz: desvanece em vez de desaparecer de repente.</summary>
    private void Fade()
    {
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(_banner, "modulate:a", 0f, 0.3);
        _tween.TweenCallback(Callable.From(() => _banner.Visible = false));
    }

    /// <summary>A sirene: duas notas geradas em código (sem ficheiro de som nenhum no jogo), harmónicas
    /// somadas para soar a buzina de fábrica e não a apito de computador.</summary>
    private static AudioStreamWav Horn()
    {
        const int rate = 22050;
        const float each = 0.45f;
        float[] notes = { 622f, 466f };
        int n = (int)(rate * each);
        var pcm = new byte[notes.Length * n * 2];
        int k = 0;
        foreach (float f in notes)
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = MathF.Min(1f, t / 0.02f) * MathF.Min(1f, (each - t) / 0.08f);
                float s = MathF.Sin(Mathf.Tau * f * t)
                        + 0.45f * MathF.Sin(Mathf.Tau * 2f * f * t)
                        + 0.25f * MathF.Sin(Mathf.Tau * 3f * f * t);
                short v = (short)Math.Clamp(s * env * 7000f, short.MinValue, short.MaxValue);
                pcm[k++] = (byte)(v & 0xFF);
                pcm[k++] = (byte)((v >> 8) & 0xFF);
            }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Stereo = false,
            Data = pcm,
        };
    }

    /// <summary>--smoke: levanta o alarme sem batalha nenhuma e diz o que foi ao ecrã.</summary>
    public string Smoke(string place, int streak, bool groundLost, bool alarm)
    {
        Raise(place, streak, groundLost, alarm);
        var wav = (AudioStreamWav)_horn.Stream;
        return $"{Last}, sirene de {wav.Data.Length / 2f / wav.MixRate:0.0}s {(LastHorn ? "a tocar" : "calada")}";
    }
}
