namespace WarGame.Core.Events;

public interface IGameEvent { }

public sealed record DayPassed(int Day) : IGameEvent;
public sealed record WarDeclared(int Aggressor, int Target) : IGameEvent;
public sealed record RegionCaptured(int RegionId, int OldController, int NewController) : IGameEvent;
public sealed record BattleStarted(int RegionId) : IGameEvent;
public sealed record BattleEnded(int RegionId, bool AttackerWon) : IGameEvent;
public sealed record DivisionDestroyed(int DivisionId) : IGameEvent;
public sealed record TechResearched(int CountryId, string TechId) : IGameEvent;

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
