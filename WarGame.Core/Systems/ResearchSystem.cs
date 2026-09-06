using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Investigação. Cada dia soma research_speed do país ao progresso de cada ranhura ocupada; ao
/// chegar a tech.cost a tecnologia entra em Country.Techs, os efeitos de país (tech_effect) são
/// recalculados e os de combate passam a valer pelos modifier com condition_key tech:&lt;id&gt;.
///
/// As ranhuras são o que o HoI4 chama research slots: quantas linhas de investigação um país aguenta ao
/// mesmo tempo. Vêm da regra research_slots multiplicada pelo stat research_slots do país (uma potência
/// industrial trabalha em mais frentes do que um país pequeno), nunca menos de uma. Enquanto havia só uma
/// linha, escolher infantaria era desistir de blindados até ao fim — uma decisão que nenhum estado-maior
/// toma, porque os laboratórios são vários.</summary>
public sealed class ResearchSystem : ISystem
{
    public string Name => "Research";

    /// <summary>Quantas linhas de investigação este país aguenta ao mesmo tempo.</summary>
    public static int Slots(World w, Country c) =>
        Math.Max(1, (int)MathF.Round(c.Stat("research_slots", w.Rule("research_slots", 2f))));

    /// <summary>Ranhuras por ocupar (0 = laboratórios cheios).</summary>
    public static int FreeSlots(World w, Country c) => Math.Max(0, Slots(w, c) - c.Research.Count);

    public void Tick(World w)
    {
        foreach (var c in w.Countries.Values)
        {
            if (c.Research.Count == 0) continue;
            foreach (var techId in c.Research.Keys.ToList())
            {
                if (!w.Techs.TryGetValue(techId, out var t) || c.Techs.Contains(techId)) { c.Research.Remove(techId); continue; }
                float done = c.Research[techId] + c.Stat("research_speed");
                if (done < t.Cost) { c.Research[techId] = done; continue; }
                c.Research.Remove(techId);
                c.Techs.Add(t.Id);
                w.ApplyTechs(c);
                w.Events.Publish(new TechResearched(c.Id, t.Id));
            }
        }
    }
}
