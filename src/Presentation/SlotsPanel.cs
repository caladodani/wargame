using Godot;

namespace WarGame.Presentation;

/// <summary>Jogos guardados: os três slots como fichas de campanha, e não como três botões numa caixa do
/// sistema. Até aqui isto era um AcceptDialog do Godot com "Slot 2 — dia 214" escrito num botão: a única
/// janela do jogo que parecia um formulário de sistema operativo, mesmo depois de o resto ganhar
/// cantoneiras e rebites. E dizer só o dia não chega para se escolher — a pergunta que se faz à lista de
/// saves é "qual era a minha campanha portuguesa?", não "qual delas ia no dia 214".
///
/// Cada ficha traz a bandeira de quem se joga lá, o dia, a dificuldade e há quanto tempo foi gravada; a do
/// slot em curso leva o selo aceso e não pega. Trocar de slot grava primeiro o que está em cima da mesa
/// (Game.SwitchSlot), e é isso que o rodapé diz antes de o dedo lá chegar.
///
/// Só lê: o Game é que sabe abrir os saves, e a troca é um método dele.</summary>
public partial class SlotsPanel : PanelContainer
{
    private Game _game = null!;
    private ScrollContainer _scroll = null!;
    private HBoxContainer _crest = null!;
    private VBoxContainer _body = null!, _stack = null!;
    private int _cards;

    /// <summary>Largura útil das fichas.</summary>
    private const float Wide = 470f;

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.97f), 16));
        Ui.Scrim(this, Close);

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_scroll);
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(Wide, 0) };
        v.AddThemeConstantOverride("separation", 10);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(v);
        _stack = v;
        _crest = new HBoxContainer(); v.AddChild(_crest);
        _body = new VBoxContainer(); _body.AddThemeConstantOverride("separation", 8); v.AddChild(_body);
    }

    public void Open() { Fill(); Fit(); Visible = true; Ui.FadeIn(this); Ui.SlideIn(this); }

    /// <summary>Três fichas cabem em qualquer ecrã; o rolo é a rede para quando não couberem (telemóvel em pé
    /// com a interface no degrau maior).</summary>
    private void Fit()
    {
        float tall = _stack.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(Wide, MathF.Min(tall, GetViewportRect().Size.Y * 0.86f));
    }
    public void Close() => Visible = false;

    /// <summary>--smoke: abre a lista, conta as fichas desenhadas e volta a fechar.</summary>
    public int Smoke() { Open(); int n = _cards; Close(); return n; }

    private void Fill()
    {
        try
        {
            var w = _game.World;
            string tag = _game.PlayerId is int pid && w.Countries.TryGetValue(pid, out var me) ? me.Tag : "—";
            Ui.CrestInto(_crest, tag, "Jogos guardados", $"{Game.SlotCount} espaços de campanha");
            Ui.Clear(_body);
            _cards = 0;

            for (int i = 1; i <= Game.SlotCount; i++) { Card(_game.SlotOf(i)); _cards++; }

            _body.AddChild(Ui.Rule());
            _body.AddChild(Ui.Wrapped("Ao trocar de slot o jogo em curso é gravado primeiro — não se perde nada.", Wide, 14));
            _body.AddChild(Ui.Btn("Fechar", Close));
        }
        catch (Exception ex) { GD.PushError("SlotsPanel: " + ex); }
    }

    /// <summary>Uma ficha por slot: brasão de quem lá se joga, o estado da campanha e a ordem que se lhe dá.</summary>
    private void Card(Game.SlotInfo s)
    {
        var w = _game.World;
        bool current = s.Slot == _game.Slot;
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(current ? Ui.Accent.Darkened(0.78f) with { A = 0.95f }
                                                              : Ui.Surface with { A = 0.85f }, 10));
        _body.AddChild(card);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); card.AddChild(row);

        // no slot em curso vale o mundo que está a correr, não o que ficou gravado no disco da última vez
        int? day = current ? w.Clock.Day : s.Day;
        int? who = current ? _game.PlayerId : s.PlayerId;
        string? diff = current ? w.Difficulty : s.Difficulty;
        string flag = who is int id && w.Countries.TryGetValue(id, out var c) ? c.Tag : "—";
        string name = who is int id2 && w.Countries.TryGetValue(id2, out var c2) ? c2.Name : "";

        string sub = day is null
            ? "espaço livre — começa aqui uma campanha nova"
            : string.Join("  ·  ", new[]
              {
                  name.Length > 0 ? name : "país por escolher",
                  $"dia {day}",
                  diff is not null && w.DifficultyDefs.TryGetValue(diff, out var def) ? def.Name : null,
                  current ? "em cima da mesa" : Ago(s.SavedAt),
              }.OfType<string>());
        row.AddChild(Ui.Grow(Ui.Crest(flag, $"Slot {s.Slot}", sub, 20)));

        if (current)
        {
            var seal = new PanelContainer();
            var box = Ui.Box(Ui.Accent.Darkened(0.6f), 6); box.BorderColor = Ui.Accent;
            seal.AddThemeStyleboxOverride("panel", box);
            var lbl = Ui.Lbl("EM CURSO", 14);
            lbl.AddThemeColorOverride("font_color", Ui.Accent.Lightened(0.4f));
            seal.AddChild(lbl);
            seal.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(seal);
            return;
        }
        row.AddChild(Ui.Btn(day is null ? "Começar aqui" : "Continuar", () => Switch(s.Slot), 170,
                            day is null ? Ui.Kind.Normal : Ui.Kind.Primary));
    }

    /// <summary>Há quanto tempo o slot foi gravado, em português de relance.</summary>
    private static string Ago(DateTime? at)
    {
        if (at is not DateTime t) return "gravado sem data";
        var d = DateTime.UtcNow - t;
        if (d.TotalMinutes < 2) return "gravado agora mesmo";
        if (d.TotalHours < 1) return $"gravado há {d.TotalMinutes:0} min";
        if (d.TotalDays < 1) return $"gravado há {d.TotalHours:0} h";
        return d.TotalDays < 2 ? "gravado ontem" : $"gravado há {d.TotalDays:0} dias";
    }

    private void Switch(int slot) { Close(); _game.SwitchSlot(slot); }
}
