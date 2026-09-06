using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara das propostas que o outro lado põe em cima da mesa (World.Offers). Sem isto a
/// iniciativa da IA morria numa notificação que passava: o jogador via "propõem uma troca" e não tinha
/// onde ir buscá-la outra vez.
///
/// Só lê o World e chama de volta: quem propõe é o OfferSystem, quem responde é o AnswerOfferCommand.</summary>
public static class OfferView
{
    public static readonly Color Table = new(0.86f, 0.78f, 0.45f);

    /// <summary>Proposta deste inimigo à espera de resposta (null quando a mesa está limpa).</summary>
    public static PendingOffer? Pending(World w, int pid, int foe) =>
        w.Offers.FirstOrDefault(o => o.ToId == pid && o.FromId == foe);

    /// <summary>Cartão da proposta: quem a fez, o que vale hoje (não no dia em que chegou), quantos dias
    /// falta para cair da mesa, e os dois botões. Null quando não há proposta deste inimigo.</summary>
    public static VBoxContainer? Card(World w, int pid, int foe, Action onAccept, Action onRefuse)
    {
        var offer = Pending(w, pid, foe);
        if (offer is null) return null;
        var deal = PrisonerExchange.Evaluate(w, foe, pid);
        int left = Math.Max(0, offer.ExpiresDay - w.Clock.Day);
        string them = w.Countries.TryGetValue(foe, out var fc) ? fc.Name : "o inimigo";

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        var title = Ui.Lbl($"✉ {them} propõe uma troca de prisioneiros", 16);
        title.AddThemeColorOverride("font_color", Table);
        head.AddChild(Ui.Grow(title));
        var days = Ui.Lbl(left <= 1 ? "cai amanhã" : $"{left} dias", 15);
        days.AddThemeColorOverride("font_color", left <= 3 ? Ui.Danger : Ui.TextDim);
        head.AddChild(days);

        // o número que interessa é o de hoje: entre a proposta e a resposta houve batalhas
        bool live = deal.Men > 0;
        var terms = Ui.Lbl(live
            ? $"{PrisonerView.Short(deal.Men)} de cada lado  ·  {PrisonerView.Short(deal.Home)} dos nossos chegam a casa"
            : "os campos mudaram desde que propuseram: já não há homens dos dois lados", 15);
        terms.AddThemeColorOverride("font_color", live ? Ui.Text : Ui.Danger);
        v.AddChild(terms);
        if (live && deal.Men != offer.Men)
        {
            var moved = Ui.Lbl($"quando propuseram eram {PrisonerView.Short(offer.Men)}", 14);
            moved.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(moved);
        }

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
        var yes = Ui.Btn("Aceitar troca", onAccept, 190, Ui.Kind.Primary);
        yes.Disabled = !live;
        row.AddChild(yes);
        row.AddChild(Ui.Btn("Recusar", onRefuse, 150, Ui.Kind.Danger));
        return v;
    }

    /// <summary>Frase curta para a notificação e para o diálogo, com os números do dia da proposta.</summary>
    public static string Line(World w, PendingOffer offer)
    {
        string them = w.Countries.TryGetValue(offer.FromId, out var fc) ? fc.Name : "O inimigo";
        return $"{them} propõe trocar {PrisonerView.Short(offer.Men)} prisioneiros de cada lado. Aceitas?";
    }
}
