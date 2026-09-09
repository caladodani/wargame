using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>Selecção de regiões — a única do jogo: um toque simples troca a marca para a região tocada (se
/// tiver divisões minhas); um toque longo (ou botão direito no PC) acrescenta/tira uma marca sem apagar as
/// outras, para mover vários exércitos de uma vez; duplo toque no destino move as divisões todas para lá SEM
/// apagar a selecção — a seta da rota e as marcas ficam, como pede o jogo original. O destino pode ter tropas
/// nossas: juntar divisões numa região onde já está gente é movimento normal (o núcleo nunca o recusou), e é
/// como se empilha no jogo original. Só um toque simples noutra região com divisões minhas é que troca a
/// selecção. Barra no topo mostra o total e dá Parar/Dissolver/Avanço automático/Limpar sem abrir painel nenhum.
/// Lê o World só via RunWhenIdle e muta só por Dispatch.</summary>
public partial class ArmySelect : PanelContainer
{
    private Game _game = null!;
    private MapView _map = null!;
    private Label _label = null!;
    private Button _stop = null!, _disband = null!, _auto = null!, _drop = null!, _rail = null!;
    /// <summary>Ordem de salto armada: o duplo toque seguinte larga os pára-quedistas em vez de os mandar
    /// marchar. Desarma-se sozinha depois da ordem, ou ao limpar a selecção.</summary>
    private bool _dropArmed;
    /// <summary>Ordem de redespacho armada: o duplo toque seguinte manda a tropa de comboio pela retaguarda
    /// em vez de a mandar marchar. Como a do salto, desarma-se depois da ordem ou ao limpar a selecção.</summary>
    private bool _railArmed;
    private readonly HashSet<int> _sel = new();
    private readonly HashSet<int> _prev = new();   // marcação de antes do último toque simples (ver DoubleTap)
    private ulong _prevAt;                          // e quando foi: fora da janela do duplo toque não se desfaz nada

    // Folga sobre a janela do duplo toque: entre os dois toques corre o RunWhenIdle de cada um.
    private const ulong RestoreMs = MapView.DoubleTapMs + 120;

    public bool Active => _sel.Count > 0;
    /// <summary>Regiões marcadas — o painel Exércitos usa a mesma marcação para recrutar divisões para um grupo.</summary>
    public IReadOnlyCollection<int> RegionIds => _sel;
    public int RegionCount => _sel.Count;

    /// <summary>Divisões do país `countryId` que estão nas regiões marcadas.</summary>
    public int DivisionCount(WarGame.Core.Model.World w, int countryId) =>
        _sel.Sum(rid => w.Regions.TryGetValue(rid, out var r)
            ? r.DivisionIds.Count(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == countryId) : 0);

    public void Setup(Game game, MapView map)
    {
        _game = game; _map = map;
        Visible = false;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        OffsetTop = 128;   // altura de partida; o Hud acerta-a pela altura real da barra de topo
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.16f, 0.14f, 0.04f, 0.92f)));
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 8); AddChild(h);
        _label = Ui.Lbl("", 18); h.AddChild(_label);
        _stop = Ui.Btn("Parar", () => _game.RunWhenIdle(StopAll)); h.AddChild(_stop);
        _disband = Ui.Btn("Dissolver", () => _game.RunWhenIdle(DisbandAll), 0f, Ui.Kind.Danger); h.AddChild(_disband);
        _auto = Ui.Btn("⚑ Avanço auto", () => _game.RunWhenIdle(ToggleAutoAdvance)); h.AddChild(_auto);
        _drop = Ui.Btn("🪂 Saltar", () => _game.RunWhenIdle(ToggleDrop)); h.AddChild(_drop);
        _rail = Ui.Btn("🚂 Redespachar", () => _game.RunWhenIdle(ToggleRail)); h.AddChild(_rail);
        h.AddChild(Ui.Btn("Limpar", () => _game.RunWhenIdle(Clear)));
    }

    /// <summary>A barra de topo cresceu (mostradores dobrados numa linha nova): a chapa da selecção
    /// acompanha, senão nasce por trás dela.</summary>
    public void PlaceUnder(float y) => OffsetTop = y;

    /// <summary>Toque simples: região com divisões minhas troca a selecção por só ela; sem divisões minhas
    /// não mexe em nada (é a ficha da região que responde por essa, no rodapé).</summary>
    public void Tap(int regionId) => _game.RunWhenIdle(() =>
    {
        if (!Mine(regionId)) return;
        // guarda-se a marcação que está a ser trocada: se este toque for o primeiro de um duplo em cima de
        // tropas nossas, é ela que o DoubleTap tem de repor para a marcha poder partir.
        _prev.Clear(); _prev.UnionWith(_sel); _prevAt = Time.GetTicksMsec();
        _sel.Clear(); _sel.Add(regionId);
        Refresh();
    });

    /// <summary>Toque longo: marca/desmarca uma região com divisões minhas, sem apagar as outras marcas.</summary>
    public void LongPress(int regionId) => _game.RunWhenIdle(() =>
    {
        if (!Mine(regionId)) { _game.Notify("Sem divisões tuas aí — o toque longo marca regiões de origem"); return; }
        if (!_sel.Add(regionId)) _sel.Remove(regionId);
        Refresh();
    });

    /// <summary>Duplo toque: com regiões marcadas, é o destino — mesmo que lá estejam divisões nossas.
    /// O primeiro toque do par já passou pelo Tap e, num destino com tropas nossas, trocou a marca para ele;
    /// sem desfazer essa troca o segundo toque só via o próprio destino marcado e a ordem nunca partia (era
    /// isto que impedia juntar tropas a tropas). Desfaz-se dentro da janela do duplo toque e só aí.
    /// Sem marcas não faz nada — o duplo toque sem selecção abre a ficha da região, decisão do Hud.</summary>
    public void DoubleTap(int regionId) => _game.RunWhenIdle(() =>
    {
        if (_sel.Count == 1 && _sel.Contains(regionId) && _prev.Count > 0
            && Time.GetTicksMsec() - _prevAt < RestoreMs)
        {
            _sel.Clear(); _sel.UnionWith(_prev);
        }
        if (!Active) return;
        if (_dropArmed) DropTo(regionId); else if (_railArmed) RailTo(regionId); else MoveTo(regionId);
    });

    /// <summary>A ordem arrastada do contador: pousa-se o dedo na pilha, puxa-se até à província e larga-se.
    /// Se a pilha de partida já estava marcada, vai a marcação toda — arrastar uma das caixas escolhidas leva
    /// as outras, como no jogo original; se não estava, a ordem é só dela e passa a ser a marcação. Respeita
    /// o que estiver armado: com o salto armado o arrasto larga pára-quedistas, com o comboio manda-os pelos
    /// carris.</summary>
    public void Order(int fromRegionId, int toRegionId) => _game.RunWhenIdle(() =>
    {
        if (!Mine(fromRegionId)) { _game.Notify("Essa caixa não é de tropa tua — arrasta a partir de uma pilha nossa"); return; }
        if (!_sel.Contains(fromRegionId)) { _sel.Clear(); _sel.Add(fromRegionId); }
        if (_dropArmed) DropTo(toRegionId); else if (_railArmed) RailTo(toRegionId); else MoveTo(toRegionId);
    });

    private bool Mine(int regionId)
    {
        if (_game.PlayerId is not int pid) return false;
        var w = _game.World;
        return w.Regions.TryGetValue(regionId, out var r)
            && r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid);
    }

    /// <summary>Uma ordem de movimento por divisão do jogador em cada região marcada. A selecção fica: a
    /// tropa continua marcada e a rota desenha-se por cima da marcha (regra do jogo original).</summary>
    public void MoveTo(int targetRegionId)
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        string? first = null; int n = 0;
        foreach (var rid in _sel.ToList())
        {
            if (rid == targetRegionId || !w.Regions.TryGetValue(rid, out var r)) continue;
            foreach (var id in r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid).ToList())
            {
                var err = _game.Dispatch(new MoveDivisionCommand(pid, id, targetRegionId));
                if (err is null) n++; else first ??= err;
            }
        }
        string dest = w.Regions.TryGetValue(targetRegionId, out var t) ? t.Name : "R" + targetRegionId;
        // A marcha que corre bem não avisa: a seta desenhada no mapa já diz para onde a tropa vai, e o aviso
        // a cada duplo toque era ruído por cima do mapa (pedido do utilizador). Só se fala quando falha —
        // o erro do comando, ou o duplo toque em cima da própria tropa, que sem resposta parece avaria.
        if (first is not null) _game.Notify(first);
        else if (n == 0) _game.Notify($"{dest} já é onde estão as divisões marcadas — marca primeiro a região de partida");
        Refresh();
    }

    /// <summary>Arma (ou desarma) a ordem de salto. Armada, o duplo toque no destino larga lá os
    /// pára-quedistas marcados em vez de os mandar a pé — é a mesma mão de sempre, com outro sentido.</summary>
    private void ToggleDrop()
    {
        _dropArmed = !_dropArmed;
        if (_dropArmed) _game.Notify("Duplo toque no sítio do salto — alcance de "
            + $"{(int)_game.World.Rule("paradrop_range_hops", 4f)} regiões, sem inimigo no chão nem no céu");
        Refresh();
    }

    /// <summary>Uma ordem de salto por divisão de pára-quedistas marcada. As que não saltam ficam onde
    /// estão: uma marcação com tudo lá dentro não obriga a desmarcar a infantaria à mão.</summary>
    private void DropTo(int targetRegionId)
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        string? first = null; int n = 0, skipped = 0;
        foreach (var rid in _sel.ToList())
        {
            if (rid == targetRegionId || !w.Regions.TryGetValue(rid, out var r)) continue;
            foreach (var id in r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid).ToList())
            {
                if (!w.Divisions.TryGetValue(id, out var div) || !ParadropSystem.IsAirborne(w, div)) { skipped++; continue; }
                var err = _game.Dispatch(new ParadropCommand(pid, id, targetRegionId));
                if (err is null) n++; else first ??= err;
            }
        }
        string dest = w.Regions.TryGetValue(targetRegionId, out var t) ? t.Name : "R" + targetRegionId;
        _game.Notify(n > 0
            ? $"🪂 {n} divis{(n == 1 ? "ão salta" : "ões saltam")} sobre {dest}"
              + (skipped > 0 ? $" ({skipped} sem pára-quedas ficam onde estão)" : "")
            : first ?? (skipped > 0 ? "Nenhuma das divisões marcadas é de pára-quedistas"
                                    : $"{dest} já é onde estão as divisões marcadas"));
        _dropArmed = false;
        Refresh();
    }

    /// <summary>Arma (ou desarma) a ordem de redespacho. Armada, o duplo toque manda as divisões marcadas
    /// pelos carris da retaguarda — depressa, mas a pagar organização e a chegar sem ela.</summary>
    private void ToggleRail()
    {
        _railArmed = !_railArmed;
        if (_railArmed)
        {
            _dropArmed = false;
            var w = _game.World;
            _game.Notify($"Duplo toque no destino — comboio a {1f / MathF.Max(0.01f, w.Rule("redeploy_speed", 0.35f)):0.#}× "
                       + $"a marcha, por {w.Rule("redeploy_org_cost", 40f):0} de organização");
        }
        Refresh();
    }

    /// <summary>Uma ordem de redespacho por divisão marcada. As que não podem embarcar (em combate, sem
    /// carris até lá) ficam onde estão e a primeira razão aparece na notificação.</summary>
    private void RailTo(int targetRegionId)
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        string? first = null; int n = 0;
        foreach (var rid in _sel.ToList())
        {
            if (rid == targetRegionId || !w.Regions.TryGetValue(rid, out var r)) continue;
            foreach (var id in r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid).ToList())
            {
                var err = _game.Dispatch(new RedeployCommand(pid, id, targetRegionId));
                if (err is null) n++; else first ??= err;
            }
        }
        string dest = w.Regions.TryGetValue(targetRegionId, out var t) ? t.Name : "R" + targetRegionId;
        _game.Notify(n > 0 ? $"🚂 {n} divis{(n == 1 ? "ão vai" : "ões vão")} de comboio para {dest}"
                           : first ?? $"{dest} já é onde estão as divisões marcadas");
        _railArmed = false;
        Refresh();
    }

    private void StopAll()
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        string? first = null;
        foreach (var rid in _sel.ToList())
        {
            if (!w.Regions.TryGetValue(rid, out var r)) continue;
            foreach (var id in r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid).ToList())
                first ??= _game.Dispatch(new StopDivisionCommand(pid, id));
        }
        if (first is not null) _game.Notify(first);
        Refresh();
    }

    private void DisbandAll()
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        string? first = null; int n = 0;
        foreach (var rid in _sel.ToList())
        {
            if (!w.Regions.TryGetValue(rid, out var r)) continue;
            foreach (var id in r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid).ToList())
            {
                var err = _game.Dispatch(new DisbandDivisionCommand(pid, id));
                if (err is null) n++; else first ??= err;
            }
        }
        _game.Notify(first ?? $"{n} divisões dissolvidas");
        Refresh();
    }

    /// <summary>Liga/desliga o avanço automático nas divisões marcadas: se alguma ainda não o tem, liga em
    /// todas; se já o têm todas, desliga.</summary>
    private void ToggleAutoAdvance()
    {
        if (_game.PlayerId is not int pid) return;
        var w = _game.World;
        var ids = _sel.SelectMany(rid => w.Regions.TryGetValue(rid, out var r)
            ? r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid) : Enumerable.Empty<int>()).ToList();
        bool on = ids.Any(id => w.Divisions.TryGetValue(id, out var d) && !d.AutoAdvance);
        string? first = null;
        foreach (var id in ids) first ??= _game.Dispatch(new SetAutoAdvanceCommand(pid, id, on));
        if (first is not null) _game.Notify(first);
        Refresh();
    }

    public void Clear() { _sel.Clear(); _prev.Clear(); _dropArmed = false; _railArmed = false; Refresh(); }

    private void Refresh()
    {
        _map.Regions.HighlightMulti(_sel);
        if (!Active) { Visible = false; return; }
        var w = _game.World; int pid = _game.PlayerId ?? -1;
        int divs = _sel.Sum(rid => w.Regions.TryGetValue(rid, out var r)
            ? r.DivisionIds.Count(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid) : 0);
        var ids = _sel.SelectMany(rid => w.Regions.TryGetValue(rid, out var r)
            ? r.DivisionIds.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == pid) : Enumerable.Empty<int>()).ToList();
        int paras = ids.Count(id => w.Divisions.TryGetValue(id, out var d) && ParadropSystem.IsAirborne(w, d));
        if (paras == 0) _dropArmed = false;   // ficou só infantaria marcada: a ordem de salto cai
        _label.Text = _dropArmed
            ? $"🪂 {paras} divis{(paras == 1 ? "ão de pára-quedistas" : "ões de pára-quedistas")} — duplo toque no sítio do salto"
            : _railArmed
            ? $"🚂 {divs} divis{(divs == 1 ? "ão" : "ões")} para o comboio — duplo toque no destino, pela retaguarda"
            : $"⚔ {_sel.Count} regiões · {divs} divisões — duplo toque no destino move";
        _auto.Text = ids.Count == 0 || ids.Any(id => w.Divisions.TryGetValue(id, out var d) && !d.AutoAdvance)
            ? "⚑ Avanço auto" : "⚑ Parar avanço";
        // o botão do salto só aparece a quem tem quem salte: numa selecção de infantaria não serve de nada
        _drop.Visible = paras > 0;
        _drop.Text = _dropArmed ? "🪂 Desistir do salto" : "🪂 Saltar";
        // o comboio serve para qualquer tropa, mas não a quem já vai nele: nesse caso o botão desce dela
        bool riding = ids.Any(id => w.Divisions.TryGetValue(id, out var d) && d.Redeploying);
        _rail.Visible = divs > 0 && !_dropArmed;
        _rail.Text = riding ? "🚂 A caminho" : _railArmed ? "🚂 Desistir do comboio" : "🚂 Redespachar";
        _rail.Disabled = riding;
        Visible = true;
    }
}
