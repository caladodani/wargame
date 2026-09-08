using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A prancheta do estado-maior: onde se desenha uma divisão.
///
/// O desenhador era uma caixa de SpinBoxes numa janelinha — uma lista de nomes com números ao lado e o
/// custo em baixo. Punha-se 6 de infantaria e 2 de artilharia às cegas, e o que aquilo dava só se sabia
/// depois de a divisão estar feita e no mapa. No HoI4 desenhar uma divisão é meia campanha: os batalhões
/// entram em ranhuras que se vêem, a ficha de combate muda a cada batalhão que se acrescenta, e a margem
/// do desenho avisa quando a coisa não fura blindagem nenhuma.
///
/// É essa prancheta que aqui se recria, com desenho nosso: as ranhuras são chapas com o símbolo NATO do
/// batalhão, a ficha em cima é a mesma que o combate lê (DivisionStatCache), e os conselhos à margem vêm
/// do Core (TemplateDesign) — nada disto se decide aqui. Serve as duas mãos: desenhar de novo, e abrir um
/// modelo que já existe para lhe mexer (os feitos em jogo redesenham-se, os da doutrina copiam-se).
///
/// Lê o World só quando desenha (mundo parado) e muta só por Game.Dispatch.</summary>
public partial class DesignerView : PanelContainer
{
    private const int LinePicker = 0, SupportPicker = 1;

    private Game _game = null!;
    private LineEdit _name = null!;
    private Label _role = null!, _bill = null!, _delta = null!, _slotsNote = null!;
    private HFlowContainer _slots = null!, _sheet = null!;
    private VBoxContainer _advice = null!, _picker = null!;
    private HBoxContainer _crest = null!, _pickTabs = null!;
    private Button _save = null!;
    private ScrollContainer _scroll = null!;
    private Action? _saved;

    /// <summary>O desenho a meio: tipo de batalhão → quantos. A ordem é a de quem os pôs lá.</summary>
    private readonly List<(int UnitTypeId, int Qty)> _draft = new();
    private int? _editing;                 // modelo que se está a redesenhar (null = modelo novo)
    private List<(int UnitTypeId, int Qty)> _base = new();   // o desenho de origem, para a linha do "vs"
    private int _pick = LinePicker;

    public void Setup(Game game, Action? onSaved = null)
    {
        _game = game; _saved = onSaved;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 1f)));

        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        _crest = new HBoxContainer(); head.AddChild(Ui.Grow(_crest));
        head.AddChild(Ui.Btn("Fechar", Close));

        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(_scroll);
        var body = Ui.Grow(new VBoxContainer()); _scroll.AddChild(body);

        _name = new LineEdit { PlaceholderText = "Nome do modelo", MaxLength = 40 };
        body.AddChild(_name);

        // a ficha de combate primeiro: é o que se está a fazer subir ou descer a cada toque
        _role = Ui.Lbl("prancheta vazia", 17);
        _role.AddThemeColorOverride("font_color", Ui.Accent);
        body.AddChild(_role);
        _sheet = new HFlowContainer(); body.AddChild(_sheet);
        _bill = Ui.Lbl("", 15); _bill.AddThemeColorOverride("font_color", Ui.TextDim); body.AddChild(_bill);
        _delta = Ui.Lbl("", 15); _delta.AddThemeColorOverride("font_color", Ui.Accent); body.AddChild(_delta);

        body.AddChild(Ui.Rule());
        var sh = new HBoxContainer();
        sh.AddChild(Ui.Grow(Ui.Head("A divisão")));
        sh.AddChild(Ui.Btn("Limpar", Clear));
        body.AddChild(sh);
        _slots = new HFlowContainer();
        _slots.AddThemeConstantOverride("h_separation", 6);
        _slots.AddThemeConstantOverride("v_separation", 6);
        body.AddChild(_slots);
        _slotsNote = Ui.Lbl("", 14); _slotsNote.AddThemeColorOverride("font_color", Ui.TextDim);
        body.AddChild(_slotsNote);

        _advice = new VBoxContainer(); body.AddChild(_advice);

        body.AddChild(Ui.Rule());
        body.AddChild(Ui.Head("Pôr batalhões"));
        _pickTabs = new HBoxContainer(); body.AddChild(_pickTabs);
        _picker = new VBoxContainer(); _picker.AddThemeConstantOverride("separation", 2); body.AddChild(_picker);

        var feet = new HBoxContainer(); feet.AddThemeConstantOverride("separation", 8); v.AddChild(feet);
        _save = Ui.Btn("✚ Criar modelo", Commit);
        feet.AddChild(Ui.Grow(_save));
        feet.AddChild(Ui.Btn("Cancelar", Close));
    }

    /// <summary>Abre a prancheta em branco (modelo novo) ou com um desenho já lá dentro. `edit` a true
    /// redesenha esse modelo; a false traz-lhe os batalhões para um modelo novo (copiar a doutrina).</summary>
    public void Open(int? templateId = null, bool edit = false)
    {
        _game.RunWhenIdle(() =>
        {
            var w = _game.World;
            _draft.Clear(); _base = new List<(int, int)>();
            _editing = null; _name.Text = "";
            if (templateId is int tid)
            {
                try
                {
                    var t = w.Units.GetTemplate(tid);
                    foreach (var u in t.Units) _draft.Add(u);
                    _base = t.Units.ToList();
                    _name.Text = edit ? t.Name : t.Name + " II";
                    if (edit) _editing = tid;
                }
                catch (Exception ex) { GD.PushError("prancheta: " + ex.Message); }
            }
            _pick = LinePicker;
            Paint();
            Visible = true;
            Ui.FadeIn(this);
        });
    }

    public void Close() => Visible = false;

    /// <summary>Redesenha a prancheta toda a partir do rascunho. Não há chave de cache nenhuma: isto só
    /// muda quando um dedo lhe toca, e cada toque tem mesmo de se ver.</summary>
    private void Paint()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) { Close(); return; }
            var sheet = TemplateDesign.Of(w, pid, _draft);

            Ui.CrestInto(_crest, c.Tag, _editing is null ? "Prancheta" : "Redesenhar",
                $"{sheet.Line}+{sheet.Support} batalhões  ·  {TemplateDesign.LineMax(w)} de linha e {TemplateDesign.SupportMax(w)} de apoio no máximo");

            _role.Text = sheet.Role;
            _role.AddThemeColorOverride("font_color", sheet.Bad ? Ui.Danger.Lightened(0.25f) : Ui.Accent);
            Ui.Clear(_sheet);
            _sheet.AddChild(DivisionView.StatSheet(w, _draft.Count == 0 ? null : sheet.Stats));
            _bill.Text = $"custo {sheet.Cost:0.0}  ·  {sheet.Manpower / 1000f:0.0}k homens  ·  anda a {sheet.Speed:0.0}"
                       + (sheet.Slowest.Length > 0 ? $" (o mais lento é {sheet.Slowest})" : "")
                       + $"  ·  pesa {sheet.Stats["supply_use"]:0.0} na retaguarda";
            _delta.Text = Delta(w, pid, sheet);
            _delta.Visible = _delta.Text.Length > 0;

            Slots(w);
            Advice(sheet);
            Picker(w, sheet);

            _save.Text = _editing is null ? "✚ Criar modelo" : "✎ Guardar o modelo";
            _save.Disabled = _draft.Count == 0;
        }
        catch (Exception ex) { GD.PushError("DesignerView.Draw: " + ex); }
    }

    /// <summary>As ranhuras: uma chapa por tipo de batalhão, com o símbolo NATO, o nome, quantos são e os
    /// dois botões que o fazem crescer ou encolher. Linha primeiro, apoio depois — como na prancheta.</summary>
    private void Slots(World w)
    {
        Ui.Clear(_slots);
        foreach (var (id, qty) in _draft.OrderBy(u => TemplateDesign.IsSupport(w, u.UnitTypeId) ? 1 : 0))
        {
            UnitType u;
            try { u = w.Units.GetUnitType(id); } catch { continue; }
            bool sup = u.Category == "support";
            var box = new PanelContainer { CustomMinimumSize = new Vector2(148, 0) };
            box.AddThemeStyleboxOverride("panel", Ui.Box(sup ? Ui.Surface.Darkened(0.25f) : Ui.Surface, 6));
            var col = new VBoxContainer(); box.AddChild(col);

            var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 6);
            top.AddChild(UnitSymbol.Of(UnitCounter.KindOf(u.Stats.Tags), 30f, 22f, NatoSymbol.SpecialtyOf(u.Stats.Tags)));
            var n = Ui.Lbl($"×{qty}", 17); n.AddThemeColorOverride("font_color", Ui.Accent);
            top.AddChild(Ui.Grow(n));
            col.AddChild(top);
            var nm = Ui.Wrapped(u.Name, 136f, 13);
            nm.AddThemeColorOverride("font_color", sup ? Ui.TextDim : Ui.Text);
            col.AddChild(nm);

            int keep = id;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4);
            row.AddChild(Ui.Grow(Ui.Btn("−", () => Add(keep, -1), 60)));
            row.AddChild(Ui.Grow(Ui.Btn("+", () => Add(keep, +1), 60)));
            col.AddChild(row);
            _slots.AddChild(box);
        }
        _slotsNote.Text = _draft.Count == 0
            ? "prancheta em branco — escolhe batalhões lá em baixo"
            : $"{_draft.Count} tipo{(_draft.Count == 1 ? "" : "s")} de batalhão nesta divisão";
    }

    /// <summary>Os conselhos à margem, do Core. Vermelho o que impede o desenho de ser assinado.</summary>
    private void Advice(DesignSheet sheet)
    {
        Ui.Clear(_advice);
        foreach (var note in sheet.Notes)
        {
            var l = Ui.Wrapped((note.Bad ? "⚠ " : "· ") + note.Text, 620f, 15);
            l.AddThemeColorOverride("font_color", note.Bad ? Ui.Danger.Lightened(0.25f) : Ui.TextDim);
            _advice.AddChild(l);
        }
    }

    /// <summary>A gaveta dos batalhões: linha de um lado, apoio do outro (a categoria é da tabela). Cada
    /// linha diz o preço e o que aquilo acrescenta, para se escolher com números e não pelo nome.</summary>
    private void Picker(World w, DesignSheet sheet)
    {
        Ui.Clear(_pickTabs); Ui.Clear(_picker);
        IReadOnlyList<UnitType> all;
        try { all = w.Units.AllUnitTypes(); } catch (Exception ex) { GD.PushError("tipos: " + ex.Message); return; }
        int line = all.Count(u => u.Category != "support"), sup = all.Count - line;
        _pickTabs.AddChild(Ui.Tabs(new[] { $"Linha ({line})", $"Apoio ({sup})" }, _pick, i => { _pick = i; Paint(); }));

        bool wantSupport = _pick == SupportPicker;
        bool full = wantSupport ? sheet.Support >= TemplateDesign.SupportMax(w) : sheet.Line >= TemplateDesign.LineMax(w);
        if (full)
        {
            var l = Ui.Lbl(wantSupport ? "as ranhuras de apoio estão cheias" : "as ranhuras de linha estão cheias", 15);
            l.AddThemeColorOverride("font_color", Ui.Danger.Lightened(0.25f));
            _picker.AddChild(l);
        }
        foreach (var u in all.Where(x => (x.Category == "support") == wantSupport).OrderBy(x => x.Cost))
        {
            int id = u.Id;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            row.AddChild(UnitSymbol.Of(UnitCounter.KindOf(u.Stats.Tags), 26f, 20f, NatoSymbol.SpecialtyOf(u.Stats.Tags)));
            var name = Ui.Lbl(u.Name, 15);
            if (_draft.Any(d => d.UnitTypeId == id)) name.AddThemeColorOverride("font_color", Ui.Accent);
            row.AddChild(Ui.Grow(name));
            var num = Ui.Lbl($"{u.Cost:0.0}  ·  ⚔{u.Stats["soft_atk"]:0} 🛡{u.Stats["defense"]:0} ⛨{u.Stats["armor"]:0}", 13);
            num.AddThemeColorOverride("font_color", Ui.TextDim);
            row.AddChild(num);
            var add = Ui.Btn("+", () => Add(id, +1), 64);
            add.Disabled = full;
            row.AddChild(add);
            _picker.AddChild(row);
        }
    }

    /// <summary>O que muda em relação ao desenho de origem: só se diz quando há origem (redesenhar ou
    /// copiar), e só os números que mexeram — uma linha de "vs" com tudo a zero não diz nada a ninguém.</summary>
    private string Delta(World w, int pid, DesignSheet now)
    {
        if (_base.Count == 0) return "";
        var was = TemplateDesign.Of(w, pid, _base);
        var bits = new List<string>();
        foreach (var def in w.UnitStatDefs.Values.Where(x => x.Shown).OrderBy(x => x.Sort))
        {
            float d = now.Stats[def.Key] - was.Stats[def.Key];
            if (MathF.Abs(d) < 0.05f) continue;
            bits.Add($"{def.Name.ToLowerInvariant()} {(d > 0 ? "+" : "")}{DivisionView.Value(def, d)}");
        }
        float dc = now.Cost - was.Cost;
        if (MathF.Abs(dc) >= 0.05f) bits.Add($"custo {(dc > 0 ? "+" : "")}{dc:0.0}");
        return bits.Count == 0 ? "" : "vs o desenho de origem: " + string.Join("  ·  ", bits.Take(6));
    }

    /// <summary>Mais um batalhão daquele tipo, ou menos um. Chegado a zero, a ranhura desaparece.</summary>
    private void Add(int unitTypeId, int delta)
    {
        int at = _draft.FindIndex(u => u.UnitTypeId == unitTypeId);
        if (at < 0)
        {
            if (delta <= 0) return;
            if (_draft.Count >= 10) { _game.Notify("São 10 tipos de batalhão no máximo"); return; }
            _draft.Add((unitTypeId, 1));
        }
        else
        {
            int qty = _draft[at].Qty + delta;
            if (qty <= 0) _draft.RemoveAt(at);
            else if (qty > 30) { _game.Notify("São 30 batalhões do mesmo tipo no máximo"); return; }
            else _draft[at] = (unitTypeId, qty);
        }
        Paint();
    }

    private void Clear() { _draft.Clear(); Paint(); }

    /// <summary>Assina o desenho: cria o modelo, ou guarda o que se estava a redesenhar.</summary>
    private void Commit()
    {
        if (_game.PlayerId is not int pid) return;
        var units = _draft.Where(u => u.Qty > 0).ToList();
        string name = _name.Text.Trim();
        if (name.Length == 0) name = TemplateDesign.Of(_game.World, pid, units).Role;
        int? editing = _editing;
        _game.RunWhenIdle(() =>
        {
            var err = editing is int tid
                ? _game.Dispatch(new EditTemplateCommand(pid, tid, name, units))
                : _game.Dispatch(new CreateTemplateCommand(pid, name, units));
            if (err is not null) { _game.Notify(err); return; }
            _game.Notify(editing is null ? "Modelo criado" : "Modelo redesenhado");
            Close();
            _saved?.Invoke();
        });
    }

    /// <summary>--smoke: desenha uma divisão inteira sem dedo nenhum — abre a prancheta, mete um batalhão
    /// de linha e uma companhia de apoio, lê a ficha e os conselhos, assina o modelo e volta a abri-lo para
    /// lhe acrescentar mais um batalhão. Sem isto, a prancheta podia deixar de somar (ou de guardar) e o
    /// jogo não dava um erro sequer.</summary>
    public string Smoke()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid) return "sem país para a prancheta";
        IReadOnlyList<UnitType> all;
        try { all = w.Units.AllUnitTypes(); } catch { return "sem tipos de unidade"; }
        var lineType = all.FirstOrDefault(u => u.Category != "support");
        var supType = all.FirstOrDefault(u => u.Category == "support");
        if (lineType is null) return "sem batalhões de linha na tabela";

        Open();
        Add(lineType.Id, +1); Add(lineType.Id, +2);
        if (supType is not null) Add(supType.Id, +1);
        var sheet = TemplateDesign.Of(w, pid, _draft);
        int slots = _slots.GetChildCount(), rows = _picker.GetChildCount(), notes = _advice.GetChildCount();

        _name.Text = "Prova da prancheta";
        int had = 0; try { had = w.Units.GetTemplates(pid).Count; } catch { }
        Commit();
        int now = had; try { now = w.Units.GetTemplates(pid).Count; } catch { }
        string made = now > had ? "modelo assinado" : "modelo recusado";

        // e redesenhar: abre-se o que acabou de se fazer e mete-se-lhe mais um batalhão
        string again = "sem modelo para redesenhar";
        if (now > had && w.CustomTemplateIds.Count > 0)
        {
            int tid = w.CustomTemplateIds[^1];
            float before = w.Stats.Get(tid)["defense"];
            Open(tid, edit: true);
            Add(lineType.Id, +1);
            Commit();
            float after = w.Stats.Get(tid)["defense"];
            again = after > before
                ? $"redesenhado (defesa {before:0} → {after:0} sem mudar de modelo)"
                : $"redesenho sem efeito (defesa {after:0})";
        }
        Close();
        return $"prancheta com {slots} ranhuras e {rows} linhas na gaveta, {notes} conselhos"
             + $" [{TemplateDesign.Short(sheet)}], {made}, {again}";
    }
}
