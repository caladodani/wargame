using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Painel "Guerra": o saldo de cada guerra do jogador (regiões tomadas, batalhas ganhas,
/// divisões perdidas) em barras de comparação lado a lado, a seguir o adido militar destacado na guerra
/// alheia, e por fim o arquivo das guerras já terminadas (World.WarHistory). Só lê o World e despacha
/// comandos — quem conta é o WarStatsSystem, quem paga o adido é o AttacheSystem.</summary>
public partial class WarPanel : PanelContainer
{
    /// <summary>As secções do painel, pela ordem em que aparecem na fila de abas.</summary>
    private static readonly string[] Sections = { "Frentes", "Ar", "Mar", "Adidos", "Material", "Arquivo" };

    private Game _game = null!;
    private VBoxContainer _body = null!;
    private string _lastKey = "";
    private HBoxContainer _crest = null!;
    private HBoxContainer _tabs = null!;
    /// <summary>Secção aberta (índice em Sections). Entra na chave do cache: sem isso trocar de aba não
    /// redesenhava nada.</summary>
    private int _tab;
    /// <summary>Levar o mapa a uma região (o Hud é que sabe mexer na câmara): usado pelo "Ver no mapa" das
    /// cedências, para ninguém assinar terra que não viu.</summary>
    public Action<int>? OnShowRegion;

    /// <summary>Só para o --smoke: desenha a lista de anfitriões do adido mesmo com a nossa guerra a
    /// bloquear a missão, para o caminho do desenho (bandeiras, guerras deles, botão) correr sem ecrã.
    /// Aceita qualquer beligerante, inimigos incluídos: o mundo do smoke tem uma guerra só, a nossa.</summary>
    private bool _smokeHosts;

    /// <summary>Guerra com a mesa de negociação aberta (id do inimigo), e o que lhe estamos a exigir.</summary>
    private int? _deal;
    private readonly HashSet<int> _demand = new();

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
        _tabs = new HBoxContainer(); v.AddChild(_tabs);   // a fila de abas de metal, enchida no Fill
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    /// <summary>Trocar de secção: guarda a aba e manda encher outra vez (o desenho é sempre no idle).</summary>
    private void Pick(int i) { _tab = i; _lastKey = ""; _game.RunWhenIdle(Fill); }

    /// <summary>Índice da aba do material em `Sections`. Quem vem da folha de acções da diplomacia quer o
    /// deslizador do empréstimo, e o índice não se escreve à mão de fora: uma aba nova pelo meio partia-o.</summary>
    // `new` porque o Node já tem um Material (o do desenho) e este é o número da aba: nomes iguais, coisas
    // sem nada a ver. Esconder é o que se quer aqui, e o `new` diz que foi de propósito.
    public new static readonly int Material = Array.IndexOf(Sections, "Material");

    /// <summary>Abre o painel. Sem `tab` fica na aba onde estava.</summary>
    public void Open(int? tab = null)
    {
        _lastKey = ""; _deal = null; _demand.Clear();
        if (tab is int t && t >= 0 && t < Sections.Length) _tab = t;
        _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); });
    }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    /// <summary>Só para o --smoke: abre a mesa de negociação da primeira guerra do jogador, para o
    /// caminho todo (candidatas, balança, botões) ser percorrido sem ninguém tocar no ecrã.</summary>
    public void SmokeDeal()
    {
        if (_game.PlayerId is not int pid) return;
        var war = _game.World.Wars.Values.FirstOrDefault(x => x.Involves(pid));
        if (war is null) return;
        _deal = war.EnemyOf(pid);
        foreach (int id in PeaceTerms.Suggest(_game.World, pid, _deal.Value)) _demand.Add(id);
        _tab = 0;                                    // a mesa vive na aba das frentes
        _lastKey = "";
        Fill();
        _deal = null; _demand.Clear();
    }

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid) { Ui.Clear(_body); _body.AddChild(Ui.Lbl("Escolhe um país primeiro", 18)); return; }
            var mine = w.Wars.Values.Where(x => x.Involves(pid)).OrderBy(x => x.StartDay).ToList();
            var past = w.WarHistory.Where(r => r.Involves(pid)).ToList();
            var key = _tab + "|" + w.Clock.Day + "|" + mine.Count + "|" + past.Count + "|" +
                      string.Join(",", mine.Select(x => string.Join("-", x.Side(pid).Goals.OrderBy(g => g)) + "/" + x.Side(pid).Goals.Count(g => w.Regions.TryGetValue(g, out var gr) && gr.ControllerId == pid))) + "|" +
                      $"deal{_deal}:{string.Join("-", _demand.OrderBy(x => x))}|" +
                      string.Join(",", mine.Select(x => $"p{PrisonerView.HeldBy(w, pid, x.EnemyOf(pid))}/{PrisonerView.HeldBy(w, x.EnemyOf(pid), pid)}")) + "|" +
                      string.Join(",", mine.Select(x => $"t{PrisonerExchange.Evaluate(w, pid, x.EnemyOf(pid)).Accepted}")) + "|" +
                      string.Join(",", w.Offers.Where(o => o.ToId == pid).Select(o => $"o{o.FromId}{o.Kind}:{o.Men}:{o.RegionId}:{o.ExpiresDay}")) + "|" +
                      AttacheKey(w, pid) + "|" + AirKey(w, pid) + "|" + SeaKey(w, pid) + "|" +
                      "ll" + string.Join(",", w.LendLeases.Where(l => l.FromId == pid || l.ToId == pid)
                                               .Select(l => $"{l.FromId}>{l.ToId}:{l.Share:0.00}")) + "|" +
                      "th" + string.Join(",", TheatreSystem.Of(w, pid).Select(t => $"{t.FoeId}:{t.RegionIds.Count}:{t.Divisions}:{t.FoeDivisions}:{t.Holes}:{(int)(t.Progress * 100f)}")) + "|" +
                      string.Join(",", mine.Select(x => $"{x.EnemyOf(pid)}:{x.Side(pid).RegionsTaken}:{x.Enemy(pid).RegionsTaken}:{x.Side(pid).DivisionsLost}:{x.Enemy(pid).DivisionsLost}:{x.Side(pid).BattlesWon}:{x.Enemy(pid).BattlesWon}"));
            if (key == _lastKey) return;
            _lastKey = key;
            Ui.CrestInto(_crest, w.Countries[pid].Tag, "Guerra",
                $"{mine.Count} em curso · {past.Count} no arquivo · {AirMissionSystem.Assigned(w, pid):0.#} asas no ar"
                + $" · {NavalMissionSystem.Assigned(w, pid):0.#} navios no mar");
            Ui.Clear(_tabs);
            _tabs.AddChild(Ui.Tabs(Sections, _tab, Pick));
            Ui.Clear(_body);

            var front = new Dictionary<int, float>();   // força útil (org×HP) por país, para a balança
            var divs = new Dictionary<int, int>();
            foreach (var d in w.Divisions.Values)
            {
                front[d.CountryId] = front.GetValueOrDefault(d.CountryId) + d.Org * d.Hp / 100f;
                divs[d.CountryId] = divs.GetValueOrDefault(d.CountryId) + 1;
            }

            if (_tab == 0) Header(mine.Count == 0 ? "Sem guerras em curso" : "Guerras em curso");
            foreach (var war in _tab == 0 ? mine : new List<WarInfo>())
            {
                int foe = war.EnemyOf(pid);
                var (box, card) = Card();
                var title = new HBoxContainer();
                var fl = Flags.Rect(22);
                if (w.Countries.TryGetValue(foe, out var fc)) { fl.Texture = Flags.Of(fc.Tag); fl.Visible = fl.Texture is not null; }
                title.AddChild(fl);
                title.AddChild(Ui.Grow(Ui.Lbl($"⚔ contra {Name(w, foe)}", 20)));
                title.AddChild(Ui.Lbl($"{w.Clock.Day - war.StartDay} dias", 16));
                card.AddChild(title);

                Compare(card, "Regiões tomadas", war.Side(pid).RegionsTaken, war.Enemy(pid).RegionsTaken);
                Compare(card, "Batalhas ganhas", war.Side(pid).BattlesWon, war.Enemy(pid).BattlesWon);
                Compare(card, "Divisões perdidas", war.Side(pid).DivisionsLost, war.Enemy(pid).DivisionsLost, lowerIsBetter: true);
                Compare(card, "Exército no terreno", front.GetValueOrDefault(pid), front.GetValueOrDefault(foe),
                        left: divs.GetValueOrDefault(pid) + " div", right: divs.GetValueOrDefault(foe) + " div");

                // balança dos campos: uma guerra parada continua a render homens a quem aguenta melhor
                if (PrisonerView.Balance(w, pid, foe) is VBoxContainer pris) card.AddChild(pris);
                // e a mesa da troca: homem por homem sem esperar pela paz, se eles assinarem
                int foeId = foe;
                // a proposta deles primeiro: a iniciativa é do outro lado e não pode ficar escondida
                if (OfferView.Card(w, pid, foeId, o => Answer(pid, o, true), o => Answer(pid, o, false),
                                   OnShowRegion is null ? null : Show) is VBoxContainer post)
                    card.AddChild(post);
                if (PrisonerView.Exchange(w, pid, foeId, () => Swap(pid, foeId)) is VBoxContainer swap) card.AddChild(swap);

                Goals(w, card, war, pid, foe);

                int stale = w.Clock.Day - war.LastProgressDay;
                if (stale > 0) card.AddChild(Ui.Lbl($"Frente parada há {stale} dias", 15));
                Deal(w, card, pid, foe);
                _body.AddChild(box);
            }

            if (_tab == 0) Theatres(w, pid);
            if (_tab == 1) AirWar(w, pid);
            if (_tab == 2) SeaWar(w, pid);
            if (_tab == 3) Attaches(w, pid);
            if (_tab == 4) LendLease(w, pid);

            if (_tab == 5 && past.Count > 0)
            {
                Header("Guerras terminadas");
                foreach (var r in past)
                {
                    var (box, card) = Card();
                    string verdict = r.Winner is null ? "Empate" : r.Winner == pid ? "Vitória" : "Derrota";
                    var colour = r.Winner is null ? Ui.TextDim : r.Winner == pid ? Ui.Good : Ui.Danger;
                    var title = new HBoxContainer();
                    title.AddChild(Ui.Grow(Ui.Lbl($"{Name(w, r.A == pid ? r.B : r.A)} · {r.Days} dias", 19)));
                    var v = Ui.Lbl(verdict, 19); v.AddThemeColorOverride("font_color", colour); title.AddChild(v);
                    card.AddChild(title);
                    card.AddChild(Ui.Lbl($"Regiões {r.Regions(pid)}–{r.Regions(r.A == pid ? r.B : r.A)}   ·   " +
                                         $"Batalhas {r.Battles(pid)}–{r.Battles(r.A == pid ? r.B : r.A)}   ·   " +
                                         $"Divisões perdidas {r.Losses(pid)}", 16));
                    _body.AddChild(box);
                }
            }
            else if (_tab == 5) Header("Arquivo vazio: ainda não acabou guerra nenhuma");
        }
        catch (Exception ex) { GD.PushError("WarPanel.Fill: " + ex); }
    }

    /// <summary>O que muda o desenho da secção do adido: a missão em curso (anfitrião e o que já aprendeu),
    /// quem está em guerra lá fora e o cofre — o botão acende no dia em que dá para pagar a estadia.</summary>
    private string AttacheKey(World w, int pid) =>
        (w.Attaches.TryGetValue(pid, out var a) ? $"a{a.HostId}:{a.Learned:0.0}" : "a-")
        + ":" + string.Join("-", Hosts(w, pid).Select(h => h.Id))
        + ":" + (int)(w.Countries.TryGetValue(pid, out var me) ? me.Money : 0f);

    /// <summary>Anfitriões possíveis: quem se está a bater e nos deixa lá pôr um observador (o Core é que
    /// decide, em World.AttacheBlock — o painel só pergunta).</summary>
    private List<Country> Hosts(World w, int pid) =>
        w.Countries.Values.Where(h => w.AttacheBlock(pid, h.Id) is null
                                   || (_smokeHosts && h.Id != pid && w.AtWar(h.Id)))
            .OrderBy(h => h.Id).ToList();

    /// <summary>O que o painel tem de redesenhar quando o céu muda: as nossas missões, as asas em casa e o
    /// que o inimigo tem por cima delas.</summary>
    private string AirKey(World w, int pid) =>
        $"air{AirMissionSystem.Free(w, pid):0.0}:"
        + string.Join(",", (w.Countries.TryGetValue(pid, out var me) ? me.Planes : new Dictionary<string, float>())
            .OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}{kv.Value:0.#}")) + ":"
        + string.Join("-", w.AirMissions.OrderBy(m => m.RegionId).ThenBy(m => m.CountryId)
            .Select(m => $"{m.CountryId}@{m.RegionId}={m.MissionId}:{m.Wings:0.0}"))
        + ":" + (AirMissionSystem.Front(w, pid)?.ToString() ?? "-");

    /// <summary>Guerra aérea: os esquadrões deixaram de ser um número no cofre e passaram a estar num sítio.
    /// A secção mostra o pool (em casa / no ar / o que custa por dia), as missões destacadas com o céu que
    /// está disputado, e dá as três tarefas para o céu da frente — superioridade, apoio e bombardeamento.
    ///
    /// Vive no painel da Guerra porque é a frente que decide para onde vão os aviões, e é aqui que a frente
    /// está escrita. Sem isto, a mecânica só existia para a IA.</summary>
    private void AirWar(World w, int pid)
    {
        var me = w.Countries[pid];
        float upkeep = w.Rule("air_mission_upkeep", 0.6f);
        float free = AirMissionSystem.Free(w, pid), flying = AirMissionSystem.Assigned(w, pid);
        Header("Guerra aérea");
        var (box, card) = Card();

        card.AddChild(Ui.Lbl($"✈ {me.AirPower:0.#} esquadrões — {free:0.#} em casa, {flying:0.#} no ar   ·   " +
                             $"estadia {flying * upkeep:0.0}/dia   ·   cofre {me.Money:0}", 16));
        // de que é feita a aviação: sem esta linha, dez asas eram dez asas e comprar não era decisão nenhuma
        var fleet = Ui.Lbl($"✈ {Air.Describe(w, me.Planes)}   ·   peso {Air.Power(w, me.Planes):0.#}", 15);
        fleet.TooltipText = "O peso é o que decide um combate aéreo. Caros a mais para tudo, os caças são o que "
                            + "impede que o resto seja abatido: as perdas caem primeiro em quem não sabe lutar lá em cima.";
        card.AddChild(fleet);
        if (me.AirPower <= 0f)
        {
            card.AddChild(Ui.Lbl("Sem esquadrões: compram-se aqui no hangar, e só depois há céu para mandar.", 16));
            Hangar(card, w, pid);
            _body.AddChild(box);
            return;
        }

        foreach (var m in w.AirMissions.Where(x => x.CountryId == pid).OrderBy(x => x.RegionId).ToList())
        {
            if (!w.AirMissionDefs.TryGetValue(m.MissionId, out var def) || !w.Regions.TryGetValue(m.RegionId, out var r)) continue;
            // o inimigo que conta é o da ZONA: é com esse que as nossas asas se batem hoje
            float foe = w.AirMissions.Where(x => Zones.Air(w, x.RegionId) == Zones.Air(w, m.RegionId)
                                                 && w.AreAtWar(pid, x.CountryId)).Sum(x => x.Wings);
            int rid = m.RegionId;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            if (OnShowRegion is not null) row.AddChild(Ui.Btn("Ver", () => Show(rid), 90));
            row.AddChild(Ui.Btn("Recolher", () => RecallAir(pid, rid), 150));
            card.AddChild(FormationView.Plate(new FormationView.Info(
                m.Name, FormationView.IsHome(w, pid, World.Air, m.Name), World.Air, def.Glyph, def.Name,
                r.Name, m.Wings, foe, w.Clock.Day - m.SinceDay, def.Note), row));

            // que aviões é que ali estão: quem manda para bombardear sem caça nenhum fica a saber porquê
            var mix = new HBoxContainer(); mix.AddThemeConstantOverride("separation", 6);
            foreach (var (cls, n) in m.Squadron.Where(kv => kv.Value > 0.05f)
                     .OrderByDescending(kv => Air.Battle(w, kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(4))
            {
                mix.AddChild(Glyph.Make(Air.Glyph(w, cls), 15f, Ui.TextDim, Air.Name(w, cls)));
                mix.AddChild(Ui.Lbl($"{Air.Name(w, cls)} ×{n:0.#}", 14));
            }
            mix.AddChild(Ui.Grow(Ui.Lbl($"peso {Air.Power(w, m.Squadron):0.#}", 14)));
            card.AddChild(mix);
        }

        ZoneBoard(card, w, Zones.AirBoard(w, pid), "asas", "céu");
        Hangar(card, w, pid);

        // céus a que se pode mandar hoje: a frente inimiga e a nossa terra onde já se combate
        var skies = new List<int>();
        if (AirMissionSystem.Front(w, pid) is int front) skies.Add(front);
        foreach (var b in w.ActiveBattles)
            if (skies.Count < 3 && !skies.Contains(b.RegionId)
                && w.Regions.TryGetValue(b.RegionId, out var br) && br.ControllerId == pid) skies.Add(b.RegionId);
        if (skies.Count == 0)
        {
            card.AddChild(Ui.Lbl("Nenhum céu ao alcance: a frente tem de tocar em terra nossa.", 16));
            _body.AddChild(box);
            return;
        }

        float lot = MathF.Max(w.Rule("air_mission_min_wings", 1f), MathF.Floor(free));
        foreach (int rid in skies)
        {
            var r = w.Regions[rid];
            card.AddChild(Ui.Lbl($"{(r.ControllerId == pid ? "Céu nosso" : "Céu deles")}: {r.Name}"
                                 + (r.ControllerId == pid ? "" : $"   ·   infra {r.Infrastructure:0.00}"), 15));
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            foreach (var def in w.AirMissionDefs.Values.OrderBy(d => d.Sort))
            {
                string mid = def.Id; int target = rid;
                string? no = AirMissionSystem.Block(w, pid, target, mid, lot);
                var b = Ui.Btn($"    {def.Name} ({lot:0.#})", () => SendAir(pid, target, mid, lot), 0,
                               no is null ? Ui.Kind.Primary : Ui.Kind.Normal);
                Glyph.Stamp(b, def.Glyph, no is null ? Ui.Ink : Ui.Accent);
                b.Disabled = no is not null;
                b.TooltipText = no ?? def.Note;
                row.AddChild(Ui.Grow(b));
            }
            card.AddChild(row);
        }
        _body.AddChild(box);
    }

    /// <summary>O quadro das zonas: uma linha por pedaço de mundo onde há asas (ou navios) e de quem é
    /// aquele céu (ou aquele mar). É a leitura que faltava — antes o painel só sabia dizer província a
    /// província, e a guerra do ar decide-se em zonas: quem tem 10 asas espalhadas por cinco zonas não
    /// manda em nenhuma, e é isto que o mostra sem abrir ficha nenhuma.</summary>
    private void ZoneBoard(Node card, World w, List<ZoneLine> lines, string unit, string what)
    {
        if (lines.Count == 0) return;
        card.AddChild(Ui.Lbl($"Zonas com {unit} ({lines.Count}):", 15));
        foreach (var l in lines.Take((int)w.Rule("map_key_max", 12f)))
        {
            var ink = l.Who == "deles" ? Ui.Danger : l.Who == "disputado" ? Ui.Accent : Ui.Text;
            string tip = $"As {unit} de toda a zona contam neste {what}: uma missão destacada aqui vale "
                         + "em todas as regiões da zona, e é aqui que se encontram as do inimigo.";
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            row.AddChild(Glyph.Make(l.Glyph, 16f, ink, tip));
            var lbl = Ui.Lbl($"{l.Name}: {l.Mine:0.#} contra {l.Theirs:0.#} — {what} {l.Who}"
                             + $"   ·   {l.Regions} regi{(l.Regions == 1 ? "ão" : "ões")}", 15);
            lbl.TooltipText = tip;
            lbl.AddThemeColorOverride("font_color", ink);
            row.AddChild(lbl);
            card.AddChild(row);
        }
    }

    /// <summary>O hangar: uma linha por modelo de avião (plane_class) com o que ele serve, o que já temos
    /// dele, quantas asas estão em casa e o botão de encomendar. É o gémeo do estaleiro — e a mesma decisão
    /// que faltava ao céu: até aqui comprar aviação era carregar num botão e não escolher nada.</summary>
    private void Hangar(Node card, World w, int pid)
    {
        if (w.PlaneClasses.Count == 0) return;
        var me = w.Countries[pid];
        card.AddChild(Ui.Lbl($"Hangar ({Air.Describe(w, me.Planes)}):", 15));
        foreach (var d in w.PlaneClasses.Values.OrderBy(x => x.Sort))
        {
            float have = me.Planes.GetValueOrDefault(d.Id), home = Air.Free(w, pid, d.Id);
            float cost = Air.Cost(w, d.Id);
            string cls = d.Id;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            row.AddChild(Glyph.Make(d.Glyph, 18f, have > 0f ? Ui.Accent : Ui.TextDim, d.Note));
            var lbl = Ui.Lbl($"{d.Name} ({d.Role})   ·   {have:0.#} nossas, {home:0.#} em casa"
                             + $"   ·   combate {d.Air:0.#}, estadia ×{d.Upkeep:0.#}", 15);
            lbl.TooltipText = d.Note + $"\nSuperioridade ×{d.Superiority:0.#}, apoio ×{d.Support:0.#}, "
                              + $"bombardeamento ×{d.Bombing:0.#}, transporte ×{d.Transport:0.#}.";
            row.AddChild(Ui.Grow(lbl));
            var buy = Ui.Btn($"{cost:0}", () => BuyPlane(pid, cls), 110);
            buy.Disabled = me.Money < cost;
            buy.TooltipText = $"Encomendar um {d.Name} por {cost:0} pontos de produção.";
            row.AddChild(buy);
            card.AddChild(row);
        }
    }

    private void BuyPlane(int pid, string classId) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new BuyPlaneCommand(pid, classId));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify($"{Air.Name(_game.World, classId)} encomendado: entra hoje no hangar");
        _lastKey = ""; Fill();
    });

    private void SendAir(int pid, int regionId, string missionId, float wings) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new AssignAirMissionCommand(pid, regionId, missionId, wings));
        if (err is not null) { _game.Notify(err); return; }
        // o aviso diz o nome da asa: é assim que o jogador fica a saber que ela tem um
        string flew = _game.World.AirMissions.FirstOrDefault(m => m.CountryId == pid && m.RegionId == regionId)?.Name ?? "";
        _game.Notify((flew.Length > 0 ? flew : $"{wings:0.#} asas") + $" a caminho de {_game.World.Regions[regionId].Name}");
        _lastKey = ""; Fill();
    });

    private void RecallAir(int pid, int regionId) => _game.RunWhenIdle(() =>
    {
        string home = _game.World.AirMissions.FirstOrDefault(m => m.CountryId == pid && m.RegionId == regionId)?.Name ?? "";
        var err = _game.Dispatch(new RecallAirMissionCommand(pid, regionId));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify((home.Length > 0 ? home : "Esquadrões") + $" de volta de {_game.World.Regions[regionId].Name}");
        _lastKey = ""; Fill();
    });

    /// <summary>O que o painel tem de redesenhar quando o mar muda: as nossas esquadras, os navios no porto,
    /// o que o inimigo tem à porta e as costas que hoje estão fechadas.</summary>
    private string SeaKey(World w, int pid) =>
        $"sea{NavalMissionSystem.Free(w, pid):0.0}:"
        + string.Join(",", (w.Countries.TryGetValue(pid, out var me) ? me.Ships : new Dictionary<string, float>())
            .OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}{kv.Value:0.#}")) + ":"
        + string.Join("-", w.NavalMissions.OrderBy(m => m.RegionId).ThenBy(m => m.CountryId)
            .Select(m => $"{m.CountryId}@{m.RegionId}={m.MissionId}:{m.Ships:0.0}"))
        + ":" + (NavalMissionSystem.Target(w, pid)?.ToString() ?? "-")
        + $":cv{ConvoySystem.Available(w, pid):0.#}/{ConvoySystem.SupplyNeed(w, pid) + ConvoySystem.TradeNeed(w, pid):0.#}/{ConvoySystem.GroundedCount(w, pid)}";

    /// <summary>Guerra naval: o mar era um cano de abastecimento que ninguém podia cortar. A secção mostra a
    /// frota (no porto / no mar / o que custa por dia), as esquadras destacadas com o mar que está disputado
    /// e as costas ao alcance — a deles para bloquear ou patrulhar, a nossa para escoltar comboios.
    ///
    /// Vive no painel da Guerra, ao lado do céu: as duas decisões são a mesma — onde é que se põe o aço que
    /// há, sabendo que espalhá-lo por toda a parte não fecha nada.</summary>
    private void SeaWar(World w, int pid)
    {
        var me = w.Countries[pid];
        float upkeep = w.Rule("naval_mission_upkeep", 0.8f);
        float free = NavalMissionSystem.Free(w, pid), sailing = NavalMissionSystem.Assigned(w, pid);
        Header("Guerra naval");
        var (box, card) = Card();

        card.AddChild(Ui.Lbl($"⚓ {me.Warships:0.#} navios — {free:0.#} no porto, {sailing:0.#} no mar   ·   " +
                             $"estadia {sailing * upkeep:0.0}/dia   ·   cofre {me.Money:0}", 16));

        // a marinha mercante ao lado da de guerra: é ela que o bloqueio inimigo come, e é dela que vive o
        // exército do outro lado do mar. Sem esta linha, a guerra ao comércio era um número invisível
        float holds = ConvoySystem.Available(w, pid);
        float busy = ConvoySystem.SupplyNeed(w, pid) + ConvoySystem.TradeNeed(w, pid);
        int stuck = ConvoySystem.GroundedCount(w, pid);
        var conv = Ui.Lbl($"⛵ {holds:0} comboios mercantes — {busy:0} ocupados"
                          + (Raided(w, pid) is float sunk && sunk > 0f ? $"   ☠ {sunk:0.0} ao fundo por dia" : "")
                          + (stuck > 0 ? $"   ⛔ {stuck} contrato{(stuck == 1 ? "" : "s")} parado{(stuck == 1 ? "" : "s")}" : ""), 16);
        conv.TooltipText = "Os comboios carregam primeiro o abastecimento por mar do exército e só depois as "
                           + "importações. O que não couber fica no cais: a frente come menos e o contrato não entrega.";
        if (stuck > 0 || busy > holds) conv.AddThemeColorOverride("font_color", Ui.Danger);
        card.AddChild(conv);
        if (me.Warships <= 0f)
        {
            card.AddChild(Ui.Lbl("Sem frota: os navios compram-se no painel do País, e só depois há mar para mandar.", 16));
            _body.AddChild(box);
            return;
        }

        foreach (var m in w.NavalMissions.Where(x => x.CountryId == pid).OrderBy(x => x.RegionId).ToList())
        {
            if (!w.NavalMissionDefs.TryGetValue(m.MissionId, out var def) || !w.Regions.TryGetValue(m.RegionId, out var r)) continue;
            // o mesmo no mar: quem está na zona está à nossa frente, ainda que o cais seja outro
            float foe = w.NavalMissions.Where(x => Zones.Sea(w, x.RegionId) == Zones.Sea(w, m.RegionId)
                                                   && w.AreAtWar(pid, x.CountryId)).Sum(x => x.Ships);
            int rid = m.RegionId;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            if (OnShowRegion is not null) row.AddChild(Ui.Btn("Ver", () => Show(rid), 90));
            row.AddChild(Ui.Btn("Recolher", () => RecallSea(pid, rid), 150));
            card.AddChild(FormationView.Plate(new FormationView.Info(
                m.Name, FormationView.IsHome(w, pid, World.Sea, m.Name), World.Sea, def.Glyph, def.Name,
                r.Name, m.Ships, foe, w.Clock.Day - m.SinceDay, def.Note,
                NavalMissionSystem.Blockaded(w, rid) ? "costa fechada" : ""), row));

            // de que é feita a esquadra: é a composição que ganha o mar, não a contagem de cascos
            var mix = new HBoxContainer(); mix.AddThemeConstantOverride("separation", 6);
            foreach (var (cls, n) in m.Squadron.Where(kv => kv.Value > 0.05f)
                     .OrderByDescending(kv => Navy.Battle(w, kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(4))
            {
                mix.AddChild(Glyph.Make(Navy.Glyph(w, cls), 15f, Ui.TextDim, Navy.Name(w, cls)));
                mix.AddChild(Ui.Lbl($"{Navy.Name(w, cls)} ×{n:0.#}", 14));
            }
            mix.AddChild(Ui.Grow(Ui.Lbl($"peso {Navy.Power(w, m.Squadron):0.#}", 14)));
            card.AddChild(mix);
        }

        ZoneBoard(card, w, Zones.SeaBoard(w, pid), "navios", "mar");
        Shipyard(card, w, pid);

        // mares a que se pode mandar hoje: a melhor costa deles ao nosso alcance e as nossas costas com porto
        var seas = new List<int>();
        if (NavalMissionSystem.Target(w, pid) is int target) seas.Add(target);
        foreach (var r in w.Regions.Values.Where(x => x.ControllerId == pid && x.SeaNeighbours.Count > 0
                                                      && x.Buildings.Count > 0).OrderByDescending(x => x.Buildings.Values.Sum()).ThenBy(x => x.Id))
            if (seas.Count < 3 && !seas.Contains(r.Id)) seas.Add(r.Id);
        if (seas.Count == 0)
        {
            card.AddChild(Ui.Lbl("Nenhum mar ao alcance: é preciso costa nossa com rota até lá.", 16));
            _body.AddChild(box);
            return;
        }

        float lot = MathF.Max(w.Rule("naval_mission_min_ships", 1f), MathF.Floor(free));
        foreach (int rid in seas)
        {
            var r = w.Regions[rid];
            card.AddChild(Ui.Lbl($"{(r.ControllerId == pid ? "Costa nossa" : "Costa deles")}: {r.Name}"
                                 + (NavalMissionSystem.Blockaded(w, rid) ? "   ·   fechada por bloqueio" : ""), 15));
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            foreach (var def in w.NavalMissionDefs.Values.OrderBy(d => d.Sort))
            {
                string mid = def.Id; int sea = rid;
                string? no = NavalMissionSystem.Block(w, pid, sea, mid, lot);
                var b = Ui.Btn($"    {def.Name} ({lot:0.#})", () => SendSea(pid, sea, mid, lot), 0,
                               no is null ? Ui.Kind.Primary : Ui.Kind.Normal);
                Glyph.Stamp(b, def.Glyph, no is null ? Ui.Ink : Ui.Accent);
                b.Disabled = no is not null;
                b.TooltipText = no ?? def.Note;
                row.AddChild(Ui.Grow(b));
            }
            card.AddChild(row);
        }
        _body.AddChild(box);
    }

    /// <summary>Mercantes nossos que o bloqueio inimigo afunda hoje: o que está em cima de costa nossa e a
    /// escolta não conseguiu abrir.</summary>
    private static float Raided(World w, int pid) =>
        w.NavalMissions.Where(m => w.AreAtWar(pid, m.CountryId)
                                   && w.NavalMissionDefs.TryGetValue(m.MissionId, out var d) && d.Effect == "blockade"
                                   && w.Regions.TryGetValue(m.RegionId, out var r) && r.ControllerId == pid
                                   && NavalMissionSystem.Blockaded(w, m.RegionId))
            .Sum(m => m.Ships) * w.Rule("convoy_raid_sink", 0.25f);

    private void SendSea(int pid, int regionId, string missionId, float ships) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new AssignNavalMissionCommand(pid, regionId, missionId, ships));
        if (err is not null) { _game.Notify(err); return; }
        string sailed = _game.World.NavalMissions.FirstOrDefault(m => m.CountryId == pid && m.RegionId == regionId)?.Name ?? "";
        _game.Notify((sailed.Length > 0 ? sailed : $"{ships:0.#} navios") + $" a caminho de {_game.World.Regions[regionId].Name}");
        _lastKey = ""; Fill();
    });

    /// <summary>O estaleiro: uma linha por classe de casco (ship_class) com o que ela serve, o que já temos
    /// dela, quantos estão no porto e o botão de encomendar. Era aqui que faltava a decisão da marinha —
    /// antes só havia "comprar navio", e um navio era um navio.</summary>
    private void Shipyard(Node card, World w, int pid)
    {
        if (w.ShipClasses.Count == 0) return;
        var me = w.Countries[pid];
        card.AddChild(Ui.Lbl($"Estaleiro ({Navy.Describe(w, me.Ships)}):", 15));
        foreach (var d in w.ShipClasses.Values.OrderBy(x => x.Sort))
        {
            float have = me.Ships.GetValueOrDefault(d.Id), port = Navy.Free(w, pid, d.Id);
            float cost = Navy.Cost(w, d.Id);
            string cls = d.Id;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            row.AddChild(Glyph.Make(d.Glyph, 18f, have > 0f ? Ui.Accent : Ui.TextDim, d.Note));
            var lbl = Ui.Lbl($"{d.Name} ({d.Role})   ·   {have:0.#} nossos, {port:0.#} no porto"
                             + $"   ·   combate {d.Battle:0.#}, couraça {d.Screen:0.#}", 15);
            lbl.TooltipText = d.Note + $"\nBloqueio ×{d.Blockade:0.#}, escolta ×{d.Escort:0.#}, patrulha ×{d.Patrol:0.#}.";
            row.AddChild(Ui.Grow(lbl));
            var buy = Ui.Btn($"{cost:0}", () => BuyShip(pid, cls), 110);
            buy.Disabled = me.Money < cost;
            buy.TooltipText = $"Encomendar um {d.Name} por {cost:0} pontos de produção.";
            row.AddChild(buy);
            card.AddChild(row);
        }
    }

    private void BuyShip(int pid, string classId) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new BuyShipCommand(pid, classId));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify($"{Navy.Name(_game.World, classId)} encomendado: entra hoje no porto");
        _lastKey = ""; Fill();
    });

    private void RecallSea(int pid, int regionId) => _game.RunWhenIdle(() =>
    {
        string port = _game.World.NavalMissions.FirstOrDefault(m => m.CountryId == pid && m.RegionId == regionId)?.Name ?? "";
        var err = _game.Dispatch(new RecallNavalMissionCommand(pid, regionId));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify((port.Length > 0 ? port : "Esquadra") + $" de volta de {_game.World.Regions[regionId].Name}");
        _lastKey = ""; Fill();
    });

    /// <summary>Adido militar: um oficial nosso a ver a guerra dos outros de dentro. Vive aqui, no painel da
    /// guerra, porque é onde estão as guerras — as nossas em cima, as alheias a seguir. Sem esta secção a
    /// missão só existia para a IA e um país em paz nunca chegava ao primeiro degrau de doutrina.</summary>
    private void Attaches(World w, int pid)
    {
        float cost = w.Rule("attache_cost_per_day", 0.5f);
        float gain = w.Rule("attache_xp_per_day", 0.3f);
        float days = w.Rule("attache_min_days", 10f);
        var me = w.Countries[pid];
        Header("Adido militar");
        var (box, card) = Card();

        if (w.Attaches.TryGetValue(pid, out var a))
        {
            var title = new HBoxContainer();
            var fl = Flags.Rect(22);
            if (w.Countries.TryGetValue(a.HostId, out var hc)) { fl.Texture = Flags.Of(hc.Tag); fl.Visible = fl.Texture is not null; }
            title.AddChild(fl);
            title.AddChild(Ui.Grow(Ui.Lbl($"🎖 destacado junto de {Name(w, a.HostId)}", 20)));
            title.AddChild(Ui.Lbl($"{w.Clock.Day - a.SinceDay} dias", 16));
            card.AddChild(title);
            card.AddChild(Ui.Lbl($"Trouxe {a.Learned:0.0} de experiência   ·   custa {cost:0.0} por dia   ·   " +
                                 $"cofre {me.Money:0}", 16));
            card.AddChild(Ui.Btn("Chamar adido de volta", () => Recall(pid), 240));
            _body.AddChild(box);
            return;
        }

        var hosts = Hosts(w, pid);
        if (hosts.Count == 0)
        {
            card.AddChild(Ui.Lbl(w.AtWar(pid)
                ? "A nossa guerra já a vemos de dentro: o adido só parte quando houver paz"
                : "Ninguém se bate lá fora: não há guerra alheia para observar", 16));
            _body.AddChild(box);
            return;
        }

        bool rich = me.Money >= cost * days;
        card.AddChild(Ui.Lbl($"Um oficial junto de um exército estrangeiro traz {gain:0.0} de experiência por dia " +
                             $"e custa {cost:0.0} — a mesa da missão pede {cost * days:0} no cofre.", 16));
        foreach (var h in hosts)
        {
            int hid = h.Id;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            var fl = Flags.Rect(20); fl.Texture = Flags.Of(h.Tag); fl.Visible = fl.Texture is not null;
            row.AddChild(fl);
            row.AddChild(Ui.Grow(Ui.Lbl($"{h.Name} contra {string.Join(", ", h.AtWarWith.OrderBy(x => x).Select(x => Name(w, x)))}", 16)));
            var b = Ui.Btn("Enviar adido", () => Send(pid, hid), 170, rich ? Ui.Kind.Primary : Ui.Kind.Normal);
            b.Disabled = !rich;
            b.TooltipText = rich ? $"a estadia sai a {cost:0.0} por dia, até ser chamado de volta"
                                 : $"faltam {cost * days - me.Money:0} no cofre";
            row.AddChild(b);
            card.AddChild(row);
        }
        _body.AddChild(box);
    }

    private void Send(int pid, int hostId) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new SendAttacheCommand(pid, hostId));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify($"Adido a caminho de {Name(_game.World, hostId)}");
        _lastKey = ""; Fill();
    });

    /// <summary>Empréstimo de material: a torneira que se abre a um aliado a arder. Uma fatia do nosso
    /// rendimento diário sai daqui todos os dias e entra no cofre dele, com as perdas do caminho pelo meio
    /// (LendLeaseSystem). É a única coisa do jogo que se pede numa gama e não num sim/não — por isso é aqui
    /// que a régua de latão (BrassSlider) aparece: arrasta-se a fatia e vê-se, na legenda, o que sai e o
    /// que chega antes de largar. A ordem só parte quando o dedo se levanta.
    ///
    /// Três blocos: a conta do país (o que ganhamos, o que já prometemos, o tecto), os acordos em vigor de
    /// cada lado, e a lista de quem se pode ajudar — aliados de facção primeiro, que são quem costuma
    /// precisar.</summary>
    private void LendLease(World w, int pid)
    {
        var me = w.Countries[pid];
        float income = EconomySystem.Income(w, pid);
        float max = w.Rule("lend_lease_max_share", 0.35f), min = w.Rule("lend_lease_min_share", 0.05f);
        float waste = w.Rule("lend_lease_waste", 0.2f);
        float given = LendLeaseSystem.Given(w, pid);

        Header("Empréstimo de material");
        var (sumBox, sum) = Card();
        var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 10);
        line.AddChild(Ui.Grow(Ui.Lbl($"Rendimento {income:0.0}/dia   ·   prometido {given:P0} de {max:P0}", 18)));
        line.AddChild(Ui.Bar(max <= 0f ? 0f : given / max, Ui.Accent, 120f));
        sum.AddChild(line);
        sum.AddChild(Ui.Lbl($"Sai {LendLeaseSystem.Out(w, pid):0.0}/dia   ·   entra {LendLeaseSystem.In(w, pid):0.0}/dia   ·   " +
                            $"perde-se {waste:P0} no caminho   ·   cofre {me.Money:0}", 15));
        _body.AddChild(sumBox);

        var out_ = w.LendLeases.Where(l => l.FromId == pid).OrderBy(l => l.ToId).ToList();
        var in_ = w.LendLeases.Where(l => l.ToId == pid).OrderBy(l => l.FromId).ToList();

        if (out_.Count > 0) Header("A sair");
        foreach (var l in out_)
        {
            int to = l.ToId;
            var (box, card) = Card();
            var title = new HBoxContainer(); title.AddThemeConstantOverride("separation", 8);
            var fl = Flags.Rect(22);
            if (w.Countries.TryGetValue(to, out var tc)) { fl.Texture = Flags.Of(tc.Tag); fl.Visible = fl.Texture is not null; }
            title.AddChild(fl);
            title.AddChild(Ui.Grow(Ui.Lbl($"⚓ a {Name(w, to)}", 20)));
            title.AddChild(Ui.Lbl($"{w.Clock.Day - l.SinceDay} dias   ·   {l.SentTotal:0} entregues", 15));
            card.AddChild(title);
            var rule = new BrassSlider();
            rule.Set(min, MathF.Min(max, l.Share + LendLeaseSystem.FreeShare(w, pid)), 0.05f, l.Share,
                     v => Cut(income, v, waste), null, v => Sign(pid, to, v));
            card.AddChild(rule);
            card.AddChild(Ui.Btn("Fechar a torneira", () => Stop(pid, to), 220, Ui.Kind.Danger));
            _body.AddChild(box);
        }

        if (in_.Count > 0) Header("A entrar");
        foreach (var l in in_)
        {
            int from = l.FromId;
            var (box, card) = Card();
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            var fl = Flags.Rect(22);
            if (w.Countries.TryGetValue(from, out var fc2)) { fl.Texture = Flags.Of(fc2.Tag); fl.Visible = fl.Texture is not null; }
            row.AddChild(fl);
            row.AddChild(Ui.Grow(Ui.Lbl($"{Name(w, from)} manda +{LendLeaseSystem.Landed(w, l):0.0}/dia", 19)));
            row.AddChild(Ui.Btn("Dispensar", () => Stop(pid, from), 150));
            card.AddChild(row);
            card.AddChild(Ui.Lbl($"{l.SentTotal:0} pontos entregues em {w.Clock.Day - l.SinceDay} dias", 15));
            _body.AddChild(box);
        }

        // candidatos: aliados de facção primeiro (é quem costuma precisar), depois qualquer país em guerra
        // com quem não estejamos em guerra. Oito chegam para um ecrã — a lista inteira eram 250 filas.
        var allies = new HashSet<int>(w.Allies(pid));
        var pool = w.Countries.Values
            .Where(x => x.Id != pid && !x.Capitulated && !w.AreAtWar(pid, x.Id)
                        && !out_.Any(l => l.ToId == x.Id) && (allies.Contains(x.Id) || x.AtWarWith.Count > 0))
            .OrderByDescending(x => allies.Contains(x.Id)).ThenByDescending(x => x.AtWarWith.Count).ThenBy(x => x.Id)
            .Take(8).ToList();
        float free = LendLeaseSystem.FreeShare(w, pid);
        if (pool.Count == 0 || free < min - 1e-4f)
        {
            var (box, card) = Card();
            card.AddChild(Ui.Lbl(free < min - 1e-4f
                ? $"Já temos {given:P0} do rendimento prometido: o tecto são {max:P0}."
                : "Ninguém a quem mandar material: não há aliado nem guerra alheia onde ele faça falta.", 16));
            _body.AddChild(box);
            return;
        }

        Header("Abrir uma torneira nova");
        foreach (var cand in pool)
        {
            int to = cand.Id;
            var (box, card) = Card();
            var title = new HBoxContainer(); title.AddThemeConstantOverride("separation", 8);
            var fl = Flags.Rect(20); fl.Texture = Flags.Of(cand.Tag); fl.Visible = fl.Texture is not null;
            title.AddChild(fl);
            title.AddChild(Ui.Grow(Ui.Lbl(cand.Name, 19)));
            title.AddChild(Ui.Lbl(cand.AtWarWith.Count == 0 ? "em paz"
                                  : $"em guerra com {string.Join(", ", cand.AtWarWith.OrderBy(x => x).Select(x => Name(w, x)))}", 15));
            card.AddChild(title);
            var rule = new BrassSlider();
            float start = Math.Clamp(_share.GetValueOrDefault(to, min), min, MathF.Min(max, free));
            _share[to] = start;
            rule.Set(min, MathF.Min(max, free), 0.05f, start, v => Cut(income, v, waste), v => _share[to] = v);
            card.AddChild(rule);
            card.AddChild(Ui.Btn("Assinar o empréstimo", () => Sign(pid, to, _share.GetValueOrDefault(to, min)), 240, Ui.Kind.Primary));
            _body.AddChild(box);
        }
    }

    /// <summary>Legenda da régua: a fatia, o que sai do nosso cofre e o que chega ao aliado.</summary>
    private static string Cut(float income, float share, float waste) =>
        $"{share:P0} do rendimento   ·   −{income * share:0.0}/dia daqui, +{income * share * (1f - waste):0.0}/dia lá";

    /// <summary>Fatias escolhidas na régua e ainda por assinar, por país. Sem isto o valor voltava ao
    /// mínimo cada vez que o painel se redesenhasse (e ele redesenha-se todos os dias de jogo).</summary>
    private readonly Dictionary<int, float> _share = new();

    private void Sign(int pid, int toId, float share) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new LendLeaseCommand(pid, toId, share));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify($"Material a caminho de {Name(_game.World, toId)} ({share:P0} do rendimento)");
        _lastKey = ""; Fill();
    });

    private void Stop(int pid, int otherId) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new CancelLendLeaseCommand(pid, otherId));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify("Empréstimo de material fechado");
        _lastKey = ""; Fill();
    });

    /// <summary>--smoke: abre a secção do material, arrasta a régua até meio e assina o empréstimo ao
    /// primeiro candidato. Devolve o que ficou assinado, em texto.</summary>
    public string SmokeMaterial()
    {
        if (_game.PlayerId is not int pid) return "sem país";
        var w = _game.World;
        _tab = 4; _lastKey = ""; Fill();
        var rule = Rules(_body).FirstOrDefault();
        float share = rule?.SmokeDrag(0.5f) ?? 0f;
        var cand = w.Countries.Values.FirstOrDefault(x => x.Id != pid && !x.Capitulated && !w.AreAtWar(pid, x.Id));
        string done = "sem candidatos";
        if (cand is not null && share > 0f)
            done = _game.Dispatch(new LendLeaseCommand(pid, cand.Id, share))
                ?? $"{share:P0} a {cand.Name} (−{LendLeaseSystem.Out(w, pid):0.0}/dia daqui, +{LendLeaseSystem.In(w, cand.Id):0.0}/dia lá)";
        _lastKey = ""; Fill();                       // a secção redesenhada já com o acordo em vigor
        int rules = Rules(_body).Count;
        _tab = 0; _lastKey = "";
        return $"{done}, {rules} régua{(rules == 1 ? "" : "s")} de latão";
    }

    private static List<BrassSlider> Rules(Node n)
    {
        var found = new List<BrassSlider>();
        foreach (var c in n.GetChildren())
        {
            if (c is BrassSlider s) found.Add(s);
            found.AddRange(Rules(c));
        }
        return found;
    }

    /// <summary>--smoke: desenha a secção do adido com a lista de anfitriões cheia e diz quantos ficaram.</summary>
    public int SmokeAttache()
    {
        if (_game.PlayerId is not int pid) return 0;
        _smokeHosts = true;
        int hosts = Hosts(_game.World, pid).Count;
        _tab = 3;                                    // a secção do adido é a quarta aba
        _lastKey = ""; Fill();
        _smokeHosts = false; _tab = 0; _lastKey = "";
        return hosts;
    }

    /// <summary>Só para o --smoke: passa por todas as abas, para o desenho de cada secção (frentes, ar,
    /// mar, adidos, arquivo) correr sem ninguém tocar no ecrã. Devolve quantas abas desenhou.</summary>
    public int SmokeTabs()
    {
        for (int i = 0; i < Sections.Length; i++) { _tab = i; _lastKey = ""; Fill(); }
        _tab = 0; _lastKey = "";
        return Sections.Length;
    }

    private void Recall(int pid) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new RecallAttacheCommand(pid));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify("Adido chamado de volta");
        _lastKey = ""; Fill();
    });

    /// <summary>Mesa de negociação: escolhem-se as regiões a exigir e vê-se, antes de propor, se o outro
    /// lado assina — a pressão que sofre (ocupação, exércitos, desgaste, capital) contra o preço do que se
    /// lhe pede (PeaceTerms.Evaluate). Sem isto o jogador só podia adivinhar termos e levar recusa atrás
    /// de recusa, enquanto a IA fechava as guerras dela sozinha.</summary>
    private void Deal(World w, VBoxContainer card, int pid, int foe)
    {
        var head = new HBoxContainer();
        head.AddChild(Ui.Grow(Ui.Lbl("Negociação", 15)));
        head.AddChild(Ui.Btn(_deal == foe ? "Fechar mesa" : "Negociar paz", () => ToggleDeal(foe), 190));
        card.AddChild(head);
        if (_deal != foe) return;

        // candidatas: o que já lhe ocupamos primeiro, depois o objectivo de guerra que ainda não é nosso
        var candidates = PeaceTerms.OccupiedRegions(w, pid, foe)
            .OrderByDescending(id => w.Regions[id].Population).ThenBy(id => id).ToList();
        foreach (int id in w.Wars[World.WarKey(pid, foe)].Side(pid).Goals)
            if (!candidates.Contains(id) && w.Regions.TryGetValue(id, out var gr) && gr.OwnerId == foe) candidates.Add(id);
        _demand.IntersectWith(candidates);

        if (candidates.Count == 0)
        {
            card.AddChild(Ui.Lbl("Nada para exigir: não ocupas nada dele. Resta a paz branca.", 15));
        }
        else
        {
            var chips = new HFlowContainer();
            foreach (int id in candidates)
            {
                int rid = id;
                var r = w.Regions[rid];
                bool on = _demand.Contains(rid);
                bool held = r.ControllerId == pid;
                var b = Ui.Btn((on ? "✔ " : "") + r.Name + (held ? "" : " (livre)"), () => ToggleRegion(rid), 0,
                               on ? Ui.Kind.Primary : Ui.Kind.Normal);
                b.TooltipText = held ? "Já ocupada: sai barata na mesa" : "Não ocupada: custa quase o dobro";
                chips.AddChild(b);
            }
            card.AddChild(chips);
        }

        var verdict = PeaceTerms.Evaluate(w, pid, foe, _demand.ToList());
        float scale = MathF.Max(0.5f, MathF.Max(verdict.Pressure, verdict.Price));
        var scales = new HBoxContainer(); scales.AddThemeConstantOverride("separation", 8);
        scales.AddChild(Ui.Lbl($"Pressão {verdict.Pressure:0.00}", 15));
        scales.AddChild(Ui.Bar(verdict.Pressure / scale, Ui.Good, 130f));
        scales.AddChild(Ui.Lbl($"Preço {verdict.Price:0.00}", 15));
        scales.AddChild(Ui.Bar(verdict.Price / scale, Ui.Danger, 130f));
        card.AddChild(scales);

        var state = Ui.Lbl(_demand.Count == 0 ? "Escolhe o que queres exigir"
                          : verdict.Accepted ? "Nestes termos, assinam" : "Nestes termos, recusam", 16);
        state.AddThemeColorOverride("font_color", _demand.Count == 0 ? Ui.TextDim : verdict.Accepted ? Ui.Good : Ui.Danger);
        card.AddChild(state);

        var actions = new HBoxContainer();
        actions.AddChild(Ui.Btn("Termos sugeridos", () => Auto(pid, foe), 200));
        actions.AddChild(Ui.Btn("Exigir paz", () => DemandPeace(pid, foe), 170, Ui.Kind.Primary));
        actions.AddChild(Ui.Btn("Paz branca", () => WhitePeace(pid, foe), 170));
        card.AddChild(actions);
    }

    /// <summary>Propõe a troca de prisioneiros a este inimigo. O veredicto já estava no cartão; aqui só
    /// se despacha e se diz quantos homens voltaram.</summary>
    private void Swap(int pid, int foe) => _game.RunWhenIdle(() =>
    {
        var offer = PrisonerExchange.Evaluate(_game.World, pid, foe);
        var err = _game.Dispatch(new ExchangePrisonersCommand(pid, foe));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify($"Troca feita: {PrisonerView.Short(offer.Home)} dos nossos a caminho de casa");
        Fill();
    });

    /// <summary>Responde a uma proposta que este inimigo pôs na mesa (paz ou troca).</summary>
    private void Answer(int pid, PendingOffer offer, bool accept) => _game.RunWhenIdle(() =>
    {
        var deal = PrisonerExchange.Evaluate(_game.World, offer.FromId, pid);
        var err = _game.Dispatch(new AnswerOfferCommand(pid, offer.FromId, accept, offer.Kind));
        if (err is not null) { _game.Notify(err); return; }
        string land = _game.World.Regions.TryGetValue(offer.RegionId, out var lr) ? lr.Name : "uma região";
        _game.Notify(!accept ? "Proposta recusada"
                    : offer.Kind == "regiao" ? $"Paz assinada: {land} passa a ser nossa"
                    : offer.Kind == "paz" ? "Paz assinada: a guerra acabou onde estava"
                    : $"Troca aceite: {PrisonerView.Short(deal.Home)} dos nossos a caminho de casa");
        _deal = null; _demand.Clear();
        Fill();
    });

    /// <summary>Fecha o painel e manda o mapa para a região: ver a terra é mais forte do que lê-la.</summary>
    private void Show(int regionId)
    {
        Close();
        OnShowRegion?.Invoke(regionId);
    }

    private void ToggleDeal(int foe) => _game.RunWhenIdle(() =>
    {
        _deal = _deal == foe ? null : foe;
        _demand.Clear();
        Fill();
    });

    private void ToggleRegion(int regionId) => _game.RunWhenIdle(() =>
    {
        if (!_demand.Add(regionId)) _demand.Remove(regionId);
        Fill();
    });

    /// <summary>Enche a mesa com a maior exigência que o outro lado ainda assina.</summary>
    private void Auto(int pid, int foe) => _game.RunWhenIdle(() =>
    {
        _demand.Clear();
        foreach (int id in PeaceTerms.Suggest(_game.World, pid, foe)) _demand.Add(id);
        if (_demand.Count == 0) _game.Notify("Ainda não há termos que ele aceite — continua a guerra");
        Fill();
    });

    private void DemandPeace(int pid, int foe) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new DemandPeaceCommand(pid, foe, _demand.ToList()));
        if (err is not null) { _game.Notify(err); return; }
        if (!_game.World.AreAtWar(pid, foe)) { _deal = null; _demand.Clear(); }
        Fill();
    });

    private void WhitePeace(int pid, int foe) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new OfferPeaceCommand(pid, foe));
        if (err is not null) { _game.Notify(err); return; }
        if (!_game.World.AreAtWar(pid, foe)) { _deal = null; _demand.Clear(); }
        Fill();
    });

    /// <summary>Objectivo de guerra: o que viemos buscar, quanto já está nas nossas mãos e o que eles
    /// nos pedem a nós. Cada região é uma etiqueta — verde quando já é nossa, cinzenta enquanto não for.</summary>
    private static void Goals(World w, VBoxContainer card, WarInfo war, int pid, int foe)
    {
        var ours = war.Side(pid).Goals.ToList();
        if (ours.Count > 0)
        {
            int held = ours.Count(id => w.Regions.TryGetValue(id, out var r) && r.ControllerId == pid);
            bool met = held == ours.Count;
            var head = new HBoxContainer();
            head.AddChild(Ui.Grow(Ui.Lbl("Objectivo de guerra", 15)));
            var state = Ui.Lbl(met ? "cumprido" : $"{held}/{ours.Count}", 15);
            state.AddThemeColorOverride("font_color", met ? Ui.Good : Ui.TextDim);
            head.AddChild(state);
            card.AddChild(head);
            card.AddChild(Ui.Bar(ours.Count == 0 ? 0f : (float)held / ours.Count, met ? Ui.Good : new Color(1f, 0.82f, 0.25f), 200f));
            card.AddChild(Chips(w, ours, pid));
        }
        var theirs = war.Enemy(pid).Goals.ToList();
        if (theirs.Count > 0)
        {
            card.AddChild(Ui.Lbl($"{Name(w, foe)} exige", 15));
            card.AddChild(Chips(w, theirs, foe));
        }
    }

    /// <summary>Etiquetas com o nome de cada região do objectivo; verdes quando `holder` já a controla.</summary>
    private static Control Chips(World w, IEnumerable<int> regionIds, int holder)
    {
        var box = new HFlowContainer();
        foreach (int id in regionIds)
        {
            bool held = w.Regions.TryGetValue(id, out var r) && r.ControllerId == holder;
            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", Ui.Box((held ? Ui.Good : Ui.SurfaceHi) with { A = held ? 0.35f : 0.6f }, 6));
            var l = Ui.Lbl((held ? "✔ " : "") + (r?.Name ?? "#" + id), 16);
            chip.AddChild(l);
            box.AddChild(chip);
        }
        return box;
    }

    /// <summary>Linha de comparação: número nosso, barra dupla, número deles. A barra dá a proporção
    /// de relance — verde do nosso lado, vermelho do inimigo (trocados quando menos é melhor).</summary>
    private static void Compare(VBoxContainer card, string label, float mine, float theirs,
                                bool lowerIsBetter = false, string? left = null, string? right = null)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Ui.Lbl(left ?? Fmt(mine), 17));
        row.AddChild(Ui.Grow(Duo(mine, theirs, lowerIsBetter)));
        row.AddChild(Ui.Lbl(right ?? Fmt(theirs), 17));
        card.AddChild(Ui.Lbl(label, 15));
        card.AddChild(row);
    }

    private static string Fmt(float v) => v >= 1000f ? $"{v / 1000f:0.0}k" : $"{v:0}";

    private static Control Duo(float mine, float theirs, bool lowerIsBetter)
    {
        var box = new HBoxContainer { CustomMinimumSize = new Vector2(140, 12), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        box.AddThemeConstantOverride("separation", 3);
        float total = mine + theirs;
        var ours = lowerIsBetter ? Ui.Danger : Ui.Good;
        var yours = lowerIsBetter ? Ui.Good : Ui.Danger;
        if (total <= 0f)   // nada aconteceu ainda: barra neutra, para a linha não desaparecer
        {
            box.AddChild(new ColorRect { Color = Ui.SurfaceHi, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            return box;
        }
        box.AddChild(new ColorRect { Color = ours, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = MathF.Max(0.02f, mine / total) });
        box.AddChild(new ColorRect { Color = yours, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = MathF.Max(0.02f, theirs / total) });
        return box;
    }

    /// <summary>Cartão: a moldura que entra na lista e o VBox onde se escreve.</summary>
    private static (PanelContainer Box, VBoxContainer Body) Card()
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface with { A = 0.85f }, 10));
        var v = new VBoxContainer(); p.AddChild(v);
        return (p, v);
    }

    private static string Name(World w, int id) => w.Countries.TryGetValue(id, out var c) ? c.Name : "#" + id;

    private void Header(string text) { var l = Ui.Lbl(text, 20); l.Modulate = new Color(1f, 0.85f, 0.4f); _body.AddChild(l); }

    /// <summary>Teatros de operações: a mesma guerra vista por troços de linha em vez de por país. Um cartão
    /// por frente, com a guarnição que lá temos contra a que a frente pede, o avanço contra aquele inimigo e o
    /// aviso quando um troço tem regiões sem uma única divisão — o buraco por onde eles entram sem disparar.</summary>
    private void Theatres(World w, int pid)
    {
        var fronts = TheatreSystem.Of(w, pid);
        if (fronts.Count == 0) return;
        Header($"Teatros de operações ({fronts.Count})");
        foreach (var t in fronts)
        {
            var (box, card) = Card();
            var title = new HBoxContainer();
            title.AddChild(Ui.Grow(Ui.Lbl($"🛡 {t.Name}", 19)));
            var state = Ui.Lbl(t.Holes > 0 ? $"☠ {t.Holes} buraco{(t.Holes == 1 ? "" : "s")}" : "linha fechada", 15);
            state.AddThemeColorOverride("font_color", t.Holes > 0 ? Ui.Danger : Ui.Good);
            title.AddChild(state);
            card.AddChild(title);
            card.AddChild(Ui.Lbl($"contra {Name(w, t.FoeId)}   ·   {t.RegionIds.Count} região{(t.RegionIds.Count == 1 ? "" : "ões")} de contacto"
                                 + $"   ·   {t.Divisions} div nossas contra {t.FoeDivisions} deles", 15));
            Gauge(card, "Guarnição", t.Coverage, FrontOverlay.Tint(t.Coverage), $"{t.Divisions} de {t.Need:0.#} divisões");
            Gauge(card, "Avanço", t.Progress, Ui.Danger.Lerp(Ui.Good, t.Progress), $"{t.Progress:P0} da terra dele");
            _body.AddChild(box);
        }
    }

    /// <summary>Uma barra com nome à esquerda e a conta à direita, para os números da frente se lerem de relance.</summary>
    private static void Gauge(VBoxContainer card, string label, float value, Color tint, string note)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Ui.Lbl(label, 15));
        var bar = Ui.Bar(Mathf.Clamp(value, 0f, 1f), tint, 0f);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(bar);
        var n = Ui.Lbl(note, 14); n.AddThemeColorOverride("font_color", Ui.TextDim);
        row.AddChild(n);
        card.AddChild(row);
    }
}
