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
    private Game _game = null!;
    private Label _title = null!;
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
        _title = Ui.Grow(Ui.Lbl("", 22)); head.AddChild(_title);
        head.AddChild(Ui.Btn("Fechar", Close));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Open(int countryId) { _countryId = countryId; _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; }); }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (!w.Countries.TryGetValue(_countryId, out var c)) { Close(); return; }
            bool mine = _game.PlayerId == c.Id;
            var key = $"{c.Id}|{mine}|{c.ResearchTech}|{(int)c.ResearchProgress}|{c.Techs.Count}|{c.CurrentFocus}|{(int)c.FocusProgress}|{c.FocusesDone.Count}|{(int)c.Stability}|{c.JustifyTarget}|{(int)c.JustifyProgress}|{string.Join(",", w.Factions.Values.Select(f => f.Id + ":" + f.Members.Count))}|{string.Join(",", c.Laws.Select(kv => kv.Key + ":" + kv.Value))}|{string.Join(",", w.ActiveSpyOps.Where(o => o.TargetCountryId == c.Id || o.CountryId == c.Id).Select(o => o.OpId + ":" + (int)o.DaysLeft))}|{(_game.PlayerId is int pi && w.HasIntel(pi, c.Id) ? "i" + (int)c.Money : "")}";
            if (key == _lastKey) return;
            _lastKey = key;
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
            Line($"Indústria ×{c.Stat("industry"):0.00}   Produção ×{c.Stat("production_speed"):0.00}   Organização ×{c.Stat("org_regain"):0.00}   Investigação ×{c.Stat("research_speed"):0.00}");
            Line($"Divisões {w.Divisions.Values.Count(d => d.CountryId == c.Id)}   ·   Regiões {w.Regions.Values.Count(r => r.ControllerId == c.Id)}   ·   Rendimento {EconomySystem.Income(w, c.Id):0.0}/dia");
            Line($"Estabilidade {c.Stability:0}%   ·   Homens {(c.Manpower < 0 ? "—" : c.Manpower >= 1e6f ? $"{c.Manpower / 1e6f:0.0}M" : $"{c.Manpower / 1e3f:0}k")}");
            if (mine && c.AtWarWith.Count > 0)
                _body.AddChild(Ui.Btn("⚔ Guarnecer fronteiras", GarrisonFronts, 300));

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
                foreach (var f in w.FactionsOf(inviter).Where(f => !f.Members.Contains(c.Id)))
                {
                    string fid = f.Id;
                    _body.AddChild(Ui.Btn($"Convidar para {f.Name}", () => Faction(new InviteToFactionCommand(inviter, fid, c.Id)), 320));
                }
                if (w.AreAtWar(inviter, c.Id))
                    _body.AddChild(Ui.Btn("Propor paz branca", () => Faction(new OfferPeaceCommand(inviter, c.Id)), 260));
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

    /// <summary>Distribui as divisões paradas (org ≥ ai_min_org, fora de batalha) pelas regiões
    /// próprias com fronteira hostil, das mais vazias para as mais cheias. Só despacha MoveDivisionCommand.</summary>
    private void GarrisonFronts() => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        var front = w.Regions.Values.Where(r => r.ControllerId == pid
                        && (r.Neighbours.Any(n => w.IsHostile(pid, w.Regions[n]))
                            || r.SeaNeighbours.Keys.Any(n => w.IsHostile(pid, w.Regions[n])))).ToList();
        if (front.Count == 0) { _game.Notify("Sem frente — nenhuma região tua toca o inimigo"); return; }
        float minOrg = w.Rule("ai_min_org", 50f);
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));
        var idle = w.Divisions.Values.Where(d => d.CountryId == pid && d.Path.Count == 0
                        && d.Org >= minOrg && d.CanFight && !inBattle.Contains(d.Id)
                        && !front.Any(f => f.Id == d.RegionId)).ToList();
        if (idle.Count == 0) { _game.Notify("Nenhuma divisão parada disponível (org baixa ou já na frente)"); return; }
        var load = front.ToDictionary(f => f.Id, f => f.DivisionIds.Count(id => w.Divisions[id].CountryId == pid));
        int sent = 0;
        foreach (var d in idle)
        {
            int dest = load.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
            if (_game.Dispatch(new MoveDivisionCommand(pid, d.Id, dest)) is not null) continue;
            load[dest]++; sent++;
        }
        _game.Notify(sent > 0 ? $"{sent} divisões a caminho da frente" : "Nenhuma divisão conseguiu caminho");
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
    private void Line(string text, int size = 18) => _body.AddChild(Ui.Lbl(text, size));
    private void Wrap(string text, int size)
    {
        var l = Ui.Lbl(text, size); l.AutowrapMode = TextServer.AutowrapMode.Word; l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _body.AddChild(l);
    }
}
