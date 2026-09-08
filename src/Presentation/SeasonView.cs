using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara das estações do ano: a chapa que vive na barra de topo, a luz que o mapa toma e a linha que
/// explica, na região aberta, o que o tempo lá vai custar à tropa. O calendário andava e o ecrã não dava
/// sinal nenhum — agora Janeiro vê-se antes de se sentir.
///
/// Só lê o World: quem gasta a organização é o WeatherSystem.</summary>
public static class SeasonView
{
    /// <summary>Cor da estação: azul frio no Inverno, verde na Primavera, ouro no Verão, cobre no Outono.
    /// Vem do id da tabela; uma estação nova que ninguém previu fica no cinzento neutro.</summary>
    public static Color Tint(string id) => id switch
    {
        "inverno" => new Color(0.62f, 0.76f, 0.95f),
        "primavera" => new Color(0.55f, 0.85f, 0.62f),
        "verao" => new Color(0.98f, 0.85f, 0.42f),
        "outono" => new Color(0.92f, 0.66f, 0.38f),
        _ => new Color(0.80f, 0.82f, 0.86f),
    };

    /// <summary>Luz que o mapa toma na estação: a mesma cor, muito lavada, para o mundo mudar de tom sem
    /// deixar de se ler. Sem estação carregada, branco — o mapa fica como sempre esteve.</summary>
    public static Color MapTint(World w) =>
        w.Season is SeasonDef s ? Colors.White.Lerp(Tint(s.Id), 0.16f) : Colors.White;

    /// <summary>Chapa da barra de topo: ícone, nome e, em letra pequena, o que a estação está a fazer.</summary>
    public static PanelContainer Badge(World w)
    {
        var card = new PanelContainer { Name = "SeasonBadge" };
        Paint(card, w);
        return card;
    }

    /// <summary>Repõe o conteúdo da chapa (a estação muda quatro vezes por ano, o painel é redesenhado todos
    /// os dias — por isso muda-se o que lá está em vez de criar um controlo novo).</summary>
    public static void Paint(PanelContainer card, World w)
    {
        foreach (var child in card.GetChildren()) child.QueueFree();
        if (w.Season is not SeasonDef s) { card.Visible = false; return; }
        card.Visible = true;

        var tint = Tint(s.Id);
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(tint.R * 0.22f, tint.G * 0.22f, tint.B * 0.26f, 0.92f), 6));
        card.TooltipText = $"{s.Name}: {s.Note}\nMarcha ×{s.MoveMult:0.00} · recomposição ×{s.OrgMult:0.00} · desgaste {s.Attrition:0.0} org/dia em campo";

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); card.AddChild(row);
        row.AddChild(Glyph.Make(s.Glyph, 20, tint));
        var name = Ui.Lbl(s.Name, 17);
        name.AddThemeColorOverride("font_color", tint);
        row.AddChild(name);
        var effect = Ui.Lbl(Effect(s), 14);
        effect.AddThemeColorOverride("font_color", s.MoveMult < 1f ? Ui.Danger : Ui.Good);
        row.AddChild(effect);
    }

    /// <summary>O efeito da estação numa palavra e um número: é o que interessa a quem está a decidir marchar.</summary>
    public static string Effect(SeasonDef s) => s.MoveMult >= 1f
        ? $"marcha +{(s.MoveMult - 1f):P0}"
        : $"marcha −{(1f - s.MoveMult):P0}";

    /// <summary>Linha para o painel da região: o que o tempo custa ali, naquele terreno, por dia. Vazio quando
    /// não há estação carregada ou quando ela não castiga nada.</summary>
    public static string RegionLine(World w, Region r) => RegionState.SeasonNote(w, r);
}
