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
            foreach (int id in g.Divisions.Where(id => !w.Divisions.TryGetValue(id, out var d) || d.CountryId != g.CountryId).ToList())
                w.LeaveGroup(id);

        int period = Math.Max(1, (int)w.Rule("army_group_order_days", 2f));
        if (w.Clock.Day % period != 0) return;

        float minOrg = w.Rule("army_group_min_org", 35f);
        int range = Math.Max(1, (int)w.Rule("army_group_march_range", 25f));
        Dictionary<int, int>? defenders = null;

        foreach (var g in w.ArmyGroups.Values)
        {
            if (!g.NeedsFront || g.Divisions.Count == 0) continue;
            if (g.FrontCountryId is not int foe) continue;
            if (!w.Countries.TryGetValue(g.CountryId, out var c) || c.Capitulated) continue;
            if (!w.AreAtWar(g.CountryId, foe)) continue;

            var dist = FrontDistance(w, g.CountryId, foe, range, g.FrontRegionId);
            if (dist.Count == 0) continue;
            defenders ??= Defenders(w);

            foreach (int id in g.Divisions)
            {
                var d = w.Divisions[id];
                if (d.Path.Count > 0 || d.Org < minOrg || !d.CanFight || w.InBattle(d.Id)) continue;
                if (!dist.TryGetValue(d.RegionId, out int here)) continue;   // frente fora de alcance
                if (Target(w, d, g.Stance, here, dist, defenders) is int hop)
                    new MoveDivisionCommand(d.CountryId, d.Id, hop).Execute(w);
            }
        }
    }

    /// <summary>Distância em saltos de cada região à frente do inimigo (0 = região controlada por ele).
    /// Sem âncora, a travessia em largura arranca de TODAS as regiões dele ao mesmo tempo — o grupo vai
    /// para o troço mais perto, seja ele qual for. Com âncora (Theatre.FacingId, uma região do inimigo
    /// escolhida no painel), arranca só dali: o grupo dedica-se àquele troço, mesmo que outro esteja
    /// mais perto. É a diferença entre "defende a Ucrânia" e "defende o Norte da Ucrânia".</summary>
    private static Dictionary<int, int> FrontDistance(World w, int countryId, int foe, int range, int? anchor = null)
    {
        var dist = new Dictionary<int, int>();
        var queue = new Queue<int>();
        if (anchor is int a && w.Regions.TryGetValue(a, out var ar) && ar.ControllerId == foe)
        {
            dist[a] = 0; queue.Enqueue(a);
        }
        else
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

    /// <summary>Para onde vai esta divisão, conforme a postura do grupo.
    ///
    /// A avançar, dá o salto que encurta a distância à frente até entrar em terreno inimigo. A defender,
    /// vai só até à última região nossa antes da frente (distância 1) e fica lá a segurar a linha — e se
    /// está metida em terreno inimigo (distância 0), recua para essa linha em vez de ficar a apanhar.
    /// Segurar uma linha é meia guerra: sem isto, um grupo ou avançava ou não fazia nada.</summary>
    private static int? Target(World w, Division d, GroupStance stance, int here, Dictionary<int, int> dist, Dictionary<int, int> defenders)
    {
        if (stance == GroupStance.Reserve) return Rear(w, d, here, dist);
        if (stance == GroupStance.Advance) return here == 0 ? null : Step(w, d.RegionId, here, dist, defenders);
        if (here == 1) return null;                       // já está na linha
        if (here > 1) return Step(w, d.RegionId, here, dist, defenders);
        return Back(w, d.RegionId, dist, defenders);      // dentro do inimigo: recua para a linha
    }

    /// <summary>Reserva: afasta-se da frente até estar a army_group_reserve_depth saltos dela, e só anda
    /// por terreno que já controlamos — recolher tropas gastas não é abrir uma segunda ofensiva. Chegada à
    /// profundidade combinada, fica parada a recompor-se (o RecoverySystem trata do resto).</summary>
    private static int? Rear(World w, Division d, int here, Dictionary<int, int> dist)
    {
        int depth = Math.Max(1, (int)w.Rule("army_group_reserve_depth", 3f));
        if (here >= depth) return null;

        var reg = w.Regions[d.RegionId];
        int? best = null; int bestDist = here;
        foreach (int n in reg.Neighbours)
        {
            if (!w.Regions.TryGetValue(n, out var r) || r.ControllerId != d.CountryId) continue;
            int there = dist.TryGetValue(n, out int v) ? v : depth;      // fora do alcance da travessia = bem atrás
            if (there > bestDist) { best = n; bestDist = there; }
        }
        return best;
    }

    /// <summary>Região nossa à beira da frente (distância 1) vizinha desta, a menos defendida.</summary>
    private static int? Back(World w, int from, Dictionary<int, int> dist, Dictionary<int, int> defenders)
    {
        var reg = w.Regions[from];
        int? best = null; int bestDef = int.MaxValue;
        foreach (int n in reg.SeaNeighbours.Count == 0 ? reg.Neighbours : reg.Neighbours.Concat(reg.SeaNeighbours.Keys))
        {
            if (!dist.TryGetValue(n, out int d) || d != 1) continue;
            int def = defenders.GetValueOrDefault(n);
            if (def < bestDef) { best = n; bestDef = def; }
        }
        return best;
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
            if (w.Divisions.TryGetValue(id, out var d)) sum += WarLedger.Strength(d);
        return sum;
    }
}
