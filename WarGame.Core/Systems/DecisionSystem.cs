using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Decisões nacionais a correr: resolve as MISSÕES (cumpridas mal a meta lá chegue, falhadas no
/// dia do prazo), expira as que acabaram e refaz todos os dias os multiplicadores por país.
///
/// A missão resolve-se ANTES de se expirar o efeito: uma missão cumprida no último dia ainda paga prémio.
/// Quem lê o que a missão pede e como vai é o Decisions (estado derivado); aqui só se decide e se paga.</summary>
public sealed class DecisionSystem : ISystem
{
    public string Name => "Decision";

    public void Tick(World w)
    {
        for (int i = w.ActiveDecisions.Count - 1; i >= 0; i--)
        {
            var a = w.ActiveDecisions[i];
            if (a.MissionUntil < 0) continue;
            if (!w.DecisionDefs.TryGetValue(a.DecisionId, out var def) || !def.IsMission) { a.MissionUntil = -1; continue; }
            if (!w.Countries.TryGetValue(a.CountryId, out var c)) continue;

            bool met = Decisions.GoalMet(w, c, def, a);
            if (!met && w.Clock.Day < a.MissionUntil) continue;   // ainda há prazo

            if (met)
            {
                c.Political += def.RewardPolitical;
                c.Stability = Math.Clamp(c.Stability + def.RewardStability, 0f, 100f);
            }
            else
            {
                c.Political = MathF.Max(0f, c.Political - def.FailPolitical);
                c.Stability = Math.Clamp(c.Stability - def.FailStability, 0f, 100f);
            }
            a.MissionUntil = -1;   // resolvida: o efeito ainda corre até UntilDay, mas o prazo acabou
            w.Events.Publish(new DecisionMissionEnded(a.CountryId, a.DecisionId, met));
        }

        for (int i = w.ActiveDecisions.Count - 1; i >= 0; i--)
        {
            var a = w.ActiveDecisions[i];
            if (a.UntilDay >= w.Clock.Day) continue;
            w.ActiveDecisions.RemoveAt(i);
            w.Events.Publish(new DecisionExpired(a.CountryId, a.DecisionId));
        }
        Recompute(w);
    }

    /// <summary>Refaz DecisionMult de todos os países (também chamado ao activar, para efeito imediato).
    /// Uma decisão pode multiplicar várias características ao mesmo tempo — são as linhas da
    /// decision_effect, e duas decisões a mexer na mesma característica multiplicam-se uma à outra.</summary>
    public static void Recompute(World w)
    {
        foreach (var c in w.Countries.Values) c.DecisionMult.Clear();
        foreach (var a in w.ActiveDecisions)
        {
            if (!w.Countries.TryGetValue(a.CountryId, out var c)) continue;
            foreach (var (key, mult) in Decisions.Effects(w, a.DecisionId))
                c.DecisionMult[key] = c.DecisionMult.GetValueOrDefault(key, 1f) * mult;
        }
    }
}
