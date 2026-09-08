using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A conta de uma obra, em chapas, como as da produção e as da região: o que custa, quanto demora,
/// quantas fábricas civis sobram e até que nível vai — com o que a obra dá ao país escrito por baixo. As
/// parcelas vêm do BuildPlan, que também é quem responde ao "porque é que não posso construir isto".</summary>
public static class BuildView
{
    /// <summary>Cartão da obra `id` (uma linha da tabela `building`, ou a estrada e o forte). `regionId`
    /// nulo = ainda não se sabe onde vai ser: mostra-se o que é de país e o tecto de cada coisa.</summary>
    public static PanelContainer? Card(World w, int countryId, int? regionId, string id)
    {
        if (BuildPlan.Find(w, id) is not BuildOffer offer) return null;
        var parts = BuildPlan.Parts(w, countryId, regionId, id);
        if (parts.Count == 0) return null;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Ink with { A = 0.85f }, 6));
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 3); card.AddChild(box);

        string travao = BuildPlan.Blocked(w, countryId, regionId, id) ?? "";
        var head = Ui.Lbl(travao.Length > 0 ? $"{offer.Name}: {travao}" : $"o que dá {offer.Name}", 13);
        head.AddThemeColorOverride("font_color", travao.Length > 0 ? Ui.Danger.Lightened(0.15f) : Ui.TextDim);
        box.AddChild(head);

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        foreach (var p in parts)
        {
            var plate = Ui.Counter(Glyph.Make(p.Glyph, 15, Ui.Accent), out var value, out var note);
            value.Text = p.Value;
            note.Text = p.Name;
            plate.TooltipText = $"{p.Name}: {p.Value}\n{p.Note}";
            flow.AddChild(plate);
        }
        box.AddChild(flow);

        var gives = Ui.Lbl(offer.Gives, 12);
        gives.AddThemeColorOverride("font_color", Ui.TextDim);
        gives.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        gives.CustomMinimumSize = new Vector2(240, 0);
        box.AddChild(gives);
        return card;
    }

    /// <summary>--smoke: a conta da obra mais cara que o país pode escolher, em texto.</summary>
    public static string Smoke(World w, int countryId)
    {
        var offers = BuildPlan.Offers(w);
        if (offers.Count == 0) return "sem obras para construir";
        var pick = offers.OrderByDescending(o => o.Cost).First();
        var parts = BuildPlan.Parts(w, countryId, null, pick.Id);
        return $"conta da obra {pick.Name}: {parts.Count} parcelas ({string.Join(", ", parts.Select(p => p.Name + " " + p.Value))})"
             + $", {offers.Count} obras no menu, {offers.Count(o => BuildPlan.Blocked(w, countryId, null, o.Id) is null)} por começar já"
             + (BuildPlan.Blocked(w, countryId, null, pick.Id) is string b ? $" ({b})" : "");
    }
}
