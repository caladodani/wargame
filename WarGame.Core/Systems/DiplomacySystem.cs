using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// Diplomacia (HoI4: justificar objectivo de guerra). Quem tem Country.JustifyTarget avança
/// 1 dia por dia; ao chegar a rule war_justify_days a guerra declara-se sozinha (o
/// DeclareWarCommand chama os aliados do alvo, como sempre). A justificação cancela-se se
/// entretanto deixar de fazer sentido: alvo capitulado, já em guerra, mesma facção, ou o
/// próprio capitulou.
/// </summary>
public sealed class DiplomacySystem : ISystem
{
    public string Name => "Diplomacy";

    public void Tick(World w)
    {
        float days = w.Rule("war_justify_days", 30f);
        foreach (var c in w.Countries.Values.ToList())
        {
            if (c.JustifyTarget is not int target) continue;
            if (c.Capitulated || !w.Countries.TryGetValue(target, out var t) || t.Capitulated
                || c.AtWarWith.Contains(target) || w.SameFaction(c.Id, target))
            { c.JustifyTarget = null; c.JustifyProgress = 0f; continue; }

            c.JustifyProgress += 1f;
            if (c.JustifyProgress < days) continue;
            c.JustifyTarget = null; c.JustifyProgress = 0f;
            var cmd = new DeclareWarCommand(c.Id, target);
            if (cmd.Validate(w) is null) cmd.Execute(w);
        }
    }
}
