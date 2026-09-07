using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Painel "Mundo" (45% inferior): ranking de países (divisões, regiões, população controlada),
/// guerras activas e facções. Só leitura do World em Fill (mundo parado); tocar num país abre o CountryPanel.</summary>
public partial class WorldPanel : PanelContainer
{
    /// <summary>Secções do painel, uma por aba de metal (as mesmas que antes eram um rolo só).</summary>
    private static readonly string[] Sections = { "Potências", "Guerras", "Espionagem", "Facções" };

    private Game _game = null!;
    private CountryPanel _countryPanel = null!;
    private VBoxContainer _body = null!;
    private HBoxContainer _tabs = null!;
    /// <summary>Aba aberta (índice em Sections). Entra na chave do cache: sem isso trocar de aba não redesenha.</summary>
    private int _tab;
    private string _lastKey = "";
    private HBoxContainer _crest = null!;
    /// <summary>Nota do primeiro classificado da última contagem: as barras são todas relativas a ela.</summary>
    private float _best;

    public void Setup(Game game, CountryPanel countryPanel)
    {
        _game = game; _countryPanel = countryPanel;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.55f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 0.95f)));
        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        _crest = new HBoxContainer(); head.AddChild(Ui.Grow(_crest));   // brasão do nosso país, enchido no Fill
        head.AddChild(Ui.Btn("Fechar", Close));
        _tabs = new HBoxContainer(); v.AddChild(_tabs);          // abas de metal, enchidas no Fill
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Open() { _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); }); }

    /// <summary>Troca de secção: limpa a chave para o Fill não achar que já está desenhado.</summary>
    private void Pick(int i) { _tab = i; _lastKey = ""; _game.RunWhenIdle(Fill); }

    /// <summary>Só para o --smoke: percorre as abas todas, para nenhuma secção passar sem ser desenhada.</summary>
    public int SmokeTabs()
    {
        Visible = true;
        for (int i = 0; i < Sections.Length; i++) { _tab = i; _lastKey = ""; Fill(); }
        _tab = 0; _lastKey = ""; Visible = false;
        return Sections.Length;
    }

    /// <summary>Só para o --smoke: enche a tabela mundial sem esperar pelo idle e devolve quantas potências
    /// ficaram desenhadas, para o caminho novo do painel não passar despercebido se rebentar.</summary>
    public int Smoke()
    {
        _lastKey = ""; Visible = true;
        Fill();
        int rows = _body.GetChildCount();
        Visible = false;
        return rows;
    }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            var w = _game.World;
            var key = _tab + "|" + w.Clock.Day + "|" + w.Divisions.Count + "|" + string.Join(",", w.Wars.Keys.Select(k => k.A + ":" + k.B)) + "|" + w.ActiveSpyOps.Count;
            if (key == _lastKey) return;
            _lastKey = key;
            Ui.CrestInto(_crest, _game.PlayerId is int crestId && w.Countries.TryGetValue(crestId, out var mc) ? mc.Tag : "",
                "Mundo", $"{w.Countries.Count} países · {w.Wars.Count} guerras abertas");
            Ui.Clear(_tabs);
            _tabs.AddChild(Ui.Tabs(Sections, _tab, Pick));
            Ui.Clear(_body);
            bool tPower = _tab == 0, tWars = _tab == 1, tSpy = _tab == 2, tFactions = _tab == 3;

            var divs = new Dictionary<int, int>();
            foreach (var d in w.Divisions.Values) divs[d.CountryId] = divs.GetValueOrDefault(d.CountryId) + 1;
            var regions = new Dictionary<int, int>();
            var pop = new Dictionary<int, long>();
            long popTotal = 0;
            foreach (var r in w.Regions.Values)
            {
                regions[r.ControllerId] = regions.GetValueOrDefault(r.ControllerId) + 1;
                pop[r.ControllerId] = pop.GetValueOrDefault(r.ControllerId) + r.Population;
                popTotal += r.Population;
            }

            // Tabela mundial: nota de potência do PowerIndex em vez da contagem de divisões, com as quatro
            // parcelas desenhadas por baixo — quem manda no mundo não é quem tem mais divisões cansadas.
            var standings = PowerIndex.Rankings(w);
            _best = standings.Count > 0 ? standings[0].Score : 1f;
            if (tPower)
            {
                Header("Potências mundiais");
                int shown = 0;
                foreach (var st in standings)
                {
                    if (shown++ >= 15) break;
                    if (!w.Countries.TryGetValue(st.CountryId, out var c)) continue;
                    _body.AddChild(Standing(w, st, shown, divs, regions, pop, popTotal));
                }
                // o jogador vê-se sempre, mesmo lá do fundo da tabela
                int mineIdx = _game.PlayerId is int me ? standings.FindIndex(x => x.CountryId == me) : -1;
                if (mineIdx >= 15) _body.AddChild(Standing(w, standings[mineIdx], mineIdx + 1, divs, regions, pop, popTotal));
            }

            if (tWars) Header("Guerras activas");
            if (tWars && w.Wars.Count == 0) Line("Nenhuma — o mundo está em paz");
            var orgSum = new Dictionary<int, float>();
            foreach (var d in w.Divisions.Values)
                orgSum[d.CountryId] = orgSum.GetValueOrDefault(d.CountryId) + d.Org * d.Hp / 100f;
            foreach (var ((a, bId), info) in tWars ? w.Wars.OrderBy(kv => kv.Value.StartDay) : Enumerable.Empty<KeyValuePair<(int A, int B), WarInfo>>())
            {
                string na = w.Countries.TryGetValue(a, out var ca) ? ca.Name : "#" + a;
                string nb = w.Countries.TryGetValue(bId, out var cb) ? cb.Name : "#" + bId;
                Line($"⚔ {na} vs {nb}   ({w.Clock.Day - info.StartDay} dias)", 17);
                // balança de força: org×HP de cada lado, barra de 10 posições
                float fa = orgSum.GetValueOrDefault(a), fb = orgSum.GetValueOrDefault(bId);
                if (fa + fb > 0f)
                {
                    int seg = (int)MathF.Round(10f * fa / (fa + fb));
                    Line($"   {divs.GetValueOrDefault(a)} div  {new string('█', seg)}{new string('░', 10 - seg)}  {divs.GetValueOrDefault(bId)} div", 15);
                }
            }

            if (tSpy && _game.PlayerId is int pid)
            {
                var mine = w.ActiveSpyOps.Where(o => o.CountryId == pid).ToList();
                var against = w.ActiveSpyOps.Where(o => o.TargetCountryId == pid).ToList();
                var intel = w.Intel.Where(kv => kv.Key.A == pid && kv.Value >= w.Clock.Day).ToList();
                if (mine.Count > 0 || against.Count > 0 || intel.Count > 0)
                {
                    Header("Espionagem");
                    foreach (var o in mine)
                        Line($"🕵 {(w.SpyOps.TryGetValue(o.OpId, out var op) ? op.Name : o.OpId)} contra {Name(w, o.TargetCountryId)} — {(int)MathF.Ceiling(o.DaysLeft)} dias", 16);
                    foreach (var (k, until) in intel)
                        Line($"👁 Intel sobre {Name(w, k.B)} até ao dia {until}", 16);
                    if (against.Count > 0)
                        Line($"⚠ {against.Count} operação(ões) inimiga(s) contra nós em curso", 16);
                }
            }

            if (!tFactions) return;
            Header("Facções");
            foreach (var f in w.Factions.Values.Where(f => f.Members.Count > 0).OrderByDescending(f => f.Members.Count))
            {
                var tags = f.Members.Where(w.Countries.ContainsKey).Select(m => w.Countries[m].Tag).OrderBy(t => t).ToList();
                string list = tags.Count > 14 ? string.Join(", ", tags.Take(14)) + ", …" : string.Join(", ", tags);
                Line($"{f.Name} ({tags.Count}): {list}", 16);
            }
        }
        catch (Exception ex) { GD.PushError("WorldPanel.Fill: " + ex); }
    }

    /// <summary>Cartão de um país na tabela mundial: lugar, seta de subida/descida, bandeira, patamar,
    /// barra da nota e as quatro parcelas que a fazem. O do jogador leva moldura acesa.</summary>
    private Control Standing(World w, PowerIndex.Standing st, int place, Dictionary<int, int> divs,
                             Dictionary<int, int> regions, Dictionary<int, long> pop, long popTotal)
    {
        var c = w.Countries[st.CountryId];
        bool mine = _game.PlayerId == c.Id;
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(mine ? new Color(0.16f, 0.22f, 0.32f, 0.95f) : Ui.Surface with { A = 0.75f }, 8));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); card.AddChild(v);

        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); v.AddChild(top);
        var pos = Ui.Lbl($"{place}.º", 18);
        pos.AddThemeColorOverride("font_color", place <= 3 ? new Color(1f, 0.82f, 0.25f) : Ui.TextDim);
        top.AddChild(pos);
        var arrow = Move(c);
        if (arrow is not null) top.AddChild(arrow);
        var fl = Flags.Rect(22); fl.Texture = Flags.Of(c.Tag); fl.Visible = fl.Texture is not null;
        top.AddChild(fl);
        int id = c.Id;
        var btn = Ui.Btn($"{c.Name}{(c.AtWarWith.Count > 0 ? "  ⚔" : "")}", () => { Close(); _countryPanel.Open(id); }, 0);
        btn.Alignment = HorizontalAlignment.Left;
        top.AddChild(Ui.Grow(btn));
        var tier = Ui.Lbl(PowerIndex.Tier(w, st.Share), 15);
        tier.AddThemeColorOverride("font_color", Ui.TextDim);
        top.AddChild(tier);

        // barra da nota, sempre relativa ao primeiro classificado: dá a distância ao topo de relance
        float best = _best > 0f ? _best : 1f;
        v.AddChild(Ui.Bar(Math.Clamp(st.Score / best, 0f, 1f), mine ? Ui.Accent : Ui.Good, 0f));

        float popShare = popTotal > 0 ? 100f * pop.GetValueOrDefault(c.Id) / popTotal : 0f;
        var note = Ui.Lbl($"nota {st.Score:0.0} · {divs.GetValueOrDefault(c.Id)} div · {regions.GetValueOrDefault(c.Id)} reg · {popShare:0.0}% pop" +
                          $"   ⚒ {st.Industry * 100f:0.0}%  ⚔ {st.Army * 100f:0.0}%  🔬 {st.Tech * 100f:0.0}%", 14);
        note.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(note);
        return card;
    }

    /// <summary>Seta de quem subiu ou desceu desde a última contagem (PowerRankingSystem).</summary>
    private static Label? Move(Country c)
    {
        if (c.PowerRankPrev == 0 || c.PowerRank == 0 || c.PowerRankPrev == c.PowerRank) return null;
        bool up = c.PowerRank < c.PowerRankPrev;
        var l = Ui.Lbl(up ? $"▲{c.PowerRankPrev - c.PowerRank}" : $"▼{c.PowerRank - c.PowerRankPrev}", 15);
        l.AddThemeColorOverride("font_color", up ? Ui.Good : Ui.Danger);
        return l;
    }

    private static string Name(WarGame.Core.Model.World w, int id) => w.Countries.TryGetValue(id, out var c) ? c.Name : "#" + id;

    private void Header(string text) { var l = Ui.Lbl(text, 20); l.Modulate = new Color(1f, 0.85f, 0.4f); _body.AddChild(l); }
    private void Line(string text, int size = 18) => _body.AddChild(Ui.Lbl(text, size));
}
