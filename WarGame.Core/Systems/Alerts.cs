using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Gravidade de um aviso: informação, atenção, ou coisa que arde já.</summary>
public enum AlertLevel { Info = 0, Warn = 1, Danger = 2 }

/// <summary>Um aviso ao jogador. Id serve para a UI não redesenhar a faixa sem necessidade; RegionId leva
/// o mapa ao sítio do problema (0 = não há sítio).</summary>
public sealed record Alert(string Id, string Icon, string Text, AlertLevel Level, int RegionId = 0);

/// <summary>A faixa de avisos. Até aqui o jogo só falava quando alguma coisa acontecia — uma notificação
/// que passava — e nunca dizia o que estava mal *agora*: o cofre a secar daqui a uma semana, a tropa a
/// beber areia, a fronteira aberta do lado do inimigo, uma província ocupada a ferver, a fila de produção
/// parada com dinheiro no cofre. Quem não fosse a cada painel ver os números não sabia de nada.
///
/// É estado derivado: não guarda nada, não publica eventos, não entra no save e não é um ISystem — corre
/// quando a UI pergunta. Os limiares vêm todos da tabela rule, como o resto.</summary>
public static class Alerts
{
    /// <summary>Avisos deste país, do mais grave para o menos, com um id estável cada.</summary>
    public static List<Alert> For(World w, int countryId)
    {
        var list = new List<Alert>();
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return list;

        // 1. O cofre. Rendimento negativo com reserva curta é o aviso mais caro de ignorar: sem dinheiro
        // a fila pára, e quando pára já não há tempo de recuperar.
        float income = EconomySystem.Income(w, countryId);
        if (income < 0f)
        {
            float days = c.Money / -income;
            if (days <= w.Rule("alert_money_days", 15f))
                list.Add(new Alert("money", "₵", days <= 1f ? "o cofre esvazia-se hoje"
                                                            : $"o cofre dá para {days:0} dias ao ritmo de agora",
                                   days <= w.Rule("alert_money_days", 15f) / 3f ? AlertLevel.Danger : AlertLevel.Warn));
        }

        // 2. Tropa sem abastecimento: perde organização todos os dias e bate como se estivesse ferida.
        float dry = w.Rule("alert_supply", 0.6f);
        Division? worst = null; int starving = 0;
        foreach (var d in w.Divisions.Values)
        {
            if (d.CountryId != countryId || d.Supply >= dry) continue;
            starving++;
            if (worst is null || d.Supply < worst.Supply) worst = d;
        }
        if (worst is not null)
            list.Add(new Alert("supply", "⛽",
                               starving == 1 ? $"1 divisão sem abastecimento em {Name(w, worst.RegionId)}"
                                             : $"{starving} divisões sem abastecimento",
                               AlertLevel.Danger, worst.RegionId));

        // 3. Fronteira aberta: região nossa encostada a terreno de quem está em guerra connosco e sem
        // ninguém lá dentro. É por onde entram.
        Region? gap = null; int gaps = 0;
        foreach (var r in w.Regions.Values.OrderBy(x => x.Id))
        {
            if (r.ControllerId != countryId) continue;
            if (r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == countryId)) continue;
            if (!r.Neighbours.Any(n => w.Regions.TryGetValue(n, out var nb) && nb.ControllerId != countryId
                                       && w.AreAtWar(countryId, nb.ControllerId))) continue;
            gaps++; gap ??= r;
        }
        if (gap is not null)
            list.Add(new Alert("border", "⚑",
                               gaps == 1 ? $"{gap.Name} está na fronteira do inimigo e sem guarnição"
                                         : $"{gaps} regiões de fronteira sem guarnição",
                               AlertLevel.Warn, gap.Id));

        // 4. Terra ocupada a ferver: resistência alta cobra tropa e produção, e acaba em revolta.
        float boil = w.Rule("alert_resistance", 0.5f);
        Region? hot = null;
        foreach (var r in w.Regions.Values)
            if (r.ControllerId == countryId && r.OwnerId != countryId && r.Resistance >= boil
                && (hot is null || r.Resistance > hot.Resistance)) hot = r;
        if (hot is not null)
            list.Add(new Alert("resistance", "✊", $"{hot.Name} resiste à ocupação ({hot.Resistance:P0})",
                               hot.Resistance >= (1f + boil) / 2f ? AlertLevel.Danger : AlertLevel.Warn, hot.Id));

        // 5. Fábricas paradas com dinheiro no cofre: produção que se perde e não volta.
        if (c.Queue.Count == 0 && c.Money >= w.Rule("alert_idle_money", 150f))
            list.Add(new Alert("queue", "⚙", $"fila de produção vazia com {c.Money:0} no cofre", AlertLevel.Warn));

        // 6. Ranhuras de investigação por ocupar: o mesmo desperdício, do lado da ciência.
        int free = ResearchSystem.FreeSlots(w, c);
        if (free > 0)
            list.Add(new Alert("research", "⚗", c.Research.Count == 0 ? "ninguém está a investigar nada"
                                              : free == 1 ? "1 ranhura de investigação livre"
                                                          : $"{free} ranhuras de investigação livres", AlertLevel.Info));

        // 7. Fábricas civis paradas: obras que se podiam ter começado hoje e ficam para depois.
        var yards = Industry.Of(w, countryId);
        if (yards.FreeCivil > 0 && c.Money >= w.Rule("alert_idle_money", 150f))
            list.Add(new Alert("factories", "🏭", yards.FreeCivil == 1 ? "1 fábrica civil parada"
                                                : $"{yards.FreeCivil} fábricas civis paradas", AlertLevel.Info));

        // 8. Propostas em cima da mesa: caem sozinhas se ninguém lhes tocar.
        // 8. Batalhas: a frente só leva um número de divisões (Frontage). Estar em maioria não vale de nada se
        // a montanha não deixar entrar mais do que três — e quem não souber disso empilha tropa a perder.
        foreach (var b in w.ActiveBattles)
        {
            if (!w.Regions.TryGetValue(b.RegionId, out var br)) continue;
            bool weAttack = b.AttackerCountryId == countryId, weDefend = br.ControllerId == countryId;
            if (!weAttack && !weDefend) continue;
            var ours = (weAttack ? b.Attackers : b.Defenders).Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>();
            var theirs = (weAttack ? b.Defenders : b.Attackers).Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>();
            int ourLine = Frontage.Split(w, br, ours).Line.Count, theirLine = Frontage.Split(w, br, theirs).Line.Count;
            if (ourLine >= theirLine) continue;
            list.Add(new Alert("battle", "⚔", $"em {br.Name} a linha é de {ourLine} contra {theirLine}",
                               AlertLevel.Warn, br.Id));
            break;
        }

        int offers = w.Offers.Count(o => o.ToId == countryId);
        if (offers > 0)
            list.Add(new Alert("offers", "✉", offers == 1 ? "1 proposta à espera de resposta"
                                                          : $"{offers} propostas à espera de resposta", AlertLevel.Info));

        return list.OrderByDescending(a => a.Level).ThenBy(a => a.Id).ToList();
    }

    /// <summary>Chave estável do conjunto de avisos: a UI só redesenha a faixa quando isto muda.</summary>
    public static string Key(IEnumerable<Alert> alerts) => string.Join("|", alerts.Select(a => a.Id + a.Text));

    private static string Name(World w, int regionId) =>
        w.Regions.TryGetValue(regionId, out var r) ? r.Name : "campo aberto";
}
