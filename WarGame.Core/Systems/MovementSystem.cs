using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Anda pelo Division.Path salto a salto; entra em região hostil defendida → abre/junta-se a Battle;
/// hostil vazia → captura (RegionCaptured). Divisões em região capturada pelo inimigo recuam ou rendem-se.
/// Regras: move_base_days, move_cost:&lt;terreno&gt;, move_infra_floor.</summary>
public sealed class MovementSystem : ISystem
{
    public string Name => "Movement";

    public void Tick(World w)
    {
        // Quem está em batalha não anda nem recua. Um HashSet por tick, nunca InBattle por divisão (O(n×batalhas)).
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));
        Retreat(w, inBattle);

        float baseDays = w.Rule("move_base_days", 80f), infraFloor = w.Rule("move_infra_floor", 0.5f);
        foreach (var d in w.Divisions.Values)
        {
            if (d.Path.Count == 0 || inBattle.Contains(d.Id)) continue;
            var target = w.Regions[d.Path[0]];
            // dias para entrar = base / mobilidade × custo do terreno / infraestrutura (com chão)
            float days = baseDays / w.Stats.Get(d.TemplateId)["mobility"] * w.MoveCost(target.Terrain)
                         / MathF.Max(infraFloor, target.Infrastructure);
            d.MoveProgress += 1f / days;
            if (d.MoveProgress < 1f) continue;

            if (target.ControllerId == d.CountryId) Enter(w, d, target, inBattle);
            else if (w.IsHostile(d.CountryId, target)) Attack(w, d, target, inBattle);
            else d.ClearPath();   // terceiro (nem nosso nem inimigo): pára à fronteira
        }
    }

    /// <summary>Divisão fora de batalha em região inimiga (acabou de ser capturada) recua para a região própria
    /// adjacente com mais divisões próprias (empate: menor id). Sem saída, rende-se.</summary>
    private static void Retreat(World w, HashSet<int> inBattle)
    {
        List<Division>? exposed = null;   // recolhe primeiro: RemoveDivision muta w.Divisions
        foreach (var d in w.Divisions.Values)
            if (!inBattle.Contains(d.Id) && w.IsHostile(d.CountryId, w.Regions[d.RegionId])) (exposed ??= new()).Add(d);
        if (exposed is null) return;

        foreach (var d in exposed)
        {
            int best = -1, bestOwn = -1;
            foreach (var n in w.Regions[d.RegionId].Neighbours)
            {
                var r = w.Regions[n];
                if (r.ControllerId != d.CountryId) continue;
                int own = r.DivisionIds.Count(id => w.Divisions[id].CountryId == d.CountryId);
                if (own > bestOwn || (own == bestOwn && n < best)) { best = n; bestOwn = own; }
            }
            if (best < 0) { w.Events.Publish(new DivisionDestroyed(d.Id)); w.RemoveDivision(d.Id); }
            else { w.PlaceDivision(d, best); d.ClearPath(); }
        }
    }

    /// <summary>Entra em região própria; se um inimigo a está a atacar, reforça a defesa.</summary>
    private static void Enter(World w, Division d, Region target, HashSet<int> inBattle)
    {
        w.PlaceDivision(d, target.Id); d.AdvanceHop();
        foreach (var b in w.ActiveBattles)
            if (b.RegionId == target.Id && w.AreAtWar(d.CountryId, b.AttackerCountryId) && !b.Defenders.Contains(d.Id))
            { b.Defenders.Add(d.Id); inBattle.Add(d.Id); }
    }

    /// <summary>Chega à fronteira de região inimiga: com defensores abre/junta-se à batalha e espera na origem
    /// (o CombatSystem captura quando os defensores caem); vazia → captura e entra.</summary>
    private static void Attack(World w, Division d, Region target, HashSet<int> inBattle)
    {
        if (!d.CanFight) { d.ClearPath(); return; }
        // Todos os inimigos contam, mesmo sem CanFight — o CombatSystem filtra e decide.
        var defenders = target.DivisionIds.Where(id => w.AreAtWar(d.CountryId, w.Divisions[id].CountryId)).ToList();
        if (defenders.Count == 0)
        {
            int old = target.ControllerId; target.ControllerId = d.CountryId;
            w.Events.Publish(new RegionCaptured(target.Id, old, d.CountryId));
            w.PlaceDivision(d, target.Id); d.AdvanceHop();
            return;
        }
        var b = w.BattleAt(target.Id, d.CountryId);
        if (b is null)
        {
            b = new Battle { RegionId = target.Id, AttackerCountryId = d.CountryId };
            b.Defenders.AddRange(defenders); inBattle.UnionWith(defenders);
            w.ActiveBattles.Add(b); w.Events.Publish(new BattleStarted(target.Id));
        }
        if (!b.Attackers.Contains(d.Id)) { b.Attackers.Add(d.Id); inBattle.Add(d.Id); }
        d.MoveProgress = 1f;   // fica na origem; entra no tick a seguir à vitória
    }
}
