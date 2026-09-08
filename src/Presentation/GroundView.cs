using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A ficha do chão, no painel da região: o que aquele terreno tira a quem assalta, paga a quem
/// espera e cobra a quem marcha. Os números vêm do GroundSystem, que por sua vez não faz conta nenhuma de
/// raiz — são os do combate e os do movimento. Aqui só se desenham.</summary>
public static class GroundView
{
    /// <summary>Cartão com o cabeçalho do terreno e uma linha por tropa a quem o chão faz outra coisa.
    /// `pid` é quem olha: sem jogador escolhido, mostra-se o chão para o dono da região.</summary>
    public static PanelContainer Card(World w, Region r, string terrainName, int countryId)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.85f }, 6));
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 2); card.AddChild(box);

        var head = Ui.Lbl($"o que este chão dá e tira  ·  {terrainName}" + (r.River ? "  ·  rio" : ""), 14);
        head.AddThemeColorOverride("font_color", Ui.TextDim);
        head.TooltipText = "a primeira linha é só o terreno, igual para toda a gente; as outras já levam os"
                         + " espíritos e a tecnologia de quem cá combate — e são essas que o combate usa";
        box.AddChild(head);

        var lines = GroundSystem.Explain(w, r, countryId);
        if (lines.Count == 0) { box.AddChild(Ui.Lbl("sem país para medir este chão", 13)); return card; }
        box.AddChild(Header());
        foreach (var l in lines) box.AddChild(Row(l.Who, l.Attack, l.Defend));

        // a fortificação não é do terreno, mas paga-se no mesmo sítio: quem espera aqui leva-a por cima
        if (r.Fort > 0)
        {
            var fort = Ui.Lbl($"forte nível {r.Fort}: ×{GroundSystem.FortDefence(w, r):0.00} a quem defende", 13);
            fort.AddThemeColorOverride("font_color", Ui.Good.Lightened(0.2f));
            box.AddChild(fort);
        }
        // a marcha: os dias que a coluna leva a entrar aqui, com a estrada e a estação de hoje
        var mover = w.Divisions.Values.FirstOrDefault(d => d.CountryId == countryId)
                 ?? w.Divisions.Values.FirstOrDefault();
        if (mover is not null)
        {
            var march = Ui.Lbl($"marcha: {GroundSystem.MarchDays(w, r, mover):0.#} dias para {DivisionView.Title(w, mover)} entrar aqui", 13);
            march.AddThemeColorOverride("font_color", Ui.TextDim);
            march.TooltipText = $"terreno ×{w.MoveCost(r.Terrain):0.00}, estrada ×{1f / MathF.Max(w.Rule("move_infra_floor", 0.5f), r.Infrastructure):0.00}"
                              + $", estação ×{1f / MathF.Max(0.01f, w.SeasonMove):0.00}";
            box.AddChild(march);
        }
        return card;
    }

    /// <summary>Cabeçalho das duas colunas — sem ele, dois números soltos não dizem qual é qual.</summary>
    private static HBoxContainer Header()
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        var who = Ui.Lbl("", 12); row.AddChild(Ui.Grow(who));
        foreach (var t in new[] { "assalto", "defesa" })
        {
            var l = Ui.Lbl(t, 12);
            l.AddThemeColorOverride("font_color", Ui.TextDim);
            l.CustomMinimumSize = new Vector2(62, 0);
            l.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(l);
        }
        return row;
    }

    private static HBoxContainer Row(string who, float att, float def)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        var name = Ui.Lbl(who, 13);
        name.AddThemeColorOverride("font_color", Ui.Text);
        row.AddChild(Ui.Grow(name));
        row.AddChild(Cell(att));
        row.AddChild(Cell(def));
        return row;
    }

    /// <summary>Um número da ficha: verde acima de 1, vermelho abaixo, apagado quando o chão não mexe.</summary>
    private static Label Cell(float mult)
    {
        var l = Ui.Lbl($"×{mult:0.00}", 13);
        l.AddThemeColorOverride("font_color", MathF.Abs(mult - 1f) < 0.005f ? Ui.TextDim
            : mult > 1f ? Ui.Good.Lightened(0.2f) : Ui.Danger.Lightened(0.15f));
        l.CustomMinimumSize = new Vector2(62, 0);
        l.HorizontalAlignment = HorizontalAlignment.Right;
        return l;
    }

    /// <summary>--smoke: o que a ficha do chão diz desta região, em texto.</summary>
    public static string Smoke(World w, Region r, int countryId)
    {
        var lines = GroundSystem.Explain(w, r, countryId);
        if (lines.Count == 0) return "ficha do chão sem país";
        string extra = lines.Count > 1
            ? $", mais {lines.Count - 1} linha{(lines.Count == 2 ? "" : "s")} de quem cá combate ({lines[1].Who} ×{lines[1].Attack:0.00}/×{lines[1].Defend:0.00})"
            : ", sem ninguém a sentir o chão de outra maneira";
        return $"ficha do chão de {r.Name} ({r.Terrain}{(r.River ? "+rio" : "")}): só o terreno, assalto ×{lines[0].Attack:0.00} e defesa ×{lines[0].Defend:0.00}{extra}";
    }
}
