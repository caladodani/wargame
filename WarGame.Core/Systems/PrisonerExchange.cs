using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>O que uma troca de prisioneiros dá e o que custa, e se o outro lado a assina. Até aqui os
/// campos só se abriam com a paz: enquanto a guerra durasse, os homens que se rendiam ficavam do outro
/// lado do arame e o pool de recrutas nunca mais os via. Faltava a coisa mais comum de uma guerra longa —
/// mandar um emissário e trocar homem por homem, mesmo com os canhões a disparar.
///
/// A troca é sempre pelo mesmo número dos dois lados (o mínimo dos dois campos): ninguém entrega mais do
/// que recebe. Nem todos chegam a casa — exchange_return diz quantos aguentam a viagem.
///
/// Quem decide é o alvo da proposta, e decide pelo que perde: se guarda muito mais gente do que nós,
/// está a trocar mão-de-obra por nada e recusa (exchange_ai_edge). Se está a raspar o fundo do pool de
/// homens (exchange_need_men), aceita mesmo em desvantagem — homens à frente valem mais do que homens
/// atrás do arame.
///
/// Só faz contas: quem mexe nos campos é o ExchangePrisonersCommand.</summary>
public static class PrisonerExchange
{
    /// <summary>Homens que um país guarda de outro.</summary>
    public static int Held(World w, int holderId, int fromId) =>
        w.Countries.TryGetValue(holderId, out var c) ? c.Prisoners.GetValueOrDefault(fromId) : 0;

    /// <summary>A proposta que `proposerId` pode pôr em cima da mesa a `targetId`: quantos homens mudam
    /// de mãos de cada lado, quantos chegam a casa vivos, e o que o outro lado responde.</summary>
    public static ExchangeOffer Evaluate(World w, int proposerId, int targetId)
    {
        int theirsWeHold = Held(w, proposerId, targetId);   // homens deles nos nossos campos
        int oursTheyHold = Held(w, targetId, proposerId);   // homens nossos nos campos deles
        int men = Math.Min(theirsWeHold, oursTheyHold);
        if (men <= 0)
            return new ExchangeOffer(0, 0, false, theirsWeHold + oursTheyHold == 0
                ? "não há prisioneiros de lado nenhum"
                : "a troca é homem por homem: um dos campos está vazio");

        int home = (int)(men * w.Rule("exchange_return", 0.85f));
        if (!w.Countries.TryGetValue(targetId, out var t))
            return new ExchangeOffer(men, home, false, "não há com quem negociar");

        // o pool a raspar o fundo compra qualquer troca: um homem na frente vale mais do que um a trabalhar
        if (t.Manpower >= 0f && t.Manpower < w.Rule("exchange_need_men", 150000f))
            return new ExchangeOffer(men, home, true, "faltam-lhes homens: aceitam de olhos fechados");

        float edge = w.Rule("exchange_ai_edge", 1.4f);
        if (oursTheyHold > theirsWeHold * edge)
            return new ExchangeOffer(men, home, false, "guardam muito mais gente do que nós: não trocam a vantagem");

        return new ExchangeOffer(men, home, true, "os campos estão equilibrados: aceitam a troca");
    }
}

/// <summary>Uma proposta de troca já avaliada: <paramref name="Men"/> homens de cada lado,
/// <paramref name="Home"/> chegam vivos a casa, e <paramref name="Accepted"/> diz se assinam.</summary>
public sealed record ExchangeOffer(int Men, int Home, bool Accepted, string Reason);
