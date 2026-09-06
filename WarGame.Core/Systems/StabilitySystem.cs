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

    public void Tick(World w)
    {
        float speed = w.Rule("stability_speed", 0.5f);
        float warPen = w.Rule("stability_war_penalty", 10f);
        float occPen = w.Rule("stability_occupied_penalty", 40f);

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
            float target = Math.Clamp(50f - MathF.Min(2, c.AtWarWith.Count) * warPen - occFrac * occPen - c.WarExhaustion, 0f, 100f);
            c.Stability = c.Stability < target
                ? MathF.Min(target, c.Stability + speed)
                : MathF.Max(target, c.Stability - speed);
        }
    }
}
