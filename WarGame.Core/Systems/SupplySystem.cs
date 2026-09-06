using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Supply de cada divisão (0..1). Contrato: corre antes de tudo no tick; escreve só Division.Supply.
/// Regras em World.Rules: supply_pocket, supply_stack.</summary>
public sealed class SupplySystem : ISystem
{
    public string Name => "Supply";
    public void Tick(World w) { /* TODO(workflow): bolsas sem ligação a território próprio + empilhamento */ }
}
