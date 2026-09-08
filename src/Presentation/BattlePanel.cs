using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Ecrã de batalha: os dois lados frente a frente, como no HoI4. Até agora uma batalha era uma linha
/// de texto na ficha da região — "batalha (3 dias): PRT ataca — org 180 vs 140" — e não se via nada do que
/// estava a acontecer: quem estava na linha, quem esperava atrás, quem ia a cair.
///
/// Em cima, a balança: uma barra dividida ao meio pela proporção de organização dos dois lados, que é o que
/// decide quem cede primeiro. Por baixo, as duas colunas — atacante à esquerda, defensor à direita — cada uma
/// com a sua linha da frente (tantas divisões quantas a largura do terreno deixa; ver Frontage) e a reserva
/// por baixo, apagada, à espera de vez. Cada divisão leva duas barras: organização e vida.
///
/// Só lê o World. Quem faz as contas é o Core (Frontage, CombatSystem).</summary>
public partial class BattlePanel : PanelContainer
{
    private Game _game = null!;
    private Label _title = null!, _sub = null!;
    private HBoxContainer _scale = null!;
    private HFlowContainer _field = null!;      // as condições do campo: chão, rio, forte, frente, tempo
    private ColorRect _scaleA = null!, _scaleD = null!;
    private VBoxContainer _left = null!, _right = null!;
    private int _regionId;
    private string _lastKey = "";
    private CombatSide? _balA, _balD;

    public void Setup(Game game)
    {
        _game = game;

        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.078f, 0.086f, 0.098f, 1f)));

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6); AddChild(v);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        _title = Ui.Lbl("", 22); head.AddChild(Ui.Grow(_title));
        head.AddChild(Ui.Btn("Fechar", Close));
        _sub = Ui.Lbl("", 16); _sub.AddThemeColorOverride("font_color", Ui.TextDim); v.AddChild(_sub);

        // a barra das condições do campo: o que é igual para os dois lados, em chapas e não em texto corrido
        _field = new HFlowContainer();
        _field.AddThemeConstantOverride("h_separation", 4);
        _field.AddThemeConstantOverride("v_separation", 4);
        v.AddChild(_field);

        // A balança: uma barra só, partida ao meio pela proporção de organização dos dois lados.
        _scale = new HBoxContainer { CustomMinimumSize = new Vector2(0, 14) };
        _scale.AddThemeConstantOverride("separation", 2);
        _scaleA = new ColorRect { Color = Ui.Accent, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scaleD = new ColorRect { Color = Ui.Danger, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scale.AddChild(_scaleA); _scale.AddChild(_scaleD);
        v.AddChild(_scale);
        v.AddChild(Ui.Rule());

        var cols = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 10);
        v.AddChild(cols);
        cols.AddChild(Ui.Grow(Column(out _left)));
        cols.AddChild(Ui.Grow(Column(out _right)));
    }

    private static ScrollContainer Column(out VBoxContainer body)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body = Ui.Grow(new VBoxContainer());
        body.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(body);
        return scroll;
    }

    /// <summary>Abre o ecrã na batalha desta região (sem batalha, diz que não há).</summary>
    public void Open(int regionId)
    {
        _regionId = regionId; _lastKey = "";
        _game.RunWhenIdle(() => { Fill(); Visible = true; Ui.FadeIn(this); });
    }

    public void Close() => Visible = false;

    /// <summary>Chamado pelo Hud a cada refresco: a batalha muda todos os dias.</summary>
    public void Refresh() { if (Visible) Fill(); }

    private void Fill()
    {
        var w = _game.World;
        if (!w.Regions.TryGetValue(_regionId, out var r)) return;
        var b = w.ActiveBattles.FirstOrDefault(x => x.RegionId == _regionId);

        // Sem batalha o ecrã continua a servir: mostra quem está no terreno e quantos deles caberiam na frente.
        var here = b is null
            ? (_game.PlayerId is int pid ? Vision.DivisionsIn(w, pid, r) : r.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>()).ToList()
            : new List<Division>();
        var att = b is null ? here.Where(d => d.CountryId == r.ControllerId).ToList()
                            : b.Attackers.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>().ToList();
        var def = b is null ? here.Where(d => d.CountryId != r.ControllerId).ToList()
                            : b.Defenders.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>().ToList();
        int width = Frontage.Width(w, r);
        var (lineA, resA) = Frontage.Split(w, r, att);
        var (lineD, resD) = Frontage.Split(w, r, def);

        // O balanço do dia: porque é que cada lado se bate como se bate. Sem batalha continua a valer — é o
        // que a tropa que ali está traria se fosse atacada hoje, e é a pergunta que se faz antes de mandar
        // marchar, não depois. Sem inimigo do outro lado, as parcelas do lado inteiro (informações, céu)
        // não aparecem: não há contra quem as medir.
        int attC = b?.AttackerCountryId ?? r.ControllerId;
        int defC = b is null ? def.FirstOrDefault()?.CountryId ?? 0 : r.ControllerId;
        _balA = lineA.Count == 0 ? null : CombatSystem.Explain(w, r, lineA, b is not null, attC, defC);
        _balD = lineD.Count == 0 ? null : CombatSystem.Explain(w, r, lineD, attacking: false, defC, attC);

        // o dia e o estado de cada divisão entram na chave: sem isso as barras congelavam no primeiro dia
        string key = _regionId + "|" + (b?.Days ?? -1) + "|" + width
            // as condições do campo entram na chave: o tempo muda quatro vezes por ano e o forte muda com a
            // guerra, e sem isto a barra de chapas ficava congelada no dia em que se abriu
            // as tácticas também: mudam de bloco em bloco sem ninguém mandar, e uma chapa congelada mentia
            + "|" + Tactics.Line(w, r)
            + "|" + r.Fort + r.River + w.Season?.Id + "|" + string.Join(",",
            att.Concat(def).Select(d => $"{d.Id}:{d.Org:0}:{d.Hp:0}"));
        if (key == _lastKey) return;
        _lastKey = key;

        _title.Text = b is null
            ? $"⚔ {r.Name} ({GroundView.Name(w, r.Terrain)}): sem batalha"
            : $"⚔ Batalha em {r.Name} ({GroundView.Name(w, r.Terrain)})";
        float orgA = att.Sum(d => d.Org), orgD = def.Sum(d => d.Org);
        // o subtítulo ficou com o que muda de dia para dia; o chão, o rio, o forte, a frente e o tempo
        // passaram para as chapas da barra de baixo, onde se vêem sem se lerem
        _sub.Text = b is null
            ? "sem batalha: o que a tropa que aqui está traria se fosse atacada hoje"
            : $"{b.Days} dia{(b.Days == 1 ? "" : "s")}  ·  organização {orgA:0} contra {orgD:0}"
              + (_balA is null || _balD is null ? ""
                 : $"  ·  força por divisão {_balA.Strength:0.00} contra {_balD.Strength:0.00}");
        Ui.Clear(_field);
        foreach (var f in BattleField.Parts(w, r))
        {
            var plate = Ui.Counter(Glyph.Make(f.Glyph, 17, Ui.Accent), out var value, out var note);
            value.Text = f.Value;
            note.Text = f.Name;
            plate.TooltipText = $"{f.Name}: {f.Value}\n{f.Note}";
            _field.AddChild(plate);
        }

        float total = MathF.Max(1f, orgA + orgD);
        _scaleA.SizeFlagsStretchRatio = MathF.Max(0.02f, orgA / total);
        _scaleD.SizeFlagsStretchRatio = MathF.Max(0.02f, orgD / total);
        _scale.Visible = b is not null;

        // a táctica de cada lado: a chapa que diz o que ele está a tentar hoje e se o outro já lha leu
        Side(_left, w, attC, b is null ? "guarda o terreno" : "ataca", lineA, resA, width, Ui.Accent, _balA,
             Tactics.Plate(w, r, attacking: true));
        Side(_right, w, defC, b is null ? "também aqui" : "defende", lineD, resD, width, Ui.Danger, _balD,
             Tactics.Plate(w, r, attacking: false));
        _right.Visible = b is not null || def.Count > 0;
    }

    private static void Side(VBoxContainer box, World w, int countryId, string role,
                             List<Division> line, List<Division> reserve, int width, Color tint,
                             CombatSide? balance = null, FieldPart? tactic = null)
    {
        Ui.Clear(box);
        var c = w.Countries.GetValueOrDefault(countryId);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 6);
        if (c is not null)
        {
            var flag = Flags.Rect(20); flag.Texture = Flags.Of(c.Tag); head.AddChild(flag);
        }
        var name = Ui.Lbl($"{c?.Name ?? "—"}  ·  {role}", 18);
        name.AddThemeColorOverride("font_color", tint);
        head.AddChild(Ui.Grow(name));
        box.AddChild(head);

        // pips da frente: quantos lugares da linha estão ocupados por este lado
        var frame = new HBoxContainer(); frame.AddThemeConstantOverride("separation", 6);
        var tag = Ui.Lbl($"linha {line.Count}/{width}", 14);
        tag.AddThemeColorOverride("font_color", Ui.TextDim);
        frame.AddChild(tag);
        frame.AddChild(Ui.Pips(line.Count, width, tint));
        box.AddChild(frame);
        // a táctica deste lado, em chapa: é o que ele está a tentar hoje, e o valor já traz lá dentro a
        // leitura do inimigo — a mesma conta com que a batalha se bate, não uma segunda versão dela
        if (tactic is FieldPart t)
        {
            var plate = Ui.Counter(Glyph.Make(t.Glyph, 17, tint), out var value, out var note);
            value.Text = t.Value;
            note.Text = t.Name;
            plate.TooltipText = $"{t.Name}: {t.Value}\n{t.Note}";
            box.AddChild(plate);
        }
        if (balance is not null) Balance(box, balance);

        foreach (var d in line) box.AddChild(Row(w, d, tint, false));
        if (reserve.Count == 0) return;
        var rHead = Ui.Lbl($"reserva ({reserve.Count}) — entra quando a linha se partir", 14);
        rHead.AddThemeColorOverride("font_color", Ui.TextDim);
        box.AddChild(rHead);
        foreach (var d in reserve) box.AddChild(Row(w, d, tint, true));
    }

    /// <summary>O balanço do lado: a força média com que cada divisão da linha se bate e as parcelas que a
    /// fazem, com o multiplicador de cada uma. É a lista de modificadores do HoI4 posta à vista em vez de
    /// escondida num tooltip — num telemóvel um tooltip é um segredo, e esta é a informação que ensina o
    /// jogo: uma batalha perdida deixa de ser azar e passa a ser o rio, a trincheira ou o céu.
    ///
    /// Mostram-se as parcelas que pesam mesmo (a 1,00 não há nada a dizer), pela ordem de quanto mexem, e
    /// no máximo oito: uma lista que não cabe no ecrã não se lê.</summary>
    private static void Balance(VBoxContainer box, CombatSide bal)
    {
        var head = Ui.Lbl($"balanço: força {bal.Strength:0.00} por divisão", 14);
        head.AddThemeColorOverride("font_color", Ui.TextDim);
        box.AddChild(head);

        var shown = bal.Factors.Where(f => MathF.Abs(f.Mult - 1f) >= 0.005f)
                               .OrderByDescending(f => MathF.Abs(MathF.Log(MathF.Max(0.01f, f.Mult))))
                               .Take(8).ToList();
        if (shown.Count == 0)
        {
            var none = Ui.Lbl("nada a pesar neste chão", 13);
            none.AddThemeColorOverride("font_color", Ui.TextDim);
            box.AddChild(none);
            return;
        }
        foreach (var f in shown)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            var name = Ui.Lbl(f.Name, 13);
            name.AddThemeColorOverride("font_color", Ui.TextDim);
            row.AddChild(Ui.Grow(name));
            var mult = Ui.Lbl($"×{f.Mult:0.00}", 13);
            mult.AddThemeColorOverride("font_color", f.Mult >= 1f ? Ui.Good.Lightened(0.2f) : Ui.Danger.Lightened(0.15f));
            row.AddChild(mult);
            box.AddChild(row);
        }
    }

    /// <summary>Uma divisão: nome, barra de organização e barra de vida. Em reserva, tudo apagado.</summary>
    private static PanelContainer Row(World w, Division d, Color tint, bool reserve)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(reserve ? Ui.Ink : Ui.Surface, 5));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2); card.AddChild(v);

        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 6); v.AddChild(top);
        var title = Ui.Lbl(DivisionView.Title(w, d), 15);
        if (reserve) title.AddThemeColorOverride("font_color", Ui.TextDim);
        top.AddChild(Ui.Grow(title));
        var num = Ui.Lbl($"{d.Org:0} org  ·  {d.Hp:0} vida", 13);
        num.AddThemeColorOverride("font_color", Ui.TextDim);
        top.AddChild(num);

        var bars = new HBoxContainer(); bars.AddThemeConstantOverride("separation", 4); v.AddChild(bars);
        bars.AddChild(Ui.Grow(Ui.Bar(d.Org / 100f, reserve ? Ui.Frame : tint, 60f)));
        bars.AddChild(Ui.Grow(Ui.Bar(d.Hp / 100f, reserve ? Ui.Frame : Ui.Good, 60f)));
        return card;
    }

    /// <summary>--smoke: abre a batalha que houver e, se não houver nenhuma, a região com mais tropa nossa
    /// (o caminho sem batalha desenha na mesma). Diz quantas linhas saíram.</summary>
    public string Smoke(int fallbackRegionId)
    {
        var w = _game.World;
        int id = w.ActiveBattles.FirstOrDefault()?.RegionId
                 ?? w.Regions.Values.Where(x => x.ControllerId == _game.PlayerId)
                        .OrderByDescending(x => x.DivisionIds.Count).ThenBy(x => x.Id)
                        .FirstOrDefault()?.Id
                 ?? fallbackRegionId;
        Open(id); Refresh();
        int rows = _left.GetChildCount() + _right.GetChildCount();
        bool fighting = w.ActiveBattles.Any(x => x.RegionId == id);
        string balance = _balA is null ? "sem tropa na linha, sem balanço"
            : _balD is null
                ? $"{(fighting ? "balanço" : "sem batalha; balanço de quem lá está")} de {_balA.Factors.Count} parcelas"
                  + $" (força {_balA.Strength:0.00}; pesa mais «{Heaviest(_balA)}»)"
                : $"{(fighting ? "balanço" : "sem batalha; balanço")} de {_balA.Factors.Count} parcelas contra {_balD.Factors.Count}"
                  + $" (força {_balA.Strength:0.00} contra {_balD.Strength:0.00}"
                  + $"; pesa mais «{Heaviest(_balA)}» contra «{Heaviest(_balD)}»)";
        var reg = w.Regions[id];
        string campo = $"campo: {BattleField.Line(w, reg)}";
        Close();
        return $"{rows} linhas, {balance}, {campo}";
    }

    /// <summary>A parcela que mais mexe num lado — é o que o smoke guarda para se ver de relance se o
    /// balanço está mesmo a medir o chão em que se combate.</summary>
    private static string Heaviest(CombatSide bal)
    {
        var f = bal.Factors.Where(x => MathF.Abs(x.Mult - 1f) >= 0.005f)
                           .OrderByDescending(x => MathF.Abs(MathF.Log(MathF.Max(0.01f, x.Mult))))
                           .FirstOrDefault();
        return f.Name is null ? "nada" : $"{f.Name} ×{f.Mult:0.00}";
    }
}
