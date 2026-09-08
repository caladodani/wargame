using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A conta da rendição: quanto falta a um país para cair.
///
/// Capitulava-se por população ocupada e mais nada: ocupar meia dúzia de serras despovoadas não fazia
/// diferença, mas ocupar terra povoada fazia — mesmo que a capital e as praças que mandam no país ficassem
/// todas de pé. No HoI4 a rendição lê-se nos pontos de vitória: um país cai quando lhe tomam o que conta,
/// e é por isso que a linha da frente se desenha às cidades e não à mancha no mapa.
///
/// Aqui a conta mistura as duas medidas, na dose que a regra capitulate_weight_vp mandar: a 0 fica a conta
/// antiga, só de população; a 1 conta-se só pelas praças. Um país sem ponto de vitória nenhum cai sozinho
/// na conta de população, por isso a tabela pode desaparecer sem partir nada.
///
/// Nada disto se guarda: lê-se do mapa a cada olhada, e por isso a barra da rendição nunca pode contradizer
/// quem controla o quê.</summary>
public static class Capitulation
{
    /// <summary>A capital dele está na mão de um inimigo?</summary>
    public static bool CapitalLost(World w, Country c) =>
        w.Regions.TryGetValue(c.CapitalRegionId, out var seat)
        && seat.OwnerId == c.Id && seat.ControllerId != c.Id && c.AtWarWith.Contains(seat.ControllerId);

    /// <summary>A fracção a que ele cai hoje: a normal, ou a mais baixa se lhe tomaram a capital.</summary>
    public static float Limit(World w, Country c) =>
        CapitalLost(w, c) ? w.Rule("capitulate_share_capital", 0.5f) : w.Rule("capitulate_share", 0.75f);

    /// <summary>As duas medidas cruas e o peso com que se misturam: a fatia da população dele em mãos
    /// inimigas, a fatia dos pontos de vitória dele em mãos inimigas, e quanto da conta vem dos pontos
    /// (0 quando ele não tem ponto nenhum — aí a conta é toda de população, como era antes).</summary>
    public static (float Pop, float Vp, float Weight) Parts(World w, Country c)
    {
        long popTotal = 0, popLost = 0;
        int vpTotal = 0, vpLost = 0;
        foreach (var r in w.Regions.Values)
        {
            if (r.OwnerId != c.Id) continue;
            int vp = VictoryPoints.Of(w, r);
            popTotal += r.Population; vpTotal += vp;
            if (r.ControllerId != c.Id && c.AtWarWith.Contains(r.ControllerId)) { popLost += r.Population; vpLost += vp; }
        }
        float pop = popTotal > 0 ? (float)popLost / popTotal : 0f;
        float vpShare = vpTotal > 0 ? (float)vpLost / vpTotal : 0f;
        float weight = vpTotal > 0 ? Math.Clamp(w.Rule("capitulate_weight_vp", 0f), 0f, 1f) : 0f;
        return (pop, vpShare, weight);
    }

    /// <summary>Quanto do país dele já está tomado, na conta que o faz cair. 1 a quem já não é dono de
    /// região nenhuma.</summary>
    public static float Progress(World w, Country c)
    {
        if (!w.Regions.Values.Any(r => r.OwnerId == c.Id)) return 1f;
        var (pop, vp, weight) = Parts(w, c);
        return (1f - weight) * pop + weight * vp;
    }

    /// <summary>Cai hoje?</summary>
    public static bool Falls(World w, Country c) =>
        c.AtWarWith.Count > 0 && !c.Capitulated && Progress(w, c) >= MathF.Min(1f, Limit(w, c));

    /// <summary>O que ainda falta tomar-lhe para ele cair, das praças que mais valem para baixo: é a lista
    /// de alvos que responde à pergunta que se faz a olhar para o mapa — "e agora, para onde?".
    /// A capital entra na conta com o desconto que ela traz: tomá-la baixa a fracção que chega.
    /// A conta é dos inimigos todos juntos, que é como a rendição se mede — não de um deles.</summary>
    public static List<Region> Needed(World w, Country c)
    {
        var mine = w.Regions.Values.Where(r => r.OwnerId == c.Id && r.ControllerId != c.Id
                                            && c.AtWarWith.Contains(r.ControllerId)).ToList();
        var taken = new HashSet<int>(mine.Select(r => r.Id));
        var free = w.Regions.Values.Where(r => r.OwnerId == c.Id && !taken.Contains(r.Id))
                    .OrderByDescending(r => VictoryPoints.Of(w, r)).ThenByDescending(r => r.Population).ThenBy(r => r.Id)
                    .ToList();
        long popTotal = 0; int vpTotal = 0;
        foreach (var r in w.Regions.Values) if (r.OwnerId == c.Id) { popTotal += r.Population; vpTotal += VictoryPoints.Of(w, r); }
        if (popTotal == 0 && vpTotal == 0) return new List<Region>();

        long popLost = mine.Sum(r => r.Population);
        int vpLost = mine.Sum(r => VictoryPoints.Of(w, r));
        bool seat = CapitalLost(w, c);
        float weight = vpTotal > 0 ? Math.Clamp(w.Rule("capitulate_weight_vp", 0f), 0f, 1f) : 0f;

        float Now() => (1f - weight) * (popTotal > 0 ? (float)popLost / popTotal : 0f)
                     + weight * (vpTotal > 0 ? (float)vpLost / vpTotal : 0f);
        float LimitNow() => seat ? w.Rule("capitulate_share_capital", 0.5f) : w.Rule("capitulate_share", 0.75f);

        var need = new List<Region>();
        foreach (var r in free)
        {
            if (Now() >= MathF.Min(1f, LimitNow())) break;
            popLost += r.Population; vpLost += VictoryPoints.Of(w, r);
            if (r.Id == c.CapitalRegionId) seat = true;
            need.Add(r);
        }
        return Now() >= MathF.Min(1f, LimitNow()) ? need : free;   // se nem tudo chega, é tudo o que resta
    }

    /// <summary>A linha da barra: onde ele vai e onde é a queda.</summary>
    public static string Line(World w, Country c)
    {
        var (pop, vp, weight) = Parts(w, c);
        string mix = weight > 0f ? $" (gente {pop:P0} · praças {vp:P0})" : "";
        return $"{Progress(w, c):P0} de {Limit(w, c):P0}{mix}";
    }
}
