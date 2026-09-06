using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Defesa da retaguarda. Desde que há sabotagem por região, mandar uma equipa rebentar o cais do
/// inimigo era um negócio sem risco: pagava-se, esperava-se e o estrago acontecia sempre. Agora, todos os
/// dias que a equipa passa em terreno inimigo é um dia em que pode ser apanhada — e ser apanhada custa a
/// operação inteira, sem devolver o que se pagou.
///
/// O que faz a diferença é quem guarda a região: cada divisão do dono ali estacionada acrescenta
/// catch_guard à hipótese diária, e a lei de segurança do país (counter_intel) multiplica tudo. É isto que
/// dá uso a tropa na retaguarda — uma frente sem reservas atrás fica com as pontes por sua conta.
///
/// Só apanha operações de scope 'region': as redes de gabinete contra o país inteiro continuam a ser
/// tratadas pela contra-espionagem (purge_spies), que as expulsa de uma vez.
///
/// Regras: catch_base, catch_guard, catch_max.</summary>
public sealed class CounterIntelSystem : ISystem
{
    public string Name => "CounterIntel";

    public void Tick(World w)
    {
        if (w.ActiveSpyOps.Count == 0) return;
        foreach (var o in w.ActiveSpyOps.ToList())
        {
            if (o.RegionId == 0 || !w.ActiveSpyOps.Contains(o)) continue;
            if (!w.SpyOps.TryGetValue(o.OpId, out var op) || !op.IsRegional) continue;
            if (!w.Regions.TryGetValue(o.RegionId, out var r) || r.ControllerId != o.TargetCountryId) continue;

            if (w.Rng.NextDouble() >= Chance(w, o.TargetCountryId, r)) continue;
            w.ActiveSpyOps.Remove(o);
            w.Events.Publish(new SabotageFoiled(o.CountryId, o.TargetCountryId, o.OpId, r.Id));
        }
    }

    /// <summary>Hipótese diária de a guarnição desta região apanhar uma equipa: base + catch_guard por
    /// divisão do dono ali estacionada, tudo × counter_intel do país, com tecto em catch_max.</summary>
    public static float Chance(World w, int countryId, Region r)
    {
        float chance = w.Rule("catch_base", 0.03f) + Guards(w, countryId, r) * w.Rule("catch_guard", 0.02f);
        if (w.Countries.TryGetValue(countryId, out var c)) chance *= c.Stat("counter_intel");
        return Math.Clamp(chance, 0f, w.Rule("catch_max", 0.35f));
    }

    /// <summary>Divisões do dono da região que lá estão de guarnição.</summary>
    public static int Guards(World w, int countryId, Region r) =>
        r.DivisionIds.Count(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == countryId);
}
