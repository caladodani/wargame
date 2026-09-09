using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Guerra aérea por região (HoI4: zonas aéreas e missões). Os esquadrões deixam de ser um número
/// nacional que pesava igual em todas as batalhas do mundo: destacam-se para o céu de uma região e ficam lá
/// a fazer uma coisa concreta — varrer o céu (superioridade), bater no chão ao lado da tropa (apoio
/// próximo) ou deitar abaixo a infraestrutura de quem manda na região (bombardeamento).
///
/// O que isto muda no jogo: a aviação passa a ser uma decisão de todos os dias, com um custo (cada asa
/// destacada come air_mission_upkeep por dia) e um risco (no céu disputado abatem-se aviões dos dois lados,
/// e as asas perdidas saem do pool nacional para sempre). Concentrar tudo numa frente ganha-a; espalhar
/// pelas cinco não ganha nenhuma. Antes, comprar esquadrões era carregar num botão e esquecer.
///
/// Corre antes do combate: a batalha do dia já vê o céu deste dia. As asas presas a missões saem do pool
/// livre — Country.AirPower é o total, Free() é o que ainda está em casa.</summary>
public sealed class AirMissionSystem : ISystem
{
    public string Name => "AirMissions";

    public void Tick(World w)
    {
        float upkeep = w.Rule("air_mission_upkeep", 0.6f);
        float floor = w.Rule("air_bomb_infra_min", 0.25f);

        // 1. estadia: quem não paga o dia recolhe os aviões a casa
        foreach (var m in w.AirMissions.OrderBy(x => x.CountryId).ThenBy(x => x.RegionId).ToList())
        {
            if (!w.Countries.TryGetValue(m.CountryId, out var c) || c.Capitulated
                || !w.Regions.ContainsKey(m.RegionId) || m.Wings <= 0f
                || !w.AirMissionDefs.ContainsKey(m.MissionId))
            { w.AirMissions.Remove(m); continue; }

            // a estadia é do que lá está: um bombardeiro estratégico come por três caças ligeiros
            float bill = 0f;
            foreach (var (cls, n) in m.Squadron) bill += n * upkeep * Air.Upkeep(w, cls);
            bill *= c.Stat("air_upkeep", 1f);
            if (c.Money < bill) { w.AirMissions.Remove(m); continue; }
            c.Money -= bill;
            Learn(w, c, m.Wings * w.Rule("air_xp_per_wing_day", 0.05f));   // voar todos os dias ensina alguma coisa
        }

        // asas sem modelo (aviação de partida, save antigo) passam pelo hangar e ganham modelo
        foreach (var c in w.Countries.Values.OrderBy(x => x.Id)) Air.Classify(w, c);

        Ground(w);
        Dogfight(w);

        // 2. bombardeamento: cada asa arranca infraestrutura ao controlador da região, até ao chão
        foreach (var m in w.AirMissions.OrderBy(x => x.CountryId).ThenBy(x => x.RegionId).ToList())
        {
            if (!w.AirMissionDefs.TryGetValue(m.MissionId, out var def) || def.Effect != "bombing") continue;
            var r = w.Regions[m.RegionId];
            if (r.ControllerId == m.CountryId) continue;              // não se bombardeia a própria casa
            float punch = w.Countries.TryGetValue(m.CountryId, out var bomber) ? bomber.Stat("air_bombing", 1f) : 1f;
            // com o céu fechado os bombardeiros levantam e não encontram o alvo: o que se arranca é o que o
            // tempo deixa arrancar (Weather.AirMult)
            r.Infrastructure = MathF.Max(floor, r.Infrastructure
                                                - Rendered(w, m, "bombing") * def.Value * punch * Weather.AirMult(w, r));
        }

        Ai(w);
    }

    /// <summary>As camas do dia (AirBases): uma asa só levanta se tiver campo nosso ao alcance daquele céu.
    /// O que não tem cama volta ao pool no mesmo dia — é o que acontece quando o campo cai em mãos inimigas,
    /// quando a frente se afasta para lá do raio dos aviões ou quando se quis pôr no ar mais gente do que a
    /// terra à volta aguenta. Não se perde nenhum avião: fica em casa.</summary>
    private static void Ground(World w)
    {
        foreach (int cid in w.AirMissions.Select(m => m.CountryId).Distinct().OrderBy(x => x).ToList())
        {
            var beds = AirBases.Beds(w, cid);
            foreach (var (m, mine) in beds.OrderBy(p => p.Key.RegionId).ToList())
            {
                float seated = mine.Sum(b => b.Wings), grounded = m.Wings - seated;
                if (grounded <= 0.001f) continue;
                m.Wings = seated;                       // o excedente volta ao pool: Free() volta a contá-lo
                if (m.Wings <= 0.001f) w.AirMissions.Remove(m);
                w.Events.Publish(new AirWingsGrounded(m.RegionId, cid, grounded));
            }
        }
    }

    /// <summary>Céu disputado: onde há asas de dois países em guerra, os dois perdem aviões à conta do lado
    /// mais fraco — quem manda pouca coisa para um céu cheio perde-a toda e não leva nada de volta.</summary>
    private static void Dogfight(World w)
    {
        float loss = w.Rule("air_dogfight_loss", 0.04f);
        if (loss <= 0f) return;

        // O combate aéreo é da ZONA e não da província: quem manda asas para o mesmo pedaço de mundo
        // encontra-se, ainda que os alvos sejam duas províncias diferentes. Antes, duas aviações no mesmo
        // teatro passavam a guerra inteira sem se cruzarem — e uma missão vizinha à batalha não corria
        // risco nenhum. O que cada país tem na zona conta todo; o que perde reparte-se pelas missões dele.
        foreach (var zone in w.AirMissions.Select(m => Zones.Air(w, m.RegionId)).Distinct()
                              .OrderBy(x => x, StringComparer.Ordinal).ToList())
        {
            var sides = w.AirMissions.Where(m => Zones.Air(w, m.RegionId) == zone)
                         .GroupBy(m => m.CountryId).OrderBy(g => g.Key)
                         .Select(g => (Country: g.Key, Wings: g.Sum(m => m.Wings), Missions: g.OrderBy(m => m.RegionId).ToList()))
                         .ToList();
            foreach (var a in sides)
                foreach (var b in sides)
                {
                    if (a.Country >= b.Country || !w.AreAtWar(a.Country, b.Country)) continue;
                    float hit = MathF.Min(a.Wings, b.Wings) * loss;
                    // quem leva os melhores aviões perde menos: é a QUALIDADE do modelo a decidir, medida
                    // por avião e não em bruto — o tamanho já conta no `hit`, e um céu com duzentos caças
                    // contra duzentos caças iguais tem de dar exactamente o que dava antes
                    float edge = Edge(w, a.Missions, b.Missions);
                    // cada lado leva o que a sua escola do ar lhe deixa levar: air_losses < 1 é caça melhor
                    float aLost = Shoot(w, a.Missions, hit * Mult(w, a.Country) * edge);
                    float bLost = Shoot(w, b.Missions, hit * Mult(w, b.Country) / edge);
                    // levou a pior quem deixou lá a maior fatia do que tinha: é a esquadrilha pequena
                    // mandada para um céu cheio, e é ela que arrisca o comandante
                    float aShare = Share(aLost, a.Wings), bShare = Share(bLost, b.Wings);
                    int ra = a.Missions[0].RegionId, rb = b.Missions[0].RegionId;
                    w.Events.Publish(new AirCombatEnded(ra, a.Country, b.Country, aLost, aShare > bShare));
                    w.Events.Publish(new AirCombatEnded(rb, b.Country, a.Country, bLost, bShare > aShare));
                }
        }
        w.AirMissions.RemoveAll(m => m.Wings <= 0.001f);
    }

    /// <summary>Quanto a diferença de modelos agrava as perdas de um lado: a qualidade média por avião de
    /// um contra a do outro, travada por air_power_swing. Vale 1 num céu sem modelos nenhuns — e vale 1
    /// entre duas forças do mesmo modelo, sejam duas asas ou duzentas.</summary>
    private static float Edge(World w, List<AirMission> mine, List<AirMission> theirs)
    {
        float swing = MathF.Max(1f, w.Rule("air_power_swing", 1.6f));
        float a = Quality(w, mine), b = Quality(w, theirs);
        return a <= 0f || b <= 0f ? 1f : Math.Clamp(b / a, 1f / swing, swing);
    }

    /// <summary>Qualidade média por avião de tudo o que um país tem numa zona.</summary>
    private static float Quality(World w, List<AirMission> missions)
    {
        var all = new Dictionary<string, float>();
        foreach (var m in missions)
            foreach (var (cls, n) in m.Squadron) all[cls] = all.GetValueOrDefault(cls) + n;
        return Air.Quality(w, all);
    }

    private static float Mult(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Stat("air_losses", 1f) : 1f;

    /// <summary>Que fatia do que estava lá ficou lá (0 quando não estava lá nada).</summary>
    private static float Share(float lost, float had) => had <= 0f ? 0f : lost / had;

    /// <summary>Aviões abatidos: saem da missão e do pool nacional — não voltam. O que a aviação aprende com
    /// isso fica: um combate aéreo ensina muito mais num dia do que um mês de patrulha em céu vazio.</summary>
    private static float Shoot(World w, List<AirMission> missions, float wings)
    {
        float pool = missions.Sum(m => m.Wings), gone = 0f;
        if (pool <= 0f) return 0f;
        if (!w.Countries.TryGetValue(missions[0].CountryId, out var c)) return 0f;
        foreach (var m in missions)
        {
            // dentro de cada asa cai primeiro quem não sabe lutar no céu (Air.Down), e o que cai sai do
            // pool nacional pelo mesmo modelo: o campo e o ar têm de contar os mesmos aviões
            var lost = Air.Down(w, m.Squadron, wings * m.Wings / pool);
            Air.Lose(w, c, lost);
            gone += lost.Values.Sum();
        }
        if (gone <= 0f) return gone;
        Learn(w, c, gone * w.Rule("air_xp_per_loss", 3f));
        return gone;
    }

    /// <summary>Experiência aérea, com o tecto da regra — é a moeda das escolas do ar.</summary>
    public static void Learn(World w, Country c, float xp)
    {
        if (xp <= 0f) return;
        c.AirXp = MathF.Min(w.Rule("air_xp_max", 400f), c.AirXp + xp);
    }

    /// <summary>A IA em guerra manda o que tem de sobra para o céu da frente: superioridade sobre a região
    /// inimiga que faz fronteira com ela e onde já se bate mais gente. Guarda air_ai_reserve asas em casa.</summary>
    private static void Ai(World w)
    {
        float reserve = w.Rule("air_ai_reserve", 1f);
        foreach (var c in w.Countries.Values.OrderBy(x => x.Id))
        {
            if (c.IsPlayer || c.Capitulated || c.AtWarWith.Count == 0) continue;
            float free = Free(w, c.Id) - reserve;
            if (free < w.Rule("air_mission_min_wings", 1f)) continue;
            if (Front(w, c.Id) is not int target) continue;
            if (!w.AirMissionDefs.TryGetValue("superioridade", out var def)) continue;
            // não se manda para o ar o que não tem onde dormir: a IA destaca até à cama que os campos dela
            // ao alcance daquele céu ainda têm (AirBases). O resto fica em casa até haver campo. A cama
            // conta-se com o raio dos aviões que levantariam — daí o efeito da missão e não o nome dela.
            free = MathF.Min(free, AirBases.Room(w, c.Id, target, def.Effect, free));
            if (free < w.Rule("air_mission_min_wings", 1f)) continue;
            Assign(w, c.Id, target, "superioridade", free);
        }
    }

    /// <summary>Região inimiga da frente deste país: a que faz fronteira com terra nossa e tem mais tropa
    /// deles. Empates pelo id, para o mundo não depender de sementes.</summary>
    public static int? Front(World w, int countryId)
    {
        int? best = null; int most = -1;
        foreach (var r in w.Regions.Values.OrderBy(x => x.Id))
        {
            if (r.ControllerId == countryId || !w.AreAtWar(countryId, r.ControllerId)) continue;
            if (!r.Neighbours.Any(n => w.Regions.TryGetValue(n, out var nb) && nb.ControllerId == countryId)) continue;
            int men = r.DivisionIds.Count;
            if (men > most) { most = men; best = r.Id; }
        }
        return best;
    }

    /// <summary>Destaca (ou engrossa) uma missão. Único sítio que escreve World.AirMissions — o comando do
    /// jogador e a IA passam os dois por aqui.</summary>
    public static void Assign(World w, int countryId, int regionId, string missionId, float wings)
    {
        if (wings <= 0f || !w.AirMissionDefs.TryGetValue(missionId, out var def)) return;
        // que aviões é que levantam: os melhores para a tarefa que se lhes vai pedir (Air.Pick) — não se
        // mandam transportes varrer o céu nem caças arrasar uma fábrica
        var take = Air.Pick(w, countryId, def.Effect, wings);
        var have = w.AirMissions.FirstOrDefault(m => m.CountryId == countryId && m.RegionId == regionId);
        if (have is not null && have.MissionId == missionId) { Join(have.Squadron, take); return; }
        // trocar de missão no mesmo céu não manda ninguém para casa: as asas que lá estavam mudam de tarefa,
        // e por isso guardam o nome — quem está naquele céu é a mesma gente
        if (have is not null) w.AirMissions.Remove(have);
        var born = new AirMission
        {
            CountryId = countryId, RegionId = regionId, MissionId = missionId, SinceDay = w.Clock.Day,
            Name = have?.Name is { Length: > 0 } old ? old : w.NextFormationName(countryId, World.Air, regionId),
        };
        if (have is not null) Join(born.Squadron, have.Squadron);
        Join(born.Squadron, take);
        w.AirMissions.Add(born);
    }

    /// <summary>Junta aviões a uma composição, modelo a modelo.</summary>
    private static void Join(Dictionary<string, float> bag, IReadOnlyDictionary<string, float> add)
    {
        foreach (var (cls, n) in add) bag[cls] = bag.GetValueOrDefault(cls) + n;
    }

    /// <summary>Chama a missão de volta: as asas voltam ao pool livre no mesmo dia.</summary>
    public static bool Recall(World w, int countryId, int regionId) =>
        w.AirMissions.RemoveAll(m => m.CountryId == countryId && m.RegionId == regionId) > 0;

    /// <summary>Asas presas a missões.</summary>
    public static float Assigned(World w, int countryId) =>
        w.AirMissions.Where(m => m.CountryId == countryId).Sum(m => m.Wings);

    /// <summary>Asas ainda em casa: o que se pode destacar hoje. Os transportes que levam pára-quedistas
    /// também estão fora de casa enquanto o salto não acaba (ParadropSystem.InFlight).</summary>
    public static float Free(World w, int countryId) =>
        MathF.Max(0f, (w.Countries.TryGetValue(countryId, out var c) ? c.AirPower : 0f)
                      - Assigned(w, countryId) - ParadropSystem.InFlight(w, countryId));

    /// <summary>Asas em casa que servem para uma tarefa concreta: um país cheio de caças não tem
    /// transportes nenhuns para largar pára-quedistas, por muito grande que a aviação dele seja. Num mundo
    /// sem modelos de avião toda a asa serve para tudo, e a conta é a de sempre.</summary>
    public static float Free(World w, int countryId, string effect)
    {
        if (effect.Length == 0 || w.PlaneClasses.Count == 0) return Free(w, countryId);
        float sum = 0f;
        foreach (var (cls, wings) in Air.Field(w, countryId))
            if (Air.Value(w, cls, effect) > 0f) sum += wings;
        if (effect == "transport") sum -= ParadropSystem.InFlight(w, countryId);
        return MathF.Max(0f, sum);
    }

    /// <summary>Peso aéreo deste país no céu desta região: as asas de superioridade que lá tem, cada uma a
    /// valer o que a tabela diz — e só o que o céu de hoje as deixa valer (Weather.AirMult). É o que entra
    /// na balança do combate, ao lado do poder aéreo nacional.</summary>
    public static float Superiority(World w, int regionId, int countryId) =>
        Weight(w, regionId, countryId, "superiority") * Sky(w, regionId);

    /// <summary>O peso de uma tarefa no céu de uma região: as asas que este país lá tem mais as que tem no
    /// resto da ZONA (Zones.Reach). É a mudança de fundo — o céu ganha-se por zona, como no HoI4, e uma asa
    /// destacada uma vez cobre a frente toda em vez de uma província só.</summary>
    private static float Weight(World w, int regionId, int countryId, string effect) =>
        w.AirMissions.Where(m => m.CountryId == countryId
                                 && w.AirMissionDefs.TryGetValue(m.MissionId, out var d) && d.Effect == effect)
            .Sum(m => Rendered(w, m, effect) * w.AirMissionDefs[m.MissionId].Value
                      * Zones.Reach(w, regionId, m.RegionId, false));

    /// <summary>Quanto é que uma asa rende nesta tarefa: cada avião vale o que o modelo dele vale nela
    /// (plane_class), somado. Uma asa de bombardeiros estratégicos mandada varrer o céu quase não conta;
    /// a mesma asa a bombardear vale por três. Num mundo sem modelos cada avião vale 1 e a conta é a de
    /// sempre — as asas a multiplicar pelo valor da missão, como sempre foi.</summary>
    private static float Rendered(World w, AirMission m, string effect)
    {
        float sum = 0f;
        foreach (var (cls, n) in m.Squadron) sum += n * Air.Value(w, cls, effect);
        return sum;
    }

    /// <summary>Bónus de apoio próximo à força de quem combate nesta região (tecto air_support_max). Debaixo
    /// de um nevão não há apoio próximo nenhum: os aviões não saem do chão.</summary>
    public static float Support(World w, int regionId, int countryId)
    {
        float sum = Weight(w, regionId, countryId, "support") * Sky(w, regionId);
        return MathF.Min(sum, w.Rule("air_support_max", 0.35f));
    }

    /// <summary>Quanto o céu desta região deixa a aviação fazer hoje (1 = céu limpo). Uma região que não
    /// exista vale 1: quem pergunta por ela já tem outro problema.</summary>
    public static float Sky(World w, int regionId) =>
        w.Regions.TryGetValue(regionId, out var r) ? Weather.AirMult(w, r) : 1f;

    /// <summary>Porque é que este país não pode destacar asas para esta região (null = pode). É a mesma
    /// razão que o comando devolve e que o painel mostra por baixo do botão desligado.</summary>
    public static string? Block(World w, int countryId, int regionId, string missionId, float wings)
    {
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Regions.TryGetValue(regionId, out var r)) return "região inválida";
        if (!w.AirMissionDefs.TryGetValue(missionId, out var def)) return "missão desconhecida";
        if (wings < w.Rule("air_mission_min_wings", 1f)) return "poucas asas para uma missão";
        if (wings > Free(w, countryId) + 0.001f) return $"só há {Free(w, countryId):0.#} asas em casa";
        bool mine = r.ControllerId == countryId;
        if (def.Effect == "bombing" && mine) return "não se bombardeia a própria casa";
        if (!mine && !w.AreAtWar(countryId, r.ControllerId)) return "não estamos em guerra com quem lá manda";
        // alcance e cama: a asa dorme num campo nosso e só chega ao céu que couber no raio dela (AirBases).
        // Era aqui que estava a maior mentira do jogo — bastava fazer fronteira e a força aérea inteira
        // aparecia em qualquer céu do mundo, sem campo, sem lotação e sem distância.
        float reach = AirBases.Range(w, Air.Pick(w, countryId, def.Effect, wings));
        if (!AirBases.Covers(w, countryId, regionId, reach))
        {
            var (near, km) = AirBases.Nearest(w, countryId, regionId);
            return near is null ? "não há terra nossa de onde levantar"
                 : $"fora do alcance: o campo mais perto é {near.Name}, a {km:0} km, e estes aviões chegam a {reach + AirBases.Extra(w, near):0} km";
        }
        float room = AirBases.Room(w, countryId, regionId, reach);
        if (wings > room + 0.001f)
            return room < 0.05f ? "os campos ao alcance estão cheios" : $"só há cama para {room:0.#} asas nos campos ao alcance";
        if (c.Money < wings * w.Rule("air_mission_upkeep", 0.6f)) return "cofre curto para a estadia do dia";
        return null;
    }
}
