using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Rendimento diário em pontos de produção → Country.Money (HoI4: fábricas; aqui população × infra).
/// Região controlada rende pop/1e6 × points_per_million × infra; ocupada (controlador ≠ dono) rende × occupied_yield × stat occupied_yield do ocupante (leis de ocupação) × a política de ocupação daquele povo (OccupationSystem) × (1 − resistência × resistance_output_hit).
/// Terreno pesa no rendimento (regras terrain_income_&lt;terreno&gt;, 1 = neutro) e a costa soma coastal_income_bonus.
/// Total × country_stat industry (1 = neutro; automático por PIB per capita no import). Regras: points_per_million, occupied_yield.</summary>
public sealed class EconomySystem : ISystem
{
    public string Name => "Economy";

    public void Tick(World w)
    {
        // Uma passagem pelas regiões, acumulando por controlador (Income() por país seria O(países × regiões)).
        float perMillion = w.Rule("points_per_million", 0.1f), occupied = w.Rule("occupied_yield", 0.5f), resistHit = w.Rule("resistance_output_hit", 0.5f);
        var income = new Dictionary<int, float>();
        foreach (var r in w.Regions.Values)
        {
            float y = Yield(w, r, perMillion, occupied, resistHit);
            if (r.ControllerId != r.OwnerId && w.Countries.TryGetValue(r.ControllerId, out var oc)) y *= oc.Stat("occupied_yield");
            income[r.ControllerId] = income.GetValueOrDefault(r.ControllerId) + y;
        }
        foreach (var (countryId, v) in income)
            if (w.Countries.TryGetValue(countryId, out var c)) c.Money += v * c.Stat("industry") * c.StabilityFactor;
    }

    /// <summary>Rendimento diário de um país (UI: "+X/dia"). Sem regiões controladas → 0.</summary>
    public static float Income(World w, int countryId)
    {
        float perMillion = w.Rule("points_per_million", 0.1f), occupied = w.Rule("occupied_yield", 0.5f), resistHit = w.Rule("resistance_output_hit", 0.5f), sum = 0f;
        float occMult = w.Countries.TryGetValue(countryId, out var oc) ? oc.Stat("occupied_yield") : 1f;
        foreach (var r in w.Regions.Values)
            if (r.ControllerId == countryId) sum += Yield(w, r, perMillion, occupied, resistHit) * (r.ControllerId == r.OwnerId ? 1f : occMult);
        return sum * (w.Countries.TryGetValue(countryId, out var cc) ? cc.Stat("industry") * cc.StabilityFactor : 1f);
    }

    private static float Yield(World w, Region r, float perMillion, float occupied, float resistHit) =>
        r.Population / 1e6f * perMillion * r.Infrastructure * TerrainMult(w, r)
        // a política de ocupação (OccupationSystem) diz se se espreme a terra tomada ou se se a deixa em paz
        * (r.ControllerId == r.OwnerId ? 1f : occupied * OccupationSystem.YieldMult(w, r) * (1f - r.Resistance * resistHit));

    /// <summary>Peso económico do terreno (regra terrain_income_&lt;terreno&gt;) e da costa (coastal_income_bonus).</summary>
    public static float TerrainMult(World w, Region r) =>
        w.Rule("terrain_income_" + r.Terrain, 1f) * (r.Coastal ? w.Rule("coastal_income_bonus", 1f) : 1f);

    /// <summary>Rendimento diário de uma região para o controlador actual (UI da região).</summary>
    public static float RegionIncome(World w, Region r)
    {
        float y = Yield(w, r, w.Rule("points_per_million", 0.1f), w.Rule("occupied_yield", 0.5f), w.Rule("resistance_output_hit", 0.5f));
        if (r.ControllerId != r.OwnerId && w.Countries.TryGetValue(r.ControllerId, out var oc)) y *= oc.Stat("occupied_yield");
        return y * (w.Countries.TryGetValue(r.ControllerId, out var c) ? c.Stat("industry") * c.StabilityFactor : 1f);
    }
}
