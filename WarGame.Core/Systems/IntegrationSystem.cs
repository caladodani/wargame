using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Integração de território ocupado: com o dono vivo e a resistência abaixo de
/// integration_max_resist, a ocupação acumula integration_speed dias por dia; aos integration_days a
/// região passa a ser do controlador (OwnerId muda, RegionIntegrated). Resistência alta faz o progresso
/// recuar integration_decay por dia. Região devolvida ou revoltada perde o progresso.</summary>
public sealed class IntegrationSystem : ISystem
{
    public string Name => "Integration";

    public void Tick(World w)
    {
        float need = w.Rule("integration_days", 150f);
        float maxResist = w.Rule("integration_max_resist", 0.1f);
        float decay = w.Rule("integration_decay", 2f);
        foreach (var r in w.Regions.Values)
        {
            bool occupied = r.ControllerId != r.OwnerId
                && w.Countries.TryGetValue(r.OwnerId, out var owner) && !owner.Capitulated
                && w.Countries.TryGetValue(r.ControllerId, out _);
            if (!occupied) { r.Integration = 0f; continue; }
            var ctrl = w.Countries[r.ControllerId];
            if (r.Resistance <= maxResist) r.Integration += ctrl.Stat("integration_speed");
            else r.Integration = MathF.Max(0f, r.Integration - decay);
            if (r.Integration >= need)
            {
                int old = r.OwnerId;
                r.OwnerId = r.ControllerId; r.Integration = 0f; r.Resistance = 0f;
                w.Events.Publish(new RegionIntegrated(r.Id, old, r.ControllerId));
            }
        }
    }
}
