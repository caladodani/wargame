namespace WarGame.Core.Data;

/// <summary>Abstracção mínima de SQLite. Impl. Godot usa godot-sqlite; testes usam Microsoft.Data.Sqlite.</summary>
public interface IDatabase : IDisposable
{
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(string sql, params object?[] args);
    int Execute(string sql, params object?[] args);
    void BeginTransaction();
    void Commit();
}
