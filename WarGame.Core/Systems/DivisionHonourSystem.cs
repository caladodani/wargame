using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Dá nome próprio às divisões veteranas. As condecorações acumulam-se e medem-se em força; a honra
/// de batalha é outra coisa — é UMA só, é do sítio onde foi merecida ("Leões de Braga") e passa a andar
/// colada ao nome da divisão até ao fim da campanha. Uma honra maior substitui a que estava.
///
/// O que dá em jogo é moral, não força bruta: uma tropa com nome recompõe-se mais depressa fora de combate
/// (RecoverySystem). Os moldes, os limiares e o bónus vivem na tabela division_honour — inventar uma honra
/// nova é uma linha de SQL.</summary>
public sealed class DivisionHonourSystem : ISystem
{
    public string Name => "DivisionHonours";

    public void Tick(World w)
    {
        if (w.HonourDefs.Count == 0) return;
        int period = Math.Max(1, (int)w.Rule("honour_check_days", 3f));
        if (w.Clock.Day % period != 0) return;

        foreach (var d in w.Divisions.Values)
        {
            var best = Earned(w, d);
            if (best is null || best.Id == d.Honour) continue;
            if (w.HonourDefs.TryGetValue(d.Honour ?? "", out var cur) && cur.Sort >= best.Sort) continue;

            d.Honour = best.Id;
            d.HonourName = Title(w, d, best);
            w.Events.Publish(new DivisionHonoured(d.Id, d.CountryId, best.Id, d.HonourName));
        }
    }

    /// <summary>A honra mais alta que os feitos da divisão já pagam, ou null se ainda não merece nenhuma.</summary>
    public static HonourDef? Earned(World w, Division d)
    {
        HonourDef? best = null;
        foreach (var h in w.HonourDefs.Values)
            if (Metric(d, h.Metric) >= h.Threshold && (best is null || h.Sort > best.Sort)) best = h;
        return best;
    }

    /// <summary>Resolve o molde com a região onde a divisão está no momento em que a merece — é essa que fica
    /// no nome. Sem região conhecida, fica só o molde sem o lugar.</summary>
    private static string Title(World w, Division d, HonourDef h)
    {
        string place = w.Regions.TryGetValue(d.RegionId, out var r) ? r.Name : "";
        return place.Length == 0
            ? h.Title.Replace(" de {r}", "").Replace("{r}", "").Trim()
            : h.Title.Replace("{r}", place);
    }

    private static float Metric(Division d, string metric) => metric switch
    {
        "xp" => d.Xp,
        "battles" => d.Battles,
        "captures" => d.Captures,
        "medals" => d.Medals.Count,
        _ => float.MinValue,      // métrica desconhecida na base de dados: nunca dá honra nenhuma
    };

    /// <summary>Quanto mais depressa a divisão se recompõe por ter nome, cortado em honour_bonus_max.</summary>
    public static float Bonus(World w, Division d) =>
        d.Honour is string id && w.HonourDefs.TryGetValue(id, out var h)
            ? MathF.Min(h.Bonus, w.Rule("honour_bonus_max", 0.5f))
            : 0f;
}
