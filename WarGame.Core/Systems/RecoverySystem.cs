using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Org recupera de graça fora de combate; HP (reforços) custa homens e pontos de produção
/// (rules reinforce_hp_manpower, reinforce_hp_money) — sem stock, a divisão fica danificada (HoI4).</summary>
public sealed class RecoverySystem : ISystem
{
    public string Name => "Recovery";

    public void Tick(World w)
    {
        float menPerHp = w.Rule("reinforce_hp_manpower", 30f), moneyPerHp = w.Rule("reinforce_hp_money", 0.05f);
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));
        foreach (var d in w.Divisions.Values)
        {
            if (inBattle.Contains(d.Id)) continue;
            var c = w.Countries[d.CountryId];
            d.Org = MathF.Min(100f, d.Org + (d.Supply >= 1f ? 8f : 3f) * c.Stat("org_regain"));
            if (d.Hp >= 100f) continue;
            float hp = MathF.Min(2f, 100f - d.Hp);
            if (menPerHp > 0f) hp = MathF.Min(hp, MathF.Max(0f, c.Manpower) / menPerHp);
            if (moneyPerHp > 0f) hp = MathF.Min(hp, MathF.Max(0f, c.Money) / moneyPerHp);
            if (hp <= 0f) continue;
            d.Hp += hp; c.Manpower -= hp * menPerHp; c.Money -= hp * moneyPerHp;
        }
    }
}
