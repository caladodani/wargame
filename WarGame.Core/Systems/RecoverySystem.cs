using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Org recupera de graça fora de combate; HP (reforços) custa homens e pontos de produção
/// (rules reinforce_hp_manpower, reinforce_hp_money) — sem stock, a divisão fica danificada (HoI4).
/// Uma divisão de um grupo em reserva recompõe-se mais depressa (reserve_org_bonus, reserve_hp_bonus):
/// é o que dá sentido a tirar um exército da linha em vez de o gastar até ao fim. Uma divisão com honra de
/// batalha soma ainda o seu bónus de moral (DivisionHonourSystem.Bonus).</summary>
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
            // Quem paga os reforços é a casa, mesmo com a divisão a combater sob outra bandeira: os
            // voluntários são nossos, e é do nosso pool de homens e do nosso cofre que eles se recompõem.
            var c = w.Countries[d.HomeId];
            bool resting = w.GroupOf(d.Id)?.Resting == true;
            float rest = resting ? w.Rule("reserve_org_bonus", 1.6f) : 1f;
            rest += DivisionHonourSystem.Bonus(w, d);   // tropa com nome próprio volta a si mais depressa
            rest *= w.SeasonOrg;                       // e a estação do ano manda em cima de tudo
            rest *= Weather.OrgMult(w, w.Regions[d.RegionId]);   // e o céu desta semana em cima da estação
            // no comboio não se descansa: vai-se sentado em cima do equipamento, e é por isso que um
            // redespacho chega depressa mas chega a dormir (Redeploy)
            if (d.Redeploying) rest *= w.Rule("redeploy_org_regain", 0.25f);
            d.Org = MathF.Min(100f, d.Org + (d.Supply >= 1f ? 8f : 3f) * c.Stat("org_regain") * w.CommandMult(d, "org_regain") * rest);
            if (d.Hp >= 100f) continue;
            float hp = MathF.Min(2f * (resting ? w.Rule("reserve_hp_bonus", 1.5f) : 1f), 100f - d.Hp);
            if (menPerHp > 0f) hp = MathF.Min(hp, MathF.Max(0f, c.Manpower) / menPerHp);
            if (moneyPerHp > 0f) hp = MathF.Min(hp, MathF.Max(0f, c.Money) / moneyPerHp);
            if (hp <= 0f) continue;
            d.Hp += hp; c.Manpower -= hp * menPerHp; c.Money -= hp * moneyPerHp;
        }
    }
}
