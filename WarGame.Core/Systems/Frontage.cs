using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Largura de frente à maneira do HoI4: numa batalha não cabe tropa a mais. O terreno diz quantas
/// divisões chegam a tocar no inimigo — uma planície abre alas, um desfiladeiro de montanha deixa passar
/// três — e as restantes ficam em reserva, atrás, sem bater nem apanhar.
///
/// Antes disto, empilhar divisões numa região era sempre bom: todas batiam ao mesmo tempo e a única penalização
/// era um arranhão no comando. Uma pilha de dez tomava qualquer coisa. Agora a montanha defende-se com poucos
/// homens, como deve ser, e o excedente serve para render quem está feito em cacos.
///
/// A rendição é automática e não precisa de ordens: a linha é escolhida todos os dias pelo estado da tropa
/// (organização + vida), por isso a divisão partida cai para a reserva e a que descansou entra no lugar dela.
///
/// As larguras vivem em regras da base de dados (front_width_&lt;terreno&gt;, com front_width por omissão e
/// front_width_river a apertar a passagem de rio). Estado derivado: não guarda nada e não é ISystem.</summary>
public static class Frontage
{
    /// <summary>Quantas divisões de cada lado cabem na frente desta região.</summary>
    public static int Width(World w, Region r)
    {
        float bas = w.Rule($"front_width_{r.Terrain}", w.Rule("front_width", 4f));
        if (r.River) bas -= w.Rule("front_width_river", 1f);   // atravessar o rio é passar por um funil
        return (int)MathF.Max(w.Rule("front_width_min", 1f), MathF.Round(bas));
    }

    /// <summary>Parte um lado em linha da frente e reserva. A linha leva as divisões mais inteiras: é o que
    /// faz a rendição acontecer sozinha de um dia para o outro.</summary>
    public static (List<Division> Line, List<Division> Reserve) Split(World w, Region r, IEnumerable<Division> divs)
    {
        var order = divs.OrderByDescending(Fitness).ThenBy(d => d.Id).ToList();
        int width = Width(w, r);
        return (order.Take(width).ToList(), order.Skip(width).ToList());
    }

    /// <summary>Quem está em melhor estado para aguentar o dia de hoje.</summary>
    public static float Fitness(Division d) => d.Org + d.Hp;
}
