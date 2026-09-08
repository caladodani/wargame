using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>A ficha de uma formação destacada — uma asa no céu de uma região ou uma esquadra no mar de uma
/// costa.
///
/// As missões eram uma linha corrida de texto: "Braga — Superioridade aérea, 4 asas há 12 dias". Não se
/// via de relance quem estava a ganhar aquele céu, e sobretudo não havia ninguém ali: as divisões têm nome
/// próprio e honras de batalha, os aviões e os navios não tinham nada. Agora cada formação leva o nome do
/// fundo do país (formation_name), com o selo ⚜ quando é um nome de casa, e a ficha mostra a tarefa, o
/// sítio, a força de cada lado na mesma barra e o que se pode fazer com ela.</summary>
public static class FormationView
{
    /// <summary>O que a ficha precisa de saber. Domain diz a arma (World.Air / World.Sea): muda a palavra
    /// que conta as unidades e o que se chama a um céu ou a um mar disputado.</summary>
    public readonly record struct Info(string Name, bool Home, string Domain, string MissionGlyph,
                                       string Mission, string Place, float Strength, float Foe, int Days,
                                       string Note, string Extra = "");

    /// <summary>A ficha inteira. As acções (Ver, Recolher) vêm de fora: quem manda na formação é o painel.</summary>
    public static PanelContainer Plate(Info i, Control? actions = null)
    {
        bool sea = i.Domain == World.Sea;
        var tint = i.Foe > i.Strength ? Ui.Danger : i.Foe > 0f ? Ui.Accent : Ui.Good;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.13f, 0.14f, 0.16f, 0.95f), 8));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); card.AddChild(v);

        // 1.ª linha: o nome da formação, o selo de casa e há quanto tempo está fora
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 6);
        head.AddChild(Glyph.Make(sea ? "ancora" : "asa", 18, tint));
        var name = Ui.Lbl(i.Name.Length > 0 ? i.Name : (sea ? "esquadra sem nome" : "asa sem nome"), 17);
        name.AddThemeColorOverride("font_color", Ui.Text);
        head.AddChild(Ui.Grow(name));
        if (i.Home) head.AddChild(Seal(i.Name, sea));
        var days = Ui.Lbl($"{i.Days} dia{(i.Days == 1 ? "" : "s")}", 13);
        days.AddThemeColorOverride("font_color", Ui.TextDim);
        head.AddChild(days);
        v.AddChild(head);

        // 2.ª linha: a chapa da tarefa, a tarefa e o sítio
        var job = new HBoxContainer(); job.AddThemeConstantOverride("separation", 5);
        job.AddChild(Glyph.Make(i.MissionGlyph, 15, Ui.TextDim));
        var task = Ui.Lbl($"{i.Mission}  ·  {i.Place}" + (i.Extra.Length > 0 ? $"  ·  {i.Extra}" : ""), 15);
        task.AddThemeColorOverride("font_color", Ui.TextDim);
        task.TooltipText = i.Note;
        job.AddChild(Ui.Grow(task));
        v.AddChild(job);

        // 3.ª linha: a balança daquele céu ou daquele mar, numa barra só
        var scale = new HBoxContainer(); scale.AddThemeConstantOverride("separation", 6);
        string unit = sea ? "navios" : "asas";
        var ours = Ui.Lbl($"{i.Strength:0.#} {unit}", 14);
        ours.AddThemeColorOverride("font_color", Ui.Text);
        scale.AddChild(ours);
        float total = i.Strength + i.Foe;
        var bar = Ui.Bar(total <= 0f ? 1f : i.Strength / total, tint, 150);
        bar.TooltipText = i.Foe > 0f
            ? $"{(sea ? "Mar" : "Céu")} disputado: {i.Strength:0.#} contra {i.Foe:0.#}. "
              + (sea ? "Vai aço ao fundo dos dois lados todos os dias." : "Abatem-se aviões dos dois lados todos os dias.")
            : $"{(sea ? "Mar" : "Céu")} nosso: ninguém lá está a disputá-lo.";
        scale.AddChild(bar);
        var them = Ui.Lbl(i.Foe > 0f ? $"⚔ {i.Foe:0.#} deles" : "sem oposição", 14);
        them.AddThemeColorOverride("font_color", i.Foe > 0f ? Ui.Danger : Ui.TextDim);
        scale.AddChild(Ui.Grow(them));
        v.AddChild(scale);

        if (actions is not null) v.AddChild(actions);
        card.TooltipText = i.Note;
        return card;
    }

    /// <summary>Este nome veio do fundo do país e não do comum?</summary>
    public static bool IsHome(World w, int countryId, string domain, string name) =>
        w.Countries.TryGetValue(countryId, out var c) && w.IsHomeFormationName(c, domain, name);

    private static PanelContainer Seal(string name, bool sea)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 3));
        var l = Ui.Lbl("⚜", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"{name}: nome de casa — {(sea ? "nenhuma outra marinha" : "nenhuma outra força aérea")} o usa";
        chip.AddChild(l);
        return chip;
    }
}
