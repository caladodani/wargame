using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Painel de produção do jogador (45% inferior): templates com custo e "+", fila com % e "×".
/// Lê o World só em Fill (mundo parado); muta só por Game.Dispatch.</summary>
public partial class ProductionPanel : PanelContainer
{
    private Game _game = null!;
    private Label _title = null!;
    private VBoxContainer _templates = null!, _queue = null!;
    private string _lastKey = "";

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.55f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 0.95f)));
        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        _title = Ui.Grow(Ui.Lbl("", 22)); head.AddChild(_title);
        head.AddChild(Ui.Btn("Fechar", Close));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        var body = Ui.Grow(new VBoxContainer()); scroll.AddChild(body);
        body.AddChild(Ui.Lbl("Modelos", 20));
        _templates = new VBoxContainer(); body.AddChild(_templates);
        body.AddChild(Ui.Lbl("Fila", 20));
        _queue = new VBoxContainer(); body.AddChild(_queue);
    }

    public void Open() { _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; }); }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (_game.PlayerId is not int pid || !w.Countries.TryGetValue(pid, out var c)) { Close(); return; }
            _title.Text = $"Produção — {c.Name}   {c.Money:0.0} pontos";
            IReadOnlyList<DivisionTemplate> tmpls;
            try { tmpls = w.Units.GetTemplates(pid); } catch (Exception ex) { GD.PushError("templates: " + ex.Message); tmpls = Array.Empty<DivisionTemplate>(); }
            var key = string.Join("|", tmpls.Select(t => t.Id)) + "#" + string.Join("|", c.Queue.Select(o => o.TemplateId + ":" + Pct(w, o)));
            if (key == _lastKey) return;
            _lastKey = key;

            Ui.Clear(_templates); Ui.Clear(_queue);
            foreach (var t in tmpls)
            {
                float cost; try { cost = w.TemplateCost(t.Id); } catch { cost = 0f; }
                int tid = t.Id;
                var row = new HBoxContainer();
                row.AddChild(Ui.Grow(Ui.Lbl($"{t.Name}   custo {cost:0.0}")));
                row.AddChild(Ui.Btn("+", () => Order(tid), 72));
                _templates.AddChild(row);
            }
            if (tmpls.Count == 0) _templates.AddChild(Ui.Lbl("Sem modelos de divisão"));
            for (int i = 0; i < c.Queue.Count; i++)
            {
                var o = c.Queue[i]; int idx = i, tid = o.TemplateId;
                string name; try { name = w.Units.GetTemplate(tid).Name; } catch { name = "T" + tid; }
                var row = new HBoxContainer();
                row.AddChild(Ui.Grow(Ui.Lbl($"{name}   {Pct(w, o)}%")));
                row.AddChild(Ui.Btn("×", () => Cancel(idx, tid), 72));
                _queue.AddChild(row);
            }
            if (c.Queue.Count == 0) _queue.AddChild(Ui.Lbl("Fila vazia"));
        }
        catch (Exception ex) { GD.PushError("ProductionPanel.Fill: " + ex); }
    }

    private static int Pct(World w, ProductionOrder o)
    {
        float cost; try { cost = w.TemplateCost(o.TemplateId); } catch { cost = 0f; }
        return cost <= 0f ? 0 : Mathf.Clamp(Mathf.RoundToInt(o.Progress / cost * 100f), 0, 100);
    }

    private void Order(int templateId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new BuildDivisionCommand(pid, templateId));
        if (err is not null) _game.Notify(err);
    });

    // O índice vem da lista desenhada; se a fila mudou entretanto (encomenda concluída) não cancela outra.
    private void Cancel(int index, int templateId) => _game.RunWhenIdle(() =>
    {
        if (_game.PlayerId is not int pid || !_game.World.Countries.TryGetValue(pid, out var c)) return;
        if (index >= c.Queue.Count || c.Queue[index].TemplateId != templateId) { _game.Notify("A fila mudou, tenta outra vez"); Refresh(); return; }
        var err = _game.Dispatch(new CancelProductionCommand(pid, index));
        if (err is not null) _game.Notify(err);
    });
}
