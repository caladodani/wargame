using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Capitulação (estilo HoI4): um país em guerra capitula quando os inimigos controlam
/// uma fracção suficiente do que ele é — gente e praças de pontos de vitória, na dose da
/// regra capitulate_weight_vp (Capitulation) — ou quando já não é dono de região nenhuma.
/// A fracção é capitulate_share, e capitulate_share_capital quando lhe tomaram a capital.
/// Capitular = as regiões que ainda controlava passam para o inimigo que mais
/// população dele ocupa, o resto fica de quem já lá está (owner=controller), o exército
/// dissolve-se e todas as guerras dele acabam. Números vêm de linhas SQLite, nunca daqui.
/// </summary>
public sealed class PeaceSystem : ISystem
{
    public string Name => "Peace";

    public void Tick(World w)
    {
        // A conta é toda do Capitulation: quanto do país está tomado (gente e praças, na dose de
        // capitulate_weight_vp) contra a fracção a que ele cai hoje. Aqui só se decide quem cai.
        foreach (var c in w.Countries.Values.ToList())
            if (Capitulation.Falls(w, c)) Capitulate(w, c);
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
