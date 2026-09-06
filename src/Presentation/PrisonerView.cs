using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara dos prisioneiros de guerra: a balança dos campos (quantos deles temos, quantos nossos
/// eles têm) e o que esse trabalho vale à indústria. Sem isto os prisioneiros eram um número escondido
/// no save — e são justamente aquilo que faz uma guerra parada continuar a valer a pena.
///
/// Só lê o World: quem os apanha, os põe a trabalhar e os manda para casa é o PrisonerSystem.</summary>
public static class PrisonerView
{
    public static readonly Color Ours = new(0.55f, 0.80f, 0.95f);
    public static readonly Color Theirs = new(0.95f, 0.55f, 0.45f);

    /// <summary>Homens que um país guarda ao todo.</summary>
    public static int Held(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Prisoners.Values.Sum() : 0;

    /// <summary>Homens de A que estão nas mãos de B.</summary>
    public static int HeldBy(World w, int holder, int from) =>
        w.Countries.TryGetValue(holder, out var c) ? c.Prisoners.GetValueOrDefault(from) : 0;

    /// <summary>Número curto para caber nas barras: 12 400 homens vira "12,4 mil", 1 200 000 vira "1,2 M".</summary>
    public static string Short(int men) => men >= 1_000_000 ? $"{men / 1_000_000f:0.0} M"
        : men >= 1_000 ? $"{men / 1_000f:0.0} mil" : men.ToString();

    /// <summary>Bónus de indústria que o trabalho dos prisioneiros está a dar a este país (0 = nenhum).</summary>
    public static float WorkBonus(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.PrisonerMult.GetValueOrDefault("industry", 1f) - 1f : 0f;

    /// <summary>Linha da balança para o painel Guerra: duas barras encostadas, a nossa e a deles, com os
    /// números por cima. Vazia (null) quando não há campos de nenhum lado — a guerra ainda não chegou lá.</summary>
    public static VBoxContainer? Balance(World w, int pid, int foe)
    {
        int ours = HeldBy(w, pid, foe);       // deles, nas nossas mãos
        int theirs = HeldBy(w, foe, pid);     // nossos, nas mãos deles
        if (ours == 0 && theirs == 0) return null;

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2);
        var head = new HBoxContainer(); v.AddChild(head);
        head.AddChild(Ui.Grow(Ui.Lbl("⛓ Prisioneiros", 16)));
        var mark = Ui.Lbl(ours >= theirs ? "a nosso favor" : "contra nós", 14);
        mark.AddThemeColorOverride("font_color", ours >= theirs ? Ui.Good : Ui.Danger);
        head.AddChild(mark);

        float total = MathF.Max(1f, ours + theirs);
        var bars = new HBoxContainer(); bars.AddThemeConstantOverride("separation", 6); v.AddChild(bars);
        bars.AddChild(Side(Short(ours) + " deles", ours / total, Ours));
        bars.AddChild(Side(Short(theirs) + " nossos", theirs / total, Theirs));

        float bonus = WorkBonus(w, pid);
        if (bonus > 0f)
        {
            var note = Ui.Lbl($"a trabalhar na retaguarda: indústria +{bonus:P0}", 14);
            note.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(note);
        }
        return v;
    }

    /// <summary>Mesa da troca de prisioneiros para o painel Guerra: quantos homens mudam de mãos de cada
    /// lado, quantos chegam vivos a casa, e o que o outro lado responde antes de se propor seja o que for.
    /// Null quando um dos campos está vazio — sem homens dos dois lados não há troca nenhuma para mostrar.
    ///
    /// O botão fica desligado quando eles recusam: o jogador vê o motivo em vez de levar com um erro.</summary>
    public static VBoxContainer? Exchange(World w, int pid, int foe, Action onPropose)
    {
        var offer = PrisonerExchange.Evaluate(w, pid, foe);
        if (offer.Men <= 0) return null;

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        head.AddChild(Ui.Grow(Ui.Lbl($"🤝 Troca de prisioneiros · {Short(offer.Men)} de cada lado", 16)));
        var verdict = Ui.Lbl(offer.Accepted ? "aceitam" : "recusam", 15);
        verdict.AddThemeColorOverride("font_color", offer.Accepted ? Ui.Good : Ui.Danger);
        head.AddChild(verdict);

        // o que sai e o que entra: os nossos voltam ao pool, os deles voltam à frente deles
        var flow = new HBoxContainer(); flow.AddThemeConstantOverride("separation", 8); v.AddChild(flow);
        flow.AddChild(Side($"{Short(offer.Home)} nossos a casa", 1f, Ours));
        flow.AddChild(Side($"{Short(offer.Home)} deles de volta", 1f, Theirs));

        int lost = offer.Men - offer.Home;
        var note = Ui.Lbl(offer.Reason + (lost > 0 ? $"  ·  {Short(lost)} de cada lado não aguentam a viagem" : ""), 14);
        note.AddThemeColorOverride("font_color", offer.Accepted ? Ui.TextDim : Ui.Danger);
        v.AddChild(note);

        var btn = Ui.Btn("Propor troca", onPropose, 190, offer.Accepted ? Ui.Kind.Primary : Ui.Kind.Normal);
        btn.Disabled = !offer.Accepted;
        btn.TooltipText = offer.Accepted
            ? "Homem por homem, com a guerra a continuar"
            : "Enquanto guardarem esta vantagem de mão-de-obra, não assinam";
        v.AddChild(btn);
        return v;
    }

    private static VBoxContainer Side(string text, float fill, Color tint)
    {
        var v = Ui.Grow(new VBoxContainer()); v.AddThemeConstantOverride("separation", 1);
        var bar = Ui.Bar(Math.Clamp(fill, 0f, 1f), tint, 0f);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        v.AddChild(bar);
        var lbl = Ui.Lbl(text, 14);
        lbl.AddThemeColorOverride("font_color", tint);
        v.AddChild(lbl);
        return v;
    }

    /// <summary>Cartão dos campos para o painel do país: quantos guardamos, de quem, e o que rendem.
    /// Null quando não há ninguém preso — o painel não mostra secções vazias.</summary>
    public static PanelContainer? Camps(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Prisoners.Count == 0) return null;
        var rows = c.Prisoners.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ToList();
        if (rows.Count == 0) return null;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.12f, 0.14f, 0.18f, 0.92f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        int men = rows.Sum(kv => kv.Value);
        float bonus = WorkBonus(w, countryId);
        var head = Ui.Lbl($"⛓ Campos de prisioneiros · {Short(men)} homens", 17);
        head.AddThemeColorOverride("font_color", Ours);
        v.AddChild(head);
        var work = Ui.Lbl(bonus > 0f ? $"trabalho forçado: indústria +{bonus:P0}" : "poucos para render trabalho", 14);
        work.AddThemeColorOverride("font_color", bonus > 0f ? Ui.Good : Ui.TextDim);
        v.AddChild(work);

        int most = rows.Max(kv => kv.Value);
        foreach (var (from, n) in rows)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
            var fl = Flags.Rect(18);
            if (w.Countries.TryGetValue(from, out var fc)) { fl.Texture = Flags.Of(fc.Tag); fl.Visible = fl.Texture is not null; }
            row.AddChild(fl);
            row.AddChild(Ui.Grow(Ui.Lbl(w.Countries.TryGetValue(from, out var nc) ? nc.Name : "país " + from, 16)));
            var bar = Ui.Bar(most <= 0 ? 0f : n / (float)most, Ours, 90f);
            row.AddChild(bar);
            var lbl = Ui.Lbl(Short(n), 15);
            lbl.AddThemeColorOverride("font_color", Ui.TextDim);
            row.AddChild(lbl);
        }
        return card;
    }
}
