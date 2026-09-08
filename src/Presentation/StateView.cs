using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A ficha de COMO a região está, a terceira do painel ao lado do que o chão tira (GroundView) e do
/// que a terra dá (YieldView): gente, estrada, forte, cais, tempo, resistência, integração, fábricas civis,
/// a conta do modo de mapa — e, por baixo, as obras a andar com barra e dias, e a batalha se a houver.
///
/// Era a última metade escrita em parágrafo corrido: uma linha de emojis separados por pontos que crescia
/// com o jogo e que se lia de uma ponta à outra para encontrar um número. No HoI4 isto é uma grelha de
/// ícones — vê-se o forte sem ler a frase do forte.
///
/// As contas não são daqui: vêm todas do RegionState, que por sua vez usa o GroundSystem, o WeatherSystem,
/// o IntegrationSystem, o ConstructionSystem e o Industry.</summary>
public static class StateView
{
    /// <summary>Cartão com uma chapa por parcela, as obras por baixo e a batalha no fim.</summary>
    public static PanelContainer Card(World w, Region r, int? viewerId, string mapMode)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.85f }, 6));
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 3); card.AddChild(box);

        var head = Ui.Lbl("como esta terra está", 14);
        head.AddThemeColorOverride("font_color", Ui.TextDim);
        head.TooltipText = "o estado da região agora: o que ela tem de seu e o que a guerra e o tempo lhe fizeram";
        box.AddChild(head);

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        foreach (var p in RegionState.Parts(w, r, viewerId, mapMode))
        {
            var plate = Ui.Counter(Glyph.Make(p.Glyph, 17, Ui.Accent), out var value, out var note);
            value.Text = p.Value;
            note.Text = p.Name;
            plate.TooltipText = $"{p.Name}: {p.Value}\n{p.Note}";
            flow.AddChild(plate);
        }
        box.AddChild(flow);

        // as obras a andar: chapa, nome, barra do que já está feito e os dias que faltam
        foreach (var o in RegionState.Works(w, r))
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            row.TooltipText = $"obra: {o.Name}\n{o.Note}";
            row.AddChild(Glyph.Make(o.Glyph, 15, Ui.Good));
            row.AddChild(Ui.Grow(Ui.Lbl($"obra: {o.Name}", 15)));
            row.AddChild(Ui.Bar(o.Progress, Ui.Good, 70f));
            var dias = Ui.Lbl($"{o.DaysLeft} d", 15);
            dias.AddThemeColorOverride("font_color", Ui.Text);
            row.AddChild(dias);
            box.AddChild(row);
        }

        if (RegionState.BattleLine(w, r) is string bat)
        {
            var line = Ui.Lbl(RegionRenderer.BattleMark + bat, 15);
            line.AddThemeColorOverride("font_color", Ui.Danger);
            box.AddChild(line);
        }
        return card;
    }

    /// <summary>--smoke: a ficha do estado, em texto.</summary>
    public static string Smoke(World w, Region r, int? viewerId, string mapMode)
    {
        var parts = RegionState.Parts(w, r, viewerId, mapMode);
        var works = RegionState.Works(w, r);
        return $"ficha de como {r.Name} está: {parts.Count} parcelas ("
             + string.Join(", ", parts.Select(p => $"{p.Name} {p.Value}")) + ")"
             + (works.Count > 0 ? $", {works.Count} obras a andar" : ", sem obras");
    }
}
