using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Redespacho estratégico: mandar uma divisão atravessar a retaguarda pelos carris em vez de a
/// marchar pela estrada. É o que no HoI4 se faz com o botão de comboio, e faltava aqui: mudar um exército
/// de uma frente para a outra custava as mesmas semanas de marcha, o que na prática queria dizer que
/// ninguém o fazia — a reserva ficava onde tinha nascido e as frentes viviam cada uma da sua tropa.
///
/// A troca é velocidade por prontidão: pelos carris andam-se os mesmos saltos em redeploy_speed do tempo,
/// mas paga-se redeploy_org_cost de organização ao embarcar e quase não se recompõe pelo caminho
/// (redeploy_org_regain). Quem chegar ao destino e for logo atacado chega a dormir — e o comboio só anda
/// por terra que os nossos (ou os aliados) controlam: não há redespacho para dentro do inimigo, nem por mar.
///
/// Não é um ISystem: quem anda com a tropa é o MovementSystem e quem a recompõe é o RecoverySystem. Aqui
/// vivem as regras de quem pode embarcar, o caminho pela retaguarda e o que custa o bilhete.</summary>
public static class Redeploy
{
    /// <summary>Porque é que esta divisão não pode ir de comboio até ali, ou null se pode. É a mesma frase
    /// que a barra de selecção mostra ao jogador — uma só razão, escrita num sítio só.</summary>
    public static string? Block(World w, int countryId, int divisionId, int targetRegionId)
    {
        if (!w.Divisions.TryGetValue(divisionId, out var d)) return "Divisão inexistente";
        if (d.CountryId != countryId) return "Divisão não é tua";
        if (!w.Regions.TryGetValue(targetRegionId, out var target)) return "Região inexistente";
        if (d.RegionId == targetRegionId) return "Já está lá";
        if (d.InFlight) return "Em voo — só depois de aterrar";
        if (w.InBattle(divisionId)) return "Em combate: não se embarca tropa a bater-se";
        if (!w.CanTraverse(countryId, target)) return "O comboio só chega a terra nossa ou de aliado";
        if (Path(w, d.RegionId, targetRegionId, countryId) is null) return "Sem carris: a retaguarda não vai lá dar";
        return null;
    }

    /// <summary>Manda a divisão para o comboio: o caminho é pela retaguarda e o bilhete paga-se já, em
    /// organização. Ordem manual, por isso desliga o avanço automático como qualquer outra.</summary>
    public static void Launch(World w, Division d, int targetRegionId)
    {
        d.AutoAdvance = false;
        d.SetPath(Path(w, d.RegionId, targetRegionId, d.CountryId)!);
        d.Redeploying = true;
        d.Org = MathF.Max(0f, d.Org - w.Rule("redeploy_org_cost", 40f));
    }

    /// <summary>Caminho só por terra que o país (ou a facção dele) controla, sem travessias marítimas: os
    /// carris acabam na costa. Devolve os saltos, do próximo ao destino, ou null se a retaguarda não liga
    /// os dois sítios.</summary>
    public static List<int>? Path(World w, int from, int to, int countryId, int maxHops = 200)
    {
        if (from == to) return new List<int>();
        var prev = new Dictionary<int, int> { [from] = from };
        var queue = new Queue<(int id, int depth)>();
        queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            var (cur, depth) = queue.Dequeue();
            if (depth >= maxHops) continue;
            foreach (int n in w.Regions[cur].Neighbours)
            {
                if (prev.ContainsKey(n) || !w.Regions.TryGetValue(n, out var r)) continue;
                if (!w.CanTraverse(countryId, r)) continue;      // terra tomada ao inimigo também tem carris nossos
                prev[n] = cur;
                if (n != to) { queue.Enqueue((n, depth + 1)); continue; }
                var path = new List<int>();
                for (int at = to; at != from; at = prev[at]) path.Add(at);
                path.Reverse();
                return path;
            }
        }
        return null;
    }

    /// <summary>Até onde os carris chegam a partir daqui: região ligada → saltos de comboio. Uma travessia
    /// só, para quem precisa da retaguarda inteira de uma vez (a barra e o smoke) em vez de perguntar
    /// caminho a caminho.</summary>
    public static Dictionary<int, int> Reach(World w, int from, int countryId, int maxHops = 200)
    {
        var depth = new Dictionary<int, int> { [from] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            if (depth[cur] >= maxHops) continue;
            foreach (int n in w.Regions[cur].Neighbours)
            {
                if (depth.ContainsKey(n) || !w.Regions.TryGetValue(n, out var r) || !w.CanTraverse(countryId, r)) continue;
                depth[n] = depth[cur] + 1;
                queue.Enqueue(n);
            }
        }
        depth.Remove(from);
        return depth;
    }

    /// <summary>Quanto do tempo de marcha leva um salto de comboio. Vale 1 para quem vai a pé, para o
    /// MovementSystem não ter de perguntar duas vezes.</summary>
    public static float Speed(World w, Division d) => d.Redeploying ? w.Rule("redeploy_speed", 0.35f) : 1f;

    /// <summary>O comboio já não passa: o salto seguinte deixou de ser terra nossa (a frente moveu-se por
    /// cima da linha). A divisão fica onde está, em terra própria, e a ordem morre — não se entra em
    /// combate a sair de uma carruagem.</summary>
    public static bool Derailed(World w, Division d) =>
        d.Redeploying && d.TargetRegionId is int next
        && (!w.Regions.TryGetValue(next, out var r) || !w.CanTraverse(d.CountryId, r));

    /// <summary>Desce do comboio: chegou, foi cortado ou recebeu outra ordem.</summary>
    public static void Stop(Division d) => d.Redeploying = false;
}
