using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Rendimento diário em pontos de produção → Country.Money. Regras: points_per_million, occupied_yield.</summary>
public sealed class EconomySystem : ISystem
{
    public string Name => "Economy";
    public void Tick(World w) { /* TODO(workflow): Σ regiões controladas: pop/1e6 × points_per_million × infra (× occupied_yield se ocupada) */ }
}
