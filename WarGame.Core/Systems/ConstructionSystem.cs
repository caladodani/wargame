using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Obras de infraestrutura (BuildInfrastructureCommand paga infra_build_cost e liga Region.Building).
/// 1 dia de progresso por dia; aos infra_build_days a infraestrutura sobe infra_step (tecto infra_max) e sai
/// InfrastructureBuilt. Região ocupada (controlador ≠ dono) perde a obra — o dinheiro já foi gasto.</summary>
public sealed class ConstructionSystem : ISystem
{
    public string Name => "Construction";

    public void Tick(World w)
    {
        float days = w.Rule("infra_build_days", 30f), step = w.Rule("infra_step", 0.25f), max = w.Rule("infra_max", 2f);
        foreach (var r in w.Regions.Values)
        {
            if (!r.Building) continue;
            if (r.ControllerId != r.OwnerId) { r.Building = false; r.BuildProgress = 0f; continue; }
            r.BuildProgress += 1f;
            if (r.BuildProgress < days) continue;
            r.Building = false; r.BuildProgress = 0f;
            r.Infrastructure = MathF.Min(max, r.Infrastructure + step);
            w.Events.Publish(new InfrastructureBuilt(r.Id));
        }
    }
}
