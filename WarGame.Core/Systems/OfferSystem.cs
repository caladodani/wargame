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
                if (!Wants(w, c.Id, foeId)) continue;
                if (w.Offers.Any(o => o.FromId == c.Id && o.ToId == foeId && o.Kind == "prisioneiros")) continue;

                int men = PrisonerExchange.Evaluate(w, c.Id, foeId).Men;
                if (foe.IsPlayer) Put(w, c.Id, foeId, men);
                else Settle(w, c.Id, foeId);
            }
    }

    /// <summary>A troca interessa a quem propõe? É a mesma conta que ele faria se lhe batessem à porta:
    /// se aceitaria a proposta ao contrário, então é dele o interesse em fazê-la.</summary>
    public static bool Wants(World w, int fromId, int toId)
    {
        var mirror = PrisonerExchange.Evaluate(w, toId, fromId);
        return mirror.Men > 0 && mirror.Accepted;
    }

    /// <summary>Põe a proposta na mesa do jogador e anuncia-a.</summary>
    private static void Put(World w, int fromId, int toId, int men)
    {
        w.Offers.Add(new PendingOffer
        {
            FromId = fromId, ToId = toId, Kind = "prisioneiros", Men = men,
            Day = w.Clock.Day, ExpiresDay = w.Clock.Day + Math.Max(1, (int)w.Rule("offer_days", 20f)),
        });
        w.Events.Publish(new OfferMade(fromId, toId, "prisioneiros", men));
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
                     || PrisonerExchange.Evaluate(w, o.FromId, o.ToId).Men <= 0;
            if (!dead) continue;
            w.Offers.Remove(o);
            w.Events.Publish(new OfferExpired(o.FromId, o.ToId, o.Kind));
        }
    }
}
