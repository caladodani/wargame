using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Os reforços de material: todos os dias, cada divisão gasta vai buscar ao armazém do país o que
/// lhe falta para voltar a estar armada (Division.Kit → 1).
///
/// É a peça que faltava ao ciclo da guerra do HoI4. Uma batalha destrói equipamento (CombatSystem baixa o
/// Kit de quem apanha), o equipamento sai das fábricas para o armazém (ProductionSystem), e é do armazém que
/// ele volta para a linha da frente. Com o armazém cheio, uma ofensiva pode continuar; com o armazém vazio,
/// o exército fica de pé mas desarmado: bate-se pior (kit_power_floor) e os homens que voltam não têm com
/// que se bater (RecoverySystem não repõe HP acima do material).
///
/// Uma divisão só sobe o que o tipo de material mais escasso deixar: de nada serve ter espingardas se o que
/// falta são os carros. Em combate repõe-se menos (kit_refill_battle) — não se reequipa uma tropa debaixo de
/// fogo —, e uma divisão cercada não repõe nada: o material que lhe chegaria está do outro lado do cerco.
///
/// Regras: kit_refill_day, kit_refill_battle.</summary>
public sealed class EquipmentSystem : ISystem
{
    public string Name => "Equipment";

    public void Tick(World w)
    {
        float step = w.Rule("kit_refill_day", 0.06f);
        float inFight = w.Rule("kit_refill_battle", 0.35f);
        var fighting = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));
        foreach (var d in w.Divisions.Values)
        {
            if (d.Kit >= 1f || d.Cut) continue;                    // cercada: o material fica do outro lado
            if (!w.Countries.TryGetValue(d.HomeId, out var c)) continue;
            float want = MathF.Min(1f - d.Kit, step * (fighting.Contains(d.Id) ? inFight : 1f));
            if (want <= 0f) continue;
            Refill(w, c, d, want);
        }
    }

    /// <summary>Repõe até `want` de material nesta divisão, com o que o armazém dá. Devolve o que se repôs
    /// mesmo: o tipo de material mais escasso manda em todos os outros — uma divisão a que falte um único
    /// tipo não sobe, mesmo com o resto do armazém cheio.</summary>
    public static float Refill(World w, Country c, Division d, float want)
    {
        var need = w.KitNeed(d.TemplateId);
        if (need.Count == 0) { d.Kit = 1f; return 0f; }
        float can = want;
        foreach (var (type, qty) in need)
        {
            if (qty <= 0) continue;
            can = MathF.Min(can, c.Stocked(type) / qty);
            if (can <= 1e-5f) return 0f;
        }
        foreach (var (type, qty) in need)
        {
            if (qty <= 0) continue;
            float taken = qty * can;
            float left = c.Stocked(type) - taken;
            c.Stock[type] = MathF.Max(0f, left);
        }
        d.Kit = MathF.Min(1f, d.Kit + can);
        return can;
    }

    /// <summary>O que uma batalha destrói: o material vai-se com os homens. `hpLoss` é o dano que a divisão
    /// levou hoje; kit_loss_per_hp diz quanto do equipamento se perde por cada ponto de HP.</summary>
    public static void Damage(World w, Division d, float hpLoss)
    {
        if (hpLoss <= 0f) return;
        d.Kit = MathF.Max(0f, d.Kit - hpLoss * w.Rule("kit_loss_per_hp", 0.006f));
    }

    /// <summary>O que o material faz à força com que uma divisão se bate: uma divisão sem equipamento não
    /// desaparece, encolhe. O chão é kit_power_floor — com o armazém vazio ainda se defende uma linha, mas
    /// não se ataca nada.</summary>
    public static float PowerMult(World w, Division d)
    {
        float floor = Math.Clamp(w.Rule("kit_power_floor", 0.45f), 0f, 1f);
        return floor + (1f - floor) * Math.Clamp(d.Kit, 0f, 1f);
    }
}
