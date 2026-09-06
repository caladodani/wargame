using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Eventos noticiosos com data marcada (tabela news_event). No dia exacto publica NewsFired
/// (a UI faz o toast) e recalcula os multiplicadores dos países afectados — os efeitos de
/// news_event_effect entram em World.ApplyTechs junto das tecnologias e focos, contando
/// todos os eventos cujo dia já passou. Assim um save recarregado fica automaticamente com
/// os efeitos certos, sem persistência própria.
/// </summary>
public sealed class NewsSystem : ISystem
{
    public string Name => "News";

    public void Tick(World w)
    {
        foreach (var e in w.NewsEvents.Values)
        {
            if (e.Day != w.Clock.Day) continue;
            if (w.NewsEffects.ContainsKey(e.Id))
            {
                if (e.CountryId is int id) { if (w.Countries.TryGetValue(id, out var c)) w.ApplyTechs(c); }
                else foreach (var c in w.Countries.Values) w.ApplyTechs(c);
            }
            w.Events.Publish(new NewsFired(e.Id));
        }
    }
}
