using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Rodapé passivo: o que a última região tocada tem para dizer, em duas ou três linhas, sem pedir
/// nenhum toque — nem sequer o consome (MouseFilter Ignore), para não estorvar o toque simples que
/// selecciona nem o duplo que move. É onde foi parar a informação da região desde que o toque simples
/// deixou de abrir o painel a ecrã inteiro: nome, dono, população, infra-estrutura, tempo (a "temperatura"
/// da campanha) e o que ali se está a construir.
/// Lê o World só via RunWhenIdle e não muta nada.</summary>
public partial class RegionFooter : PanelContainer
{
    private Game _game = null!;
    private Label _label = null!;
    private Glyph.Plate _terrain = null!;      // a chapa do chão: vê-se antes de se ler o nome
    private int? _regionId;
    private string _painted = "";

    public void Setup(Game game)
    {
        _game = game;

        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = 0; AnchorRight = 0; AnchorTop = 1; AnchorBottom = 1;
        GrowHorizontal = GrowDirection.End; GrowVertical = GrowDirection.Begin;
        OffsetLeft = 12; OffsetBottom = -12;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.06f, 0.08f, 0.78f), 8));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        row.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(row);
        _terrain = Glyph.Make("roda", 22, Ui.TextDim);
        _terrain.MouseFilter = MouseFilterEnum.Ignore;
        _terrain.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        row.AddChild(_terrain);
        _label = Ui.Lbl("", 15);
        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.CustomMinimumSize = new Vector2(360, 0);
        _label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(_label);
    }

    /// <summary>Toque (simples ou duplo) numa região: passa a ser essa a mostrada no rodapé.</summary>
    public void Show(int regionId) => _game.RunWhenIdle(() =>
    {
        _regionId = regionId; _painted = "";
        Refresh();
    });

    /// <summary>Relê o estado (chamado pelo Hud a cada tick). Sem efeito sem região escolhida ainda.</summary>
    public void Refresh()
    {
        if (_regionId is not int rid) { Visible = false; return; }
        var w = _game.World;
        if (!w.Regions.TryGetValue(rid, out var r)) { Visible = false; return; }
        var ctrl = w.Countries.GetValueOrDefault(r.ControllerId);

        string chapa = w.TerrainDefs.TryGetValue(r.Terrain, out var td) ? td.Glyph : "roda";
        if (_terrain.GlyphName != chapa) { _terrain.GlyphName = chapa; _terrain.QueueRedraw(); }
        var line1 = $"{r.Name} · {GroundView.Name(w, r.Terrain)}{(r.Coastal ? " ⚓" : "")}"
                  + $" — {ctrl?.Name ?? "—"}{(r.ControllerId != r.OwnerId ? " (ocupada)" : "")}"
                  + $" · {r.Population / 1e6f:0.0} M hab. · Infra ×{r.Infrastructure:0.00}";

        var bits = new List<string>();
        if (SeasonView.RegionLine(w, r) is string season && season.Length > 0) bits.Add(season);
        foreach (var (bid, lvl) in r.Buildings.OrderBy(kv => kv.Key))
            if (lvl > 0 && w.BuildingDefs.TryGetValue(bid, out var bd)) bits.Add($"{bd.Name} {lvl}");
        if (r.Fort > 0) bits.Add($"🏰 Forte {r.Fort}");
        string line2 = string.Join("  ·  ", bits);

        string build = r.Project is string proj && w.BuildingDefs.TryGetValue(proj, out var pd)
                ? $"🏗 a construir {pd.Name}: {(int)MathF.Ceiling(pd.Days - r.ProjectProgress)} dias"
            : r.Building
                ? $"🏗 a melhorar a infra-estrutura: {(int)MathF.Ceiling(w.Rule("infra_build_days", 30f) - r.BuildProgress)} dias"
            : r.FortBuilding
                ? $"🏰 a fortificar: {(int)MathF.Ceiling(w.Rule("fort_build_days", 20f) - r.FortProgress)} dias"
            : "";
        var battle = w.ActiveBattles.FirstOrDefault(b => b.RegionId == r.Id);
        // com batalha a decorrer, o rodapé diz onde ela se trava — as mesmas condições que o ecrã de
        // batalha põe em chapas, aqui numa linha, sem obrigar a abrir nada
        string line3 = battle is not null
            ? $"{RegionRenderer.BattleMark}batalha a decorrer ({battle.Days} dias) · {BattleField.Line(w, r)}"
            : build;

        string key = line1 + "|" + line2 + "|" + line3;
        if (key == _painted) { Visible = true; return; }
        _painted = key;
        _label.Text = line3.Length > 0 ? $"{line1}\n{line2}\n{line3}" : line2.Length > 0 ? $"{line1}\n{line2}" : line1;
        Visible = true;
    }

    /// <summary>Um painel a tapar o ecrã esconde o rodapé (nada para ler por baixo dele mesmo).</summary>
    public void SetCovered(bool covered) { if (covered) Visible = false; else if (_regionId is not null) Refresh(); }

    /// <summary>--smoke: mostra uma região e devolve o que o rodapé ficou a dizer.</summary>
    public string Smoke(int regionId)
    {
        Show(regionId);
        return _label.Text.Replace("\n", " / ");
    }
}
