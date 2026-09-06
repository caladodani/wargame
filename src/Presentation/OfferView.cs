using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara das propostas que o outro lado põe em cima da mesa (World.Offers). Sem isto a
/// iniciativa da IA morria numa notificação que passava: o jogador via "propõem uma troca" e não tinha
/// onde ir buscá-la outra vez.
///
/// A mesa leva dois assuntos — troca de prisioneiros e paz branca — e cada um mostra a conta que
/// interessa: os campos de hoje num caso, as regiões que mudam de dono no outro. Só lê o World e chama de
/// volta: quem propõe é o OfferSystem, quem responde é o AnswerOfferCommand.</summary>
public static class OfferView
{
    public static readonly Color Table = new(0.86f, 0.78f, 0.45f);

    /// <summary>Propostas deste inimigo à espera de resposta, a paz primeiro (é a que muda o mapa).</summary>
    public static List<PendingOffer> All(World w, int pid, int foe) =>
        w.Offers.Where(o => o.ToId == pid && o.FromId == foe)
                .OrderBy(o => o.Kind == "paz" ? 0 : 1).ThenBy(o => o.Day).ToList();

    /// <summary>Propostas em cima da mesa do jogador, de toda a gente (para o distintivo do HUD).</summary>
    public static int Count(World w, int pid) => w.Offers.Count(o => o.ToId == pid);

    /// <summary>Proposta deste inimigo e deste assunto (null quando a mesa está limpa).</summary>
    public static PendingOffer? Pending(World w, int pid, int foe, string kind = "prisioneiros") =>
        w.Offers.FirstOrDefault(o => o.ToId == pid && o.FromId == foe && o.Kind == kind);

    /// <summary>A primeira proposta deste inimigo, seja de que assunto for.</summary>
    public static PendingOffer? First(World w, int pid, int foe) => All(w, pid, foe).FirstOrDefault();

    /// <summary>Cartões das propostas deste inimigo, um por assunto. Null quando não há nenhuma.</summary>
    public static VBoxContainer? Card(World w, int pid, int foe, Action<PendingOffer> onAccept, Action<PendingOffer> onRefuse)
    {
        var offers = All(w, pid, foe);
        if (offers.Count == 0) return null;

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        foreach (var offer in offers) v.AddChild(One(w, pid, foe, offer, onAccept, onRefuse));
        return v;
    }

    private static VBoxContainer One(World w, int pid, int foe, PendingOffer offer,
                                     Action<PendingOffer> onAccept, Action<PendingOffer> onRefuse)
    {
        bool peace = offer.Kind == "paz";
        ExchangeOffer? deal = peace ? null : PrisonerExchange.Evaluate(w, foe, pid);
        bool live = peace ? w.AreAtWar(pid, foe) : deal!.Men > 0;
        int left = Math.Max(0, offer.ExpiresDay - w.Clock.Day);
        string them = w.Countries.TryGetValue(foe, out var fc) ? fc.Name : "o inimigo";

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        var title = Ui.Lbl(peace ? $"🕊 {them} propõe paz branca" : $"✉ {them} propõe uma troca de prisioneiros", 16);
        title.AddThemeColorOverride("font_color", Table);
        head.AddChild(Ui.Grow(title));
        var days = Ui.Lbl(left <= 1 ? "cai amanhã" : $"{left} dias", 15);
        days.AddThemeColorOverride("font_color", left <= 3 ? Ui.Danger : Ui.TextDim);
        head.AddChild(days);

        // o número que interessa é o de hoje: entre a proposta e a resposta houve batalhas
        var terms = Ui.Lbl(peace ? PeaceLine(w, pid, foe)
            : live ? $"{PrisonerView.Short(deal!.Men)} de cada lado  ·  {PrisonerView.Short(deal.Home)} dos nossos chegam a casa"
                   : "os campos mudaram desde que propuseram: já não há homens dos dois lados", 15);
        terms.AddThemeColorOverride("font_color", live ? Ui.Text : Ui.Danger);
        v.AddChild(terms);
        if (!peace && live && deal!.Men != offer.Men)
        {
            var moved = Ui.Lbl($"quando propuseram eram {PrisonerView.Short(offer.Men)}", 14);
            moved.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(moved);
        }

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
        var yes = Ui.Btn(peace ? "Assinar a paz" : "Aceitar troca", () => onAccept(offer), 190, Ui.Kind.Primary);
        yes.Disabled = !live;
        row.AddChild(yes);
        row.AddChild(Ui.Btn("Recusar", () => onRefuse(offer), 150, Ui.Kind.Danger));
        return v;
    }

    /// <summary>O que a paz branca faz ao mapa: uti possidetis, cada um fica com o que ocupa hoje.</summary>
    public static string PeaceLine(World w, int pid, int foe)
    {
        int ours = w.Regions.Values.Count(r => r.OwnerId == foe && r.ControllerId == pid);
        int theirs = w.Regions.Values.Count(r => r.OwnerId == pid && r.ControllerId == foe);
        if (ours == 0 && theirs == 0) return "a guerra acaba onde está: ninguém ocupa terreno do outro";
        return $"cada um fica com o que ocupa: ganhamos {ours} região{(ours == 1 ? "" : "ões")}, perdemos {theirs}";
    }

    /// <summary>Frase curta para a notificação e para o diálogo, com os números do dia da proposta.</summary>
    public static string Line(World w, PendingOffer offer)
    {
        string them = w.Countries.TryGetValue(offer.FromId, out var fc) ? fc.Name : "O inimigo";
        return offer.Kind == "paz"
            ? $"{them} propõe paz branca: {PeaceLine(w, offer.ToId, offer.FromId)}. Assinas?"
            : $"{them} propõe trocar {PrisonerView.Short(offer.Men)} prisioneiros de cada lado. Aceitas?";
    }
}
