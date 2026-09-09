using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A prancheta dos carros: onde se desenha um carro de combate à peça.
///
/// O exército era o único braço sem prancheta. O céu ganhou a oficina, o mar ganhou o estaleiro, e a tropa
/// de terra continuava a subir a mesma ladeira de quatro marcas que toda a gente sobe — investigar era a
/// única decisão, e o carro que saía da fábrica era igual ao do vizinho que investigou o mesmo.
///
/// Aqui o país desenha a geração SEGUINTE com as mãos dele. O que sai da prancheta não é uma classe à parte:
/// é uma marca de material com dono, a seguir à última da tabela — a fábrica reafina-se para ela, o armazém
/// mistura-a na pilha e a divisão bate-se com ela sem que nada no resto do jogo saiba que aquele carro foi
/// desenhado em casa.
///
/// As mãos são as das outras duas pranchetas: o casco em cima, as ranhuras em chapas que se tocam, a gaveta
/// em baixo só com o que entra na ranhura tocada, e os conselhos à margem vindos do Core (TankShop) — aqui
/// não se decide nada. O que é novo é a aresta: a ficha diz sempre o que a fábrica já faz, porque um carro
/// que não bata isso não se assina.
///
/// Lê o World só quando desenha (mundo parado) e muta só por Game.Dispatch.</summary>
public partial class TankShopView : PanelContainer
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
    private readonly List<string> _draft = new();     // uma peça por ranhura, na ordem do casco
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

        _name = new LineEdit { PlaceholderText = "Nome do carro", MaxLength = 40 };
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
        body.AddChild(Ui.Head("O casco"));
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
            var mine = designId != 0 ? w.TankDesigns.FirstOrDefault(d => d.Id == designId) : null;
            if (mine is not null)
            {
                _editing = mine.Id; _chassis = mine.Chassis; _name.Text = mine.Name;
                _draft.AddRange(mine.Modules);
            }
            else
            {
                _chassis = TankShop.Chassis(w).FirstOrDefault()?.Id ?? "";
            }
            Fit(w);
            Paint();
            Visible = true;
            Ui.FadeIn(this);
        });
    }

    public void Close() => Visible = false;

    /// <summary>Põe o rascunho do tamanho das ranhuras deste casco: encolher perde as peças de baixo, crescer
    /// abre ranhuras vazias. Peça que já não entra na ranhura onde estava cai fora.</summary>
    private void Fit(World w)
    {
        var slots = TankShop.Slots(w, _chassis);
        while (_draft.Count > slots.Count) _draft.RemoveAt(_draft.Count - 1);
        while (_draft.Count < slots.Count) _draft.Add("");
        for (int i = 0; i < slots.Count; i++)
            if (_draft[i].Length > 0 &&
                (!w.TankModules.TryGetValue(_draft[i], out var m) || m.Slot != slots[i])) _draft[i] = "";
        _slot = Math.Clamp(_slot, 0, Math.Max(0, slots.Count - 1));
    }

    private void Paint()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) { Close(); return; }
            var slots = TankShop.Slots(w, _chassis);
            var draft = new TankDesign { Id = _editing, CountryId = pid, Name = _name.Text.Trim(), Chassis = _chassis, Modules = _draft.ToList() };
            var def = TankShop.Build(w, draft);
            float price = TankShop.Price(w, _editing != 0);

            Ui.CrestInto(_crest, c.Tag, _editing == 0 ? "Prancheta dos carros" : "Redesenhar",
                $"{slots.Count} ranhura{(slots.Count == 1 ? "" : "s")}  ·  {c.ArmyXp:0} de exército  ·  a prancheta pede {price:0}");

            string chassisName = w.TankChassis.TryGetValue(_chassis, out var b) ? b.Name : "sem casco";
            _role.Text = $"{chassisName} — {Kind(w, b)} {Marks.Roman(def.Mark)}";
            _role.AddThemeColorOverride("font_color", TankShop.Check(w, pid, _chassis, _draft, _editing) is null
                ? Ui.Accent : Ui.Danger.Lightened(0.25f));

            Sheet(w, def, b is null ? 0f : TankShop.Bar(w, pid, b.UnitTypeId));
            _bill.Text = $"custa {def.Cost:0.00} conjuntos por carro"
                       + $"  ·  gasta-se ×{def.Wear:0.00}"
                       + $"  ·  {TankShop.Fitted(w, draft, slots).Count} peça{(TankShop.Fitted(w, draft, slots).Count == 1 ? "" : "s")} montada{(TankShop.Fitted(w, draft, slots).Count == 1 ? "" : "s")}";

            Chassis(w, pid);
            Slots(w, slots);
            Advice(w, pid);
            Picker(w, pid, slots);

            _save.Text = _editing == 0 ? "✚ Assinar o desenho" : "✎ Guardar o desenho";
            string? no = TankShop.Check(w, pid, _chassis, _draft, _editing);
            _save.Disabled = no is not null;
            _save.TooltipText = no ?? "Assina o desenho: a fábrica passa a fazer este carro em vez do da tabela.";
        }
        catch (Exception ex) { GD.PushError("TankShopView.Paint: " + ex); }
    }

    /// <summary>A tropa que este casco arma, pelo nome do tipo de unidade. Não há papel nenhum escrito em
    /// código: o casco diz a que ladeira de material pertence e é essa que se lê.</summary>
    private static string Kind(World w, TankChassisDef? b)
    {
        if (b is null) return "carro";
        try { return w.Units.GetUnitType(b.UnitTypeId).Name.ToLowerInvariant(); } catch { return "carro"; }
    }

    /// <summary>A ficha: os três números com que a ladeira das marcas se faz, e a barra que é preciso bater.
    /// A força vem acesa ou apagada conforme passa a aresta — é a única conta que impede a assinatura.</summary>
    private void Sheet(World w, EquipmentMarkDef d, float bar)
    {
        Ui.Clear(_sheet);
        void Cell(string glyph, string name, string value, bool good)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4);
            row.AddChild(Glyph.Make(glyph, 18f, good ? Ui.Accent : Ui.TextDim, name));
            var l = Ui.Lbl(value, 16);
            l.AddThemeColorOverride("font_color", good ? Ui.Text : Ui.TextDim);
            l.TooltipText = name;
            row.AddChild(l);
            _sheet.AddChild(row);
        }
        Cell("espadas", "força: o que este carro bate", $"×{d.Power:0.00}", bar <= 0f || d.Power >= bar - 0.0001f);
        Cell("bigorna", "custo: conjuntos de fábrica por carro", $"×{d.Cost:0.00}", d.Cost <= 2.5f);
        Cell("estilhaco", "desgaste: o que a frente gasta dele por dia", $"×{d.Wear:0.00}", d.Wear <= 1.02f);
        Cell("fabrica", "a força que a fábrica já sabe fazer hoje", bar <= 0f ? "sem tabela" : $"bate {bar:0.00}",
             bar <= 0f || d.Power >= bar - 0.0001f);
    }

    /// <summary>Os cascos: uma chapa por casco que se desenha, o escolhido aceso. Um casco que a investigação
    /// ainda não abriu aparece na mesma, apagado e com o nome do que falta.</summary>
    private void Chassis(World w, int pid)
    {
        Ui.Clear(_chassisBox);
        foreach (var d in TankShop.Chassis(w))
        {
            string id = d.Id;
            bool on = id == _chassis;
            string locked = TankShop.ChassisLocked(w, pid, id);
            var box = new PanelContainer { CustomMinimumSize = new Vector2(132, 0) };
            box.AddThemeStyleboxOverride("panel", Ui.Box(on ? Ui.SurfaceHi : Ui.Surface, 6));
            var col = new VBoxContainer(); box.AddChild(col);
            var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 6);
            top.AddChild(Glyph.Make(d.Glyph, 22f, locked.Length > 0 ? Ui.TextDim : on ? Ui.Accent : Ui.TextDim, d.Note));
            var n = Ui.Lbl($"{TankShop.Slots(w, id).Count} ranhuras", 13);
            n.AddThemeColorOverride("font_color", Ui.TextDim);
            top.AddChild(Ui.Grow(n));
            col.AddChild(top);
            var nm = Ui.Wrapped(d.Name, 120f, 13);
            nm.AddThemeColorOverride("font_color", locked.Length > 0 ? Ui.TextDim : on ? Ui.Accent : Ui.Text);
            col.AddChild(nm);
            if (locked.Length > 0)
            {
                var why = Ui.Wrapped("fechado: " + locked, 120f, 12);
                why.AddThemeColorOverride("font_color", Ui.TextDim);
                col.AddChild(why);
            }
            _chassisBox.AddChild(Ui.Click(box, () => PickChassis(id), locked.Length > 0 ? $"{d.Note} (precisa de {locked})" : d.Note));
        }
    }

    private void PickChassis(string id)
    {
        if (id == _chassis) return;
        _chassis = id; _slot = 0;
        Fit(_game.World);
        Paint();
    }

    /// <summary>As ranhuras: uma chapa por ranhura do casco, com a peça que lá está (ou a chapa da ranhura
    /// vazia). Tocar numa põe a gaveta a mostrar só o que lá entra.</summary>
    private void Slots(World w, List<string> slots)
    {
        Ui.Clear(_slots);
        for (int i = 0; i < slots.Count; i++)
        {
            int at = i;
            w.TankSlotDefs.TryGetValue(slots[i], out var s);
            string mid = at < _draft.Count ? _draft[at] : "";
            w.TankModules.TryGetValue(mid, out var m);
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
            ? "este casco não se desenha"
            : $"{slots.Count - empty} de {slots.Count} ranhuras cheias — toca numa para a mudar";
    }

    private void Advice(World w, int pid)
    {
        Ui.Clear(_advice);
        foreach (var (text, bad) in TankShop.Advice(w, pid, _chassis, _draft))
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
        w.TankSlotDefs.TryGetValue(slot, out var s);
        var head = Ui.Lbl($"Ranhura {_slot + 1}: {s?.Name ?? slot} — {s?.Note ?? ""}", 14);
        head.AddThemeColorOverride("font_color", Ui.TextDim);
        _picker.AddChild(head);

        foreach (var m in w.TankModules.Values.Where(x => x.Slot == slot).OrderBy(x => x.Sort)
                                              .ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            string id = m.Id, locked = TankShop.Locked(w, pid, id);
            bool on = _slot < _draft.Count && _draft[_slot] == id;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Glyph.Make(m.Glyph, 24f, locked.Length > 0 ? Ui.TextDim : on ? Ui.Accent : Ui.Text, m.Note));
            var name = Ui.Lbl(m.Name, 15);
            name.AddThemeColorOverride("font_color", locked.Length > 0 ? Ui.TextDim : on ? Ui.Accent : Ui.Text);
            name.TooltipText = m.Note;
            row.AddChild(Ui.Grow(name));
            var num = Ui.Lbl(locked.Length > 0 ? $"fechada: {locked}" : Deltas(m), 13);
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
    private static string Deltas(TankModuleDef m)
    {
        var bits = new List<string>();
        void Add(string name, float v) { if (MathF.Abs(v) >= 0.005f) bits.Add($"{name} {(v > 0 ? "+" : "")}{v:0.00}"); }
        Add("bate", m.Power); Add("custa", m.Cost); Add("gasta", m.Wear);
        return bits.Count == 0 ? "não mexe em nada" : string.Join(" · ", bits);
    }

    private void Put(int slot, string moduleId)
    {
        if (slot < 0 || slot >= _draft.Count) return;
        _draft[slot] = moduleId;
        Paint();
    }

    /// <summary>Assina o desenho: cria o carro, ou guarda o que se estava a mexer.</summary>
    private void Commit()
    {
        if (_game.PlayerId is not int pid) return;
        string name = _name.Text.Trim();
        var modules = _draft.ToList();
        string chassis = _chassis;
        int editing = _editing;
        if (name.Length == 0)
            name = "Carro " + (_game.World.TankChassis.TryGetValue(chassis, out var b) ? b.Name.ToLowerInvariant() : "de casa");
        _game.RunWhenIdle(() =>
        {
            var err = _game.Dispatch(new DesignTankCommand(pid, name, chassis, modules, editing));
            if (err is not null) { _game.Notify(err); return; }
            _game.Notify(editing == 0 ? "Carro assinado: a fábrica passa a fazê-lo" : "Desenho guardado");
            Close();
            _saved?.Invoke();
        });
    }

    /// <summary>--smoke: desenha um carro inteiro sem dedo nenhum — abre a prancheta, escolhe o casco que a
    /// casa pode levar, enche as ranhuras com a peça mais cara que já investigou, lê a ficha contra a aresta
    /// (o que a fábrica já faz), assina, e volta a abrir o desenho para lhe tirar uma peça e ver o custo
    /// descer sem cair abaixo da fábrica. Sem isto, a prancheta podia deixar de somar (ou de guardar) e o
    /// jogo não dava um erro sequer.</summary>
    public string Smoke()
    {
        var w = _game.World;
        if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) return "sem país para a prancheta";
        var chassis = TankShop.Chassis(w)
            .Where(d => TankShop.ChassisLocked(w, pid, d.Id).Length == 0
                        && TankShop.For(w, pid, d.UnitTypeId) is null)
            .OrderByDescending(d => TankShop.Slots(w, d.Id).Count).ThenBy(d => d.Sort).FirstOrDefault();
        if (chassis is null) return "sem casco que esta casa possa desenhar";

        // a experiência da prova: a prancheta paga-se em experiência de exército, e a casa pode não ter nenhuma
        float xp = c.ArmyXp;
        c.ArmyXp = MathF.Max(xp, TankShop.Price(w, false) + TankShop.Price(w, true) + 1f);

        Open(0);
        PickChassis(chassis.Id);
        var slots = TankShop.Slots(w, chassis.Id);
        for (int i = 0; i < slots.Count; i++)
        {
            var best = TankShop.Fit(w, pid, slots[i]).OrderByDescending(m => m.Cost).ThenBy(m => m.Sort).FirstOrDefault();
            if (best is not null) Put(i, best.Id);
        }
        var made = TankShop.Build(w, new TankDesign { CountryId = pid, Chassis = chassis.Id, Modules = _draft.ToList() });
        float bar = TankShop.Bar(w, pid, chassis.UnitTypeId);
        int plates = _slots.GetChildCount(), rows = _picker.GetChildCount(), notes = _advice.GetChildCount();
        _name.Text = "Prova da prancheta";
        int had = w.TankDesigns.Count;
        Commit();
        string signed = w.TankDesigns.Count > had ? "assinado" : "recusado";

        // e redesenhar: tira-se uma peça que não é obrigatória e o custo tem de descer — mas só uma que
        // deixe o carro continuar a bater o que a fábrica já faz, que é a lei desta prancheta
        string again = "sem desenho para mexer";
        if (w.TankDesigns.Count > had)
        {
            var mine = w.TankDesigns[^1];
            string markId = TankShop.MarkId(mine.Id);
            float before = w.EquipmentMarks[markId].Cost;
            Open(mine.Id);
            var lista = TankShop.Slots(w, _chassis);
            int drop = -1;
            for (int i = 0; i < _draft.Count && i < lista.Count; i++)
            {
                if (_draft[i].Length == 0) continue;
                if (w.TankSlotDefs.TryGetValue(lista[i], out var sd) && sd.Required) continue;
                string was = _draft[i];
                Put(i, "");
                if (TankShop.Check(w, pid, _chassis, _draft, _editing) is null) { drop = i; break; }
                Put(i, was);
            }
            if (drop < 0) again = $"nada que se possa tirar sem cair abaixo dos {bar:0.00} da fábrica";
            else
            {
                Commit();
                float after = w.EquipmentMarks.TryGetValue(markId, out var now) ? now.Cost : before;
                again = after < before
                    ? $"redesenhado sem mudar de marca (custo {before:0.00} → {after:0.00})"
                    : $"redesenho sem efeito (custo {after:0.00})";
            }
        }
        Close();
        c.ArmyXp = xp;
        return $"{chassis.Name} com {plates} ranhuras e {rows} linhas na gaveta, {notes} conselhos"
             + $" [{TankShop.Short(w, made)}; a fábrica bate {bar:0.00}], {signed}, {again}";
    }
}
