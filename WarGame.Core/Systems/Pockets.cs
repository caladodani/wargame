using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Um caldeirão: a terra que ficou fechada dentro do anel, quem lá está e quanto tempo lhe resta.</summary>
/// <param name="CountryId">De quem é a tropa lá dentro.</param>
/// <param name="RingCountryId">Quem fechou o anel (null quando já não há a quem entregar).</param>
/// <param name="RegionIds">A terra da bolsa, do centro para fora (a primeira é a que tem mais tropa).</param>
/// <param name="DivisionIds">As divisões apanhadas.</param>
/// <param name="Sealed">Sem uma única saída por terra amiga — é isto que faz a bolsa render-se.</param>
/// <param name="Days">Dias que a pior delas leva cercada.</param>
/// <param name="DaysLeft">Dias até baixarem as armas (null enquanto houver por onde romper).</param>
/// <param name="Men">Efectivo lá dentro (a soma da resistência das divisões).</param>
/// <param name="Vp">Pontos de vitória apanhados dentro do anel.</param>
public readonly record struct Pocket(int CountryId, int? RingCountryId, IReadOnlyList<int> RegionIds,
    IReadOnlyList<int> DivisionIds, bool Sealed, int Days, int? DaysLeft, float Men, int Vp)
{
    public int Divisions => DivisionIds.Count;
    public int Regions => RegionIds.Count;
    /// <summary>O centro da bolsa é a região com mais tropa: é lá que a chapa do caldeirão se põe.</summary>
    public int HeartId => RegionIds.Count > 0 ? RegionIds[0] : 0;
}

/// <summary>Os caldeirões do mundo, juntos e com nome — o clímax que faltava ao cerco.
///
/// O cerco já cobrava caro (PocketSystem: definha e rende-se), mas no mapa não existia: as divisões
/// apanhadas eram contadores como os outros, e a única pista era uma chapa ⛓ numa barra. No HoI4 o
/// caldeirão é a imagem da campanha — vê-se a bolsa fechar, vê-se o que ficou lá dentro e vê-se a conta
/// decrescente até a bolsa capitular. É isso que aqui se prepara: as divisões cortadas deixam de ser peças
/// soltas e passam a ser bolsas com terra, gente, dias e dono do anel.
///
/// Cortado continua a ser o que o SupplySystem escreveu (Division.Cut) — isso não se re-decide aqui. O que
/// passou para este lado foi a pergunta "a bolsa está fechada?": era feita divisão a divisão, e por isso
/// duas divisões dentro do mesmo anel achavam sempre que tinham por onde romper (uma pela terra da outra) e
/// nunca baixavam as armas. Agora pergunta-se à bolsa inteira, que é a unidade que o mapa desenha e a que
/// o PocketSystem obedece. Estado derivado: não guarda nada, não é ISystem, lê-se a cada olhada.</summary>
public static class Pockets
{
    /// <summary>Todas as bolsas do mundo, das mais aflitas para as mais folgadas (menos dias primeiro).</summary>
    public static List<Pocket> All(World w)
    {
        var list = new List<Pocket>();
        // as cortadas por país: cada país fecha as suas bolsas, e a mesma terra pode ter tropa de dois
        var byCountry = new Dictionary<int, List<Division>>();
        foreach (var d in w.Divisions.Values)
        {
            if (!d.Cut || !w.Regions.ContainsKey(d.RegionId)) continue;
            if (!byCountry.TryGetValue(d.CountryId, out var mine)) byCountry[d.CountryId] = mine = new();
            mine.Add(d);
        }

        foreach (var (country, cut) in byCountry)
        {
            var seen = new HashSet<int>();
            foreach (var seed in cut.OrderBy(d => d.RegionId).ThenBy(d => d.Id))
            {
                if (seen.Contains(seed.RegionId)) continue;
                var land = Flood(w, country, seed.RegionId, seen);
                var inside = cut.Where(d => land.Contains(d.RegionId)).ToList();
                list.Add(Make(w, country, land, inside));
            }
        }
        return list.OrderBy(p => p.DaysLeft ?? int.MaxValue).ThenByDescending(p => p.Days).ThenBy(p => p.HeartId).ToList();
    }

    /// <summary>As bolsas de um país: as nossas quando somos nós a estar lá dentro, as dele quando é ele.</summary>
    public static List<Pocket> Of(World w, int countryId) => All(w).Where(p => p.CountryId == countryId).ToList();

    /// <summary>As bolsas que este país fechou — o prémio da manobra, do lado de quem a fez.</summary>
    public static List<Pocket> Closed(World w, int countryId) => All(w).Where(p => p.RingCountryId == countryId).ToList();

    /// <summary>A pior bolsa deste país: a que se rende primeiro.</summary>
    public static Pocket? Worst(World w, int countryId) => Of(w, countryId) is { Count: > 0 } list ? list[0] : null;

    /// <summary>A terra da bolsa: a partir da região da tropa cortada, tudo o que aquele país ainda controla
    /// à volta. Como a tropa está cortada, esta mancha nunca chega a casa — se chegasse, já não estaria
    /// cortada. Por isso a travessia acaba sozinha no anel, sem tecto nenhum à mão.</summary>
    private static HashSet<int> Flood(World w, int countryId, int from, HashSet<int> seen)
    {
        var land = new HashSet<int> { from };
        var queue = new Queue<int>();
        queue.Enqueue(from); seen.Add(from);
        while (queue.Count > 0)
        {
            var here = w.Regions[queue.Dequeue()];
            foreach (int n in here.Neighbours)
            {
                if (land.Contains(n) || !w.Regions.TryGetValue(n, out var nb)) continue;
                if (nb.ControllerId != countryId) continue;      // o anel é do outro: a bolsa acaba aqui
                land.Add(n); seen.Add(n); queue.Enqueue(n);
            }
        }
        return land;
    }

    private static Pocket Make(World w, int country, HashSet<int> land, List<Division> inside)
    {
        // o coração da bolsa é onde está a maior parte da tropa: é a região que o mapa aponta e para onde a
        // câmara salta quando se toca na chapa
        var byRegion = inside.GroupBy(d => d.RegionId).ToDictionary(g => g.Key, g => g.Sum(d => d.Hp));
        var regions = land.OrderByDescending(id => byRegion.GetValueOrDefault(id, 0f)).ThenBy(id => id).ToList();

        int days = inside.Count == 0 ? 0 : inside.Max(d => d.PocketDays);

        // fechada: nem uma saída por terra amiga que não seja já da bolsa. Como a mancha é toda a terra que
        // o país ainda segura ali, "saída" só pode ser terra de um aliado de facção — o corredor que alguém
        // de fora abriu. É esta a pergunta que rende a bolsa, e é a mesma para todas as divisões lá dentro:
        // antes perguntava-se divisão a divisão, e por isso duas divisões lado a lado dentro do mesmo anel
        // achavam sempre que tinham para onde romper — uma à custa da outra — e nunca se rendiam.
        bool sealedIn = true;
        var ring = new Dictionary<int, int>();
        foreach (int id in land)
        {
            var r = w.Regions[id];
            foreach (int n in r.Neighbours)
            {
                if (land.Contains(n) || !w.Regions.TryGetValue(n, out var nb)) continue;
                if (w.CanTraverse(country, nb)) { sealedIn = false; continue; }
                if (w.AreAtWar(country, nb.ControllerId)) ring[nb.ControllerId] = ring.GetValueOrDefault(nb.ControllerId) + 1;
            }
            // terra tomada por um inimigo debaixo dos pés da bolsa conta para o anel como a de fora
            if (w.AreAtWar(country, r.ControllerId)) ring[r.ControllerId] = ring.GetValueOrDefault(r.ControllerId) + 1;
        }
        int? ringId = ring.Count == 0 ? null
            : ring.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;

        int doom = (int)w.Rule("pocket_surrender", 21f);
        int? left = doom > 0 && sealedIn && ringId is not null ? Math.Max(0, doom - days) : null;

        int vp = 0;
        foreach (int id in land) vp += VictoryPoints.Of(w, w.Regions[id]);

        return new Pocket(country, ringId, regions, inside.OrderBy(d => d.Id).Select(d => d.Id).ToList(),
                          sealedIn, days, left, inside.Sum(d => d.Hp), vp);
    }

    /// <summary>A bolsa em palavras, para a chapa do mapa e para a prova headless.</summary>
    public static string Short(World w, Pocket p)
    {
        string who = w.Countries.TryGetValue(p.CountryId, out var c) ? c.Tag : "?";
        string prazo = p.DaysLeft is int n ? $"{n} d" : p.Sealed ? "sem prazo" : "com saída";
        return $"{who} {p.Divisions} div · {p.Regions} reg · {prazo}";
    }
}
