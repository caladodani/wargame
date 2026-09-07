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

            float bill = m.Wings * upkeep * c.Stat("air_upkeep", 1f);
            if (c.Money < bill) { w.AirMissions.Remove(m); continue; }
            c.Money -= bill;
            Learn(w, c, m.Wings * w.Rule("air_xp_per_wing_day", 0.05f));   // voar todos os dias ensina alguma coisa
        }

        Dogfight(w);

        // 2. bombardeamento: cada asa arranca infraestrutura ao controlador da região, até ao chão
        foreach (var m in w.AirMissions.OrderBy(x => x.CountryId).ThenBy(x => x.RegionId).ToList())
        {
            if (!w.AirMissionDefs.TryGetValue(m.MissionId, out var def) || def.Effect != "bombing") continue;
            var r = w.Regions[m.RegionId];
            if (r.ControllerId == m.CountryId) continue;              // não se bombardeia a própria casa
            float punch = w.Countries.TryGetValue(m.CountryId, out var bomber) ? bomber.Stat("air_bombing", 1f) : 1f;
            r.Infrastructure = MathF.Max(floor, r.Infrastructure - m.Wings * def.Value * punch);
        }

        Ai(w);
    }

    /// <summary>Céu disputado: onde há asas de dois países em guerra, os dois perdem aviões à conta do lado
    /// mais fraco — quem manda pouca coisa para um céu cheio perde-a toda e não leva nada de volta.</summary>
    private static void Dogfight(World w)
    {
        float loss = w.Rule("air_dogfight_loss", 0.04f);
        if (loss <= 0f) return;

        foreach (var region in w.AirMissions.Select(m => m.RegionId).Distinct().OrderBy(x => x).ToList())
        {
            var here = w.AirMissions.Where(m => m.RegionId == region).OrderBy(m => m.CountryId).ToList();
            foreach (var a in here)
                foreach (var b in here)
                {
                    if (a.CountryId >= b.CountryId || !w.AreAtWar(a.CountryId, b.CountryId)) continue;
                    float hit = MathF.Min(a.Wings, b.Wings) * loss;
                    // cada lado leva o que a sua escola do ar lhe deixa levar: air_losses < 1 é caça melhor
                    Shoot(w, a, hit * Mult(w, a.CountryId)); Shoot(w, b, hit * Mult(w, b.CountryId));
                }
        }
        w.AirMissions.RemoveAll(m => m.Wings <= 0.001f);
    }

    private static float Mult(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Stat("air_losses", 1f) : 1f;

    /// <summary>Aviões abatidos: saem da missão e do pool nacional — não voltam. O que a aviação aprende com
    /// isso fica: um combate aéreo ensina muito mais num dia do que um mês de patrulha em céu vazio.</summary>
    private static void Shoot(World w, AirMission m, float wings)
    {
        float gone = MathF.Min(m.Wings, wings);
        m.Wings -= gone;
        if (!w.Countries.TryGetValue(m.CountryId, out var c)) return;
        c.AirPower = MathF.Max(0f, c.AirPower - gone);
        Learn(w, c, gone * w.Rule("air_xp_per_loss", 3f));
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
        if (wings <= 0f || !w.AirMissionDefs.ContainsKey(missionId)) return;
        var have = w.AirMissions.FirstOrDefault(m => m.CountryId == countryId && m.RegionId == regionId);
        if (have is not null && have.MissionId == missionId) { have.Wings += wings; return; }
        // trocar de missão no mesmo céu não manda ninguém para casa: as asas que lá estavam mudam de tarefa
        if (have is not null) w.AirMissions.Remove(have);
        w.AirMissions.Add(new AirMission
        {
            CountryId = countryId, RegionId = regionId, MissionId = missionId,
            Wings = wings + (have?.Wings ?? 0f), SinceDay = w.Clock.Day,
        });
    }

    /// <summary>Chama a missão de volta: as asas voltam ao pool livre no mesmo dia.</summary>
    public static bool Recall(World w, int countryId, int regionId) =>
        w.AirMissions.RemoveAll(m => m.CountryId == countryId && m.RegionId == regionId) > 0;

    /// <summary>Asas presas a missões.</summary>
    public static float Assigned(World w, int countryId) =>
        w.AirMissions.Where(m => m.CountryId == countryId).Sum(m => m.Wings);

    /// <summary>Asas ainda em casa: o que se pode destacar hoje.</summary>
    public static float Free(World w, int countryId) =>
        MathF.Max(0f, (w.Countries.TryGetValue(countryId, out var c) ? c.AirPower : 0f) - Assigned(w, countryId));

    /// <summary>Peso aéreo deste país no céu desta região: as asas de superioridade que lá tem, cada uma a
    /// valer o que a tabela diz. É o que entra na balança do combate, ao lado do poder aéreo nacional.</summary>
    public static float Superiority(World w, int regionId, int countryId) =>
        w.AirMissions.Where(m => m.CountryId == countryId && m.RegionId == regionId
                                 && w.AirMissionDefs.TryGetValue(m.MissionId, out var d) && d.Effect == "superiority")
            .Sum(m => m.Wings * w.AirMissionDefs[m.MissionId].Value);

    /// <summary>Bónus de apoio próximo à força de quem combate nesta região (tecto air_support_max).</summary>
    public static float Support(World w, int regionId, int countryId)
    {
        float sum = w.AirMissions.Where(m => m.CountryId == countryId && m.RegionId == regionId
                                             && w.AirMissionDefs.TryGetValue(m.MissionId, out var d) && d.Effect == "support")
            .Sum(m => m.Wings * w.AirMissionDefs[m.MissionId].Value);
        return MathF.Min(sum, w.Rule("air_support_max", 0.35f));
    }

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
        // alcance: o céu tem de estar à vista de terra nossa
        if (!mine && !r.Neighbours.Any(n => w.Regions.TryGetValue(n, out var nb) && nb.ControllerId == countryId))
            return "fora do alcance dos nossos campos";
        if (c.Money < wings * w.Rule("air_mission_upkeep", 0.6f)) return "cofre curto para a estadia do dia";
        return null;
    }
}
