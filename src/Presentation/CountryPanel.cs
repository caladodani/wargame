using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Painel de país (45% inferior): ficha (country_info), características (country_stat), espíritos nacionais
/// e investigação (tecnologia em curso, disponíveis com "Investigar", concluídas). Para o país do jogador tem
/// botões; para os outros é só leitura. Lê o World só em Fill (mundo parado); muta só por Game.Dispatch.</summary>
public partial class CountryPanel : PanelContainer
{
    private Game _game = null!;
    private Label _title = null!;
    private VBoxContainer _body = null!;
    private int _countryId;
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
        _body = Ui.Grow(new VBoxContainer()); scroll.AddChild(_body);
    }

    public void Open(int countryId) { _countryId = countryId; _lastKey = ""; _game.RunWhenIdle(() => { Fill(); Visible = true; }); }
    public void Refresh() { if (Visible) Fill(); }
    public void Close() => Visible = false;

    private void Fill()
    {
        try
        {
            var w = _game.World;
            if (!w.Countries.TryGetValue(_countryId, out var c)) { Close(); return; }
            bool mine = _game.PlayerId == c.Id;
            var key = $"{c.Id}|{mine}|{c.ResearchTech}|{(int)c.ResearchProgress}|{c.Techs.Count}";
            if (key == _lastKey) return;
            _lastKey = key;
            _title.Text = $"{c.Name} ({c.Tag})" + (mine ? "  — o teu país" : "");
            Ui.Clear(_body);

            // ficha
            CountryInfo? info = null; IReadOnlyList<NationalSpirit> spirits = Array.Empty<NationalSpirit>();
            try { info = _game.WorldRepo.GetCountryInfo(c.Tag); spirits = _game.WorldRepo.GetSpirits(c.Tag); }
            catch (Exception ex) { GD.PushError("country_info: " + ex.Message); }
            if (info is not null)
            {
                if (info.Government.Length > 0) Line($"Governo: {info.Government}" + (info.Leader.Length > 0 ? $"   ·   {info.Leader}" : ""));
                if (info.Alliance.Length > 0) Line($"Aliança: {info.Alliance}");
                if (info.Doctrine.Length > 0) Line($"Doutrina: {info.Doctrine}");
                if (info.Description.Length > 0) Wrap(info.Description, 17);
            }
            Line($"Indústria ×{c.Stat("industry"):0.00}   Produção ×{c.Stat("production_speed"):0.00}   Organização ×{c.Stat("org_regain"):0.00}   Investigação ×{c.Stat("research_speed"):0.00}");
            Line($"Divisões {w.Divisions.Values.Count(d => d.CountryId == c.Id)}   ·   Regiões {w.Regions.Values.Count(r => r.ControllerId == c.Id)}   ·   Rendimento {EconomySystem.Income(w, c.Id):0.0}/dia");

            // espíritos
            Header("Espíritos nacionais");
            if (spirits.Count == 0) Line("Nenhum (país genérico)");
            foreach (var s in spirits) { Line("• " + s.Name, 19); if (s.Description.Length > 0) Wrap("   " + s.Description, 16); }

            // facções (alianças defensivas: declarar guerra a um membro chama os outros contra o agressor)
            Header("Facções");
            var factions = w.FactionsOf(c.Id).ToList();
            if (factions.Count == 0) Line("Nenhuma");
            foreach (var f in factions)
            {
                var names = f.Members.Where(w.Countries.ContainsKey).Select(m => w.Countries[m].Tag).OrderBy(t => t).ToList();
                string list = names.Count > 12 ? string.Join(", ", names.Take(12)) + ", …" : string.Join(", ", names);
                Line($"{f.Name} — {names.Count} membros: {list}", 16);
            }
            if (c.AtWarWith.Count > 0)
            {
                var enemies = c.AtWarWith.Where(w.Countries.ContainsKey).Select(id => w.Countries[id].Tag).OrderBy(t => t);
                Line("Em guerra com: " + string.Join(", ", enemies));
            }

            // investigação
            Header("Investigação");
            if (c.ResearchTech is not null && w.Techs.TryGetValue(c.ResearchTech, out var cur))
                Line($"Em curso: {cur.Name}   {(int)(100 * c.ResearchProgress / MathF.Max(1f, cur.Cost))}%  ({(int)MathF.Ceiling((cur.Cost - c.ResearchProgress) / MathF.Max(0.01f, c.Stat("research_speed")))} dias)");
            else Line(mine ? "Nada em investigação — escolhe uma tecnologia:" : "Nada em investigação");
            foreach (var group in w.Techs.Values.Where(t => w.CanResearch(c, t.Id)).GroupBy(t => t.Branch).OrderBy(g => g.Key))
                foreach (var t in group.OrderBy(t => t.Cost))
                {
                    string id = t.Id;
                    var row = new HBoxContainer();
                    row.AddChild(Ui.Grow(Ui.Lbl($"{t.Branch}: {t.Name}   {t.Cost:0} dias" + (t.Description is { Length: > 0 } ? "\n   " + t.Description : ""), 16)));
                    if (mine && c.ResearchTech != id) row.AddChild(Ui.Btn("Investigar", () => Research(id), 150));
                    _body.AddChild(row);
                }
            var known = c.Techs.Where(w.Techs.ContainsKey).Select(id => w.Techs[id].Name).OrderBy(n => n).ToList();
            Line($"Concluídas ({known.Count}): " + (known.Count == 0 ? "nenhuma" : string.Join(", ", known)), 16);
        }
        catch (Exception ex) { GD.PushError("CountryPanel.Fill: " + ex); }
    }

    private void Research(string techId)
    {
        if (_game.PlayerId is not int pid) return;
        var err = _game.Dispatch(new ResearchTechCommand(pid, techId));
        if (err is not null) GetParent<Hud>().Toast(err);
    }

    private void Header(string text) { var l = Ui.Lbl(text, 20); l.Modulate = new Color(1f, 0.85f, 0.4f); _body.AddChild(l); }
    private void Line(string text, int size = 18) => _body.AddChild(Ui.Lbl(text, size));
    private void Wrap(string text, int size)
    {
        var l = Ui.Lbl(text, size); l.AutowrapMode = TextServer.AutowrapMode.Word; l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _body.AddChild(l);
    }
}
