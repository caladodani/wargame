using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Painel de produção do jogador (45% inferior): bancada de fábricas militares, templates com custo
/// e "+", fila com % e "×". A bancada é a fila de lâmpadas do HoI4: quantas linhas de montagem existem e
/// quantas estão a trabalhar hoje — e a fila marca as encomendas que estão à espera de fábrica, que antes
/// pareciam simplesmente paradas sem explicação. Lê o World só em Fill (mundo parado); muta só por
/// Game.Dispatch.</summary>
public partial class ProductionPanel : PanelContainer
{
    private Game _game = null!;
    private Label _title = null!;
    private VBoxContainer _templates = null!, _queue = null!;
    private HBoxContainer _bench = null!;
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
        var body = Ui.Grow(new VBoxContainer()); scroll.AddChild(body);
        // Bancada: ⚙ + lâmpadas + a conta em palavras. Fica por cima dos modelos porque é o tecto de tudo
        // o que se encomenda a seguir.
        _bench = new HBoxContainer(); _bench.AddThemeConstantOverride("separation", 8); body.AddChild(_bench);
        body.AddChild(Ui.Rule());
        var mhead = new HBoxContainer(); body.AddChild(mhead);
        mhead.AddChild(Ui.Grow(Ui.Lbl("Modelos", 20)));
        mhead.AddChild(Ui.Btn("＋ Desenhar", OpenDesigner));
        _templates = new VBoxContainer(); body.AddChild(_templates);
        var qhead = new HBoxContainer(); body.AddChild(qhead);
        qhead.AddChild(Ui.Grow(Ui.Lbl("Fila", 20)));
        var hint = Ui.Lbl("arrasta uma encomenda para lhe dar prioridade", 14);
        hint.AddThemeColorOverride("font_color", Ui.TextDim);
        qhead.AddChild(hint);
        _queue = new VBoxContainer(); body.AddChild(_queue);
    }

    public void Open() { _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); }); }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) { Close(); return; }
            _title.Text = $"Produção — {c.Name}   {c.Money:0.0} pts   ·   {(c.Manpower < 0 ? "—" : c.Manpower >= 1e6f ? $"{c.Manpower / 1e6f:0.0}M" : $"{c.Manpower / 1e3f:0}k")} homens";
            IReadOnlyList<DivisionTemplate> tmpls;
            try { tmpls = w.Units.GetTemplates(pid); } catch (Exception ex) { GD.PushError("templates: " + ex.Message); tmpls = Array.Empty<DivisionTemplate>(); }
            var y = Industry.Of(w, pid);
            var key = string.Join("|", tmpls.Select(t => t.Id)) + "#" + string.Join("|", c.Queue.Select(o => o.TemplateId + ":" + Pct(w, o) + (o.Repeat ? "R" : "") + "f" + o.Factories)) + "#" + (int)(c.Manpower / 1000f) + "#" + y.MilitaryBusy + "/" + y.Military;
            if (key == _lastKey) return;
            _lastKey = key;

            Ui.Clear(_templates); Ui.Clear(_queue); Ui.Clear(_bench);
            var gear = Ui.Lbl("⚙", 18); gear.AddThemeColorOverride("font_color", Ui.Accent); _bench.AddChild(gear);
            _bench.AddChild(Ui.Pips(y.MilitaryBusy, y.Military));
            var lines = Ui.Lbl(y.Military == 0 ? "sem fábricas militares"
                               : $"{y.MilitaryBusy} de {y.Military} linhas de montagem a trabalhar"
                                 + (y.FreeMilitary > 0 ? $"  ·  {y.FreeMilitary} por atribuir"
                                    : Industry.LinesBusy(w, c) > y.Military ? "  ·  o resto da fila espera vez" : ""), 16);
            lines.AddThemeColorOverride("font_color", Ui.TextDim);
            _bench.AddChild(Ui.Grow(lines));
            foreach (var t in tmpls)
            {
                float cost; try { cost = w.TemplateCost(t.Id); } catch { cost = 0f; }
                int tid = t.Id;
                var row = new HBoxContainer();
                row.AddChild(Ui.Grow(Ui.Lbl($"{t.Name}   custo {cost:0.0}   ·   {cost * w.Rule("manpower_per_cost", 500f) / 1000f:0.0}k homens")));
                row.AddChild(Ui.Btn("+", () => Order(tid), 72));
                _templates.AddChild(row);
            }
            if (tmpls.Count == 0) _templates.AddChild(Ui.Lbl("Sem modelos de divisão"));
            for (int i = 0; i < c.Queue.Count; i++)
            {
                var o = c.Queue[i]; int idx = i, tid = o.TemplateId;
                string name; try { name = w.Units.GetTemplate(tid).Name; } catch { name = "T" + tid; }
                float qcost; try { qcost = w.TemplateCost(tid); } catch { qcost = 0f; }
                bool waitingMen = Pct(w, o) >= 100 && c.Manpower < qcost * w.Rule("manpower_per_cost", 500f);
                // as fábricas repartem-se de cima para baixo: quem chega e já não há nenhuma livre, espera
                int ahead = FactoriesAhead(w, c, i);
                int mine = Math.Clamp(o.Factories, 1, Math.Max(1, y.Military - ahead));
                bool waitingLine = !waitingMen && ahead >= y.Military;
                bool rep = o.Repeat;

                // cada encomenda é uma chapa que se pega e se larga noutro lugar da fila (QueueRow)
                var row = new QueueRow();
                row.Bind(idx, name, waitingLine ? Ui.Surface.Darkened(0.35f) : Ui.Surface.Darkened(0.1f), Move);
                var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 8); row.AddChild(line);
                var grip = Ui.Lbl("⣿", 18); grip.AddThemeColorOverride("font_color", Ui.TextDim); line.AddChild(grip);
                var place = Ui.Lbl($"{idx + 1}º", 15);
                place.AddThemeColorOverride("font_color", waitingMen ? Ui.Danger : waitingLine ? Ui.TextDim : Ui.Accent);
                line.AddChild(place);
                var cell = Ui.Grow(new VBoxContainer());
                cell.AddThemeConstantOverride("separation", 2);
                cell.AddChild(Ui.Lbl($"{name}   {Pct(w, o)}%   ·   {Eta(w, c, o, waitingLine || waitingMen ? 0 : mine)}" + (rep ? "   🔁" : "")
                                     + (waitingMen ? "   (à espera de homens)" : waitingLine ? "   (à espera de fábrica)" : "")));
                cell.AddChild(Ui.Grow(Ui.Bar(Pct(w, o) / 100f, waitingMen ? Ui.Danger : waitingLine ? Ui.TextDim : Ui.Accent)));
                // quadro de fábricas desta encomenda: chapa acesa = fábrica dedicada, e o que se lhe dá
                // tira-se a quem vem atrás na fila
                var dial = new HBoxContainer(); dial.AddThemeConstantOverride("separation", 8);
                var knob = new FactoryDial();
                knob.Bind(mine, SetOrderFactoriesCommand.Cap(w, pid), y.FreeMilitary, n => SetFactories(idx, tid, n));
                dial.AddChild(knob);
                var lot = Ui.Lbl(mine == 1 ? "uma fábrica" : $"{mine} fábricas dedicadas", 14);
                lot.AddThemeColorOverride("font_color", mine > 1 ? Ui.Accent : Ui.TextDim);
                dial.AddChild(Ui.Grow(lot));
                cell.AddChild(dial);
                line.AddChild(cell);
                var up = Ui.Btn("▲", () => Move(idx, idx - 1), 56); up.Disabled = idx == 0; line.AddChild(up);
                var down = Ui.Btn("▼", () => Move(idx, idx + 1), 56); down.Disabled = idx == c.Queue.Count - 1; line.AddChild(down);
                line.AddChild(Ui.Btn("🔁", () => Repeat(idx, tid, !rep), 72));
                line.AddChild(Ui.Btn("×", () => Cancel(idx, tid), 72));
                _queue.AddChild(row);
            }
            if (c.Queue.Count == 0) _queue.AddChild(Ui.Lbl("Fila vazia"));
        }
        catch (Exception ex) { GD.PushError("ProductionPanel.Fill: " + ex); }
    }

    /// <summary>Quantas fábricas estão pedidas pelas encomendas por acabar à frente desta: se já forem
    /// tantas como as fábricas militares do país, esta não tem linha hoje.</summary>
    private static int FactoriesAhead(World w, Country c, int index)
    {
        int n = 0;
        for (int i = 0; i < index; i++)
        {
            float cost; try { cost = w.TemplateCost(c.Queue[i].TemplateId); } catch { cost = 0f; }
            if (c.Queue[i].Progress < cost - 1e-3f) n += Math.Max(1, c.Queue[i].Factories);
        }
        return n;
    }

    /// <summary>Quando é que esta encomenda sai da fábrica, ao ritmo de hoje — com as fábricas que hoje
    /// tem, que é o que faz a data encolher quando se lhe dedicam mais. Uma encomenda sem linha de montagem
    /// nenhuma (lines = 0) não tem data: está parada, e dizer-lhe dias seria mentir.</summary>
    private static string Eta(World w, Country c, ProductionOrder o, int lines)
    {
        float cost; try { cost = w.TemplateCost(o.TemplateId); } catch { return "—"; }
        float left = cost - o.Progress;
        if (left <= 1e-3f) return "pronta";
        if (lines <= 0) return "à espera de vez";
        float perDay = cost / MathF.Max(1f, w.Rule("build_min_days", 10f)) * c.Stat("production_speed") * lines;
        if (perDay <= 0f) return "parada";
        int days = Mathf.CeilToInt(left / perDay);
        return days == 1 ? "amanhã" : $"~{days} dias";
    }

    /// <summary>Dedica n fábricas militares a uma encomenda (chapa do FactoryDial).</summary>
    private void SetFactories(int index, int templateId, int n) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid || !_game.World.Countries.TryGetValue(pid, out var c)) return;
        if (index >= c.Queue.Count || c.Queue[index].TemplateId != templateId) { _game.Notify("A fila mudou, tenta outra vez"); Refresh(); return; }
        var err = _game.Dispatch(new SetOrderFactoriesCommand(pid, index, n));
        if (err is not null) _game.Notify(err);
        else { _lastKey = ""; Refresh(); }
    });

    /// <summary>Muda uma encomenda de lugar na fila (arrasto ou setas). Se a fila mexeu entretanto — uma
    /// encomenda entregue, por exemplo — não se arrasta a errada: pede-se outra vez.</summary>
    private void Move(int from, int to) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid || !_game.World.Countries.TryGetValue(pid, out var c)) return;
        int n = c.Queue.Count;
        if (from < 0 || to < 0 || from >= n || to >= n) { _game.Notify("A fila mudou, tenta outra vez"); Refresh(); return; }
        var err = _game.Dispatch(new MoveProductionOrderCommand(pid, from, to));
        if (err is not null) _game.Notify(err);
        else { _lastKey = ""; Refresh(); }
    });

    /// <summary>--smoke: enche a fila com dois modelos, arrasta o último para a cabeça e carrega no quadro
    /// de fábricas da encomenda da frente, para os dois caminhos novos (arrasto e dedicação de fábricas)
    /// correrem sem ecrã nem dedo.</summary>
    public string Smoke()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) return "sem fila de produção";
        IReadOnlyList<DivisionTemplate> tmpls;
        try { tmpls = w.Units.GetTemplates(pid); } catch { tmpls = Array.Empty<DivisionTemplate>(); }
        foreach (var t in tmpls.Take(2)) _game.Dispatch(new BuildDivisionCommand(pid, t.Id));
        _lastKey = ""; Fill();

        string dragged = "nada para arrastar";
        if (c.Queue.Count >= 2 && _queue.GetChildren().OfType<QueueRow>().FirstOrDefault() is QueueRow head)
        {
            string was = TemplateName(w, c.Queue[0].TemplateId);
            // --smoke: o arrasto sem dedo — a última encomenda largada sobre a primeira
            // o largar despacha o comando por si (RunWhenIdle corre já, com o mundo parado)
            if (head.Smoke(c.Queue.Count - 1))
            {
                _lastKey = ""; Fill();
                dragged = $"{TemplateName(w, c.Queue[0].TemplateId)} passou à frente de {was}";
            }
        }
        string yards = "sem quadro de fábricas";
        if (_queue.GetChildren().OfType<QueueRow>().FirstOrDefault() is QueueRow first
            && Descendants<FactoryDial>(first).FirstOrDefault() is FactoryDial knob)
        {
            int want = Math.Min(2, SetOrderFactoriesCommand.Cap(w, pid));
            // o carregar despacha o comando por si (RunWhenIdle corre já, com o mundo parado)
            knob.Smoke(want);
            _lastKey = ""; Fill();
            int got = c.Queue.Count > 0 ? c.Queue[0].Factories : 0;
            yards = got == 1 ? "cabeça da fila com uma fábrica" : $"cabeça da fila com {got} fábricas dedicadas";
        }
        return $"{_queue.GetChildren().OfType<QueueRow>().Count()} chapas na fila de produção ({dragged}, {yards})";
    }

    /// <summary>Todos os nós de um tipo por baixo deste (o quadro de fábricas vive dentro da chapa).</summary>
    private static IEnumerable<T> Descendants<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T hit) yield return hit;
            foreach (var deep in Descendants<T>(child)) yield return deep;
        }
    }

    private static string TemplateName(World w, int templateId)
    {
        try { return w.Units.GetTemplate(templateId).Name; } catch { return "T" + templateId; }
    }

    private static int Pct(World w, ProductionOrder o)
    {
        float cost; try { cost = w.TemplateCost(o.TemplateId); } catch { cost = 0f; }
        return cost <= 0f ? 0 : Mathf.Clamp(Mathf.RoundToInt(o.Progress / cost * 100f), 0, 100);
    }

    private void Order(int templateId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new BuildDivisionCommand(pid, templateId));
        if (err is not null) _game.Notify(err);
    });

    /// <summary>Desenhador de templates: um SpinBox por tipo de unidade, nome e custo ao vivo.</summary>
    private void OpenDesigner()
    {
        if (_game.PlayerId is not int pid) return;
        IReadOnlyList<UnitType> types;
        try { types = _game.World.Units.AllUnitTypes(); } catch (Exception ex) { GD.PushError("designer: " + ex.Message); return; }

        var dlg = new AcceptDialog { Title = "Desenhar template", OkButtonText = "Criar" };
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        dlg.AddChild(v);
        var name = new LineEdit { PlaceholderText = "Nome do template", MaxLength = 40 };
        v.AddChild(name);
        var costLbl = Ui.Lbl("Custo 0.0 · 0.0k homens", 18);
        v.AddChild(costLbl);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 380), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        var rows = Ui.Grow(new VBoxContainer()); scroll.AddChild(rows);

        var spins = new Dictionary<int, SpinBox>();
        void UpdateCost()
        {
            float cost = 0f;
            foreach (var t in types) if (spins[t.Id].Value > 0) cost += t.Cost * (float)spins[t.Id].Value;
            costLbl.Text = $"Custo {cost:0.0} · {cost * _game.World.Rule("manpower_per_cost", 500f) / 1000f:0.0}k homens";
        }
        foreach (var t in types)
        {
            var row = new HBoxContainer();
            row.AddChild(Ui.Grow(Ui.Lbl($"{t.Name} ({t.Cost:0.0})")));
            var spin = new SpinBox { MinValue = 0, MaxValue = 30, Value = 0, CustomMinimumSize = new Vector2(110, 0) };
            spin.ValueChanged += _ => UpdateCost();
            spins[t.Id] = spin;
            row.AddChild(spin);
            rows.AddChild(row);
        }

        dlg.Confirmed += () =>
        {
            var units = spins.Where(kv => kv.Value.Value > 0)
                             .Select(kv => (kv.Key, (int)kv.Value.Value)).ToList();
            string text = name.Text;
            _game.RunWhenIdle(() =>
            {
                var err = _game.Dispatch(new CreateTemplateCommand(pid, text, units));
                if (err is not null) _game.Notify(err);
                else { _game.Notify("Template criado"); _lastKey = ""; Refresh(); }
            });
        };
        AddChild(dlg);
        dlg.PopupCentered();
    }

    /// <summary>Marca/desmarca a encomenda como produção em série (volta à fila quando é entregue).</summary>
    private void Repeat(int index, int templateId, bool on) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid || !_game.World.Countries.TryGetValue(pid, out var c)) return;
        if (index >= c.Queue.Count || c.Queue[index].TemplateId != templateId) { _game.Notify("A fila mudou, tenta outra vez"); Refresh(); return; }
        var err = _game.Dispatch(new SetProductionRepeatCommand(pid, index, on));
        if (err is not null) _game.Notify(err);
        else { _lastKey = ""; Refresh(); }
    });

    // O índice vem da lista desenhada; se a fila mudou entretanto (encomenda concluída) não cancela outra.
    private void Cancel(int index, int templateId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid || !_game.World.Countries.TryGetValue(pid, out var c)) return;
        if (index >= c.Queue.Count || c.Queue[index].TemplateId != templateId) { _game.Notify("A fila mudou, tenta outra vez"); Refresh(); return; }
        var err = _game.Dispatch(new CancelProductionCommand(pid, index));
        if (err is not null) _game.Notify(err);
    });
}
