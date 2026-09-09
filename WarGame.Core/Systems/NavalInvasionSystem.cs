using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Operação anfíbia (HoI4: naval invasion). Até aqui uma praia inimiga tomava-se de improviso: uma
/// divisão parada numa costa nossa recebia ordem de marcha para a costa deles e o mar era um salto de estrada
/// como outro qualquer — sem transporte, sem escolta, sem preparação e sem uma decisão pelo meio. Era a maior
/// mecânica do jogo original que aqui não existia de todo.
///
/// Agora marca-se a praia, embarca-se a tropa e espera-se. A preparação leva invasion_prep_days com uma
/// divisão e mais invasion_prep_per_div por cada uma a mais — desembarcar cinco divisões não é cinco vezes
/// desembarcar uma. Enquanto prepara, a tropa fica no cais: não marcha nem aceita ordens de marcha, e é esse
/// o preço verdadeiro da operação — são divisões que não estão na frente durante duas semanas.
///
/// No dia em que está pronta ainda tem de haver as duas coisas que fazem uma invasão possível: mercantes
/// livres para a levar (invasion_convoys_per_div) e mar nosso à chegada (invasion_sea_share da força naval
/// da zona). Sem isso a operação espera no porto — e é aqui que a guerra naval passa a servir para alguma
/// coisa em terra, que era o que faltava ao mar deste jogo.
///
/// Largada, cada divisão parte com a marca Seaborne, que é a única chave que abre um assalto por mar: sem
/// ela, uma divisão que chega ao mar pára na costa. O desembarque em si continua a custar o que sempre
/// custou (naval_invasion_org_cost, naval_invasion_penalty) — o que mudou é que já não se improvisa.</summary>
public sealed class NavalInvasionSystem : ISystem
{
    public string Name => "NavalInvasion";

    public void Tick(World w)
    {
        // ToList: largar mexe nas divisões e publica eventos que mexem no mundo por baixo do foreach
        foreach (var inv in w.NavalInvasions.OrderBy(i => i.CountryId).ThenBy(i => i.TargetId).ToList())
        {
            Prune(w, inv);
            if (inv.DivisionIds.Count == 0) { Cancel(w, inv, "ficou sem tropa embarcada"); continue; }
            if (!w.Regions.TryGetValue(inv.TargetId, out var target) || !w.IsHostile(inv.CountryId, target))
            { Cancel(w, inv, "a praia já não é de quem combatemos"); continue; }

            // a margem é contra a aritmética e não contra a regra: doze somas de 1/12 dão 0,99999988, e uma
            // operação não pode ficar presa no porto para sempre por causa da última casa decimal
            inv.Prep = MathF.Min(1f, inv.Prep + 1f / Days(w, inv.DivisionIds.Count));
            if (inv.Prep < 0.999f) continue;
            inv.Prep = 1f;
            if (Hold(w, inv) is not null) continue;      // pronta, mas o mar ou os mercantes ainda não deixam
            Launch(w, inv);
        }
    }

    /// <summary>Dias de preparação de uma operação com esta tropa. A primeira divisão paga a operação toda;
    /// cada uma a mais acrescenta o seu embarque.</summary>
    public static float Days(World w, int divisions) =>
        MathF.Max(1f, w.Rule("invasion_prep_days", 12f)
                      + MathF.Max(0, divisions - 1) * w.Rule("invasion_prep_per_div", 4f));

    /// <summary>Mercantes presos por esta operação enquanto ela existir.</summary>
    public static float Convoys(World w, int divisions) => divisions * w.Rule("invasion_convoys_per_div", 2f);

    /// <summary>Mercantes que as operações deste país já têm reservados.</summary>
    public static float Booked(World w, int countryId) =>
        w.NavalInvasions.Where(i => i.CountryId == countryId).Sum(i => Convoys(w, i.DivisionIds.Count));

    /// <summary>Mercantes que sobram ao país depois de o exército beber e o comércio carregar, antes de
    /// qualquer operação prender os seus. Pode ser negativo — uma marinha mercante ao fundo não deixa nada
    /// para ninguém, e é por isso que a conta não se aperta a zero aqui.</summary>
    public static float Pool(World w, int countryId) =>
        ConvoySystem.Available(w, countryId)
        - ConvoySystem.SupplyNeed(w, countryId) - ConvoySystem.TradeNeed(w, countryId);

    /// <summary>Mercantes que sobram para uma operação nova: o que resta do saldo depois de as operações já
    /// marcadas terem prendido os seus.</summary>
    public static float FreeConvoys(World w, int countryId) =>
        MathF.Max(0f, Pool(w, countryId) - Booked(w, countryId));

    /// <summary>Quanto do mar da zona daquela praia é nosso, de 0 a 1. Mar onde o inimigo não tem esquadra
    /// nenhuma é nosso — não se exige uma armada para atravessar um mar que ninguém disputa; mar com
    /// esquadra inimiga divide-se pela força de cada lado, e é por isso que a guerra naval passou a decidir
    /// o que acontece em terra.</summary>
    public static float SeaShare(World w, int countryId, int regionId)
    {
        string zone = Zones.Sea(w, regionId);
        float mine = 0f, theirs = 0f;
        foreach (var m in w.NavalMissions)
        {
            if (Zones.Sea(w, m.RegionId) != zone) continue;
            if (m.CountryId == countryId || w.SameFaction(m.CountryId, countryId)) mine += Navy.Power(w, m.Squadron);
            else if (w.AreAtWar(m.CountryId, countryId)) theirs += Navy.Power(w, m.Squadron);
        }
        if (theirs <= 0f) return 1f;
        return mine / (mine + theirs);
    }

    /// <summary>Porque é que uma operação pronta ainda não largou (null = larga hoje). É o que o painel
    /// escreve por baixo da barra de preparação: sem isto, uma operação parada a 100% não se explicava.</summary>
    public static string? Hold(World w, NavalInvasion inv)
    {
        float share = w.Rule("invasion_sea_share", 0.5f);
        float have = SeaShare(w, inv.CountryId, inv.TargetId);
        if (have < share) return $"o mar da zona ainda não é nosso ({have:P0} de {share:P0})";
        float need = Convoys(w, inv.DivisionIds.Count);
        // a reserva desta operação sai da conta das outras: o que se pergunta é se o saldo de hoje ainda a
        // cobre — a guerra ao comércio pode ter ido ao fundo com os navios entre a marcação e este dia
        if (Pool(w, inv.CountryId) - (Booked(w, inv.CountryId) - need) < need)
            return $"faltam mercantes para a travessia ({need:0} precisos)";
        if (inv.DivisionIds.Any(id => w.InBattle(id))) return "há tropa embarcada apanhada em combate";
        return null;
    }

    /// <summary>Larga a operação: a tropa parte com a marca do assalto e a operação sai da mesa.</summary>
    private static void Launch(World w, NavalInvasion inv)
    {
        int landed = 0;
        foreach (int id in inv.DivisionIds)
        {
            if (!w.Divisions.TryGetValue(id, out var d)) continue;
            d.AutoAdvance = false;
            d.SetPath(new List<int> { inv.TargetId });
            d.Seaborne = true;             // depois da rota: SetPath apaga a marca de propósito
            landed++;
        }
        w.NavalInvasions.Remove(inv);
        w.Events.Publish(new NavalInvasionLaunched(inv.CountryId, inv.TargetId, inv.FromId, landed, inv.Name));
    }

    /// <summary>Tira da operação a tropa que já não pode ir: morta, passada a outro dono, no ar, ou saída da
    /// costa de embarque. Uma divisão que sai do cais desiste da operação — e é assim que se cancela uma
    /// invasão sem comando nenhum: manda-se a tropa marchar para outro sítio.</summary>
    private static void Prune(World w, NavalInvasion inv)
    {
        inv.DivisionIds.RemoveAll(id => !w.Divisions.TryGetValue(id, out var d)
                                        || d.CountryId != inv.CountryId || d.InFlight
                                        || d.RegionId != inv.FromId);
    }

    private static void Cancel(World w, NavalInvasion inv, string why)
    {
        w.NavalInvasions.Remove(inv);
        w.Events.Publish(new NavalInvasionCancelled(inv.CountryId, inv.TargetId, why));
    }

    /// <summary>Marca a praia e embarca a tropa. Único sítio que cria uma operação — o comando do jogador e a
    /// IA passam os dois por aqui, e a validação é sempre a do Block().</summary>
    public static NavalInvasion Plan(World w, int countryId, int targetId, IEnumerable<int> divisionIds)
    {
        var inv = w.NavalInvasions.FirstOrDefault(i => i.CountryId == countryId && i.TargetId == targetId);
        if (inv is null)
        {
            int from = w.Divisions.TryGetValue(divisionIds.First(), out var first) ? first.RegionId : 0;
            inv = new NavalInvasion
            {
                CountryId = countryId, TargetId = targetId, FromId = from, SinceDay = w.Clock.Day,
                Name = $"Operação {(w.Regions.TryGetValue(targetId, out var beach) ? beach.Name : "sem nome")}",
            };
            w.NavalInvasions.Add(inv);
        }
        foreach (int id in divisionIds)
        {
            if (inv.DivisionIds.Contains(id)) continue;
            if (!w.Divisions.TryGetValue(id, out var d) || d.RegionId != inv.FromId) continue;
            d.ClearPath();                 // quem embarca deixa de marchar: fica no cais até a operação largar
            d.AutoAdvance = false;
            inv.DivisionIds.Add(id);
        }
        // a preparação recua com tropa nova a bordo: uma operação não fica pronta por lhe chegarem reforços
        inv.Prep = MathF.Min(inv.Prep, 1f - 1f / Days(w, inv.DivisionIds.Count));
        w.Events.Publish(new NavalInvasionPlanned(countryId, targetId, inv.FromId, inv.DivisionIds.Count, inv.Name));
        return inv;
    }

    /// <summary>Porque é que esta tropa não pode marcar esta praia (null = pode). É a razão que o comando
    /// devolve e que o painel escreve no botão que não parte.</summary>
    public static string? Block(World w, int countryId, int targetId, IReadOnlyList<int> divisionIds)
    {
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Regions.TryGetValue(targetId, out var target)) return "praia inválida";
        if (!w.IsHostile(countryId, target)) return "só se assalta costa de quem combatemos";
        if (divisionIds.Count == 0) return "sem tropa para embarcar";
        // engrossar uma operação conta com a tropa que ela já leva: a lotação da praia é da operação toda
        var old = w.NavalInvasions.FirstOrDefault(i => i.CountryId == countryId && i.TargetId == targetId);
        int already = old?.DivisionIds.Count(id => !divisionIds.Contains(id)) ?? 0;
        int total = already + divisionIds.Count;
        int max = Math.Max(1, (int)w.Rule("naval_invasion_max_divs", 3f));
        if (total > max) return $"cabem {max} divisões por praia";

        int from = old?.FromId ?? -1;
        foreach (int id in divisionIds)
        {
            if (!w.Divisions.TryGetValue(id, out var d)) return "divisão inexistente";
            if (d.CountryId != countryId) return "divisão não é tua";
            if (d.InFlight) return "há tropa no ar";
            if (w.InBattle(id)) return "há tropa em combate";
            if (from < 0) from = d.RegionId;
            else if (d.RegionId != from) return "a tropa toda tem de embarcar do mesmo cais";
            if (Embarked(w, id) && (old is null || !old.DivisionIds.Contains(id)))
                return "há tropa já embarcada noutra operação";
        }
        if (!w.Regions.TryGetValue(from, out var port)) return "cais inválido";
        if (port.ControllerId != countryId && !w.SameFaction(port.ControllerId, countryId))
            return "o cais tem de ser nosso";
        if (!port.SeaNeighbours.ContainsKey(targetId)) return "não há rota de mar deste cais para essa praia";

        // engrossar uma operação que já existe não paga duas vezes os mercantes que ela já tinha presos
        float need = Convoys(w, total);
        float others = Booked(w, countryId) - (old is null ? 0f : Convoys(w, old.DivisionIds.Count));
        float free = Pool(w, countryId) - others;
        if (free < need) return $"faltam mercantes ({need:0} precisos, {MathF.Max(0f, free):0} livres)";
        return null;
    }

    /// <summary>Divisões deste país metidas em operações anfíbias (para o painel e para o movimento saberem
    /// que aquela tropa está no cais e não marcha).</summary>
    public static bool Embarked(World w, int divisionId) =>
        w.NavalInvasions.Any(i => i.DivisionIds.Contains(divisionId));

    /// <summary>As operações de um país, das mais adiantadas para as mais atrasadas.</summary>
    public static List<NavalInvasion> Of(World w, int countryId) =>
        w.NavalInvasions.Where(i => i.CountryId == countryId)
            .OrderByDescending(i => i.Prep).ThenBy(i => i.TargetId).ToList();

    /// <summary>As praias que esta tropa alcança a partir do cais onde está: costa de quem combatemos ligada
    /// por mar a esta costa. É a lista que o painel desenha.</summary>
    public static List<Region> Beaches(World w, int countryId, int fromRegionId) =>
        !w.Regions.TryGetValue(fromRegionId, out var port) ? new List<Region>()
            : port.SeaNeighbours.Keys
                .Where(id => w.Regions.TryGetValue(id, out var r) && w.IsHostile(countryId, r))
                .Select(id => w.Regions[id])
                .OrderBy(r => r.DivisionIds.Count).ThenBy(r => r.Id).ToList();

    /// <summary>As operações de um país em palavras, para o painel e para a prova.</summary>
    public static string Short(World w, int countryId)
    {
        var list = Of(w, countryId);
        if (list.Count == 0) return "nenhuma operação anfíbia";
        var first = list[0];
        string beach = w.Regions.TryGetValue(first.TargetId, out var r) ? r.Name : "praia";
        string port = w.Regions.TryGetValue(first.FromId, out var p) ? p.Name : "cais";
        return $"{list.Count} operaç{(list.Count == 1 ? "ão" : "ões")}; {first.Name}: {first.DivisionIds.Count} "
             + $"divis{(first.DivisionIds.Count == 1 ? "ão" : "ões")} de {port} sobre {beach}, "
             + $"preparação {first.Prep:P0} de {Days(w, first.DivisionIds.Count):0} dias"
             + (first.Prep >= 1f ? $" — {Hold(w, first) ?? "larga hoje"}" : "")
             + $"; mar da zona {SeaShare(w, countryId, first.TargetId):P0}, mercantes {Booked(w, countryId):0} presos";
    }
}
