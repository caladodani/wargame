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
    /// <summary>Último gráfico desenhado, só para o --smoke lhe poder mexer na métrica e na mira.</summary>
    private HistoryChart? _chart;

    private Game _game = null!;
    private Label _title = null!;
    private TextureRect _flag = null!;
    private VBoxContainer _body = null!;
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

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (!w.Countries.TryGetValue(_countryId, out var c)) { Close(); return; }
            bool mine = _game.PlayerId == c.Id;
            var key = $"{c.Id}|{mine}|{c.ResearchTech}|{(int)c.ResearchProgress}|{c.Techs.Count}|{c.CurrentFocus}|{(int)c.FocusProgress}|{c.FocusesDone.Count}|{(int)c.Stability}|{c.JustifyTarget}|{(int)c.JustifyProgress}|{string.Join(",", w.Factions.Values.Select(f => f.Id + ":" + f.Members.Count))}|{string.Join(",", c.Laws.Select(kv => kv.Key + ":" + kv.Value))}|{string.Join(",", w.ActiveSpyOps.Where(o => o.TargetCountryId == c.Id || o.CountryId == c.Id).Select(o => o.OpId + ":" + (int)o.DaysLeft))}|{(_game.PlayerId is int pi && w.HasIntel(pi, c.Id) ? "i" + (int)c.Money : "")}|{(_game.PlayerId is int pp && w.HasPact(pp, c.Id) ? "p" : "")}|d{w.Divisions.Count}|a{(int)c.AirPower}|n{c.Nukes}|h{w.History.Count}|dec{w.ActiveDecisions.Count}:{w.Clock.Day}|med{w.Divisions.Values.Where(d => d.CountryId == c.Id).Sum(d => d.Medals.Count)}|hon{w.Divisions.Values.Count(d => d.CountryId == c.Id && d.Honour is not null)}|pri{c.Prisoners.Values.Sum()}|cais{(int)c.PortCapacity}:{c.SeaSupplied}|fer{string.Join(",", c.GeneralWound.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + Math.Max(0, kv.Value - w.Clock.Day)))}|gen{c.Generals.Count}:{string.Join(",", w.ArmyGroups.Values.Where(g => g.CountryId == c.Id).Select(g => g.Id + ">" + g.GeneralId))}|o{w.Regions.Values.Count(r => r.Building || r.FortBuilding || r.Project is not null)}:{(int)w.Regions.Values.Sum(r => r.BuildProgress + r.FortProgress + r.ProjectProgress)}";
            if (key == _lastKey) return;
            _lastKey = key;
            _flag.Texture = Flags.Of(c.Tag);
            _flag.Visible = _flag.Texture is not null;
            _title.Text = $"{c.Name} ({c.Tag})" + (mine ? "  — o teu país" : "");
            Ui.Clear(_body);

            // ficha
            CountryInfo? info = null; IReadOnlyList<NationalSpirit> spirits = Array.Empty<NationalSpirit>();
            try { info = _game.WorldRepo.GetCountryInfo(c.Tag); spirits = _game.WorldRepo.GetSpirits(c.Tag); }
            catch (Exception ex) { GD.PushError("country_info: " + ex.Message); }
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
            if (c.AirPower > 0f || mine)
            {
                var arow = new HBoxContainer();
                arow.AddChild(Ui.Grow(Ui.Lbl($"✈ Poder aéreo: {c.AirPower:0} esquadrões", 16)));
                if (mine) arow.AddChild(Ui.Btn($"Comprar esquadrão ({_game.World.Rule("air_wing_cost", 60f):0})",
                    () => Faction(new BuyAirWingCommand(c.Id)), 260));
                _body.AddChild(arow);
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

            // estado-maior: comandantes ao serviço e os que se podem contratar
            if (mine && w.GeneralDefs.Count > 0)
            {
                int slots = (int)w.Rule("general_slots", 3f);
                Header($"Estado-maior ({c.Generals.Count}/{slots})");
                foreach (var def in w.GeneralDefs.Values.OrderBy(g => g.Id))
                {
                    bool serving = c.Generals.Contains(def.Id);
                    // destacado a um grupo: o bónus sai daqui e vale, amplificado, só nesse exército
                    var posted = w.ArmyGroups.Values.FirstOrDefault(g => g.CountryId == c.Id && g.GeneralId == def.Id);
                    string eff = posted is null
                        ? $"{StatName(def.StatKey)} ×{def.Mult:0.00}"
                        : $"{StatName(def.StatKey)} ×{1f + (def.Mult - 1f) * (w.Rule("general_command_bonus", 2f) + w.RankBonus(c.Id, def.Id)):0.00} no {posted.Name}";
                    var row = new HBoxContainer(); _body.AddChild(row);
                    // a divisa do posto só faz sentido em quem serve: um comandante por contratar não tem folha
                    bool hurt = serving && w.IsWounded(c.Id, def.Id);
                    string mark = hurt ? CommanderView.WoundMark(w, c.Id, def.Id)
                        : posted is not null ? "⚔ " : serving ? CommanderView.Insignia(w.RankOf(c.Id, def.Id)?.Level ?? 1) + " " : "";
                    string rank = serving && CommanderView.RankName(w, c.Id, def.Id) is string rn && rn.Length > 0 ? $" · {rn}" : "";
                    var lbl = Ui.Lbl($"{mark}{def.Name}{rank} — {(hurt ? "no hospital, não conta para nada" : eff)}", 16);
                    if (serving) lbl.AddThemeColorOverride("font_color", hurt ? CommanderView.Hurt : CommanderView.Tint(w.RankOf(c.Id, def.Id)?.Level ?? 1, CommanderView.TopLevel(w)));
                    row.AddChild(Ui.Grow(lbl));
                    if (serving)
                        row.AddChild(Ui.Btn("Dispensar", () => Faction(new DismissGeneralCommand(c.Id, def.Id)), 160));
                    else if (c.Generals.Count < slots)
                        row.AddChild(Ui.Btn($"Contratar ({def.Cost:0})", () => Faction(new HireGeneralCommand(c.Id, def.Id)), 160));
                    // barra de carreira: mostra o que a guerra lhe deu e quanto falta para a promoção
                    if (serving) _body.AddChild(hurt
                        ? CommanderView.Recovery(w, c.Id, def.Id)
                        : CommanderView.Progress(w, c.Id, def.Id, CommanderView.Tint(w.RankOf(c.Id, def.Id)?.Level ?? 1, CommanderView.TopLevel(w))));
                }
                // a enfermaria só aparece quando há quem lá esteja: é o aviso de que há exércitos por comandar
                if (CommanderView.Infirmary(w, c.Id) is PanelContainer sick) _body.AddChild(sick);
            }

            // cais: o que os portos aguentam do outro lado do mar (e o aviso quando não aguentam)
            if (mine && PortView.Card(w, c.Id) is PanelContainer quay)
            {
                Header("Cais e mar");
                _body.AddChild(quay);
            }

            // campos de prisioneiros: quem guardamos e o que rende à indústria
            if (mine && PrisonerView.Camps(w, c.Id) is PanelContainer camps)
            {
                Header("Prisioneiros de guerra");
                _body.AddChild(camps);
            }

            // decisões nacionais (só o jogador decide)
            if (mine && w.DecisionDefs.Count > 0)
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
            if (mine)
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
            if (mine && w.History.Count > 0)
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

            // espíritos
            Header("Espíritos nacionais");
            if (spirits.Count == 0) Line("Nenhum (país genérico)");
            foreach (var s in spirits) { Line("• " + s.Name, 19); if (s.Description.Length > 0) Wrap("   " + s.Description, 16); }

            // leis nacionais (uma activa por grupo; mudar custa law_change_cost)
            if (w.Laws.Count > 0)
            {
                Header("Leis");
                foreach (var grp in w.Laws.Values.Select(l => l.Group).Distinct().OrderBy(g => g))
                {
                    var active = w.ActiveLaw(c, grp);
                    Line($"{(grp == "conscription" ? "Conscrição" : grp == "economy" ? "Economia" : grp)}: {active?.Name ?? "—"}", 17);
                    if (!mine) continue;
                    foreach (var l in w.Laws.Values.Where(l => l.Group == grp && l.Id != active?.Id).OrderBy(l => l.Sort))
                    {
                        string lid = l.Id;
                        var lrow = new HBoxContainer();
                        lrow.AddChild(Ui.Grow(Ui.Lbl($"   {l.Name}" + (l.Description.Length > 0 ? $" — {l.Description}" : ""), 15)));
                        lrow.AddChild(Ui.Btn($"Mudar ({w.Rule("law_change_cost", 30f):0})", () => Faction(new ChangeLawCommand(c.Id, lid)), 170));
                        _body.AddChild(lrow);
                    }
                }
            }

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
                }
                if (!w.AreAtWar(inviter, c.Id) && w.ResourceDefs.Count > 0)
                {
                    float price = w.Rule("trade_price_per_unit", 2f);
                    foreach (var rd in w.ResourceDefs.Values.OrderBy(d => d.Id))
                    {
                        var deal = w.TradeDeals.FirstOrDefault(t => t.BuyerId == inviter && t.SellerId == c.Id && t.ResourceId == rd.Id);
                        if (deal is not null)
                        {
                            var row = new HBoxContainer(); _body.AddChild(row);
                            row.AddChild(Ui.Grow(Ui.Lbl($"Compramos {rd.Name} {deal.Units:0} ({deal.Units * price:0}/dia)", 16)));
                            row.AddChild(Ui.Btn("Cancelar", () => Faction(new CancelTradeDealCommand(inviter, c.Id, rd.Id)), 140));
                            continue;
                        }
                        float free = ResourceSystem.Controlled(w, c.Id, rd.Id) - TradeSystem.Sold(w, c.Id, rd.Id);
                        if (free < 1f) continue;
                        float lot = MathF.Min(free, 3f);
                        _body.AddChild(Ui.Btn($"Comprar {rd.Name} {lot:0} ({lot * price:0}/dia)",
                            () => Faction(new CreateTradeDealCommand(inviter, c.Id, rd.Id, lot)), 300));
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

            // focos nacionais (HoI4): um em curso, árvore por país
            var myFocuses = w.Focuses.Values.Where(f => f.CountryId == c.Id).OrderBy(f => f.Sort).ThenBy(f => f.Id).ToList();
            if (myFocuses.Count > 0)
            {
                Header("Focos nacionais");
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

            Honours(w, c);

            // investigação
            Header("Investigação");
            if (c.ResearchTech is not null && w.Techs.TryGetValue(c.ResearchTech, out var cur))
                Line($"Em curso: {cur.Name}   {(int)(100 * c.ResearchProgress / MathF.Max(1f, cur.Cost))}%  ({(int)MathF.Ceiling((cur.Cost - c.ResearchProgress) / MathF.Max(0.01f, c.Stat("research_speed")))} dias)");
            else Line(mine ? "Nada em investigação — escolhe uma tecnologia:" : "Nada em investigação");
            foreach (var group in w.Techs.Values.Where(t => w.CanResearch(c, t.Id)).GroupBy(t => t.Branch).OrderBy(g => g.Key))
                foreach (var t in group.OrderBy(t => t.Cost))
                {
                    string id = t.Id;
                    var row = new HBoxContainer();
                    row.AddChild(Ui.Grow(Ui.Lbl($"{t.Branch}: {t.Name}   {t.Cost:0} dias" + (t.Description is { Length: > 0 } ? "\n   " + t.Description : ""), 16)));
                    if (mine && c.ResearchTech != id) row.AddChild(Ui.Btn("Investigar", () => Research(id), 150));
                    _body.AddChild(row);
                }
            var known = c.Techs.Where(w.Techs.ContainsKey).Select(id => w.Techs[id].Name).OrderBy(n => n).ToList();
            Line($"Concluídas ({known.Count}): " + (known.Count == 0 ? "nenhuma" : string.Join(", ", known)), 16);
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

    private void Header(string text) { var l = Ui.Lbl(text, 20); l.Modulate = new Color(1f, 0.85f, 0.4f); _body.AddChild(l); }
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
