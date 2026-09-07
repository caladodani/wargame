using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara da árvore de investigação, à maneira do HoI4: fichas de programa em vez da lista corrida
/// de linhas de texto que aqui estava. Cada ramo abre com uma barra com a chapa dele e o que já está feito,
/// e cada programa é um cartão — chapa do ramo, nome, selo ⚜ quando é de casa, as placas do que muda no
/// país, o preço em dias (e quantos dias são ao ritmo de investigação de agora) e a descrição por baixo.
/// O cartão inteiro é o botão; quando não se pode investigar (laboratórios cheios, já em curso) apaga-se e
/// diz porquê, com a mesma frase que o comando recusaria.
///
/// Só lê o World; despachar o comando é de quem sabe.</summary>
public static class TechView
{
    /// <summary>Chapa de cada ramo. O ramo é dado (tech.branch), por isso o que não estiver aqui leva a
    /// chapa neutra em vez de partir.</summary>
    public static string BranchIcon(string branch) => branch switch
    {
        "Infantaria" => "🪖",
        "Blindados" => "🛡",
        "Artilharia" => "💥",
        "Drones" => "🛩",
        "Logística" => "🚚",
        "Indústria" => "🏭",
        "Doutrina" => "📖",
        "Ciência" => "🔬",
        "Nuclear" => "☢",
        "Aviação" => "✈",
        "Marinha" => "⚓",
        _ => "⚙",
    };

    /// <summary>A árvore por investigar deste país, ramo a ramo. Os ramos das armas (Aviação e Marinha)
    /// vêm primeiro porque são os novos e são onde vive o programa nacional; dentro do ramo os degraus
    /// vêm do mais barato ao mais caro, que é a ordem por que se fazem.</summary>
    public static VBoxContainer Tree(World w, Country c, bool mine, Action<string> onResearch)
    {
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6);
        var open = w.Techs.Values.Where(t => w.CanResearch(c, t.Id)).ToList();
        foreach (var group in open.GroupBy(t => t.Branch)
                                  .OrderByDescending(g => g.Any(t => t.CountryTag is not null))
                                  .ThenBy(g => g.Key))
        {
            v.AddChild(BranchBar(w, c, group.Key));
            var grid = Ui.Grow(new GridContainer { Columns = 2 });
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            foreach (var t in group.OrderBy(t => t.CountryTag is not null).ThenBy(t => t.Cost))
                grid.AddChild(Card(w, c, t, mine, onResearch));
            v.AddChild(grid);
        }
        if (open.Count == 0)
        {
            var none = Ui.Lbl("nada por investigar neste momento", 14);
            none.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(none);
        }
        return v;
    }

    /// <summary>Barra do ramo: chapa, nome e quantos degraus dele já estão feitos — o mesmo relance que a
    /// árvore do HoI4 dá pela coluna acesa.</summary>
    private static PanelContainer BranchBar(World w, Country c, string branch)
    {
        var all = w.Techs.Values.Where(t => t.Branch == branch && World.TechIsFor(t, c)).ToList();
        int done = all.Count(t => c.Techs.Contains(t.Id));

        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Ui.Box(Ui.SurfaceHi, 5));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); bar.AddChild(row);
        var icon = Ui.Lbl(BranchIcon(branch), 18);
        icon.AddThemeColorOverride("font_color", Ui.Accent);
        row.AddChild(icon);
        var name = Ui.Lbl(branch, 17);
        name.AddThemeColorOverride("font_color", Ui.Accent);
        row.AddChild(Ui.Grow(name));
        var count = Ui.Lbl($"{done}/{all.Count} feitos", 14);
        count.AddThemeColorOverride("font_color", Ui.TextDim);
        row.AddChild(count);
        return bar;
    }

    /// <summary>Ficha de um programa por investigar.</summary>
    private static PanelContainer Card(World w, Country c, Tech t, bool mine, Action<string> onResearch)
    {
        string? why = new ResearchTechCommand(c.Id, t.Id).Validate(w);
        bool ok = mine && why is null, home = t.CountryTag is not null;
        var tint = home ? Ui.Accent : Ui.Text;
        if (!ok) tint = tint.Darkened(0.35f);

        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", Ui.Box(ok ? new Color(0.17f, 0.16f, 0.11f, 0.95f)
                                                        : new Color(0.12f, 0.12f, 0.13f, 0.88f), 8));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); card.AddChild(row);
        row.AddChild(Emblem(BranchIcon(t.Branch), tint));

        var cell = Ui.Grow(new VBoxContainer()); cell.AddThemeConstantOverride("separation", 2);
        var title = new HBoxContainer(); title.AddThemeConstantOverride("separation", 6);
        var name = Ui.Lbl(t.Name, 16);
        name.AddThemeColorOverride("font_color", tint);
        title.AddChild(Ui.Grow(name));
        if (home) title.AddChild(Seal(c));
        cell.AddChild(title);

        var plates = new HBoxContainer(); plates.AddThemeConstantOverride("separation", 6);
        float speed = MathF.Max(0.01f, c.Stat("research_speed"));
        plates.AddChild(Plate($"{t.Cost:0} dias  ·  {(int)MathF.Ceiling(t.Cost / speed)} ao ritmo de agora", Ui.TextDim));
        if (w.TechEffects.TryGetValue(t.Id, out var effs))
            foreach (var (key, mul) in effs)
                plates.AddChild(Plate($"{Ui.StatName(key)} ×{mul:0.00}", Ui.Good));
        cell.AddChild(plates);

        if (t.Description is { Length: > 0 })
        {
            var note = Ui.Lbl(t.Description, 12);
            note.AddThemeColorOverride("font_color", Ui.TextDim);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            cell.AddChild(note);
        }
        if (mine && why is not null)
        {
            var no = Ui.Lbl(why, 12);
            no.AddThemeColorOverride("font_color", Ui.Danger);
            cell.AddChild(no);
        }
        row.AddChild(cell);

        if (ok) { string id = t.Id; Ui.Click(card, () => onResearch(id), $"investigar {t.Name}"); }
        else if (why is not null) card.TooltipText = why;
        return card;
    }

    /// <summary>Placa de dados do cartão (dias, efeito): o mesmo vidro das placas de preço do estado-maior.</summary>
    private static PanelContainer Plate(string text, Color tone)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(tone with { A = 0.16f }, 4));
        var l = Ui.Lbl(text, 13);
        l.AddThemeColorOverride("font_color", tone);
        chip.AddChild(l);
        return chip;
    }

    /// <summary>Chapa do ramo emoldurada, como o retrato do comandante na folha do estado-maior.</summary>
    private static PanelContainer Emblem(string icon, Color tint)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(46, 46) };
        frame.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink, 6));
        var face = Ui.Lbl(icon, 24);
        face.HorizontalAlignment = HorizontalAlignment.Center;
        face.VerticalAlignment = VerticalAlignment.Center;
        face.AddThemeColorOverride("font_color", tint);
        frame.AddChild(face);
        return frame;
    }

    /// <summary>Selo do programa que é só deste país — o mesmo ⚜ do estado-maior e das leis nacionais.</summary>
    private static PanelContainer Seal(Country c)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 4));
        var l = Ui.Lbl($"⚜ {c.Tag}", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"programa de {c.Name}: nenhum outro país o investiga";
        chip.AddChild(l);
        return chip;
    }
}
