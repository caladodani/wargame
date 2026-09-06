using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Conta os dias das operações de espionagem (World.ActiveSpyOps) e aplica o efeito
/// da tabela spy_op ao concluir: steal_money (fracção do tesouro do alvo passa ao autor),
/// sabotage_production (fracção do progresso das encomendas do alvo perde-se),
/// stability_hit (estabilidade do alvo cai `magnitude` pontos), intel (vês os detalhes do alvo `magnitude` dias). Autor ou alvo capitulado = operação morre.</summary>
public sealed class EspionageSystem : ISystem
{
    public string Name => "Espionage";

    public void Tick(World w)
    {
        // snapshot: Apply pode remover outras operações da lista (purge_spies) — uma operação
        // entretanto removida já não conta nem aplica efeito
        foreach (var o in w.ActiveSpyOps.ToList())
        {
            if (!w.ActiveSpyOps.Contains(o)) continue;
            if (!w.Countries.TryGetValue(o.CountryId, out var c) || c.Capitulated
                || !w.Countries.TryGetValue(o.TargetCountryId, out var t) || t.Capitulated)
            { w.ActiveSpyOps.Remove(o); continue; }
            o.DaysLeft -= 1f;
            if (o.DaysLeft > 0f) continue;
            w.ActiveSpyOps.Remove(o);
            if (w.SpyOps.TryGetValue(o.OpId, out var op)) Apply(w, op, c, t);
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
            case "intel":
                w.Intel[(actor.Id, target.Id)] = w.Clock.Day + (int)op.Magnitude;
                break;
            case "research_boost":
                if (actor.ResearchTech is not null) actor.ResearchProgress += op.Magnitude;
                break;
            case "purge_spies":
                // contra-espionagem: expulsa todas as redes do alvo contra nós
                w.ActiveSpyOps.RemoveAll(o => o.CountryId == target.Id && o.TargetCountryId == actor.Id);
                break;
        }
    }
}
