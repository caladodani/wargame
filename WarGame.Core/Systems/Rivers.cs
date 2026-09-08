using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Os rios: onde a água custa a passar e onde ela se vê no mapa.
///
/// O rio já pesava na guerra — o assalto a uma região de rio paga o modificador do chão molhado
/// (BattleField.RiverBite) e a frente aperta em front_width_river — mas o mapa não tinha água nenhuma
/// desenhada: a região dizia "rio" na ficha e no ecrã de batalha, e quem olhava para o mapa não via
/// diferença entre atravessar o Reno e andar a direito pela planície. No HoI4 o rio é traço azul no mapa,
/// e é por causa dele que uma frente se segura onde se segura.
///
/// A água mora entre duas margens: vê-se no traço comum de duas regiões que ambas têm rio. Uma região de
/// rio sem vizinha de rio nenhuma (uma ilha fluvial, um recorte da simplificação) mostra a água em toda a
/// sua orla de terra — mais vale a água estar no sítio aproximado do que não estar de todo.
///
/// Nada disto se guarda: lê-se da coluna region.river a cada olhada, tal como o combate a lê. O desenho e
/// a conta saem da mesma linha, e por isso não podem discordar.</summary>
public static class Rivers
{
    /// <summary>Assaltar para dentro desta região é atravessar rio? É o que o combate cobra —
    /// quanto custa diz-o o BattleField.RiverBite.</summary>
    public static bool Crossing(Region into) => into.River;

    /// <summary>Há água a ver no traço comum destas duas regiões? Só quando as duas margens têm rio: uma
    /// margem sozinha não faz rio nenhum. Simétrico, venha a pergunta do lado que vier.</summary>
    public static bool Between(World w, int a, int b) =>
        a != b
        && w.Regions.TryGetValue(a, out var ra) && w.Regions.TryGetValue(b, out var rb)
        && ra.River && rb.River
        && (ra.Neighbours.Contains(b) || rb.Neighbours.Contains(a));

    /// <summary>As vizinhas com quem esta região partilha margem — o traço por onde a água se desenha.
    /// Vazio para quem não tem rio; a orla de terra toda para a região de rio que não tem vizinha de rio
    /// nenhuma, que é como se diz "a água está aqui algures" sem a inventar num sítio errado.</summary>
    public static List<int> Banks(World w, Region r)
    {
        var banks = new List<int>();
        if (!r.River) return banks;
        foreach (int n in r.Neighbours)
            if (w.Regions.TryGetValue(n, out var o) && o.River) banks.Add(n);
        if (banks.Count > 0) return banks;
        foreach (int n in r.Neighbours) if (w.Regions.ContainsKey(n)) banks.Add(n);
        return banks;
    }

    /// <summary>A linha do rio, como se lê na ficha: o que ele cobra a quem assalta e por onde corre.</summary>
    public static string Line(World w, Region r)
    {
        if (!r.River) return "sem rio";
        int banks = Banks(w, r).Count;
        return $"rio ×{BattleField.RiverBite(w, r):0.00} a quem assalta, {banks} {(banks == 1 ? "margem" : "margens")}";
    }
}
