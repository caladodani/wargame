using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Conta os dias das operações de espionagem (World.ActiveSpyOps) e aplica o efeito
/// da tabela spy_op ao concluir: steal_money (fracção do tesouro do alvo passa ao autor),
/// sabotage_production (fracção do progresso das encomendas do alvo perde-se),
/// stability_hit (estabilidade do alvo cai `magnitude` pontos). Autor ou alvo capitulado = operação morre.</summary>
public sealed class EspionageSystem : ISystem
{
    public string Name => "Espionage";

    public void Tick(World w)
    {
        for (int i = w.ActiveSpyOps.Count - 1; i >= 0; i--)
        {
            var o = w.ActiveSpyOps[i];
            if (!w.Countries.TryGetValue(o.CountryId, out var c) || c.Capitulated
                || !w.Countries.TryGetValue(o.TargetCountryId, out var t) || t.Capitulated)
            { w.ActiveSpyOps.RemoveAt(i); continue; }
            o.DaysLeft -= 1f;
            if (o.DaysLeft > 0f) continue;
            if (w.SpyOps.TryGetValue(o.OpId, out var op)) Apply(w, op, c, t);
            w.ActiveSpyOps.RemoveAt(i);
            w.Events.Publish(new SpyOpCompleted(o.CountryId, o.TargetCountryId, o.OpId));
        }
    }

    private static void Apply(World w, SpyOp op, Country actor, Country target)
    {
        switch (op.Effect)
        {
            case "steal_money":
                float loot = target.Money * op.Magnitude;
                target.Money -= loot; actor.Money += loot;
                break;
            case "sabotage_production":
                foreach (var order in target.Queue) order.Progress *= 1f - op.Magnitude;
                break;
            case "stability_hit":
                target.Stability = System.MathF.Max(0f, target.Stability - op.Magnitude);
                break;
        }
    }
}
