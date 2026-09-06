using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Rendimento diário em pontos de produção → Country.Money (HoI4: fábricas; aqui população × infra).
/// Região controlada rende pop/1e6 × points_per_million × infra; ocupada (controlador ≠ dono) rende × occupied_yield.
/// Total × country_stat industry (1 = neutro; automático por PIB per capita no import). Regras: points_per_million, occupied_yield.</summary>
public sealed class EconomySystem : ISystem
{
    public string Name => "Economy";

    public void Tick(World w)
    {
        // Uma passagem pelas regiões, acumulando por controlador (Income() por país seria O(países × regiões)).
        float perMillion = w.Rule("points_per_million", 0.1f), occupied = w.Rule("occupied_yield", 0.5f);
        var income = new Dictionary<int, float>();
        foreach (var r in w.Regions.Values)
            income[r.ControllerId] = income.GetValueOrDefault(r.ControllerId) + Yield(r, perMillion, occupied);
        foreach (var (countryId, v) in income)
            if (w.Countries.TryGetValue(countryId, out var c)) c.Money += v * c.Stat("industry");
    }

    /// <summary>Rendimento diário de um país (UI: "+X/dia"). Sem regiões controladas → 0.</summary>
    public static float Income(World w, int countryId)
    {
        float perMillion = w.Rule("points_per_million", 0.1f), occupied = w.Rule("occupied_yield", 0.5f), sum = 0f;
        foreach (var r in w.Regions.Values)
            if (r.ControllerId == countryId) sum += Yield(r, perMillion, occupied);
        return sum * (w.Countries.TryGetValue(countryId, out var c) ? c.Stat("industry") : 1f);
    }

    private static float Yield(Region r, float perMillion, float occupied) =>
        r.Population / 1e6f * perMillion * r.Infrastructure * (r.ControllerId == r.OwnerId ? 1f : occupied);
}
