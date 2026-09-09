using System;
using System.Text;
using Godot;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A coluna do estado, à direita do mapa — o "outliner" do HoI4, que é o móvel mais usado daquele
/// jogo: sem abrir painel nenhum vê-se o foco a andar, os laboratórios (e os que estão parados), a fila de
/// produção, as obras nas províncias, os exércitos com o plano de batalha, as asas no céu, as esquadras no
/// mar e as missões com prazo. Cada linha tem barra, dias que faltam e leva ao ecrã que a trata.
///
/// Quem conta é o Outline (WarGame.Core): esta classe só desenha e encaminha o toque. As secções são da
/// tabela outline_section — nenhuma lista delas vive aqui. Dobra-se para o lado para devolver o mapa a quem
/// o quiser inteiro, e a chapa fechada continua a dizer quantas coisas estão a andar.</summary>
public partial class OutlinerView : PanelContainer
{
    /// <summary>Largura da coluna. Fica bem dentro dos 1152 da tela (Ui.Phone): a coluna toma menos de um
    /// terço do ecrã do telefone e o --smoke mede-a como mede tudo o resto.</summary>
    private const float Wide = 288f;

    private Game _game = null!;
    private VBoxContainer _list = null!;
    private ScrollContainer _scroll = null!;
    private Button _toggle = null!;
    private Label _count = null!;
    private bool _open = true;
    private string _painted = "";
    private float _under = 120f;

    /// <summary>O que fazer quando se toca numa linha: (target da secção, região da linha ou 0).</summary>
    public Action<string, int>? OnOpen;

    public void Setup(Game game)
    {
        _game = game;
        AnchorLeft = 1; AnchorRight = 1; AnchorTop = 0; AnchorBottom = 0;
        GrowHorizontal = GrowDirection.Begin; GrowVertical = GrowDirection.End;
        OffsetRight = -12; OffsetLeft = -(Wide + 12f);
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.92f), 6));
        MouseFilter = MouseFilterEnum.Stop;    // o toque na coluna não é pan do mapa

        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4); AddChild(v);
        var head = new HBoxContainer();
        head.AddChild(Glyph.Make("pasta", 17, Ui.Accent));
        head.AddChild(Ui.Grow(Ui.Head("Estado", 13)));
        _count = Ui.Lbl("", 13); _count.AddThemeColorOverride("font_color", Ui.TextDim); head.AddChild(_count);
        _toggle = Ui.Btn("»", Toggle, 44); head.AddChild(_toggle);
        v.AddChild(head);

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        v.AddChild(_scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        _scroll.AddChild(_list);
    }

    /// <summary>Desce com a barra de topo e nunca passa de metade do ecrã — por baixo dela ainda vive a fita
    /// dos modos de mapa, no mesmo canto.</summary>
    public void PlaceUnder(float y)
    {
        _under = y;
        OffsetTop = y;
        float tall = MathF.Max(160f, GetViewportRect().Size.Y * 0.52f - y);
        _scroll.CustomMinimumSize = new Vector2(Wide, _open ? tall : 0f);
    }

    private void Toggle()
    {
        _open = !_open;
        _toggle.Text = _open ? "»" : "«";
        _painted = "";
        PlaceUnder(_under);
        Refresh();
    }

    /// <summary>Com um painel aberto por cima do mapa a coluna sai da frente — é mobília do mapa, como a
    /// fita dos modos.</summary>
    public void SetCovered(bool covered)
    {
        _covered = covered;
        Visible = !covered && _game.PlayerId is not null;
    }

    private bool _covered;

    /// <summary>Redesenha a coluna quando alguma coisa lá dentro mudou (e à primeira vez).</summary>
    public void Refresh()
    {
        if (_game.PlayerId is not int pid) { Visible = false; return; }
        Visible = !_covered;
        var w = _game.World;
        var board = Outline.Board(w, pid);

        int total = 0;
        var key = new StringBuilder().Append(_open ? '1' : '0');
        foreach (var (sec, rows) in board)
        {
            total += rows.Count;
            key.Append(sec.Id).Append(':');
            foreach (var r in rows) key.Append(r.Title).Append('|').Append(r.Note).Append('|')
                                       .Append((int)(r.Progress * 100f)).Append('|').Append(r.Days).Append(';');
        }
        _count.Text = total.ToString();
        string k = key.ToString();
        if (k == _painted) return;
        _painted = k;

        Ui.Clear(_list);
        _scroll.Visible = _open;
        if (!_open) return;

        foreach (var (sec, rows) in board)
        {
            if (rows.Count == 0) continue;      // secção sem nada a andar não ocupa coluna
            _list.AddChild(SectionHead(sec, rows.Count));
            foreach (var r in rows) _list.AddChild(Row(sec, r));
        }
        if (_list.GetChildCount() == 0)
            _list.AddChild(Note("nada a andar — dá uma ordem e ela aparece aqui"));
    }

    private Control SectionHead(OutlineSectionDef sec, int n)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(Glyph.Make(sec.Glyph, 15, Ui.Accent));
        var name = Ui.Lbl(sec.Name.ToUpperInvariant(), 12);
        name.AddThemeColorOverride("font_color", Ui.Accent);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(name);
        var cnt = Ui.Lbl(n.ToString(), 12);
        cnt.AddThemeColorOverride("font_color", Ui.TextDim);
        row.AddChild(cnt);
        return row;
    }

    private Control Row(OutlineSectionDef sec, OutlineRow r)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 1);

        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 6);
        // O título embrulha: uma etiqueta sem autowrap é medida pelo Godot com a largura do texto inteiro,
        // e um nome comprido de divisão esticava a coluna para fora do ecrã do telefone (Ui.Phone).
        var title = Ui.Lbl(r.Title, 14);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(title);
        if (r.Days > 0)
        {
            var d = Ui.Lbl($"{r.Days} d", 13);
            d.AddThemeColorOverride("font_color", r.Days <= 3 ? Ui.Good : Ui.TextDim);
            top.AddChild(d);
        }
        box.AddChild(top);

        if (r.Note.Length > 0) box.AddChild(Note(r.Note));
        if (r.HasBar) box.AddChild(Ui.Bar(r.Progress, r.Progress >= 0.999f ? Ui.Good : Ui.Accent, Wide - 24f));

        string target = sec.Target;
        int region = r.RegionId;
        return Ui.Click(box, () => OnOpen?.Invoke(target, region), r.Title);
    }

    private static Label Note(string text)
    {
        var l = Ui.Lbl(text, 12);
        l.AddThemeColorOverride("font_color", Ui.TextDim);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return l;
    }

    /// <summary>Para o --smoke: abre e fecha a coluna e devolve o que ela mostra.</summary>
    public string Smoke(int countryId)
    {
        _covered = false; Visible = true;      // a régua do ecrã não mede o que está escondido
        Refresh();
        int lines = _list.GetChildCount();
        Toggle(); Toggle();     // dobra e desdobra: o caminho fechado desenha-se também
        Refresh();
        return $"{Outline.Smoke(_game.World, countryId)}, {lines} chapas na coluna";
    }
}
