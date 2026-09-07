using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Painel "Exércitos": os grupos de exércitos do jogador, cada um com a frente que lhe foi
/// atribuída, a postura e o estado das tropas em barras. Até aqui o comando de tropas era divisão a
/// divisão (ou o avanço automático, cego); aqui dá-se uma missão a um exército inteiro — "3.º Exército,
/// frente contra a Espanha, avançar" — e o ArmyGroupSystem trata da marcha.
///
/// Só lê o World e despacha comandos. As divisões entram no grupo pela selecção múltipla do mapa
/// (toque longo nas regiões, botão "Juntar selecção"), que é o gesto que o jogador já usa para as mover.</summary>
public partial class ArmyPanel : PanelContainer
{
    private Game _game = null!;
    private MapView _map = null!;
    private ArmySelect _select = null!;
    private VBoxContainer _body = null!;
    private string _lastKey = "";
    private HBoxContainer _crest = null!;
    private int? _generals;      // grupo com a lista de comandantes aberta
    /// <summary>Grupo com a lista de frentes aberta (só uma de cada vez, para o painel caber no telemóvel).</summary>
    private int? _fronts;

    public void Setup(Game game, MapView map, ArmySelect select)
    {
        _game = game; _map = map; _select = select;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 1f)));
        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        _crest = new HBoxContainer(); head.AddChild(Ui.Grow(_crest));   // brasão do nosso país, enchido no Fill
        head.AddChild(Ui.Btn("Fechar", Close));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Open() { _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); Ui.SlideIn(this); }); }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() { Visible = false; _fronts = null; _generals = null; }

    /// <summary>Só para o --smoke: cria um grupo, mete-lhe as divisões da capital, abre a lista de frentes
    /// e enche o painel — o caminho todo percorrido sem ninguém tocar no ecrã. Desfaz o que criou.</summary>
    public void Smoke()
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        if (_game.Dispatch(new CreateArmyGroupCommand(pid, "Grupo de teste")) is not null) return;
        var g = w.ArmyGroups.Values.LastOrDefault(x => x.CountryId == pid);
        if (g is null) return;
        foreach (var d in w.Divisions.Values.Where(d => d.CountryId == pid).Take(3))
            _game.Dispatch(new AssignDivisionCommand(pid, d.Id, g.Id));
        if (w.Wars.Values.FirstOrDefault(x => x.Involves(pid)) is WarInfo war)
        {
            _game.Dispatch(new SetArmyGroupFrontCommand(pid, g.Id, war.EnemyOf(pid)));
            _game.Dispatch(new SetArmyGroupStanceCommand(pid, g.Id, GroupStance.Reserve));
            _lastKey = ""; Fill();                                     // cartão da reserva com a barra de prontidão
            _game.Dispatch(new SetArmyGroupStanceCommand(pid, g.Id, GroupStance.Advance));
        }
        // sem comandante ao serviço o cartão de comando nunca era desenhado: contrata-se um só para o teste
        string? hired = null;
        if (w.Countries[pid].Generals.Count == 0
            && w.GeneralDefs.Values.OrderBy(x => x.Cost).FirstOrDefault() is GeneralDef cheap
            && _game.Dispatch(new HireGeneralCommand(pid, cheap.Id)) is null) hired = cheap.Id;
        if (w.Countries[pid].Generals.FirstOrDefault() is string gen)
            _game.Dispatch(new AssignGeneralCommand(pid, g.Id, gen));
        _fronts = g.Id; _generals = g.Id;
        _lastKey = ""; Fill();
        _fronts = null; _generals = null;
        _game.Dispatch(new DisbandArmyGroupCommand(pid, g.Id));
        if (hired is not null) _game.Dispatch(new DismissGeneralCommand(pid, hired));
        _lastKey = "";
    }

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid) { Ui.Clear(_body); _body.AddChild(Ui.Lbl("Escolhe um país primeiro", 18)); return; }
            var groups = w.ArmyGroups.Values.Where(g => g.CountryId == pid).OrderBy(g => g.Id).ToList();
            var foes = w.Wars.Values.Where(x => x.Involves(pid)).Select(x => x.EnemyOf(pid)).Distinct().ToList();

            var key = $"{w.Clock.Day}|{_fronts}|{_generals}|{_select.RegionCount}|{string.Join(",", w.Countries[pid].Generals)}|{string.Join(",", w.Countries[pid].GeneralWound.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + Math.Max(0, kv.Value - w.Clock.Day)))}|" + string.Join(",", foes) + "|" +
                      string.Join(";", groups.Select(g => $"{g.Id}:{g.Name}:{g.FrontCountryId}:{g.FrontRegionId}:{(int)g.Stance}:{g.Divisions.Count}:{g.GeneralId}:{(int)ArmyGroupSystem.Strength(w, g)}:{(int)(g.Planning * 100f)}"));
            if (key == _lastKey) return;
            _lastKey = key;
            Ui.CrestInto(_crest, w.Countries[pid].Tag, "Exércitos",
                $"{w.Divisions.Values.Count(d => d.CountryId == pid)} divisões · {groups.Count} grupos");
            Ui.Clear(_body);

            int max = (int)w.Rule("army_group_max", 6f);
            var top = new HBoxContainer();
            top.AddChild(Ui.Grow(Ui.Lbl($"Estados-maiores {groups.Count}/{max}", 15)));
            if (groups.Count < max) top.AddChild(Ui.Btn("Novo grupo", NewGroup, 160, Ui.Kind.Primary));
            _body.AddChild(top);

            _body.AddChild(Ui.Lbl(_select.RegionCount > 0
                ? $"Selecção no mapa: {_select.DivisionCount(w, pid)} divisões em {_select.RegionCount} regiões — juntas-se a um grupo abaixo"
                : "Marca regiões no mapa (toque longo) e depois junta essas divisões a um grupo", 15));

            if (groups.Count == 0)
            {
                _body.AddChild(Ui.Lbl("Sem grupos. Um grupo com frente atribuída marcha sozinho até ao inimigo\n"
                                    + "escolhido, salto a salto, pelo caminho mais curto que o mapa permitir.", 16));
                return;
            }

            var theatres = TheatreSystem.Of(w, pid);
            foreach (var g in groups) Card(w, g, pid, foes, theatres);
        }
        catch (Exception ex) { GD.PushError("ArmyPanel: " + ex); }
    }

    /// <summary>Um cartão por grupo: identidade, saúde das tropas, frente, postura e ordens.</summary>
    private void Card(World w, ArmyGroup g, int pid, List<int> foes, List<Theatre> theatres)
    {
        var card = new PanelContainer();
        bool active = g.NeedsFront && g.FrontCountryId is not null;
        var (icon, tint, _) = Face(g.Stance);
        card.AddThemeStyleboxOverride("panel", Ui.Box(active ? tint.Darkened(0.72f) with { A = 0.95f } : Ui.Surface with { A = 0.85f }, 10));
        _body.AddChild(card);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6); card.AddChild(v);

        var divs = g.Divisions.Where(id => w.Divisions.ContainsKey(id)).Select(id => w.Divisions[id]).ToList();
        var head = new HBoxContainer();
        var title = Ui.Lbl($"{icon} {g.Name}", 20);
        if (active) title.AddThemeColorOverride("font_color", tint.Lightened(0.4f));
        head.AddChild(Ui.Grow(title));
        head.AddChild(Ui.Btn("Ver", () => Look(g), 90));
        head.AddChild(Ui.Btn("Dissolver", () => Disband(pid, g.Id), 150, Ui.Kind.Danger));
        v.AddChild(head);

        if (divs.Count == 0) v.AddChild(Ui.Lbl("Grupo sem divisões", 15));
        else
        {
            float org = divs.Average(d => d.Org) / 100f, hp = divs.Average(d => d.Hp) / 100f, sup = divs.Average(d => d.Supply);
            int fighting = divs.Count(d => w.InBattle(d.Id)), moving = divs.Count(d => d.Path.Count > 0);
            v.AddChild(Ui.Lbl($"{divs.Count} divisões · {(int)ArmyGroupSystem.Strength(w, g)} de força · {fighting} em combate · {moving} a marchar", 15));
            v.AddChild(Meters(("Organização", org, Tint(org)), ("Efectivos", hp, Tint(hp)), ("Abastecimento", sup, Tint(sup))));
        }

        // frente atribuída: uma etiqueta por inimigo em guerra, a que está atribuída fica azul. Um país em
        // guerra grande tem vários troços (Theatre) — com um atribuído o grupo dedica-se só a esse, sem
        // RegionId marcha para o mais perto em toda a fronteira com o país.
        var frontRow = new HBoxContainer();
        string? theatreName = g.FrontCountryId is int tf && g.FrontRegionId is int trid
            ? theatres.Where(t => t.FoeId == tf && t.FacingId == trid).Select(t => t.Name).FirstOrDefault() : null;
        string frontName = g.FrontCountryId is int f
            ? (theatreName is not null ? $"{Name(w, f)} — {theatreName}" : Name(w, f))
            : "sem frente";
        frontRow.AddChild(Ui.Grow(Ui.Lbl("Frente: " + frontName, 16)));
        frontRow.AddChild(Ui.Btn(_fronts == g.Id ? "Fechar" : "Mudar frente", () => ToggleFronts(g.Id), 180));
        v.AddChild(frontRow);

        if (_fronts == g.Id)
        {
            if (foes.Count == 0) v.AddChild(Ui.Lbl("Sem guerras: uma frente só se atribui contra quem se combate", 15));
            else
            {
                var flow = new HFlowContainer();
                foreach (int foe in foes)
                {
                    var foeTheatres = theatres.Where(t => t.FoeId == foe).ToList();
                    if (foeTheatres.Count <= 1)
                    {
                        // só um troço (ou nenhum ainda calculado): o país inteiro É a frente, como sempre foi
                        flow.AddChild(Ui.Btn(Name(w, foe), () => SetFront(pid, g.Id, foe, null), 0,
                            g.FrontCountryId == foe && g.FrontRegionId is null ? Ui.Kind.Primary : Ui.Kind.Normal));
                    }
                    else
                    {
                        // vários troços: um botão por teatro, mais "toda a frente" p/ quem não quer escolher
                        flow.AddChild(Ui.Btn($"{Name(w, foe)} — toda a frente", () => SetFront(pid, g.Id, foe, null), 0,
                            g.FrontCountryId == foe && g.FrontRegionId is null ? Ui.Kind.Primary : Ui.Kind.Normal));
                        foreach (var t in foeTheatres)
                            flow.AddChild(Ui.Btn(t.Name, () => SetFront(pid, g.Id, foe, t.FacingId), 0,
                                g.FrontCountryId == foe && g.FrontRegionId == t.FacingId ? Ui.Kind.Primary : Ui.Kind.Normal));
                    }
                }
                if (g.FrontCountryId is not null) flow.AddChild(Ui.Btn("Sem frente", () => SetFront(pid, g.Id, null, null), 0));
                v.AddChild(flow);
            }
        }

        // postura: quatro ordens exclusivas, a que está em vigor fica acesa e explicada por baixo
        var stances = new HFlowContainer();
        foreach (var st in new[] { GroupStance.Advance, GroupStance.Defend, GroupStance.Reserve, GroupStance.Hold })
        {
            var (si, _, label) = Face(st);
            stances.AddChild(Ui.Btn($"{si} {label}", () => Stance(pid, g.Id, st), 160,
                g.Stance == st ? Ui.Kind.Primary : Ui.Kind.Normal));
        }
        v.AddChild(stances);

        var note = Ui.Lbl(Explain(g), 15);
        note.AddThemeColorOverride("font_color", g.NeedsFront && g.FrontCountryId is null ? Ui.Danger : Ui.TextDim);
        v.AddChild(note);

        // plano de batalha: o que o estado-maior preparou enquanto a frente esteve quieta. Uma seta igual
        // a esta barra está desenhada no mapa (PlanOverlay) — aqui ficam os números e o que eles valem.
        if (BattlePlanSystem.Plans(g) && divs.Count > 0)
        {
            float ready = Math.Clamp(g.Planning / MathF.Max(0.01f, w.Rule("planning_max", 1f)), 0f, 1f);
            var plan = new HBoxContainer(); plan.AddThemeConstantOverride("separation", 8);
            plan.AddChild(Ui.Lbl("🗺 Plano", 15));
            plan.AddChild(Ui.Grow(Ui.Bar(ready, ready >= 1f ? Ui.Good : Ui.Accent, 0f)));
            plan.AddChild(Ui.Lbl($"{ready:P0}", 15));
            v.AddChild(plan);

            int days = BattlePlanSystem.DaysToReady(w, g);
            float bonus = (BattlePlanSystem.Bonus(w, divs[0]) - 1f) * 100f;
            var planNote = Ui.Lbl(ready >= 1f
                ? $"Terreno estudado e eixos marcados: +{bonus:0}% de força enquanto o plano durar"
                : days > 0 ? $"Estado-maior a trabalhar: +{bonus:0}% agora, pronto em ~{days} dias de frente parada"
                           : $"+{bonus:0}% de força", 15);
            planNote.AddThemeColorOverride("font_color", ready >= 1f ? Ui.Good : Ui.TextDim);
            v.AddChild(planNote);
            int onTheMove = divs.Count(d => d.Path.Count > 0 || w.InBattle(d.Id));
            if (onTheMove > 0)
            {
                var spend = Ui.Lbl($"{onTheMove} de {divs.Count} em movimento: o plano gasta-se a ser executado", 15);
                spend.AddThemeColorOverride("font_color", Ui.Danger);
                v.AddChild(spend);
            }
        }

        // prontidão: em reserva o que interessa é saber quando é que este exército volta a servir
        if (g.Resting && divs.Count > 0)
        {
            float ready = divs.Average(d => d.Org) / 100f;
            float target = w.Rule("ai_group_ready_org", 75f) / 100f;
            var bar = Ui.Bar(Math.Clamp(ready / MathF.Max(0.01f, target), 0f, 1f), ready >= target ? Ui.Good : Ui.Accent, 0f);
            v.AddChild(bar);
            int away = divs.Count(d => d.Path.Count > 0);
            float gain = MathF.Max(0.5f, 8f * w.Countries[pid].Stat("org_regain") * w.Rule("reserve_org_bonus", 1.6f));
            int days = ready >= target ? 0 : (int)MathF.Ceiling((target - ready) * 100f / gain);
            var lbl = Ui.Lbl(ready >= target
                ? $"Recomposto e pronto a voltar à linha ({away} ainda a recolher)"
                : $"A recompor-se: {ready * 100f:0}% de organização, pronto em ~{days} dias", 15);
            lbl.AddThemeColorOverride("font_color", ready >= target ? Ui.Good : Ui.TextDim);
            v.AddChild(lbl);
        }

        // comandante destacado: o bónus dele sai do país e vem para aqui multiplicado, e cresce com o posto
        var genRow = new HBoxContainer();
        var gdef = g.GeneralId is string gid && w.GeneralDefs.TryGetValue(gid, out var found) ? found : null;
        if (gdef is null)
        {
            var none = Ui.Lbl(w.Countries[pid].GeneralWound.Count > 0
                ? "Comando vago — o comandante caiu e não havia ninguém livre para o render"
                : "Comandante: nenhum (o estado-maior serve o país todo)", 16);
            if (w.Countries[pid].GeneralWound.Count > 0) none.AddThemeColorOverride("font_color", CommanderView.Hurt);
            genRow.AddChild(Ui.Grow(none));
        }
        else
        {
            float mult = 1f + (gdef.Mult - 1f) * (w.Rule("general_command_bonus", 2f) + w.RankBonus(pid, gdef.Id));
            genRow.AddChild(Ui.Grow(CommanderView.Card(w, pid, gdef, $"{StatName(gdef.StatKey)} ×{mult:0.00} neste exército")));
        }
        var toggle = new VBoxContainer();
        toggle.AddChild(Ui.Btn(_generals == g.Id ? "Fechar" : "Comando", () => ToggleGenerals(g.Id), 170));
        genRow.AddChild(toggle);
        v.AddChild(genRow);

        if (_generals == g.Id)
        {
            var staff = w.Countries[pid].Generals;
            if (staff.Count == 0) v.AddChild(Ui.Lbl("Sem comandantes contratados — contrata no painel do país", 15));
            else
            {
                var flow = new HFlowContainer();
                foreach (string id in staff)
                {
                    if (!w.GeneralDefs.TryGetValue(id, out var def)) continue;
                    var busy = w.ArmyGroups.Values.FirstOrDefault(x => x.Id != g.Id && x.GeneralId == id);
                    // a divisa vai no botão: escolhe-se o comandante pelo posto que ele já ganhou, não só pelo stat
                    string mark = CommanderView.Insignia(w.RankOf(pid, id)?.Level ?? 1);
                    // um ferido não se destaca: o botão fica lá a dizer quantos dias faltam, mas não pega
                    int hurt = w.WoundDaysLeft(pid, id);
                    string label = hurt > 0 ? $"🩸 {def.Name} — hospital, {hurt} d"
                        : busy is null ? $"{mark} {def.Name} ({StatName(def.StatKey)})" : $"{mark} {def.Name} — {busy.Name}";
                    var pick = Ui.Btn(label, () => SetGeneral(pid, g.Id, id), 0,
                        hurt > 0 ? Ui.Kind.Danger : g.GeneralId == id ? Ui.Kind.Primary : Ui.Kind.Normal);
                    pick.Disabled = hurt > 0;
                    flow.AddChild(pick);
                }
                if (g.GeneralId is not null) flow.AddChild(Ui.Btn("Chamar de volta", () => SetGeneral(pid, g.Id, null), 0));
                v.AddChild(flow);
            }
        }

        var orders = new HBoxContainer();
        orders.AddChild(Ui.Btn($"Juntar selecção ({_select.DivisionCount(w, pid)})", () => Absorb(pid, g.Id), 230));
        if (divs.Count > 0) orders.AddChild(Ui.Btn("Largar todas", () => Release(pid, g.Id), 170));
        v.AddChild(orders);
    }

    /// <summary>Nome em português da estatística que um comandante melhora.</summary>
    private static string StatName(string key) => key switch
    {
        "attack" => "ataque",
        "defense" => "defesa",
        "org_regain" => "recuperação",
        "move_speed" => "marcha",
        "industry" => "indústria",
        _ => key,
    };

    /// <summary>Símbolo, cor e nome de cada postura — um só sítio para a UI toda concordar.</summary>
    private static (string Icon, Color Tint, string Label) Face(GroupStance s) => s switch
    {
        GroupStance.Advance => ("▶", Ui.Good, "Avançar"),
        GroupStance.Defend => ("⛨", Ui.Accent, "Defender"),
        GroupStance.Reserve => ("⏸", new Color(0.85f, 0.72f, 0.35f), "Reserva"),
        _ => ("■", Ui.TextDim, "Manter"),
    };

    /// <summary>O que este grupo vai fazer amanhã, em português — a postura sozinha não chega para se
    /// perceber que sem frente atribuída não há ordem nenhuma a cumprir.</summary>
    private static string Explain(ArmyGroup g)
    {
        if (g.Stance == GroupStance.Hold) return "Parado: as divisões ficam com as ordens que já tinham.";
        if (g.FrontCountryId is null) return "Sem frente atribuída não há para onde marchar — escolhe um inimigo.";
        return g.Stance switch
        {
            GroupStance.Advance => "Marcha até ao inimigo pelo caminho mais curto e entra-lhe em casa.",
            GroupStance.Reserve => "Recolhe à retaguarda em terreno nosso e recompõe-se mais depressa fora da linha.",
            _ => "Ocupa a última linha em território nosso e segura-a; quem estiver metido lá dentro recua.",
        };
    }

    /// <summary>Três barras lado a lado com legenda por cima — o estado do exército num relance.</summary>
    private static Control Meters(params (string Label, float Value, Color Color)[] meters)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14);
        foreach (var (label, value, color) in meters)
        {
            var col = new VBoxContainer();
            col.AddChild(Ui.Lbl($"{label} {value * 100f:0}%", 15));
            col.AddChild(Ui.Bar(Math.Clamp(value, 0f, 1f), color, 170f));
            row.AddChild(col);
        }
        return row;
    }

    private static Color Tint(float v) => v >= 0.66f ? Ui.Good : v >= 0.33f ? new Color(1f, 0.82f, 0.25f) : Ui.Danger;

    private static string Name(World w, int id) => w.Countries.TryGetValue(id, out var c) ? c.Name : "país " + id;

    // ---- ordens (tudo por Dispatch, sempre fora do tick)

    private void NewGroup() => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new CreateArmyGroupCommand(pid));
        if (err is not null) _game.Notify(err);
        _lastKey = ""; Fill();
    });

    private void ToggleFronts(int groupId) => _game.RunWhenIdle(() => { _fronts = _fronts == groupId ? null : groupId; _lastKey = ""; Fill(); });

    private void ToggleGenerals(int groupId) => _game.RunWhenIdle(() => { _generals = _generals == groupId ? null : groupId; _lastKey = ""; Fill(); });

    private void SetGeneral(int pid, int groupId, string? generalId) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new AssignGeneralCommand(pid, groupId, generalId));
        if (err is not null) { _game.Notify(err); return; }
        _generals = null; _lastKey = ""; Fill();
    });

    private void SetFront(int pid, int groupId, int? foe, int? anchor) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new SetArmyGroupFrontCommand(pid, groupId, foe, anchor));
        if (err is not null) { _game.Notify(err); return; }
        _fronts = null; Fill();
    });

    private void Stance(int pid, int groupId, GroupStance stance) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new SetArmyGroupStanceCommand(pid, groupId, stance));
        if (err is not null) { _game.Notify(err); return; }
        Fill();
    });

    /// <summary>Junta ao grupo todas as divisões do jogador nas regiões marcadas no mapa.</summary>
    private void Absorb(int pid, int groupId) => _game.RunWhenIdle(() =>
    {
        var w = _game.World;
        int n = 0;
        foreach (int rid in _select.RegionIds)
            if (w.Regions.TryGetValue(rid, out var r))
                foreach (int id in r.DivisionIds.ToList())
                    if (w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid
                        && _game.Dispatch(new AssignDivisionCommand(pid, id, groupId)) is null) n++;
        _game.Notify(n == 0 ? "Marca primeiro regiões tuas no mapa (toque longo)" : $"{n} divisões às ordens do grupo");
        _select.Clear();
        Fill();
    });

    private void Release(int pid, int groupId) => _game.RunWhenIdle(() =>
    {
        if (!_game.World.ArmyGroups.TryGetValue(groupId, out var g)) return;
        foreach (int id in g.Divisions.ToList()) _game.Dispatch(new AssignDivisionCommand(pid, id, null));
        Fill();
    });

    private void Disband(int pid, int groupId) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new DisbandArmyGroupCommand(pid, groupId));
        if (err is not null) _game.Notify(err);
        if (_fronts == groupId) _fronts = null;
        Fill();
    });

    /// <summary>Leva a câmara ao centro de gravidade do grupo — com 2700 divisões no mapa, achá-lo à mão é impossível.</summary>
    private void Look(ArmyGroup g) => _game.RunWhenIdle(() =>
    {
        var w = _game.World;
        var pts = g.Divisions.Where(id => w.Divisions.ContainsKey(id))
            .Select(id => w.Regions[w.Divisions[id].RegionId])
            .Select(r => new Vector2(r.CenterX, r.CenterY)).ToList();
        if (pts.Count == 0) { _game.Notify("Grupo sem divisões para ver"); return; }
        var mid = new Vector2(pts.Average(p => p.X), pts.Average(p => p.Y));
        _map.MoveTo(mid);
        Close();
    });
}
