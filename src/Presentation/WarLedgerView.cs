using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>O cartão do saldo de uma guerra, na aba "Guerras" do painel Mundo: os dois beligerantes com
/// bandeira, a balança de força entre eles, o veredicto de quem está por cima e, por baixo de cada nome, as
/// cinco chapas do que a guerra lhe deu e lhe tirou.
///
/// Era a última secção escrita em texto corrido: "⚔ Alfa vs Beta (12 dias)" e por baixo uma barra feita de
/// blocos de texto — "16 div ██████░░░░ 9 div" — que só dizia quantas divisões cada um tinha. As regiões
/// tomadas, as divisões perdidas e as batalhas ganhas já eram contadas pelo WarStatsSystem desde sempre e
/// não apareciam em lado nenhum. No HoI4 é este o ecrã que se abre para saber como vai a guerra.
///
/// As contas não são daqui: vêm do WarLedger, que usa os contadores do WarStatsSystem e a mesma regra de
/// quem ganhou que o arquivo usa quando a paz é assinada.</summary>
public static class WarLedgerView
{
    /// <summary>Cartão de uma guerra a decorrer.</summary>
    public static PanelContainer Card(World w, WarInfo war, int? viewerId)
    {
        bool mine = viewerId is int me && war.Involves(me);
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(mine ? new Color(0.20f, 0.14f, 0.14f, 0.95f) : Ui.Ink with { A = 0.85f }, 6));
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 3); card.AddChild(box);

        // o cabeçalho: bandeira, nome, dias, e a bandeira do outro do lado de lá
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 6); box.AddChild(top);
        top.AddChild(Flag(w, war.A));
        top.AddChild(Ui.Grow(Ui.Lbl(Name(w, war.A), 17)));
        var days = Ui.Lbl($"{w.Clock.Day - war.StartDay} dias", 14);
        days.AddThemeColorOverride("font_color", Ui.TextDim);
        top.AddChild(days);
        var right = Ui.Lbl(Name(w, war.B), 17);
        right.HorizontalAlignment = HorizontalAlignment.Right;
        top.AddChild(Ui.Grow(right));
        top.AddChild(Flag(w, war.B));

        // a balança: a força de A contra a de B, na barra do jogo e não em blocos de texto
        float bal = WarLedger.Balance(w, war);
        var bar = Ui.Bar(bal, Ui.Accent, 0f);
        bar.TooltipText = $"força de {Name(w, war.A)} {WarLedger.Strength(w, war.A):0} contra"
                        + $" {WarLedger.Strength(w, war.B):0} de {Name(w, war.B)}"
                        + "\norganização × saúde de toda a tropa em campo";
        box.AddChild(bar);

        int battles = WarLedger.Battles(w, war);
        var verdict = Ui.Lbl(WarLedger.Verdict(w, war) + (battles > 0 ? $" · {battles} batalha(s) hoje" : ""), 14);
        verdict.AddThemeColorOverride("font_color", battles > 0 ? Ui.Danger : Ui.TextDim);
        box.AddChild(verdict);

        int? ahead = WarLedger.Ahead(w, war);
        box.AddChild(Side(w, war, war.A, ahead == war.A));
        box.AddChild(Side(w, war, war.B, ahead == war.B));
        return card;
    }

    /// <summary>As cinco chapas de um dos lados. Quem está por cima leva-as em verde.</summary>
    private static HFlowContainer Side(World w, WarInfo war, int countryId, bool ahead)
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        foreach (var p in WarLedger.Parts(w, war, countryId))
        {
            var plate = Ui.Counter(Glyph.Make(p.Glyph, 16, ahead ? Ui.Good : Ui.Accent), out var value, out var note);
            value.Text = p.Value;
            note.Text = p.Name;
            plate.TooltipText = $"{Name(w, countryId)} — {p.Name}: {p.Value}\n{p.Note}";
            flow.AddChild(plate);
        }
        return flow;
    }

    private static TextureRect Flag(World w, int id)
    {
        var fl = Flags.Rect(20);
        fl.Texture = Flags.Of(w.Countries.TryGetValue(id, out var c) ? c.Tag : "");
        fl.Visible = fl.Texture is not null;
        return fl;
    }

    private static string Name(World w, int id) => w.Countries.TryGetValue(id, out var c) ? c.Name : "#" + id;

    /// <summary>--smoke: o saldo da primeira guerra do mundo, em texto.</summary>
    public static string Smoke(World w)
    {
        if (w.Wars.Count == 0) return "sem guerras no mundo";
        var war = w.Wars.Values.OrderBy(x => x.StartDay).First();
        return "saldo da guerra: " + WarLedger.Line(w, war);
    }
}
