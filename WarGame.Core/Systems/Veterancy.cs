using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Veterania: o que a tropa aprendeu à custa de sangue.
///
/// O XP já existia — o CombatSystem escrevia-o a cada dia de batalha e trocava-o por uma recta de força
/// extra — mas era um número solto: para saber se aquela pilha era gente verde ou uma divisão que fez a
/// campanha toda era preciso abrir a ficha, uma divisão de cada vez. No HoI4 isso lê-se do mapa: os galões
/// no canto do contador dizem o grau, e é por eles que se decide qual das pilhas vai ao assalto e qual é
/// que se poupa.
///
/// Nada disto se guarda. O grau lê-se do Division.Xp, por isso não há degrau guardado que possa contradizer
/// o que o combate escreveu — e mexer na tabela veterancy muda o mapa no mesmo instante. Sem tabela
/// carregada, o bónus volta à recta antiga (Xp/xp_max × veterancy_bonus), que é como o combate media isto
/// antes de haver graus.</summary>
public static class Veterancy
{
    /// <summary>O grau de uma experiência. Null só quando a tabela não veio.</summary>
    public static VeterancyDef? Tier(World w, float xp)
    {
        VeterancyDef? best = null;
        foreach (var t in w.VeterancyTiers.Values)
        {
            if (xp < t.MinXp) continue;
            if (best is null || t.MinXp > best.MinXp) best = t;
        }
        return best;
    }

    /// <summary>O grau desta divisão.</summary>
    public static VeterancyDef? Tier(World w, Division d) => Tier(w, d.Xp);

    /// <summary>A força extra que a experiência vale em combate — os degraus da tabela, ou a recta antiga
    /// quando a tabela não veio. É este número, e só este, que o CombatSystem soma.</summary>
    public static float Bonus(World w, float xp) =>
        Tier(w, xp) is VeterancyDef t ? t.Bonus
                                      : xp / MathF.Max(1f, w.Rule("xp_max", 100f)) * w.Rule("veterancy_bonus", 0.25f);

    public static float Bonus(World w, Division d) => Bonus(w, d.Xp);

    /// <summary>Os galões do contador do mapa: quantos riscos se desenham para este grau.</summary>
    public static int Chevrons(World w, float xp) => Tier(w, xp)?.Chevrons ?? 0;

    /// <summary>Quanto falta para o degrau seguinte, e qual ele é. Null a quem já está no topo (ou sem
    /// tabela): é o que a ficha da divisão diz por baixo da barra de XP.</summary>
    public static (VeterancyDef Next, float Missing)? Ahead(World w, float xp)
    {
        VeterancyDef? next = null;
        foreach (var t in w.VeterancyTiers.Values)
        {
            if (t.MinXp <= xp) continue;
            if (next is null || t.MinXp < next.MinXp) next = t;
        }
        return next is null ? null : (next, next.MinXp - xp);
    }

    /// <summary>A linha da ficha: o grau, o que ele dá e o que falta para o próximo.</summary>
    public static string Line(World w, Division d)
    {
        if (Tier(w, d) is not VeterancyDef t) return "";
        string gain = t.Bonus > 0f ? $" (+{t.Bonus:P0} força)" : "";
        string next = Ahead(w, d.Xp) is (VeterancyDef n, float missing) ? $" · faltam {missing:0} XP para {n.Name}" : "";
        return $"{t.Icon} {t.Name}{gain}{next}";
    }

    /// <summary>O grau de uma pilha de divisões: o da experiência média, que é o que o contador do mapa
    /// mostra — uma pilha não tem um XP só, e o que interessa de longe é como está a força toda.</summary>
    public static VeterancyDef? Stack(World w, IEnumerable<Division> divs)
    {
        float sum = 0f; int n = 0;
        foreach (var d in divs) { sum += d.Xp; n++; }
        return n == 0 ? null : Tier(w, sum / n);
    }
}
