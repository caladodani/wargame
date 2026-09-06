namespace WarGame.Core.Events;

public interface IGameEvent { }

public sealed record DayPassed(int Day) : IGameEvent;
public sealed record WarDeclared(int Aggressor, int Target) : IGameEvent;
/// <summary>Um membro de uma facção do alvo é chamado à guerra contra o agressor (DeclareWarCommand: só a facção do defensor chama).</summary>
public sealed record FactionJoinedWar(string FactionId, int MemberCountryId, int AgainstCountryId) : IGameEvent;
public sealed record RegionCaptured(int RegionId, int OldController, int NewController) : IGameEvent;
public sealed record BattleStarted(int RegionId) : IGameEvent;
public sealed record BattleEnded(int RegionId, bool AttackerWon) : IGameEvent;
public sealed record DivisionDestroyed(int DivisionId) : IGameEvent;
public sealed record TechResearched(int CountryId, string TechId) : IGameEvent;
/// <summary>Um país capitulou (PeaceSystem); Winner ficou com as regiões que o capitulado ainda controlava.</summary>
public sealed record CountryCapitulated(int CountryId, int WinnerId) : IGameEvent;
public sealed record WarEnded(int A, int B) : IGameEvent;
/// <summary>Paz branca por estagnação (TruceSystem); sai sempre antes do WarEnded da mesma guerra.</summary>
public sealed record WhitePeaceSigned(int A, int B) : IGameEvent;
public sealed record FocusCompleted(int CountryId, string FocusId) : IGameEvent;
public sealed record WarJustifyStarted(int CountryId, int TargetCountryId) : IGameEvent;
public sealed record NewsFired(string EventId) : IGameEvent;

/// <summary>Pub/sub tipado. UI e sistemas subscrevem; ninguém chama ninguém directamente.</summary>
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subs = new();

    public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        if (!_subs.TryGetValue(typeof(T), out var list)) _subs[typeof(T)] = list = new();
        list.Add(handler);
        return new Unsub(() => list.Remove(handler));
    }

    public void Publish<T>(T evt) where T : IGameEvent
    {
        if (!_subs.TryGetValue(typeof(T), out var list)) return;
        foreach (var d in list.ToArray()) ((Action<T>)d)(evt);
    }

    private sealed class Unsub : IDisposable
    {
        private readonly Action _a; public Unsub(Action a) => _a = a; public void Dispose() => _a();
    }
}
