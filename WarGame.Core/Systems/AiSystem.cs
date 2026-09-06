using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Países sem jogador: produção, guarnição de fronteira, ataques com superioridade. Só via ICommand.
/// Regras: ai_period_days, ai_attack_ratio, ai_max_queue.</summary>
public sealed class AiSystem : ISystem
{
    public string Name => "AI";
    public void Tick(World w) { /* TODO(workflow) */ }
}
