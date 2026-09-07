using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Comércio de recursos (World.TradeDeals): todos os dias o comprador paga ao vendedor as unidades
/// alugadas ao preço travado no contrato. O acordo cai (TradeDealEnded) se os dois entram em guerra, se o
/// vendedor deixa de controlar depósitos suficientes (contando todos os acordos dele), se o comprador não
/// consegue pagar, ou no dia em que o prazo acaba. As unidades passam para o comprador no ResourceSystem.
///
/// O preço já não é um número fixo do mundo inteiro: é de mercado (ver Price). Quanto mais do que um
/// vendedor tem já está prometido a terceiros, mais caro fica o que lhe resta, e um vendedor em guerra
/// cobra prémio — vender aço a meio de uma campanha é vender o que faz falta em casa. Assinar um tratado
/// trava esse preço até ao fim do prazo, que é o que dá sentido a um contrato longo: compra-se barato hoje
/// para o dia em que o mercado apertar.</summary>
public sealed class TradeSystem : ISystem
{
    public string Name => "Trade";

    public void Tick(World w)
    {
        if (w.TradeDeals.Count == 0) return;
        for (int i = w.TradeDeals.Count - 1; i >= 0; i--)
        {
            var d = w.TradeDeals[i];
            // sem mercantes que o carreguem, o contrato fica de pé mas hoje não entrega nem se paga
            bool grounded = ConvoySystem.Grounded(w, d);
            float cost = grounded ? 0f : d.Units * Paid(w, d);
            bool dead = w.AreAtWar(d.BuyerId, d.SellerId)
                        || (d.UntilDay > 0 && w.Clock.Day >= d.UntilDay)     // prazo cumprido: o contrato acaba
                        || !w.Countries.TryGetValue(d.BuyerId, out var buyer) || buyer.Capitulated
                        || !w.Countries.TryGetValue(d.SellerId, out var seller) || seller.Capitulated
                        || ResourceSystem.Controlled(w, d.SellerId, d.ResourceId) < Sold(w, d.SellerId, d.ResourceId) - 1e-3f
                        || buyer.Money < cost;
            if (dead)
            {
                w.TradeDeals.RemoveAt(i);
                w.Events.Publish(new TradeDealEnded(d.BuyerId, d.SellerId, d.ResourceId));
                continue;
            }
            if (grounded) continue;
            w.Countries[d.BuyerId].Money -= cost;
            w.Countries[d.SellerId].Money += cost;
        }
    }

    /// <summary>Preço por unidade que este acordo cobra hoje: o travado à assinatura, ou o do mercado quando
    /// o contrato vem de um save anterior aos tratados (PricePerUnit 0).</summary>
    public static float Paid(World w, TradeDeal d) =>
        d.PricePerUnit > 0f ? d.PricePerUnit : Price(w, d.SellerId, d.ResourceId);

    /// <summary>Preço de mercado de uma unidade deste recurso a este vendedor: o de tabela
    /// (trade_price_per_unit) mais o que a escassez dele pedir (a fatia dos depósitos que já está prometida
    /// a terceiros, pesada por trade_price_scarcity) e o prémio de quem está em guerra
    /// (trade_war_premium). Cortado por trade_price_min e trade_price_max, para nunca haver aço de graça
    /// nem aço a peso de ouro.</summary>
    public static float Price(World w, int sellerId, string resourceId)
    {
        float baseP = w.Rule("trade_price_per_unit", 2f);
        float have = ResourceSystem.Controlled(w, sellerId, resourceId);
        float scarcity = have <= 0f ? 1f : MathF.Min(1f, Sold(w, sellerId, resourceId) / have);
        float price = baseP * (1f + scarcity * w.Rule("trade_price_scarcity", 1.5f));
        if (w.Countries.TryGetValue(sellerId, out var s) && s.AtWarWith.Count > 0)
            price *= w.Rule("trade_war_premium", 1.4f);
        return Math.Clamp(price, w.Rule("trade_price_min", 1f), w.Rule("trade_price_max", 12f));
    }

    /// <summary>Dias que faltam a um contrato (0 = sem prazo, para quem não quis marcar data).</summary>
    public static int DaysLeft(World w, TradeDeal d) => d.UntilDay <= 0 ? 0 : Math.Max(0, d.UntilDay - w.Clock.Day);

    /// <summary>Unidades de um recurso já vendidas por um país (todos os acordos em que é vendedor).</summary>
    public static float Sold(World w, int sellerId, string resourceId) =>
        w.TradeDeals.Where(d => d.SellerId == sellerId && d.ResourceId == resourceId).Sum(d => d.Units);

    /// <summary>Unidades efectivas de um país: controladas − vendidas + compradas (as compras que os
    /// comboios não conseguiram carregar hoje não entram — o aço está no cais do vendedor).</summary>
    public static float Effective(World w, int countryId, string resourceId)
    {
        float units = ResourceSystem.Controlled(w, countryId, resourceId) - Sold(w, countryId, resourceId);
        foreach (var d in w.TradeDeals)
            if (d.BuyerId == countryId && d.ResourceId == resourceId && !ConvoySystem.Grounded(w, d)) units += d.Units;
        return MathF.Max(0f, units);
    }
}
