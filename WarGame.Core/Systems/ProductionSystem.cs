using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Gasta Country.Money nas encomendas (Country.Queue) e cria divisões na capital. Regras: build_min_days, new_division_org.</summary>
public sealed class ProductionSystem : ISystem
{
    public string Name => "Production";
    public void Tick(World w) { /* TODO(workflow): progresso ≤ custo/build_min_days por dia por encomenda; pronta → AddDivision na capital controlada */ }
}
