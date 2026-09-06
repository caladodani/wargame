using Godot;
using WarGame.Core.Data;
using GC = Godot.Collections;

namespace WarGame.Data;

/// <summary>IDatabase sobre o addon godot-sqlite (GDExtension), acedido via ClassDB — funciona em Android.</summary>
public sealed class GdSqliteDatabase : IDatabase
{
    private readonly GodotObject _db;

    public GdSqliteDatabase(string path, bool readOnly = false)
    {
        _db = ClassDB.Instantiate("SQLite").AsGodotObject();
        _db.Set("path", path);
        _db.Set("read_only", readOnly);
        _db.Set("foreign_keys", true);
        if (!(bool)_db.Call("open_db")) throw new InvalidOperationException($"Não abriu {path}");
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(string sql, params object?[] args)
    {
        var ok = (bool)_db.Call("query_with_bindings", sql, ToVariantArray(args));
        if (!ok) throw new InvalidOperationException((string)_db.Get("error_message"));
        var rows = (GC.Array)_db.Get("query_result");
        var list = new List<IReadOnlyDictionary<string, object?>>(rows.Count);
        foreach (var row in rows)
        {
            var dict = (GC.Dictionary)row;
            var d = new Dictionary<string, object?>();
            foreach (var k in dict.Keys) d[(string)k] = FromVariant(dict[k]);
            list.Add(d);
        }
        return list;
    }

    public int Execute(string sql, params object?[] args)
    {
        if (!(bool)_db.Call("query_with_bindings", sql, ToVariantArray(args)))
            throw new InvalidOperationException((string)_db.Get("error_message"));
        return (int)_db.Get("last_insert_rowid");
    }

    public void BeginTransaction() => _db.Call("query", "BEGIN");
    public void Commit() => _db.Call("query", "COMMIT");
    public void Dispose() => _db.Call("close_db");

    private static GC.Array ToVariantArray(object?[] args)
    {
        var a = new GC.Array();
        foreach (var o in args) a.Add(o switch
        {
            null => default, int i => i, long l => l, float f => f, double d => d,
            string s => s, bool b => b, _ => o.ToString() ?? ""
        });
        return a;
    }

    private static object? FromVariant(Variant v) => v.VariantType switch
    {
        Variant.Type.Nil => null,
        Variant.Type.Int => v.AsInt64(),
        Variant.Type.Float => v.AsDouble(),
        Variant.Type.String => v.AsString(),
        Variant.Type.Bool => v.AsBool(),
        _ => v.Obj
    };
}
