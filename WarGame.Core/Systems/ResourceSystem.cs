using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Recursos estratégicos (tabelas resource + region_resource). Todos os dias soma os depósitos
/// das regiões que cada país controla e recalcula Country.ResourceMult: por tipo, StatKey ×
/// (1 + PerUnit × min(unidades, Cap)). Entra em Country.Stat ao lado das tecnologias — capturar uma
/// região com depósitos mexe nas stats no dia seguinte. Nenhum tipo é hardcoded: tudo vem da BD.</summary>
public sealed class ResourceSystem : ISystem
{
    public string Name => "Resources";

    public void Tick(World w)
    {
        if (w.ResourceDefs.Count == 0) return;
        var totals = new Dictionary<(int country, string res), float>();
        foreach (var r in w.Regions.Values)
            foreach (var (res, amount) in r.Resources)
                totals[(r.ControllerId, res)] = totals.GetValueOrDefault((r.ControllerId, res)) + amount;
        foreach (var d in w.TradeDeals)   // comércio: unidades passam do vendedor para o comprador
        {
            totals[(d.SellerId, d.ResourceId)] = totals.GetValueOrDefault((d.SellerId, d.ResourceId)) - d.Units;
            totals[(d.BuyerId, d.ResourceId)] = totals.GetValueOrDefault((d.BuyerId, d.ResourceId)) + d.Units;
        }

        foreach (var c in w.Countries.Values)
        {
            c.ResourceMult.Clear();
            if (c.Capitulated) continue;
            foreach (var def in w.ResourceDefs.Values)
            {
                float units = MathF.Min(MathF.Max(totals.GetValueOrDefault((c.Id, def.Id)), 0f), def.Cap);
                if (units <= 0f) continue;
                c.ResourceMult[def.StatKey] = c.ResourceMult.GetValueOrDefault(def.StatKey, 1f) * (1f + def.PerUnit * units);
            }
        }
    }

    /// <summary>Unidades controladas de um recurso (UI).</summary>
    public static float Controlled(World w, int countryId, string resourceId)
    {
        float sum = 0f;
        foreach (var r in w.Regions.Values)
            if (r.ControllerId == countryId && r.Resources.TryGetValue(resourceId, out var a)) sum += a;
        return sum;
    }
}
