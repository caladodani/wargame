using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Experiência de campanha dos comandantes. Um general no estado-maior é um número que se
/// compra; um general que faz a guerra ganha o posto no terreno.
///
/// Em terra é ao acontecimento: cada batalha que o grupo dele trava dá-lhe general_xp_battle, a que ganha
/// dá general_xp_win a mais e cada região tomada pelas divisões dele dá general_xp_capture.
///
/// No ar e no mar não há grupos de exércitos onde destacar ninguém — o comandante de asa e o de esquadra
/// valem para o país inteiro. A carreira deles conta-se ao dia e pelo que está destacado: cada asa no céu
/// dá general_xp_air_day e cada navio no mar dá general_xp_sea_day a quem manda naquela arma. Uma guerra
/// aérea grande promove depressa; um estado-maior de asa comprado e deixado em casa não sobe nunca.
///
/// Passados os limiares da escada da ARMA dele (tabela general_rank, uma escada por domain) sobe de posto
/// e o bónus cresce (World.RankBonus), o que dá continuidade à guerra: vale a pena deixar o mesmo homem
/// à frente da mesma coisa em vez de andar a trocar.
///
/// Não decide nada por si — só ouve os eventos do combate e olha para as missões do dia. A experiência
/// nunca desce e está limitada a general_xp_max, para o posto de topo não ficar a acumular sem fim.</summary>
public sealed class GeneralXpSystem : ISystem
{
    public string Name => "GeneralXp";

    private World? _bound;

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) Bind(w);
        Missions(w);
    }

    /// <summary>A carreira do ar e do mar: quem manda na arma ganha o dia que a arma andou fora. Conta-se
    /// o que está destacado (asas no céu, navios no mar), não o que está comprado — a frota no porto não
    /// faz almirantes. Um comandante no hospital não conta o dia.</summary>
    private static void Missions(World w)
    {
        float air = w.Rule("general_xp_air_day", 0.15f), sea = w.Rule("general_xp_sea_day", 0.15f);
        foreach (var c in w.Countries.Values.OrderBy(x => x.Id))
        {
            if (c.Capitulated || c.Generals.Count == 0) continue;
            if (air > 0f && w.AirMissions.Count > 0)
                Serve(w, c, World.Air, air * w.AirMissions.Where(m => m.CountryId == c.Id).Sum(m => m.Wings));
            if (sea > 0f && w.NavalMissions.Count > 0)
                Serve(w, c, World.Sea, sea * w.NavalMissions.Where(m => m.CountryId == c.Id).Sum(m => m.Ships));
        }
    }

    /// <summary>Dá o dia a todos os comandantes desta arma ao serviço deste país.</summary>
    private static void Serve(World w, Country c, string domain, float xp)
    {
        if (xp <= 0f) return;
        foreach (string id in c.Generals.Where(id => w.DomainOfGeneral(id) == domain
                                                     && !w.IsWounded(c.Id, id)).OrderBy(x => x).ToList())
            Add(w, c, id, xp);
    }

    /// <summary>Liga-se ao barramento do mundo. Idempotente por mundo: um save carregado traz um World
    /// novo e volta a subscrever; o mesmo mundo nunca subscreve duas vezes.</summary>
    public void Bind(World w)
    {
        _bound = w;
        w.Events.Subscribe<BattleEnded>(e => OnBattle(w, e));
        w.Events.Subscribe<RegionCaptured>(e => Award(w, e.NewController, e.RegionId, w.Rule("general_xp_capture", 4f)));
    }

    private static void OnBattle(World w, BattleEnded e)
    {
        float fought = w.Rule("general_xp_battle", 2f), won = w.Rule("general_xp_win", 3f);
        int winner = e.AttackerWon ? e.AttackerCountryId : e.DefenderCountryId;
        Award(w, e.AttackerCountryId, e.RegionId, fought + (e.AttackerCountryId == winner ? won : 0f));
        Award(w, e.DefenderCountryId, e.RegionId, fought + (e.DefenderCountryId == winner ? won : 0f));
    }

    /// <summary>Dá experiência aos comandantes dos grupos que tinham divisões deste país naquela região.
    /// Um grupo conta uma vez por acontecimento, por muitas divisões que lá tenha: o mérito é do
    /// comandante, não da contagem de unidades.</summary>
    private static void Award(World w, int countryId, int regionId, float xp)
    {
        if (xp <= 0f || w.ArmyGroups.Count == 0) return;
        if (!w.Regions.TryGetValue(regionId, out var reg) || !w.Countries.TryGetValue(countryId, out var c)) return;

        var done = new HashSet<int>();
        foreach (int id in reg.DivisionIds)
        {
            if (!w.Divisions.TryGetValue(id, out var d) || d.CountryId != countryId || d.GroupId is not int gid) continue;
            if (!done.Add(gid) || !w.ArmyGroups.TryGetValue(gid, out var g) || g.GeneralId is not string gen) continue;
            Add(w, c, gen, xp);
        }
    }

    private static void Add(World w, Country c, string generalId, float xp)
    {
        var before = w.RankOf(c.Id, generalId);
        c.GeneralXp[generalId] = MathF.Min(w.Rule("general_xp_max", 400f), c.GeneralXp.GetValueOrDefault(generalId) + xp);
        var after = w.RankOf(c.Id, generalId);
        if (after is not null && (before is null || after.Level > before.Level))
            w.Events.Publish(new GeneralPromoted(c.Id, generalId, after.Level, after.Name));
    }
}
