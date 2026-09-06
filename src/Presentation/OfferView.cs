using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara das propostas que o outro lado põe em cima da mesa (World.Offers). Sem isto a
/// iniciativa da IA morria numa notificação que passava: o jogador via "propõem uma troca" e não tinha
/// onde ir buscá-la outra vez.
///
/// A mesa leva três assuntos — troca de prisioneiros, paz branca e cedência de território — e cada um
/// mostra a conta que interessa: os campos de hoje no primeiro, as regiões que mudam de dono no segundo, e
/// no terceiro a ficha da terra que nos entregam, com um botão para a ir ver no mapa antes de assinar. Só
/// lê o World e chama de volta: quem propõe é o OfferSystem, quem responde é o AnswerOfferCommand.</summary>
public static class OfferView
{
    public static readonly Color Table = new(0.86f, 0.78f, 0.45f);

    /// <summary>Propostas deste inimigo à espera de resposta, a paz primeiro (é a que muda o mapa).</summary>
    public static List<PendingOffer> All(World w, int pid, int foe) =>
        w.Offers.Where(o => o.ToId == pid && o.FromId == foe)
                .OrderBy(o => o.Kind == "regiao" ? 0 : o.Kind == "paz" ? 1 : 2).ThenBy(o => o.Day).ToList();

    /// <summary>Propostas em cima da mesa do jogador, de toda a gente (para o distintivo do HUD).</summary>
    public static int Count(World w, int pid) => w.Offers.Count(o => o.ToId == pid);

    /// <summary>Proposta deste inimigo e deste assunto (null quando a mesa está limpa).</summary>
    public static PendingOffer? Pending(World w, int pid, int foe, string kind = "prisioneiros") =>
        w.Offers.FirstOrDefault(o => o.ToId == pid && o.FromId == foe && o.Kind == kind);

    /// <summary>A primeira proposta deste inimigo, seja de que assunto for.</summary>
    public static PendingOffer? First(World w, int pid, int foe) => All(w, pid, foe).FirstOrDefault();

    /// <summary>Cartões das propostas deste inimigo, um por assunto. Null quando não há nenhuma.</summary>
    public static VBoxContainer? Card(World w, int pid, int foe, Action<PendingOffer> onAccept,
                                      Action<PendingOffer> onRefuse, Action<int>? onShow = null)
    {
        var offers = All(w, pid, foe);
        if (offers.Count == 0) return null;

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        foreach (var offer in offers) v.AddChild(One(w, pid, foe, offer, onAccept, onRefuse, onShow));
        return v;
    }

    private static VBoxContainer One(World w, int pid, int foe, PendingOffer offer,
                                     Action<PendingOffer> onAccept, Action<PendingOffer> onRefuse,
                                     Action<int>? onShow)
    {
        bool cede = offer.Kind == "regiao";
        bool peace = offer.Kind == "paz" || cede;
        ExchangeOffer? deal = peace ? null : PrisonerExchange.Evaluate(w, foe, pid);
        w.Regions.TryGetValue(offer.RegionId, out var land);
        bool live = cede ? w.AreAtWar(pid, foe) && land is not null && land.OwnerId == foe
                  : peace ? w.AreAtWar(pid, foe) : deal!.Men > 0;
        int left = Math.Max(0, offer.ExpiresDay - w.Clock.Day);
        string them = w.Countries.TryGetValue(foe, out var fc) ? fc.Name : "o inimigo";

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        var title = Ui.Lbl(cede ? $"🏳 {them} paga a paz com {land?.Name ?? "uma região"}"
                          : peace ? $"🕊 {them} propõe paz branca"
                                  : $"✉ {them} propõe uma troca de prisioneiros", 16);
        title.AddThemeColorOverride("font_color", Table);
        head.AddChild(Ui.Grow(title));
        var days = Ui.Lbl(left <= 1 ? "cai amanhã" : $"{left} dias", 15);
        days.AddThemeColorOverride("font_color", left <= 3 ? Ui.Danger : Ui.TextDim);
        head.AddChild(days);

        // o número que interessa é o de hoje: entre a proposta e a resposta houve batalhas
        var terms = Ui.Lbl(cede ? (live ? $"{land!.Name} passa a ser nossa, e o resto fica onde está: {PeaceLine(w, pid, foe)}"
                                        : "a região prometida já não é deles: a proposta caiu")
            : peace ? PeaceLine(w, pid, foe)
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

        if (cede && land is not null) v.AddChild(Sheet(w, land));

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
        var yes = Ui.Btn(cede ? "Aceitar a terra e a paz" : peace ? "Assinar a paz" : "Aceitar troca",
                         () => onAccept(offer), 220, Ui.Kind.Primary);
        yes.Disabled = !live;
        row.AddChild(yes);
        row.AddChild(Ui.Btn("Recusar", () => onRefuse(offer), 150, Ui.Kind.Danger));
        // ninguém assina uma cedência sem ir ver o que lhe dão: o botão leva o mapa até lá
        if (cede && land is not null && onShow is not null)
            row.AddChild(Ui.Btn("Ver no mapa", () => onShow(land.Id), 150));
        return v;
    }

    /// <summary>Ficha da terra que nos entregam: gente, terreno, estradas e muralhas. É o que separa uma
    /// cedência boa de um deserto com um nome — e vê-se antes de assinar, não depois.</summary>
    private static HBoxContainer Sheet(World w, Region r)
    {
        var box = new HBoxContainer(); box.AddThemeConstantOverride("separation", 14);
        void Cell(string head, string val, Color tint)
        {
            var c = new VBoxContainer(); c.AddThemeConstantOverride("separation", 0);
            var h = Ui.Lbl(head, 13); h.AddThemeColorOverride("font_color", Ui.TextDim); c.AddChild(h);
            var b = Ui.Lbl(val, 16); b.AddThemeColorOverride("font_color", tint); c.AddChild(b);
            box.AddChild(c);
        }
        Cell("gente", PrisonerView.Short(r.Population), Ui.Text);
        Cell("terreno", r.Terrain, Ui.Text);
        Cell("estradas", $"{r.Infrastructure:0.0}", r.Infrastructure >= 1f ? Ui.Good : Ui.TextDim);
        Cell("fortificação", r.Fort > 0 ? $"nível {r.Fort}" : "aberta", r.Fort > 0 ? Ui.Good : Ui.TextDim);
        if (r.Coastal) Cell("costa", "porto possível", Ui.Accent);
        int guard = r.DivisionIds.Count(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == r.ControllerId);
        if (guard > 0) Cell("guarnição", $"{guard} divisões", Ui.Danger);
        return box;
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
        if (offer.Kind == "regiao")
        {
            string land = w.Regions.TryGetValue(offer.RegionId, out var r) ? r.Name : "uma região";
            return $"{them} cede {land} para acabar a guerra. Aceitas a terra e a paz?";
        }
        return offer.Kind == "paz"
            ? $"{them} propõe paz branca: {PeaceLine(w, offer.ToId, offer.FromId)}. Assinas?"
            : $"{them} propõe trocar {PrisonerView.Short(offer.Men)} prisioneiros de cada lado. Aceitas?";
    }
}
