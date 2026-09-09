using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Guerra naval por mar de costa (HoI4: zonas navais e missões de esquadra). O mar existia no jogo
/// só como cano de abastecimento: as rotas do sea_link carregavam divisões para ilhas e cabeças-de-praia e
/// não havia maneira nenhuma de as cortar. Uma potência marítima e uma potência terrestre tinham exactamente
/// o mesmo mar.
///
/// Agora compram-se navios (Country.Warships) e destacam-se para o mar em frente a uma costa, com uma
/// tarefa: fechar aquele mar a quem lá manda (bloqueio), acompanhar os nossos comboios e desfazer o bloqueio
/// alheio (escolta), ou vigiar aquele mar (patrulha, que tira a costa do nevoeiro). Onde os dois lados
/// mandam esquadras, afunda-se aço todos os dias — e o que vai ao fundo sai do pool nacional para sempre.
///
/// Corre antes do abastecimento: o cais fechado hoje sente-se na fome de hoje. O bloqueio já não se ganha
/// em número mas em composição (Navy): quatro submarinos fecham um cais que quatro contratorpedeiros não
/// fecham, e enquanto a escolta do dono valer mais do que o bloqueio, o mar continua aberto.</summary>
public sealed class NavalMissionSystem : ISystem
{
    public string Name => "NavalMissions";

    public void Tick(World w)
    {
        float upkeep = w.Rule("naval_mission_upkeep", 0.8f);

        // 1. estadia no mar: quem não paga o dia manda a esquadra para o porto
        foreach (var m in w.NavalMissions.OrderBy(x => x.CountryId).ThenBy(x => x.RegionId).ToList())
        {
            if (!w.Countries.TryGetValue(m.CountryId, out var c) || c.Capitulated
                || !w.Regions.ContainsKey(m.RegionId) || m.Ships <= 0f
                || !w.NavalMissionDefs.ContainsKey(m.MissionId))
            { w.NavalMissions.Remove(m); continue; }

            float bill = m.Squadron.Sum(kv => kv.Value * Navy.Upkeep(w, kv.Key)) * upkeep * c.Stat("naval_upkeep", 1f);
            if (c.Money < bill) { w.NavalMissions.Remove(m); continue; }
            c.Money -= bill;
            Learn(w, c, m.Ships * w.Rule("navy_xp_per_ship_day", 0.05f));  // o mar ensina a quem anda nele
        }

        // cascos sem classe (marinha de partida, save antigo) passam pelo estaleiro e ganham classe
        foreach (var c in w.Countries.Values.OrderBy(x => x.Id)) Navy.Classify(w, c);

        SeaBattle(w);
        Ai(w);
    }

    /// <summary>Mar disputado: onde há esquadras de dois países em guerra, os dois vão perdendo navios à
    /// conta do lado mais fraco — mandar dois navios para um mar cheio é perdê-los sem levar nada de volta.</summary>
    private static void SeaBattle(World w)
    {
        float loss = w.Rule("naval_battle_loss", 0.05f);
        if (loss <= 0f) return;

        // O combate naval é da ZONA e não da costa: duas esquadras no mesmo mar encontram-se, ainda que
        // tenham sido destacadas para cais diferentes. Era o buraco do mar: dava para bloquear um porto ao
        // lado de uma esquadra inimiga e nunca a ver.
        foreach (var zone in w.NavalMissions.Select(m => Zones.Sea(w, m.RegionId)).Distinct()
                              .OrderBy(x => x, StringComparer.Ordinal).ToList())
        {
            var sides = w.NavalMissions.Where(m => Zones.Sea(w, m.RegionId) == zone)
                         .GroupBy(m => m.CountryId).OrderBy(g => g.Key)
                         .Select(g => (Country: g.Key, Ships: g.Sum(m => m.Ships),
                                       Power: g.Sum(m => Navy.Power(w, m.Squadron)),
                                       Missions: g.OrderBy(m => m.RegionId).ToList()))
                         .ToList();
            foreach (var a in sides)
                foreach (var b in sides)
                {
                    if (a.Country >= b.Country || !w.AreAtWar(a.Country, b.Country)) continue;
                    float hit = MathF.Min(a.Ships, b.Ships) * loss;
                    // quem leva a esquadra mais pesada afunda mais e perde menos: é a QUALIDADE do casco a
                    // decidir, medida por navio (peso ÷ cascos) e não em bruto — senão a esquadra maior seria
                    // castigada por ser maior, quando o tamanho já conta no `hit`. Duas esquadras da mesma
                    // classe dão sempre 1, sejam duas ou duzentas: o mundo sem classes luta como sempre lutou.
                    float swing = MathF.Max(1f, w.Rule("naval_power_swing", 1.5f));
                    float aAvg = a.Ships > 0f ? a.Power / a.Ships : 0f, bAvg = b.Ships > 0f ? b.Power / b.Ships : 0f;
                    float edge = aAvg <= 0f || bAvg <= 0f ? 1f : Math.Clamp(bAvg / aAvg, 1f / swing, swing);
                    // naval_losses < 1 é couraça e pontaria: leva-se menos aço ao fundo pelo mesmo combate
                    float aLost = Sink(w, a.Missions, hit * Mult(w, a.Country) * edge);
                    float bLost = Sink(w, b.Missions, hit * Mult(w, b.Country) / edge);
                    // levou a pior quem deixou lá a maior fatia da esquadra — e é essa que arrisca o almirante
                    float aShare = Share(aLost, a.Ships), bShare = Share(bLost, b.Ships);
                    int ra = a.Missions[0].RegionId, rb = b.Missions[0].RegionId;
                    w.Events.Publish(new SeaCombatEnded(ra, a.Country, b.Country, aLost, aShare > bShare));
                    w.Events.Publish(new SeaCombatEnded(rb, b.Country, a.Country, bLost, bShare > aShare));
                }
        }
        w.NavalMissions.RemoveAll(m => m.Ships <= 0.001f);
    }

    private static float Mult(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Stat("naval_losses", 1f) : 1f;

    /// <summary>Que fatia da esquadra que estava lá ficou lá (0 quando não estava lá nada).</summary>
    private static float Share(float lost, float had) => had <= 0f ? 0f : lost / had;

    /// <summary>Navios ao fundo: saem da missão e do pool nacional — não voltam. A marinha que os perdeu
    /// aprende com o combate: é assim que se pagam as escolas do mar. Público porque o aço já não vai ao
    /// fundo só por obra de outro navio: o ataque naval pelo ar (AirMissionSystem) afunda pela mesma conta,
    /// com a mesma escolta a levar os tiros primeiro.</summary>
    public static float Sink(World w, List<NavalMission> missions, float ships)
    {
        float pool = missions.Sum(m => m.Ships), gone = 0f;
        if (pool <= 0f) return 0f;
        if (!w.Countries.TryGetValue(missions[0].CountryId, out var c)) return 0f;
        foreach (var m in missions)
        {
            // a escolta leva os tiros primeiro (Navy.Sink) e o que se afundou sai também do pool nacional
            var lost = Navy.Sink(w, m.Squadron, MathF.Min(m.Ships, ships * m.Ships / pool));
            Navy.Lose(w, c, lost);
            gone += lost.Values.Sum();
        }
        if (gone <= 0f) return gone;
        Learn(w, c, gone * w.Rule("navy_xp_per_loss", 3f));
        return gone;
    }

    /// <summary>Experiência naval, com o tecto da regra — é a moeda das escolas do mar.</summary>
    public static void Learn(World w, Country c, float xp)
    {
        if (xp <= 0f) return;
        c.NavyXp = MathF.Min(w.Rule("navy_xp_max", 400f), c.NavyXp + xp);
    }

    /// <summary>A IA em guerra manda o que tem de sobra para bloquear a costa inimiga que lhe fica ao
    /// alcance e mais valor tem (cais primeiro, depois população). Guarda naval_ai_reserve navios em casa.</summary>
    private static void Ai(World w)
    {
        float reserve = w.Rule("naval_ai_reserve", 1f);
        foreach (var c in w.Countries.Values.OrderBy(x => x.Id))
        {
            if (c.IsPlayer || c.Capitulated || c.AtWarWith.Count == 0) continue;
            float free = Free(w, c.Id) - reserve;
            if (free < w.Rule("naval_mission_min_ships", 1f)) continue;
            if (Target(w, c.Id) is not int coast) continue;
            Assign(w, c.Id, coast, "bloqueio", free);
        }
    }

    /// <summary>Costa inimiga que mais dói bloquear e que está ao nosso alcance: primeiro a que tem cais,
    /// depois a mais povoada. Empates pelo id, para o mundo não depender de sementes.</summary>
    public static int? Target(World w, int countryId)
    {
        int? best = null; (int Port, float People) score = (-1, -1f);
        foreach (var r in w.Regions.Values.OrderBy(x => x.Id))
        {
            if (r.ControllerId == countryId || !w.AreAtWar(countryId, r.ControllerId)) continue;
            if (!InRange(w, countryId, r)) continue;
            var here = (r.Buildings.Values.Sum(), (float)r.Population);
            if (here.Item1 > score.Port || (here.Item1 == score.Port && here.Item2 > score.People))
            { score = here; best = r.Id; }
        }
        return best;
    }

    /// <summary>Este mar está ao alcance dos nossos portos? Há rota marítima (sea_link) até uma costa
    /// nossa dentro de naval_range_km — as esquadras não aparecem do outro lado do mundo.</summary>
    public static bool InRange(World w, int countryId, Region r)
    {
        if (r.ControllerId == countryId) return true;         // a nossa própria costa está sempre à mão
        float range = w.Rule("naval_range_km", 1500f);
        foreach (var (dst, km) in r.SeaNeighbours)
            if (km <= range && w.Regions.TryGetValue(dst, out var near) && near.ControllerId == countryId)
                return true;
        return false;
    }

    /// <summary>Destaca (ou engrossa) uma esquadra. Único sítio que escreve World.NavalMissions — o comando
    /// do jogador e a IA passam os dois por aqui.</summary>
    public static void Assign(World w, int countryId, int regionId, string missionId, float ships)
    {
        if (ships <= 0f || !w.NavalMissionDefs.ContainsKey(missionId)) return;
        string effect = w.NavalMissionDefs[missionId].Effect;
        var have = w.NavalMissions.FirstOrDefault(m => m.CountryId == countryId && m.RegionId == regionId);
        if (have is not null && have.MissionId == missionId)
        {   // engrossar a esquadra que já lá está: saem do porto os melhores cascos que restam para a tarefa
            foreach (var (cls, n) in Navy.Pick(w, countryId, effect, ships))
                have.Squadron[cls] = have.Squadron.GetValueOrDefault(cls) + n;
            return;
        }
        // trocar de tarefa no mesmo mar não manda ninguém para o porto: os navios que lá estavam mudam de
        // ordem, e por isso guardam o nome da esquadra
        if (have is not null) w.NavalMissions.Remove(have);
        var fresh = new NavalMission
        {
            CountryId = countryId, RegionId = regionId, MissionId = missionId, SinceDay = w.Clock.Day,
            Name = have?.Name is { Length: > 0 } old ? old : w.NextFormationName(countryId, World.Sea, regionId),
        };
        // os que já lá estavam mudam de ordem sem voltar ao porto; os novos escolhem-se para a tarefa nova
        if (have is not null)
            foreach (var (cls, n) in have.Squadron) fresh.Squadron[cls] = fresh.Squadron.GetValueOrDefault(cls) + n;
        w.NavalMissions.Add(fresh);
        foreach (var (cls, n) in Navy.Pick(w, countryId, effect, ships))
            fresh.Squadron[cls] = fresh.Squadron.GetValueOrDefault(cls) + n;
    }

    /// <summary>Chama a esquadra de volta: os navios voltam ao pool livre no mesmo dia.</summary>
    public static bool Recall(World w, int countryId, int regionId) =>
        w.NavalMissions.RemoveAll(m => m.CountryId == countryId && m.RegionId == regionId) > 0;

    /// <summary>Navios presos a missões.</summary>
    public static float Assigned(World w, int countryId) =>
        w.NavalMissions.Where(m => m.CountryId == countryId).Sum(m => m.Ships);

    /// <summary>Navios ainda no porto: o que se pode destacar hoje.</summary>
    public static float Free(World w, int countryId) =>
        MathF.Max(0f, (w.Countries.TryGetValue(countryId, out var c) ? c.Warships : 0f) - Assigned(w, countryId));

    /// <summary>Peso de uma tarefa neste mar, somando os navios de um país (e dos aliados dele, que escoltam
    /// os mesmos comboios) que lá estão a fazê-la. A escola do mar de cada um pesa aqui: uma marinha de corso
    /// aperta mais o bloqueio com os mesmos navios, uma marinha de esquadra escolta melhor.</summary>
    private static float Weight(World w, int regionId, string effect, Func<int, bool> side) =>
        w.NavalMissions.Where(m => side(m.CountryId)
                                   && w.NavalMissionDefs.TryGetValue(m.MissionId, out var d) && d.Effect == effect)
            .Sum(m => m.Squadron.Sum(kv => kv.Value * Navy.Value(w, kv.Key, effect))
                      * w.NavalMissionDefs[m.MissionId].Value * School(w, m.CountryId, effect)
                      * Zones.Reach(w, regionId, m.RegionId, true));

    /// <summary>Quanto a escola do mar deste país acrescenta a esta tarefa (1 = marinha sem escola).</summary>
    private static float School(World w, int countryId, string effect) =>
        w.Countries.TryGetValue(countryId, out var c)
            ? c.Stat(effect switch { "blockade" => "naval_blockade", "escort" => "naval_escort", _ => "naval_patrol" }, 1f)
            : 1f;

    /// <summary>Esta costa está bloqueada? Há mais bloqueio inimigo do que escolta de quem manda na região
    /// (a escolta dos aliados dele conta). Enquanto a escolta igualar, o mar continua aberto.</summary>
    public static bool Blockaded(World w, int regionId)
    {
        if (w.NavalMissions.Count == 0 || !w.Regions.TryGetValue(regionId, out var r)) return false;
        int held = r.ControllerId;
        float siege = Weight(w, regionId, "blockade", cid => w.AreAtWar(cid, held));
        if (siege <= 0f) return false;
        float guard = Weight(w, regionId, "escort", cid => cid == held || w.SameFaction(cid, held));
        return siege > guard;
    }

    /// <summary>Temos patrulha neste mar? (a patrulha de um aliado vale pela nossa: vê-se o mesmo mar).</summary>
    public static bool Patrols(World w, int countryId, int regionId) =>
        w.NavalMissions.Count > 0
        && Weight(w, regionId, "patrol", cid => cid == countryId || w.SameFaction(cid, countryId)) > 0f;

    /// <summary>Porque é que este país não pode destacar navios para este mar (null = pode). É a mesma razão
    /// que o comando devolve e que o painel mostra por baixo do botão desligado.</summary>
    public static string? Block(World w, int countryId, int regionId, string missionId, float ships)
    {
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Regions.TryGetValue(regionId, out var r)) return "região inválida";
        if (!w.NavalMissionDefs.TryGetValue(missionId, out var def)) return "missão desconhecida";
        if (ships < w.Rule("naval_mission_min_ships", 1f)) return "poucos navios para uma esquadra";
        if (ships > Free(w, countryId) + 0.001f) return $"só há {Free(w, countryId):0.#} navios no porto";
        if (r.SeaNeighbours.Count == 0) return "aquilo não tem mar";
        // a guerra manda: uma costa de quem nos declarou guerra é costa inimiga, ainda que a facção seja a mesma
        bool foe = w.AreAtWar(countryId, r.ControllerId);
        bool mine = !foe && (r.ControllerId == countryId || w.SameFaction(countryId, r.ControllerId));
        if (def.Effect == "blockade" && !foe)
            return mine ? "não se bloqueia a nossa própria costa" : "não estamos em guerra com quem lá manda";
        if (def.Effect == "escort" && !mine) return "só se escoltam comboios em costa nossa";
        if (!mine && !foe) return "não estamos em guerra com quem lá manda";
        if (!InRange(w, countryId, r)) return "sem rota marítima nossa até esse mar";
        if (c.Money < ships * w.Rule("naval_mission_upkeep", 0.8f)) return "cofre curto para a estadia do dia";
        return null;
    }
}
