using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Países sem jogador (HoI4): produzem continuamente, guarnecem a fronteira e atacam a província
/// inimiga mais fraca quando têm superioridade. Toda a acção passa por ICommand (validado antes de executar).
/// Corre de ai_period_days em ai_period_days. Regras: ai_period_days, ai_attack_ratio, ai_max_queue,
/// ai_min_org, ai_heavy_every.</summary>
public sealed class AiSystem : ISystem
{
    public string Name => "AI";

    public void Tick(World w)
    {
        int period = Math.Max(1, (int)w.Rule("ai_period_days", 3));
        if (w.Clock.Day % period != 0) return;

        // Índices construídos uma vez por tick (~250 países, ~3000 regiões): país→divisões, região→país→CanFight.
        var divsByCountry = new Dictionary<int, List<Division>>();
        var fighters = new Dictionary<int, Dictionary<int, int>>();
        foreach (var d in w.Divisions.Values)
        {
            if (!divsByCountry.TryGetValue(d.CountryId, out var list)) divsByCountry[d.CountryId] = list = new();
            list.Add(d);
            if (!d.CanFight) continue;
            if (!fighters.TryGetValue(d.RegionId, out var byCountry)) fighters[d.RegionId] = byCountry = new();
            byCountry[d.CountryId] = byCountry.GetValueOrDefault(d.CountryId) + 1;
        }
        var regionsByController = new Dictionary<int, List<Region>>();
        foreach (var r in w.Regions.Values)
        {
            if (!regionsByController.TryGetValue(r.ControllerId, out var list)) regionsByController[r.ControllerId] = list = new();
            list.Add(r);
        }
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));

        foreach (var c in w.Countries.Values)
        {
            if (c.IsPlayer) continue;   // o jogador nunca é mexido pela IA
            var divs = divsByCountry.GetValueOrDefault(c.Id);
            if (divs is null && c.Money <= 0f) continue;
            Produce(w, c, divs?.Count ?? 0);
            // Sem guerra não há nada a fazer por terra. TODO: "war goals" (declarar guerra a vizinhos fracos).
            if (c.AtWarWith.Count == 0 || divs is null) continue;
            Fight(w, c, divs, regionsByController.GetValueOrDefault(c.Id), fighters, inBattle);
        }
    }

    /// <summary>Mantém ai_max_queue encomendas: normalmente o template mais barato; cada ai_heavy_every-ésima
    /// (contando divisões existentes + fila) o de maior breakthrough, se o orçamento chegar. O dinheiro não é
    /// gasto aqui (ProductionSystem fá-lo aos poucos), mas cada encomenda desta ronda abate no orçamento
    /// para a IA não encomendar acima do que tem.</summary>
    private static void Produce(World w, Country c, int existing)
    {
        var templates = w.Units.GetTemplates(c.Id);
        if (templates.Count == 0) return;
        int maxQueue = (int)w.Rule("ai_max_queue", 3), heavyEvery = Math.Max(1, (int)w.Rule("ai_heavy_every", 3));
        var cheapest = templates.MinBy(t => w.TemplateCost(t.Id))!;
        var strongest = templates.MaxBy(t => w.Stats.Get(t.Id)["breakthrough"])!;
        float budget = c.Money;
        while (c.Queue.Count < maxQueue)
        {
            int order = existing + c.Queue.Count + 1;
            var pick = order % heavyEvery == 0 && budget >= w.TemplateCost(strongest.Id) ? strongest : cheapest;
            float cost = w.TemplateCost(pick.Id);
            if (budget < cost) return;
            var cmd = new BuildDivisionCommand(c.Id, pick.Id);
            if (cmd.Validate(w) is not null) return;
            cmd.Execute(w); budget -= cost;
        }
    }

    /// <summary>Frente = regiões nossas com vizinho inimigo. Em cada região da frente com divisões disponíveis:
    /// alvo = região inimiga adjacente com menos defensores; vazia → 1 divisão (se sobra guarnição ou não há
    /// ameaça ao lado); defendida → grupo inteiro se ≥ defensores × ai_attack_ratio; senão fica a defender.
    /// Retaguarda → região da frente mais próxima por território próprio (BFS multi-fonte a partir da frente).</summary>
    private static void Fight(World w, Country c, List<Division> divs, List<Region>? owned,
        Dictionary<int, Dictionary<int, int>> fighters, HashSet<int> inBattle)
    {
        if (owned is null) return;
        var front = owned.Where(r => r.Neighbours.Any(n => w.IsHostile(c.Id, w.Regions[n]))).ToList();
        if (front.Count == 0) return;   // inimigo além-mar: sem naval não há nada a fazer

        float minOrg = w.Rule("ai_min_org", 50), ratio = w.Rule("ai_attack_ratio", 1.5f);
        var groups = new Dictionary<int, List<Division>>();   // região → divisões disponíveis
        foreach (var d in divs)
        {
            if (d.Path.Count > 0 || d.Org < minOrg || !d.CanFight || inBattle.Contains(d.Id)) continue;
            if (!groups.TryGetValue(d.RegionId, out var g)) groups[d.RegionId] = g = new();
            g.Add(d);
        }
        if (groups.Count == 0) return;

        var frontIds = new HashSet<int>(front.Select(r => r.Id));
        foreach (var f in front)
        {
            if (!groups.TryGetValue(f.Id, out var g)) continue;
            Region? target = null; int best = int.MaxValue, total = 0;
            foreach (var n in f.Neighbours)
            {
                var r = w.Regions[n];
                if (!w.IsHostile(c.Id, r)) continue;
                int def = Defenders(c, r.Id, fighters); total += def;
                if (def < best) { best = def; target = r; }
            }
            if (target is null) continue;
            if (best == 0) { if (g.Count >= 2 || total == 0) Send(w, c, g.Take(1), target.Id); }
            else if (g.Count >= best * ratio) Send(w, c, g, target.Id);
        }

        var nearest = new Dictionary<int, int>();   // região nossa → região da frente mais próxima
        var queue = new Queue<int>();
        foreach (var f in front) { nearest[f.Id] = f.Id; queue.Enqueue(f.Id); }
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (var n in w.Regions[cur].Neighbours)
            {
                if (nearest.ContainsKey(n) || w.Regions[n].ControllerId != c.Id) continue;
                nearest[n] = nearest[cur]; queue.Enqueue(n);
            }
        }
        foreach (var (regionId, g) in groups)
            if (!frontIds.Contains(regionId) && nearest.TryGetValue(regionId, out var dest)) Send(w, c, g, dest);
    }

    /// <summary>Divisões CanFight de países com quem `c` está em guerra, numa região.</summary>
    private static int Defenders(Country c, int regionId, Dictionary<int, Dictionary<int, int>> fighters)
    {
        if (!fighters.TryGetValue(regionId, out var byCountry)) return 0;
        int n = 0;
        foreach (var e in c.AtWarWith) n += byCountry.GetValueOrDefault(e);
        return n;
    }

    private static void Send(World w, Country c, IEnumerable<Division> divs, int target)
    {
        foreach (var d in divs)
        {
            var cmd = new MoveDivisionCommand(c.Id, d.Id, target);
            if (cmd.Validate(w) is null) cmd.Execute(w);
        }
    }
}
