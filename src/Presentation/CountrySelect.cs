using System.Globalization;
using System.Text;
using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Escolher com quem falar: a lista de todos os países do mundo, com procura e filtros, que abre a
/// mesa de diplomacia de quem se tocar.
///
/// Existe porque não havia porta nenhuma para a maior parte do mundo. A diplomacia toda já estava feita —
/// justificar guerra, pactos, facções, mercado, material, adidos, espionagem — mas só se chegava a um país
/// por duas vias: tocar-lhe no mapa (e o toque no mapa é para escolher tropas) ou encontrá-lo nas quinze
/// linhas da tabela de potências. São 247 países: 232 deles não tinham como ser abertos. Um jogo de grande
/// estratégia sem lista de países é um jogo em que a diplomacia existe e não se alcança.
///
/// A procura ignora acentos e maiúsculas e aceita a sigla (PRT, BRA) tal como o nome — é como se procura
/// um país num mapa, pelas três letras que estão escritas nele. Os filtros são para quando não se sabe o
/// nome: quem faz fronteira connosco, quem nos faz guerra, quem está nas nossas facções.
///
/// Só lê o World e abre outro painel: não muta nada e não despacha comandos.</summary>
public partial class CountrySelect : PanelContainer
{
    /// <summary>Filtros, um por aba de metal. "Vizinhos" conta a fronteira de mar curta, senão uma ilha não
    /// tinha vizinho nenhum e o filtro nascia vazio para quem joga o Reino Unido ou o Japão.</summary>
    private static readonly string[] Filters = { "Todos", "Vizinhos", "Em guerra", "Facção" };

    /// <summary>Linhas desenhadas de uma vez. Não é preguiça: 247 fichas com bandeira de uma assentada
    /// enchem o painel de coisa que ninguém lê e travam o telemóvel. Quem procura escreve.</summary>
    private const int Shown = 40;

    private Game _game = null!;
    private CountryPanel _countryPanel = null!;
    private LineEdit _search = null!;
    private HBoxContainer _tabs = null!;
    private HBoxContainer _crest = null!;
    private VBoxContainer _body = null!;
    private int _tab;
    private string _lastKey = "";

    public void Setup(Game game, CountryPanel countryPanel)
    {
        _game = game; _countryPanel = countryPanel;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 1f)));

        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        _crest = new HBoxContainer(); head.AddChild(Ui.Grow(_crest));
        head.AddChild(Ui.Btn("Fechar", Close));

        _search = new LineEdit
        {
            PlaceholderText = "Procurar país ou sigla…",
            CustomMinimumSize = new Vector2(0, 48),
            ClearButtonEnabled = true,
        };
        _search.AddThemeFontSizeOverride("font_size", 20);
        _search.TextChanged += _ => { _lastKey = ""; _game.RunWhenIdle(Fill); };
        v.AddChild(_search);

        _tabs = new HBoxContainer(); v.AddChild(_tabs);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Open()
    {
        _lastKey = "";
        _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); });
    }

    public void Close() => Visible = false;

    private void Pick(int i) { _tab = i; _lastKey = ""; _game.RunWhenIdle(Fill); }

    /// <summary>Sem acentos e em minúsculas: escrever "sao tome" tem de encontrar "São Tomé". Quem procura
    /// um país no telemóvel não vai buscar o til ao teclado.</summary>
    private static string Plain(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (char ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Países que fazem fronteira com o nosso, por terra ou por uma costa em frente (a mesma regra
    /// que o nevoeiro usa para decidir o que as patrulhas vêem).</summary>
    private static HashSet<int> Neighbours(World w, int pid)
    {
        var found = new HashSet<int>();
        float reach = w.Rule("vision_sea_km", 250f);
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId != pid) continue;
            foreach (int n in r.Neighbours)
                if (w.Regions.TryGetValue(n, out var o) && o.ControllerId != pid) found.Add(o.ControllerId);
            foreach (var (n, km) in r.SeaNeighbours)
                if (km <= reach && w.Regions.TryGetValue(n, out var o) && o.ControllerId != pid) found.Add(o.ControllerId);
        }
        found.Remove(0);
        return found;
    }

    /// <summary>Como estamos com ele, numa palavra e num símbolo. É o que se quer saber antes de abrir a
    /// ficha: a lista serve para escolher com quem falar, não para ler estatística.</summary>
    private static (string Text, Color Tint) Relation(World w, int pid, int id)
    {
        if (id == pid) return ("nós", Ui.Accent);
        if (w.AreAtWar(pid, id)) return ("⚔ em guerra", Ui.Danger);
        if (w.SameFaction(pid, id)) return ("🤝 aliado", Ui.Good);
        if (w.HasPact(pid, id)) return ("📜 não-agressão", Ui.Text);
        return ("neutro", Ui.TextDim);
    }

    private void Fill()
    {
        try
        {
            var w = _game.World;
            string q = Plain(_search.Text.Trim());
            var key = $"{_tab}|{q}|{w.Clock.Day}|{w.Countries.Count}|{w.Wars.Count}|{w.Factions.Count}";
            if (key == _lastKey) return;
            _lastKey = key;

            int pid = _game.PlayerId ?? 0;
            Ui.CrestInto(_crest, w.Countries.TryGetValue(pid, out var mine) ? mine.Tag : "",
                "Países", $"{w.Countries.Count} no mundo — escolhe com quem falar");
            Ui.Clear(_tabs);
            _tabs.AddChild(Ui.Tabs(Filters, _tab, Pick));
            Ui.Clear(_body);

            var divs = new Dictionary<int, int>();
            foreach (var d in w.Divisions.Values) divs[d.CountryId] = divs.GetValueOrDefault(d.CountryId) + 1;
            var regions = new Dictionary<int, int>();
            foreach (var r in w.Regions.Values) regions[r.ControllerId] = regions.GetValueOrDefault(r.ControllerId) + 1;

            var near = _tab == 1 && pid > 0 ? Neighbours(w, pid) : null;
            var rank = new Dictionary<int, int>();
            var standings = PowerIndex.Rankings(w);
            for (int i = 0; i < standings.Count; i++) rank[standings[i].CountryId] = i;

            var list = new List<Country>();
            foreach (var c in w.Countries.Values)
            {
                if (c.Id == pid) continue;                       // a nossa ficha abre-se na barra, em "País"
                if (q.Length > 0 && !Plain(c.Name).Contains(q) && !Plain(c.Tag).Contains(q)) continue;
                if (near is not null && !near.Contains(c.Id)) continue;
                if (_tab == 2 && !(pid > 0 && w.AreAtWar(pid, c.Id))) continue;
                if (_tab == 3 && !(pid > 0 && w.SameFaction(pid, c.Id))) continue;
                list.Add(c);
            }
            // Pela ordem da tabela de potências: quem procura um nome escreve-o, quem não sabe o nome quer
            // ver primeiro quem pesa no mundo. Sem nota fica no fim, por ordem alfabética.
            list.Sort((a, b) =>
            {
                int ra = rank.GetValueOrDefault(a.Id, int.MaxValue), rb = rank.GetValueOrDefault(b.Id, int.MaxValue);
                return ra != rb ? ra.CompareTo(rb) : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });

            if (list.Count == 0)
            {
                var none = Ui.Lbl(q.Length > 0 ? $"Nenhum país dá \"{_search.Text.Trim()}\"" : "Nenhum país neste filtro", 18);
                none.AddThemeColorOverride("font_color", Ui.TextDim);
                _body.AddChild(none);
                return;
            }

            foreach (var c in list.Take(Shown)) _body.AddChild(Row(w, pid, c, divs, regions));
            if (list.Count > Shown)
            {
                var more = Ui.Lbl($"… e mais {list.Count - Shown} — escreve o nome ou a sigla para os encontrar", 15);
                more.AddThemeColorOverride("font_color", Ui.TextDim);
                _body.AddChild(more);
            }
        }
        catch (Exception ex) { GD.PushError("CountrySelect.Fill: " + ex); }
    }

    /// <summary>Ficha de um país na lista: bandeira, nome com a sigla, como estamos com ele e o peso que
    /// tem. Toda a linha é o botão — no telemóvel um alvo de linha inteira acerta-se sempre.</summary>
    private Control Row(World w, int pid, Country c, Dictionary<int, int> divs, Dictionary<int, int> regions)
    {
        var plate = new PanelContainer();
        plate.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface.Darkened(0.15f), 6));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); plate.AddChild(row);

        var flag = Flags.Rect(26);
        flag.Texture = Flags.Of(c.Tag);
        if (flag.Texture is null)
        {
            var tag = Ui.Lbl(c.Tag, 16);
            tag.AddThemeColorOverride("font_color", Ui.Accent);
            tag.HorizontalAlignment = HorizontalAlignment.Center;
            tag.CustomMinimumSize = new Vector2(39, 26);
            row.AddChild(tag);
        }
        else row.AddChild(flag);

        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 0);
        var name = Ui.Lbl($"{c.Name} ({c.Tag})", 18);
        name.AddThemeColorOverride("font_color", Ui.Text);
        stack.AddChild(name);
        stack.AddChild(Ui.Lbl($"{divs.GetValueOrDefault(c.Id)} divisões · {regions.GetValueOrDefault(c.Id)} regiões", 13));
        row.AddChild(Ui.Grow(stack));

        var (text, tint) = Relation(w, pid, c.Id);
        var rel = Ui.Lbl(text, 15);
        rel.AddThemeColorOverride("font_color", tint);
        row.AddChild(rel);

        int id = c.Id;
        return Ui.Click(plate, () => { Close(); _countryPanel.Open(id, CountryPanel.Diplomacy); },
            $"Abrir a mesa de diplomacia com {c.Name}");
    }

    /// <summary>Só para o --smoke: percorre os filtros todos e uma procura, para nenhum caminho da lista
    /// passar sem ser desenhado. Uma lista que rebenta num filtro não dá erro nenhum — fica vazia.</summary>
    public string Smoke()
    {
        Visible = true;
        var counts = new List<string>();
        for (int i = 0; i < Filters.Length; i++)
        {
            _tab = i; _lastKey = ""; Fill();
            counts.Add($"{Filters[i]}:{_body.GetChildren().OfType<PanelContainer>().Count()}");
        }
        _tab = 0; _search.Text = "por"; _lastKey = ""; Fill();
        int found = _body.GetChildren().OfType<PanelContainer>().Count();
        _search.Text = ""; _lastKey = ""; Fill();
        Visible = false;
        return $"escolha de países: {_game.World.Countries.Count} no mundo ({string.Join(", ", counts)}), \"por\" dá {found}";
    }
}
