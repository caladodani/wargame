using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Adidos militares (HoI4: military attaché). Manda-se um oficial para junto de um exército
/// estrangeiro que está a bater-se, paga-se-lhe a estadia todos os dias, e o que ele vê volta para casa
/// como experiência de exército — a moeda das escolas de guerra.
///
/// É a resposta a um problema que as doutrinas trouxeram: um país em paz não junta experiência nenhuma e
/// nunca chega ao primeiro degrau de doutrina; ficava a assistir à guerra dos outros sem aprender nada.
/// Agora a paz também se pode aproveitar — desde que haja cofre para pagar a missão, porque o adido custa
/// attache_cost_per_day por dia esteja ou não a haver batalha.
///
/// A missão dura até ser chamada de volta (RecallAttacheCommand) ou até deixar de fazer sentido: o
/// anfitrião capitula, o anfitrião faz a paz, ou a guerra chega entre nós e ele — e nesse caso o oficial
/// vem para casa no próprio dia, que ninguém deixa um observador dentro do inimigo.
///
/// A IA manda adidos pela mesma regra: quem está em paz, tem cofre e ainda não escolheu doutrina, procura
/// uma guerra alheia para observar. Assim as escolas de guerra também se enchem fora dos beligerantes.</summary>
public sealed class AttacheSystem : ISystem
{
    public string Name => "Attaches";

    public void Tick(World w)
    {
        float cost = w.Rule("attache_cost_per_day", 0.5f);
        float gain = w.Rule("attache_xp_per_day", 0.3f);
        float max = w.Rule("army_xp_max", 600f);

        foreach (int id in w.Attaches.Keys.OrderBy(x => x).ToList())
        {
            var a = w.Attaches[id];
            if (!w.Countries.TryGetValue(id, out var c) || c.Capitulated) { w.Attaches.Remove(id); continue; }
            // a missão acabou: o anfitrião caiu, assinou a paz, ou passámos a inimigos dele
            if (!w.Countries.TryGetValue(a.HostId, out var host) || host.Capitulated
                || w.AreAtWar(id, a.HostId) || !w.AtWar(a.HostId))
            { w.Attaches.Remove(id); continue; }

            if (c.Money < cost) { w.Attaches.Remove(id); continue; }   // sem cofre não há missão
            c.Money -= cost;
            float before = c.ArmyXp;
            c.ArmyXp = MathF.Min(max, c.ArmyXp + gain);
            a.Learned += c.ArmyXp - before;
        }

        Ai(w, cost);
    }

    /// <summary>A IA em paz procura guerra alheia para observar: paga se o cofre aguentar a missão inteira
    /// de arranque (attache_ai_money) e escolhe a guerra mais próxima de casa — vizinho primeiro, depois o
    /// id mais baixo, para o mundo não depender de sementes.</summary>
    private static void Ai(World w, float cost)
    {
        float min = w.Rule("attache_ai_money", 60f);
        foreach (var c in w.Countries.Values.OrderBy(x => x.Id))
        {
            if (c.IsPlayer || c.Capitulated || c.Money < min) continue;
            if (c.AtWarWith.Count > 0 || w.Attaches.ContainsKey(c.Id)) continue;
            if (Pick(w, c.Id) is int host) Send(w, c.Id, host);
        }
    }

    /// <summary>Melhor anfitrião para este país: dos que estão em guerra e não em guerra connosco, primeiro
    /// um aliado de facção, depois o de id mais baixo.</summary>
    public static int? Pick(World w, int countryId)
    {
        int? ally = null, any = null;
        foreach (var h in w.Countries.Values.OrderBy(x => x.Id))
        {
            if (w.AttacheBlock(countryId, h.Id) is not null) continue;
            any ??= h.Id;
            if (ally is null && w.SameFaction(countryId, h.Id)) ally = h.Id;
        }
        return ally ?? any;
    }

    /// <summary>Destaca o adido. Único sítio que escreve World.Attaches — o comando do jogador passa aqui.</summary>
    public static void Send(World w, int countryId, int hostId)
    {
        if (w.AttacheBlock(countryId, hostId) is not null) return;
        w.Attaches[countryId] = new Attache { CountryId = countryId, HostId = hostId, SinceDay = w.Clock.Day };
    }

    /// <summary>O que esta missão já trouxe de casa, para o painel dizer se valeu a pena.</summary>
    public static float Learned(World w, int countryId) =>
        w.Attaches.TryGetValue(countryId, out var a) ? a.Learned : 0f;
}
