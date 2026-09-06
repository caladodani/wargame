using System.Text;
using WarGame.Core.Model;

namespace WarGame.Core.Data;

/// <summary>Carrega regiões/países/regras de static.db e lê/escreve save.db (só estado mutável).</summary>
public sealed class SqlWorldRepository : IWorldRepository
{
    private readonly IDatabase _static;
    public SqlWorldRepository(IDatabase staticDb) => _static = staticDb;

    public void LoadStatic(World w)
    {
        foreach (var r in _static.Query("SELECT key,value FROM rule"))
            w.Rules[(string)r["key"]!] = Convert.ToSingle(r["value"]);
        foreach (var r in _static.Query("SELECT id,move_cost FROM terrain"))
            w.Rules["move_cost:" + (string)r["id"]!] = Convert.ToSingle(r["move_cost"]);

        var byTag = new Dictionary<string, Country>();
        foreach (var r in _static.Query("SELECT id,tag,name,capital_region_id FROM country"))
        {
            var c = new Country
            {
                Id = Convert.ToInt32(r["id"]), Tag = (string)r["tag"]!, Name = (string)r["name"]!,
                CapitalRegionId = r["capital_region_id"] is null ? 0 : Convert.ToInt32(r["capital_region_id"]),
            };
            w.Countries[c.Id] = c; byTag[c.Tag] = c;
        }
        foreach (var r in _static.Query("SELECT country_tag,key,value FROM country_stat"))
            if (byTag.TryGetValue((string)r["country_tag"]!, out var c)) c.Stats[(string)r["key"]!] = Convert.ToSingle(r["value"]);

        foreach (var r in _static.Query("SELECT id,name,owner_id,terrain,river,population,infrastructure,centroid_x,centroid_y FROM region"))
        {
            int id = Convert.ToInt32(r["id"]), owner = Convert.ToInt32(r["owner_id"]);
            w.Regions[id] = new Region
            {
                Id = id, Name = (string)r["name"]!, OwnerId = owner, ControllerId = owner,
                Terrain = (string)r["terrain"]!, River = Convert.ToInt32(r["river"]) == 1,
                Population = Convert.ToInt32(r["population"]), Infrastructure = Convert.ToSingle(r["infrastructure"]),
                CenterX = r["centroid_x"] is null ? 0f : Convert.ToSingle(r["centroid_x"]),
                CenterY = r["centroid_y"] is null ? 0f : Convert.ToSingle(r["centroid_y"]),
            };
        }
        foreach (var r in _static.Query("SELECT region_id,neighbour_id FROM region_neighbour"))
            w.Regions[Convert.ToInt32(r["region_id"])].Neighbours.Add(Convert.ToInt32(r["neighbour_id"]));
    }

    public void LoadStartArmies(World w)
    {
        foreach (var r in _static.Query("SELECT id,country_id,template_id,region_id,name FROM start_division ORDER BY id"))
            w.AddDivision(new Division
            {
                Id = Convert.ToInt32(r["id"]), CountryId = Convert.ToInt32(r["country_id"]),
                TemplateId = Convert.ToInt32(r["template_id"]), RegionId = Convert.ToInt32(r["region_id"]),
                Name = r["name"] as string,
            });
    }

    public IReadOnlyList<NationalSpirit> GetSpirits(string countryTag) =>
        _static.Query("SELECT id,country_tag,name,description FROM national_spirit WHERE country_tag=? ORDER BY rowid", countryTag)
               .Select(r => new NationalSpirit((string)r["id"]!, (string)r["country_tag"]!, (string)r["name"]!, r["description"] as string ?? "")).ToList();

    public CountryInfo? GetCountryInfo(string countryTag) =>
        _static.Query("SELECT country_tag,government,leader,doctrine,alliance,description FROM country_info WHERE country_tag=?", countryTag)
               .Select(r => new CountryInfo((string)r["country_tag"]!, r["government"] as string ?? "", r["leader"] as string ?? "",
                                            r["doctrine"] as string ?? "", r["alliance"] as string ?? "", r["description"] as string ?? ""))
               .FirstOrDefault();

    /// <summary>Anéis já projectados (float32 x,y). Só a apresentação precisa disto.</summary>
    public IEnumerable<(int RegionId, float[] Points)> ReadPolygons()
    {
        foreach (var r in _static.Query("SELECT region_id,points FROM region_polygon ORDER BY region_id,ring_index"))
        {
            var bytes = (byte[])r["points"]!;
            var pts = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, pts, 0, bytes.Length);
            yield return (Convert.ToInt32(r["region_id"]), pts);
        }
    }

    /// <summary>Cria as tabelas do save num ficheiro novo: executa schema.sql instrução a instrução
    /// (o driver Godot só aceita uma por chamada). Comentários `--` são retirados antes.</summary>
    public static void EnsureSaveSchema(IDatabase save, string schemaSql)
    {
        var sb = new StringBuilder();
        foreach (var raw in schemaSql.Split('\n'))
        {
            var line = raw; int c = line.IndexOf("--", StringComparison.Ordinal);
            if (c >= 0) line = line[..c];
            sb.Append(line).Append('\n');
        }
        foreach (var stmt in sb.ToString().Split(';'))
            if (!string.IsNullOrWhiteSpace(stmt)) save.Execute(stmt.Trim());
    }

    public static bool HasSave(IDatabase save) =>
        save.Query("SELECT name FROM sqlite_master WHERE type='table' AND name='save_meta'").Count > 0
        && save.Query("SELECT value FROM save_meta WHERE key='day'").Count > 0;

    public void LoadSave(World w, IDatabase save)
    {
        foreach (var r in save.Query("SELECT key,value FROM save_meta"))
            if ((string)r["key"]! == "day") for (int i = 0; i < Convert.ToInt32(r["value"]); i++) w.Clock.Advance();
        foreach (var r in save.Query("SELECT id,is_player,money FROM s_country"))
        {
            var c = w.Countries[Convert.ToInt32(r["id"])];
            c.IsPlayer = Convert.ToInt32(r["is_player"]) == 1; c.Money = Convert.ToSingle(r["money"]);
        }
        foreach (var r in save.Query("SELECT country_id,tech_id FROM s_country_tech"))
            w.Countries[Convert.ToInt32(r["country_id"])].Techs.Add((string)r["tech_id"]!);
        foreach (var r in save.Query("SELECT id,controller_id,infrastructure FROM s_region"))
        { var reg = w.Regions[Convert.ToInt32(r["id"])]; reg.ControllerId = Convert.ToInt32(r["controller_id"]); reg.Infrastructure = Convert.ToSingle(r["infrastructure"]); }
        foreach (var r in save.Query("SELECT id,country_id,template_id,region_id,hp,org,supply,move_progress,path,name FROM s_division ORDER BY id"))
        {
            var d = new Division
            {
                Id = Convert.ToInt32(r["id"]), CountryId = Convert.ToInt32(r["country_id"]), TemplateId = Convert.ToInt32(r["template_id"]),
                RegionId = Convert.ToInt32(r["region_id"]), Name = r["name"] as string,
                Hp = Convert.ToSingle(r["hp"]), Org = Convert.ToSingle(r["org"]), Supply = Convert.ToSingle(r["supply"]),
            };
            if (r["path"] is string p && p.Length > 0) d.SetPath(p.Split(',').Select(int.Parse));
            d.MoveProgress = Convert.ToSingle(r["move_progress"]);
            w.AddDivision(d);
        }
        foreach (var r in save.Query("SELECT a,b FROM s_war"))
        { int a = Convert.ToInt32(r["a"]), b = Convert.ToInt32(r["b"]); w.Countries[a].AtWarWith.Add(b); w.Countries[b].AtWarWith.Add(a); }
        foreach (var r in save.Query("SELECT country_id,template_id,progress FROM s_production_queue ORDER BY id"))
            w.Countries[Convert.ToInt32(r["country_id"])].Queue.Add(new ProductionOrder { TemplateId = Convert.ToInt32(r["template_id"]), Progress = Convert.ToSingle(r["progress"]) });
        foreach (var r in save.Query("SELECT region_id,attacker_country_id,days FROM s_battle"))
        {
            var b = new Battle { RegionId = Convert.ToInt32(r["region_id"]), AttackerCountryId = Convert.ToInt32(r["attacker_country_id"]), Days = Convert.ToInt32(r["days"]) };
            foreach (var d in save.Query("SELECT division_id,side FROM s_battle_division WHERE region_id=?", b.RegionId))
                ((string)d["side"]! == "att" ? b.Attackers : b.Defenders).Add(Convert.ToInt32(d["division_id"]));
            if (b.Attackers.Count > 0 && b.Defenders.Count > 0) w.ActiveBattles.Add(b);
        }
    }

    public void WriteSave(World w, IDatabase save)
    {
        save.BeginTransaction();
        foreach (var t in new[] { "s_region", "s_division", "s_war", "save_meta", "s_country", "s_country_tech", "s_production_queue", "s_battle", "s_battle_division" })
            save.Execute("DELETE FROM " + t);
        save.Execute("INSERT INTO save_meta VALUES ('day',?)", w.Clock.Day);
        save.Execute("INSERT INTO save_meta VALUES ('saved_at',?)", DateTime.UtcNow.ToString("o"));
        foreach (var c in w.Countries.Values)
        {
            if (c.IsPlayer || c.Money != 0f) save.Execute("INSERT INTO s_country (id,is_player,money) VALUES (?,?,?)", c.Id, c.IsPlayer ? 1 : 0, c.Money);
            foreach (var t in c.Techs) save.Execute("INSERT INTO s_country_tech VALUES (?,?)", c.Id, t);
            foreach (var o in c.Queue) save.Execute("INSERT INTO s_production_queue (country_id,template_id,progress) VALUES (?,?,?)", c.Id, o.TemplateId, o.Progress);
            foreach (var e in c.AtWarWith) if (c.Id < e) save.Execute("INSERT INTO s_war VALUES (?,?,?)", c.Id, e, w.Clock.Day);
        }
        foreach (var r in w.Regions.Values)
            if (r.ControllerId != r.OwnerId || r.Infrastructure != 1f)
                save.Execute("INSERT INTO s_region VALUES (?,?,?)", r.Id, r.ControllerId, r.Infrastructure);
        foreach (var d in w.Divisions.Values)
            save.Execute("INSERT INTO s_division VALUES (?,?,?,?,?,?,?,?,?,?,?)", d.Id, d.CountryId, d.TemplateId, d.RegionId, d.TargetRegionId,
                d.Hp, d.Org, d.Supply, d.MoveProgress, d.Path.Count == 0 ? null : string.Join(',', d.Path), d.Name);
        foreach (var b in w.ActiveBattles)
        {
            // uma batalha por região no schema: se dois países atacam a mesma região só a primeira persiste
            if (save.Query("SELECT 1 FROM s_battle WHERE region_id=?", b.RegionId).Count > 0) continue;
            save.Execute("INSERT INTO s_battle VALUES (?,?,?)", b.RegionId, b.AttackerCountryId, b.Days);
            foreach (var a in b.Attackers) save.Execute("INSERT INTO s_battle_division VALUES (?,?,'att')", b.RegionId, a);
            foreach (var d in b.Defenders) save.Execute("INSERT INTO s_battle_division VALUES (?,?,'def')", b.RegionId, d);
        }
        save.Commit();
    }
}
