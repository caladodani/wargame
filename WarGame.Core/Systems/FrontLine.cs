using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Um encosto da linha da frente: uma região minha colada a uma região dele. O ponto é onde a linha
/// passa (por omissão o meio dos dois centros; quem tem os polígonos do mapa pode acertá-lo na fronteira
/// verdadeira) e a normal aponta para a terra do inimigo, que é para onde os dentes se viram.</summary>
public readonly record struct FrontContact(int MineId, int FoeId, float X, float Y,
                                           float NormalX, float NormalY, bool Hole);

/// <summary>A linha da frente como linha, e não como um punhado de traços soltos.
///
/// Até aqui cada par (região minha, região dele) desenhava-se sozinho, com uma barra atravessada a meio
/// caminho entre os dois centros. O resultado no mapa era uma frente picada, de traços que não se tocavam —
/// e uma frente picada não se lê: não se vê onde começa, não se vê onde acaba, não se vê que aquilo é uma
/// linha só. No HoI4 a frente é um traço corrido que acompanha a fronteira, e é isso que isto faz.
///
/// O trabalho é de grafo, não de desenho: os encostos são os nós, dois encostos ligam-se quando os seus
/// troços de fronteira se encontram (partilham uma região, ou tocam-se dos dois lados no canto onde as
/// quatro se juntam), e o fio é um caminho que os percorre. Onde a frente parte mesmo — uma ilha, um
/// teatro à parte, um troço que não encosta a nada — o caminho acaba e começa outro: contínua sempre que
/// possível, nunca inventada.
///
/// Fica no Core porque é geometria e vizinhança, coisas que se provam sem abrir o Godot; o FrontOverlay só
/// pega nos fios e desenha-os.</summary>
public static class FrontLine
{
    /// <summary>Os encostos de um teatro, já encadeados em fios contínuos.</summary>
    public static List<List<FrontContact>> Strands(World w, Theatre t) => Chain(w, Contacts(w, t));

    /// <summary>Os encostos crus deste teatro, por ordem de região. Quem quiser acertar o ponto na fronteira
    /// verdadeira muda o X/Y antes de encadear — o encadeamento usa a distância entre pontos para escolher
    /// por onde continuar.</summary>
    public static List<FrontContact> Contacts(World w, Theatre t)
    {
        // regiões com tropa do dono lá dentro: um troço sem ninguém é um buraco, e desenha-se aos bocados
        var manned = new HashSet<int>();
        foreach (var d in w.Divisions.Values)
            if (w.Regions.TryGetValue(d.RegionId, out var r) && r.ControllerId == d.CountryId) manned.Add(d.RegionId);

        var list = new List<FrontContact>();
        foreach (int id in t.RegionIds)
        {
            if (!w.Regions.TryGetValue(id, out var mine)) continue;
            bool hole = !manned.Contains(id);
            foreach (int n in mine.Neighbours.OrderBy(x => x))
            {
                if (!w.Regions.TryGetValue(n, out var foe) || foe.ControllerId != t.FoeId) continue;
                float dx = foe.CenterX - mine.CenterX, dy = foe.CenterY - mine.CenterY;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                if (len < 0.001f) continue;              // duas regiões no mesmo sítio não fazem frente nenhuma
                list.Add(new FrontContact(id, n, (mine.CenterX + foe.CenterX) / 2f, (mine.CenterY + foe.CenterY) / 2f,
                                          dx / len, dy / len, hole));
            }
        }
        return list;
    }

    /// <summary>Encadeia os encostos em fios: cada fio é uma sequência em que um encosto encosta ao seguinte.
    /// Começa-se sempre por uma ponta (o nó com menos saídas por usar) e segue-se pelo vizinho mais próximo,
    /// depois estende-se para o outro lado — assim um troço de frente sai num fio só em vez de dois meios.</summary>
    public static List<List<FrontContact>> Chain(World w, IReadOnlyList<FrontContact> contacts)
    {
        var strands = new List<List<FrontContact>>();
        int n = contacts.Count;
        if (n == 0) return strands;

        // só se comparam encostos que partilham uma região ou cujas regiões são vizinhas: sem isto uma
        // guerra mundial punha-se a comparar milhares de pares que nunca se poderiam tocar
        var byMine = new Dictionary<int, List<int>>();
        var byFoe = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            if (!byMine.TryGetValue(contacts[i].MineId, out var mb)) byMine[contacts[i].MineId] = mb = new List<int>();
            mb.Add(i);
            if (!byFoe.TryGetValue(contacts[i].FoeId, out var fb)) byFoe[contacts[i].FoeId] = fb = new List<int>();
            fb.Add(i);
        }

        var adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();
        for (int i = 0; i < n; i++)
        {
            foreach (int j in Candidates(w, byMine, byFoe, contacts[i]))
            {
                if (j <= i) continue;
                if (!Meet(w, contacts[i], contacts[j])) continue;
                adj[i].Add(j); adj[j].Add(i);
            }
        }

        var left = new HashSet<int>(Enumerable.Range(0, n));
        while (left.Count > 0)
        {
            int start = -1, startFree = int.MaxValue;
            foreach (int i in left.OrderBy(x => contacts[x].MineId).ThenBy(x => contacts[x].FoeId))
            {
                int free = adj[i].Count(j => left.Contains(j));
                if (free < startFree) { startFree = free; start = i; }
                if (free <= 1) break;                     // ponta: não há começo melhor do que este
            }

            var path = Walk(w, contacts, adj, left, start);
            // e agora para o outro lado, a partir do vizinho ainda livre mais perto do começo
            int seed = Nearest(contacts, adj, left, start);
            if (seed >= 0)
            {
                var back = Walk(w, contacts, adj, left, seed);
                back.Reverse();
                back.AddRange(path);
                path = back;
            }
            strands.Add(path.Select(i => contacts[i]).ToList());
        }
        return strands;
    }

    /// <summary>Encostos que podem tocar neste: os que partilham uma das duas regiões e os das vizinhas de
    /// qualquer um dos lados. Peneira grosseira — quem decide é o <see cref="Meet"/>.</summary>
    private static IEnumerable<int> Candidates(World w, Dictionary<int, List<int>> byMine,
                                               Dictionary<int, List<int>> byFoe, FrontContact c)
    {
        foreach (int j in Around(w, byMine, c.MineId)) yield return j;
        foreach (int j in Around(w, byFoe, c.FoeId)) yield return j;
    }

    private static IEnumerable<int> Around(World w, Dictionary<int, List<int>> index, int regionId)
    {
        if (index.TryGetValue(regionId, out var same)) foreach (int j in same) yield return j;
        if (!w.Regions.TryGetValue(regionId, out var r)) yield break;
        foreach (int nb in r.Neighbours)
            if (index.TryGetValue(nb, out var near)) foreach (int j in near) yield return j;
    }

    /// <summary>Dois encostos continuam-se um ao outro quando os seus troços de fronteira se encontram. Isso
    /// acontece de duas maneiras: partilham uma região — os dois troços estão desenhados no contorno dessa
    /// região, e o contorno de uma região é uma curva só, por isso um leva ao outro (é assim que a frente
    /// dobra a esquina de um saliente) — ou então nenhuma é partilhada mas as duas de cá tocam-se e as duas de
    /// lá também, e nesse caso os troços encontram-se no canto onde as quatro regiões se juntam.</summary>
    private static bool Meet(World w, FrontContact a, FrontContact b) =>
        a.MineId == b.MineId || a.FoeId == b.FoeId
            || (Touch(w, a.MineId, b.MineId) && Touch(w, a.FoeId, b.FoeId));

    /// <summary>Anda de encosto em encosto enquanto houver por onde ir, escolhendo sempre o vizinho mais
    /// perto — é o que faz o fio seguir a fronteira em vez de saltar de um lado para o outro.</summary>
    private static List<int> Walk(World w, IReadOnlyList<FrontContact> c, List<int>[] adj, HashSet<int> left, int from)
    {
        var path = new List<int> { from };
        left.Remove(from);
        int cur = from;
        while (true)
        {
            int next = Nearest(c, adj, left, cur);
            if (next < 0) break;
            left.Remove(next); path.Add(next); cur = next;
        }
        return path;
    }

    private static int Nearest(IReadOnlyList<FrontContact> c, List<int>[] adj, HashSet<int> left, int from)
    {
        int best = -1; float bestD = float.MaxValue;
        foreach (int j in adj[from])
        {
            if (!left.Contains(j)) continue;
            float dx = c[j].X - c[from].X, dy = c[j].Y - c[from].Y;
            float d = dx * dx + dy * dy;
            if (best < 0 || d < bestD || (d == bestD && j < best)) { best = j; bestD = d; }
        }
        return best;
    }

    /// <summary>Duas regiões tocam-se se forem a mesma ou vizinhas: um fio pode virar dentro da minha região
    /// (mesma minha, inimigos vizinhos) ou correr ao longo da dele (mesmo inimigo, minhas vizinhas).</summary>
    private static bool Touch(World w, int a, int b) =>
        a == b || (w.Regions.TryGetValue(a, out var r) && r.Neighbours.Contains(b));
}
