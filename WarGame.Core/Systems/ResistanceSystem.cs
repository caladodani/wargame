using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Resistência nas regiões ocupadas (HoI4 simplificado, 0..1 por região). Enquanto o dono original
/// está vivo cresce resistance_growth por dia; uma divisão do ocupante na região suprime-a (desce
/// resistance_suppress, ocupação "garrisonada" fica em paz). O rendimento da região ocupada cai com a
/// resistência (EconomySystem × (1 − Resistance × resistance_output_hit)). Ao chegar a 1.0 sem guarnição
/// a região revolta-se: o controlo volta ao dono e sai RegionRevolted. Dono capitulado ou região devolvida →
/// a resistência esvai-se ao mesmo ritmo da supressão.</summary>
public sealed class ResistanceSystem : ISystem
{
    public string Name => "Resistance";

    public void Tick(World w)
    {
        float growth = w.Rule("resistance_growth", 0.02f), suppress = w.Rule("resistance_suppress", 0.04f);
        foreach (var r in w.Regions.Values)
        {
            bool occupied = r.ControllerId != r.OwnerId
                            && w.Countries.TryGetValue(r.OwnerId, out var owner) && !owner.Capitulated;
            bool garrisoned = occupied && r.DivisionIds.Any(id =>
                w.Divisions.TryGetValue(id, out var d) && d.CountryId == r.ControllerId);
            if (!occupied || garrisoned)
            {
                if (r.Resistance > 0f) r.Resistance = MathF.Max(0f, r.Resistance - suppress);
                continue;
            }
            r.Resistance = MathF.Min(1f, r.Resistance + growth);
            if (r.Resistance >= 1f)
            {
                int old = r.ControllerId;
                r.ControllerId = r.OwnerId;
                r.Resistance = 0f;
                w.Events.Publish(new RegionRevolted(r.Id, old));
            }
        }
    }
}
