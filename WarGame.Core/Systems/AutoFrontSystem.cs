using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Frentes automáticas: divisões com AutoAdvance que estejam paradas, fora de combate e com
/// organização acima de auto_advance_min_org avançam sozinhas para a região vizinha controlada por um
/// país com quem estão em guerra, escolhendo a menos defendida. Sem vizinho inimigo ficam onde estão —
/// a ordem permanece de pé para quando a frente chegar a elas.</summary>
public sealed class AutoFrontSystem : ISystem
{
    public string Name => "AutoFront";

    public void Tick(World w)
    {
        float minOrg = w.Rule("auto_advance_min_org", 40f);
        var defenders = new Dictionary<int, int>();
        foreach (var d in w.Divisions.Values) defenders[d.RegionId] = defenders.GetValueOrDefault(d.RegionId) + 1;

        foreach (var d in w.Divisions.Values)
        {
            if (!d.AutoAdvance || d.Path.Count > 0 || d.Org < minOrg || !d.CanFight) continue;
            if (w.InBattle(d.Id)) continue;
            if (!w.Countries.TryGetValue(d.CountryId, out var c) || c.Capitulated) continue;
            if (!w.Regions.TryGetValue(d.RegionId, out var here)) continue;

            int? best = null; int bestDefenders = int.MaxValue;
            foreach (var nId in here.Neighbours)
            {
                if (!w.Regions.TryGetValue(nId, out var n) || !c.AtWarWith.Contains(n.ControllerId)) continue;
                int def = defenders.GetValueOrDefault(nId);
                if (def < bestDefenders) { best = nId; bestDefenders = def; }
            }
            if (best is int target) new MoveDivisionCommand(d.CountryId, d.Id, target).Execute(w);
        }
    }
}
