using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Painel "Mundo" (45% inferior): ranking de países (divisões, regiões, população controlada),
/// guerras activas e facções. Só leitura do World em Fill (mundo parado); tocar num país abre o CountryPanel.</summary>
public partial class WorldPanel : PanelContainer
{
    private Game _game = null!;
    private CountryPanel _countryPanel = null!;
    private VBoxContainer _body = null!;
    private string _lastKey = "";

    public void Setup(Game game, CountryPanel countryPanel)
    {
        _game = game; _countryPanel = countryPanel;
        Visible = false;
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = 0.55f; AnchorBottom = 1;
        OffsetLeft = OffsetRight = OffsetTop = OffsetBottom = 0;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.10f, 0.11f, 0.14f, 0.95f)));
        var v = new VBoxContainer(); AddChild(v);
        var head = new HBoxContainer(); v.AddChild(head);
        head.AddChild(Ui.Grow(Ui.Lbl("Mundo", 22)));
        head.AddChild(Ui.Btn("Fechar", Close));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Open() { _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; }); }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            var w = _game.World;
            var key = w.Clock.Day + "|" + w.Divisions.Count + "|" + string.Join(",", w.Wars.Keys.Select(k => k.A + ":" + k.B)) + "|" + w.ActiveSpyOps.Count;
            if (key == _lastKey) return;
            _lastKey = key;
            Ui.Clear(_body);

            var divs = new Dictionary<int, int>();
            foreach (var d in w.Divisions.Values) divs[d.CountryId] = divs.GetValueOrDefault(d.CountryId) + 1;
            var regions = new Dictionary<int, int>();
            var pop = new Dictionary<int, long>();
            long popTotal = 0;
            foreach (var r in w.Regions.Values)
            {
                regions[r.ControllerId] = regions.GetValueOrDefault(r.ControllerId) + 1;
                pop[r.ControllerId] = pop.GetValueOrDefault(r.ControllerId) + r.Population;
                popTotal += r.Population;
            }

            Header("Potências (por divisões)");
            foreach (var c in w.Countries.Values.Where(c => !c.Capitulated)
                        .OrderByDescending(c => divs.GetValueOrDefault(c.Id)).Take(15))
            {
                int id = c.Id;
                float share = popTotal > 0 ? 100f * pop.GetValueOrDefault(c.Id) / popTotal : 0f;
                string atWar = c.AtWarWith.Count > 0 ? "  ⚔" : "";
                var b = Ui.Btn($"{c.Name}   {divs.GetValueOrDefault(c.Id)} div · {regions.GetValueOrDefault(c.Id)} reg · {share:0.0}% pop{atWar}",
                    () => { Close(); _countryPanel.Open(id); }, 0);
                b.Alignment = HorizontalAlignment.Left;
                _body.AddChild(b);
            }

            Header("Guerras activas");
            if (w.Wars.Count == 0) Line("Nenhuma — o mundo está em paz");
            foreach (var ((a, bId), info) in w.Wars.OrderBy(kv => kv.Value.StartDay))
            {
                string na = w.Countries.TryGetValue(a, out var ca) ? ca.Name : "#" + a;
                string nb = w.Countries.TryGetValue(bId, out var cb) ? cb.Name : "#" + bId;
                Line($"⚔ {na} vs {nb}   ({w.Clock.Day - info.StartDay} dias)", 17);
            }

            if (_game.PlayerId is int pid)
            {
                var mine = w.ActiveSpyOps.Where(o => o.CountryId == pid).ToList();
                var against = w.ActiveSpyOps.Where(o => o.TargetCountryId == pid).ToList();
                var intel = w.Intel.Where(kv => kv.Key.A == pid && kv.Value >= w.Clock.Day).ToList();
                if (mine.Count > 0 || against.Count > 0 || intel.Count > 0)
                {
                    Header("Espionagem");
                    foreach (var o in mine)
                        Line($"🕵 {(w.SpyOps.TryGetValue(o.OpId, out var op) ? op.Name : o.OpId)} contra {Name(w, o.TargetCountryId)} — {(int)MathF.Ceiling(o.DaysLeft)} dias", 16);
                    foreach (var (k, until) in intel)
                        Line($"👁 Intel sobre {Name(w, k.B)} até ao dia {until}", 16);
                    if (against.Count > 0)
                        Line($"⚠ {against.Count} operação(ões) inimiga(s) contra nós em curso", 16);
                }
            }

            Header("Facções");
            foreach (var f in w.Factions.Values.Where(f => f.Members.Count > 0).OrderByDescending(f => f.Members.Count))
            {
                var tags = f.Members.Where(w.Countries.ContainsKey).Select(m => w.Countries[m].Tag).OrderBy(t => t).ToList();
                string list = tags.Count > 14 ? string.Join(", ", tags.Take(14)) + ", …" : string.Join(", ", tags);
                Line($"{f.Name} ({tags.Count}): {list}", 16);
            }
        }
        catch (Exception ex) { GD.PushError("WorldPanel.Fill: " + ex); }
    }

    private static string Name(WarGame.Core.Model.World w, int id) => w.Countries.TryGetValue(id, out var c) ? c.Name : "#" + id;

    private void Header(string text) { var l = Ui.Lbl(text, 20); l.Modulate = new Color(1f, 0.85f, 0.4f); _body.AddChild(l); }
    private void Line(string text, int size = 18) => _body.AddChild(Ui.Lbl(text, size));
}
