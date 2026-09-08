using Godot;
using WarGame.Core.Commands;
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

    /// <summary>Posto de topo da escada desta arma — é contra ele que a cor da divisa se mede. Cada arma
    /// tem a sua carreira, por isso o topo do mar não é o topo da infantaria; e cada país sobe pela escada
    /// dele, que pode ter outros nomes (a mesma altura, chamada de outra maneira).</summary>
    public static int TopLevel(World w, string domain = World.Land, int countryId = 0)
    {
        var ranks = countryId == 0 ? w.Ranks(domain) : w.Ranks(domain, countryId);
        return ranks.Count == 0 ? 1 : ranks.Max(r => r.Level);
    }

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
        var tint = Tint(level, TopLevel(w, def.Domain, countryId));

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

        bool hurt = w.IsWounded(countryId, def.Id);
        if (hurt) card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.20f, 0.10f, 0.11f, 0.94f), 10));

        var eff = Ui.Lbl(hurt ? "Fora de serviço — o exército não leva nada do que ele vale" : effect, 15);
        eff.AddThemeColorOverride("font_color", hurt ? Ui.Danger : Ui.Text);
        v.AddChild(eff);

        v.AddChild(hurt ? Recovery(w, countryId, def.Id) : Progress(w, countryId, def.Id, tint));
        if (!hurt) v.AddChild(Ladder(w, countryId, def.Id, tint));
        return card;
    }

    /// <summary>Escada de postos da arma, à maneira da folha de carreira do HoI4: uma placa por posto, da
    /// esquerda para a direita, com as divisas por cima. Os postos já feitos ficam acesos com a cor do
    /// comandante, o de agora leva a placa iluminada e o nome por extenso, e os que faltam ficam apagados
    /// com a experiência que ainda pedem. A barra dizia só quanto faltava para o degrau seguinte; assim
    /// vê-se a carreira inteira — e vê-se que a do ar e a do mar não são a da infantaria: um Chefe de
    /// Esquadrilha sobe a Marechal do Ar, não a Marechal do Reino.</summary>
    public static HBoxContainer Ladder(World w, int countryId, string generalId, Color tint)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 3);
        int level = w.RankOf(countryId, generalId)?.Level ?? 0;
        foreach (var r in w.Ranks(w.DomainOfGeneral(generalId), countryId))
        {
            bool now = r.Level == level, done = r.Level <= level;
            var plate = new PanelContainer();
            plate.AddThemeStyleboxOverride("panel", Ui.Box(now ? new Color(tint, 0.22f)
                                                         : done ? new Color(0.20f, 0.18f, 0.12f, 0.85f)
                                                                : new Color(0.09f, 0.09f, 0.10f, 0.70f), 4));
            var cell = new VBoxContainer(); cell.AddThemeConstantOverride("separation", 0); plate.AddChild(cell);

            var stars = Ui.Lbl(Insignia(r.Level), now ? 13 : 11);
            stars.AddThemeColorOverride("font_color", done ? tint : Ui.TextDim.Darkened(0.35f));
            stars.HorizontalAlignment = HorizontalAlignment.Center;
            cell.AddChild(stars);

            var name = Ui.Lbl(now ? r.Name : done ? "feito" : $"{r.Xp:0}", 11);
            name.AddThemeColorOverride("font_color", now ? tint : Ui.TextDim.Darkened(done ? 0.1f : 0.35f));
            name.HorizontalAlignment = HorizontalAlignment.Center;
            cell.AddChild(name);

            plate.TooltipText = $"{r.Name} — {r.Xp:0} de experiência, +{r.Bonus:0.00} ao comando"
                                + (r.CountryTag is null ? "" : $" (posto de {r.CountryTag})");
            row.AddChild(plate);
        }
        return row;
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

    /// <summary>Cor da enfermaria: vermelho apagado, para o comandante ferido não se confundir com o que
    /// está de pé.</summary>
    public static readonly Color Hurt = new(0.92f, 0.45f, 0.42f);

    /// <summary>Barra da convalescença: enche à medida que os dias passam, medida contra a baixa mais
    /// longa da tabela. Substitui a barra de carreira enquanto o comandante está no hospital — a carreira
    /// dele está parada, e quem olha para o painel tem de ver isso.</summary>
    public static VBoxContainer Recovery(World w, int countryId, string generalId)
    {
        int left = w.WoundDaysLeft(countryId, generalId);
        float longest = w.WoundKinds.Count == 0 ? 60f : MathF.Max(1f, w.WoundKinds.Values.Max(k => k.Days));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2);
        v.AddChild(Ui.Bar(Math.Clamp(1f - left / longest, 0f, 1f), Hurt, 0f));
        var note = Ui.Lbl(left <= 1 ? "volta ao serviço amanhã" : $"volta ao serviço daqui a {left} dias", 14);
        note.AddThemeColorOverride("font_color", Ui.TextDim);
        v.AddChild(note);
        return v;
    }

    /// <summary>O estado-maior inteiro à maneira do HoI4: uma folha de comandantes com retrato emoldurado
    /// em vez da lista corrida de linhas de texto que aqui estava. Cada homem ao serviço leva a chapa dele
    /// numa placa, a divisa do posto, o que multiplica, a linha da folha de serviço e a barra de carreira;
    /// os que faltam contratar aparecem por baixo, separados em dois blocos — os de casa (com o selo ⚜ e a
    /// bandeira do país) e os mercenários, que qualquer estado-maior pode chamar.
    ///
    /// Agora com as três armas em abas de metal, como o HoI4 separa o comando de terra, o do ar e o do mar:
    /// cada arma tem as suas cadeiras (regras general_slots/air_general_slots/navy_general_slots) e o seu
    /// bolso de experiência, e uma nomeação de asa paga-se com horas de voo além do dinheiro. Encher o
    /// comando de terra deixou de impedir que se chame um almirante.
    ///
    /// Só lê o World; contratar e dispensar é de quem sabe despachar comandos.</summary>
    public static PanelContainer? Roster(World w, Country c, bool mine, string domain,
                                         Action<string> onHire, Action<string> onDismiss, Action<int>? onArm = null)
    {
        if (w.GeneralPool(c).Count == 0) return null;
        var pool = w.GeneralPool(c).Where(g => g.Domain == domain).ToList();
        int slots = w.GeneralSlots(domain);
        int serving = w.GeneralsInService(c, domain);
        int top = TopLevel(w, domain, c.Id);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.14f, 0.13f, 0.10f, 0.94f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 5); card.AddChild(v);

        if (onArm is not null)
        {
            var tabs = new MetalTabs();
            tabs.Set(Ui.Arms, Math.Max(0, Array.IndexOf(World.Domains, domain)), onArm);
            v.AddChild(tabs);
        }

        int home = c.Generals.Count(id => w.GeneralDefs.TryGetValue(id, out var g)
                                          && g.CountryTag is not null && g.Domain == domain);
        var head = Ui.Lbl($"{Ui.Arms[Math.Max(0, Array.IndexOf(World.Domains, domain))]} · {serving}/{slots} ao serviço"
                          + (home > 0 ? $" · {home} de casa" : "")
                          + $"   ·   {World.Xp(c, domain):0} de {World.XpName(domain)}", 17);
        head.AddThemeColorOverride("font_color", Ui.Accent);
        v.AddChild(head);
        if (pool.Count == 0)
        {
            var none = Ui.Lbl("ainda não há comandantes desta arma para chamar", 14);
            none.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(none);
            return card;
        }

        foreach (var def in pool.Where(g => c.Generals.Contains(g.Id))
                                .OrderByDescending(g => w.RankOf(c.Id, g.Id)?.Level ?? 1).ThenBy(g => g.Name))
        {
            v.AddChild(Ui.Rule());
            bool hurt = w.IsWounded(c.Id, def.Id);
            int level = w.RankOf(c.Id, def.Id)?.Level ?? 1;
            var tint = hurt ? Hurt : Tint(level, top);
            // destacado a um grupo: o bónus sai do país e vale, amplificado, só nesse exército
            var posted = w.ArmyGroups.Values.FirstOrDefault(g => g.CountryId == c.Id && g.GeneralId == def.Id);

            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
            row.AddChild(Portrait(def.Icon, tint, hurt));
            var cell = Ui.Grow(new VBoxContainer()); cell.AddThemeConstantOverride("separation", 2);

            var title = new HBoxContainer(); title.AddThemeConstantOverride("separation", 8);
            var badge = Ui.Lbl(Insignia(level), 15);
            badge.AddThemeColorOverride("font_color", tint);
            title.AddChild(badge);
            string rank = RankName(w, c.Id, def.Id) is string rn && rn.Length > 0 ? $" · {rn}" : "";
            var who = Ui.Lbl(def.Name + rank, 17);
            who.AddThemeColorOverride("font_color", tint);
            title.AddChild(Ui.Grow(who));
            if (def.CountryTag is not null) title.AddChild(Seal(c));
            if (mine)
            {
                string id = def.Id;
                title.AddChild(Ui.Btn("Dispensar", () => onDismiss(id), 0, Ui.Kind.Danger));
            }
            cell.AddChild(title);

            var eff = Ui.Lbl(hurt
                ? "🩸 no hospital — o exército não leva nada do que ele vale"
                : posted is null
                    ? $"{Ui.StatName(def.StatKey)} ×{def.Mult:0.00} em todo o país"
                    : $"⚔ {Ui.StatName(def.StatKey)} ×{1f + (def.Mult - 1f) * (w.Rule("general_command_bonus", 2f) + w.RankBonus(c.Id, def.Id)):0.00} no {posted.Name}", 15);
            eff.AddThemeColorOverride("font_color", hurt ? Hurt : Ui.Good);
            cell.AddChild(eff);
            if (def.Note.Length > 0 && !hurt)
            {
                var note = Ui.Lbl(def.Note, 13);
                note.AddThemeColorOverride("font_color", Ui.TextDim);
                note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                cell.AddChild(note);
            }
            cell.AddChild(hurt ? Recovery(w, c.Id, def.Id) : Progress(w, c.Id, def.Id, tint));
            if (!hurt) cell.AddChild(Ladder(w, c.Id, def.Id, tint));
            row.AddChild(cell);
            v.AddChild(row);
        }

        // por contratar: primeiro os de casa, depois os que se contratam em qualquer lado
        var free = pool.Where(g => !c.Generals.Contains(g.Id)).ToList();
        if (mine && free.Count > 0)
        {
            v.AddChild(Ui.Rule());
            foreach (var group in new[] { true, false })
            {
                var men = free.Where(g => (g.CountryTag is not null) == group).ToList();
                if (men.Count == 0) continue;
                var label = Ui.Lbl(group ? $"      ⚜ de casa ({c.Name})" : "      mercenários, de qualquer lado", 13);
                label.AddThemeColorOverride("font_color", group ? Ui.Accent : Ui.TextDim);
                v.AddChild(label);
                var grid = Ui.Grow(new GridContainer { Columns = 2 });
                grid.AddThemeConstantOverride("h_separation", 6);
                grid.AddThemeConstantOverride("v_separation", 6);
                foreach (var g in men) grid.AddChild(Candidate(w, c, g, onHire));
                v.AddChild(grid);
            }
        }
        return card;
    }

    /// <summary>Ficha de recrutamento, à maneira das listas de comandantes do HoI4: retrato emoldurado,
    /// nome, selo ⚜ de quem é de casa, o que multiplica, a linha da folha de serviço e as placas do preço
    /// — pontos de produção e, para a asa e a esquadra, a experiência da arma. A ficha inteira é o botão.
    ///
    /// Substitui a linha de botão corrida que aqui estava: o jogador escolhia um comandante por um preço
    /// escrito num sítio e uma promessa noutro, sem ver de relance quem era de casa nem o que já podia pagar.
    /// Quando a nomeação não pode ser feita, a ficha apaga-se e a razão fica na legenda e por baixo do
    /// preço — a mesma frase que o comando recusaria.</summary>
    private static PanelContainer Candidate(World w, Country c, GeneralDef g, Action<string> onHire)
    {
        string? why = new HireGeneralCommand(c.Id, g.Id).Validate(w);
        bool ok = why is null, home = g.CountryTag is not null;
        var tint = home ? Ui.Accent : Ui.Text;
        if (!ok) tint = tint.Darkened(0.35f);

        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", Ui.Box(ok ? new Color(0.17f, 0.16f, 0.11f, 0.95f)
                                                        : new Color(0.12f, 0.12f, 0.13f, 0.88f), 8));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); card.AddChild(row);
        row.AddChild(Portrait(g.Icon, tint, false));

        var cell = Ui.Grow(new VBoxContainer()); cell.AddThemeConstantOverride("separation", 2);
        var title = new HBoxContainer(); title.AddThemeConstantOverride("separation", 6);
        var who = Ui.Lbl(g.Name, 16);
        who.AddThemeColorOverride("font_color", tint);
        title.AddChild(Ui.Grow(who));
        if (home) title.AddChild(Seal(c));
        cell.AddChild(title);

        var eff = Ui.Lbl($"{Ui.StatName(g.StatKey)} ×{g.Mult:0.00}", 14);
        eff.AddThemeColorOverride("font_color", ok ? Ui.Good : Ui.TextDim);
        cell.AddChild(eff);
        if (g.Note.Length > 0)
        {
            var note = Ui.Lbl(g.Note, 12);
            note.AddThemeColorOverride("font_color", Ui.TextDim);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            cell.AddChild(note);
        }

        var price = new HBoxContainer(); price.AddThemeConstantOverride("separation", 6);
        price.AddChild(Plate($"{g.Cost:0} pp", c.Money >= g.Cost));
        if (g.Xp > 0f) price.AddChild(Plate($"{g.Xp:0} {World.XpName(g.Domain)}", World.Xp(c, g.Domain) >= g.Xp));
        if (!ok)
        {
            var no = Ui.Lbl(why!, 12);
            no.AddThemeColorOverride("font_color", Ui.Danger);
            price.AddChild(Ui.Grow(no));
        }
        cell.AddChild(price);
        row.AddChild(cell);

        if (ok) { string id = g.Id; Ui.Click(card, () => onHire(id), $"nomear {g.Name}"); }
        else card.TooltipText = why;
        return card;
    }

    /// <summary>Placa de preço: verde quando o cofre (ou o bolso da arma) já chega, vermelha quando falta.</summary>
    private static PanelContainer Plate(string text, bool afford)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box((afford ? Ui.Good : Ui.Danger) with { A = 0.16f }, 4));
        var l = Ui.Lbl(text, 13);
        l.AddThemeColorOverride("font_color", afford ? Ui.Good : Ui.Danger);
        chip.AddChild(l);
        return chip;
    }

    /// <summary>Retrato do comandante: a chapa dele numa placa emoldurada, apagada quando está no hospital.</summary>
    private static PanelContainer Portrait(string icon, Color tint, bool hurt)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(54, 54) };
        frame.AddThemeStyleboxOverride("panel", Ui.Box(hurt ? new Color(0.20f, 0.10f, 0.11f, 0.94f) : Ui.Ink, 6));
        var face = Ui.Lbl(icon, 26);
        face.HorizontalAlignment = HorizontalAlignment.Center;
        face.VerticalAlignment = VerticalAlignment.Center;
        face.AddThemeColorOverride("font_color", tint);
        frame.AddChild(face);
        return frame;
    }

    /// <summary>Selo do comandante que é de casa (o mesmo do gabinete e das leis nacionais).</summary>
    private static PanelContainer Seal(Country c)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 4));
        var l = Ui.Lbl($"⚜ {c.Tag}", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"comandante de {c.Name}: nenhum outro estado-maior o chama";
        chip.AddChild(l);
        return chip;
    }

    /// <summary>Quadro de postos do país, à maneira da folha de carreira do HoI4: as três armas lado a
    /// lado, cada uma com a escada inteira do topo para baixo, e em cada degrau quem lá está agora. A
    /// escada só se via no cartão de um comandante — e só a arma dele; assim vê-se o estado-maior todo
    /// de uma vez, onde há gente parada no degrau de baixo e quantos degraus faltam ao país.
    ///
    /// Quando o país traz escada própria (general_rank.country_tag) a coluna leva o selo ⚜ e os nomes da
    /// tradição dele: um Generalfeldmarschall e um Marechal do Reino valem o mesmo, chamam-se é de
    /// maneira diferente.</summary>
    public static PanelContainer Board(World w, Country c)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.12f, 0.12f, 0.14f, 0.94f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 5); card.AddChild(v);

        int own = World.Domains.Count(d => w.HasOwnRanks(c, d));
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8);
        var title = Ui.Lbl("Quadro de postos", 17);
        title.AddThemeColorOverride("font_color", Ui.Accent);
        head.AddChild(Ui.Grow(title));
        if (own > 0) head.AddChild(RankSeal(c));
        v.AddChild(head);

        var cols = Ui.Grow(new GridContainer { Columns = World.Domains.Length });
        cols.AddThemeConstantOverride("h_separation", 6);
        v.AddChild(cols);
        for (int i = 0; i < World.Domains.Length; i++) cols.AddChild(Column(w, c, World.Domains[i], Ui.Arms[i]));
        return card;
    }

    /// <summary>Uma coluna do quadro: a arma, e a escada dela do posto mais alto para o mais baixo.</summary>
    private static PanelContainer Column(World w, Country c, string domain, string arm)
    {
        var box = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface, 6));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); box.AddChild(v);

        var ranks = w.Ranks(domain, c);
        bool mine = w.HasOwnRanks(c, domain);
        int top = ranks.Count == 0 ? 1 : ranks.Max(r => r.Level);
        var name = Ui.Lbl(mine ? $"{arm} ⚜" : arm, 15);
        name.AddThemeColorOverride("font_color", mine ? Ui.Accent : Ui.TextDim);
        name.TooltipText = mine ? $"postos de {c.Name}: só esta bandeira os usa" : "postos da escada comum";
        v.AddChild(name);

        foreach (var r in ranks.OrderByDescending(x => x.Level))
        {
            var men = c.Generals.Where(id => w.DomainOfGeneral(id) == domain
                                             && (w.RankOf(c.Id, id)?.Level ?? 0) == r.Level)
                                .Select(id => w.GeneralDefs.TryGetValue(id, out var g) ? g.Name : id)
                                .OrderBy(x => x).ToList();
            var tint = men.Count > 0 ? Tint(r.Level, top) : Ui.TextDim.Darkened(0.3f);
            var plate = new PanelContainer();
            plate.AddThemeStyleboxOverride("panel", Ui.Box(men.Count > 0 ? new Color(tint, 0.16f)
                                                                        : new Color(0.09f, 0.09f, 0.10f, 0.65f), 4));
            var cell = new VBoxContainer(); cell.AddThemeConstantOverride("separation", 0); plate.AddChild(cell);

            var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 5);
            var stars = Ui.Lbl(Insignia(r.Level), 11);
            stars.AddThemeColorOverride("font_color", tint);
            line.AddChild(stars);
            var who = Ui.Lbl(r.Name, 13);
            who.AddThemeColorOverride("font_color", tint);
            line.AddChild(Ui.Grow(who));
            cell.AddChild(line);

            var note = Ui.Lbl(men.Count > 0 ? string.Join(", ", men) : $"{r.Xp:0} de experiência", 11);
            note.AddThemeColorOverride("font_color", men.Count > 0 ? Ui.Text : Ui.TextDim.Darkened(0.25f));
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            cell.AddChild(note);

            plate.TooltipText = $"{r.Name} — {r.Xp:0} de experiência, +{r.Bonus:0.00} ao comando"
                                + (men.Count == 0 ? "" : $" · {string.Join(", ", men)}");
            v.AddChild(plate);
        }
        return box;
    }

    /// <summary>Selo do quadro: a escada é desta bandeira e de mais nenhuma.</summary>
    private static PanelContainer RankSeal(Country c)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.18f }, 4));
        var l = Ui.Lbl($"⚜ postos de {c.Tag}", 13);
        l.AddThemeColorOverride("font_color", Ui.Accent);
        l.TooltipText = $"escada de postos de {c.Name}: nenhum outro exército tem estes nomes";
        chip.AddChild(l);
        return chip;
    }

    /// <summary>Enfermaria do estado-maior, arma a arma: quem está fora, com que gravidade (a chapa da
    /// tabela wound_kind), quantos dias faltam e o que o país deixa de ter enquanto ele não volta.
    ///
    /// Era uma lista corrida de nomes e dias: não se via se quem estava no hospital era o marechal ou o
    /// almirante — e agora que o céu e o mar também ferem comandantes, essa distinção é a informação
    /// toda. A contagem por arma vai no cabeçalho, para quem olha de passagem saber onde tem o buraco.
    ///
    /// Vazia (null) quando não há baixas — o painel não mostra secções vazias.</summary>
    public static PanelContainer? Infirmary(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return null;
        var hurt = c.GeneralWound.Where(kv => kv.Value > w.Clock.Day)
                                 .OrderBy(kv => kv.Value).ToList();
        if (hurt.Count == 0) return null;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.20f, 0.10f, 0.11f, 0.90f), 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); card.AddChild(v);

        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 6);
        var title = Ui.Lbl($"🏥 Enfermaria · {hurt.Count} fora de serviço", 17);
        title.AddThemeColorOverride("font_color", Hurt);
        head.AddChild(Ui.Grow(title));
        for (int i = 0; i < World.Domains.Length; i++)
        {
            int n = hurt.Count(kv => w.DomainOfGeneral(kv.Key) == World.Domains[i]);
            if (n > 0) head.AddChild(Tally(Ui.Arms[i], n));
        }
        v.AddChild(head);

        for (int i = 0; i < World.Domains.Length; i++)
        {
            var here = hurt.Where(kv => w.DomainOfGeneral(kv.Key) == World.Domains[i]).ToList();
            if (here.Count == 0) continue;                              // arma inteira de pé: não se anuncia
            var arm = Ui.Lbl(Ui.Arms[i], 14);
            arm.AddThemeColorOverride("font_color", Ui.TextDim);
            v.AddChild(arm);
            foreach (var (id, until) in here) v.AddChild(Bed(w, c, id, until));
        }
        return card;
    }

    /// <summary>Contagem de baixas de uma arma no cabeçalho da enfermaria ("✈ Ar 2").</summary>
    private static PanelContainer Tally(string arm, int men)
    {
        var chip = new PanelContainer();
        chip.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Hurt, 0.18f), 4));
        var l = Ui.Lbl($"{arm} {men}", 13);
        l.AddThemeColorOverride("font_color", Hurt);
        l.TooltipText = $"{men} fora de serviço no comando de {arm}";
        chip.AddChild(l);
        return chip;
    }

    /// <summary>Uma cama da enfermaria: a chapa da gravidade que o tirou de serviço, o nome, os dias que
    /// faltam, o que o país perde enquanto ele lá está e a barra da convalescença.</summary>
    private static PanelContainer Bed(World w, Country c, string generalId, int until)
    {
        var kind = c.GeneralWoundKind.TryGetValue(generalId, out var kid)
                   && w.WoundKinds.TryGetValue(kid, out var k) ? k : null;
        string name = w.GeneralDefs.TryGetValue(generalId, out var def) ? def.Name : generalId;

        var plate = new PanelContainer();
        plate.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.27f, 0.13f, 0.14f, 0.85f), 6));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 1); plate.AddChild(v);

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); v.AddChild(row);
        row.AddChild(Glyph.Make(kind?.Glyph is { Length: > 0 } g ? g : "gota", 17, Hurt, kind?.Name));
        var who = Ui.Lbl(name, 16);
        who.AddThemeColorOverride("font_color", Ui.Text);
        row.AddChild(Ui.Grow(who));
        var when = Ui.Lbl($"{Math.Max(0, until - w.Clock.Day)} dias", 15);
        when.AddThemeColorOverride("font_color", Hurt);
        row.AddChild(when);

        // o preço da baixa em números: enquanto está no hospital o multiplicador dele não conta a ninguém
        string cost = def is null ? "" : $" · o país fica sem {Ui.StatName(def.StatKey)} ×{def.Mult:0.00}";
        var note = Ui.Lbl((kind?.Name ?? "Ferido em combate") + cost, 13);
        note.AddThemeColorOverride("font_color", Ui.TextDim);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(note);

        v.AddChild(Recovery(w, c.Id, generalId));
        plate.TooltipText = kind is null
            ? $"{name} está fora de serviço"
            : $"{kind.Name} — {kind.Days} dias de baixa"
              + (kind.Domain is null ? "" : $"; é baixa de {kind.Domain} e só acontece a quem serve nessa arma");
        return plate;
    }
}
