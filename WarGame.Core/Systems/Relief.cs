using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>O chão desenhado no mapa político: serras, cidades, dunas, mata e gelo marcados por cima da cor
/// do dono, como em qualquer carta militar — e como no HoI4, onde se vê onde é que a coluna vai abrandar sem
/// abrir ficha nenhuma. Até aqui o terreno só existia dentro da ficha da região e no modo de mapa próprio: o
/// mapa de sempre, o que se olha a maior parte do tempo, não dizia por onde é que se anda.
///
/// Só se marca o chão que custa a atravessar (passo acima de relief_mark_min_cost). A planície é o normal e o
/// normal não leva sinal — marcar tudo é o mesmo que não marcar nada, e ainda tapa o mapa. O desenho e a cor
/// saem da linha da tabela terrain, os mesmos da chave do modo Terreno: um chão só tem uma cara.
///
/// Estado derivado: não guarda nada, não entra no save e não é ISystem.</summary>
public static class Relief
{
    /// <summary>O desenho deste chão, ou null quando não se marca (planície, chão por classificar, terreno
    /// sem desenho na tabela).</summary>
    public static string? Mark(World w, Region r) =>
        Of(w, r) is TerrainDef t && t.Glyph.Length > 0 ? t.Glyph : null;

    /// <summary>A cor da linha do terreno, para o desenho não ser outra invenção do mapa. "" quando não se
    /// marca ou quando a tabela não dá cor.</summary>
    public static string Colour(World w, Region r) => Of(w, r) is TerrainDef t ? t.Color : "";

    /// <summary>O chão marcável desta região: a linha da tabela, se houver e se custar a atravessar.</summary>
    public static TerrainDef? Of(World w, Region r) =>
        w.TerrainDefs.TryGetValue(r.Terrain, out var t) && t.MoveCost > w.Rule("relief_mark_min_cost", 1.05f)
            ? t : null;

    /// <summary>Quantos sinais leva uma região desta largura (em unidades de mapa). Uma província pequena
    /// leva um; a Sibéria não pode levar o mesmo, senão fica um símbolo perdido no meio do branco.</summary>
    public static int Marks(float span) => span >= 200f ? 3 : span >= 110f ? 2 : 1;

    /// <summary>Que chão é que o mundo tem marcado, e quantas regiões de cada — a conta que a prova headless
    /// escreve, pela mesma ordem da chave do mapa (do mais fácil de atravessar ao mais duro).</summary>
    public static List<(TerrainDef Ground, int Regions)> Census(World w)
    {
        var count = new Dictionary<string, int>();
        foreach (var r in w.Regions.Values)
            if (Of(w, r) is TerrainDef t) count[t.Id] = count.GetValueOrDefault(t.Id) + 1;
        return w.TerrainDefs.Values.Where(t => count.ContainsKey(t.Id))
                .OrderBy(t => t.MoveCost).ThenBy(t => t.Id)
                .Select(t => (t, count[t.Id])).ToList();
    }
}
