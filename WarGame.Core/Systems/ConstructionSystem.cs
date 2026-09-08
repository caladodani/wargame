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
            // carris: a mesma obra da infraestrutura, mas o que sobe é a via férrea da região — e é ela que
            // o mapa desenha e que a rede de abastecimento conta (SupplySystem.StepCost)
            if (r.RailBuilding)
            {
                if (r.ControllerId != r.OwnerId) { r.RailBuilding = false; r.RailProgress = 0f; }
                else if ((r.RailProgress += 1f) >= w.Rule("rail_days", 20f))
                {
                    r.RailBuilding = false; r.RailProgress = 0f;
                    r.Rail = Math.Min((int)w.Rule("rail_max", 4f), Math.Max(0, r.Rail) + 1);
                    w.Events.Publish(new RailBuilt(r.Id, r.Rail));
                }
            }
            if (r.Project is string proj)
            {
                // um depósito é a excepção: continua a levantar-se em terra tomada, porque é exactamente
                // para isso que ele serve — levar a rede atrás da ofensiva
                bool hub = w.BuildingDefs.TryGetValue(proj, out var pdef) && pdef.IsHub;
                bool lost = hub ? r.ProjectOwner != 0 && r.ControllerId != r.ProjectOwner   // depósito: cai se a terra mudar de mãos
                                : r.ControllerId != r.OwnerId;
                if (lost || !w.BuildingDefs.TryGetValue(proj, out var def)) { r.Project = null; r.ProjectProgress = 0f; r.ProjectOwner = 0; }
                else if ((r.ProjectProgress += 1f) >= def.Days)
                {
                    r.Project = null; r.ProjectProgress = 0f; r.ProjectOwner = 0;
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
