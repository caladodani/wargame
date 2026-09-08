using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Guarda o saldo de cada guerra: regiões tomadas, divisões perdidas e batalhas ganhas por lado.
/// Não decide nada — só ouve os eventos das outras mecânicas e escreve nos contadores da WarInfo.
/// Quando uma guerra acaba, tira uma fotografia final para World.WarHistory (e publica WarSummary),
/// porque a WarInfo desaparece com a paz e o jogador ainda quer saber quanto lhe custou.</summary>
public sealed class WarStatsSystem : ISystem
{
    public string Name => "WarStats";

    private World? _bound;
    /// <summary>Última fotografia de cada guerra viva. Quem fecha uma guerra apaga-a de World.Wars
    /// antes de publicar WarEnded, por isso os contadores já não estariam lá para arquivar.</summary>
    private readonly Dictionary<(int A, int B), WarRecord> _snap = new();

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) Bind(w);
        foreach (var (key, war) in w.Wars) _snap[key] = Snapshot(w, war);
        // Guerras que já não existem não interessam para nada além do arquivo, que já foi feito.
        foreach (var key in _snap.Keys.Where(k => !w.Wars.ContainsKey(k)).ToList()) _snap.Remove(key);
    }

    /// <summary>A fotografia dos contadores é a mesma que a UI mostra a meio da guerra (WarLedger): o
    /// saldo do arquivo e o saldo do ecrã nunca podem ser dois números diferentes.</summary>
    private static WarRecord Snapshot(World w, WarInfo war) => WarLedger.Snapshot(w, war);

    /// <summary>Liga-se ao barramento de eventos do mundo. Idempotente por mundo: um save carregado
    /// traz um World novo e volta a subscrever; o mesmo mundo nunca subscreve duas vezes.</summary>
    public void Bind(World w)
    {
        _bound = w;
        _snap.Clear();
        // Uma guerra declarada e fechada no mesmo dia ainda assim entra no arquivo.
        w.Events.Subscribe<WarDeclared>(e => Remember(w, World.WarKey(e.Aggressor, e.Target)));
        w.Events.Subscribe<FactionJoinedWar>(e => Remember(w, World.WarKey(e.MemberCountryId, e.AgainstCountryId)));
        w.Events.Subscribe<RegionCaptured>(e => OnCapture(w, e));
        w.Events.Subscribe<DivisionDestroyed>(e => OnDivisionLost(w, e));
        w.Events.Subscribe<BattleEnded>(e => OnBattle(w, e));
        w.Events.Subscribe<WarEnded>(e => Archive(w, World.WarKey(e.A, e.B)));
    }

    private void Remember(World w, (int A, int B) key)
    { if (w.Wars.TryGetValue(key, out var war)) _snap[key] = Snapshot(w, war); }

    private void OnCapture(World w, RegionCaptured e)
    {
        var key = World.WarKey(e.OldController, e.NewController);
        if (!w.Wars.TryGetValue(key, out var war)) return;
        war.Side(e.NewController).RegionsTaken++;
        _snap[key] = Snapshot(w, war);
    }

    private void OnDivisionLost(World w, DivisionDestroyed e)
    {
        // O evento é publicado antes de a divisão sair do mundo, por isso o dono ainda se sabe.
        if (!w.Divisions.TryGetValue(e.DivisionId, out var d)) return;
        // A perda conta na guerra onde ela caiu: quem manda na região onde estava, se for inimigo;
        // sem isso (retirada, cerco), na única guerra em curso do país.
        int? enemy = w.Regions.TryGetValue(d.RegionId, out var r) && w.AreAtWar(d.CountryId, r.ControllerId)
            ? r.ControllerId
            : w.Countries.TryGetValue(d.CountryId, out var c) && c.AtWarWith.Count == 1 ? c.AtWarWith.First() : null;
        if (enemy is not int foe) return;
        var key = World.WarKey(d.CountryId, foe);
        if (!w.Wars.TryGetValue(key, out var war)) return;
        war.Side(d.CountryId).DivisionsLost++;
        _snap[key] = Snapshot(w, war);
    }

    private void OnBattle(World w, BattleEnded e)
    {
        var key = World.WarKey(e.AttackerCountryId, e.DefenderCountryId);
        if (!w.Wars.TryGetValue(key, out var war)) return;
        war.Side(e.AttackerWon ? e.AttackerCountryId : e.DefenderCountryId).BattlesWon++;
        _snap[key] = Snapshot(w, war);
    }

    /// <summary>Arquiva a guerra pela última fotografia conhecida (a entrada em World.Wars já foi removida
    /// por quem assinou a paz) e anuncia o saldo.</summary>
    private void Archive(World w, (int A, int B) key)
    {
        if (!_snap.Remove(key, out var snap)) return;
        var rec = snap with { EndDay = w.Clock.Day };
        w.WarHistory.Insert(0, rec);
        int max = Math.Max(1, (int)w.Rule("war_history_max", 40f));
        if (w.WarHistory.Count > max) w.WarHistory.RemoveRange(max, w.WarHistory.Count - max);
        w.Events.Publish(new WarSummary(rec));
    }
}
