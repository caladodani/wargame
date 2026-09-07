using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Entrincheiramento (HoI4: dig-in). Tropa que fica quieta cava: cada dia parado sobe um degrau de
/// trincheira e cada degrau vale defesa. Quem marcha perde tudo — a trincheira não se leva às costas — e
/// quem assalta gasta-a, porque sair do buraco para atacar é justamente deixá-lo.
///
/// É o que faltava para uma frente parada ter significado. Antes, deixar as divisões quietas era só não dar
/// ordens: o inimigo atacava no dia 50 exactamente como no dia 1. Agora o tempo de mãos quietas é um
/// investimento, defender é uma escolha e não uma falta de plano, e o assalto tardio a uma linha velha custa
/// caro. Casa com os fortes: cada nível de forte levanta o tecto do que se pode cavar ali.
///
/// Corre depois do movimento e do combate: nessa altura já se sabe quem marchou e quem assaltou hoje, e a
/// batalha do dia usa a trincheira com que se acordou.</summary>
public sealed class EntrenchSystem : ISystem
{
    public string Name => "Entrench";

    public void Tick(World w)
    {
        float per = w.Rule("entrench_per_day", 0.5f);
        float loss = w.Rule("entrench_attack_loss", 1.5f);
        var attacking = new HashSet<int>();
        foreach (var b in w.ActiveBattles) foreach (int id in b.Attackers) attacking.Add(id);

        foreach (var d in w.Divisions.Values)
        {
            if (d.Path.Count > 0) { d.Entrench = 0f; continue; }             // a caminho: não há onde cavar
            if (attacking.Contains(d.Id)) { d.Entrench = MathF.Max(0f, d.Entrench - loss); continue; }
            d.Entrench = MathF.Min(Max(w, d), d.Entrench + per);
        }
    }

    /// <summary>Quanto se pode cavar onde a divisão está: o tecto de campo mais o que o forte da região já
    /// deixou feito. Vale também para cortar quem perdeu o forte com a terra debaixo dos pés.</summary>
    public static float Max(World w, Division d) =>
        w.Rule("entrench_max", 5f)
        + (w.Regions.TryGetValue(d.RegionId, out var r) ? r.Fort * w.Rule("entrench_per_fort", 1f) : 0f);

    /// <summary>Multiplicador de força a defender: 1 sem trincheira, mais entrench_defense_per_level por
    /// degrau. O CombatSystem só o aplica ao lado que defende — quem ataca não está no buraco.</summary>
    public static float Bonus(World w, Division d) =>
        1f + MathF.Max(0f, d.Entrench) * w.Rule("entrench_defense_per_level", 0.06f);
}
