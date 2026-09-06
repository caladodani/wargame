using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Condecora as divisões que passam os limiares da tabela medal (XP, batalhas travadas, regiões
/// tomadas). Antes disto a veterania era um número escondido no painel; agora uma divisão que aguentou dez
/// batalhas traz isso no nome e vale mais em campo — cada medalha soma Bonus à força, até medal_bonus_max.
/// Só acrescenta: uma condecoração nunca se perde, nem quando a divisão é reforçada.</summary>
public sealed class MedalSystem : ISystem
{
    public string Name => "Medals";

    public void Tick(World w)
    {
        if (w.MedalDefs.Count == 0) return;
        int period = Math.Max(1, (int)w.Rule("medal_check_days", 2f));
        if (w.Clock.Day % period != 0) return;

        foreach (var d in w.Divisions.Values)
            foreach (var m in w.MedalDefs.Values)
            {
                if (d.Medals.Contains(m.Id) || Metric(d, m.Metric) < m.Threshold) continue;
                d.Medals.Add(m.Id);
                w.Events.Publish(new MedalAwarded(d.Id, d.CountryId, m.Id));
            }
    }

    private static float Metric(Division d, string metric) => metric switch
    {
        "xp" => d.Xp,
        "battles" => d.Battles,
        "captures" => d.Captures,
        _ => float.MinValue,      // métrica desconhecida na base de dados: nunca condecora
    };

    /// <summary>Bónus de força somado das condecorações da divisão, cortado em medal_bonus_max.</summary>
    public static float Bonus(World w, Division d)
    {
        if (d.Medals.Count == 0) return 0f;
        float sum = 0f;
        foreach (var id in d.Medals)
            if (w.MedalDefs.TryGetValue(id, out var m)) sum += m.Bonus;
        return MathF.Min(sum, w.Rule("medal_bonus_max", 0.12f));
    }
}
