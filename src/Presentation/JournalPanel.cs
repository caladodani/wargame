using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Painel "Crónica": a linha do tempo da campanha. Duas memórias no mesmo carril — a crónica que vai
/// no save (World.Chronicle, escrita pelo ChronicleSystem: guerras, capitais tomadas, capitulações, pazes,
/// bombas) e os boletins da sessão que passaram pelos toasts do Hud. Antes só havia os segundos, e fechar o
/// jogo apagava a história toda.
///
/// Desenha-se como uma linha do tempo a sério: carril vertical, um ponto por acontecimento com a cor do peso,
/// o ano a separar os capítulos e chapas para filtrar por género.</summary>
public partial class JournalPanel : PanelContainer
{
    private const int Max = 200;
    private Game _game = null!;
    private VBoxContainer _body = null!;
    private HBoxContainer _filters = null!;
    private readonly List<(int Day, string Text)> _entries = new();
    private string _filter = "";        // "" = tudo; senão, id do género
    private string _key = "";
    private HBoxContainer _crest = null!;

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 1f)));
        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        _crest = new HBoxContainer(); head.AddChild(Ui.Grow(_crest));   // brasão do nosso país, enchido no Fill
        head.AddChild(Ui.Btn("Fechar", Close));
        var filterScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(0, 44),
        };
        v.AddChild(filterScroll);
        _filters = new HBoxContainer(); _filters.AddThemeConstantOverride("separation", 6); filterScroll.AddChild(_filters);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Add(string text)
    {
        _entries.Add((_game.World.Clock.Day, text));
        if (_entries.Count > Max) _entries.RemoveAt(0);
        if (Visible) Fill();
    }

    public void Open() { Fill(); Visible = true; Ui.FadeIn(this); }
    public void Close() => Visible = false;

    /// <summary>--smoke: enche a crónica sem esperar e devolve quantas linhas ficaram no carril.</summary>
    public int Smoke()
    {
        Visible = true; _key = ""; Fill();
        string first = _game.World.Chronicle.Select(e => e.Kind).FirstOrDefault() ?? "";
        if (first.Length > 0) { SetFilter(first); SetFilter(""); }   // filtrar e voltar a tudo
        return _body.GetChildCount();
    }

    private void SetFilter(string kind) { _filter = _filter == kind ? "" : kind; _key = ""; Fill(); }

    /// <summary>Cor do acontecimento pelo peso do género: o que faz história salta à vista.</summary>
    private static Color Tone(int weight) => weight switch
    {
        >= 3 => new Color(0.95f, 0.55f, 0.45f),
        2 => new Color(0.55f, 0.75f, 0.95f),
        _ => new Color(0.65f, 0.70f, 0.78f),
    };

    private void Fill()
    {
        var w = _game.World;
        string key = $"{w.Chronicle.Count}|{_entries.Count}|{_filter}";
        if (key == _key) return;
        _key = key;

        Ui.CrestInto(_crest, _game.PlayerId is int me && w.Countries.TryGetValue(me, out var mc) ? mc.Tag : "",
            "Crónica", $"{w.Chronicle.Count} entradas · dia {w.Clock.Day}");
        FillFilters(w);
        Ui.Clear(_body);

        // As duas memórias no mesmo carril, do mais recente para o mais antigo.
        IEnumerable<(int Day, string Kind, string Text)> chronicled = w.Chronicle
            .Where(e => _filter.Length == 0 || e.Kind == _filter)
            .Select(e => (e.Day, e.Kind, e.Text));
        IEnumerable<(int Day, string Kind, string Text)> bulletins = _filter.Length == 0
            ? _entries.Select(e => (e.Day, Kind: "", e.Text))
            : Enumerable.Empty<(int Day, string Kind, string Text)>();
        var rows = chronicled.Concat(bulletins).OrderByDescending(e => e.Day).ToList();

        if (rows.Count == 0)
        {
            _body.AddChild(Ui.Lbl(_filter.Length == 0 ? "A campanha ainda não tem história." : "Nada deste género até hoje.", 18));
            return;
        }

        var clock = w.Clock;
        int lastYear = int.MinValue;
        foreach (var (day, kind, text) in rows)
        {
            var date = clock.Date.AddDays(day - clock.Day);
            if (date.Year != lastYear)
            {
                lastYear = date.Year;
                var year = Ui.Lbl($"— {lastYear} —", 20);
                year.HorizontalAlignment = HorizontalAlignment.Center;
                year.AddThemeColorOverride("font_color", Ui.TextDim);
                _body.AddChild(year);
            }

            var def = kind.Length > 0 ? w.ChronicleKinds.GetValueOrDefault(kind) : null;
            var tone = def is null ? Tone(0) : Tone(def.Weight);

            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            var dot = new PanelContainer { CustomMinimumSize = new Vector2(4, 0), SizeFlagsVertical = SizeFlags.Fill };
            dot.AddThemeStyleboxOverride("panel", Ui.Box(tone with { A = def is null ? 0.35f : 0.9f }, 0));
            row.AddChild(dot);                                    // carril: a barra fina que faz a linha do tempo

            // A marca do género: chapa desenhada, na cor do peso. Sem género (boletim da sessão) fica o
            // ponto de texto — não há chapa para "nada em especial", e inventar uma era mentir.
            Control mark;
            if (def is not null)
            {
                var plate = Glyph.Make(def.Glyph, 18, tone, def.Name);
                plate.CustomMinimumSize = new Vector2(26, 18);
                plate.SizeFlagsVertical = SizeFlags.ShrinkCenter;   // linha alta de texto não estica a chapa
                mark = plate;
            }
            else
            {
                var dotLbl = Ui.Lbl("·", 17);
                dotLbl.CustomMinimumSize = new Vector2(26, 0);
                dotLbl.AddThemeColorOverride("font_color", tone);
                dotLbl.TooltipText = "Boletim da sessão";
                mark = dotLbl;
            }
            row.AddChild(mark);

            var when = Ui.Lbl(date.ToString("dd MMM"), 15);
            when.CustomMinimumSize = new Vector2(80, 0);
            when.AddThemeColorOverride("font_color", Ui.TextDim);
            row.AddChild(when);

            var lbl = Ui.Grow(Ui.Lbl(text, def is not null && def.Weight >= 3 ? 19 : 18));
            lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (def is not null && def.Weight >= 3) lbl.AddThemeColorOverride("font_color", tone);
            row.AddChild(lbl);

            _body.AddChild(row);
        }
    }

    /// <summary>Chapas de filtro: uma por género que a campanha chegou a ver, com a conta ao lado.</summary>
    private void FillFilters(World w)
    {
        Ui.Clear(_filters);
        var counts = w.Chronicle.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
        if (counts.Count == 0) return;

        _filters.AddChild(Chip("Tudo", "", w.Chronicle.Count));
        foreach (var (kind, n) in counts.OrderByDescending(kv => w.ChronicleKinds.GetValueOrDefault(kv.Key)?.Weight ?? 0).ThenBy(kv => kv.Key))
        {
            var def = w.ChronicleKinds.GetValueOrDefault(kind);
            _filters.AddChild(Chip(def?.Name ?? kind, kind, n, def?.Glyph ?? "roda"));
        }
    }

    /// <summary>Chapa de filtro. Com desenho leva um recuo à cabeça do texto do tamanho dele, porque um
    /// Button não arruma filhos e a chapa é ancorada por cima. O "Tudo" não é género nenhum e não leva
    /// desenho: é o botão que tira o filtro, não mais um género da lista.</summary>
    private Button Chip(string text, string kind, int count, string? glyph = null)
    {
        bool on = _filter == kind;
        var b = Ui.Btn(glyph is null ? $"{text} ({count})" : $"      {text} ({count})",
                       () => SetFilter(kind), 0f, on ? Ui.Kind.Primary : Ui.Kind.Normal);
        b.AddThemeFontSizeOverride("font_size", 15);
        b.CustomMinimumSize = new Vector2(0, 36);
        if (glyph is not null) Glyph.Stamp(b, glyph, on ? Ui.Ink : Ui.Accent, 15f);
        return b;
    }
}
