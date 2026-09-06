using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Decisões nacionais activas: expira as vencidas (DecisionExpired) e recalcula todos os dias
/// os multiplicadores por país a partir das que restam.</summary>
public sealed class DecisionSystem : ISystem
{
    public string Name => "Decision";

    public void Tick(World w)
    {
        for (int i = w.ActiveDecisions.Count - 1; i >= 0; i--)
        {
            var a = w.ActiveDecisions[i];
            if (a.UntilDay >= w.Clock.Day) continue;
            w.ActiveDecisions.RemoveAt(i);
            w.Events.Publish(new DecisionExpired(a.CountryId, a.DecisionId));
        }
        Recompute(w);
    }

    /// <summary>Refaz DecisionMult de todos os países (também chamado ao activar, para efeito imediato).</summary>
    public static void Recompute(World w)
    {
        foreach (var c in w.Countries.Values) c.DecisionMult.Clear();
        foreach (var a in w.ActiveDecisions)
            if (w.Countries.TryGetValue(a.CountryId, out var c) && w.DecisionDefs.TryGetValue(a.DecisionId, out var def))
                c.DecisionMult[def.StatKey] = c.DecisionMult.GetValueOrDefault(def.StatKey, 1f) * def.Mult;
    }
}
