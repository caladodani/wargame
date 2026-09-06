using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Grupos de exércitos com frente atribuída. Um grupo em avanço marcha para o país inimigo que
/// lhe deram: uma só travessia em largura a partir de TODAS as regiões controladas por esse inimigo dá a
/// distância à frente, e cada divisão parada do grupo dá um salto na direcção que a encurta (empates
/// desfeitos pela região menos defendida). É a diferença entre o avanço automático — que só olha para o
/// vizinho do lado — e um exército que atravessa o mapa até à guerra que interessa.
///
/// Corre de army_group_order_days em army_group_order_days para não refazer a travessia todos os dias, e
/// nunca mexe em divisões em combate, sem organização (army_group_min_org) ou já com ordens a cumprir.</summary>
public sealed class ArmyGroupSystem : ISystem
{
    public string Name => "ArmyGroups";

    public void Tick(World w)
    {
        if (w.ArmyGroups.Count == 0) return;

        // limpeza: divisões mortas ou de outro país não ficam agarradas a um grupo
        foreach (var g in w.ArmyGroups.Values)
            g.Divisions.RemoveWhere(id => !w.Divisions.TryGetValue(id, out var d) || d.CountryId != g.CountryId);

        int period = Math.Max(1, (int)w.Rule("army_group_order_days", 2f));
        if (w.Clock.Day % period != 0) return;

        float minOrg = w.Rule("army_group_min_org", 35f);
        int range = Math.Max(1, (int)w.Rule("army_group_march_range", 25f));
        Dictionary<int, int>? defenders = null;

        foreach (var g in w.ArmyGroups.Values)
        {
            if (!g.Advancing || g.Divisions.Count == 0) continue;
            if (g.FrontCountryId is not int foe) continue;
            if (!w.Countries.TryGetValue(g.CountryId, out var c) || c.Capitulated) continue;
            if (!w.AreAtWar(g.CountryId, foe)) continue;

            var dist = FrontDistance(w, g.CountryId, foe, range);
            if (dist.Count == 0) continue;
            defenders ??= Defenders(w);

            foreach (int id in g.Divisions)
            {
                var d = w.Divisions[id];
                if (d.Path.Count > 0 || d.Org < minOrg || !d.CanFight || w.InBattle(d.Id)) continue;
                if (!dist.TryGetValue(d.RegionId, out int here) || here == 0) continue;   // já está na frente
                if (Step(w, d.RegionId, here, dist, defenders) is int hop)
                    new MoveDivisionCommand(d.CountryId, d.Id, hop).Execute(w);
            }
        }
    }

    /// <summary>Distância em saltos de cada região à frente do inimigo (0 = região controlada por ele).
    /// Travessia em largura a partir de todas as regiões dele ao mesmo tempo, limitada a `range` saltos:
    /// uma passagem serve o grupo inteiro, por muitas divisões que tenha.</summary>
    private static Dictionary<int, int> FrontDistance(World w, int countryId, int foe, int range)
    {
        var dist = new Dictionary<int, int>();
        var queue = new Queue<int>();
        foreach (var r in w.Regions.Values)
            if (r.ControllerId == foe) { dist[r.Id] = 0; queue.Enqueue(r.Id); }

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            int next = dist[cur] + 1;
            if (next > range) continue;
            var reg = w.Regions[cur];
            foreach (int n in reg.SeaNeighbours.Count == 0 ? reg.Neighbours : reg.Neighbours.Concat(reg.SeaNeighbours.Keys))
            {
                if (dist.ContainsKey(n) || !w.Regions.TryGetValue(n, out var r)) continue;
                if (!w.CanTraverse(countryId, r) && !w.AreAtWar(countryId, r.ControllerId)) continue;
                dist[n] = next;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    /// <summary>Vizinho que encurta a distância à frente; entre iguais, o menos defendido.</summary>
    private static int? Step(World w, int from, int here, Dictionary<int, int> dist, Dictionary<int, int> defenders)
    {
        var reg = w.Regions[from];
        int? best = null; int bestDef = int.MaxValue;
        foreach (int n in reg.SeaNeighbours.Count == 0 ? reg.Neighbours : reg.Neighbours.Concat(reg.SeaNeighbours.Keys))
        {
            if (!dist.TryGetValue(n, out int d) || d >= here) continue;
            int def = defenders.GetValueOrDefault(n);
            if (def < bestDef) { best = n; bestDef = def; }
        }
        return best;
    }

    private static Dictionary<int, int> Defenders(World w)
    {
        var by = new Dictionary<int, int>();
        foreach (var d in w.Divisions.Values) by[d.RegionId] = by.GetValueOrDefault(d.RegionId) + 1;
        return by;
    }

    /// <summary>Força útil do grupo (org × HP), para a UI mostrar a mesma conta que o painel Guerra usa.</summary>
    public static float Strength(World w, ArmyGroup g)
    {
        float sum = 0f;
        foreach (int id in g.Divisions)
            if (w.Divisions.TryGetValue(id, out var d)) sum += d.Org * d.Hp / 100f;
        return sum;
    }
}
