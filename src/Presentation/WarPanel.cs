using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Painel "Guerra": o saldo de cada guerra do jogador (regiões tomadas, batalhas ganhas,
/// divisões perdidas) em barras de comparação lado a lado, mais o arquivo das guerras já terminadas
/// (World.WarHistory). Só lê o World — quem conta é o WarStatsSystem.</summary>
public partial class WarPanel : PanelContainer
{
    private Game _game = null!;
    private VBoxContainer _body = null!;
    private string _lastKey = "";

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

    public void Open() { _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); }); }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid) { Ui.Clear(_body); _body.AddChild(Ui.Lbl("Escolhe um país primeiro", 18)); return; }
            var mine = w.Wars.Values.Where(x => x.Involves(pid)).OrderBy(x => x.StartDay).ToList();
            var past = w.WarHistory.Where(r => r.Involves(pid)).ToList();
            var key = w.Clock.Day + "|" + mine.Count + "|" + past.Count + "|" +
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

                int stale = w.Clock.Day - war.LastProgressDay;
                if (stale > 0) card.AddChild(Ui.Lbl($"Frente parada há {stale} dias", 15));
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
