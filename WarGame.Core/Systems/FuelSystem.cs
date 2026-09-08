using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Combustível: o petróleo que se controla refina-se todos os dias, guarda-se num depósito com
/// fundo, e quem o bebe são as máquinas — blindados, mecanizadas, a aviação e a armada.
///
/// Até aqui o petróleo era só mais um depósito que multiplicava a indústria, e por isso um país sem uma
/// gota de petróleo podia manter um exército inteiro de blindados sem nunca dar por nada. É a diferença
/// entre um jogo de números e um jogo de guerra: no HoI4 a Alemanha ganha batalhas com petróleo romeno e
/// perde-as quando o perde, e uma frota sem combustível é sucata cara ancorada no porto.
///
/// A conta do dia: refina-se `resource.fuel_per_unit` por unidade controlada (o comércio conta — comprar
/// petróleo enche o depósito de quem compra e esvazia o de quem vende), bebe-se `fuel_use` somado por
/// divisão (vem de unit_stat, por isso é a ficha da unidade que decide, não o código), mais a aviação e os
/// navios. Em guerra as máquinas andam e o consumo sobe por `fuel_war_mult`. O que sobra fica no depósito
/// até `fuel_cap_base` + `fuel_cap_days` de produção; o que falta seca-o.
///
/// Quando o dia não paga o dia, o país fica em seca e o combate sabe disso: o `CombatSystem` põe
/// `fuel_out` no contexto e quem cobra é uma linha da tabela modifier — blindados a metade. Nenhum número
/// desta mecânica está em código, nem sequer o nome do petróleo: um recurso é combustível porque a coluna
/// `fuel_per_unit` o diz.</summary>
public sealed class FuelSystem : ISystem
{
    public string Name => "Fuel";

    public void Tick(World w)
    {
        foreach (var c in w.Countries.Values)
        {
            if (c.Capitulated)
            {
                c.Fuel = 0f; c.FuelIn = 0f; c.FuelUse = 0f; c.FuelCap = 0f; c.FuelOut = false;
                continue;
            }
            float prod = Refined(w, c.Id), use = Burned(w, c.Id), cap = Capacity(w, c.Id);
            c.FuelIn = prod; c.FuelUse = use; c.FuelCap = cap;
            // A seca é do dia: o que há no depósito mais o que se refinou hoje não chegou para hoje. Quem
            // tem depósito cheio e produção zero não está em seca — está a viver das reservas, como deve ser.
            float have = c.Fuel + prod;
            c.FuelOut = have + 0.0001f < use;
            c.Fuel = Math.Clamp(have - use, 0f, cap);
        }
    }

    /// <summary>Combustível refinado por dia. `fuel_gain` é o gancho de país (tecnologias, focos, leis) —
    /// uma refinaria melhor é um multiplicador, não outra fórmula.</summary>
    public static float Refined(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return 0f;
        float sum = 0f;
        foreach (var def in w.ResourceDefs.Values)
            if (def.FuelPerUnit > 0f)
                sum += def.FuelPerUnit * ResourceSystem.Available(w, countryId, def.Id);
        return sum * c.Stat("fuel_gain");
    }

    /// <summary>Combustível bebido por dia: divisões (soma de unit_stat fuel_use), aviação e armada. Em
    /// guerra as máquinas mexem-se e o consumo sobe.</summary>
    public static float Burned(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return 0f;
        float ground = 0f;
        foreach (var d in w.Divisions.Values)
            if (d.CountryId == countryId) ground += w.Stats.Get(d.TemplateId)["fuel_use"];
        if (c.AtWarWith.Count > 0) ground *= w.Rule("fuel_war_mult", 1.6f);
        return ground + c.AirPower * w.Rule("fuel_per_air", 0.02f)
                      + c.Warships * w.Rule("fuel_per_ship", 0.05f);
    }

    /// <summary>O que o depósito aguenta: um fundo que todos têm mais os dias de produção que se conseguem
    /// guardar. Sem petróleo o país ainda tem onde meter o que compre.
    ///
    /// Isto tem um dente afiado de propósito: perder os campos de petróleo encolhe o depósito no mesmo dia, e
    /// o que lá estava a mais não tem onde ficar. Quem perde os poços não perde só a produção — perde a
    /// reserva, que é o que torna a Roménia um objectivo e não uma província qualquer.</summary>
    public static float Capacity(World w, int countryId)
    {
        float baseCap = w.Rule("fuel_cap_base", 60f) + Refined(w, countryId) * w.Rule("fuel_cap_days", 30f);
        return baseCap * (w.Countries.TryGetValue(countryId, out var c) ? c.Stat("fuel_capacity") : 1f);
    }

    /// <summary>Dias que o depósito aguenta ao ritmo de hoje: -1 quando o dia paga o dia e não há conta
    /// regressiva nenhuma a fazer (é o que a faixa de avisos precisa de saber).</summary>
    public static float DaysLeft(Country c)
    {
        float deficit = c.FuelUse - c.FuelIn;
        return deficit <= 0f ? -1f : c.Fuel / deficit;
    }
}
