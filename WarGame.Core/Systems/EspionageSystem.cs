using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Conta os dias das operações de espionagem (World.ActiveSpyOps) e aplica o efeito
/// da tabela spy_op ao concluir: steal_money (fracção do tesouro do alvo passa ao autor),
/// sabotage_production (fracção do progresso das encomendas do alvo perde-se),
/// stability_hit (estabilidade do alvo cai `magnitude` pontos), intel (vês os detalhes do alvo `magnitude` dias). Autor ou alvo capitulado = operação morre.
///
/// As operações de scope 'region' (sabotagem na retaguarda) não tocam no país: caem numa região que ele
/// controla e estragam o que lá está — o cais (sabotage_port), as vias (sabotage_infra) ou as casamatas
/// (sabotage_fort). Se a região mudou de mãos entretanto, a equipa chegou tarde e a operação morre sem
/// efeito: sabota-se a retaguarda do inimigo, não a nossa.</summary>
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
            if (w.SpyOps.TryGetValue(o.OpId, out var op)) Apply(w, op, c, t, o.RegionId);
            w.Events.Publish(new SpyOpCompleted(o.CountryId, o.TargetCountryId, o.OpId));
        }
    }

    private static void Apply(World w, SpyOp op, Country actor, Country target, int regionId)
    {
        if (op.IsRegional) { Sabotage(w, op, actor, target, regionId); return; }
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
            case "desertion":
                // fomenta deserção: fracção das divisões do alvo (as de menor org) dissolve-se
                int nDes = (int)MathF.Ceiling(w.Divisions.Values.Count(d => d.CountryId == target.Id) * op.Magnitude);
                foreach (var d in w.Divisions.Values.Where(d => d.CountryId == target.Id)
                             .OrderBy(d => d.Org).Take(nDes).ToList())
                    w.RemoveDivision(d.Id);
                break;
            case "purge_spies":
                // contra-espionagem: expulsa todas as redes do alvo contra nós
                w.ActiveSpyOps.RemoveAll(o => o.CountryId == target.Id && o.TargetCountryId == actor.Id);
                break;
        }
    }

    /// <summary>Sabotagem na retaguarda: o estrago cai numa região que o alvo ainda controla. O que se
    /// estraga vem do efeito da tabela, a dose de magnitude — nada disto está escrito aqui em números.</summary>
    private static void Sabotage(World w, SpyOp op, Country actor, Country target, int regionId)
    {
        if (!w.Regions.TryGetValue(regionId, out var r) || r.ControllerId != target.Id) return;   // chegou tarde
        string damage = "";
        switch (op.Effect)
        {
            case "sabotage_port":
                // o cais vai abaixo um nível por magnitude; sem porto nenhum não há o que rebentar
                var quay = r.Buildings.Keys.FirstOrDefault(b => w.BuildingDefs.TryGetValue(b, out var d) && d.SupplyRange > 0f);
                if (quay is null) return;
                int left = r.Buildings[quay] - (int)MathF.Max(1f, op.Magnitude);
                if (left > 0) r.Buildings[quay] = left; else r.Buildings.Remove(quay);
                damage = $"o porto de {r.Name} vai abaixo";
                break;
            case "sabotage_infra":
                float before = r.Infrastructure;
                r.Infrastructure = MathF.Max(0.1f, r.Infrastructure * (1f - op.Magnitude));
                if (before - r.Infrastructure < 0.001f) return;
                damage = $"as vias de {r.Name} ficam em ×{r.Infrastructure:0.00}";
                break;
            case "sabotage_fort":
                if (r.Fort <= 0) return;
                r.Fort = Math.Max(0, r.Fort - (int)MathF.Max(1f, op.Magnitude));
                damage = $"as defesas de {r.Name} caem para {r.Fort}";
                break;
            default: return;
        }
        w.Events.Publish(new RegionSabotaged(actor.Id, target.Id, op.Id, r.Id, damage));
    }
}
