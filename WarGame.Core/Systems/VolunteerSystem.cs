using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Voluntários (HoI4: volunteers). Mandam-se divisões nossas para a guerra de outro país sem
/// entrar nela: passam a combater sob a bandeira dele — o mapa, as frentes e o combate tratam-nas como
/// dele, porque é debaixo do comando dele que se batem — mas continuam nossas. Os homens e o material dos
/// reforços saem sempre do nosso cofre (RecoverySystem lê Division.HomeId), e no fim voltam para casa.
///
/// É a última perna do tripé de ajudar sem entrar. Já havia o adido militar (olhos) e o material de guerra
/// (ferro); faltava a única forma de ajudar que custa sangue. É o que a Espanha de 1936 ensina: quem manda
/// voluntários não está em guerra e mesmo assim enterra os seus.
///
/// O tecto é uma fatia do nosso exército (volunteer_share), nunca abaixo de um exército mínimo em casa
/// (volunteer_min_army) e nunca acima de um tecto absoluto (volunteer_max), tudo esticado pelo
/// country_stat volunteer_cap — que é por onde uma lei ou um espírito nacional mexe nisto sem tocar em
/// código. Longe de casa batem-se pior: quem cobra isso é uma linha da tabela modifier pela chave
/// `volunteer` no contexto de combate, não uma conta aqui.
///
/// A missão acaba sozinha quando deixa de fazer sentido: o anfitrião capitulou, o anfitrião fez a paz, ou
/// a guerra chegou entre nós e ele. Nesse dia as divisões voltam para casa — para a capital, porque o sítio
/// onde estavam é território de uma guerra que já não é nossa.</summary>
public sealed class VolunteerSystem : ISystem
{
    public string Name => "Volunteers";

    public void Tick(World w)
    {
        List<Division>? coming = null;
        foreach (var d in w.Divisions.Values)
        {
            if (d.VolunteerFrom is not int home) continue;
            if (Over(w, home, d.CountryId)) (coming ??= new()).Add(d);
        }
        if (coming is not null) foreach (var d in coming) Recall(w, d);
        Ai(w);
    }

    /// <summary>A IA em paz manda voluntários para a guerra de um aliado de facção — e só desse. É a regra
    /// mais apertada que a do adido de propósito: um adido custa dinheiro, um voluntário custa gente, e uma
    /// IA que despejasse um quinto do exército na primeira guerra que visse mudava o mundo todo por engano.
    /// Manda uma divisão por dia enquanto houver folga, que é como um contingente se junta na vida real.</summary>
    private static void Ai(World w)
    {
        foreach (var c in w.Countries.Values.OrderBy(x => x.Id))
        {
            if (c.IsPlayer || c.Capitulated || c.AtWarWith.Count > 0) continue;
            if (Away(w, c.Id) >= Cap(w, c.Id)) continue;
            if (Pick(w, c.Id) is int host) Send(w, c.Id, host, 1);
        }
    }

    /// <summary>Aliado de facção que se está a bater e a quem podemos mandar gente (id mais baixo primeiro,
    /// para o mundo não depender de sementes). null = não há a quem mandar.</summary>
    public static int? Pick(World w, int countryId)
    {
        foreach (var h in w.Countries.Values.OrderBy(x => x.Id))
            if (w.SameFaction(countryId, h.Id) && w.VolunteerBlock(countryId, h.Id) is null) return h.Id;
        return null;
    }

    /// <summary>A missão desta divisão já não faz sentido?</summary>
    private static bool Over(World w, int homeId, int hostId)
        => !w.Countries.TryGetValue(homeId, out var home) || home.Capitulated
        || !w.Countries.TryGetValue(hostId, out var host) || host.Capitulated
        || w.AreAtWar(homeId, hostId)
        || !w.AtWar(hostId);

    /// <summary>Quantas divisões este país pode ter fora de casa ao mesmo tempo.</summary>
    public static int Cap(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return 0;
        int army = w.Divisions.Values.Count(d => d.HomeId == countryId);
        int min = (int)w.Rule("volunteer_min_army", 5f);
        if (army < min) return 0;
        float share = w.Rule("volunteer_share", 0.2f) * c.Stat("volunteer_cap");
        return Math.Clamp((int)(army * share), 0, (int)w.Rule("volunteer_max", 8f));
    }

    /// <summary>Divisões deste país que já andam fora.</summary>
    public static int Away(World w, int countryId)
        => w.Divisions.Values.Count(d => d.VolunteerFrom == countryId);

    /// <summary>Manda `count` divisões nossas para a guerra do anfitrião. Escolhem-se as que estão longe de
    /// combate — uma divisão em batalha não desaparece do sítio onde se bate — e as mais inteiras primeiro,
    /// que mandar farrapos não é ajuda nenhuma. Devolve quantas foram.</summary>
    public static int Send(World w, int fromId, int hostId, int count)
    {
        int room = Math.Min(count, Cap(w, fromId) - Away(w, fromId));
        if (room <= 0) return 0;
        int landing = Capital(w, hostId);
        if (landing == 0) return 0;
        var busy = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));

        var picked = w.Divisions.Values
            .Where(d => d.CountryId == fromId && !d.IsVolunteer && !busy.Contains(d.Id))
            .OrderByDescending(d => d.Hp + d.Org).ThenBy(d => d.Id)
            .Take(room).ToList();

        foreach (var d in picked)
        {
            d.VolunteerFrom = fromId;
            d.CountryId = hostId;
            d.ClearPath();
            w.PlaceDivision(d, landing);
        }
        return picked.Count;
    }

    /// <summary>Chama de volta todas as nossas divisões que andam com aquele anfitrião (ou todas, se
    /// hostId for null). Devolve quantas voltaram.</summary>
    public static int RecallAll(World w, int fromId, int? hostId = null)
    {
        var home = w.Divisions.Values.Where(d => d.VolunteerFrom == fromId
                                             && (hostId is null || d.CountryId == hostId)).ToList();
        foreach (var d in home) Recall(w, d);
        return home.Count;
    }

    /// <summary>Uma divisão volta para casa: baixa a bandeira do anfitrião e aparece na nossa capital. Se a
    /// casa já não tem capital nenhuma (foi toda ocupada) fica onde está, sob a bandeira de quem a acolheu —
    /// não há para onde a mandar, e sumi-la do mapa seria pior.</summary>
    private static void Recall(World w, Division d)
    {
        if (d.VolunteerFrom is not int home) return;
        int back = Capital(w, home);
        d.VolunteerFrom = null;
        if (back == 0) return;
        d.CountryId = home;
        d.ClearPath();
        w.PlaceDivision(d, back);
    }

    /// <summary>Onde é que este país ainda manda: a capital, se ainda for dele, senão qualquer região sua.
    /// 0 = já não tem chão nenhum.</summary>
    private static int Capital(World w, int countryId)
    {
        if (w.Countries.TryGetValue(countryId, out var c)
            && w.Regions.TryGetValue(c.CapitalRegionId, out var r) && r.ControllerId == countryId)
            return c.CapitalRegionId;
        foreach (int id in w.Regions.Keys.OrderBy(x => x))
            if (w.Regions[id].ControllerId == countryId) return id;
        return 0;
    }
}
