using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara do gabinete civil (HoI4: political advisors). O país tinha estado-maior e não tinha governo:
/// não havia uma única cadeira civil para preencher, nem ecrã onde se visse que a indústria rende mais porque
/// há um homem sentado a tratar dela.
///
/// Agora há quatro pastas, cada uma com a sua chapa: quem lá está, o que multiplica, há quanto tempo serve e
/// quanto leva por dia. Por baixo, os candidatos daquela pasta — cada botão diz o preço e o que faz, acende
/// quando o cofre chega e leva no tooltip a razão pela qual não se pode nomear.
///
/// Só lê o World; nomear e demitir é de quem sabe despachar comandos.</summary>
public static class CabinetView
{
    /// <summary>Cartão do gabinete deste país. Null quando a BD não traz pastas nenhumas.</summary>
    public static PanelContainer? Card(World w, Country c, int day, Action<string> onAppoint, Action<string> onDismiss)
    {
        if (w.CabinetSlots.Count == 0) return null;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.12f, 0.13f, 0.18f, 0.94f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 5); card.AddChild(v);

        float wages = CabinetSystem.Wages(w, c);
        var head = Ui.Lbl($"🏛 Gabinete: {c.Cabinet.Count}/{w.CabinetSlots.Count} pastas ocupadas · folha de {wages:0.0}/dia", 17);
        head.AddThemeColorOverride("font_color", Ui.Accent);
        v.AddChild(head);

        foreach (var slot in w.CabinetSlots)
        {
            v.AddChild(Ui.Rule());
            string? seated = c.Cabinet.GetValueOrDefault(slot.Id);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Ui.Lbl($"{slot.Icon} {slot.Name}", 16));
            if (seated is not null && w.AdvisorDefs.TryGetValue(seated, out var sat))
            {
                int since = day - c.CabinetSince.GetValueOrDefault(slot.Id);
                var who = Ui.Lbl($"{sat.Icon} {sat.Name} — {Effects(sat)} · {since} dias de casa", 15);
                who.AddThemeColorOverride("font_color", Ui.Good);
                row.AddChild(Ui.Grow(who));
                string id = slot.Id;
                row.AddChild(Ui.Btn("Demitir", () => onDismiss(id), 0, Ui.Kind.Danger));
            }
            else
            {
                var empty = Ui.Lbl("cadeira vazia", 15);
                empty.AddThemeColorOverride("font_color", Ui.TextDim);
                row.AddChild(Ui.Grow(empty));
            }
            v.AddChild(row);

            foreach (var a in CabinetSystem.Candidates(w, c, slot.Id))
            {
                if (a.Id == seated) continue;
                string id = a.Id;
                var b = Ui.Btn($"{a.Icon} {a.Name}   —   {Effects(a)}   ·   {a.Cost:0} pp + {a.Cost * w.Rule("advisor_wage_share", 0.01f):0.0}/dia",
                               () => onAppoint(id), 0, Ui.Kind.Normal);
                b.Disabled = c.Money < a.Cost;
                b.TooltipText = b.Disabled ? $"faltam {a.Cost - c.Money:0} pontos de produção" : a.Note;
                b.AddThemeFontSizeOverride("font_size", 14);
                v.AddChild(Ui.Grow(b));
            }
        }
        return card;
    }

    /// <summary>O que o homem vale, em texto curto: "indústria +10%, recruta +15%".</summary>
    public static string Effects(AdvisorDef a) =>
        a.Effects.Count == 0 ? "sem efeito"
        : string.Join(", ", a.Effects.OrderBy(kv => kv.Key)
            .Select(kv => $"{Pretty(kv.Key)} {(kv.Value >= 1f ? "+" : "")}{(kv.Value - 1f) * 100f:0}%"));

    /// <summary>Nome de cozinha do stat em português de painel (a tabela vive no Ui, que é de todos).</summary>
    private static string Pretty(string key) => Ui.StatName(key);
}
