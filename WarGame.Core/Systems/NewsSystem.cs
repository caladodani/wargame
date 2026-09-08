using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Os eventos noticiosos (tabela news_event), de dois feitios.
///
/// Os de data marcada caem no dia que trazem escrito, e são o pano de fundo do mundo: cimeiras, crises,
/// feiras de defesa. Os de estado não têm data nenhuma — trazem uma sonda (WorldWatch) e ficam à espera
/// que o mundo faça alguma coisa connosco: a guerra rebentar, a capital cair, a bolsa fechar-se, os
/// cofres secarem. São estes que tiram o jogo do silêncio entre uma ofensiva e a seguinte, que é a
/// queixa que mais se lê sobre jogos deste feitio: o relógio anda e ninguém bate à porta.
///
/// Cada evento cai uma vez e fica escrito em World.NewsFired (com o país em que caiu), porque um evento
/// de estado não tem calendário por onde se recalcule. Os efeitos entram em World.ApplyTechs junto das
/// tecnologias e dos focos — quem decide se contam é o World.NewsHit.
///
/// Quem apanha um evento de estado sem tag é quem joga: é a notícia que chega à secretária dele. Com tag,
/// é o país da tag, jogue-o quem o jogar. Eventos com escolhas param à espera do jogador
/// (NewsChoiceRequired → cartão de ecrã inteiro); a IA fica com a primeira opção na hora.
/// </summary>
public sealed class NewsSystem : ISystem
{
    public string Name => "News";

    public void Tick(World w)
    {
        foreach (var e in w.NewsEvents.Values)
        {
            if (w.NewsFired.ContainsKey(e.Id)) continue;      // cada evento cai uma vez só
            if (Lands(w, e) is not int on) continue;
            w.NewsFired[e.Id] = (w.Clock.Day, on);
            Recalc(w, on);

            // Eventos com escolhas: o jogador escolhe (NewsChoiceRequired → UI); a IA e os
            // eventos globais ficam com a primeira opção (sort) na hora.
            if (w.NewsOptions.TryGetValue(e.Id, out var opts) && opts.Count > 0 && !w.NewsChoices.ContainsKey(e.Id))
            {
                bool playerChooses = on != 0 && w.Countries.TryGetValue(on, out var target)
                                     && target.IsPlayer && !target.Capitulated;
                if (playerChooses) w.Events.Publish(new NewsChoiceRequired(e.Id));
                else
                {
                    w.NewsChoices[e.Id] = opts[0].Id;
                    Recalc(w, on);
                }
            }
            w.Events.Publish(new NewsFired(e.Id));
        }
    }

    /// <summary>Em cima de quem é que o evento cai hoje: id do país, 0 para o mundo inteiro, null se
    /// ainda não é hoje. É aqui que se juntam os dois feitios de evento.</summary>
    private static int? Lands(World w, NewsEvent e)
    {
        if (!e.IsWatch) return e.Day == w.Clock.Day ? e.CountryId ?? 0 : null;
        if (w.Clock.Day < w.Rule("event_watch_day", 7f)) return null;   // semana de descanso no arranque
        var c = e.CountryId is int cid ? w.Countries.GetValueOrDefault(cid) : Player(w);
        if (c is null || c.Capitulated) return null;
        return WorldWatch.Fires(w, c, e.Watch, e.Arg) ? c.Id : null;
    }

    /// <summary>O país que joga; sem ninguém a jogar (mundo só de IA) os eventos de estado sem tag não
    /// caem em ninguém — não há secretária onde pousar a notícia.</summary>
    private static Country? Player(World w)
    {
        foreach (var c in w.Countries.Values) if (c.IsPlayer) return c;
        return null;
    }

    private static void Recalc(World w, int countryId)
    {
        if (countryId != 0) { if (w.Countries.TryGetValue(countryId, out var c)) w.ApplyTechs(c); }
        else foreach (var c in w.Countries.Values) w.ApplyTechs(c);
    }
}
