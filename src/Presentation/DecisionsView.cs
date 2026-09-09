using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>O quadro das decisões (HoI4: o ecrã das decisões e das missões).
///
/// Era uma listinha de três linhas na aba Nação. Agora é um quadro com abas por categoria e um cartão por
/// decisão, com as mesmas mãos das pranchetas: chapa desenhada, preço em cima, efeito por palavras,
/// barra de tempo quando está a correr, e o cartão CINZENTO com o motivo quando a porta está fechada —
/// esconder uma decisão é esconder uma razão para jogar de outra maneira.
///
/// Só lê o World e chama o comando; quem decide se se pode assinar é o Decisions.Check, o mesmo que o
/// comando usa. As categorias e os cartões vêm todos da base de dados.</summary>
public static class DecisionsView
{
    /// <summary>As categorias pela ordem da tabela.</summary>
    public static List<DecisionCategoryDef> Categories(World w) =>
        w.DecisionCategories.Values.OrderBy(d => d.Sort).ThenBy(d => d.Id).ToList();

    /// <summary>Cor da chapa de uma categoria: a mesma convenção das barras dos partidos — cada categoria
    /// tem sempre a mesma cor em todos os ecrãs.</summary>
    public static Color Of(DecisionCategoryDef d) => PartyView.Palette[Math.Clamp(d.Sort - 1, 0, PartyView.Palette.Length - 1)];

    /// <summary>O quadro de uma categoria: um cartão por decisão. Vazio nunca acontece — uma categoria
    /// sem decisões não devia estar na tabela —, mas se acontecer devolve-se uma linha a dizê-lo.</summary>
    public static Control Board(World w, Country c, bool mine, DecisionCategoryDef cat, Action<string> onActivate)
    {
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6);
        var rows = Decisions.InCategory(w, c, cat.Id);
        if (rows.Count == 0) { v.AddChild(Ui.Lbl("sem decisões nesta pasta", 15)); return v; }
        foreach (var (def, blocked) in rows) v.AddChild(Card(w, c, mine, cat, def, blocked, onActivate));
        return v;
    }

    /// <summary>Um cartão. Estados: a correr (com barra de dias e, se for missão, a barra da meta), em
    /// espera, fechada (cinzenta, com o motivo) e por assinar (com o botão e o preço).</summary>
    public static PanelContainer Card(World w, Country c, bool mine, DecisionCategoryDef cat, DecisionDef def,
                                      string? blocked, Action<string> onActivate)
    {
        var act = Decisions.Active(w, c.Id, def.Id);
        bool running = act is not null;
        var tint = Of(cat);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(running ? new Color(0.14f, 0.18f, 0.16f, 0.95f)
                                                              : new Color(0.12f, 0.14f, 0.18f, 0.92f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); card.AddChild(v);

        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        head.AddChild(Glyph.Make(def.Glyph.Length > 0 ? def.Glyph : cat.Glyph, 22,
                                 blocked is null || running ? tint : Ui.TextDim, def.Note));
        var name = Ui.Lbl(def.Name + (def.IsMission ? "  ⏱ missão" : ""), 17);
        name.AddThemeColorOverride("font_color", running ? Ui.Good : blocked is null ? Ui.Text : Ui.TextDim);
        head.AddChild(Ui.Grow(name));
        var price = Ui.Lbl(Decisions.CostText(def), 14);
        price.AddThemeColorOverride("font_color", Ui.TextDim);
        head.AddChild(price);

        if (def.Note.Length > 0)
        {
            var note = Ui.Wrapped(def.Note, 520f, 14);
            note.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(note);
        }

        if (Decisions.EffectText(w, def) is string eff && eff.Length > 0)
        {
            var el = Ui.Wrapped(eff + (def.Days > 0 ? $"  ·  {def.Days} dias" : ""), 520f, 15);
            el.AddThemeColorOverride("font_color", running ? Ui.Good : Ui.Accent);
            v.AddChild(el);
        }

        if (running && act is not null)
        {
            int left = Math.Max(0, act.UntilDay - w.Clock.Day + 1);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
            row.AddChild(Ui.Grow(Ui.Lbl($"a correr — faltam {left} dia{(left == 1 ? "" : "s")}", 15)));
            row.AddChild(Ui.Bar(def.Days <= 0 ? 1f : Math.Clamp(1f - (float)left / def.Days, 0f, 1f), Ui.Good, 120f));

            if (def.IsMission && act.MissionUntil >= 0)
            {
                int dead = Math.Max(0, act.MissionUntil - w.Clock.Day);
                float p = Decisions.GoalProgress(w, c, def, act);
                var mrow = new HBoxContainer(); mrow.AddThemeConstantOverride("separation", 8); v.AddChild(mrow);
                var goal = Ui.Lbl($"⏱ {Decisions.GoalText(w, c, def, act)} · prazo em {dead} dia{(dead == 1 ? "" : "s")}", 15);
                goal.AddThemeColorOverride("font_color", dead <= 10 && p < 1f ? Ui.Danger : Ui.Accent);
                mrow.AddChild(Ui.Grow(goal));
                mrow.AddChild(Ui.Bar(p, p >= 1f ? Ui.Good : Ui.Accent, 120f));
                var prize = Ui.Lbl($"cumprida: +{def.RewardPolitical:0} pp · falhada: −{def.FailPolitical:0} pp,"
                                 + $" −{def.FailStability:0} de estabilidade", 13);
                prize.AddThemeColorOverride("font_color", Ui.TextDim);
                v.AddChild(prize);
            }
        }
        else if (mine)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
            if (def.IsMission)
            {
                var m = Ui.Lbl($"meta: {Decisions.MetricName(w, def.GoalKey)} {def.GoalValue:+0.#;-0.#}"
                             + $" em {def.MissionDays} dias · prémio {def.RewardPolitical:0} pp", 14);
                m.AddThemeColorOverride("font_color", Ui.TextDim);
                row.AddChild(Ui.Grow(m));
            }
            else row.AddChild(Ui.Grow(Ui.Lbl("", 14)));

            if (blocked is null)
                row.AddChild(Ui.Big("Assinar", () => onActivate(def.Id), 150, Ui.Kind.Primary));
            else
            {
                var why = Ui.Lbl("🔒 " + blocked, 15);
                why.AddThemeColorOverride("font_color", Ui.TextDim);
                row.AddChild(why);
            }
        }
        return card;
    }

    /// <summary>Uma linha de prova para o headless: o que o quadro tem para mostrar ao jogador.</summary>
    public static string Smoke(World w, int countryId, int cards) =>
        $"{Decisions.Smoke(w, countryId)}, {cards} cartões desenhados";
}
