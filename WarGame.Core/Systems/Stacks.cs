using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A PILHA: o que está numa província, lido como uma coisa só.
///
/// O mapa já desenhava contadores, mas eles eram uma pintura: sabiam dizer quantas divisões ali estavam e
/// em que estado de organização, e mais nada. No HoI4 o contador é a própria unidade — diz o que ela está a
/// fazer (a marchar, a bater-se, cercada, a ir de comboio) e é nele que se toca para lhe dar ordens. Sem
/// isto, o jogador tinha de abrir a ficha da região para saber se aquela caixa estava parada ou a andar.
///
/// Isto é estado derivado: não entra no save, não é ISystem, não tem migração. Quem pergunta, mede na hora.
///
/// Os estados vêm da tabela `stack_state` — nome, chapa desenhada e a ordem por que se pergunta. O C# só
/// sabe medir se cada um se aplica; qual é o mais grave, como se chama e que chapa leva é coisa da base de
/// dados. Estado novo = uma linha de SQL mais um caso no <see cref="Holds"/>.</summary>
public static class Stacks
{
    /// <summary>A leitura de uma pilha: quantos são, como estão, e o que estão a fazer.</summary>
    public readonly record struct StackRead(int Count, float Org, float Hp, float Kit, float Entrench,
                                            string State, string Name, string Glyph);

    /// <summary>A pilha que `countryId` tem em `regionId`, ou null se não tem lá nada.</summary>
    public static StackRead? In(World w, int regionId, int countryId)
    {
        if (!w.Regions.TryGetValue(regionId, out var r)) return null;
        var stack = r.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>()
                     .Where(d => d.CountryId == countryId).ToList();
        return stack.Count == 0 ? null : Read(w, r, stack);
    }

    /// <summary>A leitura de uma pilha já escolhida — é por aqui que o contador do mapa entra, que ele já
    /// escolheu a maior força da província antes de perguntar.</summary>
    public static StackRead Read(World w, Region r, IReadOnlyList<Division> stack)
    {
        string id = State(w, r, stack);
        var def = w.StackStates.GetValueOrDefault(id);
        return new StackRead(stack.Count,
                             stack.Average(d => d.Org) / 100f,
                             stack.Average(d => d.Hp) / 100f,
                             stack.Average(d => d.Kit),
                             stack.Average(d => d.Entrench),
                             id, def?.Name ?? "", def?.Glyph ?? "");
    }

    /// <summary>O estado desta pilha: pergunta-se pela ordem da tabela e o primeiro que se aplica ganha.
    /// Sem tabela nenhuma carregada devolve string vazia — o contador desenha-se à mesma, só sem chapa.</summary>
    public static string State(World w, Region r, IReadOnlyList<Division> stack)
    {
        foreach (var s in w.StackStates.Values.OrderBy(s => s.Sort).ThenBy(s => s.Id, StringComparer.Ordinal))
            if (Holds(w, r, stack, s.Id)) return s.Id;
        return "";
    }

    /// <summary>Este estado aplica-se a esta pilha? É a única coisa que o C# sabe destes ids — o nome, a
    /// chapa e a gravidade estão na tabela.</summary>
    private static bool Holds(World w, Region r, IReadOnlyList<Division> stack, string id) => id switch
    {
        "cercada" => stack.Any(d => d.Cut || d.PocketDays > 0),
        "combate" => w.ActiveBattles.Any(b => b.RegionId == r.Id),
        "voo" => stack.Any(d => d.InFlight),
        "mar" => stack.Any(d => d.Seaborne),
        "comboio" => stack.Any(d => d.Redeploying),
        "marcha" => stack.Any(d => d.Path.Count > 0),
        "cavada" => stack.Average(d => d.Entrench) >= 1f,
        "parada" => true,
        _ => false,
    };

    /// <summary>--smoke: quantas pilhas do jogador estão em cada estado, do mais grave ao mais banal.</summary>
    public static string Report(World w, int countryId)
    {
        var count = new Dictionary<string, int>();
        foreach (var r in w.Regions.Values)
            if (In(w, r.Id, countryId) is StackRead s && s.State.Length > 0)
                count[s.State] = count.GetValueOrDefault(s.State) + 1;
        var order = w.StackStates.Values.OrderBy(s => s.Sort).ThenBy(s => s.Id, StringComparer.Ordinal)
                     .Where(s => count.ContainsKey(s.Id)).Select(s => $"{s.Name.ToLowerInvariant()} {count[s.Id]}");
        return string.Join(", ", order) is { Length: > 0 } line ? line : "nenhuma";
    }
}
