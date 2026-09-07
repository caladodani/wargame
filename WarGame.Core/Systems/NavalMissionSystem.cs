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
/// Corre antes do abastecimento: o cais fechado hoje sente-se na fome de hoje. O bloqueio ganha-se em
/// número: enquanto a escolta do dono da costa igualar os navios do bloqueio, o mar continua aberto.</summary>
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

            float bill = m.Ships * upkeep * c.Stat("naval_upkeep", 1f);
            if (c.Money < bill) { w.NavalMissions.Remove(m); continue; }
            c.Money -= bill;
            Learn(w, c, m.Ships * w.Rule("navy_xp_per_ship_day", 0.05f));  // o mar ensina a quem anda nele
        }

        SeaBattle(w);
        Ai(w);
    }

    /// <summary>Mar disputado: onde há esquadras de dois países em guerra, os dois vão perdendo navios à
    /// conta do lado mais fraco — mandar dois navios para um mar cheio é perdê-los sem levar nada de volta.</summary>
    private static void SeaBattle(World w)
    {
        float loss = w.Rule("naval_battle_loss", 0.05f);
        if (loss <= 0f) return;

        foreach (int region in w.NavalMissions.Select(m => m.RegionId).Distinct().OrderBy(x => x).ToList())
        {
            var here = w.NavalMissions.Where(m => m.RegionId == region).OrderBy(m => m.CountryId).ToList();
            foreach (var a in here)
                foreach (var b in here)
                {
                    if (a.CountryId >= b.CountryId || !w.AreAtWar(a.CountryId, b.CountryId)) continue;
                    float aHad = a.Ships, bHad = b.Ships;
                    float hit = MathF.Min(aHad, bHad) * loss;
                    // naval_losses < 1 é couraça e pontaria: leva-se menos aço ao fundo pelo mesmo combate
                    float aLost = Sink(w, a, hit * Mult(w, a.CountryId));
                    float bLost = Sink(w, b, hit * Mult(w, b.CountryId));
                    // levou a pior quem deixou lá a maior fatia da esquadra — e é essa que arrisca o almirante
                    float aShare = Share(aLost, aHad), bShare = Share(bLost, bHad);
                    w.Events.Publish(new SeaCombatEnded(region, a.CountryId, b.CountryId, aLost, aShare > bShare));
                    w.Events.Publish(new SeaCombatEnded(region, b.CountryId, a.CountryId, bLost, bShare > aShare));
                }
        }
        w.NavalMissions.RemoveAll(m => m.Ships <= 0.001f);
    }

    private static float Mult(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Stat("naval_losses", 1f) : 1f;

    /// <summary>Que fatia da esquadra que estava lá ficou lá (0 quando não estava lá nada).</summary>
    private static float Share(float lost, float had) => had <= 0f ? 0f : lost / had;

    /// <summary>Navios ao fundo: saem da missão e do pool nacional — não voltam. A marinha que os perdeu
    /// aprende com o combate: é assim que se pagam as escolas do mar.</summary>
    private static float Sink(World w, NavalMission m, float ships)
    {
        float gone = MathF.Min(m.Ships, ships);
        m.Ships -= gone;
        if (!w.Countries.TryGetValue(m.CountryId, out var c)) return gone;
        c.Warships = MathF.Max(0f, c.Warships - gone);
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
        var have = w.NavalMissions.FirstOrDefault(m => m.CountryId == countryId && m.RegionId == regionId);
        if (have is not null && have.MissionId == missionId) { have.Ships += ships; return; }
        // trocar de tarefa no mesmo mar não manda ninguém para o porto: os navios que lá estavam mudam de
        // ordem, e por isso guardam o nome da esquadra
        if (have is not null) w.NavalMissions.Remove(have);
        w.NavalMissions.Add(new NavalMission
        {
            CountryId = countryId, RegionId = regionId, MissionId = missionId,
            Ships = ships + (have?.Ships ?? 0f), SinceDay = w.Clock.Day,
            Name = have?.Name is { Length: > 0 } old ? old : w.NextFormationName(countryId, World.Sea, regionId),
        });
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
        w.NavalMissions.Where(m => m.RegionId == regionId && side(m.CountryId)
                                   && w.NavalMissionDefs.TryGetValue(m.MissionId, out var d) && d.Effect == effect)
            .Sum(m => m.Ships * w.NavalMissionDefs[m.MissionId].Value * School(w, m.CountryId, effect));

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
