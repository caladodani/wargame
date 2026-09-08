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

    /// <summary>População que este país pode recrutar: a das regiões que controla, e da terra ocupada só a
    /// fatia que a política de ocupação deixa. É a mesma passagem que o Tick faz, mas para um país só — a UI
    /// precisa dela para dizer de onde vêm os homens sem refazer a conta.</summary>
    public static float Pop(World w, int countryId)
    {
        float pop = 0f;
        foreach (var r in w.Regions.Values)
            if (r.ControllerId == countryId) pop += r.Population * OccupationSystem.ManpowerMult(w, r);
        return pop;
    }

    /// <summary>Tecto do pool: a população recrutável × manpower_cap_share × recruta do país.</summary>
    public static float Cap(World w, Country c, float pop) =>
        pop * w.Rule("manpower_cap_share", 0.05f) * c.Stat("conscription");

    /// <summary>Homens que entram no pool por dia, antes do tecto: por milhão de gente, à velocidade da
    /// regra, vezes a recruta e a estabilidade.</summary>
    public static float Gain(World w, Country c, float pop) =>
        pop / 1e6f * w.Rule("manpower_per_million_daily", 60f) * c.Stat("conscription") * c.StabilityFactor;

    public void Tick(World w)
    {
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
            float cap = Cap(w, c, p);
            if (c.Manpower < 0f) { c.Manpower = cap * startShare; continue; }
            c.Manpower = MathF.Min(cap, c.Manpower + Gain(w, c, p));
        }
    }
}
