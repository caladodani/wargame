using Godot;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Ecrã de fim de campanha: a folha de serviço do país em vez das duas linhas de texto que
/// havia antes na derrota e no domínio mundial. Faixa com o veredicto e o título ganho, pontuação,
/// cartões com os números todos, as conquistas por país vencido, a divisão mais condecorada e o
/// gráfico da campanha inteira. Abre-se também a meio do jogo pelo menu ("Resumo da campanha"), com
/// veredicto "em curso" — ver como vai a partida é metade da graça de um jogo destes.
///
/// Todo o conteúdo vem do CampaignReport (WarGame.Core): aqui só se desenha.</summary>
public partial class EndScreen : PanelContainer
{
    private Game _game = null!;
    private VBoxContainer _body = null!;
    private Label _title = null!, _sub = null!;
    private PanelContainer _banner = null!;
    private Button _restart = null!;
    private string _verdict = CampaignReport.Ongoing;

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.09f, 0.97f), 10));

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 10); AddChild(v);

        _banner = new PanelContainer();
        v.AddChild(_banner);
        var bv = new VBoxContainer(); _banner.AddChild(bv);
        _title = Ui.Lbl("", 30); bv.AddChild(_title);
        _sub = Ui.Lbl("", 17); bv.AddChild(_sub);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); _body.AddThemeConstantOverride("separation", 10); scroll.AddChild(_body);

        var actions = new HBoxContainer(); v.AddChild(actions);
        _restart = Ui.Btn("Novo jogo", () => _game.NewGame(), 200, Ui.Kind.Primary);
        actions.AddChild(_restart);
        actions.AddChild(Ui.Grow(new Control()));
        actions.AddChild(Ui.Btn("Fechar", Close, 180));
    }

    /// <summary>Mostra o balanço. `verdict` é uma das constantes do CampaignReport.</summary>
    public void Show(string verdict) => _game.RunWhenIdle(() =>
    {
        _verdict = verdict;
        Fill();
        Visible = true;
        Ui.FadeIn(this, 0.25);
    });

    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            if (_game.PlayerId is not int pid) return;
            var w = _game.World;
            var r = CampaignReport.Build(w, pid, _verdict);
            Ui.Clear(_body);

            var (colour, headline) = _verdict switch
            {
                CampaignReport.Domination => (Ui.Good, "O mundo é teu"),
                CampaignReport.Defeat => (Ui.Danger, "O país capitulou"),
                _ => (Ui.Accent, "Resumo da campanha"),
            };
            _banner.AddThemeStyleboxOverride("panel", Ui.Box(colour.Darkened(0.55f), 12));
            _title.Text = $"{headline} — {CampaignReport.Rank(w, r)}";
            _title.AddThemeColorOverride("font_color", colour.Lightened(0.35f));
            _sub.Text = $"{r.CountryName} · dia {r.Days} · {r.Score} pontos";

            Cards(r,
                ("Regiões", $"{r.Regions}", Delta(r.NetRegions) + " desde o início"),
                ("População", $"{r.Population / 1_000_000f:0.#} M", $"{r.PopulationShare * 100f:0.0}% do mundo"),
                ("Guerras", $"{r.WarsWon}–{r.WarsLost}", $"{r.WarsFought} travadas"),
                ("Batalhas ganhas", $"{r.BattlesWon}", $"{r.RegionsTaken} regiões tomadas"),
                ("Exército", $"{r.Divisions}", $"{(int)r.Strength} de força útil"),
                ("Baixas", $"{r.DivisionsLost}", $"{r.RegionsGivenUp} regiões perdidas"),
                ("Condecorações", $"{r.Medals}", r.BestDivisionId is int id ? $"melhor: divisão {id}" : "sem heróis"),
                ("Avanços", $"{r.Techs + r.Focuses}", $"{r.Techs} tecnologias · {r.Focuses} focos"));

            // barra da população mundial: a única medida que vale para a vitória por domínio
            _body.AddChild(Ui.Lbl($"Quota da população mundial — vitória aos {w.Rule("victory_pop_share", 0.6f) * 100f:0}%", 15));
            _body.AddChild(Ui.Bar(Math.Clamp(r.PopulationShare / Math.Max(0.01f, w.Rule("victory_pop_share", 0.6f)), 0f, 1f), colour, 0f));

            if (r.Conquests.Count > 0)
            {
                _body.AddChild(Header("Territórios conquistados"));
                int top = r.Conquests.Max(c => c.Regions);
                foreach (var c in r.Conquests.Take(8))
                {
                    var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
                    row.AddChild(Ui.Grow(Ui.Lbl(c.Name, 17)));
                    row.AddChild(Ui.Lbl($"{c.Regions} regiões · {c.Population / 1_000_000f:0.#} M", 15));
                    _body.AddChild(row);
                    _body.AddChild(Ui.Bar(top == 0 ? 0f : (float)c.Regions / top, Ui.Good, 0f));
                }
            }

            if (r.BestDivisionId is int hero && w.Divisions.TryGetValue(hero, out var d))
            {
                _body.AddChild(Header("Divisão mais condecorada"));
                var card = new PanelContainer();
                card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface with { A = 0.85f }, 10));
                _body.AddChild(card);
                var cv = new VBoxContainer(); card.AddChild(cv);
                string name = d.Name ?? SafeTemplate(w, d);
                cv.AddChild(Ui.Lbl($"🎖 {name}", 19));
                cv.AddChild(Ui.Lbl($"{r.BestDivisionMedals} condecorações · {r.BestDivisionBattles} batalhas · XP {d.Xp:0}", 15));
                cv.AddChild(Ui.Bar(Math.Clamp(d.Xp / w.Rule("xp_max", 100f), 0f, 1f), new Color(1f, 0.82f, 0.25f), 0f));
            }

            _body.AddChild(Header("A campanha, dia a dia"));
            if (r.PowerPeak > 0f)
            {
                string peak = $"Auge da potência: {r.PowerPeak:0.0} ao dia {r.PowerPeakDay}";
                var note = Ui.Lbl(r.PowerFromPeak < -0.05f
                    ? $"{peak} — acabámos {MathF.Abs(r.PowerFromPeak):0.0} abaixo disso"
                    : $"{peak} — e é onde estamos", 16);
                note.AddThemeColorOverride("font_color", r.PowerFromPeak < -0.05f ? Ui.Danger : Ui.Good);
                _body.AddChild(note);
            }
            var chart = new HistoryChart(); chart.Setup(_game);
            chart.SetMetric("power");                                    // o fim da campanha conta-se pela nota
            var crow = new HBoxContainer(); _body.AddChild(crow);
            foreach (var (m, label) in new[] { ("power", "Potência"), ("divisions", "Divisões"), ("regions", "Regiões"), ("money", "Pontos") })
                crow.AddChild(Ui.Btn(label, () => chart.SetMetric(m), 110));
            _body.AddChild(Ui.Grow(chart));

            _restart.Text = _verdict == CampaignReport.Ongoing ? "Recomeçar" : "Novo jogo";
        }
        catch (Exception ex) { GD.PushError("EndScreen: " + ex); }
    }

    /// <summary>Cartões em grelha de duas colunas: número grande, legenda por cima e nota por baixo.</summary>
    private void Cards(CampaignReport.Report r, params (string Label, string Value, string Note)[] cards)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        _body.AddChild(Ui.Grow(grid));
        foreach (var (label, value, note) in cards)
        {
            var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface with { A = 0.8f }, 10));
            var v = new VBoxContainer(); card.AddChild(v);
            v.AddChild(Ui.Lbl(label, 15));
            v.AddChild(Ui.Lbl(value, 26));
            v.AddChild(Ui.Lbl(note, 15));
            grid.AddChild(card);
        }
    }

    private static Label Header(string text)
    {
        var l = Ui.Lbl(text, 20);
        l.AddThemeColorOverride("font_color", Ui.TextDim);
        return l;
    }

    private static string Delta(int n) => n > 0 ? $"+{n}" : n.ToString();

    private static string SafeTemplate(WarGame.Core.Model.World w, WarGame.Core.Model.Division d)
    {
        try { return w.Units.GetTemplate(d.TemplateId).Name; } catch { return "Divisão " + d.Id; }
    }
}
