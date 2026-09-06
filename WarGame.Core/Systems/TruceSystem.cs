using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Paz branca por estagnação: uma guerra sem captura de região entre os dois durante
/// rule war_white_peace_days fecha-se sozinha em uti possidetis — cada um anexa o que
/// controla do outro (owner = controller) e a guerra acaba (WhitePeaceSigned + WarEnded).
/// Batalhas entre os dois são limpas; divisões encalhadas em território que deixou de
/// ser hostil recuam pelo MovementSystem normal (região anexada já é do dono novo).
/// </summary>
public sealed class TruceSystem : ISystem
{
    public string Name => "Truce";

    public void Tick(World w)
    {
        float staleDays = w.Rule("war_white_peace_days", 240f);
        List<(int A, int B)>? stale = null;
        foreach (var (key, info) in w.Wars)
            if (w.Clock.Day - Math.Max(info.StartDay, info.LastProgressDay) >= staleDays)
                (stale ??= new()).Add(key);
        if (stale is null) return;

        foreach (var (a, b) in stale)
        {
            if (!w.AreAtWar(a, b)) { w.Wars.Remove((a, b)); continue; }

            // Uti possidetis: o que cada um controla do outro passa a ser dele.
            foreach (var r in w.Regions.Values)
            {
                if (r.OwnerId == a && r.ControllerId == b) r.OwnerId = b;
                else if (r.OwnerId == b && r.ControllerId == a) r.OwnerId = a;
            }

            // Batalhas entre os dois morrem (lados por país do atacante vs. controlador da região).
            w.ActiveBattles.RemoveAll(bt =>
                (bt.AttackerCountryId == a || bt.AttackerCountryId == b)
                && !bt.Attackers.Concat(bt.Defenders).Any(id =>
                    w.Divisions.TryGetValue(id, out var d) && d.CountryId != a && d.CountryId != b));

            w.EndWar(a, b);
            w.Events.Publish(new WhitePeaceSigned(a, b));
            w.Events.Publish(new WarEnded(a, b));
        }
    }
}
