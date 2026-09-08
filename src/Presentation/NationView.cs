using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A ficha de como o PAÍS está, no topo da aba da Nação: fábricas civis, fábricas militares,
/// estaleiros, cofre, homens, estabilidade, divisões, regiões, laboratórios e lugar no mundo — e, por baixo,
/// os recursos que a terra dá.
///
/// Era o último parágrafo corrido do jogo: quatro linhas de "coisa ×0,00" separadas por espaços, que se liam
/// de uma ponta à outra para encontrar um número e não diziam de onde ele vinha. No HoI4 isto é a barra que
/// está sempre no topo do ecrã — vê-se quantas fábricas sobram sem ler a frase das fábricas.
///
/// As contas não são daqui: vêm do NationSheet, que por sua vez usa o Industry, o EconomySystem, o
/// ManpowerSystem, o StabilitySystem, o ResearchSystem e o ResourceSystem.</summary>
public static class NationView
{
    /// <summary>Cartão com uma chapa por parcela e, se os houver, os recursos numa segunda fila.</summary>
    public static PanelContainer Card(World w, Country c)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.85f }, 6));
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 3); card.AddChild(box);

        box.AddChild(Head("como o país está", "as contas da nação hoje: o que produz, o que rende, quem tem e onde está entre os outros"));
        box.AddChild(Flow(NationSheet.Parts(w, c, Ui.StatName), Ui.Accent));

        var res = NationSheet.Resources(w, c, Ui.StatName);
        if (res.Count > 0)
        {
            box.AddChild(Ui.Rule());
            box.AddChild(Head("o que a terra dá", "recursos estratégicos das regiões que controlamos"));
            box.AddChild(Flow(res, Ui.Good));
        }
        return card;
    }

    private static Label Head(string text, string tip)
    {
        var head = Ui.Lbl(text, 14);
        head.AddThemeColorOverride("font_color", Ui.TextDim);
        head.TooltipText = tip;
        return head;
    }

    private static HFlowContainer Flow(List<NationPart> parts, Color ink)
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        foreach (var p in parts)
        {
            var plate = Ui.Counter(Glyph.Make(p.Glyph, 17, ink), out var value, out var note);
            value.Text = p.Value;
            note.Text = p.Name;
            plate.TooltipText = $"{p.Name}: {p.Value}\n{p.Note}";
            flow.AddChild(plate);
        }
        return flow;
    }

    /// <summary>--smoke: a ficha da nação, em texto.</summary>
    public static string Smoke(World w, Country c)
    {
        var parts = NationSheet.Parts(w, c, Ui.StatName);
        var res = NationSheet.Resources(w, c, Ui.StatName);
        return $"ficha de como {c.Tag} está: {parts.Count} parcelas ("
             + string.Join(", ", parts.Select(p => $"{p.Name} {p.Value}")) + ")"
             + (res.Count > 0 ? $", recursos: {string.Join(", ", res.Select(p => $"{p.Name} {p.Value}"))}" : ", sem recursos");
    }
}
