using System.Collections.Generic;
using System.Linq;
using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A conta aberta de cada número do país — a caixa que no HoI4 se abre por baixo do rato quando se
/// passa por cima de um valor: linha a linha, quem o multiplica e por quanto. Aqui os números saíam certos
/// e mudos, e a única forma de perceber porque é que a nossa indústria rendia aquilo era ir somar leis,
/// tecnologias e conselheiros de cabeça.
///
/// Quem conta é o StatLedger (WarGame.Core); isto só desenha: uma chapa por característica, com o valor de
/// partida à esquerda, o valor de hoje à direita, e por baixo uma linha por fonte com a chapa da família
/// (tabela stat_source), o nome próprio da fonte e quanto ela mexe, a verde ou a vermelho.</summary>
public static class StatSheetView
{
    /// <summary>A folha inteira: uma chapa por característica que alguém mexe, e no fim a lista curta das
    /// que ninguém mexe — que também é resposta ("não é de lá que vem").</summary>
    public static List<Control> Board(World w, Country c)
    {
        var made = new List<Control>();
        var touched = StatLedger.Touched(w, c);
        foreach (var key in touched) made.Add(Card(w, c, key));

        var quiet = StatLedger.Keys(w, c).Where(k => !touched.Contains(k)).Select(k => StatLedger.Name(w, k)).ToList();
        if (quiet.Count > 0)
        {
            var l = Ui.Lbl($"Sem nada a mexer ({quiet.Count}): {string.Join(", ", quiet)}", 14);
            l.AddThemeColorOverride("font_color", Ui.TextDim);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            made.Add(l);
        }
        return made;
    }

    /// <summary>A chapa de uma característica: de quanto partia, quanto vale hoje, e todas as fontes.</summary>
    public static PanelContainer Card(World w, Country c, string key)
    {
        float bas = StatLedger.Base(c, key), now = StatLedger.Total(w, c, key);
        var lines = StatLedger.Lines(w, c, key);

        var plate = new PanelContainer();
        plate.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface, 8));
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", 3);
        plate.AddChild(v);

        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8);
        if (w.CountryStatDefs.TryGetValue(key, out var def) && def.Glyph.Length > 0)
            head.AddChild(Glyph.Make(def.Glyph, 20, Ui.Accent));
        var name = Ui.Lbl(StatLedger.Name(w, key), 17);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(name);
        var total = Ui.Lbl($"{bas:0.##} → {now:0.##}", 17);
        total.AddThemeColorOverride("font_color", now >= bas ? Ui.Good : Ui.Danger);
        head.AddChild(total);
        head.AddChild(Chip(bas <= 0f ? 0f : now / bas));
        v.AddChild(head);

        if (w.CountryStatDefs.TryGetValue(key, out var d2) && d2.Note.Length > 0)
        {
            var note = Ui.Lbl(d2.Note, 13);
            note.AddThemeColorOverride("font_color", Ui.TextDim);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            v.AddChild(note);
        }

        foreach (var l in lines) v.AddChild(SourceRow(w, l));
        return plate;
    }

    /// <summary>Uma fonte: a chapa da família, como ela se chama, o nome próprio e quanto mexe.</summary>
    private static Control SourceRow(World w, StatLine l)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        var fam = w.StatSourceDefs.GetValueOrDefault(l.Kind);
        if (fam is not null && fam.Glyph.Length > 0) row.AddChild(Glyph.Make(fam.Glyph, 15, Ui.TextDim));
        var kind = Ui.Lbl(fam?.Name ?? l.Kind, 13);
        kind.AddThemeColorOverride("font_color", Ui.TextDim);
        row.AddChild(kind);
        var who = Ui.Lbl(l.Name, 14);
        who.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        who.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(who);
        row.AddChild(Chip(l.Mult));
        return row;
    }

    /// <summary>A pastilha da percentagem, com sinal e cor: é o que se lê de relance.</summary>
    private static Control Chip(float mult)
    {
        float pct = (mult - 1f) * 100f;
        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", Ui.Box(mult >= 1f ? new Color(0.14f, 0.22f, 0.14f)
                                                                : new Color(0.24f, 0.13f, 0.13f), 4));
        var l = Ui.Lbl($"{(pct >= 0f ? "+" : "−")}{MathF.Abs(pct):0.#}%", 13);
        l.AddThemeColorOverride("font_color", mult >= 1f ? Ui.Good.Lightened(0.35f) : Ui.Danger.Lightened(0.35f));
        box.AddChild(l);
        return box;
    }
}
