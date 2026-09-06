using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Cara da sabotagem na retaguarda: o que se pode mandar rebentar numa região inimiga, o que
/// isso custa e o que já lá vai a caminho. Sem isto as operações por região eram invisíveis — o jogador
/// só tinha espionagem contra o país inteiro, escolhida numa lista sem mapa.
///
/// Só lê o World e monta os botões; quem valida e cobra é o StartSpyOpCommand, quem estraga é o
/// EspionageSystem.</summary>
public static class SabotageView
{
    public static readonly Color Fuse = new(0.95f, 0.62f, 0.35f);

    /// <summary>Operações de sabotagem que fazem sentido nesta região (a que não tem o que estragar não
    /// aparece: rebentar um porto que não existe seria dinheiro deitado fora).</summary>
    public static List<SpyOp> Options(World w, Region r) =>
        w.SpyOps.Values.Where(o => o.IsRegional && Worth(w, o, r)).OrderBy(o => o.Cost).ToList();

    private static bool Worth(World w, SpyOp op, Region r) => op.Effect switch
    {
        "sabotage_port" => r.Buildings.Any(b => w.BuildingDefs.TryGetValue(b.Key, out var d) && d.SupplyRange > 0f),
        "sabotage_fort" => r.Fort > 0,
        "sabotage_infra" => r.Infrastructure > 0.15f,
        _ => false,
    };

    /// <summary>Operação nossa já a caminho desta região (null quando não há nenhuma).</summary>
    public static ActiveSpyOp? Running(World w, int playerId, int regionId) =>
        w.ActiveSpyOps.FirstOrDefault(o => o.CountryId == playerId && o.RegionId == regionId);

    /// <summary>Cartão da retaguarda para o painel da região: o que lá está para estragar, o preço de cada
    /// golpe e a barra da equipa que já vai a caminho. Null quando a região não é inimiga ou não há guerra —
    /// e nesse caso o painel nem mostra a secção.</summary>
    public static VBoxContainer? Card(World w, int playerId, Region r, Action<string> onPick)
    {
        if (r.ControllerId == playerId || !w.AreAtWar(playerId, r.ControllerId)) return null;
        var running = Running(w, playerId, r.Id);
        var options = Options(w, r);
        if (running is null && options.Count == 0) return null;

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3);
        var head = Ui.Lbl("💥 Sabotagem na retaguarda", 17);
        head.AddThemeColorOverride("font_color", Fuse);
        v.AddChild(head);

        if (running is not null && w.SpyOps.TryGetValue(running.OpId, out var op))
        {
            float total = MathF.Max(1f, op.Days);
            float done = Math.Clamp(1f - running.DaysLeft / total, 0f, 1f);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
            row.AddChild(Ui.Grow(Ui.Lbl($"{op.Name}: equipa a caminho", 16)));
            row.AddChild(Ui.Bar(done, Fuse, 110f));
            var left = Ui.Lbl($"{(int)MathF.Ceiling(running.DaysLeft)} dias", 15);
            left.AddThemeColorOverride("font_color", Ui.TextDim);
            row.AddChild(left);
            return v;                                  // uma operação de cada vez: não se oferecem mais
        }

        var target = w.Countries.GetValueOrDefault(r.ControllerId);
        float money = w.Countries.TryGetValue(playerId, out var me) ? me.Money : 0f;
        foreach (var o in options)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
            var what = Ui.Lbl(Damage(w, o, r), 15);
            what.AddThemeColorOverride("font_color", Ui.TextDim);
            row.AddChild(Ui.Grow(what));
            string id = o.Id;
            var btn = Ui.Btn($"{o.Name} ({o.Cost:0}, {o.Days} d)", () => onPick(id), 250);
            btn.Disabled = money < o.Cost;
            row.AddChild(btn);
        }
        if (target is not null)
        {
            var note = Ui.Lbl($"equipas nossas em terreno de {target.Name}: uma operação de cada vez por inimigo", 14);
            note.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(note);
        }
        return v;
    }

    /// <summary>O que este golpe faz a esta região, em números que já contam com o que lá está.</summary>
    public static string Damage(World w, SpyOp op, Region r) => op.Effect switch
    {
        "sabotage_port" => $"cais {r.Buildings.Where(b => w.BuildingDefs.TryGetValue(b.Key, out var d) && d.SupplyRange > 0f).Sum(b => b.Value)} → "
                         + $"{Math.Max(0, r.Buildings.Where(b => w.BuildingDefs.TryGetValue(b.Key, out var d) && d.SupplyRange > 0f).Sum(b => b.Value) - (int)MathF.Max(1f, op.Magnitude))}",
        "sabotage_fort" => $"forte {r.Fort} → {Math.Max(0, r.Fort - (int)MathF.Max(1f, op.Magnitude))}",
        "sabotage_infra" => $"infra ×{r.Infrastructure:0.00} → ×{MathF.Max(0.1f, r.Infrastructure * (1f - op.Magnitude)):0.00}",
        _ => op.Description,
    };
}
