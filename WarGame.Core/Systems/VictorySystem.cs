using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Domínio mundial: quem controla ≥ rule victory_pop_share da população do mundo domina o
/// mundo — WorldDominated uma única vez (a UI faz o ecrã de vitória/derrota). Verificado de
/// victory_check_days em victory_check_days para não somar 3000 regiões todos os dias.
/// </summary>
public sealed class VictorySystem : ISystem
{
    public string Name => "Victory";

    private bool _fired;

    public void Tick(World w)
    {
        if (_fired) return;
        int period = Math.Max(1, (int)w.Rule("victory_check_days", 7f));
        if (w.Clock.Day == 0 || w.Clock.Day % period != 0) return;

        float share = w.Rule("victory_pop_share", 0.6f);
        long total = 0;
        var byController = new Dictionary<int, long>();
        foreach (var r in w.Regions.Values)
        {
            total += r.Population;
            byController[r.ControllerId] = byController.GetValueOrDefault(r.ControllerId) + r.Population;
        }
        if (total == 0) return;

        foreach (var (id, pop) in byController)
            if ((float)pop / total >= share && w.Countries.TryGetValue(id, out var c) && !c.Capitulated)
            {
                _fired = true;
                w.Events.Publish(new WorldDominated(id));
                return;
            }
    }
}
