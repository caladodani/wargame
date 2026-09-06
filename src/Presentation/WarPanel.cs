using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Painel "Guerra": o saldo de cada guerra do jogador (regiões tomadas, batalhas ganhas,
/// divisões perdidas) em barras de comparação lado a lado, mais o arquivo das guerras já terminadas
/// (World.WarHistory). Só lê o World — quem conta é o WarStatsSystem.</summary>
public partial class WarPanel : PanelContainer
{
    private Game _game = null!;
    private VBoxContainer _body = null!;
    private string _lastKey = "";
    /// <summary>Levar o mapa a uma região (o Hud é que sabe mexer na câmara): usado pelo "Ver no mapa" das
    /// cedências, para ninguém assinar terra que não viu.</summary>
    public Action<int>? OnShowRegion;

    /// <summary>Guerra com a mesa de negociação aberta (id do inimigo), e o que lhe estamos a exigir.</summary>
    private int? _deal;
    private readonly HashSet<int> _demand = new();

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.42f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 0.95f)));
        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        head.AddChild(Ui.Grow(Ui.Lbl("Guerra", 22)));
        head.AddChild(Ui.Btn("Fechar", Close));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Open() { _lastKey = ""; _deal = null; _demand.Clear(); _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); }); }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    /// <summary>Só para o --smoke: abre a mesa de negociação da primeira guerra do jogador, para o
    /// caminho todo (candidatas, balança, botões) ser percorrido sem ninguém tocar no ecrã.</summary>
    public void SmokeDeal()
    {
        if (_game.PlayerId is not int pid) return;
        var war = _game.World.Wars.Values.FirstOrDefault(x => x.Involves(pid));
        if (war is null) return;
        _deal = war.EnemyOf(pid);
        foreach (int id in PeaceTerms.Suggest(_game.World, pid, _deal.Value)) _demand.Add(id);
        _lastKey = "";
        Fill();
        _deal = null; _demand.Clear();
    }

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid) { Ui.Clear(_body); _body.AddChild(Ui.Lbl("Escolhe um país primeiro", 18)); return; }
            var mine = w.Wars.Values.Where(x => x.Involves(pid)).OrderBy(x => x.StartDay).ToList();
            var past = w.WarHistory.Where(r => r.Involves(pid)).ToList();
            var key = w.Clock.Day + "|" + mine.Count + "|" + past.Count + "|" +
                      string.Join(",", mine.Select(x => string.Join("-", x.Side(pid).Goals.OrderBy(g => g)) + "/" + x.Side(pid).Goals.Count(g => w.Regions.TryGetValue(g, out var gr) && gr.ControllerId == pid))) + "|" +
                      $"deal{_deal}:{string.Join("-", _demand.OrderBy(x => x))}|" +
                      string.Join(",", mine.Select(x => $"p{PrisonerView.HeldBy(w, pid, x.EnemyOf(pid))}/{PrisonerView.HeldBy(w, x.EnemyOf(pid), pid)}")) + "|" +
                      string.Join(",", mine.Select(x => $"t{PrisonerExchange.Evaluate(w, pid, x.EnemyOf(pid)).Accepted}")) + "|" +
                      string.Join(",", w.Offers.Where(o => o.ToId == pid).Select(o => $"o{o.FromId}{o.Kind}:{o.Men}:{o.RegionId}:{o.ExpiresDay}")) + "|" +
                      string.Join(",", mine.Select(x => $"{x.EnemyOf(pid)}:{x.Side(pid).RegionsTaken}:{x.Enemy(pid).RegionsTaken}:{x.Side(pid).DivisionsLost}:{x.Enemy(pid).DivisionsLost}:{x.Side(pid).BattlesWon}:{x.Enemy(pid).BattlesWon}"));
            if (key == _lastKey) return;
            _lastKey = key;
            Ui.Clear(_body);

            var front = new Dictionary<int, float>();   // força útil (org×HP) por país, para a balança
            var divs = new Dictionary<int, int>();
            foreach (var d in w.Divisions.Values)
            {
                front[d.CountryId] = front.GetValueOrDefault(d.CountryId) + d.Org * d.Hp / 100f;
                divs[d.CountryId] = divs.GetValueOrDefault(d.CountryId) + 1;
            }

            Header(mine.Count == 0 ? "Sem guerras em curso" : "Guerras em curso");
            foreach (var war in mine)
            {
                int foe = war.EnemyOf(pid);
                var (box, card) = Card();
                var title = new HBoxContainer();
                var fl = Flags.Rect(22);
                if (w.Countries.TryGetValue(foe, out var fc)) { fl.Texture = Flags.Of(fc.Tag); fl.Visible = fl.Texture is not null; }
                title.AddChild(fl);
                title.AddChild(Ui.Grow(Ui.Lbl($"⚔ contra {Name(w, foe)}", 20)));
                title.AddChild(Ui.Lbl($"{w.Clock.Day - war.StartDay} dias", 16));
                card.AddChild(title);

                Compare(card, "Regiões tomadas", war.Side(pid).RegionsTaken, war.Enemy(pid).RegionsTaken);
                Compare(card, "Batalhas ganhas", war.Side(pid).BattlesWon, war.Enemy(pid).BattlesWon);
                Compare(card, "Divisões perdidas", war.Side(pid).DivisionsLost, war.Enemy(pid).DivisionsLost, lowerIsBetter: true);
                Compare(card, "Exército no terreno", front.GetValueOrDefault(pid), front.GetValueOrDefault(foe),
                        left: divs.GetValueOrDefault(pid) + " div", right: divs.GetValueOrDefault(foe) + " div");

                // balança dos campos: uma guerra parada continua a render homens a quem aguenta melhor
                if (PrisonerView.Balance(w, pid, foe) is VBoxContainer pris) card.AddChild(pris);
                // e a mesa da troca: homem por homem sem esperar pela paz, se eles assinarem
                int foeId = foe;
                // a proposta deles primeiro: a iniciativa é do outro lado e não pode ficar escondida
                if (OfferView.Card(w, pid, foeId, o => Answer(pid, o, true), o => Answer(pid, o, false),
                                   OnShowRegion is null ? null : Show) is VBoxContainer post)
                    card.AddChild(post);
                if (PrisonerView.Exchange(w, pid, foeId, () => Swap(pid, foeId)) is VBoxContainer swap) card.AddChild(swap);

                Goals(w, card, war, pid, foe);

                int stale = w.Clock.Day - war.LastProgressDay;
                if (stale > 0) card.AddChild(Ui.Lbl($"Frente parada há {stale} dias", 15));
                Deal(w, card, pid, foe);
                _body.AddChild(box);
            }

            if (past.Count > 0)
            {
                Header("Guerras terminadas");
                foreach (var r in past)
                {
                    var (box, card) = Card();
                    string verdict = r.Winner is null ? "Empate" : r.Winner == pid ? "Vitória" : "Derrota";
                    var colour = r.Winner is null ? Ui.TextDim : r.Winner == pid ? Ui.Good : Ui.Danger;
                    var title = new HBoxContainer();
                    title.AddChild(Ui.Grow(Ui.Lbl($"{Name(w, r.A == pid ? r.B : r.A)} · {r.Days} dias", 19)));
                    var v = Ui.Lbl(verdict, 19); v.AddThemeColorOverride("font_color", colour); title.AddChild(v);
                    card.AddChild(title);
                    card.AddChild(Ui.Lbl($"Regiões {r.Regions(pid)}–{r.Regions(r.A == pid ? r.B : r.A)}   ·   " +
                                         $"Batalhas {r.Battles(pid)}–{r.Battles(r.A == pid ? r.B : r.A)}   ·   " +
                                         $"Divisões perdidas {r.Losses(pid)}", 16));
                    _body.AddChild(box);
                }
            }
        }
        catch (Exception ex) { GD.PushError("WarPanel.Fill: " + ex); }
    }

    /// <summary>Mesa de negociação: escolhem-se as regiões a exigir e vê-se, antes de propor, se o outro
    /// lado assina — a pressão que sofre (ocupação, exércitos, desgaste, capital) contra o preço do que se
    /// lhe pede (PeaceTerms.Evaluate). Sem isto o jogador só podia adivinhar termos e levar recusa atrás
    /// de recusa, enquanto a IA fechava as guerras dela sozinha.</summary>
    private void Deal(World w, VBoxContainer card, int pid, int foe)
    {
        var head = new HBoxContainer();
        head.AddChild(Ui.Grow(Ui.Lbl("Negociação", 15)));
        head.AddChild(Ui.Btn(_deal == foe ? "Fechar mesa" : "Negociar paz", () => ToggleDeal(foe), 190));
        card.AddChild(head);
        if (_deal != foe) return;

        // candidatas: o que já lhe ocupamos primeiro, depois o objectivo de guerra que ainda não é nosso
        var candidates = PeaceTerms.OccupiedRegions(w, pid, foe)
            .OrderByDescending(id => w.Regions[id].Population).ThenBy(id => id).ToList();
        foreach (int id in w.Wars[World.WarKey(pid, foe)].Side(pid).Goals)
            if (!candidates.Contains(id) && w.Regions.TryGetValue(id, out var gr) && gr.OwnerId == foe) candidates.Add(id);
        _demand.IntersectWith(candidates);

        if (candidates.Count == 0)
        {
            card.AddChild(Ui.Lbl("Nada para exigir: não ocupas nada dele. Resta a paz branca.", 15));
        }
        else
        {
            var chips = new HFlowContainer();
            foreach (int id in candidates)
            {
                int rid = id;
                var r = w.Regions[rid];
                bool on = _demand.Contains(rid);
                bool held = r.ControllerId == pid;
                var b = Ui.Btn((on ? "✔ " : "") + r.Name + (held ? "" : " (livre)"), () => ToggleRegion(rid), 0,
                               on ? Ui.Kind.Primary : Ui.Kind.Normal);
                b.TooltipText = held ? "Já ocupada: sai barata na mesa" : "Não ocupada: custa quase o dobro";
                chips.AddChild(b);
            }
            card.AddChild(chips);
        }

        var verdict = PeaceTerms.Evaluate(w, pid, foe, _demand.ToList());
        float scale = MathF.Max(0.5f, MathF.Max(verdict.Pressure, verdict.Price));
        var scales = new HBoxContainer(); scales.AddThemeConstantOverride("separation", 8);
        scales.AddChild(Ui.Lbl($"Pressão {verdict.Pressure:0.00}", 15));
        scales.AddChild(Ui.Bar(verdict.Pressure / scale, Ui.Good, 130f));
        scales.AddChild(Ui.Lbl($"Preço {verdict.Price:0.00}", 15));
        scales.AddChild(Ui.Bar(verdict.Price / scale, Ui.Danger, 130f));
        card.AddChild(scales);

        var state = Ui.Lbl(_demand.Count == 0 ? "Escolhe o que queres exigir"
                          : verdict.Accepted ? "Nestes termos, assinam" : "Nestes termos, recusam", 16);
        state.AddThemeColorOverride("font_color", _demand.Count == 0 ? Ui.TextDim : verdict.Accepted ? Ui.Good : Ui.Danger);
        card.AddChild(state);

        var actions = new HBoxContainer();
        actions.AddChild(Ui.Btn("Termos sugeridos", () => Auto(pid, foe), 200));
        actions.AddChild(Ui.Btn("Exigir paz", () => DemandPeace(pid, foe), 170, Ui.Kind.Primary));
        actions.AddChild(Ui.Btn("Paz branca", () => WhitePeace(pid, foe), 170));
        card.AddChild(actions);
    }

    /// <summary>Propõe a troca de prisioneiros a este inimigo. O veredicto já estava no cartão; aqui só
    /// se despacha e se diz quantos homens voltaram.</summary>
    private void Swap(int pid, int foe) => _game.RunWhenIdle(() =>
    {
        var offer = PrisonerExchange.Evaluate(_game.World, pid, foe);
        var err = _game.Dispatch(new ExchangePrisonersCommand(pid, foe));
        if (err is not null) { _game.Notify(err); return; }
        _game.Notify($"Troca feita: {PrisonerView.Short(offer.Home)} dos nossos a caminho de casa");
        Fill();
    });

    /// <summary>Responde a uma proposta que este inimigo pôs na mesa (paz ou troca).</summary>
    private void Answer(int pid, PendingOffer offer, bool accept) => _game.RunWhenIdle(() =>
    {
        var deal = PrisonerExchange.Evaluate(_game.World, offer.FromId, pid);
        var err = _game.Dispatch(new AnswerOfferCommand(pid, offer.FromId, accept, offer.Kind));
        if (err is not null) { _game.Notify(err); return; }
        string land = _game.World.Regions.TryGetValue(offer.RegionId, out var lr) ? lr.Name : "uma região";
        _game.Notify(!accept ? "Proposta recusada"
                    : offer.Kind == "regiao" ? $"Paz assinada: {land} passa a ser nossa"
                    : offer.Kind == "paz" ? "Paz assinada: a guerra acabou onde estava"
                    : $"Troca aceite: {PrisonerView.Short(deal.Home)} dos nossos a caminho de casa");
        _deal = null; _demand.Clear();
        Fill();
    });

    /// <summary>Fecha o painel e manda o mapa para a região: ver a terra é mais forte do que lê-la.</summary>
    private void Show(int regionId)
    {
        Close();
        OnShowRegion?.Invoke(regionId);
    }

    private void ToggleDeal(int foe) => _game.RunWhenIdle(() =>
    {
        _deal = _deal == foe ? null : foe;
        _demand.Clear();
        Fill();
    });

    private void ToggleRegion(int regionId) => _game.RunWhenIdle(() =>
    {
        if (!_demand.Add(regionId)) _demand.Remove(regionId);
        Fill();
    });

    /// <summary>Enche a mesa com a maior exigência que o outro lado ainda assina.</summary>
    private void Auto(int pid, int foe) => _game.RunWhenIdle(() =>
    {
        _demand.Clear();
        foreach (int id in PeaceTerms.Suggest(_game.World, pid, foe)) _demand.Add(id);
        if (_demand.Count == 0) _game.Notify("Ainda não há termos que ele aceite — continua a guerra");
        Fill();
    });

    private void DemandPeace(int pid, int foe) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new DemandPeaceCommand(pid, foe, _demand.ToList()));
        if (err is not null) { _game.Notify(err); return; }
        if (!_game.World.AreAtWar(pid, foe)) { _deal = null; _demand.Clear(); }
        Fill();
    });

    private void WhitePeace(int pid, int foe) => _game.RunWhenIdle(() =>
    {
        var err = _game.Dispatch(new OfferPeaceCommand(pid, foe));
        if (err is not null) { _game.Notify(err); return; }
        if (!_game.World.AreAtWar(pid, foe)) { _deal = null; _demand.Clear(); }
        Fill();
    });

    /// <summary>Objectivo de guerra: o que viemos buscar, quanto já está nas nossas mãos e o que eles
    /// nos pedem a nós. Cada região é uma etiqueta — verde quando já é nossa, cinzenta enquanto não for.</summary>
    private static void Goals(World w, VBoxContainer card, WarInfo war, int pid, int foe)
    {
        var ours = war.Side(pid).Goals.ToList();
        if (ours.Count > 0)
        {
            int held = ours.Count(id => w.Regions.TryGetValue(id, out var r) && r.ControllerId == pid);
            bool met = held == ours.Count;
            var head = new HBoxContainer();
            head.AddChild(Ui.Grow(Ui.Lbl("Objectivo de guerra", 15)));
            var state = Ui.Lbl(met ? "cumprido" : $"{held}/{ours.Count}", 15);
            state.AddThemeColorOverride("font_color", met ? Ui.Good : Ui.TextDim);
            head.AddChild(state);
            card.AddChild(head);
            card.AddChild(Ui.Bar(ours.Count == 0 ? 0f : (float)held / ours.Count, met ? Ui.Good : new Color(1f, 0.82f, 0.25f), 200f));
            card.AddChild(Chips(w, ours, pid));
        }
        var theirs = war.Enemy(pid).Goals.ToList();
        if (theirs.Count > 0)
        {
            card.AddChild(Ui.Lbl($"{Name(w, foe)} exige", 15));
            card.AddChild(Chips(w, theirs, foe));
        }
    }

    /// <summary>Etiquetas com o nome de cada região do objectivo; verdes quando `holder` já a controla.</summary>
    private static Control Chips(World w, IEnumerable<int> regionIds, int holder)
    {
        var box = new HFlowContainer();
        foreach (int id in regionIds)
        {
            bool held = w.Regions.TryGetValue(id, out var r) && r.ControllerId == holder;
            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", Ui.Box((held ? Ui.Good : Ui.SurfaceHi) with { A = held ? 0.35f : 0.6f }, 6));
            var l = Ui.Lbl((held ? "✔ " : "") + (r?.Name ?? "#" + id), 16);
            chip.AddChild(l);
            box.AddChild(chip);
        }
        return box;
    }

    /// <summary>Linha de comparação: número nosso, barra dupla, número deles. A barra dá a proporção
    /// de relance — verde do nosso lado, vermelho do inimigo (trocados quando menos é melhor).</summary>
    private static void Compare(VBoxContainer card, string label, float mine, float theirs,
                                bool lowerIsBetter = false, string? left = null, string? right = null)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Ui.Lbl(left ?? Fmt(mine), 17));
        row.AddChild(Ui.Grow(Duo(mine, theirs, lowerIsBetter)));
        row.AddChild(Ui.Lbl(right ?? Fmt(theirs), 17));
        card.AddChild(Ui.Lbl(label, 15));
        card.AddChild(row);
    }

    private static string Fmt(float v) => v >= 1000f ? $"{v / 1000f:0.0}k" : $"{v:0}";

    private static Control Duo(float mine, float theirs, bool lowerIsBetter)
    {
        var box = new HBoxContainer { CustomMinimumSize = new Vector2(140, 12), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        box.AddThemeConstantOverride("separation", 3);
        float total = mine + theirs;
        var ours = lowerIsBetter ? Ui.Danger : Ui.Good;
        var yours = lowerIsBetter ? Ui.Good : Ui.Danger;
        if (total <= 0f)   // nada aconteceu ainda: barra neutra, para a linha não desaparecer
        {
            box.AddChild(new ColorRect { Color = Ui.SurfaceHi, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            return box;
        }
        box.AddChild(new ColorRect { Color = ours, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = MathF.Max(0.02f, mine / total) });
        box.AddChild(new ColorRect { Color = yours, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = MathF.Max(0.02f, theirs / total) });
        return box;
    }

    /// <summary>Cartão: a moldura que entra na lista e o VBox onde se escreve.</summary>
    private static (PanelContainer Box, VBoxContainer Body) Card()
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface with { A = 0.85f }, 10));
        var v = new VBoxContainer(); p.AddChild(v);
        return (p, v);
    }

    private static string Name(World w, int id) => w.Countries.TryGetValue(id, out var c) ? c.Name : "#" + id;

    private void Header(string text) { var l = Ui.Lbl(text, 20); l.Modulate = new Color(1f, 0.85f, 0.4f); _body.AddChild(l); }
}
