using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Comércio de recursos (World.TradeDeals): todos os dias o comprador paga
/// Units × trade_price_per_unit ao vendedor. O acordo cai (TradeDealEnded) se os dois entram em guerra,
/// se o vendedor deixa de controlar depósitos suficientes (contando todos os acordos dele) ou se o
/// comprador não consegue pagar. As unidades passam para o comprador no ResourceSystem (Effective).</summary>
public sealed class TradeSystem : ISystem
{
    public string Name => "Trade";

    public void Tick(World w)
    {
        if (w.TradeDeals.Count == 0) return;
        float price = w.Rule("trade_price_per_unit", 2f);
        for (int i = w.TradeDeals.Count - 1; i >= 0; i--)
        {
            var d = w.TradeDeals[i];
            bool dead = w.AreAtWar(d.BuyerId, d.SellerId)
                        || !w.Countries.TryGetValue(d.BuyerId, out var buyer) || buyer.Capitulated
                        || !w.Countries.TryGetValue(d.SellerId, out var seller) || seller.Capitulated
                        || ResourceSystem.Controlled(w, d.SellerId, d.ResourceId) < Sold(w, d.SellerId, d.ResourceId) - 1e-3f
                        || buyer.Money < d.Units * price;
            if (dead)
            {
                w.TradeDeals.RemoveAt(i);
                w.Events.Publish(new TradeDealEnded(d.BuyerId, d.SellerId, d.ResourceId));
                continue;
            }
            float cost = d.Units * price;
            w.Countries[d.BuyerId].Money -= cost;
            w.Countries[d.SellerId].Money += cost;
        }
    }

    /// <summary>Unidades de um recurso já vendidas por um país (todos os acordos em que é vendedor).</summary>
    public static float Sold(World w, int sellerId, string resourceId) =>
        w.TradeDeals.Where(d => d.SellerId == sellerId && d.ResourceId == resourceId).Sum(d => d.Units);

    /// <summary>Unidades efectivas de um país: controladas − vendidas + compradas.</summary>
    public static float Effective(World w, int countryId, string resourceId)
    {
        float units = ResourceSystem.Controlled(w, countryId, resourceId) - Sold(w, countryId, resourceId);
        foreach (var d in w.TradeDeals)
            if (d.BuyerId == countryId && d.ResourceId == resourceId) units += d.Units;
        return MathF.Max(0f, units);
    }
}
