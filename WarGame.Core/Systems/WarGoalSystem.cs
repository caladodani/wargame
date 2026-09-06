using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Dá a cada guerra um objectivo concreto: que regiões do inimigo cada lado quer ficar a controlar.
/// Antes disto uma guerra não tinha fim natural — a IA batia-se até alguém capitular. Com um objectivo,
/// sabe-se para onde empurrar (o AiSystem ataca primeiro o que está na lista) e quando parar.
///
/// A escolha é feita uma vez por lado, no primeiro dia em que há candidatas:
/// 1) terras irredentas — regiões que nos pertencem no papel (OwnerId) e o inimigo controla;
/// 2) senão, as regiões inimigas encostadas a nós, as mais povoadas primeiro;
/// 3) mais a capital inimiga, se formos war_goal_capital_ratio vezes mais fortes.
/// Nunca mais muda: um objectivo que se mexe com a frente não é objectivo nenhum.</summary>
public sealed class WarGoalSystem : ISystem
{
    public string Name => "WarGoals";

    /// <summary>Objectivos já anunciados como cumpridos, para o evento sair uma vez só.</summary>
    private readonly HashSet<(int Country, int Target)> _announced = new();

    public void Tick(World w)
    {
        int period = Math.Max(1, (int)w.Rule("war_goal_period_days", 3f));
        if (w.Clock.Day % period != 0) return;

        foreach (var war in w.Wars.Values.ToList())
        {
            Assign(w, war, war.A, war.B);
            Assign(w, war, war.B, war.A);
            Check(w, war, war.A, war.B);
            Check(w, war, war.B, war.A);
        }
        // Guerras que acabaram deixam de contar para o anúncio (a seguinte volta a poder anunciar).
        _announced.RemoveWhere(k => !w.AreAtWar(k.Country, k.Target));
    }

    private static void Assign(World w, WarInfo war, int countryId, int enemyId)
    {
        var side = war.Side(countryId);
        if (side.Goals.Count > 0) return;
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return;
        var goals = Pick(w, countryId, enemyId);
        if (goals.Count == 0) return;
        foreach (int id in goals) side.Goals.Add(id);
        w.Events.Publish(new WarGoalDeclared(countryId, enemyId, goals));
    }

    /// <summary>As regiões que `countryId` vai exigir a `enemyId`, por ordem de prioridade.</summary>
    public static List<int> Pick(World w, int countryId, int enemyId)
    {
        int max = Math.Max(1, (int)w.Rule("war_goal_max", 4f));
        var mineNow = w.Regions.Values.Where(r => r.ControllerId == countryId).ToList();
        var irredenta = w.Regions.Values
            .Where(r => r.OwnerId == countryId && r.ControllerId == enemyId)
            .OrderByDescending(r => r.Population).Select(r => r.Id).ToList();

        var goals = irredenta.Take(max).ToList();
        if (goals.Count < max)
        {
            // fronteira: regiões do inimigo encostadas ao que controlamos, por terra ou por mar
            var border = new HashSet<int>();
            foreach (var r in mineNow)
                foreach (int n in r.Neighbours.Concat(r.SeaNeighbours.Keys))
                    if (w.Regions.TryGetValue(n, out var nr) && nr.ControllerId == enemyId && nr.OwnerId == enemyId)
                        border.Add(n);
            foreach (int id in border.OrderByDescending(id => w.Regions[id].Population).ThenBy(id => id))
            {
                if (goals.Count >= max) break;
                if (!goals.Contains(id)) goals.Add(id);
            }
        }

        // A capital só entra quando há força para a ir buscar; é o objectivo que decide a guerra.
        if (w.Countries.TryGetValue(enemyId, out var e) && w.Regions.ContainsKey(e.CapitalRegionId)
            && w.Regions[e.CapitalRegionId].ControllerId == enemyId && !goals.Contains(e.CapitalRegionId))
        {
            float mine = w.Divisions.Values.Count(d => d.CountryId == countryId);
            float theirs = w.Divisions.Values.Count(d => d.CountryId == enemyId);
            if (mine >= MathF.Max(1f, theirs) * w.Rule("war_goal_capital_ratio", 2f)) goals.Add(e.CapitalRegionId);
        }
        return goals;
    }

    /// <summary>Objectivo cumprido: todas as regiões pedidas estão sob o nosso controlo.</summary>
    public static bool Met(World w, WarInfo war, int countryId)
    {
        var goals = war.Side(countryId).Goals;
        return goals.Count > 0 && goals.All(id => w.Regions.TryGetValue(id, out var r) && r.ControllerId == countryId);
    }

    private void Check(World w, WarInfo war, int countryId, int enemyId)
    {
        if (!Met(w, war, countryId)) { _announced.Remove((countryId, enemyId)); return; }
        if (!_announced.Add((countryId, enemyId))) return;
        w.Events.Publish(new WarGoalAchieved(countryId, enemyId));
    }
}
