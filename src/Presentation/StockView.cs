using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>O armazém de material do país, prateleira a prateleira: o que há de cada tipo, o que as
/// fábricas fazem por dia, o que a tropa está a pedir e quantos dias é que aquilo aguenta.
///
/// É a folha que faltava do lado da guerra longa. A fila de produção diz o que vai nascer; esta diz com que
/// é que se aguenta o que já existe. No HoI4 é o ecrã onde se percebe, antes de atacar, que a ofensiva vai
/// parar por falta de equipamento e não por falta de homens — e é a diferença entre planear uma campanha e
/// carregar em avançar até a linha se desfazer.
///
/// As contas não são daqui: vêm todas do Warehouse, que é quem o EquipmentSystem e o ProductionSystem
/// também leem. Aqui só se desenha.</summary>
public static class StockView
{
    /// <summary>A folha inteira: cabeçalho com o estado do exército e uma chapa por prateleira. `order`, se
    /// vier, põe um "+" em cada prateleira para abrir ali uma linha de material.</summary>
    public static VBoxContainer Sheet(World w, Country c, Action<int>? order = null)
    {
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 6);
        float avg = Warehouse.Average(w, c);
        var worst = Warehouse.Worst(w, c);
        var head = Ui.Lbl($"exército armado a {avg:P0}"
                          + (worst is null ? "  ·  não falta material a ninguém"
                             : $"  ·  pior: {Name(w, worst)} a {worst.Kit:P0}"), 15);
        head.AddThemeColorOverride("font_color", avg >= 0.95f ? Ui.Good : avg >= 0.7f ? Ui.Text : Ui.Danger);
        head.TooltipText = "O material que as divisões têm hoje. O combate destrói equipamento; o armazém repõe-no."
                         + "\nSem material, uma divisão bate-se pior e não repõe efectivo — os homens voltam, as armas não.";
        box.AddChild(head);

        var rows = Warehouse.Rows(w, c);
        if (rows.Count == 0) { box.AddChild(Ui.Lbl("Sem exército nem linhas de material — armazém por abrir.")); return box; }
        foreach (var r in rows) box.AddChild(Shelf(w, c, r, order));
        return box;
    }

    /// <summary>Uma prateleira: símbolo NATO do que ali se guarda, o nome, os números do dia e a barra que
    /// diz quanto do que a tropa pede é que está em casa.</summary>
    private static PanelContainer Shelf(World w, Country c, StockRow r, Action<int>? order)
    {
        var card = new PanelContainer();
        bool dry = r.Missing > r.Have + 1e-3f;
        var skin = Ui.Box(Ui.Ink with { A = 0.85f }, 6);
        if (dry) skin.BorderColor = Ui.Danger with { A = 0.6f };   // prateleira em falta dá nas vistas
        card.AddThemeStyleboxOverride("panel", skin);
        var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 8); card.AddChild(line);
        line.AddChild(Symbol(w, r.UnitTypeId));

        var cell = Ui.Grow(new VBoxContainer()); cell.AddThemeConstantOverride("separation", 2);
        var title = Ui.Lbl($"{r.Name}   ·   {r.Have:0.0} em armazém", 16);
        title.AddThemeColorOverride("font_color", dry ? Ui.Danger : Ui.Text);
        cell.AddChild(title);
        var sub = Ui.Lbl(Line(r) + Mark(w, c, r.UnitTypeId), 13);
        sub.AddThemeColorOverride("font_color", Ui.TextDim);
        cell.AddChild(sub);
        float want = MathF.Max(r.Have + r.Missing, 1e-3f);
        cell.AddChild(Ui.Grow(Ui.Bar(r.Have / want, dry ? Ui.Danger : Ui.Good)));
        line.AddChild(cell);

        if (order is not null)
        {
            int type = r.UnitTypeId;
            int lines = c.Queue.Count(o => o.IsKit && o.UnitTypeId == type);
            var btn = Ui.Btn(lines > 0 ? $"+ ({lines})" : "+", () => order(type), 84);
            btn.TooltipText = lines == 0 ? "Abrir uma linha de material deste tipo"
                                         : $"{lines} linha{(lines == 1 ? "" : "s")} deste material a trabalhar — abre outra";
            line.AddChild(btn);
        }
        card.TooltipText = Why(w, c, r);
        return card;
    }

    /// <summary>Os números do dia em palavras: o que se faz, o que se pede e quanto aguenta.</summary>
    public static string Line(StockRow r) =>
        $"faz {r.PerDay:0.00}/dia"
        + (r.Missing <= 1e-3f ? "  ·  ninguém a pedir" : $"  ·  faltam {r.Missing:0.0} à tropa")
        + "  ·  " + Days(r.Days);

    public static string Days(float days) =>
        float.IsPositiveInfinity(days) ? "faz-se mais do que se gasta"
        : days <= 0f ? "armazém vazio"
        : days < 1f ? "acaba hoje"
        : $"aguenta {days:0} dia{(days < 1.5f ? "" : "s")}";

    /// <summary>A marca do material que está nesta prateleira, e a que a indústria já sabe fazer. Vazio num
    /// mundo sem tabela de marcas — a prateleira volta a ser a de sempre.</summary>
    public static string Mark(World w, Country c, int unitTypeId)
    {
        if (Marks.All(w, unitTypeId).Count == 0) return "";
        float have = c.StockedMark(unitTypeId), open = Marks.Open(w, c, unitTypeId);
        string s = "  ·  " + Marks.Describe(w, unitTypeId, have <= 0f ? open : have);
        if (open > have + 0.01f && have > 0f) s += $" (a fábrica já faz {Marks.Roman((int)open)})";
        return s;
    }

    private static string Why(World w, Country c, StockRow r) =>
        $"{r.Name} ({r.Category})\n"
      + $"· em armazém: {r.Have:0.0} conjuntos, {Marks.Describe(w, r.UnitTypeId, c.StockedMark(r.UnitTypeId))}\n"
      + $"· a indústria já sabe fazer: {Marks.Describe(w, r.UnitTypeId, Marks.Open(w, c, r.UnitTypeId))}\n"
      + $"· fábricas: {r.PerDay:0.00} por dia\n"
      + $"· a tropa pede hoje: {Warehouse.DailyDraw(w, c, r.UnitTypeId):0.00}\n"
      + $"· falta ao exército inteiro: {r.Missing:0.0}\n"
      + $"· exército completo levaria: {Warehouse.Fleet(w, c, r.UnitTypeId):0.0}";

    /// <summary>O símbolo NATO do material, tirado das etiquetas do tipo de unidade — o mesmo desenho que a
    /// fila de produção e os contadores do mapa usam.</summary>
    private static Control Symbol(World w, int unitTypeId)
    {
        try
        {
            var t = w.Units.GetUnitType(unitTypeId);
            return UnitSymbol.Of(UnitCounter.KindOf(t.Stats.Tags), 34f, 24f, NatoSymbol.SpecialtyOf(t.Stats.Tags));
        }
        catch { return Ui.Lbl("▣", 18); }
    }

    private static string Name(World w, Division d)
    {
        if (d.WarName is string n && n.Length > 0) return n;
        try { return w.Units.GetTemplate(d.TemplateId).Name; } catch { return "divisão " + d.Id; }
    }

    /// <summary>--smoke: o armazém em texto, com a prateleira mais apertada à frente.</summary>
    public static string Smoke(World w, Country c)
    {
        var rows = Warehouse.Rows(w, c);
        if (rows.Count == 0) return "sem armazém";
        var top = rows[0];
        string worst = Warehouse.Worst(w, c) is Division d ? $"{Name(w, d)} a {d.Kit:P0}" : "ninguém por armar";
        string mark = Marks.All(w, top.UnitTypeId).Count == 0 ? "sem marcas"
            : $"{top.Name}: prateleira {Marks.Describe(w, top.UnitTypeId, c.StockedMark(top.UnitTypeId))}, "
              + $"fábrica {Marks.Describe(w, top.UnitTypeId, Marks.Open(w, c, top.UnitTypeId))}";
        return $"{rows.Count} prateleiras, marcas: {mark}, exército armado a {Warehouse.Average(w, c):P0} ({worst}); "
             + $"a mais apertada: {top.Name} com {top.Have:0.0} em casa, {top.PerDay:0.00}/dia, "
             + $"faltam {top.Missing:0.0}, {Days(top.Days)}";
    }
}
