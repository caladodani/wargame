using Microsoft.Data.Sqlite;
using WarGame.Core.Data;

namespace WarGame.Core.Tests;

/// <summary>IDatabase para testes no PC (sem Godot).</summary>
public sealed class MsSqliteDatabase : IDatabase
{
    private readonly SqliteConnection _c;
    private SqliteTransaction? _tx;

    public MsSqliteDatabase(string connStr = "Data Source=:memory:") { _c = new SqliteConnection(connStr); _c.Open(); }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(string sql, params object?[] args)
    {
        using var cmd = Cmd(sql, args);
        using var r = cmd.ExecuteReader();
        var list = new List<IReadOnlyDictionary<string, object?>>();
        while (r.Read())
        {
            var d = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) d[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            list.Add(d);
        }
        return list;
    }

    public int Execute(string sql, params object?[] args) { using var cmd = Cmd(sql, args); return cmd.ExecuteNonQuery(); }
    public void ExecuteScript(string sql) { using var cmd = _c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
    public void BeginTransaction() => _tx = _c.BeginTransaction();
    public void Commit() { _tx?.Commit(); _tx = null; }
    public void Dispose() => _c.Dispose();

    private SqliteCommand Cmd(string sql, object?[] args)
    {
        var cmd = _c.CreateCommand(); cmd.Transaction = _tx;
        // '?' posicional → @p0..@pn
        int i = 0; cmd.CommandText = string.Concat(sql.Split('?').Select((part, idx) => idx == 0 ? part : $"@p{idx - 1}{part}"));
        foreach (var a in args) cmd.Parameters.AddWithValue($"@p{i++}", a ?? DBNull.Value);
        return cmd;
    }
}
