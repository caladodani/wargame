using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A ficha do que a região DÁ, ao lado da ficha do que o chão tira: dinheiro por dia, homens por
/// dia, os depósitos e as obras já de pé, cada um com a sua chapa e a sua explicação. É a metade que
/// faltava — o rendimento estava lá, mas escrito no meio de um parágrafo corrido de emojis e pontos, sem
/// uma palavra sobre de onde vinha o número.
///
/// As contas não são daqui: vêm todas do RegionYield, que por sua vez usa o EconomySystem e a mesma
/// parcela que o ManpowerSystem soma ao pool.</summary>
public static class YieldView
{
    /// <summary>Cartão com uma chapa por parcela. As parcelas vêm em fila corrida (HFlow) porque são
    /// poucas e de largura diferente — numa região sem recursos são duas, numa com tudo são seis.</summary>
    public static PanelContainer Card(World w, Region r)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.85f }, 6));
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 3); card.AddChild(box);

        var head = Ui.Lbl("o que esta terra dá", 14);
        head.AddThemeColorOverride("font_color", Ui.TextDim);
        head.TooltipText = "por dia, a quem controla a região: o cofre e o recrutamento saem daqui;"
                         + " os depósitos e as obras contam para o país inteiro, não só para esta terra";
        box.AddChild(head);

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        foreach (var p in RegionYield.Parts(w, r))
        {
            var plate = Ui.Counter(Glyph.Make(p.Glyph, 17, Ui.Accent), out var value, out var note);
            value.Text = p.Value;
            note.Text = p.Name;
            plate.TooltipText = $"{p.Name}: {p.Value}\n{p.Note}";
            flow.AddChild(plate);
        }
        box.AddChild(flow);
        return card;
    }

    /// <summary>--smoke: a ficha do que a região dá, em texto.</summary>
    public static string Smoke(World w, Region r)
    {
        var parts = RegionYield.Parts(w, r);
        string extra = parts.Count > 2 ? ", mais " + string.Join(" e ", parts.Skip(2).Select(p => $"{p.Value} de {p.Name}")) : "";
        return $"ficha do que {r.Name} dá: {parts[0].Value} por dia e {parts[1].Value} homens por dia{extra}";
    }
}
