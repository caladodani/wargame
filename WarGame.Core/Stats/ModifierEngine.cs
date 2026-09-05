namespace WarGame.Core.Stats;

/// <summary>Contexto avaliado pelos modificadores (terreno, ar, tech, clima…). Só strings — data-driven.</summary>
public sealed class ModContext : Dictionary<string, string>
{
    public ModContext With(string key, string value) { this[key] = value; return this; }
}

/// <summary>Pipeline genérico: base → +flat → ×mult. Indexado por StatKey para O(1) por lookup.</summary>
public sealed class ModifierEngine
{
    private readonly Dictionary<string, List<Modifier>> _byKey;

    public ModifierEngine(IEnumerable<Modifier> mods)
    {
        _byKey = mods.GroupBy(m => m.StatKey).ToDictionary(g => g.Key, g => g.ToList());
    }

    public (float flat, float mul) Evaluate(string statKey, StatBlock stats, ModContext ctx)
    {
        float flat = 0f, mul = 1f;
        if (!_byKey.TryGetValue(statKey, out var list)) return (flat, mul);
        foreach (var m in list)
        {
            if (m.RequiredTag is not null && !stats.Tags.Contains(m.RequiredTag)) continue;
            if (m.ConditionKey is not null &&
                (!ctx.TryGetValue(m.ConditionKey, out var v) || v != m.ConditionValue)) continue;
            if (m.Op == ModOp.Add) flat += m.Value; else mul *= m.Value;
        }
        return (flat, mul);
    }
}
