using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Investigação (HoI4: slots de investigação; aqui um por país). Cada dia soma research_speed do país ao
/// progresso; ao chegar a tech.cost a tecnologia entra em Country.Techs, os efeitos de país (tech_effect) são
/// recalculados e os de combate passam a valer pelos modifier com condition_key tech:&lt;id&gt;. Sem regras próprias.</summary>
public sealed class ResearchSystem : ISystem
{
    public string Name => "Research";

    public void Tick(World w)
    {
        foreach (var c in w.Countries.Values)
        {
            if (c.ResearchTech is null) continue;
            if (!w.Techs.TryGetValue(c.ResearchTech, out var t) || c.Techs.Contains(t.Id)) { c.ResearchTech = null; c.ResearchProgress = 0f; continue; }
            c.ResearchProgress += c.Stat("research_speed");
            if (c.ResearchProgress < t.Cost) continue;
            c.Techs.Add(t.Id); c.ResearchTech = null; c.ResearchProgress = 0f;
            w.ApplyTechs(c);
            w.Events.Publish(new TechResearched(c.Id, t.Id));
        }
    }
}
