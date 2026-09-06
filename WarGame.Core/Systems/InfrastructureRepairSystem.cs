using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Reparação natural da infraestrutura danificada pela guerra: uma região calma repõe
/// infra_repair_per_day por dia até à Region.BaseInfrastructure (o valor de origem), nunca acima —
/// passar disso continua a exigir obra paga. Não repara enquanto houver batalha na região nem se for
/// ocupação com resistência acima de infra_repair_max_resist. Publica InfrastructureRepaired ao chegar
/// à base.</summary>
public sealed class InfrastructureRepairSystem : ISystem
{
    public string Name => "InfrastructureRepair";

    public void Tick(World w)
    {
        float perDay = w.Rule("infra_repair_per_day", 0.002f);
        if (perDay <= 0f) return;
        float maxResist = w.Rule("infra_repair_max_resist", 0.3f);
        var contested = new HashSet<int>(w.ActiveBattles.Select(b => b.RegionId));
        foreach (var r in w.Regions.Values)
        {
            if (r.Infrastructure >= r.BaseInfrastructure - 1e-4f) continue;
            if (contested.Contains(r.Id)) continue;
            if (r.ControllerId != r.OwnerId && r.Resistance > maxResist) continue;
            r.Infrastructure = MathF.Min(r.BaseInfrastructure, r.Infrastructure + perDay);
            if (r.Infrastructure >= r.BaseInfrastructure - 1e-4f)
                w.Events.Publish(new InfrastructureRepaired(r.Id));
        }
    }
}
