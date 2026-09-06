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
    private readonly Dictionary<string, string> _terrainNames = new();
    private Label _title = null!, _sub = null!;
    private HBoxContainer _scale = null!;
    private ColorRect _scaleA = null!, _scaleD = null!;
    private VBoxContainer _left = null!, _right = null!;
    private int _regionId;
    private string _lastKey = "";

    public void Setup(Game game)
    {
        _game = game;
        try { foreach (var r in game.StaticDb.Query("SELECT id,name FROM terrain")) _terrainNames[(string)r["id"]!] = (string)r["name"]!; }
        catch (Exception ex) { GD.PushError("BattlePanel terrain: " + ex.Message); }

        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.30f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.078f, 0.086f, 0.098f, 0.96f)));

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6); AddChild(v);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8); v.AddChild(head);
        _title = Ui.Lbl("", 22); head.AddChild(Ui.Grow(_title));
        head.AddChild(Ui.Btn("Fechar", Close));
        _sub = Ui.Lbl("", 16); _sub.AddThemeColorOverride("font_color", Ui.TextDim); v.AddChild(_sub);

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

        // o dia e o estado de cada divisão entram na chave: sem isso as barras congelavam no primeiro dia
        string key = _regionId + "|" + (b?.Days ?? -1) + "|" + width + "|" + string.Join(",",
            att.Concat(def).Select(d => $"{d.Id}:{d.Org:0}:{d.Hp:0}"));
        if (key == _lastKey) return;
        _lastKey = key;

        string terrain = _terrainNames.GetValueOrDefault(r.Terrain, r.Terrain);
        _title.Text = b is null ? $"⚔ {r.Name}: sem batalha" : $"⚔ Batalha em {r.Name}";
        float orgA = att.Sum(d => d.Org), orgD = def.Sum(d => d.Org);
        _sub.Text = b is null
            ? $"{terrain}  ·  frente de {width} divisões por lado"
            : $"{terrain}  ·  {b.Days} dia{(b.Days == 1 ? "" : "s")}  ·  frente de {width} por lado"
              + $"  ·  organização {orgA:0} contra {orgD:0}"
              + (r.Fort > 0 ? $"  ·  🏰 forte {r.Fort}" : "") + (r.River ? "  ·  🌊 rio pelo meio" : "");

        float total = MathF.Max(1f, orgA + orgD);
        _scaleA.SizeFlagsStretchRatio = MathF.Max(0.02f, orgA / total);
        _scaleD.SizeFlagsStretchRatio = MathF.Max(0.02f, orgD / total);
        _scale.Visible = b is not null;

        int attCountry = b?.AttackerCountryId ?? r.ControllerId;
        int defCountry = b is null ? def.FirstOrDefault()?.CountryId ?? 0 : r.ControllerId;
        Side(_left, w, attCountry, b is null ? "guarda o terreno" : "ataca", lineA, resA, width, Ui.Accent);
        Side(_right, w, defCountry, b is null ? "também aqui" : "defende", lineD, resD, width, Ui.Danger);
        _right.Visible = b is not null || def.Count > 0;
    }

    private static void Side(VBoxContainer box, World w, int countryId, string role,
                             List<Division> line, List<Division> reserve, int width, Color tint)
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

        foreach (var d in line) box.AddChild(Row(w, d, tint, false));
        if (reserve.Count == 0) return;
        var rHead = Ui.Lbl($"reserva ({reserve.Count}) — entra quando a linha se partir", 14);
        rHead.AddThemeColorOverride("font_color", Ui.TextDim);
        box.AddChild(rHead);
        foreach (var d in reserve) box.AddChild(Row(w, d, tint, true));
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
    public int Smoke(int fallbackRegionId)
    {
        var w = _game.World;
        int id = w.ActiveBattles.FirstOrDefault()?.RegionId
                 ?? w.Regions.Values.Where(x => x.ControllerId == _game.PlayerId)
                        .OrderByDescending(x => x.DivisionIds.Count).ThenBy(x => x.Id)
                        .FirstOrDefault()?.Id
                 ?? fallbackRegionId;
        Open(id); Refresh();
        int rows = _left.GetChildCount() + _right.GetChildCount();
        Close();
        return rows;
    }
}
