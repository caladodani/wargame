using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Data;

/// <summary>Lê unit_type/unit_stat/unit_tag/template/modifier. Cache em memória — static.db é só leitura.</summary>
public sealed class SqlUnitRepository : IUnitRepository
{
    private readonly IDatabase _db;
    private readonly Dictionary<int, UnitType> _units = new();
    private readonly Dictionary<int, DivisionTemplate> _templates = new();

    public SqlUnitRepository(IDatabase db) => _db = db;

    public UnitType GetUnitType(int id)
    {
        if (_units.TryGetValue(id, out var u)) return u;
        var row = _db.Query("SELECT id,name,category,cost,build_days FROM unit_type WHERE id=?", id).Single();
        var stats = new StatBlock();
        foreach (var r in _db.Query("SELECT stat_key,value FROM unit_stat WHERE unit_type_id=?", id))
            stats[(string)r["stat_key"]!] = Convert.ToSingle(r["value"]);
        foreach (var r in _db.Query("SELECT tag FROM unit_tag WHERE unit_type_id=?", id))
            stats.Tags.Add((string)r["tag"]!);
        u = new UnitType(id, (string)row["name"]!, (string)row["category"]!,
            Convert.ToSingle(row["cost"]), Convert.ToInt32(row["build_days"]), stats);
        _units[id] = u; return u;
    }

    public DivisionTemplate GetTemplate(int id)
    {
        if (_templates.TryGetValue(id, out var t)) return t;
        var row = _db.Query("SELECT country_id,name FROM template WHERE id=?", id).Single();
        var units = _db.Query("SELECT unit_type_id,qty FROM template_unit WHERE template_id=?", id)
            .Select(r => (Convert.ToInt32(r["unit_type_id"]), Convert.ToInt32(r["qty"]))).ToList();
        t = new DivisionTemplate(id, Convert.ToInt32(row["country_id"]), (string)row["name"]!, units);
        _templates[id] = t; return t;
    }

    public IEnumerable<Modifier> GetModifiers() =>
        _db.Query("SELECT id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value FROM modifier")
           .Select(r => new Modifier(
               Convert.ToInt32(r["id"]), (string)r["source_kind"]!,
               r["condition_key"] as string, r["condition_value"] as string,
               (string)r["stat_key"]!, r["required_tag"] as string,
               (string)r["op"]! == "mul" ? ModOp.Mul : ModOp.Add,
               Convert.ToSingle(r["value"])));
}
