using WarGame.Core.Model;

namespace WarGame.Core.Systems;

// Esqueletos — cada um cresce sem tocar nos outros.
public sealed class SupplySystem : ISystem     { public string Name => "Supply";     public void Tick(World w) { /* linhas de suprimento por infra/portos */ } }
public sealed class EconomySystem : ISystem    { public string Name => "Economy";    public void Tick(World w) { /* recursos, fábricas, orçamento */ } }
public sealed class ProductionSystem : ISystem { public string Name => "Production"; public void Tick(World w) { /* filas, stock de equipamento */ } }
public sealed class MovementSystem : ISystem   { public string Name => "Movement";   public void Tick(World w) { /* TargetRegionId → mover, abrir Battle se hostil */ } }
public sealed class AiSystem : ISystem         { public string Name => "AI";         public void Tick(World w) { /* máquina de estados por país */ } }
