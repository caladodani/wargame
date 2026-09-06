using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Experiência de campanha dos comandantes destacados. Um general no estado-maior é um número
/// que se compra; um general à frente de um grupo de exércitos ganha o posto no terreno — cada batalha
/// que o grupo trava dá-lhe general_xp_battle, a que ganha dá general_xp_win a mais e cada região tomada
/// pelas divisões dele dá general_xp_capture. Passados os limiares da tabela general_rank sobe de posto e
/// o bónus que traz ao grupo cresce (World.RankBonus), o que dá continuidade à guerra: vale a pena
/// deixar o mesmo homem à frente do mesmo exército em vez de andar a trocar.
///
/// Não decide nada por si — só ouve os eventos do combate. A experiência nunca desce e está limitada a
/// general_xp_max, para o posto de topo não ficar a acumular sem fim.</summary>
public sealed class GeneralXpSystem : ISystem
{
    public string Name => "GeneralXp";

    private World? _bound;

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) Bind(w);
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
