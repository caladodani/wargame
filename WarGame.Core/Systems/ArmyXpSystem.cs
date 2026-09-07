using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Experiência de exército e doutrinas (HoI4: army experience + doutrinas de terra). Um exército
/// não sai igual de uma guerra: aprende. Cada dia de batalha dá experiência ao país por cada divisão que lá
/// está, e as manobras de tempo de paz dão um fio dela a quem tem tropa no terreno.
///
/// Essa experiência é a moeda com que se compra uma escola militar — Guerra de Movimento, Superioridade de
/// Fogo, Assalto em Massa — degrau a degrau (tabela army_doctrine). Escolhido um ramo, os outros fecham-se:
/// um exército não se treina ao mesmo tempo em duas maneiras contrárias de fazer a guerra. Os efeitos são
/// multiplicadores de país e entram pelo World.ApplyTechs, ao lado das tecnologias e das leis.
///
/// Isto não são as leis do grupo doctrine, que o governo troca por decreto quando lhe apetece: uma lei
/// muda-se numa tarde, uma escola de guerra aprende-se com sangue e não se desaprende.
///
/// A IA adopta sozinha o degrau mais barato que tiver à mão; o jogador escolhe por AdoptDoctrineCommand.</summary>
public sealed class ArmyXpSystem : ISystem
{
    public string Name => "ArmyXp";

    public void Tick(World w)
    {
        if (w.ArmyDoctrines.Count == 0) return;

        float perBattleDay = w.Rule("army_xp_per_battle_day", 0.4f);
        float perDay = w.Rule("army_xp_per_day", 0.1f);
        float max = w.Rule("army_xp_max", 600f);

        // um varrimento só pelas divisões: quem tem exército, e quem o tem metido em batalha hoje
        var hasArmy = new HashSet<int>();
        foreach (var d in w.Divisions.Values) hasArmy.Add(d.CountryId);
        var fighting = new Dictionary<int, int>();
        foreach (var b in w.ActiveBattles)
            foreach (int id in b.Attackers.Concat(b.Defenders))
                if (w.Divisions.TryGetValue(id, out var d))
                    fighting[d.CountryId] = fighting.GetValueOrDefault(d.CountryId) + 1;

        foreach (var c in w.Countries.Values)
        {
            if (c.Capitulated) continue;
            float gain = fighting.GetValueOrDefault(c.Id) * perBattleDay + (hasArmy.Contains(c.Id) ? perDay : 0f);
            if (gain > 0f) c.ArmyXp = MathF.Min(max, c.ArmyXp + gain);
            if (c.IsPlayer) continue;                       // o jogador escolhe a escola no painel
            if (Next(w, c) is string pick) Adopt(w, c, pick);
        }
    }

    /// <summary>Degrau mais barato que este país pode adoptar já (empates pelo id), ou null. A escola
    /// nacional passa à frente das comuns quando as duas estão pagas: um exército que tem maneira própria de
    /// fazer a guerra treina a sua, não a do vizinho.
    ///
    /// Com uma arma (World.Land/Air/Sea) responde só por ela — é o que a barra de cima pergunta a cada
    /// mostrador de experiência: "o que é que isto hoje já dá para comprar?".</summary>
    public static string? Next(World w, Country c, string? domain = null)
    {
        ArmyDoctrine? best = null;
        foreach (var d in w.ArmyDoctrines.Values)
        {
            if (domain is not null && w.DomainOf(d) != domain) continue;
            if (!w.CanAdopt(c, d.Id)) continue;
            if (best is null || Better(d, best)) best = d;
        }
        return best?.Id;

        static bool Better(ArmyDoctrine a, ArmyDoctrine b)
        {
            bool an = a.CountryTag is not null, bn = b.CountryTag is not null;
            if (an != bn) return an;                        // a escola de casa primeiro
            if (a.Cost != b.Cost) return a.Cost < b.Cost;
            return string.CompareOrdinal(a.Id, b.Id) < 0;
        }
    }

    /// <summary>Adopta a doutrina: paga a experiência, aprende-a para sempre e recalcula os multiplicadores.
    /// Único sítio que escreve Country.Doctrines — o comando do jogador passa por aqui. Uma escola que já se
    /// sabe não se paga outra vez (o Add é que decide, para o preço nunca sair sem a doutrina entrar).</summary>
    public static void Adopt(World w, Country c, string doctrineId)
    {
        if (!w.ArmyDoctrines.TryGetValue(doctrineId, out var d) || !c.Doctrines.Add(doctrineId)) return;
        World.SpendXp(c, w.DomainOf(d), d.Cost);        // cada arma paga do seu bolso
        w.ApplyTechs(c);
    }
}
