using Godot;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Fita dos modos de mapa, no canto inferior direito: o mapa político de sempre e, por baixo dele,
/// as pinturas por conta — abastecimento, resistência, indústria, população. É a fila de botões que qualquer
/// jogo de grande estratégia tem à beira do mapa, e que aqui faltava: os números existiam todos mas viviam
/// dentro da ficha de cada província, uma de cada vez.
///
/// Os modos vêm da base de dados (tabela map_mode) pelo MapModes; a barra só desenha um botão por linha e
/// manda o RegionRenderer repintar. Por baixo fica a legenda: a escala de cor com as duas pontas escritas,
/// para o degradê querer dizer alguma coisa.</summary>
public partial class MapModeBar : PanelContainer
{
    private Game _game = null!;
    private RegionRenderer _regions = null!;
    private VBoxContainer _list = null!, _legend = null!;
    private Button _toggle = null!;
    private bool _open = true;
    private string _painted = "";

    public void Setup(Game game, RegionRenderer regions)
    {
        _game = game; _regions = regions;
        AnchorLeft = 1; AnchorRight = 1; AnchorTop = 1; AnchorBottom = 1;
        GrowHorizontal = GrowDirection.Begin; GrowVertical = GrowDirection.Begin;
        OffsetRight = -12; OffsetBottom = -12;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.92f), 6));
        MouseFilter = MouseFilterEnum.Stop;    // o toque na fita não é pan do mapa

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); AddChild(v);
        var head = new HBoxContainer();
        head.AddChild(Ui.Grow(Ui.Head("Modo", 13)));
        _toggle = Ui.Btn("–", Toggle, 44); head.AddChild(_toggle);
        v.AddChild(head);
        _list = new VBoxContainer(); _list.AddThemeConstantOverride("separation", 2); v.AddChild(_list);
        _legend = new VBoxContainer(); _legend.AddThemeConstantOverride("separation", 2); v.AddChild(_legend);
    }

    /// <summary>Redesenha a fita quando o modo escolhido muda (e à primeira vez).</summary>
    public void Refresh()
    {
        var modes = MapModes.All(_game.World);
        string key = _regions.Mode + "|" + _open + "|" + modes.Count;
        if (key == _painted) return;
        _painted = key;

        Ui.Clear(_list); Ui.Clear(_legend);
        _list.Visible = _legend.Visible = _open;
        if (!_open) return;

        foreach (var m in modes)
        {
            bool on = m.Id == _regions.Mode;
            string id = m.Id;
            var b = Ui.Btn($"      {m.Name}", () => Pick(id), 190, on ? Ui.Kind.Primary : Ui.Kind.Normal);
            b.Alignment = HorizontalAlignment.Left;
            Glyph.Stamp(b, m.Glyph, on ? Ui.Ink : Ui.Accent);
            _list.AddChild(b);
        }

        var cur = modes.FirstOrDefault(m => m.Id == _regions.Mode);
        if (cur is null || cur.Metric == "owner") return;
        // Legenda: dez chapas do frio ao quente, com a ponta de cada lado escrita por baixo.
        var ramp = new HBoxContainer(); ramp.AddThemeConstantOverride("separation", 1);
        for (int i = 0; i < 10; i++)
            ramp.AddChild(Ui.Grow(new ColorRect { CustomMinimumSize = new Vector2(0, 10), Color = Ui.Heat(i / 9f) }));
        _legend.AddChild(ramp);
        var ends = new HBoxContainer();
        var lo = Ui.Lbl(cur.Low, 12); lo.AddThemeColorOverride("font_color", Ui.TextDim);
        var hi = Ui.Lbl(cur.High, 12); hi.AddThemeColorOverride("font_color", Ui.TextDim);
        hi.HorizontalAlignment = HorizontalAlignment.Right;
        ends.AddChild(Ui.Grow(lo)); ends.AddChild(Ui.Grow(hi));
        _legend.AddChild(ends);
    }

    private void Pick(string modeId) => _game.RunWhenIdle(() =>
    {
        _regions.SetMode(modeId);
        Refresh();
    });

    private void Toggle()
    {
        _open = !_open;
        _toggle.Text = _open ? "–" : "+";
        Refresh();
    }

    /// <summary>Um painel a tapar meio ecrã esconde a fita, como acontece ao mini-mapa.</summary>
    public void SetCovered(bool covered) => Visible = !covered;

    /// <summary>--smoke: passa por todos os modos, volta ao político e diz quantos existem.</summary>
    public int Smoke()
    {
        var modes = MapModes.All(_game.World);
        foreach (var m in modes) { _regions.SetMode(m.Id); Refresh(); }
        _regions.SetMode(MapModes.Political); Refresh();
        return modes.Count;
    }
}
