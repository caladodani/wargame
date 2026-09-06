using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Cara dos comandantes: divisa do posto, cor e o cartão com a barra de experiência até à
/// promoção seguinte. Vive à parte porque dois painéis mostram o mesmo homem — o exército que ele
/// comanda (ArmyPanel) e o estado-maior do país (CountryPanel) — e a divisa tem de ser a mesma nos dois.
///
/// Só lê o World: quem dá a experiência é o GeneralXpSystem.</summary>
public static class CommanderView
{
    /// <summary>Divisa do posto: uma estrela por nível, como nas platinas.</summary>
    public static string Insignia(int level) => new string('★', Math.Clamp(level, 1, 6));

    /// <summary>Prata nos postos baixos, ouro no topo — dá para ver a folha de serviço de relance.</summary>
    public static Color Tint(int level, int top) => top <= 1
        ? new Color(1f, 0.82f, 0.25f)
        : new Color(0.78f, 0.80f, 0.86f).Lerp(new Color(1f, 0.78f, 0.20f), Math.Clamp((level - 1f) / (top - 1f), 0f, 1f));

    public static int TopLevel(World w) => w.GeneralRanks.Count == 0 ? 1 : w.GeneralRanks.Max(r => r.Level);

    /// <summary>Nome do posto ("Marechal") ou vazio se a tabela de postos não estiver carregada.</summary>
    public static string RankName(World w, int countryId, string generalId) =>
        w.RankOf(countryId, generalId)?.Name ?? "";

    /// <summary>Cartão do comandante destacado: posto, efeito amplificado e a barra do que falta para
    /// subir. A experiência era um número invisível; posta assim, o jogador percebe porque é que vale a
    /// pena deixar o mesmo homem à frente do mesmo exército campanha fora.</summary>
    public static PanelContainer Card(World w, int countryId, GeneralDef def, string effect)
    {
        var rank = w.RankOf(countryId, def.Id);
        int level = rank?.Level ?? 1;
        var tint = Tint(level, TopLevel(w));

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.16f, 0.14f, 0.09f, 0.92f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 8); v.AddChild(top);
        var badge = Ui.Lbl(Insignia(level), 17);
        badge.AddThemeColorOverride("font_color", tint);
        top.AddChild(badge);
        var title = Ui.Lbl(rank is null ? def.Name : $"{def.Name} · {rank.Name}", 17);
        title.AddThemeColorOverride("font_color", tint);
        top.AddChild(Ui.Grow(title));

        var eff = Ui.Lbl(effect, 15);
        eff.AddThemeColorOverride("font_color", Ui.Text);
        v.AddChild(eff);

        v.AddChild(Progress(w, countryId, def.Id, tint));
        return card;
    }

    /// <summary>Barra de experiência até ao posto seguinte, com a legenda por baixo. No topo da carreira
    /// fica cheia e diz que já não há mais nada a ganhar.</summary>
    public static VBoxContainer Progress(World w, int countryId, string generalId, Color tint)
    {
        float xp = w.Countries.TryGetValue(countryId, out var c) ? c.GeneralXp.GetValueOrDefault(generalId) : 0f;
        var rank = w.RankOf(countryId, generalId);
        var next = w.NextRank(countryId, generalId);

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2);
        float from = rank?.Xp ?? 0f;
        float fill = next is null ? 1f : Math.Clamp((xp - from) / MathF.Max(1f, next.Xp - from), 0f, 1f);
        v.AddChild(Ui.Bar(fill, tint, 0f));

        string lead = next is not null && fill >= 0.75f ? "quase" : "a caminho de";
        var note = Ui.Lbl(next is null
            ? $"{xp:0} de experiência de campanha — no topo da carreira"
            : $"{xp:0}/{next.Xp:0} de experiência — {lead} {next.Name}", 14);
        note.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(note);
        return v;
    }
}
