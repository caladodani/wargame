using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Porque é que a encomenda da frente demora o que demora, em chapas: o custo, o que uma fábrica
/// faz por dia, quantas fábricas lá estão, a indústria do país, o ritmo da linha, os homens que a divisão
/// leva e os dias que faltam. É a mesma multiplicação que a fábrica faz — as parcelas vêm do ProductionPlan,
/// que é também quem o ProductionSystem usa para gastar.</summary>
public static class PlanView
{
    /// <summary>Cartão da encomenda `index` da fila. Null se a fila estiver vazia.</summary>
    public static PanelContainer? Card(World w, Country c, int index)
    {
        if (index < 0 || index >= c.Queue.Count) return null;
        var o = c.Queue[index];
        int lines = ProductionPlan.LinesFor(w, c, index);
        string name;
        try { name = o.IsKit ? "o material de " + w.Units.GetUnitType(o.UnitTypeId).Name : w.Units.GetTemplate(o.TemplateId).Name; }
        catch { name = o.IsKit ? "o material U" + o.UnitTypeId : "T" + o.TemplateId; }

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.85f }, 6));
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 3); card.AddChild(box);

        string travao = ProductionPlan.Blocked(w, c, o, lines) ?? "";
        var head = Ui.Lbl($"porque é que {name} demora o que demora" + (travao.Length > 0 ? $"  ·  {travao}" : ""), 14);
        head.AddThemeColorOverride("font_color", travao.Length > 0 ? Ui.Danger.Lightened(0.15f) : Ui.TextDim);
        head.TooltipText = "as parcelas multiplicam-se por esta ordem e dão o trabalho de um dia;"
                         + " o que falta a dividir por ele são os dias que faltam";
        box.AddChild(head);

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        foreach (var p in ProductionPlan.Parts(w, c, o, lines))
        {
            var plate = Ui.Counter(Glyph.Make(p.Glyph, 17, Ui.Accent), out var value, out var note);
            value.Text = p.Value;
            note.Text = p.Name;
            plate.TooltipText = $"{p.Name}: {p.Value}\n{p.Note}";
            if (p.Value == "—" || p.Value == "0") value.AddThemeColorOverride("font_color", Ui.TextDim);
            flow.AddChild(plate);
        }
        box.AddChild(flow);
        return card;
    }

    /// <summary>--smoke: a conta da encomenda da frente, em texto.</summary>
    public static string Smoke(World w, Country c)
    {
        if (c.Queue.Count == 0) return "fila de produção vazia";
        var o = c.Queue[0];
        int lines = ProductionPlan.LinesFor(w, c, 0);
        var parts = ProductionPlan.Parts(w, c, o, lines);
        float dias = ProductionPlan.Days(w, c, o, lines);
        return $"conta da encomenda da frente: {parts.Count} parcelas ({string.Join(", ", parts.Select(p => p.Name + " " + p.Value))})"
             + $", trabalho de um dia {ProductionPlan.DayOutput(w, c, o, lines):0.00}"
             + (dias < 0f ? ", parada" : dias <= 0f ? ", pronta" : $", {MathF.Ceiling(dias):0} dias")
             + (ProductionPlan.Blocked(w, c, o, lines) is string b ? $" ({b})" : "");
    }
}
