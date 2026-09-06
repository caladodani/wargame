using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A iniciativa do outro lado. Até aqui a troca de prisioneiros era um botão do jogador: a IA
/// nunca batia à porta. Um país com os campos cheios de gente sua do outro lado do arame e o pool de
/// recrutas a secar não fica à espera que lhe façam a proposta — manda o emissário.
///
/// De offer_period_days em offer_period_days cada país da IA em guerra olha para os campos e, se a troca
/// lhe convier (é a conta do PrisonerExchange vista do lado dele), propõe. Contra outro país da IA a
/// resposta sai no mesmo dia; contra o jogador fica uma proposta em cima da mesa, válida offer_days, e
/// quem responde é o AnswerOfferCommand.
///
/// A mesa leva dois assuntos. O outro é a paz branca: a IA já a oferecia entre si (AiSystem.Peace), mas
/// ao jogador nunca — fazia-se-lhe a paz sem ele dizer nada, por isso era proibido. Com a mesa deixa de
/// ser: numa guerra parada há peace_stale_days em que a IA está mais fraca ou já ocupa terreno nosso, ela
/// propõe, e quem decide é o jogador.
///
/// Uma proposta por par e assunto: a IA não enche o ecrã com a mesma conversa.</summary>
public sealed class OfferSystem : ISystem
{
    public string Name => "Offers";

    public void Tick(World w)
    {
        Expire(w);
        int every = Math.Max(1, (int)w.Rule("offer_period_days", 10f));
        if (w.Clock.Day % every != 0) return;

        foreach (var c in w.Countries.Values.Where(x => !x.IsPlayer && !x.Capitulated).OrderBy(x => x.Id))
            foreach (int foeId in c.AtWarWith.OrderBy(x => x))
            {
                if (!w.Countries.TryGetValue(foeId, out var foe) || foe.Capitulated) continue;
                if (Wants(w, c.Id, foeId) && !Open(w, c.Id, foeId, "prisioneiros"))
                {
                    int men = PrisonerExchange.Evaluate(w, c.Id, foeId).Men;
                    if (foe.IsPlayer) Put(w, c.Id, foeId, "prisioneiros", men);
                    else Settle(w, c.Id, foeId);
                }
                // a paz branca só se propõe ao jogador: entre países da IA já é o AiSystem que a fecha
                if (foe.IsPlayer && WantsPeace(w, c.Id, foeId) && !Open(w, c.Id, foeId, "paz"))
                    Put(w, c.Id, foeId, "paz", 0);
            }
    }

    /// <summary>A troca interessa a quem propõe? É a mesma conta que ele faria se lhe batessem à porta:
    /// se aceitaria a proposta ao contrário, então é dele o interesse em fazê-la.</summary>
    public static bool Wants(World w, int fromId, int toId)
    {
        var mirror = PrisonerExchange.Evaluate(w, toId, fromId);
        return mirror.Men > 0 && mirror.Accepted;
    }

    /// <summary>A paz branca interessa a quem a propõe? Guerra parada há peace_stale_days e, ou está mais
    /// fraco no terreno (corta perdas), ou já ocupa terreno nosso (uti possidetis: a paz consolida-lho).
    /// É a mesma conta que a IA faz entre si — só que agora com o jogador do outro lado da mesa.</summary>
    public static bool WantsPeace(World w, int fromId, int toId)
    {
        var info = w.Wars.GetValueOrDefault(World.WarKey(fromId, toId));
        if (info is null) return false;
        if (w.Clock.Day - Math.Max(info.StartDay, info.LastProgressDay) < w.Rule("peace_stale_days", 60f)) return false;

        int mine = 0, theirs = 0;
        foreach (var d in w.Divisions.Values)
        {
            if (d.CountryId == fromId) mine++;
            else if (d.CountryId == toId) theirs++;
        }
        bool holdsTheirLand = w.Regions.Values.Any(r => r.OwnerId == toId && r.ControllerId == fromId);
        return mine < theirs || holdsTheirLand;
    }

    /// <summary>Já há uma proposta deste assunto em cima da mesa deste par?</summary>
    private static bool Open(World w, int fromId, int toId, string kind) =>
        w.Offers.Any(o => o.FromId == fromId && o.ToId == toId && o.Kind == kind);

    /// <summary>Põe a proposta na mesa do jogador e anuncia-a.</summary>
    private static void Put(World w, int fromId, int toId, string kind, int men)
    {
        w.Offers.Add(new PendingOffer
        {
            FromId = fromId, ToId = toId, Kind = kind, Men = men,
            Day = w.Clock.Day, ExpiresDay = w.Clock.Day + Math.Max(1, (int)w.Rule("offer_days", 20f)),
        });
        w.Events.Publish(new OfferMade(fromId, toId, kind, men));
    }

    /// <summary>Proposta entre dois países da IA: resolve-se no dia, pelas contas de quem a recebe.</summary>
    private static void Settle(World w, int fromId, int toId)
    {
        var offer = PrisonerExchange.Evaluate(w, fromId, toId);
        if (offer.Men <= 0 || !offer.Accepted) return;
        Exchange(w, fromId, toId, offer);
        w.Events.Publish(new OfferAnswered(fromId, toId, "prisioneiros", true));
    }

    /// <summary>Abre os dois campos pelo tamanho combinado. Caminho comum da proposta aceite (aqui e no
    /// AnswerOfferCommand) — a viagem para casa é sempre a do PrisonerSystem.</summary>
    public static void Exchange(World w, int a, int b, ExchangeOffer offer)
    {
        float back = w.Rule("exchange_return", 0.85f);
        PrisonerSystem.Release(w, w.Countries[a], b, offer.Men, back);
        PrisonerSystem.Release(w, w.Countries[b], a, offer.Men, back);
        w.Events.Publish(new PrisonersExchanged(a, b, offer.Men, offer.Home));
    }

    /// <summary>Propostas fora de prazo, sem campos para trocar ou com a guerra acabada caem da mesa.</summary>
    private static void Expire(World w)
    {
        foreach (var o in w.Offers.ToList())
        {
            bool dead = w.Clock.Day >= o.ExpiresDay
                     || !w.AreAtWar(o.FromId, o.ToId)
                     || (o.Kind == "prisioneiros" && PrisonerExchange.Evaluate(w, o.FromId, o.ToId).Men <= 0);
            if (!dead) continue;
            w.Offers.Remove(o);
            w.Events.Publish(new OfferExpired(o.FromId, o.ToId, o.Kind));
        }
    }
}
