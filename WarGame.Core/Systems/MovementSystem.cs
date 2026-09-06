using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Anda pelo Division.Path salto a salto; entra em região hostil defendida → abre/junta-se a Battle;
/// hostil vazia → captura (RegionCaptured). Divisões em região capturada pelo inimigo recuam ou rendem-se.
/// Regras: move_base_days, move_cost:&lt;terreno&gt;.</summary>
public sealed class MovementSystem : ISystem
{
    public string Name => "Movement";
    public void Tick(World w) { /* TODO(workflow) */ }
}
