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
        float fortDays = w.Rule("fort_build_days", 20f); int fortMax = (int)w.Rule("fort_max", 5f);
        foreach (var r in w.Regions.Values)
        {
            if (r.Building)
            {
                if (r.ControllerId != r.OwnerId) { r.Building = false; r.BuildProgress = 0f; }
                else if ((r.BuildProgress += 1f) >= days)
                {
                    r.Building = false; r.BuildProgress = 0f;
                    r.Infrastructure = MathF.Min(max, r.Infrastructure + step);
                    w.Events.Publish(new InfrastructureBuilt(r.Id));
                }
            }
            if (r.Project is string proj)
            {
                if (r.ControllerId != r.OwnerId || !w.BuildingDefs.TryGetValue(proj, out var def)) { r.Project = null; r.ProjectProgress = 0f; }
                else if ((r.ProjectProgress += 1f) >= def.Days)
                {
                    r.Project = null; r.ProjectProgress = 0f;
                    int lvl = Math.Min(def.MaxLevel, r.Buildings.GetValueOrDefault(proj) + 1);
                    r.Buildings[proj] = lvl;
                    w.Events.Publish(new BuildingBuilt(r.Id, proj, lvl));
                }
            }
            if (r.FortBuilding)
            {
                if (r.ControllerId != r.OwnerId) { r.FortBuilding = false; r.FortProgress = 0f; }
                else if ((r.FortProgress += 1f) >= fortDays)
                {
                    r.FortBuilding = false; r.FortProgress = 0f;
                    r.Fort = Math.Min(fortMax, r.Fort + 1);
                    w.Events.Publish(new FortBuilt(r.Id, r.Fort));
                }
            }
        }

        // efeito dos edifícios: multiplicador por país recalculado todos os dias (como os recursos)
        foreach (var c in w.Countries.Values) c.BuildingMult.Clear();
        foreach (var r in w.Regions.Values)
        {
            if (r.Buildings.Count == 0 || !w.Countries.TryGetValue(r.ControllerId, out var c)) continue;
            foreach (var (bid, lvl) in r.Buildings)
                if (w.BuildingDefs.TryGetValue(bid, out var def))
                    c.BuildingMult[def.StatKey] = c.BuildingMult.GetValueOrDefault(def.StatKey, 1f) * (1f + def.PerLevel * lvl);
        }
    }
}
