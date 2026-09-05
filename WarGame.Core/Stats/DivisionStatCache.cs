using WarGame.Core.Data;
using WarGame.Core.Model;

namespace WarGame.Core.Stats;

/// <summary>Agrega stats de batalhões por template. Recalcula só quando marcado dirty (template/tech mudou).</summary>
public sealed class DivisionStatCache
{
    private readonly IUnitRepository _units;
    private readonly Dictionary<int, StatBlock> _cache = new();

    public DivisionStatCache(IUnitRepository units) => _units = units;

    public void Invalidate(int templateId) => _cache.Remove(templateId);
    public void InvalidateAll() => _cache.Clear();

    public StatBlock Get(int templateId)
    {
        if (_cache.TryGetValue(templateId, out var s)) return s;
        s = Aggregate(_units.GetTemplate(templateId));
        _cache[templateId] = s;
        return s;
    }

    private StatBlock Aggregate(DivisionTemplate t)
    {
        var s = new StatBlock();
        var max = new Dictionary<string, float>();
        int n = 0;
        foreach (var (unitTypeId, qty) in t.Units)
        {
            var u = _units.GetUnitType(unitTypeId);
            foreach (var (k, v) in u.Stats.All)
            {
                s[k] += v * qty;
                max[k] = MathF.Max(max.GetValueOrDefault(k), v);
            }
            foreach (var tag in u.Stats.Tags) s.Tags.Add(tag);
            n += qty;
        }
        if (n == 0) return s;
        s["hardness"] /= n;
        s["armor"]    = 0.3f * max.GetValueOrDefault("armor")    + 0.7f * s["armor"] / n;
        s["piercing"] = 0.5f * max.GetValueOrDefault("piercing") + 0.5f * s["piercing"] / n;
        return s;
    }
}
