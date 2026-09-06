using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Cara dos cais: quantas divisões o outro lado do mar nos pede e quantas os portos aguentam.
/// Sem isto, o estrangulamento do SupplySystem era invisível — as divisões desembarcadas encolhiam de
/// abastecimento sem o jogador perceber que a culpa era do cais, não do inimigo.
///
/// Só lê o World: quem conta a carga e aperta o abastecimento é o SupplySystem.</summary>
public static class PortView
{
    public static readonly Color Quay = new(0.45f, 0.78f, 0.92f);

    /// <summary>Regiões deste país com cais (edifício que alcança mar), das maiores para as menores.</summary>
    public static List<Region> Ports(World w, int countryId) =>
        w.Regions.Values.Where(r => r.ControllerId == countryId && Levels(w, r) > 0)
                        .OrderByDescending(r => Levels(w, r)).ThenBy(r => r.Name).ToList();

    /// <summary>Níveis de cais de uma região (só os edifícios com alcance por mar contam).</summary>
    public static int Levels(World w, Region r) =>
        r.Buildings.Where(kv => w.BuildingDefs.TryGetValue(kv.Key, out var d) && d.SupplyRange > 0f).Sum(kv => kv.Value);

    /// <summary>Alcance por mar somado dos cais de uma região, em quilómetros de travessia.</summary>
    public static float Reach(World w, Region r) =>
        r.Buildings.Sum(kv => w.BuildingDefs.TryGetValue(kv.Key, out var d) ? d.SupplyRange * kv.Value : 0f);

    /// <summary>Fracção do cais em uso (1 = cheio, &gt;1 = a rebentar).</summary>
    public static float Load(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) && c.PortCapacity > 0f
            ? c.SeaSupplied / c.PortCapacity
            : (w.Countries.TryGetValue(countryId, out var c2) && c2.SeaSupplied > 0 ? 2f : 0f);

    /// <summary>Cartão dos cais para o painel do país: barra de lotação, aviso quando estamos a estrangular
    /// os nossos, e uma linha por porto com nível e alcance. Null quando não há cais nem ninguém no mar —
    /// um país continental não precisa de ver isto.</summary>
    public static PanelContainer? Card(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return null;
        var ports = Ports(w, countryId);
        if (ports.Count == 0 && c.SeaSupplied == 0) return null;

        float load = Load(w, countryId);
        bool over = load > 1f;
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.15f, 0.19f, 0.92f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        var head = Ui.Lbl($"⚓ Cais · {c.SeaSupplied} de {c.PortCapacity:0} divisões do outro lado do mar", 17);
        head.AddThemeColorOverride("font_color", over ? Ui.Danger : Quay);
        v.AddChild(head);

        var bar = Ui.Bar(Math.Clamp(load, 0f, 1f), over ? Ui.Danger : Quay, 0f);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        v.AddChild(bar);

        float floor = w.Rule("port_overflow_min", 0.35f);
        float strain = c.PortCapacity > 0f && c.SeaSupplied > 0
            ? Math.Clamp(c.PortCapacity / c.SeaSupplied, floor, 1f) : 1f;
        var note = Ui.Lbl(over
            ? $"cais a rebentar: quem desembarcou recebe {strain:P0} do que devia — construir porto ou trazer divisões de volta"
            : ports.Count == 0 ? "sem cais nenhum: quem passar o mar fica em bolsa"
            : "os cais dão conta do recado", 14);
        note.AddThemeColorOverride("font_color", over ? Ui.Danger : Ui.TextDim);
        v.AddChild(note);

        foreach (var r in ports)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
            row.AddChild(Ui.Grow(Ui.Lbl($"⚓ {r.Name}", 16)));
            var lvl = Ui.Lbl($"nível {Levels(w, r)}", 15);
            lvl.AddThemeColorOverride("font_color", Quay);
            row.AddChild(lvl);
            var km = Ui.Lbl($"{Reach(w, r):0} km", 15);
            km.AddThemeColorOverride("font_color", Ui.TextDim);
            row.AddChild(km);
            var carry = Ui.Lbl($"+{Levels(w, r) * w.Rule("port_capacity_per_level", 6f):0} div.", 15);
            carry.AddThemeColorOverride("font_color", Ui.Text);
            row.AddChild(carry);
        }
        return card;
    }

    /// <summary>Linha curta para o painel da região: o que este cais carrega e até onde. Vazia quando a
    /// região não tem porto.</summary>
    public static string RegionLine(World w, Region r)
    {
        int lvl = Levels(w, r);
        if (lvl <= 0) return "";
        return $"⚓ cais {lvl}: carrega {lvl * w.Rule("port_capacity_per_level", 6f):0} divisões até {Reach(w, r):0} km";
    }
}
