using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Capitulação (estilo HoI4): um país em guerra capitula quando os inimigos controlam
/// uma fracção suficiente da população das suas regiões (rule capitulate_share; com a
/// capital perdida basta capitulate_share_capital) ou quando já não é dono de região
/// nenhuma. Capitular = as regiões que ainda controlava passam para o inimigo que mais
/// população dele ocupa, o resto fica de quem já lá está (owner=controller), o exército
/// dissolve-se e todas as guerras dele acabam. Números vêm de linhas SQLite, nunca daqui.
/// </summary>
public sealed class PeaceSystem : ISystem
{
    public string Name => "Peace";

    public void Tick(World w)
    {
        var share = w.Rule("capitulate_share", 0.75f);
        var shareCapital = w.Rule("capitulate_share_capital", 0.5f);

        foreach (var c in w.Countries.Values.ToList())
        {
            if (c.Capitulated || c.AtWarWith.Count == 0) continue;

            long total = 0, lost = 0;
            var owned = 0;
            var capitalLost = false;
            foreach (var r in w.Regions.Values)
            {
                if (r.OwnerId != c.Id) continue;
                owned++;
                total += r.Population;
                if (r.ControllerId != c.Id && c.AtWarWith.Contains(r.ControllerId))
                {
                    lost += r.Population;
                    if (r.Id == c.CapitalRegionId) capitalLost = true;
                }
            }

            var frac = total > 0 ? (float)lost / total : 0f;
            if (owned == 0 || frac >= share || (capitalLost && frac >= shareCapital))
                Capitulate(w, c);
        }
    }

    private static void Capitulate(World w, Country c)
    {
        // Vencedor: o inimigo que controla mais população das regiões do capitulado.
        var popByEnemy = new Dictionary<int, long>();
        foreach (var r in w.Regions.Values)
            if (r.OwnerId == c.Id && r.ControllerId != c.Id && c.AtWarWith.Contains(r.ControllerId))
                popByEnemy[r.ControllerId] = popByEnemy.GetValueOrDefault(r.ControllerId) + r.Population;
        var winner = popByEnemy.Count > 0
            ? popByEnemy.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key
            : c.AtWarWith.Min();

        // Conferência de paz: quem fez a guerra reparte a terra por pontos de espólio (PeaceSpoils).
        // Corre antes da regra velha e à frente dela: só o que ninguém reclamar é que cai lá abaixo.
        foreach (var g in PeaceSpoils.Divide(w, c).GroupBy(x => x.WinnerId).OrderBy(g => g.Key))
            w.Events.Publish(new SpoilsTaken(g.Key, c.Id, g.Count(), g.Sum(x => x.Cost)));

        // Regiões: o que ele ainda controlava vai para o vencedor; o resto fica de quem ocupa.
        foreach (var r in w.Regions.Values)
        {
            if (r.OwnerId != c.Id) continue;
            if (r.ControllerId == c.Id) { r.OwnerId = winner; r.ControllerId = winner; }
            else r.OwnerId = r.ControllerId;
        }

        // Exército dissolve-se (RemoveDivision também o tira das batalhas).
        foreach (var d in w.Divisions.Values.Where(d => d.CountryId == c.Id).ToList())
        {
            w.Events.Publish(new DivisionDestroyed(d.Id));
            w.RemoveDivision(d.Id);
        }
        w.ActiveBattles.RemoveAll(b => b.AttackerCountryId == c.Id || b.Attackers.Count == 0 || b.Defenders.Count == 0);

        // Guerras acabam todas.
        foreach (var enemy in c.AtWarWith.ToList())
        {
            w.EndWar(c.Id, enemy);
            w.Events.Publish(new WarEnded(Math.Min(c.Id, enemy), Math.Max(c.Id, enemy)));
        }
        c.AtWarWith.Clear();

        c.Capitulated = true;
        c.CapitulatedDay = w.Clock.Day;
        w.Events.Publish(new CountryCapitulated(c.Id, winner));
    }
}
