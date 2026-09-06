using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Focos nacionais (HoI4): um foco em curso por país, avança 1 dia por dia, conclui ao fim
/// de focus.days e aplica os multiplicadores de focus_effect (via World.ApplyTechs, junto
/// das tecnologias). A IA escolhe sozinha o próximo foco disponível (sort, depois id);
/// o jogador escolhe por SelectFocusCommand. Países sem focos na base de dados ignoram-se.
/// </summary>
public sealed class FocusSystem : ISystem
{
    public string Name => "Focus";

    public void Tick(World w)
    {
        if (w.Focuses.Count == 0) return;
        foreach (var c in w.Countries.Values)
        {
            if (c.Capitulated) continue;
            if (c.CurrentFocus is null)
            {
                if (c.IsPlayer) continue;   // o jogador escolhe no painel
                var next = NextFocus(w, c);
                if (next is null) continue;
                c.CurrentFocus = next; c.FocusProgress = 0f;
            }
            if (!w.Focuses.TryGetValue(c.CurrentFocus, out var f) || c.FocusesDone.Contains(f.Id))
            { c.CurrentFocus = null; c.FocusProgress = 0f; continue; }
            c.FocusProgress += 1f;
            if (c.FocusProgress < f.Days) continue;
            c.FocusesDone.Add(f.Id); c.CurrentFocus = null; c.FocusProgress = 0f;
            w.ApplyTechs(c);
            w.Events.Publish(new FocusCompleted(c.Id, f.Id));
        }
    }

    /// <summary>Próximo foco disponível do país (menor sort, depois id) ou null.</summary>
    public static string? NextFocus(World w, Country c)
    {
        Focus? best = null;
        foreach (var f in w.Focuses.Values)
            if (f.CountryId == c.Id && w.CanFocus(c, f.Id)
                && (best is null || f.Sort < best.Sort || (f.Sort == best.Sort && string.CompareOrdinal(f.Id, best.Id) < 0)))
                best = f;
        return best?.Id;
    }
}
