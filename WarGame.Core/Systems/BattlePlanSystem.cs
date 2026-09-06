using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Planos de batalha (HoI4: battle plans). Um exército a que se deu uma frente e que fica quieto
/// não está a perder tempo: o estado-maior levanta o terreno, marca os eixos de avanço, junta pontes e
/// combustível nos sítios certos. Isso é a preparação do plano, e é o que faz a diferença entre um ataque
/// improvisado e uma ofensiva que já sabe por onde vai.
///
/// Até aqui só havia dois estados possíveis para um grupo: parado, sem servir de nada, ou a marchar. Ficar
/// à espera era puro desperdício, e a única razão para não atacar já era a organização estar em baixo.
/// Agora a espera compra alguma coisa: cada dia de frente sossegada soma planning_per_day à preparação,
/// e o que ela vale em força de combate está em Bonus.
///
/// O plano gasta-se ao ser executado — planning_decay por dia, na proporção das divisões do grupo que estão
/// a marchar ou metidas em combate. Uma ofensiva longa acaba a bater como qualquer outra; quem pára, volta
/// a preparar. Só há plano com frente atribuída e postura de avançar ou defender: parado ou em reserva não
/// há plano nenhum (a preparação cai a zero, porque não há eixo nenhum a estudar).
///
/// A preparação vive no ArmyGroup e vai ao save (coluna s_army_group.planning).</summary>
public sealed class BattlePlanSystem : ISystem
{
    public string Name => "BattlePlans";

    public void Tick(World w)
    {
        if (w.ArmyGroups.Count == 0) return;
        float max = w.Rule("planning_max", 1f);
        float gain = w.Rule("planning_per_day", 0.05f);
        float decay = w.Rule("planning_decay", 0.18f);

        foreach (var g in w.ArmyGroups.Values)
        {
            if (!Plans(g)) { g.Planning = 0f; continue; }

            int busy = 0, total = 0;
            foreach (int id in g.Divisions)
            {
                if (!w.Divisions.TryGetValue(id, out var d)) continue;
                total++;
                if (d.Path.Count > 0 || w.InBattle(id)) busy++;
            }
            if (total == 0) { g.Planning = 0f; continue; }

            float share = (float)busy / total;
            float step = share > 0f ? -decay * share : gain;
            g.Planning = Math.Clamp(g.Planning + step, 0f, max);
        }
    }

    /// <summary>Um grupo só tem plano se lhe deram uma frente e uma missão de linha (avançar ou defender).</summary>
    public static bool Plans(ArmyGroup g) =>
        g.FrontCountryId is not null && g.Stance is GroupStance.Advance or GroupStance.Defend;

    /// <summary>O que o plano vale à divisão: 1 sem plano, até 1+planning_bonus com ele feito. Vale para
    /// atacar e para defender — o terreno estudado serve para as duas coisas.</summary>
    public static float Bonus(World w, Division d)
    {
        if (w.ArmyGroups.Count == 0 || d.GroupId is not int gid) return 1f;
        if (!w.ArmyGroups.TryGetValue(gid, out var g) || g.Planning <= 0f) return 1f;
        return 1f + g.Planning * w.Rule("planning_bonus", 0.25f);
    }

    /// <summary>Dias que faltam para o plano deste grupo estar feito (0 = já está, -1 = não há plano).</summary>
    public static int DaysToReady(World w, ArmyGroup g)
    {
        if (!Plans(g)) return -1;
        float max = w.Rule("planning_max", 1f), gain = w.Rule("planning_per_day", 0.05f);
        if (g.Planning >= max) return 0;
        return gain <= 0f ? -1 : (int)MathF.Ceiling((max - g.Planning) / gain);
    }
}
