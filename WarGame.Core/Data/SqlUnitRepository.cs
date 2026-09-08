using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Data;

/// <summary>Lê unit_type/unit_stat/unit_tag/template/modifier. Cache em memória — static.db é só leitura.</summary>
public sealed class SqlUnitRepository : IUnitRepository
{
    private readonly IDatabase _db;
    private readonly Dictionary<int, UnitType> _units = new();
    private readonly Dictionary<int, DivisionTemplate> _templates = new();
    private readonly Dictionary<int, List<DivisionTemplate>> _byCountry = new();
    private IReadOnlyList<UnitType>? _allUnits;

    public SqlUnitRepository(IDatabase db) => _db = db;

    public UnitType GetUnitType(int id)
    {
        if (_units.TryGetValue(id, out var u)) return u;
        var row = _db.Query("SELECT id,name,category,cost,build_days,supply,mobility FROM unit_type WHERE id=?", id).Single();
        var stats = new StatBlock();
        foreach (var r in _db.Query("SELECT stat_key,value FROM unit_stat WHERE unit_type_id=?", id))
            stats[(string)r["stat_key"]!] = Convert.ToSingle(r["value"]);
        foreach (var r in _db.Query("SELECT tag FROM unit_tag WHERE unit_type_id=?", id))
            stats.Tags.Add((string)r["tag"]!);
        u = new UnitType(id, (string)row["name"]!, (string)row["category"]!,
            Convert.ToSingle(row["cost"]), Convert.ToInt32(row["build_days"]),
            Convert.ToSingle(row["mobility"]), Convert.ToSingle(row["supply"]), stats);
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

    public IReadOnlyList<DivisionTemplate> GetTemplates(int countryId)
    {
        if (_byCountry.TryGetValue(countryId, out var list)) return list;
        list = _db.Query("SELECT id FROM template WHERE country_id=? ORDER BY id", countryId)
                  .Select(r => GetTemplate(Convert.ToInt32(r["id"]))).ToList();
        _byCountry[countryId] = list; return list;
    }

    public IReadOnlyList<UnitType> AllUnitTypes() =>
        _allUnits ??= _db.Query("SELECT id FROM unit_type ORDER BY id")
                         .Select(r => GetUnitType(Convert.ToInt32(r["id"]))).ToList();

    public DivisionTemplate AddCustomTemplate(int id, int countryId, string name, IReadOnlyList<(int UnitTypeId, int Qty)> units)
    {
        var t = new DivisionTemplate(id, countryId, name, units.ToList());
        _templates[id] = t;
        GetTemplates(countryId);           // garante a lista carregada antes de acrescentar
        // redesenhar é voltar a passar aqui com o mesmo id: a lista do país tem de ficar com o desenho
        // novo, senão o painel continuava a mostrar os batalhões antigos de um modelo já mudado
        var mine = _byCountry[countryId];
        int at = mine.FindIndex(x => x.Id == id);
        if (at >= 0) mine[at] = t; else mine.Add(t);
        return t;
    }

    public IEnumerable<Modifier> GetModifiers() =>
        _db.Query("SELECT id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id FROM modifier")
           .Select(r => new Modifier(
               Convert.ToInt32(r["id"]), (string)r["source_kind"]!,
               r["condition_key"] as string, r["condition_value"] as string,
               (string)r["stat_key"]!, r["required_tag"] as string,
               (string)r["op"]! == "mul" ? ModOp.Mul : ModOp.Add,
               Convert.ToSingle(r["value"]), r["country_tag"] as string, r["spirit_id"] as string));
}
