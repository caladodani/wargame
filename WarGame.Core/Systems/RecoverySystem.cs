using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Org/HP recuperam fora de combate.</summary>
public sealed class RecoverySystem : ISystem
{
    public string Name => "Recovery";

    public void Tick(World w)
    {
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));
        foreach (var d in w.Divisions.Values)
        {
            if (inBattle.Contains(d.Id)) continue;
            d.Org = MathF.Min(100f, d.Org + (d.Supply >= 1f ? 8f : 3f) * w.Countries[d.CountryId].Stat("org_regain"));
            d.Hp = MathF.Min(100f, d.Hp + 2f);   // TODO: consumir equipamento do stock (ProductionSystem)
        }
    }
}
