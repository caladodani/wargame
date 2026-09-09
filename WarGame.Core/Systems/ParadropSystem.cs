using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Salto de pára-quedas (HoI4: airborne assault). Até aqui, uma divisão só chegava a uma região por
/// terra ou por mar: a frente era uma parede e a retaguarda do inimigo estava tão longe como a linha da
/// frente. As divisões de pára-quedistas existiam na tabela — Fallschirmjäger, VDV, Folgore, BRIPAC — e
/// marchavam a pé como as outras. Agora saltam.
///
/// A regra é a do jogo original, apertada ao que este mundo sabe: quem tem a marca `airborne` na ficha pode
/// ser largado a paradrop_range_hops saltos de região da sua posição, por cima de tudo o que esteja pelo
/// meio; o voo prende paradrop_wings asas de transporte por divisão (saem do pool livre, como uma missão) e
/// leva paradrop_days dias; aterrar custa organização e gente, porque um pára-quedista chega ao chão sozinho
/// e a pé. E, como no original, há duas coisas que não se fazem: saltar em cima de tropa inimiga e saltar
/// debaixo de um céu que é do inimigo — sem superioridade aérea os transportes não chegam lá.
///
/// O que isto muda no jogo: a retaguarda deixou de ser segura. Uma ponte, um cruzamento de estradas ou uma
/// capital mal guarnecida a quatro regiões da frente passaram a ser alvo — e quem defende passa a ter de
/// olhar para trás. O preço é real: a divisão cai com metade da organização, sem trincheira, e sozinha lá
/// atrás. Serve para tomar terreno vazio, não para ganhar batalhas.
///
/// Enquanto voa, a divisão continua marcada na região de partida (é de lá que os aviões levantam) mas não
/// marcha nem aceita ordens de marcha. Se, no dia da aterragem, o chão já não estiver livre — inimigo em
/// cima, região passada a terceiro, ou a própria tropa apanhada em combate — o salto desfaz-se e ela fica
/// onde estava, com o combustível gasto (metade do custo de organização).</summary>
public sealed class ParadropSystem : ISystem
{
    public string Name => "Paradrop";

    public void Tick(World w)
    {
        // ToList: aterrar muta regiões e publica eventos que mexem no mundo por baixo do foreach
        foreach (var d in w.Divisions.Values.Where(x => x.InFlight).OrderBy(x => x.Id).ToList())
        {
            d.DropDays -= 1f;
            if (d.DropDays > 0f) continue;
            Land(w, d);
        }
    }

    /// <summary>Chegou o dia: o chão volta a ser olhado antes de alguém saltar para cima dele.</summary>
    private static void Land(World w, Division d)
    {
        int target = d.DropTargetId ?? d.RegionId;
        d.DropDays = 0f; d.DropTargetId = null;

        if (!w.Regions.TryGetValue(target, out var r)) { Abort(w, d, target, "o alvo já não está no mapa"); return; }
        if (w.InBattle(d.Id)) { Abort(w, d, target, "a tropa foi apanhada em combate antes de levantar"); return; }
        if (Occupied(w, d.CountryId, r)) { Abort(w, d, target, "o inimigo ocupou o terreno de salto"); return; }
        if (!w.CanTraverse(d.CountryId, r) && !w.IsHostile(d.CountryId, r))
        { Abort(w, d, target, "a região passou a terceiro"); return; }

        d.Org = MathF.Max(0f, d.Org - w.Rule("paradrop_org_cost", 45f));
        d.Hp = MathF.Max(1f, d.Hp - w.Rule("paradrop_hp_cost", 8f));
        d.Entrench = 0f;                       // caiu agora: não há trincheira nenhuma cavada lá atrás

        bool captured = w.IsHostile(d.CountryId, r);
        if (captured)
        {
            int old = r.ControllerId;
            r.ControllerId = d.CountryId;
            CombatSystem.CaptureDamage(w, r);
            w.NoteWarProgress(old, d.CountryId);
            w.Events.Publish(new RegionCaptured(r.Id, old, d.CountryId));
            d.Captures++;
        }
        w.PlaceDivision(d, r.Id);
        d.ClearPath();
        w.Events.Publish(new ParadropLanded(d.Id, d.CountryId, r.Id, captured));
    }

    /// <summary>Salto desfeito em voo: a tropa fica onde estava e paga metade da queda em organização.</summary>
    private static void Abort(World w, Division d, int regionId, string why)
    {
        d.Org = MathF.Max(0f, d.Org - w.Rule("paradrop_org_cost", 45f) * 0.5f);
        w.Events.Publish(new ParadropAborted(d.Id, d.CountryId, regionId, why));
    }

    /// <summary>Manda a divisão para o ar. Único sítio que escreve DropTargetId/DropDays — o comando do
    /// jogador passa por aqui e valida-se sempre pelo Block().</summary>
    public static void Launch(World w, Division d, int regionId)
    {
        d.ClearPath();
        d.AutoAdvance = false;          // ordem manual manda, como no movimento
        d.DropTargetId = regionId;
        d.DropDays = MathF.Max(1f, w.Rule("paradrop_days", 2f));
        w.Events.Publish(new ParadropLaunched(d.Id, d.CountryId, d.RegionId, regionId, d.DropDays));
    }

    /// <summary>Porque é que esta divisão não pode saltar para esta região (null = pode). É a mesma razão que
    /// o comando devolve e que a barra de selecção mostra quando a ordem não parte.</summary>
    public static string? Block(World w, int countryId, int divisionId, int regionId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Divisions.TryGetValue(divisionId, out var d)) return "divisão inexistente";
        if (d.CountryId != countryId) return "divisão não é tua";
        if (!w.Regions.TryGetValue(regionId, out var r)) return "região inválida";
        if (d.InFlight) return "já vai no ar";
        if (!IsAirborne(w, d)) return "não são pára-quedistas";
        if (w.InBattle(d.Id)) return "em combate";
        if (d.RegionId == regionId) return "já está lá";
        float minOrg = w.Rule("paradrop_min_org", 40f);
        if (d.Org < minOrg) return $"organização abaixo de {minOrg:0} para embarcar";
        float wings = w.Rule("paradrop_wings", 3f);
        // e têm de ser mesmo transportes: um céu cheio de caças não larga um pára-quedista que seja
        if (AirMissionSystem.Free(w, countryId, "transport") < wings)
            return $"faltam transportes livres ({wings:0.#} asas por divisão)";
        int range = (int)w.Rule("paradrop_range_hops", 4f);
        if (Hops(w, d.RegionId, regionId, range) is null) return $"fora do alcance ({range} regiões)";
        if (!w.CanTraverse(countryId, r) && !w.IsHostile(countryId, r))
            return "nem é nossa nem é de quem combatemos";
        if (Occupied(w, countryId, r)) return "há tropa inimiga no terreno de salto";
        if (EnemySky(w, countryId, regionId)) return "o céu sobre a região é do inimigo";
        return null;
    }

    /// <summary>Esta divisão salta? A marca vem da tabela unit_tag — nenhum tipo de unidade vive no código.
    /// Template partido (sem unidades na tabela) não salta, mas também não rebenta com o mundo.</summary>
    public static bool IsAirborne(World w, Division d)
    {
        try { return w.Stats.Get(d.TemplateId).Tags.Contains("airborne"); }
        catch { return false; }
    }

    /// <summary>Asas presas a voos de transporte a decorrer. Saem do pool livre enquanto a tropa está no
    /// ar — é por isto que dois saltos ao mesmo tempo custam o dobro dos aviões.</summary>
    public static float InFlight(World w, int countryId) =>
        w.Divisions.Values.Count(d => d.CountryId == countryId && d.InFlight) * w.Rule("paradrop_wings", 3f);

    /// <summary>Divisões deste país no ar (para a UI dizer quantas vão a caminho).</summary>
    public static IEnumerable<Division> Flying(World w, int countryId) =>
        w.Divisions.Values.Where(d => d.CountryId == countryId && d.InFlight).OrderBy(d => d.Id);

    /// <summary>Tropa em guerra connosco parada nesta região.</summary>
    private static bool Occupied(World w, int countryId, Region r) =>
        r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var e) && w.AreAtWar(countryId, e.CountryId));

    /// <summary>O céu da região é do inimigo: ele tem lá mais superioridade aérea do que nós. Com o céu
    /// disputado ao meio ainda se salta — o que não se faz é atravessá-lo em transportes desarmados.</summary>
    public static bool EnemySky(World w, int countryId, int regionId)
    {
        float mine = AirMissionSystem.Superiority(w, regionId, countryId);
        float theirs = w.AirMissions
            .Where(m => m.RegionId == regionId && w.AreAtWar(countryId, m.CountryId))
            .Select(m => m.CountryId).Distinct()
            .Sum(id => AirMissionSystem.Superiority(w, regionId, id));
        return theirs > mine;
    }

    /// <summary>Regiões ao alcance dos transportes a partir desta, com quantos saltos ficam (a origem não
    /// entra). É a mesma busca do Hops, feita uma vez só — serve a quem precisa da lista toda em vez de uma
    /// pergunta de cada vez.</summary>
    public static Dictionary<int, int> Reach(World w, int from, int max)
    {
        var found = new Dictionary<int, int>();
        if (max <= 0) return found;
        var seen = new HashSet<int> { from };
        var queue = new Queue<(int Id, int Depth)>();
        queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            var (cur, depth) = queue.Dequeue();
            if (depth >= max || !w.Regions.TryGetValue(cur, out var reg)) continue;
            foreach (var n in reg.SeaNeighbours.Count == 0 ? reg.Neighbours : reg.Neighbours.Concat(reg.SeaNeighbours.Keys))
            {
                if (!seen.Add(n)) continue;
                found[n] = depth + 1;
                queue.Enqueue((n, depth + 1));
            }
        }
        return found;
    }

    /// <summary>Saltos de região entre duas regiões, até ao tecto `max` (null = mais longe do que isso).
    /// Os transportes voam por cima de tudo — terra de quem for, e mar — por isso a busca ignora quem manda
    /// no chão; o que conta é a distância. Ligações marítimas contam como um salto, como no movimento.</summary>
    public static int? Hops(World w, int from, int to, int max)
    {
        if (from == to) return 0;
        if (max <= 0) return null;
        var seen = new HashSet<int> { from };
        var queue = new Queue<(int Id, int Depth)>();
        queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            var (cur, depth) = queue.Dequeue();
            if (depth >= max || !w.Regions.TryGetValue(cur, out var reg)) continue;
            foreach (var n in reg.SeaNeighbours.Count == 0 ? reg.Neighbours : reg.Neighbours.Concat(reg.SeaNeighbours.Keys))
            {
                if (!seen.Add(n)) continue;
                if (n == to) return depth + 1;
                queue.Enqueue((n, depth + 1));
            }
        }
        return null;
    }
}
