using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Cara da opinião do país (PartySystem): as barras dos partidos, quem governa, a data das
/// próximas eleições e o aviso de golpe. É o ecrã de política do HoI4 traduzido para a nossa convenção —
/// uma chapa desenhada por partido, barras encostadas com a percentagem, e por baixo o puxão do dia
/// (porque é que a opinião está a andar para onde está a andar).
///
/// Só lê o World e despacha o empurrão da propaganda; quem mexe nos números é o PartySystem.</summary>
public static class PartyView
{
    /// <summary>Cor de cada partido pela ordem da tabela: não é bandeira de ninguém, é só a convenção de
    /// mapa aplicada às barras — cada linha tem sempre a mesma cor em todos os ecrãs.</summary>
    public static readonly Color[] Palette =
    {
        new(0.85f, 0.72f, 0.35f),   // 1.º da tabela
        new(0.80f, 0.45f, 0.35f),   // 2.º
        new(0.72f, 0.35f, 0.40f),   // 3.º
        new(0.45f, 0.55f, 0.72f),   // 4.º
        new(0.45f, 0.68f, 0.52f),   // 5.º em diante
    };

    public static Color Of(PartyDef p) => Palette[Math.Clamp(p.Sort - 1, 0, Palette.Length - 1)];

    /// <summary>Os partidos deste país do mais popular para o menos, já com a definição ao lado. Partido
    /// que a tabela não conhece fica de fora — um save antigo pode trazer nomes que já não existem.</summary>
    public static List<(PartyDef Def, float Popularity)> Rows(World w, Country c) =>
        c.Parties.Where(kv => w.PartyDefs.ContainsKey(kv.Key))
                 .Select(kv => (Def: w.PartyDefs[kv.Key], Popularity: kv.Value))
                 .OrderByDescending(x => x.Popularity).ThenBy(x => x.Def.Sort).ToList();

    /// <summary>Os ministros desta cor sentados no gabinete, em texto: é quem está a puxar a barra.</summary>
    private static string Ministers(World w, Country c, string partyId)
    {
        var names = CabinetSystem.Ministers(w, c).Where(a => a.Party == partyId).Select(a => a.Name).ToList();
        return names.Count == 0 ? "ninguém" : string.Join(", ", names);
    }

    /// <summary>Quantos dias faltam para as urnas (0 = não há relógio nenhum).</summary>
    public static int DaysToElection(World w, Country c) =>
        c.NextElection <= 0 ? 0 : Math.Max(0, c.NextElection - w.Clock.Day);

    /// <summary>A frase do relógio: a data e os dias que faltam, ou porque é que não há data nenhuma.</summary>
    public static string Clock(World w, Country c)
    {
        if (c.NextElection <= 0)
            return w.PartyDefs.TryGetValue(c.Party, out var p) && !p.Elections
                ? "sem eleições marcadas — este governo não as faz"
                : "sem eleições marcadas";
        int days = DaysToElection(w, c);
        var when = w.Clock.Date.AddDays(days);
        return $"eleições a {when:dd/MM/yyyy} · faltam {days} dia{(days == 1 ? "" : "s")}";
    }

    /// <summary>O aviso do golpe: quem está em condições de o dar, ou quanto lhe falta. Vazio quando o país
    /// está calmo — não se avisa de um golpe que ninguém está a preparar.</summary>
    public static string CoupWarning(World w, Country c)
    {
        float needPop = w.Rule("coup_popularity", 60f), needStab = w.Rule("coup_stability", 25f);
        if (PartySystem.CoupCandidate(w, c) is string who)
            return $"golpe iminente: {(w.PartyDefs.TryGetValue(who, out var d) ? d.Name : who)} tem a rua e o país está pelas ruas";
        var top = c.Parties.Where(kv => kv.Key != c.Party).OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (top.Key is null) return "";
        if (c.Stability >= needStab && top.Value < needPop) return "";
        return c.Stability >= needStab
            ? $"a oposição tem a rua ({top.Value:0}%), mas o país aguenta-se (estabilidade {c.Stability:0} de {needStab:0})"
            : $"país instável (estabilidade {c.Stability:0}), mas nenhum partido chega aos {needPop:0}% para o golpe";
    }

    /// <summary>Cartão da opinião para a aba Nação: cabeçalho com quem governa e o que isso vale, o relógio
    /// das urnas, uma linha por partido com chapa, barra e o puxão do dia, e o botão da propaganda quando
    /// o país é nosso. Null quando ainda não há partidos nenhuns carregados.</summary>
    public static PanelContainer? Card(World w, Country c, bool mine, Action<string>? onPush = null)
    {
        var rows = Rows(w, c);
        if (rows.Count == 0) return null;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.12f, 0.14f, 0.18f, 0.92f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        var ruling = w.PartyDefs.GetValueOrDefault(c.Party);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        if (ruling is not null) head.AddChild(Glyph.Make(ruling.Glyph, 22, Of(ruling), ruling.Note));
        var title = Ui.Lbl("🗳 A opinião do país" + (ruling is not null ? $" · governo: {ruling.Name}" : ""), 17);
        title.AddThemeColorOverride("font_color", ruling is null ? Ui.Text : Of(ruling));
        head.AddChild(Ui.Grow(title));

        // o que o governo vale ao país: a única razão prática para se querer um partido e não outro
        if (ruling is not null && !string.IsNullOrEmpty(ruling.StatKey))
        {
            var gain = Ui.Lbl($"{ruling.StatKey} ×{ruling.StatMult:0.00}", 15);
            gain.AddThemeColorOverride("font_color", ruling.StatMult >= 1f ? Ui.Good : Ui.Danger);
            head.AddChild(gain);
        }

        var clock = Ui.Lbl(Clock(w, c), 14);
        clock.AddThemeColorOverride("font_color", DaysToElection(w, c) is int d && d > 0 && d < 90 ? Ui.Accent : Ui.TextDim);
        v.AddChild(clock);

        if (CoupWarning(w, c) is string warn && warn.Length > 0)
        {
            var wl = Ui.Wrapped(warn, 520f, 14);
            wl.AddThemeColorOverride("font_color", PartySystem.CoupCandidate(w, c) is not null ? Ui.Danger : Ui.TextDim);
            v.AddChild(wl);
        }

        float cost = w.Rule("party_push_cost", 25f), points = w.Rule("party_push_points", 5f);
        foreach (var (p, pop) in rows)
        {
            bool govern = p.Id == c.Party;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); v.AddChild(row);
            row.AddChild(Glyph.Make(p.Glyph, 20, Of(p), p.Note));
            var name = Ui.Lbl(p.Name + (govern ? "  ▲ governo" : ""), 16);
            name.AddThemeColorOverride("font_color", govern ? Of(p) : Ui.Text);
            row.AddChild(Ui.Grow(name));

            row.AddChild(Ui.Bar(Math.Clamp(pop / 100f, 0f, 1f), Of(p), 120f));
            var pct = Ui.Lbl($"{pop:0}%", 15);
            pct.AddThemeColorOverride("font_color", Of(p));
            row.AddChild(pct);

            // o puxão do dia: a seta que diz para onde a barra vai amanhã e porquê
            float dr = PartySystem.Drift(w, c, p);
            float gab = CabinetSystem.PartyPull(w, c, p.Id);
            var arrow = Ui.Lbl(MathF.Abs(dr) < 0.005f ? "—" : (dr > 0 ? $"▲ {dr:0.00}" : $"▼ {MathF.Abs(dr):0.00}"), 14);
            arrow.AddThemeColorOverride("font_color", MathF.Abs(dr) < 0.005f ? Ui.TextDim : dr > 0 ? Ui.Good : Ui.Danger);
            arrow.TooltipText = $"pontos por dia: base {p.Base:0}, guerra {p.DriftWar:+0.000;-0.000;0}, "
                              + $"instabilidade {p.DriftUnstable:+0.000;-0.000;0}, desgaste {p.DriftExhaustion:+0.000;-0.000;0}"
                              + $", gabinete {gab:+0.000;-0.000;0}";
            row.AddChild(arrow);

            // a parcela do gabinete sai à vista: o ministro desta cor faz campanha de dentro do Estado
            if (gab > 0.0005f)
            {
                var mesa = Ui.Lbl($"🏛 {gab:+0.00}", 13);
                mesa.AddThemeColorOverride("font_color", Of(p));
                mesa.TooltipText = $"{Ministers(w, c, p.Id)} na mesa do governo: {gab:+0.000} pontos por dia";
                row.AddChild(mesa);
            }

            if (mine && onPush is not null)
            {
                var btn = Ui.Btn($"📣 +{points:0}", () => onPush(p.Id), 76, govern ? Ui.Kind.Primary : Ui.Kind.Normal);
                btn.Disabled = c.Political < cost;
                btn.TooltipText = $"Manda a propaganda para a rua a favor d{(p.Name.EndsWith("s") ? "os" : "o")} {p.Name}: "
                                + $"{cost:0} de poder político por {points:0} pontos de opinião";
                row.AddChild(btn);
            }
        }
        return card;
    }
}
