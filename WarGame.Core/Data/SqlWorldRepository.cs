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
        foreach (var r in _static.Query("SELECT id,name,move_cost,glyph,color FROM terrain"))
        {
            string id = (string)r["id"]!;
            w.Rules["move_cost:" + id] = Convert.ToSingle(r["move_cost"]);
            w.TerrainDefs[id] = new TerrainDef(id, (string)r["name"]!, Convert.ToSingle(r["move_cost"]),
                                              (string)r["glyph"]!, (string?)r["color"] ?? "");
        }

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

        foreach (var r in _static.Query("SELECT id,name,description FROM faction"))
            w.Factions[(string)r["id"]!] = new Faction((string)r["id"]!, (string)r["name"]!, r["description"] as string ?? "", new List<int>());
        foreach (var r in _static.Query("SELECT faction_id,country_tag FROM faction_member"))
            if (w.Factions.TryGetValue((string)r["faction_id"]!, out var f) && byTag.TryGetValue((string)r["country_tag"]!, out var c))
                f.Members.Add(c.Id);

        foreach (var r in _static.Query("SELECT id,branch,name,cost,requires,description,country_tag FROM tech"))
            w.Techs[(string)r["id"]!] = new Tech((string)r["id"]!, (string)r["branch"]!, (string)r["name"]!, Convert.ToSingle(r["cost"]), r["requires"] as string, r["description"] as string,
                                                 r["country_tag"] as string);
        foreach (var r in _static.Query("SELECT id,day,country_tag,title,body,watch,arg,glyph,tone,pause FROM news_event"))
        {
            int? cid = r["country_tag"] is string tag && byTag.TryGetValue(tag, out var nc) ? nc.Id : null;
            w.NewsEvents[(string)r["id"]!] = new NewsEvent((string)r["id"]!, Convert.ToInt32(r["day"]), cid,
                (string)r["title"]!, (string)r["body"]!, (string)r["watch"]!, Convert.ToSingle(r["arg"]),
                (string)r["glyph"]!, (string)r["tone"]!, Convert.ToInt32(r["pause"]) != 0);
        }
        foreach (var r in _static.Query("SELECT event_id,stat_key,value FROM news_event_effect"))
        {
            var eid = (string)r["event_id"]!;
            if (!w.NewsEffects.TryGetValue(eid, out var elist)) w.NewsEffects[eid] = elist = new();
            elist.Add(((string)r["stat_key"]!, Convert.ToSingle(r["value"])));
        }
        foreach (var r in _static.Query("SELECT id,event_id,title,sort FROM news_event_option ORDER BY event_id,sort,id"))
        {
            var eid = (string)r["event_id"]!;
            if (!w.NewsOptions.TryGetValue(eid, out var olist)) w.NewsOptions[eid] = olist = new();
            olist.Add(new NewsOption((string)r["id"]!, eid, (string)r["title"]!, Convert.ToInt32(r["sort"])));
        }
        foreach (var r in _static.Query("SELECT option_id,stat_key,value FROM news_event_option_effect"))
        {
            var oid = (string)r["option_id"]!;
            if (!w.NewsOptionEffects.TryGetValue(oid, out var olist)) w.NewsOptionEffects[oid] = olist = new();
            olist.Add(((string)r["stat_key"]!, Convert.ToSingle(r["value"])));
        }
        foreach (var r in _static.Query("SELECT id,grp,name,description,sort,is_default,min_tension,country_tag FROM law ORDER BY grp,sort"))
            w.Laws[(string)r["id"]!] = new Law((string)r["id"]!, (string)r["grp"]!, (string)r["name"]!,
                r["description"] as string ?? "", Convert.ToInt32(r["sort"]), Convert.ToInt32(r["is_default"]) == 1,
                r["country_tag"] as string, r["min_tension"] is null ? 0f : Convert.ToSingle(r["min_tension"]));
        foreach (var r in _static.Query("SELECT id,name,icon,sort,country_tag FROM law_group ORDER BY sort,id"))
            w.LawGroupDefs[(string)r["id"]!] = new LawGroupDef((string)r["id"]!, (string)r["name"]!,
                r["icon"] as string ?? "", Convert.ToInt32(r["sort"]), r["country_tag"] as string);
        foreach (var r in _static.Query("SELECT law_id,stat_key,value FROM law_effect"))
        {
            var lid = (string)r["law_id"]!;
            if (!w.LawEffects.TryGetValue(lid, out var llist)) w.LawEffects[lid] = llist = new();
            llist.Add(((string)r["stat_key"]!, Convert.ToSingle(r["value"])));
        }
        foreach (var r in _static.Query("SELECT id,name,icon,sort,country_tag,domain FROM army_doctrine_branch ORDER BY sort,id"))
            w.DoctrineBranches[(string)r["id"]!] = new DoctrineBranch((string)r["id"]!, (string)r["name"]!,
                (string)r["icon"]! , Convert.ToInt32(r["sort"]), r["country_tag"] as string,
                r["domain"] as string ?? World.Land);
        foreach (var r in _static.Query("SELECT id,branch,name,description,cost,requires,sort,country_tag FROM army_doctrine ORDER BY branch,sort"))
            w.ArmyDoctrines[(string)r["id"]!] = new ArmyDoctrine((string)r["id"]!, (string)r["branch"]!, (string)r["name"]!,
                r["description"] as string ?? "", Convert.ToSingle(r["cost"]), r["requires"] as string,
                Convert.ToInt32(r["sort"]), r["country_tag"] as string);
        foreach (var r in _static.Query("SELECT doctrine_id,stat_key,value FROM army_doctrine_effect"))
        {
            var did = (string)r["doctrine_id"]!;
            if (!w.DoctrineEffects.TryGetValue(did, out var dlist)) w.DoctrineEffects[did] = dlist = new();
            dlist.Add(((string)r["stat_key"]!, Convert.ToSingle(r["value"])));
        }
        foreach (var r in _static.Query("SELECT id,name,stat_key,per_unit,cap,fuel_per_unit,glyph FROM resource"))
            w.ResourceDefs[(string)r["id"]!] = new ResourceDef((string)r["id"]!, (string)r["name"]!,
                (string)r["stat_key"]!, Convert.ToSingle(r["per_unit"]), Convert.ToSingle(r["cap"]),
                r["fuel_per_unit"] is null ? 0f : Convert.ToSingle(r["fuel_per_unit"]),
                r["glyph"] as string ?? "caixa");
        foreach (var r in _static.Query("SELECT id,name,cost,days,stat_key,per_level,max_level,coastal,supply_range,hub_range,yard,icon,glyph FROM building"))
            w.BuildingDefs[(string)r["id"]!] = new BuildingDef((string)r["id"]!, (string)r["name"]!,
                Convert.ToSingle(r["cost"]), Convert.ToSingle(r["days"]), (string)r["stat_key"]!,
                Convert.ToSingle(r["per_level"]), Convert.ToInt32(r["max_level"]),
                Convert.ToInt32(r["coastal"]) != 0, Convert.ToSingle(r["supply_range"]),
                Convert.ToSingle(r["hub_range"]), (string)r["yard"]!,
                (string)r["icon"]!, (string)r["glyph"]!);
        foreach (var r in _static.Query("SELECT key,name,note,sort,glyph,digits,percent,shown FROM unit_stat_def ORDER BY sort"))
            w.UnitStatDefs[(string)r["key"]!] = new UnitStatDef((string)r["key"]!, (string)r["name"]!,
                (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!,
                Convert.ToInt32(r["digits"]), Convert.ToInt32(r["percent"]) != 0, Convert.ToInt32(r["shown"]) != 0);
        foreach (var r in _static.Query("SELECT id,name,glyph,sort FROM tech_branch ORDER BY sort"))
            w.TechBranches[(string)r["id"]!] = new TechBranchDef((string)r["id"]!, (string)r["name"]!,
                (string)r["glyph"]!, Convert.ToInt32(r["sort"]));
        foreach (var r in _static.Query("SELECT id,name,description,metric,threshold,bonus,sort,country_tag FROM medal ORDER BY sort"))
            w.MedalDefs[(string)r["id"]!] = new MedalDef((string)r["id"]!, (string)r["name"]!, (string)r["description"]!,
                (string)r["metric"]!, Convert.ToSingle(r["threshold"]), Convert.ToSingle(r["bonus"]), Convert.ToInt32(r["sort"]),
                r["country_tag"] as string);
        foreach (var r in _static.Query("SELECT id,name,domain,sort,country_tag FROM formation_name ORDER BY sort,id"))
            w.FormationNames.Add(new FormationName((string)r["id"]!, (string)r["name"]!, (string)r["domain"]!,
                Convert.ToInt32(r["sort"]), r["country_tag"] as string));
        foreach (var r in _static.Query("SELECT id,title,description,metric,threshold,bonus,sort FROM division_honour ORDER BY sort"))
            w.HonourDefs[(string)r["id"]!] = new HonourDef((string)r["id"]!, (string)r["title"]!, (string)r["description"]!,
                (string)r["metric"]!, Convert.ToSingle(r["threshold"]), Convert.ToSingle(r["bonus"]), Convert.ToInt32(r["sort"]));
        foreach (var r in _static.Query("SELECT id,name,icon,metric,low,high,sort,glyph FROM map_mode ORDER BY sort"))
            w.MapModeDefs[(string)r["id"]!] = new MapModeDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                (string)r["metric"]!, (string)r["low"]!, (string)r["high"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!);
        foreach (var r in _static.Query("SELECT id,name,icon,effect,value,note,sort,glyph FROM air_mission ORDER BY sort"))
            w.AirMissionDefs[(string)r["id"]!] = new AirMissionDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                (string)r["effect"]!, Convert.ToSingle(r["value"]), (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!);
        foreach (var r in _static.Query("SELECT id,name,icon,effect,value,note,sort,glyph FROM naval_mission ORDER BY sort"))
            w.NavalMissionDefs[(string)r["id"]!] = new NavalMissionDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                (string)r["effect"]!, Convert.ToSingle(r["value"]), (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!);
        foreach (var r in _static.Query("SELECT id,name,icon,resistance_mult,yield_mult,manpower_mult,note,sort,glyph FROM occupation_policy ORDER BY sort"))
            w.OccupationPolicyDefs[(string)r["id"]!] = new OccupationPolicyDef((string)r["id"]!, (string)r["name"]!,
                (string)r["icon"]!, Convert.ToSingle(r["resistance_mult"]), Convert.ToSingle(r["yield_mult"]),
                Convert.ToSingle(r["manpower_mult"]), (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!);
        foreach (var r in _static.Query("SELECT id,name,icon,weight,glyph FROM chronicle_kind"))
            w.ChronicleKinds[(string)r["id"]!] = new ChronicleKind((string)r["id"]!, (string)r["name"]!,
                (string)r["icon"]!, Convert.ToInt32(r["weight"]), (string)r["glyph"]!);
        w.SeasonDefs.Clear(); w.SeasonMonths.Clear(); w.SeasonTerrain.Clear();
        foreach (var r in _static.Query("SELECT id,name,icon,move_mult,org_mult,attrition,note,glyph,cold FROM season"))
            w.SeasonDefs[(string)r["id"]!] = new SeasonDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                Convert.ToSingle(r["move_mult"]), Convert.ToSingle(r["org_mult"]), Convert.ToSingle(r["attrition"]), (string)r["note"]!,
                (string)r["glyph"]!, Convert.ToSingle(r["cold"]));
        w.WeatherDefs.Clear();
        foreach (var r in _static.Query("SELECT id,name,icon,move_mult,org_mult,air_mult,cold_min,cold_max,terrain,weight,note,sort,glyph FROM weather ORDER BY sort"))
            w.WeatherDefs[(string)r["id"]!] = new WeatherDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                Convert.ToSingle(r["move_mult"]), Convert.ToSingle(r["org_mult"]), Convert.ToSingle(r["air_mult"]),
                Convert.ToSingle(r["cold_min"]), Convert.ToSingle(r["cold_max"]), (string)r["terrain"]!,
                Convert.ToSingle(r["weight"]), (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!);
        w.SubjectTypeDefs.Clear();
        foreach (var r in _static.Query("SELECT id,name,icon,autonomy_min,yield_share,manpower_share,drift,note,sort,glyph FROM subject_type ORDER BY sort"))
            w.SubjectTypeDefs[(string)r["id"]!] = new SubjectTypeDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                Convert.ToSingle(r["autonomy_min"]), Convert.ToSingle(r["yield_share"]), Convert.ToSingle(r["manpower_share"]),
                Convert.ToSingle(r["drift"]), (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!);
        w.VictoryTiers.Clear();
        foreach (var r in _static.Query("SELECT id,name,icon,points,min_pop,capital,note,sort,glyph,shape FROM victory_tier ORDER BY sort"))
            w.VictoryTiers[(string)r["id"]!] = new VictoryTierDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                Convert.ToInt32(r["points"]), Convert.ToInt64(r["min_pop"]), Convert.ToInt32(r["capital"]) != 0,
                (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!, (string)r["shape"]!);
        w.VeterancyTiers.Clear();
        foreach (var r in _static.Query("SELECT id,name,icon,min_xp,bonus,chevrons,note,sort,glyph FROM veterancy ORDER BY sort"))
            w.VeterancyTiers[(string)r["id"]!] = new VeterancyDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                Convert.ToSingle(r["min_xp"]), Convert.ToSingle(r["bonus"]), Convert.ToInt32(r["chevrons"]),
                (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!);
        w.TacticDefs.Clear();
        foreach (var r in _static.Query("SELECT id,name,icon,side,mult,counter_id,terrain,weight,note,sort,glyph FROM tactic ORDER BY sort"))
            w.TacticDefs[(string)r["id"]!] = new TacticDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                (string)r["side"]!, Convert.ToSingle(r["mult"]), (string)r["counter_id"]!, (string)r["terrain"]!,
                Convert.ToSingle(r["weight"]), (string)r["note"]!, Convert.ToInt32(r["sort"]), (string)r["glyph"]!);
        foreach (var r in _static.Query("SELECT month,season_id FROM season_month"))
            w.SeasonMonths[Convert.ToInt32(r["month"])] = (string)r["season_id"]!;
        foreach (var r in _static.Query("SELECT season_id,terrain,bite FROM season_terrain"))
        {
            string sid = (string)r["season_id"]!;
            if (!w.SeasonTerrain.TryGetValue(sid, out var byTerrain)) w.SeasonTerrain[sid] = byTerrain = new();
            byTerrain[(string)r["terrain"]!] = Convert.ToSingle(r["bite"]);
        }
        foreach (var r in _static.Query("SELECT id,name,sort FROM difficulty ORDER BY sort"))
            w.DifficultyDefs[(string)r["id"]!] = new DifficultyDef((string)r["id"]!, (string)r["name"]!,
                Convert.ToInt32(r["sort"]), new Dictionary<string, float>());
        foreach (var r in _static.Query("SELECT difficulty_id,rule_key,value FROM difficulty_effect"))
            if (w.DifficultyDefs.TryGetValue((string)r["difficulty_id"]!, out var dd))
                dd.Effects[(string)r["rule_key"]!] = Convert.ToSingle(r["value"]);
        foreach (var r in _static.Query("SELECT id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp FROM general"))
            w.GeneralDefs[(string)r["id"]!] = new GeneralDef((string)r["id"]!, (string)r["name"]!,
                (string)r["stat_key"]!, Convert.ToSingle(r["mult"]), Convert.ToSingle(r["cost"]),
                r["country_tag"] as string, r["icon"] as string ?? "🎖", r["note"] as string ?? "",
                r["domain"] as string ?? World.Land, r["xp"] is null ? 0f : Convert.ToSingle(r["xp"]));
        w.CabinetSlots.Clear();
        foreach (var r in _static.Query("SELECT id,name,icon,sort,glyph FROM cabinet_slot ORDER BY sort"))
            w.CabinetSlots.Add(new CabinetSlotDef((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                Convert.ToInt32(r["sort"]), (string)r["glyph"]!));
        foreach (var r in _static.Query("SELECT id,country_tag,slot,name,icon,cost,note FROM advisor ORDER BY id"))
            w.AdvisorDefs[(string)r["id"]!] = new AdvisorDef((string)r["id"]!, r["country_tag"] as string,
                (string)r["slot"]!, (string)r["name"]!, (string)r["icon"]!, Convert.ToSingle(r["cost"]),
                r["note"] as string ?? "", new Dictionary<string, float>());
        foreach (var r in _static.Query("SELECT advisor_id,stat_key,mult FROM advisor_effect"))
            if (w.AdvisorDefs.TryGetValue((string)r["advisor_id"]!, out var ad))
                ad.Effects[(string)r["stat_key"]!] = Convert.ToSingle(r["mult"]);
        w.PowerTiers.Clear();
        foreach (var r in _static.Query("SELECT level,name,min_share FROM power_tier ORDER BY min_share"))
            w.PowerTiers.Add(new PowerTier(Convert.ToInt32(r["level"]), (string)r["name"]!, Convert.ToSingle(r["min_share"])));
        foreach (var r in _static.Query("SELECT id,name,icon,days,weight,fatal,domain,glyph FROM wound_kind"))
            w.WoundKinds[(string)r["id"]!] = new WoundKind((string)r["id"]!, (string)r["name"]!, (string)r["icon"]!,
                Convert.ToInt32(r["days"]), Convert.ToSingle(r["weight"]), Convert.ToInt32(r["fatal"]) != 0,
                r["domain"] as string, (string)r["glyph"]!);
        w.GeneralRanks.Clear();
        foreach (var r in _static.Query("SELECT domain,level,name,xp,bonus,country_tag FROM general_rank ORDER BY domain,xp"))
            w.GeneralRanks.Add(new GeneralRank((string)r["domain"]!, Convert.ToInt32(r["level"]), (string)r["name"]!,
                Convert.ToSingle(r["xp"]), Convert.ToSingle(r["bonus"]), r["country_tag"] as string));
        foreach (var r in _static.Query("SELECT id,name,cost,days,cooldown,stat_key,mult FROM decision"))
            w.DecisionDefs[(string)r["id"]!] = new DecisionDef((string)r["id"]!, (string)r["name"]!,
                Convert.ToSingle(r["cost"]), Convert.ToInt32(r["days"]), Convert.ToInt32(r["cooldown"]),
                (string)r["stat_key"]!, Convert.ToSingle(r["mult"]));
        foreach (var r in _static.Query("SELECT id,name,description,cost,days,effect,magnitude,scope FROM spy_op"))
            w.SpyOps[(string)r["id"]!] = new SpyOp((string)r["id"]!, (string)r["name"]!, r["description"] as string ?? "",
                Convert.ToSingle(r["cost"]), Convert.ToInt32(r["days"]), (string)r["effect"]!, Convert.ToSingle(r["magnitude"]),
                r["scope"] as string ?? "country");
        foreach (var r in _static.Query("SELECT id,country_tag,name,description,days,requires,sort FROM focus"))
            if (byTag.TryGetValue((string)r["country_tag"]!, out var fc))
                w.Focuses[(string)r["id"]!] = new Focus((string)r["id"]!, fc.Id, (string)r["name"]!, (string)r["description"]!,
                    Convert.ToInt32(r["days"]), r["requires"] as string, Convert.ToInt32(r["sort"]));
        // Ligações da árvore: pré-requisitos extra (AND) e ramos que se excluem (sempre nos dois sentidos).
        foreach (var r in _static.Query("SELECT focus_id,requires_id FROM focus_link"))
        {
            string fid = (string)r["focus_id"]!, need = (string)r["requires_id"]!;
            if (!w.Focuses.ContainsKey(fid) || !w.Focuses.ContainsKey(need)) continue;
            if (!w.FocusLinks.TryGetValue(fid, out var list)) w.FocusLinks[fid] = list = new();
            if (!list.Contains(need)) list.Add(need);
        }
        foreach (var r in _static.Query("SELECT focus_id,rival_id FROM focus_rival"))
        {
            string a = (string)r["focus_id"]!, b = (string)r["rival_id"]!;
            if (!w.Focuses.ContainsKey(a) || !w.Focuses.ContainsKey(b) || a == b) continue;
            Add(a, b); Add(b, a);
            void Add(string x, string y)
            {
                if (!w.FocusRivals.TryGetValue(x, out var list)) w.FocusRivals[x] = list = new();
                if (!list.Contains(y)) list.Add(y);
            }
        }
        foreach (var r in _static.Query("SELECT focus_id,stat_key,value FROM focus_effect"))
        {
            var fid = (string)r["focus_id"]!;
            if (!w.FocusEffects.TryGetValue(fid, out var flist)) w.FocusEffects[fid] = flist = new();
            flist.Add(((string)r["stat_key"]!, Convert.ToSingle(r["value"])));
        }
        foreach (var r in _static.Query("SELECT tech_id,stat_key,value FROM tech_effect"))
        {
            string id = (string)r["tech_id"]!;
            if (!w.TechEffects.TryGetValue(id, out var list)) w.TechEffects[id] = list = new();
            list.Add(((string)r["stat_key"]!, Convert.ToSingle(r["value"])));
        }
        foreach (var r in _static.Query("SELECT country_tag,tech_id FROM country_tech"))
            if (byTag.TryGetValue((string)r["country_tag"]!, out var c) && w.Techs.ContainsKey((string)r["tech_id"]!)) c.Techs.Add((string)r["tech_id"]!);
        foreach (var c in w.Countries.Values) w.ApplyTechs(c);

        foreach (var r in _static.Query("SELECT id,name,kind,color,glyph,sort FROM zone ORDER BY sort"))
            w.Zones[(string)r["id"]!] = new ZoneDef((string)r["id"]!, (string)r["name"]!, (string)r["kind"]!,
                (string)r["color"]!, (string)r["glyph"]!, Convert.ToInt32(r["sort"]));
        foreach (var r in _static.Query("SELECT id,name,owner_id,terrain,river,population,infrastructure,centroid_x,centroid_y,coastal,lat,zone_id,sea_zone_id FROM region"))
        {
            int id = Convert.ToInt32(r["id"]), owner = Convert.ToInt32(r["owner_id"]);
            w.Regions[id] = new Region
            {
                Id = id, Name = (string)r["name"]!, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner,
                Terrain = (string)r["terrain"]!, River = Convert.ToInt32(r["river"]) == 1,
                Population = Convert.ToInt32(r["population"]), Infrastructure = Convert.ToSingle(r["infrastructure"]), BaseInfrastructure = Convert.ToSingle(r["infrastructure"]),
                CenterX = r["centroid_x"] is null ? 0f : Convert.ToSingle(r["centroid_x"]),
                CenterY = r["centroid_y"] is null ? 0f : Convert.ToSingle(r["centroid_y"]),
                Coastal = Convert.ToInt32(r["coastal"]) == 1,
                Lat = r["lat"] is null ? 0f : Convert.ToSingle(r["lat"]),
                ZoneId = r["zone_id"] as string ?? "", SeaZoneId = r["sea_zone_id"] as string ?? "",
            };
        }
        foreach (var r in _static.Query("SELECT region_id,neighbour_id FROM region_neighbour"))
            w.Regions[Convert.ToInt32(r["region_id"])].Neighbours.Add(Convert.ToInt32(r["neighbour_id"]));
        foreach (var r in _static.Query("SELECT region_id,neighbour_id,km FROM sea_link"))
        {
            int p = Convert.ToInt32(r["region_id"]), q = Convert.ToInt32(r["neighbour_id"]);
            float km = Convert.ToSingle(r["km"]);
            w.Regions[p].SeaNeighbours[q] = km; w.Regions[q].SeaNeighbours[p] = km;
        }
        // As cidades chegam já ordenadas da maior para a menor: o mapa mostra-as por essa ordem e não tem
        // de as ordenar outra vez a cada mudança de zoom.
        foreach (var r in _static.Query("SELECT id,region_id,name,population,capital,x,y FROM city ORDER BY population DESC, id"))
            w.Cities.Add(new CityDef(Convert.ToInt32(r["id"]), Convert.ToInt32(r["region_id"]), (string)r["name"]!,
                                     Convert.ToInt32(r["population"]), Convert.ToInt32(r["capital"]) == 1,
                                     Convert.ToSingle(r["x"]), Convert.ToSingle(r["y"])));
        foreach (var r in _static.Query("SELECT region_id,resource,amount FROM region_resource"))
            if (w.Regions.TryGetValue(Convert.ToInt32(r["region_id"]), out var rr))
                rr.Resources[(string)r["resource"]!] = Convert.ToSingle(r["amount"]);
        w.SeedRails();   // a rede ferroviária de partida sai da infraestrutura que a terra já tinha
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

    public void LoadStartWars(World w)
    {
        var byTag = w.Countries.Values.ToDictionary(c => c.Tag);
        foreach (var r in _static.Query("SELECT a_tag,b_tag FROM start_war"))
            if (byTag.TryGetValue((string)r["a_tag"]!, out var a) && byTag.TryGetValue((string)r["b_tag"]!, out var b) && a.Id != b.Id)
                w.StartWar(a.Id, b.Id);
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

    /// <summary>O schema do save reconstruído a partir do sqlite_master de uma base já feita — é o que o
    /// telemóvel tem, porque os *.sql ficam fora do export do APK. O SQLite guarda os CREATE sem o
    /// IF NOT EXISTS: repõe-se aqui, e num sítio só, para o jogo e os testes lerem o mesmo texto (o
    /// UNIQUE INDEX é caso à parte, e ficar de fora dava "index já existe" ao reabrir um save).</summary>
    public static string SchemaFromSqliteMaster(IDatabase staticDb) =>
        string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!)
                .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ")
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ")
                .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";

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
        // Colunas acrescentadas a tabelas de save já existentes (saves de versões anteriores).
        foreach (var (table, column, ddl) in SaveMigrations)
            if (!save.Query($"PRAGMA table_info({table})").Any(r => (string)r["name"]! == column))
                save.Execute($"ALTER TABLE {table} ADD COLUMN {column} {ddl}");
    }

    private static readonly (string Table, string Column, string Ddl)[] SaveMigrations =
    {
        ("s_production_queue", "unit_type_id", "INTEGER NOT NULL DEFAULT 0"),
        ("s_spy_op", "region_id", "INTEGER NOT NULL DEFAULT 0"),
        ("s_offer", "region_id", "INTEGER NOT NULL DEFAULT 0"),
        ("s_history", "power", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "research_tech", "TEXT"),
        ("s_country", "research_progress", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "capitulated", "INTEGER NOT NULL DEFAULT 0"),
        ("s_country", "capitulated_day", "INTEGER"),
        ("s_region", "owner_id", "INTEGER"),
        ("s_country", "manpower", "REAL NOT NULL DEFAULT -1"),
        ("s_country", "focus", "TEXT"),
        ("s_country", "focus_progress", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "justify_target", "INTEGER"),
        ("s_country", "justify_progress", "REAL NOT NULL DEFAULT 0"),
        ("s_war", "last_progress_day", "INTEGER"),
        ("s_region", "building", "INTEGER NOT NULL DEFAULT 0"),
        ("s_region", "build_progress", "REAL NOT NULL DEFAULT 0"),
        ("s_region", "fort", "INTEGER NOT NULL DEFAULT 0"),
        ("s_region", "fort_building", "INTEGER NOT NULL DEFAULT 0"),
        ("s_region", "fort_progress", "REAL NOT NULL DEFAULT 0"),
        ("s_region", "resistance", "REAL NOT NULL DEFAULT 0"),
        ("s_region", "project", "TEXT"),
        ("s_region", "project_progress", "REAL NOT NULL DEFAULT 0"),
        ("s_region", "integration", "REAL NOT NULL DEFAULT 0"),
        ("s_region", "rail", "INTEGER NOT NULL DEFAULT -1"),
        ("s_region", "rail_building", "INTEGER NOT NULL DEFAULT 0"),
        ("s_region", "rail_progress", "REAL NOT NULL DEFAULT 0"),
        ("s_region", "project_owner", "INTEGER NOT NULL DEFAULT 0"),
        ("s_country", "war_exhaustion", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "political", "REAL NOT NULL DEFAULT 0"),
        ("s_division", "xp", "REAL NOT NULL DEFAULT 0"),
        ("s_division", "auto_advance", "INTEGER NOT NULL DEFAULT 0"),
        ("s_country", "air_power", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "warships", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "convoys", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "nukes", "INTEGER NOT NULL DEFAULT 0"),
        ("s_production_queue", "repeat_order", "INTEGER NOT NULL DEFAULT 0"),
        ("s_production_queue", "factories", "INTEGER NOT NULL DEFAULT 1"),
        ("s_war", "a_regions", "INTEGER NOT NULL DEFAULT 0"),
        ("s_war", "b_regions", "INTEGER NOT NULL DEFAULT 0"),
        ("s_war", "a_losses", "INTEGER NOT NULL DEFAULT 0"),
        ("s_war", "b_losses", "INTEGER NOT NULL DEFAULT 0"),
        ("s_war", "a_battles", "INTEGER NOT NULL DEFAULT 0"),
        ("s_war", "b_battles", "INTEGER NOT NULL DEFAULT 0"),
        ("s_division", "battles", "INTEGER NOT NULL DEFAULT 0"),
        ("s_division", "captures", "INTEGER NOT NULL DEFAULT 0"),
        ("s_army_group", "stance", "INTEGER NOT NULL DEFAULT 0"),
        ("s_army_group", "general", "TEXT"),
        ("s_general", "xp", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "fuel", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "power_rank", "INTEGER NOT NULL DEFAULT 0"),
        ("s_country", "power_rank_prev", "INTEGER NOT NULL DEFAULT 0"),
        ("s_division", "honour", "TEXT"),
        ("s_division", "honour_name", "TEXT"),
        ("s_general", "wound_until", "INTEGER NOT NULL DEFAULT 0"),
        ("s_army_group", "planning", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "army_xp", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "defeat_streak", "INTEGER NOT NULL DEFAULT 0"),
        ("s_country", "last_defeat_day", "INTEGER NOT NULL DEFAULT -1"),
        ("s_country", "last_defeat_region", "INTEGER NOT NULL DEFAULT 0"),
        ("s_division", "entrench", "REAL NOT NULL DEFAULT 0"),
        ("s_trade_deal", "price_per_unit", "REAL NOT NULL DEFAULT 0"),
        ("s_trade_deal", "until_day", "INTEGER NOT NULL DEFAULT 0"),
        ("s_production_queue", "efficiency", "REAL NOT NULL DEFAULT 1"),
        ("s_production_queue", "delivered", "INTEGER NOT NULL DEFAULT 0"),
        ("s_country", "air_xp", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "navy_xp", "REAL NOT NULL DEFAULT 0"),
        ("s_general", "wound_kind", "TEXT NOT NULL DEFAULT ''"),
        ("s_air_mission", "name", "TEXT NOT NULL DEFAULT ''"),
        ("s_naval_mission", "name", "TEXT NOT NULL DEFAULT ''"),
        ("s_division", "pocket_days", "INTEGER NOT NULL DEFAULT 0"),
        ("s_division", "volunteer_from", "INTEGER"),
        ("s_army_group", "front_region_id", "INTEGER"),
        ("s_division", "drop_target", "INTEGER"),
        ("s_division", "drop_days", "REAL NOT NULL DEFAULT 0"),
        ("s_division", "redeploy", "INTEGER NOT NULL DEFAULT 0"),
    };

    public static bool HasSave(IDatabase save) =>
        save.Query("SELECT name FROM sqlite_master WHERE type='table' AND name='save_meta'").Count > 0
        && save.Query("SELECT value FROM save_meta WHERE key='day'").Count > 0;

    public void LoadSave(World w, IDatabase save)
    {
        foreach (var r in save.Query("SELECT key,value FROM save_meta"))
        {
            if ((string)r["key"]! == "day") for (int i = 0; i < Convert.ToInt32(r["value"]); i++) w.Clock.Advance();
            else if ((string)r["key"]! == "difficulty") w.ApplyDifficulty((string)r["value"]!);
        }
        // Templates desenhados em jogo — antes das divisões, que podem referenciá-los.
        foreach (var r in save.Query("SELECT id,country_id,name FROM template ORDER BY id"))
        {
            int tid = Convert.ToInt32(r["id"]);
            var units = save.Query("SELECT unit_type_id,qty FROM template_unit WHERE template_id=?", tid)
                .Select(u => (Convert.ToInt32(u["unit_type_id"]), Convert.ToInt32(u["qty"]))).ToList();
            w.Units.AddCustomTemplate(tid, Convert.ToInt32(r["country_id"]), (string)r["name"]!, units);
            w.CustomTemplateIds.Add(tid);
        }
        // Facções fundadas em jogo + composição gravada (linhas presentes substituem a da static.db)
        foreach (var r in save.Query("SELECT id,name,description FROM s_faction"))
            w.CreateFaction((string)r["id"]!, (string)r["name"]!, r["description"] as string ?? "");
        var factionMembers = save.Query("SELECT faction_id,country_id FROM s_faction_member");
        if (factionMembers.Count > 0)
        {
            foreach (var f in w.Factions.Values) f.Members.Clear();
            foreach (var r in factionMembers)
                if (w.Factions.TryGetValue((string)r["faction_id"]!, out var f)) f.Members.Add(Convert.ToInt32(r["country_id"]));
        }
        foreach (var r in save.Query("SELECT id,is_player,money,political,research_tech,research_progress,capitulated,capitulated_day,manpower,focus,focus_progress,stability,justify_target,justify_progress,war_exhaustion,air_power,warships,convoys,nukes,power_rank,power_rank_prev,army_xp,air_xp,navy_xp,defeat_streak,last_defeat_day,last_defeat_region,fuel,overlord,autonomy FROM s_country"))
        {
            var c = w.Countries[Convert.ToInt32(r["id"])];
            c.IsPlayer = Convert.ToInt32(r["is_player"]) == 1; c.Money = Convert.ToSingle(r["money"]);
            c.ResearchTech = r["research_tech"] as string; c.ResearchProgress = Convert.ToSingle(r["research_progress"]);
            c.Capitulated = r["capitulated"] is not null && Convert.ToInt32(r["capitulated"]) == 1;
            c.CapitulatedDay = r["capitulated_day"] is null ? null : Convert.ToInt32(r["capitulated_day"]);
            if (r["manpower"] is not null) c.Manpower = Convert.ToSingle(r["manpower"]);
            c.CurrentFocus = r["focus"] as string;
            if (r["focus_progress"] is not null) c.FocusProgress = Convert.ToSingle(r["focus_progress"]);
            if (r["stability"] is not null) c.Stability = Convert.ToSingle(r["stability"]);
            c.JustifyTarget = r["justify_target"] is null ? null : Convert.ToInt32(r["justify_target"]);
            if (r["justify_progress"] is not null) c.JustifyProgress = Convert.ToSingle(r["justify_progress"]);
            if (r["war_exhaustion"] is not null) c.WarExhaustion = Convert.ToSingle(r["war_exhaustion"]);
            if (r["political"] is not null) c.Political = Convert.ToSingle(r["political"]);
            if (r["air_power"] is not null) c.AirPower = Convert.ToSingle(r["air_power"]);
            if (r["warships"] is not null) c.Warships = Convert.ToSingle(r["warships"]);
            if (r["convoys"] is not null) c.Convoys = Convert.ToSingle(r["convoys"]);
            if (r["nukes"] is not null) c.Nukes = Convert.ToInt32(r["nukes"]);
            if (r["fuel"] is not null) c.Fuel = Convert.ToSingle(r["fuel"]);
            if (r["power_rank"] is not null) c.PowerRank = Convert.ToInt32(r["power_rank"]);
            if (r["power_rank_prev"] is not null) c.PowerRankPrev = Convert.ToInt32(r["power_rank_prev"]);
            if (r["army_xp"] is not null) c.ArmyXp = Convert.ToSingle(r["army_xp"]);
            if (r["air_xp"] is not null) c.AirXp = Convert.ToSingle(r["air_xp"]);
            if (r["navy_xp"] is not null) c.NavyXp = Convert.ToSingle(r["navy_xp"]);
            if (r["defeat_streak"] is not null) c.DefeatStreak = Convert.ToInt32(r["defeat_streak"]);
            if (r["last_defeat_day"] is not null) c.LastDefeatDay = Convert.ToInt32(r["last_defeat_day"]);
            if (r["last_defeat_region"] is not null) c.LastDefeatRegion = Convert.ToInt32(r["last_defeat_region"]);
            if (r["overlord"] is not null) c.OverlordId = Convert.ToInt32(r["overlord"]);
            if (r["autonomy"] is not null) c.Autonomy = Convert.ToSingle(r["autonomy"]);
        }
        foreach (var r in save.Query("SELECT country_id,tech_id FROM s_country_tech"))
            w.Countries[Convert.ToInt32(r["country_id"])].Techs.Add((string)r["tech_id"]!);
        foreach (var r in save.Query("SELECT country_id,focus_id FROM s_focus"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var cf)) cf.FocusesDone.Add((string)r["focus_id"]!);
        foreach (var r in save.Query("SELECT country_id,doctrine_id FROM s_army_doctrine"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var cd)) cd.Doctrines.Add((string)r["doctrine_id"]!);
        foreach (var r in save.Query("SELECT event_id,option_id FROM s_news_choice"))
            w.NewsChoices[(string)r["event_id"]!] = (string)r["option_id"]!;
        foreach (var r in save.Query("SELECT event_id,day,country_id FROM s_news_fired"))
            w.NewsFired[(string)r["event_id"]!] = (Convert.ToInt32(r["day"]), Convert.ToInt32(r["country_id"]));
        foreach (var r in save.Query("SELECT country_id,grp,law_id FROM s_country_law"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var cl)) cl.Laws[(string)r["grp"]!] = (string)r["law_id"]!;
        foreach (var r in save.Query("SELECT country_id,general,xp,wound_until,wound_kind FROM s_general"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var gc))
            {
                string gid = (string)r["general"]!;
                gc.Generals.Add(gid);
                gc.GeneralXp[gid] = Convert.ToSingle(r["xp"]);
                int until = Convert.ToInt32(r["wound_until"]);
                if (until > w.Clock.Day)                                 // ferimento já sarado não volta do save
                {
                    gc.GeneralWound[gid] = until;
                    if (r["wound_kind"] as string is { Length: > 0 } kind) gc.GeneralWoundKind[gid] = kind;
                }
            }
        foreach (var r in save.Query("SELECT country_id,from_country_id,men FROM s_prisoner"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var pc))
                pc.Prisoners[Convert.ToInt32(r["from_country_id"])] = Convert.ToInt32(r["men"]);
        foreach (var r in save.Query("SELECT country_id,host_id,since_day,legitimacy FROM s_exile"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var ec))
            {
                ec.ExileHostId = Convert.ToInt32(r["host_id"]);
                ec.ExileDay = Convert.ToInt32(r["since_day"]);
                ec.ExileLegitimacy = Convert.ToSingle(r["legitimacy"]);
            }
        foreach (var c in w.Countries.Values) World.ApplyGenerals(w, c);
        foreach (var r in save.Query("SELECT country_id,slot,advisor,since_day FROM s_cabinet"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var cab))
            {
                cab.Cabinet[(string)r["slot"]!] = (string)r["advisor"]!;
                cab.CabinetSince[(string)r["slot"]!] = Convert.ToInt32(r["since_day"]);
            }
        foreach (var c in w.Countries.Values) World.ApplyCabinet(w, c);
        foreach (var r in save.Query("SELECT country_id,decision,until_day,cooldown_until FROM s_decision"))
        {
            int cid = Convert.ToInt32(r["country_id"]); string did = (string)r["decision"]!;
            int until = Convert.ToInt32(r["until_day"]);
            if (until >= w.Clock.Day)
                w.ActiveDecisions.Add(new ActiveDecision { CountryId = cid, DecisionId = did, UntilDay = until });
            if (w.Countries.TryGetValue(cid, out var dc)) dc.DecisionCooldownUntil[did] = Convert.ToInt32(r["cooldown_until"]);
        }
        foreach (var r in save.Query("SELECT region_id,building,level FROM s_region_building"))
            if (w.Regions.TryGetValue(Convert.ToInt32(r["region_id"]), out var reg))
                reg.Buildings[(string)r["building"]!] = Convert.ToInt32(r["level"]);
        foreach (var r in save.Query("SELECT day,kind,text,country_id,region_id FROM s_chronicle ORDER BY ord"))
            w.Chronicle.Add(new ChronicleEntry(Convert.ToInt32(r["day"]), (string)r["kind"]!, (string)r["text"]!,
                Convert.ToInt32(r["country_id"]), Convert.ToInt32(r["region_id"])));
        foreach (var r in save.Query("SELECT day,country_id,money,divisions,regions,power FROM s_history ORDER BY day"))
            w.History.Add(new HistorySample(Convert.ToInt32(r["day"]), Convert.ToInt32(r["country_id"]),
                Convert.ToSingle(r["money"]), Convert.ToInt32(r["divisions"]), Convert.ToInt32(r["regions"]),
                Convert.ToSingle(r["power"])));
        foreach (var r in save.Query("SELECT buyer_id,seller_id,resource,units,price_per_unit,until_day FROM s_trade_deal"))
            w.TradeDeals.Add(new TradeDeal { BuyerId = Convert.ToInt32(r["buyer_id"]), SellerId = Convert.ToInt32(r["seller_id"]),
                ResourceId = (string)r["resource"]!, Units = Convert.ToSingle(r["units"]),
                PricePerUnit = r["price_per_unit"] is null ? 0f : Convert.ToSingle(r["price_per_unit"]),
                UntilDay = r["until_day"] is null ? 0 : Convert.ToInt32(r["until_day"]) });
        foreach (var r in save.Query("SELECT from_id,to_id,share,since_day,sent_total FROM s_lend_lease"))
            w.LendLeases.Add(new LendLease { FromId = Convert.ToInt32(r["from_id"]), ToId = Convert.ToInt32(r["to_id"]),
                Share = Convert.ToSingle(r["share"]), SinceDay = Convert.ToInt32(r["since_day"]),
                SentTotal = Convert.ToSingle(r["sent_total"]) });
        foreach (var r in save.Query("SELECT from_id,to_id,kind,men,region_id,day,expires_day FROM s_offer"))
            w.Offers.Add(new PendingOffer { FromId = Convert.ToInt32(r["from_id"]), ToId = Convert.ToInt32(r["to_id"]),
                Kind = (string)r["kind"]!, Men = Convert.ToInt32(r["men"]), RegionId = Convert.ToInt32(r["region_id"]),
                Day = Convert.ToInt32(r["day"]), ExpiresDay = Convert.ToInt32(r["expires_day"]) });
        foreach (var r in save.Query("SELECT country_id,target_id,op_id,days_left,region_id FROM s_spy_op"))
            w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = Convert.ToInt32(r["country_id"]), TargetCountryId = Convert.ToInt32(r["target_id"]),
                OpId = (string)r["op_id"]!, DaysLeft = Convert.ToSingle(r["days_left"]), RegionId = Convert.ToInt32(r["region_id"]) });
        foreach (var r in save.Query("SELECT country_id,target_id,until_day FROM s_intel"))
            w.Intel[(Convert.ToInt32(r["country_id"]), Convert.ToInt32(r["target_id"]))] = Convert.ToInt32(r["until_day"]);
        foreach (var r in save.Query("SELECT a,b,until_day FROM s_pact"))
            w.Pacts[(Convert.ToInt32(r["a"]), Convert.ToInt32(r["b"]))] = Convert.ToInt32(r["until_day"]);
        foreach (var r in save.Query("SELECT country_id,region_id,mission_id,wings,since_day,name FROM s_air_mission"))
            w.AirMissions.Add(new AirMission
            {
                CountryId = Convert.ToInt32(r["country_id"]), RegionId = Convert.ToInt32(r["region_id"]),
                MissionId = (string)r["mission_id"]!, Wings = Convert.ToSingle(r["wings"]),
                SinceDay = Convert.ToInt32(r["since_day"]), Name = r["name"] as string ?? "",
            });
        foreach (var r in save.Query("SELECT country_id,region_id,mission_id,ships,since_day,name FROM s_naval_mission"))
            w.NavalMissions.Add(new NavalMission
            {
                CountryId = Convert.ToInt32(r["country_id"]), RegionId = Convert.ToInt32(r["region_id"]),
                MissionId = (string)r["mission_id"]!, Ships = Convert.ToSingle(r["ships"]),
                SinceDay = Convert.ToInt32(r["since_day"]), Name = r["name"] as string ?? "",
            });
        // saves feitos antes de haver nomes de formação trazem as missões com a coluna vazia (é o DEFAULT ''
        // da migração): baptizam-se aqui, senão a asa ficava para sempre "asa sem nome" na ficha da Guerra
        foreach (var m in w.AirMissions.Where(m => m.Name.Length == 0).ToList())
            m.Name = w.NextFormationName(m.CountryId, World.Air, m.RegionId);
        foreach (var m in w.NavalMissions.Where(m => m.Name.Length == 0).ToList())
            m.Name = w.NextFormationName(m.CountryId, World.Sea, m.RegionId);
        foreach (var r in save.Query("SELECT country_id,target_id,policy_id,since_day FROM s_occupation"))
            w.Occupations.Add(new Occupation
            {
                CountryId = Convert.ToInt32(r["country_id"]), TargetId = Convert.ToInt32(r["target_id"]),
                PolicyId = (string)r["policy_id"]!, SinceDay = Convert.ToInt32(r["since_day"]),
            });
        foreach (var r in save.Query("SELECT country_id,host_id,since_day,learned FROM s_attache"))
            w.Attaches[Convert.ToInt32(r["country_id"])] = new Attache
            {
                CountryId = Convert.ToInt32(r["country_id"]), HostId = Convert.ToInt32(r["host_id"]),
                SinceDay = Convert.ToInt32(r["since_day"]), Learned = Convert.ToSingle(r["learned"]),
            };
        foreach (var c in w.Countries.Values) w.ApplyTechs(c);
        foreach (var r in save.Query("SELECT id,controller_id,infrastructure,owner_id,building,build_progress,fort,fort_building,fort_progress,resistance,project,project_progress,integration,rail,rail_building,rail_progress,project_owner FROM s_region"))
        {
            var reg = w.Regions[Convert.ToInt32(r["id"])];
            reg.ControllerId = Convert.ToInt32(r["controller_id"]); reg.Infrastructure = Convert.ToSingle(r["infrastructure"]);
            if (r["owner_id"] is not null && Convert.ToInt32(r["owner_id"]) > 0) reg.OwnerId = Convert.ToInt32(r["owner_id"]);   // NULL = dono da static
            if (r["building"] is not null) reg.Building = Convert.ToInt32(r["building"]) == 1;
            if (r["build_progress"] is not null) reg.BuildProgress = Convert.ToSingle(r["build_progress"]);
            if (r["fort"] is not null) reg.Fort = Convert.ToInt32(r["fort"]);
            if (r["fort_building"] is not null) reg.FortBuilding = Convert.ToInt32(r["fort_building"]) == 1;
            if (r["fort_progress"] is not null) reg.FortProgress = Convert.ToSingle(r["fort_progress"]);
            if (r["resistance"] is not null) reg.Resistance = Convert.ToSingle(r["resistance"]);
            if (r["project"] is string proj && proj.Length > 0) reg.Project = proj;
            if (r["project_progress"] is not null) reg.ProjectProgress = Convert.ToSingle(r["project_progress"]);
            if (r["integration"] is not null) reg.Integration = Convert.ToSingle(r["integration"]);
            if (r["rail"] is not null && Convert.ToInt32(r["rail"]) >= 0) reg.Rail = Convert.ToInt32(r["rail"]);
            if (r["rail_building"] is not null) reg.RailBuilding = Convert.ToInt32(r["rail_building"]) == 1;
            if (r["rail_progress"] is not null) reg.RailProgress = Convert.ToSingle(r["rail_progress"]);
            if (r["project_owner"] is not null) reg.ProjectOwner = Convert.ToInt32(r["project_owner"]);
        }
        foreach (var r in save.Query("SELECT id,country_id,template_id,region_id,hp,org,supply,move_progress,path,name,xp,auto_advance,battles,captures,honour,honour_name,entrench,pocket_days,volunteer_from,drop_target,drop_days,redeploy FROM s_division ORDER BY id"))
        {
            var d = new Division
            {
                Id = Convert.ToInt32(r["id"]), CountryId = Convert.ToInt32(r["country_id"]), TemplateId = Convert.ToInt32(r["template_id"]),
                RegionId = Convert.ToInt32(r["region_id"]), Name = r["name"] as string,
                Hp = Convert.ToSingle(r["hp"]), Org = Convert.ToSingle(r["org"]), Supply = Convert.ToSingle(r["supply"]),
            };
            if (r["xp"] is not null) d.Xp = Convert.ToSingle(r["xp"]);
            if (r["auto_advance"] is not null) d.AutoAdvance = Convert.ToInt32(r["auto_advance"]) != 0;
            if (r["battles"] is not null) d.Battles = Convert.ToInt32(r["battles"]);
            if (r["captures"] is not null) d.Captures = Convert.ToInt32(r["captures"]);
            if (r["honour"] is string hon && hon.Length > 0) { d.Honour = hon; d.HonourName = r["honour_name"] as string; }
            if (r["entrench"] is not null) d.Entrench = Convert.ToSingle(r["entrench"]);
            if (r["pocket_days"] is not null) d.PocketDays = Convert.ToInt32(r["pocket_days"]);
            if (r["volunteer_from"] is not null) d.VolunteerFrom = Convert.ToInt32(r["volunteer_from"]);
            // salto a meio: só se guarda se ainda faltarem dias de voo, senão a tropa ficava presa no ar
            if (r["drop_days"] is not null) d.DropDays = Convert.ToSingle(r["drop_days"]);
            if (r["drop_target"] is not null && d.DropDays > 0f) d.DropTargetId = Convert.ToInt32(r["drop_target"]);
            else d.DropDays = 0f;
            if (r["path"] is string p && p.Length > 0) d.SetPath(p.Split(',').Select(int.Parse));
            // o comboio a meio caminho guarda-se, mas só enquanto houver caminho: sem rota não há redespacho
            if (r["redeploy"] is not null) d.Redeploying = Convert.ToInt32(r["redeploy"]) != 0 && d.Path.Count > 0;
            d.MoveProgress = Convert.ToSingle(r["move_progress"]);
            w.AddDivision(d);
        }
        foreach (var r in save.Query("SELECT division_id,medal FROM s_division_medal"))
            if (w.Divisions.TryGetValue(Convert.ToInt32(r["division_id"]), out var md)) md.Medals.Add((string)r["medal"]!);
        // material de cada divisão (0..1); um save antigo não tem linha nenhuma e a divisão fica armada, que
        // é como ela sempre esteve antes de haver armazém
        foreach (var r in save.Query("SELECT division_id,kit FROM s_division_kit"))
            if (w.Divisions.TryGetValue(Convert.ToInt32(r["division_id"]), out var kd))
                kd.Kit = Math.Clamp(Convert.ToSingle(r["kit"]), 0f, 1f);
        foreach (var r in save.Query("SELECT id,country_id,name,front_country_id,front_region_id,advancing,stance,general,planning FROM s_army_group ORDER BY id"))
        {
            var g = new ArmyGroup
            {
                Id = Convert.ToInt32(r["id"]), CountryId = Convert.ToInt32(r["country_id"]), Name = (string)r["name"]!,
                FrontCountryId = r["front_country_id"] is null ? null : Convert.ToInt32(r["front_country_id"]),
                FrontRegionId = r["front_region_id"] is null ? null : Convert.ToInt32(r["front_region_id"]),
                // saves anteriores à postura de defesa só trazem advancing: vale como "avançar"
                Stance = r["stance"] is not null ? (GroupStance)Convert.ToInt32(r["stance"])
                       : r["advancing"] is not null && Convert.ToInt32(r["advancing"]) != 0 ? GroupStance.Advance
                       : GroupStance.Hold,
                GeneralId = r["general"] as string,
                Planning = r["planning"] is null ? 0f : Convert.ToSingle(r["planning"]),
            };
            w.ArmyGroups[g.Id] = g;
        }
        foreach (var r in save.Query("SELECT group_id,division_id FROM s_army_group_member"))
        {
            int div = Convert.ToInt32(r["division_id"]);
            if (w.ArmyGroups.TryGetValue(Convert.ToInt32(r["group_id"]), out var g) && w.Divisions.ContainsKey(div)) w.JoinGroup(g, div);
        }
        // os grupos chegam depois dos generais: quem está destacado tem de sair outra vez do bónus do país
        foreach (int cid in w.ArmyGroups.Values.Where(g => g.GeneralId is not null).Select(g => g.CountryId).Distinct())
            if (w.Countries.TryGetValue(cid, out var gc)) World.ApplyGenerals(w, gc);
        foreach (var r in save.Query("SELECT a,b,since_day,last_progress_day,a_regions,b_regions,a_losses,b_losses,a_battles,b_battles FROM s_war"))
        {
            int a = Convert.ToInt32(r["a"]), b = Convert.ToInt32(r["b"]);
            w.StartWar(a, b, Convert.ToInt32(r["since_day"]));
            var info = w.Wars[World.WarKey(a, b)];
            info.LastProgressDay = r["last_progress_day"] is null ? Convert.ToInt32(r["since_day"]) : Convert.ToInt32(r["last_progress_day"]);
            info.SideA.RegionsTaken = Convert.ToInt32(r["a_regions"]); info.SideB.RegionsTaken = Convert.ToInt32(r["b_regions"]);
            info.SideA.DivisionsLost = Convert.ToInt32(r["a_losses"]); info.SideB.DivisionsLost = Convert.ToInt32(r["b_losses"]);
            info.SideA.BattlesWon = Convert.ToInt32(r["a_battles"]); info.SideB.BattlesWon = Convert.ToInt32(r["b_battles"]);
        }
        foreach (var r in save.Query("SELECT a,b,country_id,region_id FROM s_war_goal"))
            if (w.Wars.TryGetValue((Convert.ToInt32(r["a"]), Convert.ToInt32(r["b"])), out var war))
                war.Side(Convert.ToInt32(r["country_id"])).Goals.Add(Convert.ToInt32(r["region_id"]));
        foreach (var r in save.Query("SELECT a,b,start_day,end_day,a_regions,b_regions,a_losses,b_losses,a_battles,b_battles FROM s_war_history ORDER BY id"))
            w.WarHistory.Add(new WarRecord(Convert.ToInt32(r["a"]), Convert.ToInt32(r["b"]), Convert.ToInt32(r["start_day"]), Convert.ToInt32(r["end_day"]),
                Convert.ToInt32(r["a_regions"]), Convert.ToInt32(r["b_regions"]), Convert.ToInt32(r["a_losses"]), Convert.ToInt32(r["b_losses"]),
                Convert.ToInt32(r["a_battles"]), Convert.ToInt32(r["b_battles"])));
        // ranhuras de investigação: a tabela própria manda sobre as colunas antigas de s_country, que só
        // seguram a primeira linha (saves anteriores às ranhuras entram por lá e ficam com uma)
        var lines = save.Query("SELECT country_id,tech_id,progress FROM s_research ORDER BY country_id,tech_id");
        foreach (var cid in lines.Select(r => Convert.ToInt32(r["country_id"])).Distinct())
            if (w.Countries.TryGetValue(cid, out var rc)) rc.Research.Clear();
        foreach (var r in lines)
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var rc))
                rc.Research[(string)r["tech_id"]!] = Convert.ToSingle(r["progress"]);
        foreach (var r in save.Query("SELECT country_id,template_id,progress,repeat_order,factories,efficiency,delivered,unit_type_id FROM s_production_queue ORDER BY id"))
            w.Countries[Convert.ToInt32(r["country_id"])].Queue.Add(new ProductionOrder { TemplateId = Convert.ToInt32(r["template_id"]), Progress = Convert.ToSingle(r["progress"]), Repeat = Convert.ToInt32(r["repeat_order"]) != 0, Factories = Math.Max(1, Convert.ToInt32(r["factories"])), Efficiency = MathF.Max(1f, Convert.ToSingle(r["efficiency"])), Delivered = Convert.ToInt32(r["delivered"]), UnitTypeId = Convert.ToInt32(r["unit_type_id"]) });
        // armazém de material: prateleira por tipo de unidade (Warehouse)
        foreach (var r in save.Query("SELECT country_id,unit_type_id,qty FROM s_stock"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var sc))
                sc.Stock[Convert.ToInt32(r["unit_type_id"])] = MathF.Max(0f, Convert.ToSingle(r["qty"]));
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
        foreach (var t in new[] { "s_region", "s_division", "s_war", "save_meta", "s_country", "s_country_tech", "s_focus", "s_production_queue", "s_battle", "s_battle_division", "template_unit", "template", "s_news_choice", "s_news_fired", "s_faction_member", "s_faction", "s_country_law", "s_spy_op", "s_intel", "s_pact", "s_trade_deal", "s_lend_lease", "s_history", "s_region_building", "s_decision", "s_general", "s_war_history", "s_war_goal", "s_division_medal", "s_division_kit", "s_stock", "s_army_group", "s_army_group_member", "s_chronicle", "s_prisoner", "s_offer", "s_research", "s_army_doctrine", "s_attache", "s_air_mission", "s_naval_mission", "s_occupation", "s_cabinet", "s_exile" })
            save.Execute("DELETE FROM " + t);
        save.Execute("INSERT INTO save_meta VALUES ('day',?)", w.Clock.Day);
        save.Execute("INSERT INTO save_meta VALUES ('saved_at',?)", DateTime.UtcNow.ToString("o"));
        if (w.Difficulty is string diff) save.Execute("INSERT INTO save_meta VALUES ('difficulty',?)", diff);
        foreach (var (eventId, optionId) in w.NewsChoices)
            save.Execute("INSERT INTO s_news_choice VALUES (?,?)", eventId, optionId);
        foreach (var (eventId, hit) in w.NewsFired)
            save.Execute("INSERT INTO s_news_fired (event_id,day,country_id) VALUES (?,?,?)", eventId, hit.Day, hit.CountryId);
        foreach (var c in w.Countries.Values)
            foreach (var g in c.Generals)
                save.Execute("INSERT INTO s_general (country_id,general,xp,wound_until,wound_kind) VALUES (?,?,?,?,?)",
                    c.Id, g, c.GeneralXp.GetValueOrDefault(g), c.GeneralWound.GetValueOrDefault(g),
                    c.GeneralWoundKind.GetValueOrDefault(g, ""));
        foreach (var c in w.Countries.Values)
            foreach (var (slot, advisor) in c.Cabinet)
                save.Execute("INSERT INTO s_cabinet (country_id,slot,advisor,since_day) VALUES (?,?,?,?)",
                    c.Id, slot, advisor, c.CabinetSince.GetValueOrDefault(slot));
        foreach (var c in w.Countries.Values)
            foreach (var (from, men) in c.Prisoners)
                save.Execute("INSERT INTO s_prisoner (country_id,from_country_id,men) VALUES (?,?,?)", c.Id, from, men);
        foreach (var c in w.Countries.Values)
            if (c.ExileHostId is int exileHost)
                save.Execute("INSERT INTO s_exile (country_id,host_id,since_day,legitimacy) VALUES (?,?,?,?)",
                    c.Id, exileHost, c.ExileDay ?? 0, c.ExileLegitimacy);
        foreach (var c in w.Countries.Values)
            foreach (var (did, cd) in c.DecisionCooldownUntil)
                save.Execute("INSERT INTO s_decision (country_id,decision,until_day,cooldown_until) VALUES (?,?,?,?)",
                    c.Id, did, w.ActiveDecisions.FirstOrDefault(a => a.CountryId == c.Id && a.DecisionId == did)?.UntilDay ?? -1, cd);
        foreach (var h in w.History)
            save.Execute("INSERT INTO s_history (day,country_id,money,divisions,regions,power) VALUES (?,?,?,?,?,?)",
                h.Day, h.CountryId, h.Money, h.Divisions, h.Regions, h.Power);
        for (int i = 0; i < w.Chronicle.Count; i++)
        {
            var e = w.Chronicle[i];
            save.Execute("INSERT INTO s_chronicle (ord,day,kind,text,country_id,region_id) VALUES (?,?,?,?,?,?)",
                i, e.Day, e.Kind, e.Text, e.CountryId, e.RegionId);
        }
        foreach (var d in w.TradeDeals)
            save.Execute("INSERT INTO s_trade_deal (buyer_id,seller_id,resource,units,price_per_unit,until_day) VALUES (?,?,?,?,?,?)",
                d.BuyerId, d.SellerId, d.ResourceId, d.Units, d.PricePerUnit, d.UntilDay);
        foreach (var l in w.LendLeases)
            save.Execute("INSERT INTO s_lend_lease (from_id,to_id,share,since_day,sent_total) VALUES (?,?,?,?,?)",
                l.FromId, l.ToId, l.Share, l.SinceDay, l.SentTotal);
        foreach (var o in w.ActiveSpyOps)
            save.Execute("INSERT INTO s_spy_op (country_id,target_id,op_id,days_left,region_id) VALUES (?,?,?,?,?)",
                o.CountryId, o.TargetCountryId, o.OpId, o.DaysLeft, o.RegionId);
        foreach (var o in w.Offers)
            save.Execute("INSERT INTO s_offer (from_id,to_id,kind,men,region_id,day,expires_day) VALUES (?,?,?,?,?,?,?)",
                o.FromId, o.ToId, o.Kind, o.Men, o.RegionId, o.Day, o.ExpiresDay);
        foreach (var ((ia, ib), until) in w.Intel)
            if (until >= w.Clock.Day) save.Execute("INSERT INTO s_intel VALUES (?,?,?)", ia, ib, until);
        foreach (var ((pa, pb), until) in w.Pacts)
            if (until >= w.Clock.Day) save.Execute("INSERT INTO s_pact VALUES (?,?,?)", pa, pb, until);
        foreach (var m in w.AirMissions)
            save.Execute("INSERT INTO s_air_mission (country_id,region_id,mission_id,wings,since_day,name) VALUES (?,?,?,?,?,?)",
                m.CountryId, m.RegionId, m.MissionId, m.Wings, m.SinceDay, m.Name);
        foreach (var m in w.NavalMissions)
            save.Execute("INSERT INTO s_naval_mission (country_id,region_id,mission_id,ships,since_day,name) VALUES (?,?,?,?,?,?)",
                m.CountryId, m.RegionId, m.MissionId, m.Ships, m.SinceDay, m.Name);
        foreach (var o in w.Occupations)
            save.Execute("INSERT INTO s_occupation (country_id,target_id,policy_id,since_day) VALUES (?,?,?,?)",
                o.CountryId, o.TargetId, o.PolicyId, o.SinceDay);
        foreach (var a in w.Attaches.Values)
            save.Execute("INSERT INTO s_attache (country_id,host_id,since_day,learned) VALUES (?,?,?,?)",
                a.CountryId, a.HostId, a.SinceDay, a.Learned);
        foreach (var id in w.CustomFactionIds)
        {
            var f = w.Factions[id];
            save.Execute("INSERT INTO s_faction VALUES (?,?,?)", f.Id, f.Name, f.Description);
        }
        foreach (var f in w.Factions.Values)
            foreach (var m in f.Members)
                save.Execute("INSERT INTO s_faction_member VALUES (?,?)", f.Id, m);
        foreach (var id in w.CustomTemplateIds)
        {
            var t = w.Units.GetTemplate(id);
            save.Execute("INSERT INTO template (id,country_id,name) VALUES (?,?,?)", t.Id, t.CountryId, t.Name);
            foreach (var (unitId, qty) in t.Units)
                save.Execute("INSERT INTO template_unit VALUES (?,?,?)", t.Id, unitId, qty);
        }
        foreach (var c in w.Countries.Values)
        {
            // países sem estado nenhum não gastam linha; o lugar na tabela mundial não os obriga a ter uma
            // (o PowerRankingSystem refá-la em power_rank_days), mas o do jogador vai sempre com o save
            if (c.IsPlayer || c.Money != 0f || c.ResearchTech is not null || c.Capitulated || c.CurrentFocus is not null || c.JustifyTarget is not null || c.ArmyXp > 0f || c.Convoys != 0f || c.LastDefeatDay >= 0 || c.IsSubject)
                save.Execute("INSERT INTO s_country (id,is_player,money,political,research_tech,research_progress,capitulated,capitulated_day,manpower,focus,focus_progress,stability,justify_target,justify_progress,war_exhaustion,air_power,warships,convoys,nukes,power_rank,power_rank_prev,army_xp,air_xp,navy_xp,defeat_streak,last_defeat_day,last_defeat_region,fuel,overlord,autonomy) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    c.Id, c.IsPlayer ? 1 : 0, c.Money, c.Political, c.ResearchTech, c.ResearchProgress, c.Capitulated ? 1 : 0, c.CapitulatedDay, c.Manpower, c.CurrentFocus, c.FocusProgress, c.Stability, c.JustifyTarget, c.JustifyProgress, c.WarExhaustion, c.AirPower, c.Warships, c.Convoys, c.Nukes, c.PowerRank, c.PowerRankPrev, c.ArmyXp, c.AirXp, c.NavyXp, c.DefeatStreak, c.LastDefeatDay, c.LastDefeatRegion, c.Fuel, c.OverlordId, c.Autonomy);
            // as ranhuras vão todas para a tabela própria; as colunas antigas de s_country guardam a
            // primeira, para um save novo ainda abrir num binário anterior às ranhuras
            foreach (var (techId, progress) in c.Research)
                save.Execute("INSERT INTO s_research (country_id,tech_id,progress) VALUES (?,?,?)", c.Id, techId, progress);
            foreach (var t in c.Techs) save.Execute("INSERT INTO s_country_tech VALUES (?,?)", c.Id, t);
            foreach (var (grp, lawId) in c.Laws) save.Execute("INSERT INTO s_country_law VALUES (?,?,?)", c.Id, grp, lawId);
            foreach (var f in c.FocusesDone) save.Execute("INSERT INTO s_focus VALUES (?,?)", c.Id, f);
            foreach (var d in c.Doctrines) save.Execute("INSERT INTO s_army_doctrine VALUES (?,?)", c.Id, d);
            foreach (var o in c.Queue) save.Execute("INSERT INTO s_production_queue (country_id,template_id,progress,repeat_order,factories,efficiency,delivered,unit_type_id) VALUES (?,?,?,?,?,?,?,?)", c.Id, o.TemplateId, o.Progress, o.Repeat ? 1 : 0, o.Factories, o.Efficiency, o.Delivered, o.UnitTypeId);
            foreach (var (type, qty) in c.Stock)
                if (qty > 1e-4f) save.Execute("INSERT INTO s_stock (country_id,unit_type_id,qty) VALUES (?,?,?)", c.Id, type, qty);
            foreach (var e in c.AtWarWith)
                if (c.Id < e)
                {
                    var info = w.Wars.GetValueOrDefault(World.WarKey(c.Id, e));
                    save.Execute("INSERT INTO s_war (a,b,since_day,last_progress_day,a_regions,b_regions,a_losses,b_losses,a_battles,b_battles) VALUES (?,?,?,?,?,?,?,?,?,?)",
                        c.Id, e, info?.StartDay ?? w.Clock.Day, info?.LastProgressDay ?? w.Clock.Day,
                        info?.SideA.RegionsTaken ?? 0, info?.SideB.RegionsTaken ?? 0,
                        info?.SideA.DivisionsLost ?? 0, info?.SideB.DivisionsLost ?? 0,
                        info?.SideA.BattlesWon ?? 0, info?.SideB.BattlesWon ?? 0);
                }
        }
        foreach (var (key, war) in w.Wars)
            foreach (var (side, owner) in new[] { (war.SideA, war.A), (war.SideB, war.B) })
                foreach (int regionId in side.Goals)
                    save.Execute("INSERT INTO s_war_goal (a,b,country_id,region_id) VALUES (?,?,?,?)", key.A, key.B, owner, regionId);
        foreach (var rec in w.WarHistory)   // pela ordem da lista (mais recente primeiro); o load lê por id e mantém-na
            save.Execute("INSERT INTO s_war_history (a,b,start_day,end_day,a_regions,b_regions,a_losses,b_losses,a_battles,b_battles) VALUES (?,?,?,?,?,?,?,?,?,?)",
                rec.A, rec.B, rec.StartDay, rec.EndDay, rec.ARegions, rec.BRegions, rec.ALosses, rec.BLosses, rec.ABattles, rec.BBattles);
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId != r.OwnerId || r.Infrastructure != 1f || r.OwnerId != r.InitialOwnerId || r.Building || r.Fort > 0 || r.FortBuilding || r.Resistance > 0f || r.Project is not null || r.Integration > 0f
                || r.Rail != r.BaseRail || r.RailBuilding)
                save.Execute("INSERT INTO s_region (id,controller_id,infrastructure,owner_id,building,build_progress,fort,fort_building,fort_progress,resistance,project,project_progress,integration,rail,rail_building,rail_progress,project_owner) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    r.Id, r.ControllerId, r.Infrastructure, r.OwnerId == r.InitialOwnerId ? null : r.OwnerId, r.Building ? 1 : 0, r.BuildProgress,
                    r.Fort, r.FortBuilding ? 1 : 0, r.FortProgress, r.Resistance, r.Project, r.ProjectProgress, r.Integration,
                    r.Rail, r.RailBuilding ? 1 : 0, r.RailProgress, r.ProjectOwner);
            foreach (var (bid, lvl) in r.Buildings)
                if (lvl > 0) save.Execute("INSERT INTO s_region_building (region_id,building,level) VALUES (?,?,?)", r.Id, bid, lvl);
        }
        foreach (var d in w.Divisions.Values)
        {
            // colunas nomeadas: a tabela cresce por migração e um INSERT posicional partia-se à coluna seguinte
            save.Execute("INSERT INTO s_division (id,country_id,template_id,region_id,target_region_id,hp,org,supply,move_progress,path,name,xp,auto_advance,battles,captures,honour,honour_name,entrench,pocket_days,volunteer_from,drop_target,drop_days,redeploy)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", d.Id, d.CountryId, d.TemplateId, d.RegionId, d.TargetRegionId,
                d.Hp, d.Org, d.Supply, d.MoveProgress, d.Path.Count == 0 ? null : string.Join(',', d.Path), d.Name, d.Xp, d.AutoAdvance ? 1 : 0,
                d.Battles, d.Captures, d.Honour, d.HonourName, d.Entrench, d.PocketDays, d.VolunteerFrom, d.DropTargetId, d.DropDays, d.Redeploying ? 1 : 0);
            foreach (var medal in d.Medals)
                save.Execute("INSERT INTO s_division_medal VALUES (?,?)", d.Id, medal);
            if (d.Kit < 1f) save.Execute("INSERT INTO s_division_kit (division_id,kit) VALUES (?,?)", d.Id, d.Kit);
        }
        foreach (var g in w.ArmyGroups.Values)
        {
            save.Execute("INSERT INTO s_army_group (id,country_id,name,front_country_id,front_region_id,advancing,stance,general,planning) VALUES (?,?,?,?,?,?,?,?,?)",
                g.Id, g.CountryId, g.Name, g.FrontCountryId, g.FrontRegionId, g.Advancing ? 1 : 0, (int)g.Stance, g.GeneralId, g.Planning);
            foreach (int id in g.Divisions)
                save.Execute("INSERT INTO s_army_group_member (group_id,division_id) VALUES (?,?)", g.Id, id);
        }
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
