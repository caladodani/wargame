using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Estabilidade (HoI4: stability + war support num só número, 0..100, 50 = neutro).
/// Cada dia anda rule stability_speed em direcção a um alvo situacional:
/// 50 − guerras activas (máx 2) × stability_war_penalty − fracção da população própria
/// ocupada por inimigos × stability_occupied_penalty. O efeito lê-se em
/// Country.StabilityFactor (0.5..1.5): multiplica o rendimento (EconomySystem) e o
/// recrutamento (ManpowerSystem).
/// </summary>
public sealed class StabilitySystem : ISystem
{
    public string Name => "Stability";

    /// <summary>Fracção da população PRÓPRIA deste país que o inimigo tem ocupada. É a mesma passagem que o
    /// Tick faz, para um país só: a UI precisa dela para dizer porque é que a estabilidade está onde está.</summary>
    public static float OccupiedShare(World w, int countryId)
    {
        long total = 0, taken = 0;
        foreach (var r in w.Regions.Values)
        {
            if (r.OwnerId != countryId) continue;
            total += r.Population;
            if (r.ControllerId != r.OwnerId && w.AreAtWar(r.OwnerId, r.ControllerId)) taken += r.Population;
        }
        return total > 0 ? (float)taken / total : 0f;
    }

    /// <summary>Para onde a estabilidade deste país está a andar: 50, menos as guerras (até duas), menos a
    /// terra ocupada, menos o desgaste de guerra.</summary>
    public static float Target(World w, Country c, float occupiedShare) =>
        Math.Clamp(50f - MathF.Min(2, c.AtWarWith.Count) * w.Rule("stability_war_penalty", 10f)
                       - occupiedShare * w.Rule("stability_occupied_penalty", 40f) - c.WarExhaustion, 0f, 100f);

    public void Tick(World w)
    {
        float speed = w.Rule("stability_speed", 0.5f);

        // População própria total e ocupada, uma passagem pelas regiões.
        var total = new Dictionary<int, long>();
        var occupied = new Dictionary<int, long>();
        foreach (var r in w.Regions.Values)
        {
            total[r.OwnerId] = total.GetValueOrDefault(r.OwnerId) + r.Population;
            if (r.ControllerId != r.OwnerId && w.AreAtWar(r.OwnerId, r.ControllerId))
                occupied[r.OwnerId] = occupied.GetValueOrDefault(r.OwnerId) + r.Population;
        }

        foreach (var c in w.Countries.Values)
        {
            if (c.Capitulated) continue;
            var tot = total.GetValueOrDefault(c.Id);
            float occFrac = tot > 0 ? (float)occupied.GetValueOrDefault(c.Id) / tot : 0f;
            if (c.AtWarWith.Count == 0 && c.WarExhaustion > 0f)
                c.WarExhaustion = MathF.Max(0f, c.WarExhaustion - w.Rule("exhaustion_decay", 0.1f));
            float target = Target(w, c, occFrac);
            c.Stability = c.Stability < target
                ? MathF.Min(target, c.Stability + speed)
                : MathF.Max(target, c.Stability - speed);
        }
    }
}
