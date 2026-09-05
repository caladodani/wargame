using WarGame.Core.Model;

namespace WarGame.Core.Data;

/// <summary>Carrega regiões/países de static.db e lê/escreve save_N.db.</summary>
public sealed class SqlWorldRepository : IWorldRepository
{
    private readonly IDatabase _static;
    public SqlWorldRepository(IDatabase staticDb) => _static = staticDb;

    public void LoadStatic(World w)
    {
        foreach (var r in _static.Query("SELECT id,tag,name FROM country"))
            w.Countries[Convert.ToInt32(r["id"])] = new Country { Id = Convert.ToInt32(r["id"]), Tag = (string)r["tag"]!, Name = (string)r["name"]! };

        foreach (var r in _static.Query("SELECT id,name,owner_id,terrain,river,population,infrastructure FROM region"))
        {
            int id = Convert.ToInt32(r["id"]), owner = Convert.ToInt32(r["owner_id"]);
            w.Regions[id] = new Region
            {
                Id = id, Name = (string)r["name"]!, OwnerId = owner, ControllerId = owner,
                Terrain = (string)r["terrain"]!, River = Convert.ToInt32(r["river"]) == 1,
                Population = Convert.ToInt32(r["population"]), Infrastructure = Convert.ToSingle(r["infrastructure"]),
            };
        }
        foreach (var r in _static.Query("SELECT region_id,neighbour_id FROM region_neighbour"))
            w.Regions[Convert.ToInt32(r["region_id"])].Neighbours.Add(Convert.ToInt32(r["neighbour_id"]));
    }

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

    public void LoadSave(World w, IDatabase save)
    {
        foreach (var r in save.Query("SELECT key,value FROM save_meta"))
            if ((string)r["key"]! == "day") for (int i = 0; i < Convert.ToInt32(r["value"]); i++) w.Clock.Advance();
        foreach (var r in save.Query("SELECT id,controller_id,infrastructure FROM s_region"))
        { var reg = w.Regions[Convert.ToInt32(r["id"])]; reg.ControllerId = Convert.ToInt32(r["controller_id"]); reg.Infrastructure = Convert.ToSingle(r["infrastructure"]); }
        foreach (var r in save.Query("SELECT id,country_id,template_id,region_id,target_region_id,hp,org,supply FROM s_division"))
        {
            var d = new Division
            {
                Id = Convert.ToInt32(r["id"]), CountryId = Convert.ToInt32(r["country_id"]), TemplateId = Convert.ToInt32(r["template_id"]),
                RegionId = Convert.ToInt32(r["region_id"]), TargetRegionId = r["target_region_id"] is null ? null : Convert.ToInt32(r["target_region_id"]),
                Hp = Convert.ToSingle(r["hp"]), Org = Convert.ToSingle(r["org"]), Supply = Convert.ToSingle(r["supply"]),
            };
            w.Divisions[d.Id] = d; w.Regions[d.RegionId].DivisionIds.Add(d.Id);
        }
        foreach (var r in save.Query("SELECT a,b FROM s_war"))
        { int a = Convert.ToInt32(r["a"]), b = Convert.ToInt32(r["b"]); w.Countries[a].AtWarWith.Add(b); w.Countries[b].AtWarWith.Add(a); }
    }

    public void WriteSave(World w, IDatabase save)
    {
        save.BeginTransaction();
        save.Execute("DELETE FROM s_region"); save.Execute("DELETE FROM s_division"); save.Execute("DELETE FROM s_war"); save.Execute("DELETE FROM save_meta");
        save.Execute("INSERT INTO save_meta VALUES ('day',?)", w.Clock.Day);
        foreach (var r in w.Regions.Values)
            if (r.ControllerId != r.OwnerId || r.Infrastructure != 1f)
                save.Execute("INSERT INTO s_region VALUES (?,?,?)", r.Id, r.ControllerId, r.Infrastructure);
        foreach (var d in w.Divisions.Values)
            save.Execute("INSERT INTO s_division VALUES (?,?,?,?,?,?,?,?)", d.Id, d.CountryId, d.TemplateId, d.RegionId, d.TargetRegionId, d.Hp, d.Org, d.Supply);
        foreach (var c in w.Countries.Values) foreach (var e in c.AtWarWith) if (c.Id < e)
            save.Execute("INSERT INTO s_war VALUES (?,?,?)", c.Id, e, w.Clock.Day);
        save.Commit();
    }
}
