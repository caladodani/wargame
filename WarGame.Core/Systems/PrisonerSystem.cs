using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Prisioneiros de guerra. Uma divisão desfeita era mão-de-obra que desaparecia do mundo: os
/// homens morriam todos, sempre. Não é assim que as guerras acabam — grande parte de um exército
/// derrotado rende-se, e quem os apanha fica com eles: trabalham na retaguarda enquanto durar a guerra
/// (prisioneiros dão indústria, até um tecto) e só voltam a casa quando se assina a paz, e nem todos.
///
/// Dá três coisas ao jogo que não existiam: uma guerra longa passa a valer alguma coisa mesmo quando a
/// frente não anda, a paz passa a ter um preço humano do lado de quem perdeu, e a fuga diária faz com
/// que um campo enorme não seja dinheiro parado para sempre.
///
/// Tudo o que decide vem das regras: prisoner_share (quanto de uma divisão se rende), prisoner_work_men
/// e prisoner_work_max (o que valem a trabalhar), prisoner_escape (fuga por dia) e prisoner_return
/// (quanto volta a casa na paz).</summary>
public sealed class PrisonerSystem : ISystem
{
    public string Name => "Prisoners";
    private World? _bound;

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) Bind(w);
        Escape(w);
        foreach (var c in w.Countries.Values) Work(w, c);
    }

    /// <summary>Liga-se ao barramento. Idempotente por mundo (padrão do WarStatsSystem).</summary>
    public void Bind(World w)
    {
        _bound = w;
        w.Events.Subscribe<DivisionDestroyed>(e => OnDivisionLost(w, e));
        w.Events.Subscribe<DivisionSurrendered>(e => OnSurrender(w, e));
        w.Events.Subscribe<PeaceSigned>(e => Repatriate(w, e.Winner, e.Loser));
        w.Events.Subscribe<WhitePeaceSigned>(e => Repatriate(w, e.A, e.B));
        w.Events.Subscribe<CountryCapitulated>(e => Repatriate(w, e.CountryId, e.WinnerId));
    }

    /// <summary>Divisão desfeita em terreno inimigo: parte dos homens rende-se a quem manda ali. Sem
    /// inimigo por perto (retirada, cerco desfeito longe da linha) não há quem os apanhe.</summary>
    private static void OnDivisionLost(World w, DivisionDestroyed e)
    {
        // O evento chega antes de a divisão sair do mundo — o dono e o sítio ainda se sabem.
        if (!w.Divisions.TryGetValue(e.DivisionId, out var d)) return;
        if (!w.Regions.TryGetValue(d.RegionId, out var r)) return;
        int captor = r.ControllerId;
        if (captor == d.CountryId || !w.AreAtWar(d.CountryId, captor)) return;
        if (!w.Countries.TryGetValue(captor, out var cap)) return;

        int men = Men(w, d);
        if (men <= 0) return;
        cap.Prisoners[d.CountryId] = cap.Prisoners.GetValueOrDefault(d.CountryId) + men;
        w.Events.Publish(new PrisonersTaken(captor, d.CountryId, men, r.Id));
    }

    /// <summary>Divisão cercada que baixou as armas (PocketSystem): entrega-se inteira a quem fechou o
    /// anel. Uma rendição não é uma divisão desfeita — ninguém se dispersou pelo mato a combater — por
    /// isso os homens contam-se por pocket_prisoner_share, que é quase toda a gente.</summary>
    private static void OnSurrender(World w, DivisionSurrendered e)
    {
        if (!w.Divisions.TryGetValue(e.DivisionId, out var d)) return;
        if (!w.Countries.TryGetValue(e.CaptorId, out var cap)) return;
        int men = MenSurrendered(w, d);
        if (men <= 0) return;
        cap.Prisoners[e.CountryId] = cap.Prisoners.GetValueOrDefault(e.CountryId) + men;
        w.Events.Publish(new PrisonersTaken(e.CaptorId, e.CountryId, men, e.RegionId));
    }

    /// <summary>Homens que uma divisão rendida entrega: o efectivo dela vezes pocket_prisoner_share.</summary>
    public static int MenSurrendered(World w, Division d) =>
        (int)(w.TemplateCost(d.TemplateId) * w.Rule("manpower_per_cost", 500f) * w.Rule("pocket_prisoner_share", 0.9f));

    /// <summary>Homens que uma divisão desfeita entrega vivos: o efectivo dela (custo × manpower_per_cost)
    /// vezes prisoner_share.</summary>
    public static int Men(World w, Division d) =>
        (int)(w.TemplateCost(d.TemplateId) * w.Rule("manpower_per_cost", 500f) * w.Rule("prisoner_share", 0.3f));

    /// <summary>Fuga: todos os dias uma fracção dos prisioneiros escapa e volta ao pool de casa. Um campo
    /// grande perde mais gente em números absolutos — guardar meio milhão de homens não sai de graça.</summary>
    private static void Escape(World w)
    {
        float rate = w.Rule("prisoner_escape", 0.001f);
        if (rate <= 0f) return;
        foreach (var c in w.Countries.Values)
        {
            if (c.Prisoners.Count == 0) continue;
            foreach (var from in c.Prisoners.Keys.ToList())
            {
                int gone = (int)MathF.Ceiling(c.Prisoners[from] * rate);
                if (gone <= 0) continue;
                Release(w, c, from, gone, 1f);
            }
        }
    }

    /// <summary>Trabalho forçado: os prisioneiros que um país guarda valem indústria, até prisoner_work_max.
    /// prisoner_work_men é quantos homens é preciso ter para chegar ao tecto.</summary>
    private static void Work(World w, Country c)
    {
        float max = w.Rule("prisoner_work_max", 0.2f);
        float full = MathF.Max(1f, w.Rule("prisoner_work_men", 400000f));
        int men = c.Prisoners.Values.Sum();
        if (men <= 0 || max <= 0f) { c.PrisonerMult.Remove("industry"); return; }
        c.PrisonerMult["industry"] = 1f + max * Math.Clamp(men / full, 0f, 1f);
    }

    /// <summary>Paz assinada: os campos abrem-se dos dois lados. Nem todos voltam — prisoner_return diz
    /// quantos chegam a casa, e o resto ficou pelo caminho.</summary>
    public static void Repatriate(World w, int a, int b)
    {
        float back = w.Rule("prisoner_return", 0.6f);
        foreach (var (holder, from) in new[] { (a, b), (b, a) })
        {
            if (!w.Countries.TryGetValue(holder, out var c) || !c.Prisoners.TryGetValue(from, out var men) || men <= 0) continue;
            Release(w, c, from, men, back);
            w.Events.Publish(new PrisonersReturned(holder, from, (int)(men * back)));
        }
    }

    /// <summary>Tira homens do campo e devolve ao pool de quem os perdeu a fracção que sobreviveu.
    /// Público porque a troca negociada (ExchangePrisonersCommand) abre os campos pelo mesmo caminho da
    /// fuga e da paz — a viagem para casa é sempre esta.</summary>
    public static void Release(World w, Country holder, int from, int men, float share)
    {
        int left = holder.Prisoners.GetValueOrDefault(from) - men;
        if (left > 0) holder.Prisoners[from] = left; else holder.Prisoners.Remove(from);
        if (w.Countries.TryGetValue(from, out var home) && home.Manpower >= 0f)
            home.Manpower += men * share;
    }
}
