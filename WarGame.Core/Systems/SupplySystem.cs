using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Supply de cada divisão (0..1). Contrato: corre antes de tudo no tick; escreve só Division.Supply.
/// HoI4 simplificado: território próprio ou de aliado de facção abastece (portos incluídos — ilhas não sofrem); região ocupada
/// só está abastecida se houver cadeia de regiões controladas até território próprio; o resto (bolsa cercada,
/// divisão em região hostil) leva supply_pocket. Empilhar mais de supply_stack divisões do mesmo país numa
/// região divide o supply por n/supply_stack.
///
/// Quem vive do outro lado do mar vive do porto, e um porto tem cais a mais não tem: cada nível carrega
/// port_capacity_per_level divisões. Passar disso não corta o abastecimento de vez, estrangula-o —
/// capacidade/divisões, com chão em port_overflow_min. É isto que impede desembarcar meio exército numa
/// ilha com um cais de pesca. Regras: supply_pocket, supply_stack, port_supply_factor,
/// port_capacity_per_level, port_overflow_min.</summary>
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

        // países cuja rede interessa: os que têm divisões + os controladores das regiões onde elas estão
        // (acesso militar: divisão em território de aliado bebe da rede do aliado)
        var countries = stacked.Keys.Select(k => k.country).ToHashSet();
        foreach (var d in w.Divisions.Values) countries.Add(w.Regions[d.RegionId].ControllerId);
        var linked = LinkedRegions(w, countries);
        var bySea = PortReach(w, linked, out var capacity);   // cabeças-de-praia por porto + cais de cada país
        float seaFactor = w.Rule("port_supply_factor", 0.85f);
        var strain = Strain(w, bySea, capacity);              // quanto o porto de cada país está a aguentar

        foreach (var d in w.Divisions.Values)
        {
            // ligada = controlada pelo país da divisão (ou aliado de facção) E com cadeia até território próprio
            var reg = w.Regions[d.RegionId];
            bool friendly = reg.ControllerId == d.CountryId || w.SameFaction(d.CountryId, reg.ControllerId);
            float s = friendly && linked.Contains(d.RegionId)
                ? (bySea.Contains(d.RegionId) ? seaFactor * strain.GetValueOrDefault(reg.ControllerId, 1f) : 1f)
                : pocket;
            int n = stacked[(d.CountryId, d.RegionId)];
            if (n > stack) s *= stack / n;
            d.Supply = s;
        }
    }

    /// <summary>Abastecimento por mar: um porto numa região já ligada por terra alcança, até
    /// supply_range × nível quilómetros de travessia, outras regiões costeiras do mesmo controlador —
    /// e daí a cadeia segue por terra. É isto que torna um desembarque sustentável: sem porto, a
    /// cabeça-de-praia fica em bolsa (supply_pocket) por muito que se ganhe a batalha.
    /// Devolve as regiões que só estão abastecidas por esta via (levam port_supply_factor) e, em capacity,
    /// as divisões que os cais de cada país conseguem carregar.</summary>
    private static HashSet<int> PortReach(World w, HashSet<int> linked, out Dictionary<int, float> capacity)
    {
        var bySea = new HashSet<int>();
        capacity = new Dictionary<int, float>();
        // cais bloqueado não carrega nada: enquanto a esquadra inimiga estiver naquele mar, o porto é uma
        // pedra na costa (NavalMissionSystem.Blockaded; sem missões navais no mundo isto não muda nada)
        var ports = w.Regions.Values.Where(r => r.Buildings.Count > 0 && linked.Contains(r.Id)
                                             && r.Buildings.Any(b => Range(w, b) > 0f)
                                             && !NavalMissionSystem.Blockaded(w, r.Id)).ToList();
        float perLevel = w.Rule("port_capacity_per_level", 6f);
        foreach (var port in ports)
            capacity[port.ControllerId] = capacity.GetValueOrDefault(port.ControllerId)
                                        + Levels(w, port) * perLevel;
        if (ports.Count == 0) return bySea;

        var queue = new Queue<Region>();
        foreach (var port in ports)
        {
            float reach = port.Buildings.Sum(b => Range(w, b));
            foreach (var (dst, km) in port.SeaNeighbours)
            {
                if (km > reach || !w.Regions.TryGetValue(dst, out var r)) continue;
                if (NavalMissionSystem.Blockaded(w, dst)) continue;      // rota fechada pela esquadra inimiga
                if (r.ControllerId != port.ControllerId || !linked.Add(dst)) continue;
                bySea.Add(dst); queue.Enqueue(r);
            }
        }
        // a partir da cabeça-de-praia, a cadeia continua por terra dentro do que esse país controla
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var n in cur.Neighbours)
            {
                var r = w.Regions[n];
                if (r.ControllerId != cur.ControllerId || !linked.Add(n)) continue;
                bySea.Add(n); queue.Enqueue(r);
            }
        }
        return bySea;
    }

    /// <summary>Estrangulamento de cais: as divisões que só bebem por mar contam-se ao controlador da
    /// cabeça-de-praia — é a rede dele que as carrega — e, se forem mais do que o porto aguenta, todas
    /// perdem na mesma proporção. Um país sem excesso não aparece aqui e não perde nada.</summary>
    private static Dictionary<int, float> Strain(World w, HashSet<int> bySea, Dictionary<int, float> capacity)
    {
        var strain = new Dictionary<int, float>();
        foreach (var c in w.Countries.Values) { c.PortCapacity = capacity.GetValueOrDefault(c.Id); c.SeaSupplied = 0; }
        if (bySea.Count == 0) return strain;

        var load = new Dictionary<int, int>();
        foreach (var d in w.Divisions.Values)
        {
            if (!bySea.Contains(d.RegionId)) continue;
            int cid = w.Regions[d.RegionId].ControllerId;
            load[cid] = load.GetValueOrDefault(cid) + 1;
        }

        float floor = w.Rule("port_overflow_min", 0.35f);
        foreach (var (cid, n) in load)
        {
            if (w.Countries.TryGetValue(cid, out var c)) c.SeaSupplied = n;
            float cap = capacity.GetValueOrDefault(cid);
            if (n > cap) strain[cid] = Math.Clamp(cap / n, floor, 1f);
        }
        return strain;
    }

    /// <summary>Níveis de cais de uma região: só os edifícios que alcançam mar contam.</summary>
    private static int Levels(World w, Region r) =>
        r.Buildings.Where(b => Range(w, b) > 0f).Sum(b => b.Value);

    /// <summary>Alcance por mar de um edifício construído: supply_range da definição × níveis.</summary>
    private static float Range(World w, KeyValuePair<string, int> built) =>
        w.BuildingDefs.TryGetValue(built.Key, out var def) ? def.SupplyRange * built.Value : 0f;

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
