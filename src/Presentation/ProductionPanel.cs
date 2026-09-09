using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Painel de produção do jogador: bancada de fábricas militares, modelos com custo e "+", fila com
/// % e "×". A bancada é a fila de lâmpadas do HoI4: quantas linhas de montagem existem e quantas estão a
/// trabalhar hoje — e a fila marca as encomendas que estão à espera de fábrica, que antes pareciam
/// simplesmente paradas sem explicação.
///
/// A fila e os modelos vivem em abas de metal próprias, e não uma debaixo da outra. Empilhados, numa fila
/// com uma dúzia de encomendas — cada uma com barra, quadro de fábricas e mostrador de ritmo — não se
/// chegava ao fim da lista sem passar por tudo o resto, e num telemóvel não se via a fila de todo. Cada aba
/// desliza sozinha e o painel abre naquela que interessa: a fila, ou os modelos quando não há nada
/// encomendado.
///
/// Lê o World só em Fill (mundo parado); muta só por Game.Dispatch.</summary>
public partial class ProductionPanel : PanelContainer
{
    private static readonly string[] Sections = { "Fila", "Modelos", "Armazém" };

    private Game _game = null!;
    private HBoxContainer _crest = null!, _tabs = null!;
    private VBoxContainer _templates = null!, _queue = null!, _queueBox = null!, _tmplBox = null!, _stockBox = null!, _stock = null!;
    private HBoxContainer _bench = null!;
    private ScrollContainer _scroll = null!;
    private Label _tally = null!;
    private DesignerView _designer = null!;
    private TankShopView _tank = null!;
    private VBoxContainer _tanks = null!;
    private Button _tankBtn = null!;
    private int _tab;
    private string _lastKey = "";

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
        // Bancada: ⚙ + lâmpadas + a conta em palavras. Fica fora do deslizador e por cima das abas porque é
        // o tecto de tudo o que se encomenda — vale para as duas secções.
        _bench = new HBoxContainer(); _bench.AddThemeConstantOverride("separation", 8); v.AddChild(_bench);
        _tabs = new HBoxContainer(); v.AddChild(_tabs);                 // abas de metal, enchidas no Fill
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(_scroll);
        var body = Ui.Grow(new VBoxContainer()); _scroll.AddChild(body);

        _queueBox = Ui.Grow(new VBoxContainer()); body.AddChild(_queueBox);
        var qhead = new HBoxContainer(); _queueBox.AddChild(qhead);
        qhead.AddChild(Ui.Grow(Ui.Head("Fila de produção")));
        var hint = Ui.Lbl("arrasta uma encomenda para lhe dar prioridade", 14);
        hint.AddThemeColorOverride("font_color", Ui.TextDim);
        qhead.AddChild(hint);
        _queue = new VBoxContainer(); _queueBox.AddChild(_queue);

        _tmplBox = Ui.Grow(new VBoxContainer()); body.AddChild(_tmplBox);
        var mhead = new HBoxContainer(); _tmplBox.AddChild(mhead);
        mhead.AddChild(Ui.Grow(Ui.Head("Modelos de divisão")));
        mhead.AddChild(Ui.Btn("✎ Prancheta", OpenDesigner));
        _templates = new VBoxContainer(); _tmplBox.AddChild(_templates);
        _tally = Ui.Lbl("", 14); _tally.AddThemeColorOverride("font_color", Ui.TextDim);
        _tmplBox.AddChild(_tally);

        // Armazém: o que a guerra gasta e as fábricas repõem (StockView / Warehouse)
        _stockBox = Ui.Grow(new VBoxContainer()); body.AddChild(_stockBox);
        var shead = new HBoxContainer(); _stockBox.AddChild(shead);
        shead.AddChild(Ui.Grow(Ui.Head("Armazém de material")));
        var snote = Ui.Lbl("as fábricas sem encomenda enchem-no sozinhas", 14);
        snote.AddThemeColorOverride("font_color", Ui.TextDim);
        shead.AddChild(snote);
        // a prancheta dos carros mora aqui: o que sai dela é uma marca de material, e é neste armazém que
        // ela aparece na prateleira ao lado das da tabela
        _tankBtn = Ui.Btn("✎ Carros", OpenTankShop, 132);
        shead.AddChild(_tankBtn);
        _tanks = new VBoxContainer(); _tanks.AddThemeConstantOverride("separation", 2); _stockBox.AddChild(_tanks);
        _stock = new VBoxContainer(); _stock.AddThemeConstantOverride("separation", 4); _stockBox.AddChild(_stock);

        // a prancheta por cima do painel: abre-se daqui e volta-se aqui quando o desenho está assinado
        _designer = new DesignerView { Name = "Designer" };
        AddChild(_designer);
        _designer.Setup(game, () => { _lastKey = ""; Refresh(); });
        _tank = new TankShopView { Name = "TankShop" };
        AddChild(_tank);
        _tank.Setup(game, () => { _lastKey = ""; Refresh(); });
        Show(0);
    }

    /// <summary>Troca de secção. As duas ficam montadas e só se acende uma: assim a fila fica com o
    /// deslizador inteiro para ela, e voltar atrás não a manda desenhar outra vez.</summary>
    private void Show(int tab)
    {
        _tab = Math.Clamp(tab, 0, Sections.Length - 1);
        _queueBox.Visible = _tab == 0;
        _tmplBox.Visible = _tab == 1;
        _stockBox.Visible = _tab == 2;
        _scroll.ScrollVertical = 0;
    }

    private void Pick(int i) { Show(i); _lastKey = ""; _game.RunWhenIdle(Fill); }

    /// <summary>Abre na fila; com a fila vazia abre nos modelos, que é onde está o "+" de quem vem do
    /// alarme da fila parada.</summary>
    public void Open()
    {
        _lastKey = "";
        _game.RunWhenIdle(() =>
        {
            bool empty = _game.PlayerId is int pid && _game.World.Countries.TryGetValue(pid, out var c) && c.Queue.Count == 0;
            Show(empty ? 1 : 0);
            Fill();
            Visible = true;
            Ui.FadeIn(this);
        });
    }

    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) { Close(); return; }
            IReadOnlyList<DivisionTemplate> tmpls;
            try { tmpls = w.Units.GetTemplates(pid); } catch (Exception ex) { GD.PushError("templates: " + ex.Message); tmpls = Array.Empty<DivisionTemplate>(); }
            var y = Industry.Of(w, pid);
            var key = _tab + "#" + string.Join("|", tmpls.Select(t => t.Id)) + "#" + string.Join("|", c.Queue.Select(o => o.TemplateId + ":" + Pct(w, o) + (o.Repeat ? "R" : "") + "f" + o.Factories + "e" + Mathf.RoundToInt(o.Efficiency * 100f))) + "#" + (int)(c.Manpower / 1000f) + "#" + y.MilitaryBusy + "/" + y.Military + "#" + Mathf.RoundToInt(Warehouse.Average(w, c) * 100f) + "#" + Mathf.RoundToInt(c.Stock.Values.Sum())
                + "#" + string.Join("|", TankShop.Of(w, pid).Select(d => d.Id + ":" + d.Chassis + ":" + string.Join(",", d.Modules))) + "#" + Mathf.RoundToInt(c.ArmyXp);
            if (key == _lastKey) return;
            _lastKey = key;

            Ui.CrestInto(_crest, c.Tag, "Produção",
                $"{c.Money:0.0} pts no cofre  ·  {(c.Manpower < 0 ? "—" : c.Manpower >= 1e6f ? $"{c.Manpower / 1e6f:0.0}M" : $"{c.Manpower / 1e3f:0}k")} homens"
                + $"  ·  {c.Queue.Count} na fila");
            Ui.Clear(_tabs);
            _tabs.AddChild(Ui.Tabs(new[] { $"Fila ({c.Queue.Count})", $"Modelos ({tmpls.Count})", $"Armazém ({Warehouse.Average(w, c):P0})" }, _tab, Pick));
            Ui.Clear(_templates); Ui.Clear(_queue); Ui.Clear(_bench); Ui.Clear(_stock); Ui.Clear(_tanks);
            Tanks(w, c);
            _stock.AddChild(StockView.Sheet(w, c, OrderKit));
            var gear = Ui.Lbl("⚙", 18); gear.AddThemeColorOverride("font_color", Ui.Accent); _bench.AddChild(gear);
            _bench.AddChild(Ui.Pips(y.MilitaryBusy, y.Military));
            var lines = Ui.Lbl(y.Military == 0 ? "sem fábricas militares"
                               : $"{y.MilitaryBusy} de {y.Military} linhas de montagem a trabalhar"
                                 + (y.FreeMilitary > 0 ? $"  ·  {y.FreeMilitary} por atribuir"
                                    : Industry.LinesBusy(w, c) > y.Military ? "  ·  o resto da fila espera vez" : "")
                                 + (c.Queue.Count > 0 ? $"  ·  ritmo médio {c.Queue.Average(o => o.Efficiency):P0}" : ""), 16);
            lines.AddThemeColorOverride("font_color", Ui.TextDim);
            _bench.AddChild(Ui.Grow(lines));
            foreach (var t in tmpls)
            {
                float cost; try { cost = w.TemplateCost(t.Id); } catch { cost = 0f; }
                int tid = t.Id;
                bool mine = w.CustomTemplateIds.Contains(tid);       // desenhado em jogo: redesenha-se
                var sheet = TemplateDesign.OfTemplate(w, tid);
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(UnitSymbol.For(w, tid));
                var col = new VBoxContainer();
                col.AddChild(Ui.Lbl($"{t.Name}   custo {cost:0.0}   ·   {cost * w.Rule("manpower_per_cost", 500f) / 1000f:0.0}k homens"));
                // o que o modelo é, lido dos números: a lista dizia o nome que alguém lhe deu e mais nada
                var what = Ui.Lbl($"{sheet.Line}+{sheet.Support} batalhões  ·  {sheet.Role}"
                                  + (sheet.Bad ? "  ·  ⚠ a prancheta tem reparos" : ""), 13);
                what.AddThemeColorOverride("font_color", sheet.Bad ? Ui.Danger.Lightened(0.25f) : Ui.TextDim);
                col.AddChild(what);
                row.AddChild(Ui.Grow(col));
                var edit = Ui.Btn(mine ? "✎" : "⧉", () => _designer.Open(tid, edit: mine), 64);
                edit.TooltipText = mine ? "redesenhar este modelo" : "copiar para uma prancheta nova";
                row.AddChild(edit);
                row.AddChild(Ui.Btn("+", () => Order(tid), 72));
                _templates.AddChild(row);
            }
            if (tmpls.Count == 0) _templates.AddChild(Ui.Lbl("Sem modelos de divisão"));
            _tally.Text = c.Queue.Count == 0
                ? "Nada encomendado — carrega no + de um modelo para o pôr na fila."
                : $"{c.Queue.Count} encomenda{(c.Queue.Count == 1 ? "" : "s")} na fila, na aba ao lado.";
            // a conta da encomenda da frente, em chapas: é a que está a gastar as fábricas hoje
            if (PlanView.Card(w, c, 0) is PanelContainer plan) _queue.AddChild(plan);
            for (int i = 0; i < c.Queue.Count; i++)
            {
                var o = c.Queue[i]; int idx = i, tid = o.TemplateId;
                string name = OrderName(w, o);
                float qcost = w.OrderCost(o);
                bool waitingMen = !o.IsKit && Pct(w, o) >= 100 && c.Manpower < qcost * w.Rule("manpower_per_cost", 500f);
                // as fábricas repartem-se de cima para baixo (ProductionPlan.LinesFor, a mesma repartição
                // que a fábrica faz): quem chega e já não há nenhuma livre, espera
                int lines0 = ProductionPlan.LinesFor(w, c, i);
                int mine = Math.Max(1, lines0);
                bool waitingLine = !waitingMen && lines0 <= 0;
                bool rep = o.Repeat;

                // cada encomenda é uma chapa que se pega e se larga noutro lugar da fila (QueueRow)
                string kind = KindOf(w, o), spec = SpecOf(w, o);
                var row = new QueueRow();
                row.Bind(idx, name, kind, spec, waitingLine ? Ui.Surface.Darkened(0.35f) : Ui.Surface.Darkened(0.1f), Move);
                var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 8); row.AddChild(line);
                var grip = Ui.Lbl("⣿", 18); grip.AddThemeColorOverride("font_color", Ui.TextDim); line.AddChild(grip);
                var place = Ui.Lbl($"{idx + 1}º", 15);
                place.AddThemeColorOverride("font_color", waitingMen ? Ui.Danger : waitingLine ? Ui.TextDim : Ui.Accent);
                line.AddChild(place);
                // o que ali se fabrica, em símbolo: numa fila de uma dúzia de encomendas todas escritas
                // igual, a coluna blindada distingue-se da de infantaria sem se ler nome nenhum
                line.AddChild(UnitSymbol.Of(kind, 34f, 24f, spec));
                var cell = Ui.Grow(new VBoxContainer());
                cell.AddThemeConstantOverride("separation", 2);
                var head = Ui.Lbl($"{name}   {Pct(w, o)}%   ·   {Eta(w, c, o, waitingLine || waitingMen ? 0 : mine)}"
                                   + (o.IsKit ? $"   ·   {o.Delivered} conjunto{(o.Delivered == 1 ? "" : "s")} no armazém" : rep ? "   🔁" : "")
                                   + (waitingMen ? "   (à espera de homens)" : waitingLine ? "   (à espera de fábrica)" : ""));
                // a data não chega: o dedo em cima diz de que parcelas ela sai
                head.TooltipText = ProductionPlan.Why(w, c, o, lines0);
                head.MouseFilter = MouseFilterEnum.Stop;
                cell.AddChild(head);
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
                // ritmo da linha de montagem: a série que anda há semanas produz mais depressa do que a que
                // acabou de abrir, e é isto que o diz sem se ter de fazer a conta
                cell.AddChild(Rhythm(w, o, working: !waitingLine && !waitingMen));
                line.AddChild(cell);
                var up = Ui.Btn("▲", () => Move(idx, idx - 1), 56); up.Disabled = idx == 0; line.AddChild(up);
                var down = Ui.Btn("▼", () => Move(idx, idx + 1), 56); down.Disabled = idx == c.Queue.Count - 1; line.AddChild(down);
                if (!o.IsKit) line.AddChild(Ui.Btn("🔁", () => Repeat(idx, tid, !rep), 72));
                line.AddChild(Ui.Btn("×", () => Cancel(idx, tid), 72));
                _queue.AddChild(row);
            }
            if (c.Queue.Count == 0) _queue.AddChild(Ui.Lbl("Fila vazia"));
        }
        catch (Exception ex) { GD.PushError("ProductionPanel.Fill: " + ex); }
    }


    /// <summary>Os carros desenhados em casa, por cima das prateleiras do armazém: uma linha por desenho com
    /// a ficha em números, o "✎" para lhe voltar a mexer e o "×" para o deitar abaixo. Sem nenhum desenhado
    /// diz-se o que a prancheta faz e quanto pede — a mecânica não pode viver escondida atrás de um botão
    /// sem explicação.</summary>
    private void Tanks(World w, Country c)
    {
        var mine = TankShop.Of(w, c.Id);
        _tankBtn.Text = $"✎ Carros ({c.ArmyXp:0} xp)";
        _tankBtn.TooltipText = "A prancheta dos carros: desenha a geração seguinte de material blindado à peça.";
        if (mine.Count == 0)
        {
            var none = Ui.Wrapped($"Sem carros de casa. A prancheta desenha a geração seguinte de material "
                                + $"blindado à peça, por {TankShop.Price(w, false):0} de experiência de exército.", 620f, 14);
            none.AddThemeColorOverride("font_color", Ui.TextDim);
            _tanks.AddChild(none);
            return;
        }
        foreach (var d in mine)
        {
            int did = d.Id;
            if (!w.EquipmentMarks.TryGetValue(TankShop.MarkId(did), out var mark)) continue;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Glyph.Make(mark.Glyph, 24f, Ui.Accent, mark.Note));
            var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 0);
            col.AddChild(Ui.Lbl(TankShop.Short(w, mark), 15));
            var note = Ui.Lbl(mark.Note, 13);
            note.AddThemeColorOverride("font_color", Ui.TextDim);
            col.AddChild(note);
            row.AddChild(Ui.Grow(col));
            row.AddChild(Ui.Btn("✎", () => _tank.Open(did), 56));
            row.AddChild(Ui.Btn("×", () => ScrapTank(did), 56));
            _tanks.AddChild(row);
        }
    }

    /// <summary>Abre a prancheta dos carros, em branco.</summary>
    private void OpenTankShop() => _tank.Open();

    /// <summary>Deita abaixo um carro de casa. O material já feito fica no armazém — o comando recusa se
    /// houver linha a fazê-lo, e é ele que o diz.</summary>
    private void ScrapTank(int designId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new ScrapTankDesignCommand(pid, designId));
        if (err is not null) _game.Notify(err);
        else { _game.Notify("Desenho deitado abaixo"); _lastKey = ""; Refresh(); }
    });

    /// <summary>--smoke: a prancheta dos carros, provada de ponta a ponta.</summary>
    public string SmokeTank() => _tank.Smoke();

    /// <summary>O ritmo da linha, à maneira dos mostradores de fábrica do HoI4: uma calha escura com a
    /// agulha de latão a subir do ritmo de origem (100%) até ao tecto, o número por extenso e a seta a dizer
    /// para que lado vai hoje. Uma linha que ainda não entregou nada é protótipo e diz-se isso — é a mesma
    /// informação que explica porque é que a segunda unidade sai mais depressa do que a primeira.</summary>
    private static Control Rhythm(World w, ProductionOrder o, bool working)
    {
        float max = MathF.Max(1.01f, w.Rule("line_efficiency_max", 1.5f));
        float t = Math.Clamp((o.Efficiency - 1f) / (max - 1f), 0f, 1f);
        bool proto = o.Delivered <= 0;
        var tint = proto ? Ui.TextDim : Ui.Heat(0.25f + t * 0.5f);

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        var chip = Ui.Lbl(proto ? "⚙ protótipo" : working ? "▲ a ganhar ritmo" : "▼ a arrefecer", 13);
        chip.AddThemeColorOverride("font_color", proto ? Ui.TextDim : working ? Ui.Good : Ui.Danger);
        row.AddChild(chip);

        var track = new PanelContainer { CustomMinimumSize = new Vector2(120, 12) };
        track.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink, 2));
        var fill = new ColorRect { Color = tint, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        fill.CustomMinimumSize = new Vector2(MathF.Max(2f, 116f * t), 8);
        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left", 2); pad.AddThemeConstantOverride("margin_top", 2);
        pad.AddChild(fill);
        track.AddChild(pad);
        row.AddChild(track);

        var num = Ui.Lbl($"ritmo {o.Efficiency:P0} (tecto {max:P0})"
                         + (o.Delivered > 0 ? $"  ·  {o.Delivered} entregue{(o.Delivered == 1 ? "" : "s")} nesta linha" : ""), 13);
        num.AddThemeColorOverride("font_color", proto ? Ui.TextDim : Ui.Text);
        row.AddChild(Ui.Grow(num));
        return row;
    }

    /// <summary>Quando é que esta encomenda sai da fábrica, ao ritmo de hoje — com as fábricas que hoje
    /// tem, que é o que faz a data encolher quando se lhe dedicam mais, e com o jeito que a linha já tem.
    /// Uma encomenda sem linha de montagem nenhuma (lines = 0) não tem data: está parada, e dizer-lhe dias
    /// seria mentir.</summary>
    private static string Eta(World w, Country c, ProductionOrder o, int lines)
    {
        if (lines <= 0 && o.Progress < w.OrderCost(o) - 1e-3f) return "à espera de vez";
        float dias = ProductionPlan.Days(w, c, o, lines);
        if (dias < 0f) return "parada";
        if (dias <= 0f) return "pronta";
        int days = Mathf.CeilToInt(dias);
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

    /// <summary>--smoke: enche a fila com dois modelos, arrasta o último para a cabeça, carrega no quadro
    /// de fábricas da encomenda da frente e passa pelas duas abas, para os caminhos todos (arrasto,
    /// dedicação de fábricas, troca de secção) correrem sem ecrã nem dedo.</summary>
    public string Smoke()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) return "sem fila de produção";
        Pick(1); Pick(2); Pick(0);           // as três abas desenhadas, e volta-se à fila
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
        // ritmo: dá-se à mão à encomenda da frente o jeito de uma dúzia de dias de série, para o mostrador
        // acender — o motor faz isto sozinho ao fim de uma série, mas o --smoke não corre semanas
        string rhythm = "sem linha";
        if (c.Queue.Count > 0)
        {
            var lead = c.Queue[0];
            lead.Repeat = true;
            lead.Delivered = Math.Max(1, lead.Delivered);
            for (int i = 0; i < 12; i++) ProductionSystem.Age(w, lead, worked: true);
            _lastKey = ""; Fill();
            rhythm = $"linha da frente a {lead.Efficiency:P0} do ritmo de origem"
                   + $" (tecto {w.Rule("line_efficiency_max", 1.5f):P0}) depois de {lead.Delivered} entrega"
                   + (lead.Delivered == 1 ? "" : "s");
        }
        // a fila tem de caber no deslizador: se a altura pedida passar a janela, o painel desliza mesmo.
        // A janela mede-se pelo ecrã e pela âncora do painel, que é o que o Godot lhe vai dar — as medidas
        // dos containers só existem depois de um frame de layout, e o --smoke não desenha nenhum.
        float tall = _queue.GetCombinedMinimumSize().Y;
        float window = GetViewportRect().Size.Y * (1f - AnchorTop) - _bench.GetCombinedMinimumSize().Y - 120f;
        string roll = window <= 0f ? "sem janela medida"
                    : tall > window ? $"desliza ({tall:0}px de fila em {window:0}px de janela)"
                    : $"cabe inteira ({tall:0}px em {window:0}px)";
        // Os símbolos não têm como falhar alto (um Control que não desenha nada não dá erro nenhum), por
        // isso contam-se: se a fila ou os modelos perderem o símbolo, o número cai e o smoke acusa.
        var syms = Descendants<UnitSymbol>(this).ToList();
        var kinds = syms.Select(s => s.Kind).ToList();
        var specs = syms.Select(s => s.Spec).Where(s => s.Length > 0).Distinct().OrderBy(s => s).ToList();
        string symbols = kinds.Count == 0 ? "sem símbolos"
            : $"{kinds.Count} símbolos NATO ({string.Join(", ", kinds.Distinct().OrderBy(k => k).Select(k => NatoSymbol.Name(k)))}"
            + (specs.Count == 0 ? "" : $"; marcas {string.Join(", ", specs.Select(NatoSymbol.SpecMark))}") + ")";
        // armazém: abre-se uma linha de material do tipo que mais falta, pelo mesmo botão da aba
        string depot = "sem armazém";
        if (Warehouse.Neediest(w, c) is int need)
        {
            int before = c.Queue.Count(o => o.IsKit);
            OrderKit(need);                  // RunWhenIdle corre já, com o mundo parado
            _lastKey = ""; Pick(2); Fill();
            int after = c.Queue.Count(o => o.IsKit);
            string name; try { name = w.Units.GetUnitType(need).Name; } catch { name = "U" + need; }
            depot = after > before ? $"linha de material de {name} aberta pela aba do armazém" : $"linha de material de {name} recusada";
            depot += $" ({Descendants<PanelContainer>(_stock).Count()} prateleiras desenhadas)";
            Pick(0);
        }
        // e a prancheta: desenha-se uma divisão de raiz, assina-se e redesenha-se, tudo sem dedo
        string board = _designer.Smoke();
        _lastKey = ""; Fill();
        return $"{_queue.GetChildren().OfType<QueueRow>().Count()} chapas na fila de produção em {Sections.Length} abas"
             + $", {roll} ({dragged}, {yards}, {rhythm}, {depot}, {symbols}); {board}";
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
        float cost = w.OrderCost(o);
        return cost <= 0f ? 0 : Mathf.Clamp(Mathf.RoundToInt(o.Progress / cost * 100f), 0, 100);
    }

    /// <summary>O nome do que ali se fabrica: a divisão, ou o material de um tipo de unidade.</summary>
    private static string OrderName(World w, ProductionOrder o)
    {
        if (!o.IsKit) return TemplateName(w, o.TemplateId);
        try { return "Material · " + w.Units.GetUnitType(o.UnitTypeId).Name; } catch { return "Material U" + o.UnitTypeId; }
    }

    /// <summary>Símbolo NATO da encomenda: o do modelo, ou o do tipo de unidade que a linha de material
    /// arma — a fila continua a ler-se de relance mesmo cheia de linhas de material.</summary>
    private static string KindOf(World w, ProductionOrder o)
    {
        if (!o.IsKit) return UnitSymbol.KindFor(w, o.TemplateId);
        try { return UnitCounter.KindOf(w.Units.GetUnitType(o.UnitTypeId).Stats.Tags); } catch { return "support"; }
    }

    private static string SpecOf(World w, ProductionOrder o)
    {
        if (!o.IsKit) return UnitSymbol.SpecFor(w, o.TemplateId);
        try { return NatoSymbol.SpecialtyOf(w.Units.GetUnitType(o.UnitTypeId).Stats.Tags); } catch { return ""; }
    }

    /// <summary>Abre uma linha de material daquele tipo (aba do armazém).</summary>
    private void OrderKit(int unitTypeId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new BuildKitCommand(pid, unitTypeId));
        if (err is not null) _game.Notify(err);
        else { _game.Notify("Linha de material aberta"); _lastKey = ""; Refresh(); }
    });

    private void Order(int templateId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new BuildDivisionCommand(pid, templateId));
        if (err is not null) _game.Notify(err);
    });

    /// <summary>Abre a prancheta do estado-maior, em branco.</summary>
    private void OpenDesigner() => _designer.Open();

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
