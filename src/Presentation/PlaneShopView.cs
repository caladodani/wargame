using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A oficina: onde se desenha um avião à peça.
///
/// O hangar já deixava escolher o modelo, mas o modelo vinha feito da tabela — nove aviões iguais para toda
/// a gente, e um país que investisse trinta anos em aviação voava no mesmo caça do vizinho que não
/// investira nada. No HoI4 a fuselagem é só o princípio: o que decide se aquilo ganha o céu, abre esquadras
/// ao meio ou leva pára-quedistas é o que se lhe mete nas ranhuras, e vê-se a ficha a mexer a cada peça.
///
/// É essa prancheta que aqui se recria, com desenho nosso e com as mesmas mãos da prancheta das divisões:
/// a fuselagem escolhe-se em cima, as ranhuras são chapas que se tocam, a gaveta em baixo só mostra o que
/// entra na ranhura tocada, e os conselhos à margem vêm do Core (PlaneShop) — aqui não se decide nada.
///
/// Lê o World só quando desenha (mundo parado) e muta só por Game.Dispatch.</summary>
public partial class PlaneShopView : PanelContainer
{
    private Game _game = null!;
    private LineEdit _name = null!;
    private Label _role = null!, _bill = null!, _slotsNote = null!;
    private HFlowContainer _chassisBox = null!, _slots = null!, _sheet = null!;
    private VBoxContainer _advice = null!, _picker = null!;
    private HBoxContainer _crest = null!;
    private Button _save = null!;
    private ScrollContainer _scroll = null!;
    private Action? _saved;

    private string _chassis = "";
    private readonly List<string> _draft = new();     // uma peça por ranhura, na ordem da fuselagem
    private int _slot;                                 // ranhura em cima da gaveta
    private int _editing;                              // desenho que se está a mexer (0 = desenho novo)

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

        _name = new LineEdit { PlaceholderText = "Nome do avião", MaxLength = 40 };
        body.AddChild(_name);

        // a ficha primeiro: é o que se está a fazer subir ou descer a cada peça
        _role = Ui.Lbl("prancheta vazia", 17);
        _role.AddThemeColorOverride("font_color", Ui.Accent);
        body.AddChild(_role);
        _sheet = new HFlowContainer();
        _sheet.AddThemeConstantOverride("h_separation", 10);
        body.AddChild(_sheet);
        _bill = Ui.Lbl("", 15); _bill.AddThemeColorOverride("font_color", Ui.TextDim); body.AddChild(_bill);

        body.AddChild(Ui.Rule());
        body.AddChild(Ui.Head("A fuselagem"));
        _chassisBox = new HFlowContainer();
        _chassisBox.AddThemeConstantOverride("h_separation", 6);
        _chassisBox.AddThemeConstantOverride("v_separation", 6);
        body.AddChild(_chassisBox);

        body.AddChild(Ui.Rule());
        body.AddChild(Ui.Head("As ranhuras"));
        _slots = new HFlowContainer();
        _slots.AddThemeConstantOverride("h_separation", 6);
        _slots.AddThemeConstantOverride("v_separation", 6);
        body.AddChild(_slots);
        _slotsNote = Ui.Lbl("", 14); _slotsNote.AddThemeColorOverride("font_color", Ui.TextDim);
        body.AddChild(_slotsNote);

        _advice = new VBoxContainer(); body.AddChild(_advice);

        body.AddChild(Ui.Rule());
        body.AddChild(Ui.Head("Pôr peças"));
        _picker = new VBoxContainer(); _picker.AddThemeConstantOverride("separation", 2); body.AddChild(_picker);

        var feet = new HBoxContainer(); feet.AddThemeConstantOverride("separation", 8); v.AddChild(feet);
        _save = Ui.Btn("✚ Assinar o desenho", Commit);
        feet.AddChild(Ui.Grow(_save));
        feet.AddChild(Ui.Btn("Cancelar", Close));
    }

    /// <summary>Abre a prancheta em branco, ou com um desenho lá dentro para se lhe mexer.</summary>
    public void Open(int designId = 0)
    {
        _game.RunWhenIdle(() =>
        {
            var w = _game.World;
            _editing = 0; _draft.Clear(); _name.Text = ""; _slot = 0;
            var mine = designId != 0 ? w.PlaneDesigns.FirstOrDefault(d => d.Id == designId) : null;
            if (mine is not null)
            {
                _editing = mine.Id; _chassis = mine.Chassis; _name.Text = mine.Name;
                _draft.AddRange(mine.Modules);
            }
            else
            {
                _chassis = PlaneShop.Chassis(w).FirstOrDefault()?.Id ?? "";
            }
            Fit(w);
            Paint();
            Visible = true;
            Ui.FadeIn(this);
        });
    }

    public void Close() => Visible = false;

    /// <summary>Põe o rascunho do tamanho das ranhuras desta fuselagem: encolher perde as peças de baixo,
    /// crescer abre ranhuras vazias. Peça que já não entra na ranhura onde estava cai fora.</summary>
    private void Fit(World w)
    {
        var slots = PlaneShop.Slots(w, _chassis);
        while (_draft.Count > slots.Count) _draft.RemoveAt(_draft.Count - 1);
        while (_draft.Count < slots.Count) _draft.Add("");
        for (int i = 0; i < slots.Count; i++)
            if (_draft[i].Length > 0 &&
                (!w.PlaneModules.TryGetValue(_draft[i], out var m) || m.Slot != slots[i])) _draft[i] = "";
        _slot = Math.Clamp(_slot, 0, Math.Max(0, slots.Count - 1));
    }

    private void Paint()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) { Close(); return; }
            var slots = PlaneShop.Slots(w, _chassis);
            var draft = new PlaneDesign { Id = _editing, CountryId = pid, Name = _name.Text.Trim(), Chassis = _chassis, Modules = _draft.ToList() };
            var def = PlaneShop.Build(w, draft);
            float price = PlaneShop.Price(w, _editing != 0);

            Ui.CrestInto(_crest, c.Tag, _editing == 0 ? "Oficina" : "Redesenhar",
                $"{slots.Count} ranhura{(slots.Count == 1 ? "" : "s")}  ·  {c.AirXp:0} horas de voo  ·  a prancheta pede {price:0}");

            string chassisName = w.PlaneClasses.TryGetValue(_chassis, out var b) ? b.Name : "sem fuselagem";
            _role.Text = $"{chassisName} — {Role(def)}";
            _role.AddThemeColorOverride("font_color", PlaneShop.Check(w, pid, _chassis, _draft, _editing) is null
                ? Ui.Accent : Ui.Danger.Lightened(0.25f));

            Sheet(def);
            _bill.Text = $"custo {w.Rule("air_wing_cost", 60f) * def.Cost:0} por asa"
                       + $"  ·  estadia ×{def.Upkeep:0.0}  ·  alcance {def.RangeKm:0} km"
                       + (def.Deck ? "  ·  cabe num convés" : "");

            Chassis(w);
            Slots(w, slots);
            Advice(w, pid);
            Picker(w, pid, slots);

            _save.Text = _editing == 0 ? "✚ Assinar o desenho" : "✎ Guardar o desenho";
            string? no = PlaneShop.Check(w, pid, _chassis, _draft, _editing);
            _save.Disabled = no is not null;
            _save.TooltipText = no ?? "Assina o desenho: passa a ser um modelo do hangar como os outros.";
        }
        catch (Exception ex) { GD.PushError("PlaneShopView.Paint: " + ex); }
    }

    /// <summary>O que este avião é, dito pelos números: a tarefa em que rende mais. Não há papel nenhum
    /// escrito em código — lê-se a maior das colunas, como o Core faz para escolher quem levanta.</summary>
    private static string Role(PlaneClassDef d)
    {
        var bits = new (string Name, float Value)[]
        {
            ("caça", d.Superiority), ("apoio à tropa", d.Support), ("bombardeamento", d.Bombing),
            ("ataque naval", d.Naval), ("transporte", d.Transport),
        };
        var best = bits.OrderByDescending(x => x.Value).First();
        return best.Value < 0.3f ? "não serve para nada" : best.Name;
    }

    private void Sheet(PlaneClassDef d)
    {
        Ui.Clear(_sheet);
        void Cell(string glyph, string name, float value)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4);
            row.AddChild(Glyph.Make(glyph, 18f, value > 0.05f ? Ui.Accent : Ui.TextDim, name));
            var l = Ui.Lbl($"{value:0.0}", 16);
            l.AddThemeColorOverride("font_color", value > 0.05f ? Ui.Text : Ui.TextDim);
            l.TooltipText = name;
            row.AddChild(l);
            _sheet.AddChild(row);
        }
        Cell("caca", "combate no ar", d.Air);
        Cell("asa", "superioridade aérea", d.Superiority);
        Cell("obus", "apoio à tropa", d.Support);
        Cell("bomba", "bombardeamento", d.Bombing);
        Cell("torpedo", "ataque naval", d.Naval);
        Cell("carga", "transporte", d.Transport);
    }

    /// <summary>As fuselagens: uma chapa por casco que se desenha, a escolhida acesa. Trocar de fuselagem
    /// muda as ranhuras — as peças que já não entram ficam pelo caminho, e diz-se.</summary>
    private void Chassis(World w)
    {
        Ui.Clear(_chassisBox);
        foreach (var d in PlaneShop.Chassis(w))
        {
            string id = d.Id;
            bool on = id == _chassis;
            var box = new PanelContainer { CustomMinimumSize = new Vector2(132, 0) };
            box.AddThemeStyleboxOverride("panel", Ui.Box(on ? Ui.SurfaceHi : Ui.Surface, 6));
            var col = new VBoxContainer(); box.AddChild(col);
            var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 6);
            top.AddChild(Glyph.Make(d.Glyph, 22f, on ? Ui.Accent : Ui.TextDim, d.Note));
            var n = Ui.Lbl($"{PlaneShop.Slots(w, id).Count} ranhuras", 13);
            n.AddThemeColorOverride("font_color", Ui.TextDim);
            top.AddChild(Ui.Grow(n));
            col.AddChild(top);
            var nm = Ui.Wrapped(d.Name, 120f, 13);
            nm.AddThemeColorOverride("font_color", on ? Ui.Accent : Ui.Text);
            col.AddChild(nm);
            _chassisBox.AddChild(Ui.Click(box, () => PickChassis(id), d.Note));
        }
    }

    private void PickChassis(string id)
    {
        if (id == _chassis) return;
        _chassis = id; _slot = 0;
        Fit(_game.World);
        Paint();
    }

    /// <summary>As ranhuras: uma chapa por ranhura da fuselagem, com a peça que lá está (ou a chapa da
    /// ranhura vazia). Tocar numa põe a gaveta a mostrar só o que lá entra.</summary>
    private void Slots(World w, List<string> slots)
    {
        Ui.Clear(_slots);
        for (int i = 0; i < slots.Count; i++)
        {
            int at = i;
            w.PlaneSlotDefs.TryGetValue(slots[i], out var s);
            string mid = at < _draft.Count ? _draft[at] : "";
            w.PlaneModules.TryGetValue(mid, out var m);
            bool on = at == _slot;

            var box = new PanelContainer { CustomMinimumSize = new Vector2(140, 0) };
            box.AddThemeStyleboxOverride("panel", Ui.Box(on ? Ui.SurfaceHi : Ui.Surface, 6));
            var col = new VBoxContainer(); box.AddChild(col);
            var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 6);
            top.AddChild(Glyph.Make(m?.Glyph ?? s?.Glyph ?? "caixa", 26f,
                                    m is null ? Ui.TextDim : Ui.Accent, m?.Note ?? s?.Note ?? ""));
            var tag = Ui.Lbl(s?.Name ?? slots[at], 13);
            tag.AddThemeColorOverride("font_color", Ui.TextDim);
            top.AddChild(Ui.Grow(tag));
            col.AddChild(top);
            var nm = Ui.Wrapped(m?.Name ?? (s?.Required == true ? "vazia — falta" : "vazia"), 128f, 13);
            nm.AddThemeColorOverride("font_color", m is null && s?.Required == true ? Ui.Danger.Lightened(0.25f) : Ui.Text);
            col.AddChild(nm);
            if (m is not null)
                col.AddChild(Ui.Btn("Tirar", () => Put(at, ""), 128));
            _slots.AddChild(Ui.Click(box, () => { _slot = at; Paint(); }, s?.Note ?? ""));
        }
        int empty = _draft.Count(x => x.Length == 0);
        _slotsNote.Text = slots.Count == 0
            ? "esta fuselagem não se desenha"
            : $"{slots.Count - empty} de {slots.Count} ranhuras cheias — toca numa para a mudar";
    }

    private void Advice(World w, int pid)
    {
        Ui.Clear(_advice);
        foreach (var (text, bad) in PlaneShop.Advice(w, pid, _chassis, _draft))
        {
            var l = Ui.Wrapped((bad ? "⚠ " : "· ") + text, 620f, 15);
            l.AddThemeColorOverride("font_color", bad ? Ui.Danger.Lightened(0.25f) : Ui.TextDim);
            _advice.AddChild(l);
        }
    }

    /// <summary>A gaveta: só as peças da ranhura tocada. As que a investigação ainda não abriu aparecem na
    /// mesma, apagadas e com o nome do que falta — esconder uma peça é esconder uma razão para investigar.</summary>
    private void Picker(World w, int pid, List<string> slots)
    {
        Ui.Clear(_picker);
        if (slots.Count == 0) return;
        string slot = slots[Math.Clamp(_slot, 0, slots.Count - 1)];
        w.PlaneSlotDefs.TryGetValue(slot, out var s);
        var head = Ui.Lbl($"Ranhura {_slot + 1}: {s?.Name ?? slot} — {s?.Note ?? ""}", 14);
        head.AddThemeColorOverride("font_color", Ui.TextDim);
        _picker.AddChild(head);

        foreach (var m in w.PlaneModules.Values.Where(x => x.Slot == slot).OrderBy(x => x.Sort))
        {
            string id = m.Id, locked = PlaneShop.Locked(w, pid, id);
            bool on = _slot < _draft.Count && _draft[_slot] == id;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Glyph.Make(m.Glyph, 24f, locked.Length > 0 ? Ui.TextDim : on ? Ui.Accent : Ui.Text, m.Note));
            var name = Ui.Lbl(m.Name, 15);
            name.AddThemeColorOverride("font_color", locked.Length > 0 ? Ui.TextDim : on ? Ui.Accent : Ui.Text);
            name.TooltipText = m.Note;
            row.AddChild(Ui.Grow(name));
            var num = Ui.Lbl(locked.Length > 0 ? $"fechada: {locked}" : Deltas(w, m), 13);
            num.AddThemeColorOverride("font_color", Ui.TextDim);
            row.AddChild(num);
            var put = Ui.Btn(on ? "posta" : "+", () => Put(_slot, id), 72);
            put.Disabled = locked.Length > 0 || on;
            row.AddChild(put);
            _picker.AddChild(row);
        }
    }

    /// <summary>O que a peça soma, em palavras curtas: só os números que mexem. Uma linha de zeros não diz
    /// nada a ninguém, e é sobre isto que se escolhe.</summary>
    private static string Deltas(World w, PlaneModuleDef m)
    {
        var bits = new List<string>();
        void Add(string name, float v, string fmt = "0.0") { if (MathF.Abs(v) >= 0.05f) bits.Add($"{name} {(v > 0 ? "+" : "")}{v.ToString(fmt)}"); }
        Add("ar", m.Air); Add("céu", m.Superiority); Add("apoio", m.Support);
        Add("bombas", m.Bombing); Add("mar", m.Naval); Add("carga", m.Transport);
        if (MathF.Abs(m.RangeKm) >= 1f) bits.Add($"{(m.RangeKm > 0 ? "+" : "")}{m.RangeKm:0} km");
        if (m.Deck > 0) bits.Add("convés");
        bits.Add($"custa {w.Rule("air_wing_cost", 60f) * m.Cost:+0;-0;0}");
        return string.Join(" · ", bits.Take(5));
    }

    private void Put(int slot, string moduleId)
    {
        if (slot < 0 || slot >= _draft.Count) return;
        _draft[slot] = moduleId;
        Paint();
    }

    /// <summary>Assina o desenho: cria o avião, ou guarda o que se estava a mexer.</summary>
    private void Commit()
    {
        if (_game.PlayerId is not int pid) return;
        string name = _name.Text.Trim();
        var modules = _draft.ToList();
        string chassis = _chassis;
        int editing = _editing;
        if (name.Length == 0)
            name = (_game.World.PlaneClasses.TryGetValue(chassis, out var b) ? b.Name : "Avião") + " de casa";
        _game.RunWhenIdle(() =>
        {
            var err = _game.Dispatch(new DesignPlaneCommand(pid, name, chassis, modules, editing));
            if (err is not null) { _game.Notify(err); return; }
            _game.Notify(editing == 0 ? "Avião assinado: já se compra no hangar" : "Desenho guardado");
            Close();
            _saved?.Invoke();
        });
    }

    /// <summary>--smoke: desenha um avião inteiro sem dedo nenhum — abre a prancheta, escolhe a fuselagem,
    /// enche as ranhuras com a peça mais cara que a casa já investigou, lê a ficha, assina, e volta a abrir
    /// o desenho para lhe trocar uma peça e ver a ficha mexer. Sem isto, a oficina podia deixar de somar
    /// (ou de guardar) e o jogo não dava um erro sequer.</summary>
    public string Smoke()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) return "sem país para a oficina";
        var chassis = PlaneShop.Chassis(w).OrderByDescending(d => PlaneShop.Slots(w, d.Id).Count)
                        .ThenBy(d => d.Sort).FirstOrDefault();
        if (chassis is null) return "sem fuselagem que se desenhe na tabela";

        // as horas de voo da prova: a oficina paga-se em experiência aérea, e a casa pode não ter nenhuma
        float horas = c.AirXp;
        c.AirXp = MathF.Max(horas, PlaneShop.Price(w, false) + PlaneShop.Price(w, true) + 1f);

        Open(0);
        PickChassis(chassis.Id);
        var slots = PlaneShop.Slots(w, chassis.Id);
        for (int i = 0; i < slots.Count; i++)
        {
            var best = PlaneShop.Fit(w, pid, slots[i]).OrderByDescending(m => m.Cost).ThenBy(m => m.Sort).FirstOrDefault();
            if (best is not null) Put(i, best.Id);
        }
        var made = PlaneShop.Build(w, new PlaneDesign { Chassis = chassis.Id, Modules = _draft.ToList() });
        int plates = _slots.GetChildCount(), rows = _picker.GetChildCount(), notes = _advice.GetChildCount();
        _name.Text = "Prova da oficina";
        int had = w.PlaneDesigns.Count;
        Commit();
        string signed = w.PlaneDesigns.Count > had ? "assinado" : "recusado";

        // e redesenhar: tira-se a peça mais cara e a ficha tem de descer
        string again = "sem desenho para mexer";
        if (w.PlaneDesigns.Count > had)
        {
            var mine = w.PlaneDesigns[^1];
            string cls = PlaneShop.ClassId(mine.Id);
            float before = w.PlaneClasses[cls].Cost;
            Open(mine.Id);
            // tira-se de uma ranhura que não é obrigatória: sem motor o desenho nem se assinava, e a prova
            // ficava a dizer que o redesenho não pega quando o que não pegava era o avião
            var lista = PlaneShop.Slots(w, _chassis);
            int drop = -1;
            for (int i = 0; i < _draft.Count && i < lista.Count; i++)
                if (_draft[i].Length > 0 && w.PlaneSlotDefs.TryGetValue(lista[i], out var sd) && !sd.Required) { drop = i; break; }
            if (drop >= 0) Put(drop, "");
            Commit();
            float after = w.PlaneClasses.TryGetValue(cls, out var now) ? now.Cost : before;
            again = after < before
                ? $"redesenhado sem mudar de modelo (custo {before:0.0} → {after:0.0})"
                : $"redesenho sem efeito (custo {after:0.0})";
        }
        Close();
        c.AirXp = horas;
        return $"{chassis.Name} com {plates} ranhuras e {rows} linhas na gaveta, {notes} conselhos"
             + $" [{PlaneShop.Short(w, made)}], {signed}, {again}";
    }
}
