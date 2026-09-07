using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Pool de homens (HoI4: manpower). Cresce por dia com a população das regiões controladas
/// (rule manpower_per_million_daily × country_stat conscription) até um tecto
/// (população × manpower_cap_share × conscription; a terra ocupada entra pela fatia que a política de
/// ocupação daquele povo deixa recrutar — OccupationSystem). Country.Manpower = -1 significa "por
/// inicializar" (jogo novo ou save antigo): o primeiro tick põe o pool em tecto ×
/// manpower_start_share. Gastam-no ProductionSystem (divisões novas) e RecoverySystem (reforços).
/// </summary>
public sealed class ManpowerSystem : ISystem
{
    public string Name => "Manpower";

    public void Tick(World w)
    {
        float perMillion = w.Rule("manpower_per_million_daily", 60f);
        float capShare = w.Rule("manpower_cap_share", 0.05f);
        float startShare = w.Rule("manpower_start_share", 0.5f);

        // Uma passagem pelas regiões, população por controlador (como o EconomySystem).
        // a população de terra ocupada só conta a fatia que a política de ocupação daquele povo deixa recrutar
        var pop = new Dictionary<int, float>();
        foreach (var r in w.Regions.Values)
            pop[r.ControllerId] = pop.GetValueOrDefault(r.ControllerId) + r.Population * OccupationSystem.ManpowerMult(w, r);

        foreach (var c in w.Countries.Values)
        {
            if (c.Capitulated) continue;
            var p = pop.GetValueOrDefault(c.Id);
            float cap = p * capShare * c.Stat("conscription");
            if (c.Manpower < 0f) { c.Manpower = cap * startShare; continue; }
            c.Manpower = MathF.Min(cap, c.Manpower + p / 1e6f * perMillion * c.Stat("conscription") * c.StabilityFactor);
        }
    }
}
