using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Anda pelo Division.Path salto a salto; entra em região hostil defendida → abre/junta-se a Battle;
/// hostil vazia → captura (RegionCaptured). Divisões em região capturada pelo inimigo recuam ou rendem-se.
/// Regras: move_base_days, move_cost:&lt;terreno&gt;, move_infra_floor. Dias ÷ country_stat move_speed.
/// A estação do ano multiplica os dias de marcha (World.SeasonMove), por mar como por terra.</summary>
public sealed class MovementSystem : ISystem
{
    public string Name => "Movement";

    public void Tick(World w)
    {
        // Quem está em batalha não anda nem recua. Um HashSet por tick, nunca InBattle por divisão (O(n×batalhas)).
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));
        Retreat(w, inBattle);

        foreach (var d in w.Divisions.Values)
        {
            // no ar não se marcha: os pára-quedistas estão dentro dos aviões (ParadropSystem)
            if (d.Path.Count == 0 || d.InFlight || inBattle.Contains(d.Id)) continue;
            // o comboio parou: a frente moveu-se por cima da linha e o salto seguinte já não é terra nossa
            if (Redeploy.Derailed(w, d)) { Redeploy.Stop(d); d.ClearPath(); continue; }
            var target = w.Regions[d.Path[0]];
            var origin = w.Regions[d.RegionId];
            bool bySea = w.IsSeaHop(origin.Id, target.Id);
            d.MoveProgress += 1f / HopDays(w, d, origin, target);
            if (d.MoveProgress < 1f) continue;

            if (w.CanTraverse(d.CountryId, target)) Enter(w, d, target, inBattle, bySea);
            else if (w.IsHostile(d.CountryId, target)) Attack(w, d, target, inBattle, bySea);
            else d.ClearPath();   // terceiro (nem nosso, nem aliado, nem inimigo): pára à fronteira
        }
    }

    /// <summary>Dias que esta divisão leva a entrar naquela região vinda desta. Por mar é a distância pela
    /// velocidade dos transportes (com um mínimo, que embarcar e desembarcar também leva tempo); por terra é
    /// a base a dividir pela mobilidade da divisão, vezes o custo do terreno, a dividir pela infraestrutura
    /// (com chão) e pela velocidade de marcha do país e do comando. A estação do ano multiplica tudo: no
    /// Inverno as colunas atolam-se.
    ///
    /// É público porque não serve só para andar: o mapa usa-o para dizer ao jogador quantos dias faltam para
    /// a divisão chegar ao fim da rota, e a conta tem de ser a mesma que o mundo faz — uma estimativa que
    /// mentisse era pior do que não haver estimativa nenhuma.</summary>
    public static float HopDays(World w, Division d, Region origin, Region target)
    {
        float days;
        if (w.IsSeaHop(origin.Id, target.Id))
            days = MathF.Max(w.Rule("sea_min_days", 2f), origin.SeaNeighbours[target.Id] / w.Rule("sea_speed_kmd", 400f));
        else
            days = w.Rule("move_base_days", 80f) / w.Stats.Get(d.TemplateId)["mobility"] * w.MoveCost(target.Terrain)
                   / MathF.Max(w.Rule("move_infra_floor", 0.5f), target.Infrastructure)
                   / (w.Countries[d.CountryId].Stat("move_speed") * w.CommandMult(d, "move_speed"));
        // pelos carris fazem-se os mesmos saltos numa fracção do tempo (Redeploy): é a razão de ser do
        // redespacho, e a conta tem de ser esta mesma para o mapa não prometer uma data e o mundo cumprir outra
        return MathF.Max(0.01f, days * Redeploy.Speed(w, d) / w.SeasonMove);
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
                if (!w.CanTraverse(d.CountryId, r)) continue;
                int own = r.DivisionIds.Count(id => w.Divisions[id].CountryId == d.CountryId)
                          + (r.ControllerId == d.CountryId ? 1000 : 0);   // território próprio antes do de aliado
                if (own > bestOwn || (own == bestOwn && n < best)) { best = n; bestOwn = own; }
            }
            if (best < 0) { w.Events.Publish(new DivisionDestroyed(d.Id)); w.RemoveDivision(d.Id); }
            else { w.PlaceDivision(d, best); d.ClearPath(); }
        }
    }

    /// <summary>Entra em região própria; se um inimigo a está a atacar, reforça a defesa.
    /// Vindo do mar, desembarcar custa naval_invasion_org_cost de organização.</summary>
    private static void Enter(World w, Division d, Region target, HashSet<int> inBattle, bool bySea = false)
    {
        if (bySea) Disembark(w, d);
        w.PlaceDivision(d, target.Id); d.AdvanceHop();
        if (d.Path.Count == 0) Redeploy.Stop(d);      // chegou ao destino: desce do comboio
        foreach (var b in w.ActiveBattles)
            if (b.RegionId == target.Id && w.AreAtWar(d.CountryId, b.AttackerCountryId) && !b.Defenders.Contains(d.Id))
            { b.Defenders.Add(d.Id); inBattle.Add(d.Id); }
    }

    /// <summary>Chega à fronteira de região inimiga: com defensores abre/junta-se à batalha e espera na origem
    /// (o CombatSystem captura quando os defensores caem); vazia → captura e entra.</summary>
    private static void Attack(World w, Division d, Region target, HashSet<int> inBattle, bool bySea = false)
    {
        if (!d.CanFight) { d.ClearPath(); return; }
        if (bySea && !CanLand(w, d, target)) return;
        // Todos os inimigos contam, mesmo sem CanFight — o CombatSystem filtra e decide.
        var defenders = target.DivisionIds.Where(id => w.AreAtWar(d.CountryId, w.Divisions[id].CountryId)).ToList();
        if (defenders.Count == 0)
        {
            if (bySea) Disembark(w, d);
            int old = target.ControllerId; target.ControllerId = d.CountryId;
            CombatSystem.CaptureDamage(w, target);
            w.NoteWarProgress(old, d.CountryId);
            w.Events.Publish(new RegionCaptured(target.Id, old, d.CountryId));
            d.Captures++;                       // entrar numa região inimiga vazia também é tomá-la
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

    /// <summary>Desembarcar desorganiza a tropa: perde naval_invasion_org_cost de organização.</summary>
    private static void Disembark(World w, Division d) =>
        d.Org = MathF.Max(0f, d.Org - w.Rule("naval_invasion_org_cost", 25f));

    /// <summary>Assalto a uma costa inimiga: só embarca quem tem organização acima de
    /// naval_invasion_min_org (senão desiste e fica em casa) e cabem naval_invasion_max_divs
    /// divisões por praia ao mesmo tempo — as restantes esperam ao largo.</summary>
    private static bool CanLand(World w, Division d, Region target)
    {
        if (d.Org < w.Rule("naval_invasion_min_org", 45f))
        {
            d.ClearPath();
            w.Events.Publish(new LandingAborted(d.Id, target.Id));
            return false;
        }
        var b = w.BattleAt(target.Id, d.CountryId);
        if (b is null || b.Attackers.Contains(d.Id)) return true;
        int landing = b.Attackers.Count(id => w.Divisions.TryGetValue(id, out var a) && w.IsSeaHop(a.RegionId, target.Id));
        if (landing < (int)w.Rule("naval_invasion_max_divs", 3f)) return true;
        d.MoveProgress = 1f;   // praia cheia: espera ao largo pela vaga seguinte
        return false;
    }
}
