using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Avaliação de uma paz negociada: quanto o derrotado já está pressionado contra o que lhe é
/// exigido. A pressão soma a fatia do país dele que está ocupada, a diferença de divisões, a exaustão de
/// guerra e a capital perdida; o preço é a fatia do país que as regiões exigidas representam, mais barata
/// quando já estão ocupadas por quem exige. Aceita quando a pressão paga o preço.</summary>
public static class PeaceTerms
{
    /// <summary>Resultado da avaliação: se aceita, e os dois números que a UI mostra ao jogador.</summary>
    public readonly record struct Verdict(bool Accepted, float Pressure, float Price);

    public static Verdict Evaluate(World w, int demanderId, int targetId, IReadOnlyCollection<int> regionIds)
    {
        if (!w.Countries.TryGetValue(demanderId, out var c) || !w.Countries.TryGetValue(targetId, out var t))
            return new Verdict(false, 0f, float.MaxValue);

        var theirs = w.Regions.Values.Where(r => r.OwnerId == targetId).ToList();
        if (theirs.Count == 0) return new Verdict(true, 1f, 0f);   // já não tem país que defender

        float occupiedShare = (float)theirs.Count(r => r.ControllerId == demanderId) / theirs.Count;
        int myDivs = w.Divisions.Values.Count(d => d.CountryId == demanderId);
        int theirDivs = w.Divisions.Values.Count(d => d.CountryId == targetId);
        float strength = theirDivs == 0 ? 1f : MathF.Min(1f, MathF.Max(0f, (float)myDivs / theirDivs - 1f));
        float exhaustion = t.WarExhaustion / MathF.Max(1f, w.Rule("exhaustion_max", 30f));
        bool capitalHeld = w.Regions.TryGetValue(t.CapitalRegionId, out var cap) && cap.ControllerId == demanderId;

        float pressure = occupiedShare * w.Rule("peace_weight_occupied", 1f)
                       + strength * w.Rule("peace_weight_strength", 0.3f)
                       + exhaustion * w.Rule("peace_weight_exhaustion", 0.3f)
                       + (capitalHeld ? w.Rule("peace_weight_capital", 0.3f) : 0f);

        float held = w.Rule("peace_price_held", 0.6f), free = w.Rule("peace_price_free", 1.4f), cost = 0f;
        foreach (var id in regionIds)
        {
            if (!w.Regions.TryGetValue(id, out var r) || r.OwnerId != targetId) continue;
            cost += r.ControllerId == demanderId ? held : free;
        }
        float price = cost / theirs.Count * w.Rule("peace_demand_greed", 1f);
        return new Verdict(pressure >= price, pressure, price);
    }

    /// <summary>Fecha a guerra com as regiões exigidas a mudar de dono (e de controlador) para quem
    /// exigiu; o resto volta ao dono de direito. Batalhas entre os dois morrem, PeaceSigned + WarEnded.</summary>
    public static void Sign(World w, int demanderId, int targetId, IReadOnlyCollection<int> regionIds)
    {
        var taken = new HashSet<int>();
        foreach (var id in regionIds)
            if (w.Regions.TryGetValue(id, out var r) && r.OwnerId == targetId)
            {
                r.OwnerId = demanderId; r.ControllerId = demanderId; r.Resistance = 0f; r.Integration = 0f;
                taken.Add(id);
            }

        // O que ficou ocupado e não foi exigido volta a quem o tem de direito, dos dois lados.
        foreach (var r in w.Regions.Values)
        {
            if (taken.Contains(r.Id)) continue;
            if (r.OwnerId == targetId && r.ControllerId == demanderId) r.ControllerId = targetId;
            else if (r.OwnerId == demanderId && r.ControllerId == targetId) r.ControllerId = demanderId;
        }

        w.ActiveBattles.RemoveAll(bt =>
            (bt.AttackerCountryId == demanderId || bt.AttackerCountryId == targetId)
            && !bt.Attackers.Concat(bt.Defenders).Any(id =>
                w.Divisions.TryGetValue(id, out var d) && d.CountryId != demanderId && d.CountryId != targetId));

        w.EndWar(demanderId, targetId);
        w.Events.Publish(new PeaceSigned(demanderId, targetId, taken.Count));
        w.Events.Publish(new WarEnded(demanderId, targetId));
    }

    /// <summary>A maior exigência que o alvo ainda assina, montada por ordem de valor para quem exige:
    /// primeiro o objectivo de guerra (o que se veio buscar), depois as regiões dele que já ocupamos,
    /// das mais povoadas para as menos, e por fim nada mais — regiões livres custam quase o dobro
    /// (peace_price_free) e são o primeiro sítio onde a mesa cai.
    ///
    /// É a diferença entre o jogador ter de adivinhar termos aceitáveis à segunda dúzia de tentativas
    /// e ter uma proposta pronta a assinar; a IA usa-a como recuo quando o objectivo sozinho não passa.</summary>
    public static List<int> Suggest(World w, int demanderId, int targetId)
    {
        var picked = new List<int>();
        if (!w.Countries.ContainsKey(demanderId) || !w.Countries.ContainsKey(targetId)) return picked;

        var order = new List<int>();
        if (w.Wars.TryGetValue(World.WarKey(demanderId, targetId), out var war))
            order.AddRange(war.Side(demanderId).Goals
                .Where(id => w.Regions.TryGetValue(id, out var r) && r.OwnerId == targetId && r.ControllerId == demanderId));
        order.AddRange(OccupiedRegions(w, demanderId, targetId)
            .Except(order)
            .OrderByDescending(id => w.Regions[id].Population).ThenBy(id => id));

        foreach (int id in order)
        {
            picked.Add(id);
            if (!Evaluate(w, demanderId, targetId, picked).Accepted) picked.RemoveAt(picked.Count - 1);
        }
        return picked;
    }

    /// <summary>As regiões do alvo que quem exige já ocupa — a exigência natural de quem está a ganhar.</summary>
    public static List<int> OccupiedRegions(World w, int demanderId, int targetId) =>
        w.Regions.Values.Where(r => r.OwnerId == targetId && r.ControllerId == demanderId).Select(r => r.Id).ToList();
}
