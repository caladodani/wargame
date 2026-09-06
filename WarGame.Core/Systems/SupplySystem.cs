using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Supply de cada divisão (0..1). Contrato: corre antes de tudo no tick; escreve só Division.Supply.
/// HoI4 simplificado: território próprio abastece sempre (portos incluídos — ilhas não sofrem); região ocupada
/// só está abastecida se houver cadeia de regiões controladas até território próprio; o resto (bolsa cercada,
/// divisão em região hostil) leva supply_pocket. Empilhar mais de supply_stack divisões do mesmo país numa
/// região divide o supply por n/supply_stack. Regras em World.Rules: supply_pocket, supply_stack.</summary>
public sealed class SupplySystem : ISystem
{
    public string Name => "Supply";

    public void Tick(World w)
    {
        if (w.Divisions.Count == 0) return;
        float pocket = w.Rule("supply_pocket", 0.5f), stack = w.Rule("supply_stack", 6f);

        // divisões por (país, região): o empilhamento só conta as do mesmo país
        var stacked = new Dictionary<(int country, int region), int>();
        foreach (var d in w.Divisions.Values)
        {
            var k = (d.CountryId, d.RegionId);
            stacked[k] = stacked.GetValueOrDefault(k) + 1;
        }

        var linked = LinkedRegions(w, stacked.Keys.Select(k => k.country).ToHashSet());

        foreach (var d in w.Divisions.Values)
        {
            // ligada = controlada pelo país da divisão E com cadeia até território próprio
            float s = w.Regions[d.RegionId].ControllerId == d.CountryId && linked.Contains(d.RegionId) ? 1f : pocket;
            int n = stacked[(d.CountryId, d.RegionId)];
            if (n > stack) s *= stack / n;
            d.Supply = s;
        }
    }

    /// <summary>Regiões ligadas ao território próprio do seu controlador: BFS multi-fonte por país (fontes =
    /// regiões que possui E controla), expandindo só por regiões que esse país controla. Cada região tem um só
    /// controlador, logo um conjunto único serve para todos. Só corre para os países dados (os que têm divisões);
    /// custo total O(regiões + adjacências).</summary>
    private static HashSet<int> LinkedRegions(World w, HashSet<int> countries)
    {
        // índice controlador → território próprio (fontes da BFS), numa só passagem pelo mapa
        var sources = new Dictionary<int, List<Region>>();
        foreach (var r in w.Regions.Values)
        {
            if (r.OwnerId != r.ControllerId || !countries.Contains(r.ControllerId)) continue;
            if (!sources.TryGetValue(r.ControllerId, out var list)) sources[r.ControllerId] = list = new();
            list.Add(r);
        }

        var linked = new HashSet<int>();
        var queue = new Queue<Region>();
        foreach (var (country, own) in sources)
        {
            foreach (var r in own) if (linked.Add(r.Id)) queue.Enqueue(r);
            while (queue.Count > 0)
                foreach (var n in queue.Dequeue().Neighbours)
                {
                    var r = w.Regions[n];
                    if (r.ControllerId == country && linked.Add(n)) queue.Enqueue(r);
                }
        }
        return linked;
    }
}
