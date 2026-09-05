namespace WarGame.Core.Stats;

/// <summary>Stats dinâmicos: nenhuma propriedade fixa, chaves vêm da tabela unit_stat.</summary>
public sealed class StatBlock
{
    private readonly Dictionary<string, float> _values = new();
    public HashSet<string> Tags { get; } = new();

    public float this[string key]
    {
        get => _values.TryGetValue(key, out var v) ? v : 0f;
        set => _values[key] = value;
    }

    public bool Has(string key) => _values.ContainsKey(key);
    public IReadOnlyDictionary<string, float> All => _values;
}
