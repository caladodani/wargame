using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Painel de país (45% inferior): ficha (country_info), características (country_stat), espíritos nacionais
/// e investigação (tecnologia em curso, disponíveis com "Investigar", concluídas). Para o país do jogador tem
/// botões; para os outros é só leitura. Lê o World só em Fill (mundo parado); muta só por Game.Dispatch.</summary>
public partial class CountryPanel : PanelContainer
{
    /// <summary>O Hud liga isto à árvore de focos (o painel não conhece os outros painéis).</summary>
    public Action<int>? OnFocusTree;

    /// <summary>E isto à árvore das escolas de guerra (doutrinas de exército).</summary>
    public Action<int>? OnDoctrines;

    /// <summary>Último gráfico desenhado, só para o --smoke lhe poder mexer na métrica e na mira.</summary>
    private HistoryChart? _chart;

    /// <summary>As abas do painel. O painel do país era uma tira única com tudo lá dentro — ficha, exército,
    /// diplomacia, laboratórios — e via-se um terço de cada vez, à custa de rolar. Agora arruma-se por
    /// secções, como no HoI4, na mesma chapa de latão que já divide o painel da Guerra.</summary>
    private static readonly string[] Sections = { "Nação", "Guerra", "Diplomacia", "Ciência" };

    private Game _game = null!;
    private Label _title = null!;
    private TextureRect _flag = null!;
    private VBoxContainer _body = null!;
    private HBoxContainer _tabs = null!;
    /// <summary>Aba aberta (índice em Sections). Entra na chave do cache: sem isso, trocar de aba não
    /// mudava nada no ecrã.</summary>
    private int _tab;
    /// <summary>Arma do estado-maior à vista (índice em World.Domains): terra, ar ou mar.</summary>
    private int _staffArm;
    private int _countryId;
    private string _lastKey = "";

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.55f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 0.95f)));
        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        _flag = Flags.Rect(30); head.AddChild(_flag);
        _title = Ui.Grow(Ui.Lbl("", 22)); head.AddChild(_title);
        head.AddChild(Ui.Btn("Fechar", Close));
        _tabs = new HBoxContainer(); v.AddChild(_tabs);   // a fila de abas de metal, enchida no Fill
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Open(int countryId) { _countryId = countryId; _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); }); }
    public void Refresh() { if (Visible) Fill(); }

    /// <summary>--smoke: enche o painel do país sem esperar pelo RunWhenIdle e devolve quantas divisões
    /// entraram na folha de serviço, para o arranque provar que os cartões se desenham mesmo.</summary>
    public int Smoke(int countryId)
    {
        _countryId = countryId; _lastKey = "";
        Visible = true; Fill();
        var w = _game.World;
        return w.Divisions.Values.Count(d => d.CountryId == countryId && (d.Honour is not null || d.Medals.Count > 0));
    }
    /// <summary>Só para o --smoke: passa o gráfico para a nota de potência e pousa-lhe a mira no último dia,
    /// para o desenho novo (mancha do jogador, vertical, tabela flutuante) ser percorrido sem ecrã.</summary>
    public int SmokeChart()
    {
        if (_chart is null) return 0;
        _chart.SetMetric("power");
        int day = _chart.SmokeCursor();
        _chart.QueueRedraw();
        return day;
    }

    public void Close() => Visible = false;

    /// <summary>Carregar numa aba: guarda-a e volta a encher (o mundo pode estar a correr, daí o RunWhenIdle).</summary>
    private void Pick(int i) { _tab = i; _lastKey = ""; _game.RunWhenIdle(Fill); }

    /// <summary>Troca a arma do estado-maior sem sair do separador da guerra.</summary>
    private void PickArm(int i) { _staffArm = i; _lastKey = ""; _game.RunWhenIdle(Fill); }

    /// <summary>--smoke: passa por todas as abas para nenhuma secção ficar por desenhar (e, na da guerra,
    /// pelas três armas do estado-maior), e volta ao princípio.</summary>
    public int SmokeTabs()
    {
        for (int i = 0; i < Sections.Length; i++)
        {
            _tab = i;
            for (int arm = 0; arm < World.Domains.Length; arm++) { _staffArm = arm; _lastKey = ""; Fill(); }
        }
        _tab = 0; _staffArm = 0; _lastKey = "";
        return Sections.Length;
    }

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (!w.Countries.TryGetValue(_countryId, out var c)) { Close(); return; }
            bool mine = _game.PlayerId == c.Id;
            // ocupação: a política que aplicamos a este povo e quanto lhe ferve a rua entram na chave, senão
            // o cartão fica com a política velha depois de a assinarmos
            string occKey = _game.PlayerId is int po && po != c.Id
                ? $"{OccupationSystem.Regions(w, po, c.Id)}:{OccupationSystem.Policy(w, po, c.Id).Id}:{(int)(OccupationSystem.Heat(w, po, c.Id) * 20f)}:{OccupationSystem.Since(w, po, c.Id)}"
                : "";
            var key = $"{_tab}:{_staffArm}|{c.Id}|{mine}|occ{occKey}|{string.Join(",", c.Research.Select(kv => kv.Key + ":" + (int)kv.Value))}|{c.Techs.Count}|{c.CurrentFocus}|{(int)c.FocusProgress}|{c.FocusesDone.Count}|{(int)c.Stability}|{c.JustifyTarget}|{(int)c.JustifyProgress}|{string.Join(",", w.Factions.Values.Select(f => f.Id + ":" + f.Members.Count))}|{string.Join(",", c.Laws.Select(kv => kv.Key + ":" + kv.Value))}|{string.Join(",", w.ActiveSpyOps.Where(o => o.TargetCountryId == c.Id || o.CountryId == c.Id).Select(o => o.OpId + ":" + (int)o.DaysLeft))}|{(_game.PlayerId is int pi && w.HasIntel(pi, c.Id) ? "i" + (int)c.Money : "")}|{(_game.PlayerId is int pp && w.HasPact(pp, c.Id) ? "p" : "")}|d{w.Divisions.Count}|xp{(int)c.ArmyXp}:{string.Join(",", c.Doctrines.OrderBy(x => x))}|a{(int)c.AirPower}|nv{c.Warships:0.#}:{NavalMissionSystem.Assigned(w, c.Id):0.#}|cv{ConvoySystem.Available(w, c.Id):0.#}:{ConvoySystem.SupplyNeed(w, c.Id) + ConvoySystem.TradeNeed(w, c.Id):0.#}:{ConvoySystem.GroundedCount(w, c.Id)}|n{c.Nukes}|h{w.History.Count}|dec{w.ActiveDecisions.Count}:{w.Clock.Day}|med{w.Divisions.Values.Where(d => d.CountryId == c.Id).Sum(d => d.Medals.Count)}|hon{w.Divisions.Values.Count(d => d.CountryId == c.Id && d.Honour is not null)}|pri{c.Prisoners.Values.Sum()}|cais{(int)c.PortCapacity}:{c.SeaSupplied}|fer{string.Join(",", c.GeneralWound.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + Math.Max(0, kv.Value - w.Clock.Day)))}|gen{c.Generals.Count}:{string.Join(",", w.ArmyGroups.Values.Where(g => g.CountryId == c.Id).Select(g => g.Id + ">" + g.GeneralId))}|tr{string.Join(",", w.TradeDeals.Where(t => t.BuyerId == c.Id || t.SellerId == c.Id).Select(t => t.ResourceId + (int)t.Units + ":" + (int)t.PricePerUnit + ":" + t.UntilDay))}|gov{string.Join(",", c.Cabinet.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + kv.Value))}:{(int)CabinetSystem.Wages(w, c)}|sp{(_game.PlayerId is int spy && !mine && w.AreAtWar(spy, c.Id) ? PeaceSpoils.Points(w, spy, c.Id) : 0f):0}|o{w.Regions.Values.Count(r => r.Building || r.FortBuilding || r.Project is not null)}:{(int)w.Regions.Values.Sum(r => r.BuildProgress + r.FortProgress + r.ProjectProgress)}";
            if (key == _lastKey) return;
            _lastKey = key;
            _flag.Texture = Flags.Of(c.Tag);
            _flag.Visible = _flag.Texture is not null;
            _title.Text = $"{c.Name} ({c.Tag})" + (mine ? "  — o teu país" : "");
            Ui.Clear(_body);
            Ui.Clear(_tabs);
            _tabs.AddChild(Ui.Tabs(Sections, _tab, Pick));
            // que secções é que esta aba deixa desenhar
            bool tNation = _tab == 0, tWar = _tab == 1, tDip = _tab == 2, tSci = _tab == 3;

            // comparação directa: a pergunta antes de declarar guerra ("nós contra eles, como estamos?")
            if (tNation && !mine && _game.PlayerId is not null)
            {
                int other = c.Id;
                var cmp = new HBoxContainer(); cmp.AddThemeConstantOverride("separation", 8);
                cmp.AddChild(Ui.Btn("⚖ Comparar com o nosso país", () => GetParent<Hud>().OpenCompare(other), 320, Ui.Kind.Primary));
                _body.AddChild(cmp);
            }

            // ficha e espíritos: quem os mostra é só a aba da Nação, por isso só ela vai à base
            CountryInfo? info = null; IReadOnlyList<NationalSpirit> spirits = Array.Empty<NationalSpirit>();
            if (tNation)
                try { info = _game.WorldRepo.GetCountryInfo(c.Tag); spirits = _game.WorldRepo.GetSpirits(c.Tag); }
                catch (Exception ex) { GD.PushError("country_info: " + ex.Message); }
            // ficha, tabela mundial e as contas do país: a aba de casa
            if (tNation)
            {
                if (info is not null)
                {
                    if (info.Government.Length > 0) Line($"Governo: {info.Government}" + (info.Leader.Length > 0 ? $"   ·   {info.Leader}" : ""));
                    if (info.Alliance.Length > 0) Line($"Aliança: {info.Alliance}");
                    if (info.Doctrine.Length > 0) Line($"Doutrina: {info.Doctrine}");
                    if (info.Description.Length > 0) Wrap(info.Description, 17);
                }
                // lugar na tabela mundial: o painel do país dizia tudo menos onde ele está entre os outros
                if (c.PowerRank > 0)
                {
                    string move = c.PowerRankPrev > 0 && c.PowerRankPrev != c.PowerRank
                        ? c.PowerRank < c.PowerRankPrev ? $"  ▲{c.PowerRankPrev - c.PowerRank}" : $"  ▼{c.PowerRank - c.PowerRankPrev}"
                        : "";
                    var rank = Ui.Lbl($"🌍 {c.PowerRank}.º do mundo · nota {c.PowerScore:0.0}{move}", 17);
                    rank.AddThemeColorOverride("font_color", c.PowerRank <= 3 ? new Color(1f, 0.82f, 0.25f) : Ui.Text);
                    _body.AddChild(rank);
                }
                Line($"Indústria ×{c.Stat("industry"):0.00}   Produção ×{c.Stat("production_speed"):0.00}   Organização ×{c.Stat("org_regain"):0.00}   Investigação ×{c.Stat("research_speed"):0.00}");
                Line($"Divisões {w.Divisions.Values.Count(d => d.CountryId == c.Id)}   ·   Regiões {w.Regions.Values.Count(r => r.ControllerId == c.Id)}   ·   Rendimento {EconomySystem.Income(w, c.Id):0.0}/dia");
                if (w.ResourceDefs.Count > 0)
                {
                    var parts = w.ResourceDefs.Values.OrderBy(d => d.Id)
                        .Select(d => (d, units: ResourceSystem.Controlled(w, c.Id, d.Id)))
                        .Where(t => t.units > 0f)
                        .Select(t => $"{t.d.Name} {t.units:0} (+{MathF.Min(t.units, t.d.Cap) * t.d.PerUnit:P0} {StatName(t.d.StatKey)})");
                    var txt = string.Join("   ·   ", parts);
                    if (txt.Length > 0) Line("Recursos: " + txt);
                }
                if (!mine && _game.PlayerId is int me && w.Countries.TryGetValue(me, out var my))
                {
                    string Cmp(string k) { float d = c.Stat(k) - my.Stat(k); return MathF.Abs(d) < 0.005f ? "=" : d > 0 ? "▲" : "▼"; }
                    int cd = w.Divisions.Values.Count(d => d.CountryId == c.Id), md = w.Divisions.Values.Count(d => d.CountryId == me);
                    Line($"vs {my.Tag}: indústria {Cmp("industry")}  produção {Cmp("production_speed")}  investigação {Cmp("research_speed")}  divisões {cd}/{md}", 15);
                }
                Line($"Estabilidade {c.Stability:0}%   ·   Homens {(c.Manpower < 0 ? "—" : c.Manpower >= 1e6f ? $"{c.Manpower / 1e6f:0.0}M" : $"{c.Manpower / 1e3f:0}k")}");
                if (c.WarExhaustion >= 1f) Line($"Desgaste de guerra: −{c.WarExhaustion:0} estabilidade");
            }
            // as três armas e o que se compra para elas: a aba da guerra
            if (tWar)
            {
                if (c.AirPower > 0f || mine)
                {
                    var arow = new HBoxContainer();
                    arow.AddChild(Ui.Grow(Ui.Lbl($"✈ Poder aéreo: {c.AirPower:0} esquadrões", 16)));
                    if (mine) arow.AddChild(Ui.Btn($"Comprar esquadrão ({_game.World.Rule("air_wing_cost", 60f):0})",
                        () => Faction(new BuyAirWingCommand(c.Id)), 260));
                    _body.AddChild(arow);
                }
                if (c.Warships > 0f || mine)
                {
                    // a frota ao lado do poder aéreo: as duas compram-se aqui e mandam-se no painel da Guerra
                    var nrow = new HBoxContainer();
                    nrow.AddChild(Ui.Grow(Ui.Lbl($"⚓ Frota: {c.Warships:0} navios"
                        + (c.Warships > 0f ? $" ({NavalMissionSystem.Assigned(w, c.Id):0.#} no mar)" : ""), 16)));
                    if (mine) nrow.AddChild(Ui.Btn($"Comprar navio ({_game.World.Rule("naval_ship_cost", 90f):0})",
                        () => Faction(new BuyWarshipCommand(c.Id)), 260));
                    _body.AddChild(nrow);
                }
                {
                    // marinha mercante: o que carrega o abastecimento por mar e as importações, e o que o
                    // bloqueio inimigo vai afundando — um número que só se vê quando já falta
                    float holds = ConvoySystem.Available(w, c.Id);
                    float taken = ConvoySystem.SupplyNeed(w, c.Id) + ConvoySystem.TradeNeed(w, c.Id);
                    int stuck = ConvoySystem.GroundedCount(w, c.Id);
                    var crow = new HBoxContainer();
                    var lbl = Ui.Lbl($"⛵ Marinha mercante: {holds:0} comboios ({taken:0} ocupados"
                                     + (stuck > 0 ? $", {stuck} contrato{(stuck == 1 ? "" : "s")} parado{(stuck == 1 ? "" : "s")}" : "") + ")", 16);
                    if (stuck > 0 || taken > holds) lbl.AddThemeColorOverride("font_color", Ui.Danger);
                    crow.AddChild(Ui.Grow(lbl));
                    if (mine) crow.AddChild(Ui.Btn($"Comprar comboio ({_game.World.Rule("convoy_cost", 25f):0})",
                        () => Faction(new BuyConvoyCommand(c.Id)), 260));
                    _body.AddChild(crow);
                }
                if (c.Nukes > 0 || (mine && c.Stat("nuclear") > 1f))
                {
                    var nrow = new HBoxContainer();
                    nrow.AddChild(Ui.Grow(Ui.Lbl($"☢ Ogivas nucleares: {c.Nukes}", 16)));
                    if (mine && c.Stat("nuclear") > 1f)
                        nrow.AddChild(Ui.Btn($"Construir ogiva ({_game.World.Rule("nuke_cost", 400f):0})",
                            () => Faction(new BuildNukeCommand(c.Id)), 260));
                    _body.AddChild(nrow);
                }
                if (mine && c.AtWarWith.Count > 0)
                    _body.AddChild(Ui.Btn("⚔ Guarnecer fronteiras", GarrisonFronts, 300));
            }

            // estado-maior: a folha de comandantes (retratos, divisas, carreira) e quem falta contratar
            if (tWar && mine && w.GeneralDefs.Count > 0)
            {
                Header($"Estado-maior ({string.Join(" · ", World.Domains.Select(d => $"{w.GeneralsInService(c, d)}/{w.GeneralSlots(d)}"))})");
                string arm = World.Domains[Math.Clamp(_staffArm, 0, World.Domains.Length - 1)];
                if (CommanderView.Roster(w, c, mine, arm,
                        gid => Faction(new HireGeneralCommand(c.Id, gid)),
                        gid => Faction(new DismissGeneralCommand(c.Id, gid)), PickArm) is PanelContainer staff)
                    _body.AddChild(staff);
                // a enfermaria só aparece quando há quem lá esteja: é o aviso de que há exércitos por comandar
                if (CommanderView.Infirmary(w, c.Id) is PanelContainer sick) _body.AddChild(sick);
            }

            // cais: o que os portos aguentam do outro lado do mar (e o aviso quando não aguentam)
            if (tWar && mine && PortView.Card(w, c.Id) is PanelContainer quay)
            {
                Header("Cais e mar");
                _body.AddChild(quay);
            }

            // campos de prisioneiros: quem guardamos e o que rende à indústria
            if (tWar && mine && PrisonerView.Camps(w, c.Id) is PanelContainer camps)
            {
                Header("Prisioneiros de guerra");
                _body.AddChild(camps);
            }

            // ocupação: a lei que aplicamos ao povo desta terra enquanto lhe mandarmos nela. Aparece no
            // painel de quem é dono da terra — é o povo dele que sofre a política, não o nosso
            if (tWar && !mine && _game.PlayerId is int occupier
                && OccupationView.Card(w, occupier, c.Id, id => Faction(new SetOccupationPolicyCommand(occupier, c.Id, id))) is PanelContainer occ)
            {
                Header("Ocupação");
                _body.AddChild(occ);
            }

            // gabinete civil: as quatro pastas do governo, com quem lá está e quem se pode chamar
            if (tNation && mine
                && CabinetView.Card(w, c, w.Clock.Day,
                                    id => Faction(new AppointAdvisorCommand(c.Id, id)),
                                    slot => Faction(new DismissAdvisorCommand(c.Id, slot))) is PanelContainer gov)
            {
                Header("Gabinete");
                _body.AddChild(gov);
            }

            // decisões nacionais (só o jogador decide)
            if (tNation && mine && w.DecisionDefs.Count > 0)
            {
                Header("Decisões");
                foreach (var def in w.DecisionDefs.Values.OrderBy(d => d.Id))
                {
                    var active = w.ActiveDecisions.FirstOrDefault(a => a.CountryId == c.Id && a.DecisionId == def.Id);
                    string eff = $"{StatName(def.StatKey)} ×{def.Mult:0.00}";
                    if (active is not null)
                        Line($"✅ {def.Name} — {eff}, faltam {active.UntilDay - w.Clock.Day + 1} dias", 16);
                    else if (c.DecisionCooldownUntil.TryGetValue(def.Id, out var until) && until > w.Clock.Day)
                        Line($"⏳ {def.Name} — disponível daqui a {until - w.Clock.Day} dias", 15);
                    else
                    {
                        var row = new HBoxContainer(); _body.AddChild(row);
                        row.AddChild(Ui.Grow(Ui.Lbl($"{def.Name} — {eff} por {def.Days} dias", 16)));
                        row.AddChild(Ui.Btn($"Activar ({def.Cost:0})", () => Faction(new ActivateDecisionCommand(c.Id, def.Id)), 160));
                    }
                }
            }

            // obras em curso nas regiões do jogador
            if (tNation && mine)
            {
                var works = new List<string>();
                foreach (var r in w.Regions.Values)
                {
                    if (r.ControllerId != c.Id) continue;
                    if (r.Building) works.Add($"{r.Name}: infraestrutura, {(int)MathF.Ceiling(w.Rule("infra_build_days", 30f) - r.BuildProgress)} dias");
                    if (r.FortBuilding) works.Add($"{r.Name}: forte {r.Fort + 1}, {(int)MathF.Ceiling(w.Rule("fort_build_days", 20f) - r.FortProgress)} dias");
                    if (r.Project is string proj && w.BuildingDefs.TryGetValue(proj, out var bd))
                        works.Add($"{r.Name}: {bd.Name} {r.Buildings.GetValueOrDefault(proj) + 1}, {(int)MathF.Ceiling(bd.Days - r.ProjectProgress)} dias");
                }
                if (works.Count > 0)
                {
                    Header("Obras em curso");
                    foreach (var t in works.OrderBy(t => t).Take(12)) Line("🏗 " + t, 15);
                    if (works.Count > 12) Line($"… e mais {works.Count - 12}", 14);
                }
            }

            // evolução (só no país do jogador; amostras do HistorySystem)
            if (tNation && mine && w.History.Count > 0)
            {
                Header("Evolução");
                var chart = new HistoryChart(); chart.Setup(_game);
                _chart = chart;
                var mrow = new HBoxContainer(); _body.AddChild(mrow);
                foreach (var (m, label) in new[] { ("divisions", "Divisões"), ("regions", "Regiões"), ("money", "Pontos"), ("power", "Potência") })
                    mrow.AddChild(Ui.Btn(label, () => chart.SetMetric(m), 110));
                _body.AddChild(Ui.Lbl("Toca no gráfico para ver os números de um dia", 14));
                _body.AddChild(chart);
                chart.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            }

            // espíritos nacionais: o que este povo tem de seu
            if (tNation)
            {
                // espíritos
                Header("Espíritos nacionais");
                if (spirits.Count == 0) Line("Nenhum (país genérico)");
                foreach (var s in spirits) { Line("• " + s.Name, 19); if (s.Description.Length > 0) Wrap("   " + s.Description, 16); }
            }

            // leis nacionais: uma escada por grupo, com o degrau em vigor aceso (LawsView)
            if (tNation && w.Laws.Count > 0)
            {
                Header("Leis");
                if (LawsView.Cards(w, c, mine, lid => Faction(new ChangeLawCommand(c.Id, lid))) is VBoxContainer ladders)
                    _body.AddChild(Ui.Grow(ladders));
            }

            // facções, mesa de paz, mercado e espionagem: tudo o que se faz com os outros
            if (tDip)
            {
                // facções (alianças defensivas: declarar guerra a um membro chama os outros contra o agressor)
                Header("Facções");
                var factions = w.FactionsOf(c.Id).ToList();
                if (factions.Count == 0) Line("Nenhuma");
                foreach (var f in factions)
                {
                    var names = f.Members.Where(w.Countries.ContainsKey).Select(m => w.Countries[m].Tag).OrderBy(t => t).ToList();
                    string list = names.Count > 12 ? string.Join(", ", names.Take(12)) + ", …" : string.Join(", ", names);
                    var frow = new HBoxContainer();
                    frow.AddChild(Ui.Grow(Ui.Lbl($"{f.Name} — {names.Count} membros: {list}", 16)));
                    string fid = f.Id;
                    if (mine) frow.AddChild(Ui.Btn("Sair", () => Faction(new LeaveFactionCommand(c.Id, fid)), 100));
                    _body.AddChild(frow);
                }
                if (mine)
                {
                    foreach (var f in w.Factions.Values.Where(f => !f.Members.Contains(c.Id)).OrderBy(f => f.Name))
                    {
                        string fid = f.Id;
                        var frow = new HBoxContainer();
                        frow.AddChild(Ui.Grow(Ui.Lbl($"{f.Name} — {f.Members.Count} membros", 16)));
                        frow.AddChild(Ui.Btn("Aderir", () => Faction(new JoinFactionCommand(c.Id, fid)), 130));
                        _body.AddChild(frow);
                    }
                    _body.AddChild(Ui.Btn("＋ Fundar facção", OpenCreateFaction, 220));
                }
                else if (_game.PlayerId is int inviter)
                {
                    // balança militar contra este país: divisões e força org×HP de cada lado
                    int myDivs = 0, theirDivs = 0; float myStr = 0f, theirStr = 0f;
                    foreach (var d in w.Divisions.Values)
                    {
                        if (d.CountryId == inviter) { myDivs++; myStr += d.Org * d.Hp / 100f; }
                        else if (d.CountryId == c.Id) { theirDivs++; theirStr += d.Org * d.Hp / 100f; }
                    }
                    if (myStr + theirStr > 0f)
                    {
                        int seg = (int)MathF.Round(10f * myStr / (myStr + theirStr));
                        Line($"Balança militar: nós {myDivs} div  {new string('█', seg)}{new string('░', 10 - seg)}  {theirDivs} div eles");
                    }
                    foreach (var f in w.FactionsOf(inviter).Where(f => !f.Members.Contains(c.Id)))
                    {
                        string fid = f.Id;
                        _body.AddChild(Ui.Btn($"Convidar para {f.Name}", () => Faction(new InviteToFactionCommand(inviter, fid, c.Id)), 320));
                    }
                    if (w.AreAtWar(inviter, c.Id))
                    {
                        _body.AddChild(Ui.Btn("Propor paz branca", () => Faction(new OfferPeaceCommand(inviter, c.Id)), 260));
                        // Paz negociada: exigimos o que já ocupamos. Mostra de antemão se ele assina.
                        var held = PeaceTerms.OccupiedRegions(w, inviter, c.Id);
                        if (held.Count > 0)
                        {
                            var v = PeaceTerms.Evaluate(w, inviter, c.Id, held);
                            Line($"Exigência: {held.Count} regiões ocupadas — pressão {v.Pressure:0.00} contra preço {v.Price:0.00}");
                            Line(v.Accepted ? "✔ Nas condições de hoje, ele assina." : "✘ Ainda não cede — ocupa mais ou desgasta-o.");
                            _body.AddChild(Ui.Btn($"Exigir {held.Count} regiões e fazer paz",
                                () => Faction(new DemandPeaceCommand(inviter, c.Id, held)), 320));
                        }
                        // Conferência de paz: se ele cair, a terra dele reparte-se por pontos de espólio entre
                        // todos os que lhe fizeram guerra. Aqui vê-se com quantos pontos chegamos à mesa, quantos
                        // se sentam nela connosco e quanto custa a jóia da coroa.
                        var seats = PeaceSpoils.Table(w, c);
                        Line($"🏆 Espólio: {PeaceSpoils.Points(w, inviter, c.Id):0} pontos numa mesa de {seats.Count} vencedor{(seats.Count == 1 ? "" : "es")}", 16);
                        if (w.Regions.TryGetValue(c.CapitalRegionId, out var seat))
                            Line($"   a capital dele custa {PeaceSpoils.Cost(w, seat, true):0} pontos na conferência", 15);
                    }
                    if (!w.AreAtWar(inviter, c.Id) && w.ResourceDefs.Count > 0)
                    {
                        // Mercado: o preço já não é uma tabela — sobe com o que o vendedor tem prometido e com a
                        // guerra dele. Comprar à vista paga o preço do dia todos os dias; o tratado trava-o.
                        _body.AddChild(Ui.Head("Mercado"));
                        int term = (int)w.Rule("trade_deal_days", 180f);
                        foreach (var rd in w.ResourceDefs.Values.OrderBy(d => d.Id))
                        {
                            var deal = w.TradeDeals.FirstOrDefault(t => t.BuyerId == inviter && t.SellerId == c.Id && t.ResourceId == rd.Id);
                            if (deal is not null)
                            {
                                float paid = TradeSystem.Paid(w, deal);
                                int left = TradeSystem.DaysLeft(w, deal);
                                var row = new HBoxContainer(); _body.AddChild(row);
                                var lbl = Ui.Grow(Ui.Lbl($"{rd.Name} {deal.Units:0} a {paid:0.0}/un ({deal.Units * paid:0}/dia)"
                                                         + (left > 0 ? $" — tratado, faltam {left} dias" : " — à vista"), 16));
                                lbl.TooltipText = left > 0
                                    ? $"Preço travado à assinatura. Hoje o mercado pede {TradeSystem.Price(w, c.Id, rd.Id):0.0}/un."
                                    : "Sem prazo: paga o preço do dia e acaba quando um dos dois quiser.";
                                row.AddChild(lbl);
                                row.AddChild(Ui.Btn("Cancelar", () => Faction(new CancelTradeDealCommand(inviter, c.Id, rd.Id)), 140));
                                continue;
                            }
                            float free = ResourceSystem.Controlled(w, c.Id, rd.Id) - TradeSystem.Sold(w, c.Id, rd.Id);
                            if (free < 1f) continue;
                            float lot = MathF.Min(free, 3f);
                            float price = TradeSystem.Price(w, c.Id, rd.Id);
                            var offer = new HBoxContainer(); _body.AddChild(offer);
                            var head = Ui.Grow(Ui.Lbl($"{rd.Name} — {price:0.0}/un, livre {free:0}", 16));
                            head.TooltipText = $"Tabela {w.Rule("trade_price_per_unit", 2f):0.0}/un; sobe com o que ele já vendeu"
                                             + (w.Countries[c.Id].AtWarWith.Count > 0 ? " e com a guerra dele." : ".");
                            offer.AddChild(head);
                            offer.AddChild(Ui.Btn($"À vista {lot:0} ({lot * price:0}/dia)",
                                () => Faction(new CreateTradeDealCommand(inviter, c.Id, rd.Id, lot)), 220));
                            var pact = Ui.Btn($"Tratado {term} d", () => Faction(new CreateTradeDealCommand(inviter, c.Id, rd.Id, lot, term)), 170);
                            float need = lot * price * MathF.Min(term, w.Rule("trade_deal_deposit_days", 10f));
                            pact.Disabled = w.Countries[inviter].Money < need;
                            pact.TooltipText = $"Trava {price:0.0}/un durante {term} dias. O contrato exige {need:0} no cofre à assinatura.";
                            offer.AddChild(pact);
                        }
                    }
                    else if (w.HasPact(inviter, c.Id))
                        Line($"🤝 Pacto de não-agressão até ao dia {w.Pacts[WarGame.Core.Model.World.WarKey(inviter, c.Id)]}");
                    else if (!w.SameFaction(inviter, c.Id))
                        _body.AddChild(Ui.Btn($"Propor não-agressão ({w.Rule("nap_cost", 20f):0})", () => Faction(new ProposeNonAggressionCommand(inviter, c.Id)), 300));
                    if (w.SameFaction(inviter, c.Id))
                    {
                        var aid = new HBoxContainer();
                        aid.AddChild(Ui.Grow(Ui.Lbl("Apoio financeiro (aliado):", 16)));
                        foreach (float amt in new[] { 25f, 100f })
                        {
                            float a = amt;
                            aid.AddChild(Ui.Btn($"{a:0} pts", () => Faction(new TransferMoneyCommand(inviter, c.Id, a)), 110));
                        }
                        _body.AddChild(aid);
                    }
                    if (w.HasIntel(inviter, c.Id))
                    {
                        int until = w.Intel[(inviter, c.Id)];
                        Line($"🕵 Intel (até dia {until}): {c.Money:0} pts · {c.Manpower / 1000f:0.#}k homens · " +
                             $"{w.Divisions.Values.Count(d => d.CountryId == c.Id)} divisões · {c.Queue.Count} em produção");
                    }
                    else if (w.SpyOps.Count > 0)
                    {
                        var running = w.ActiveSpyOps.FirstOrDefault(o => o.CountryId == inviter && o.TargetCountryId == c.Id);
                        if (running is not null && w.SpyOps.TryGetValue(running.OpId, out var rop))
                            Line($"Operação em curso: {rop.Name} — {(int)MathF.Ceiling(running.DaysLeft)} dias");
                        else
                        {
                            _body.AddChild(Ui.Lbl("Espionagem:", 16));
                            foreach (var op in w.SpyOps.Values.OrderBy(o => o.Cost))
                            {
                                string oid = op.Id;
                                _body.AddChild(Ui.Btn($"{op.Name} ({op.Cost:0} pts, {op.Days} d)", () => Faction(new StartSpyOpCommand(inviter, c.Id, oid)), 340));
                            }
                        }
                    }
                }
                if (c.JustifyTarget is int jt && w.Countries.TryGetValue(jt, out var jtc))
                    Line($"A justificar guerra contra {jtc.Name}: {(int)MathF.Ceiling(w.Rule("war_justify_days", 30f) - c.JustifyProgress)} dias");
                if (c.AtWarWith.Count > 0)
                {
                    var enemies = c.AtWarWith.Where(w.Countries.ContainsKey).Select(id => w.Countries[id].Tag).OrderBy(t => t);
                    Line("Em guerra com: " + string.Join(", ", enemies));
            }
            }

            // focos, escolas de guerra e laboratórios: a aba do que se aprende
            if (tSci)
            {
                // focos nacionais (HoI4): um em curso, árvore por país
                var myFocuses = w.Focuses.Values.Where(f => f.CountryId == c.Id).OrderBy(f => f.Sort).ThenBy(f => f.Id).ToList();
                if (myFocuses.Count > 0)
                {
                    Header("Focos nacionais");
                    // a árvore desenhada é o sítio de ver o que abre o quê; aqui fica só o resumo
                    var treeRow = new HBoxContainer();
                    int who = c.Id;
                    treeRow.AddChild(Ui.Btn("⚑ Ver árvore de focos", () => { Close(); OnFocusTree?.Invoke(who); }, 260, Ui.Kind.Primary));
                    _body.AddChild(treeRow);
                    if (c.CurrentFocus is not null && w.Focuses.TryGetValue(c.CurrentFocus, out var curF))
                        Line($"Em curso: {curF.Name}   {(int)(100 * c.FocusProgress / MathF.Max(1, curF.Days))}%  ({(int)MathF.Ceiling(curF.Days - c.FocusProgress)} dias)");
                    else Line(mine ? "Nenhum em curso — escolhe um foco:" : "Nenhum em curso");
                    foreach (var f in myFocuses.Where(f => w.CanFocus(c, f.Id)))
                    {
                        string fid = f.Id;
                        var row = new HBoxContainer();
                        row.AddChild(Ui.Grow(Ui.Lbl($"{f.Name}   {f.Days} dias" + (f.Description.Length > 0 ? "\n   " + f.Description : ""), 16)));
                        if (mine && c.CurrentFocus != fid) row.AddChild(Ui.Btn("Escolher", () => PickFocus(fid), 150));
                        _body.AddChild(row);
                    }
                    var doneF = c.FocusesDone.Where(w.Focuses.ContainsKey).Select(id => w.Focuses[id].Name).OrderBy(n => n).ToList();
                    Line($"Concluídos ({doneF.Count}): " + (doneF.Count == 0 ? "nenhum" : string.Join(", ", doneF)), 16);
                }

                // escolas de guerra (doutrinas de exército): a experiência de campanha e o ramo escolhido
                if (w.ArmyDoctrines.Count > 0)
                {
                    Header("Escolas de guerra");
                    int whoD = c.Id;
                    var docRow = new HBoxContainer();
                    docRow.AddChild(Ui.Btn("⚔ Ver escolas de guerra", () => { Close(); OnDoctrines?.Invoke(whoD); }, 300, Ui.Kind.Primary));
                    _body.AddChild(docRow);
                    var br = w.DoctrineBranchOf(c);
                    Line($"Experiência de exército: {c.ArmyXp:0}   ·   "
                       + (br is null ? "sem ramo escolhido" : $"ramo {w.DoctrineBranches[br].Name}"));
                    var learned = c.Doctrines.Where(w.ArmyDoctrines.ContainsKey).Select(id => w.ArmyDoctrines[id].Name).OrderBy(n => n).ToList();
                    Line($"Aprendidas ({learned.Count}): " + (learned.Count == 0 ? "nenhuma" : string.Join(", ", learned)), 16);
                }

                if (tWar) Honours(w, c);

                // investigação
                Header("Investigação");
                // Ranhuras: uma chapa por linha de investigação, cheia ou vazia, como a fila de slots do HoI4.
                // Vê-se num relance quantos laboratórios há, o que está em cada um e quanto falta.
                int labs = ResearchSystem.Slots(w, c);
                var rack = new HBoxContainer(); rack.AddThemeConstantOverride("separation", 6); _body.AddChild(rack);
                float speed = MathF.Max(0.01f, c.Stat("research_speed"));
                var busy = c.Research.ToList();
                for (int i = 0; i < labs; i++)
                {
                    var plate = new PanelContainer();
                    var cell = new VBoxContainer(); cell.AddThemeConstantOverride("separation", 2); plate.AddChild(cell);
                    if (i < busy.Count && w.Techs.TryGetValue(busy[i].Key, out var t))
                    {
                        plate.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface, 6));
                        float done = busy[i].Value / MathF.Max(1f, t.Cost);
                        var name = Ui.Lbl(t.Name, 16); name.AddThemeColorOverride("font_color", Ui.Text); cell.AddChild(name);
                        cell.AddChild(Ui.Bar(done, Ui.Accent, 190));
                        var left = Ui.Lbl($"{(int)(100 * done)}%  ·  {(int)MathF.Ceiling((t.Cost - busy[i].Value) / speed)} dias", 14);
                        left.AddThemeColorOverride("font_color", Ui.TextDim); cell.AddChild(left);
                        if (mine)
                        {
                            string id = busy[i].Key;
                            cell.AddChild(Ui.Btn("Largar", () => Cancel(id), 190, Ui.Kind.Danger));
                        }
                    }
                    else
                    {
                        // ranhura vazia: chapa apagada, para o buraco na fila dar nas vistas
                        plate.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink, 6));
                        var name = Ui.Lbl("ranhura livre", 16); name.AddThemeColorOverride("font_color", Ui.TextDim); cell.AddChild(name);
                        cell.AddChild(Ui.Bar(0f, Ui.TextDim, 190));
                        var hint = Ui.Lbl(mine ? "escolhe abaixo" : "sem investigação", 14);
                        hint.AddThemeColorOverride("font_color", Ui.TextDim); cell.AddChild(hint);
                    }
                    rack.AddChild(plate);
                }
                if (mine && ResearchSystem.FreeSlots(w, c) == 0) Line("Laboratórios cheios: larga uma linha para abrir outra.", 15);
                foreach (var group in w.Techs.Values.Where(t => w.CanResearch(c, t.Id)).GroupBy(t => t.Branch).OrderBy(g => g.Key))
                    foreach (var t in group.OrderBy(t => t.Cost))
                    {
                        string id = t.Id;
                        var row = new HBoxContainer();
                        row.AddChild(Ui.Grow(Ui.Lbl($"{t.Branch}: {t.Name}   {t.Cost:0} dias" + (t.Description is { Length: > 0 } ? "\n   " + t.Description : ""), 16)));
                        if (mine && !c.Research.ContainsKey(id)) row.AddChild(Ui.Btn("Investigar", () => Research(id), 150));
                        _body.AddChild(row);
                    }
                var known = c.Techs.Where(w.Techs.ContainsKey).Select(id => w.Techs[id].Name).OrderBy(n => n).ToList();
                Line($"Concluídas ({known.Count}): " + (known.Count == 0 ? "nenhuma" : string.Join(", ", known)), 16);
            }
        }
        catch (Exception ex) { GD.PushError("CountryPanel.Fill: " + ex); }
    }

    /// <summary>Folha de serviço: as divisões que mais mereceram do país — primeiro as que já têm nome
    /// próprio (honra de batalha), depois as mais condecoradas. Cada uma leva o cartão completo do
    /// DivisionView, o mesmo que aparece na lista da região, para a tropa se reconhecer nos dois sítios.</summary>
    private void Honours(World w, Country c)
    {
        var top = w.Divisions.Values.Where(d => d.CountryId == c.Id && (d.Honour is not null || d.Medals.Count > 0))
            .OrderByDescending(d => d.Honour is string h ? w.HonourDefs.GetValueOrDefault(h)?.Sort ?? 0 : 0)
            .ThenByDescending(d => MedalSystem.Bonus(w, d)).ThenByDescending(d => d.Xp).ThenBy(d => d.Id)
            .Take(8).ToList();
        if (top.Count == 0) return;

        int named = top.Count(d => d.Honour is not null);
        Header(named > 0 ? $"Folha de serviço · {named} com nome próprio" : "Folha de serviço");
        foreach (var d in top) _body.AddChild(DivisionView.Card(w, d));
    }

    /// <summary>Guarnecer fronteiras: despacha DefendBordersCommand (plano de batalha simplificado).</summary>
    private void GarrisonFronts() => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new DefendBordersCommand(pid));
        _game.Notify(err ?? "Divisões paradas a caminho da frente");
        Fill();
    });

    private void Faction(WarGame.Core.Commands.ICommand cmd)
    {
        _game.RunWhenIdle(() =>
        {
            var err = _game.Dispatch(cmd);
            if (err is not null) GetParent<Hud>().Toast(err);
        });
    }

    private void OpenCreateFaction()
    {
        if (_game.PlayerId is not int pid) return;
        var dlg = new AcceptDialog { Title = "Fundar facção", OkButtonText = "Fundar" };
        var name = new LineEdit { PlaceholderText = "Nome da facção", MaxLength = 40, CustomMinimumSize = new Vector2(360, 0) };
        dlg.AddChild(name);
        dlg.Confirmed += () => Faction(new CreateFactionCommand(pid, name.Text));
        dlg.Canceled += () => dlg.QueueFree();
        dlg.Confirmed += () => dlg.QueueFree();
        AddChild(dlg);
        dlg.PopupCentered();
    }

    private void PickFocus(string focusId)
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new SelectFocusCommand(pid, focusId));
        if (err is not null) GetParent<Hud>().Toast(err);
    }

    private void Research(string techId)
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new ResearchTechCommand(pid, techId));
        if (err is not null) GetParent<Hud>().Toast(err);
    }

    /// <summary>Larga a linha de investigação e liberta a ranhura (o progresso perde-se).</summary>
    private void Cancel(string techId)
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new CancelResearchCommand(pid, techId));
        if (err is not null) GetParent<Hud>().Toast(err);
    }

    private void Header(string text)
    {
        // título de secção em versaletes de latão, como na faixa de alarmes e no mini-mapa
        _body.AddChild(Ui.Head(text, 15));
    }
    private static string StatName(string key) => key switch
    {
        "production_speed" => "produção", "industry" => "indústria", "research_speed" => "investigação",
        _ => key,
    };

    private void Line(string text, int size = 18) => _body.AddChild(Ui.Lbl(text, size));
    private void Wrap(string text, int size)
    {
        var l = Ui.Lbl(text, size); l.AutowrapMode = TextServer.AutowrapMode.Word; l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _body.AddChild(l);
    }
}
