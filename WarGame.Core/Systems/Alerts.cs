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

        // 2b. Cerco: pior do que ter fome é não ter por onde sair. Estas divisões perdem gente todos os
        // dias e, com a bolsa fechada, acabam a render-se — é o aviso que dá tempo de romper para fora.
        var pocketed = PocketSystem.Of(w, countryId);
        if (pocketed.Count > 0)
        {
            var worstPocket = pocketed[0];
            int? left = PocketSystem.DaysToSurrender(w, worstPocket);
            string where = Name(w, worstPocket.RegionId);
            string doom = left is int days
                ? days <= 0 ? ", rende-se hoje" : days == 1 ? ", rende-se amanhã" : $", rende-se em {days} dias"
                : "";
            list.Add(new Alert("pocket", "⛓",
                               pocketed.Count == 1 ? $"1 divisão cercada em {where}{doom}"
                                                   : $"{pocketed.Count} divisões cercadas — a pior em {where}{doom}",
                               AlertLevel.Danger, worstPocket.RegionId));
        }

        // 2c. Combustível: uma frota e uns blindados param na véspera do dia em que o depósito acaba, e o
        // jogador só dá por isso quando os blindados batem a metade. A conta regressiva é o aviso.
        if (c.FuelOut)
            list.Add(new Alert("fuel", "🛢", "sem combustível: os blindados batem a meio gás", AlertLevel.Danger));
        else if (FuelSystem.DaysLeft(c) is float fd && fd >= 0f && fd <= w.Rule("alert_fuel_days", 10f))
            list.Add(new Alert("fuel", "🛢",
                               fd < 1f ? "o combustível acaba hoje"
                                       : $"o combustível dá para {fd:0} dias ao ritmo de agora",
                               fd <= w.Rule("alert_fuel_days", 10f) / 3f ? AlertLevel.Danger : AlertLevel.Warn));

        // 2d. Voluntários: as nossas divisões que se batem na guerra de outro não aparecem em lado nenhum do
        // mapa nosso, e é fácil esquecê-las lá — enquanto vão comendo homens e material do nosso cofre.
        int away = VolunteerSystem.Away(w, c.Id);
        if (away > 0)
        {
            var hosts = w.Divisions.Values.Where(d => d.VolunteerFrom == c.Id)
                         .Select(d => w.Countries.TryGetValue(d.CountryId, out var h) ? h.Name : "?")
                         .Distinct().OrderBy(n => n).ToList();
            list.Add(new Alert("volunteers", "🤝",
                               $"{away} {(away == 1 ? "divisão nossa bate-se" : "divisões nossas batem-se")} por {string.Join(" e ", hosts)}",
                               AlertLevel.Info));
        }

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

        // 9. Plano feito: um exército a defender com a preparação no máximo já não ganha nada em ficar
        // quieto — é o momento de mandar avançar, antes que o plano comece a envelhecer sem servir.
        float planMax = w.Rule("planning_max", 1f);
        foreach (var g in w.ArmyGroups.Values.OrderBy(x => x.Id))
        {
            if (g.CountryId != countryId || g.Stance != GroupStance.Defend) continue;
            if (g.Planning < planMax || g.Divisions.Count == 0) continue;
            list.Add(new Alert("plan", "🗺", $"o plano do {g.Name} está pronto ({g.Planning:P0})", AlertLevel.Info));
            break;
        }

        // 10. Batalha perdida há pouco: enquanto a derrota é fresca (alert_defeat_days) fica na faixa, e a
        // série de derrotas seguidas diz se aquilo foi um empurrão ou a frente a ceder.
        if (DefeatAlarmSystem.Fresh(w, c) && c.DefeatStreak > 0)
        {
            int need = Math.Max(1, (int)w.Rule("defeat_streak_alarm", 3f));
            int ago = w.Clock.Day - c.LastDefeatDay;
            string when = ago == 0 ? "hoje" : ago == 1 ? "ontem" : $"há {ago} dias";
            list.Add(new Alert("defeat", "☠",
                               c.DefeatStreak == 1 ? $"batalha perdida em {Name(w, c.LastDefeatRegion)} {when}"
                                                  : $"{c.DefeatStreak} batalhas perdidas seguidas — a última em {Name(w, c.LastDefeatRegion)} {when}",
                               c.DefeatStreak >= need ? AlertLevel.Danger : AlertLevel.Warn, c.LastDefeatRegion));
        }

        // 11. Empréstimo de material: o que entra de fora conta-se como rendimento, mas não é nosso — pode
        // fechar num dia. Quem tem uma frente sustentada por material emprestado deve sabê-lo antes de
        // encomendar a pensar no total.
        float lent = LendLeaseSystem.In(w, countryId), given = LendLeaseSystem.Out(w, countryId);
        if (lent > 0f)
        {
            int from = w.LendLeases.Count(l => l.ToId == countryId);
            list.Add(new Alert("lend_in", "⚓",
                               from == 1 ? $"+{lent:0.0}/dia de material emprestado por {Name2(w, w.LendLeases.First(l => l.ToId == countryId).FromId)}"
                                         : $"+{lent:0.0}/dia de material emprestado por {from} países",
                               AlertLevel.Info));
        }
        if (given > 0f)
            list.Add(new Alert("lend_out", "⚓", $"−{given:0.0}/dia do nosso rendimento emprestado lá fora",
                               income - given < 0f ? AlertLevel.Warn : AlertLevel.Info));

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

    private static string Name2(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Name : "um aliado";
}
