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

        foreach (var r in _static.Query("SELECT id,name,description FROM faction"))
            w.Factions[(string)r["id"]!] = new Faction((string)r["id"]!, (string)r["name"]!, r["description"] as string ?? "", new List<int>());
        foreach (var r in _static.Query("SELECT faction_id,country_tag FROM faction_member"))
            if (w.Factions.TryGetValue((string)r["faction_id"]!, out var f) && byTag.TryGetValue((string)r["country_tag"]!, out var c))
                f.Members.Add(c.Id);

        foreach (var r in _static.Query("SELECT id,branch,name,cost,requires,description FROM tech"))
            w.Techs[(string)r["id"]!] = new Tech((string)r["id"]!, (string)r["branch"]!, (string)r["name"]!, Convert.ToSingle(r["cost"]), r["requires"] as string, r["description"] as string);
        foreach (var r in _static.Query("SELECT id,day,country_tag,title,body FROM news_event"))
        {
            int? cid = r["country_tag"] is string tag && byTag.TryGetValue(tag, out var nc) ? nc.Id : null;
            w.NewsEvents[(string)r["id"]!] = new NewsEvent((string)r["id"]!, Convert.ToInt32(r["day"]), cid, (string)r["title"]!, (string)r["body"]!);
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
        foreach (var r in _static.Query("SELECT id,grp,name,description,sort,is_default FROM law ORDER BY grp,sort"))
            w.Laws[(string)r["id"]!] = new Law((string)r["id"]!, (string)r["grp"]!, (string)r["name"]!,
                r["description"] as string ?? "", Convert.ToInt32(r["sort"]), Convert.ToInt32(r["is_default"]) == 1);
        foreach (var r in _static.Query("SELECT law_id,stat_key,value FROM law_effect"))
        {
            var lid = (string)r["law_id"]!;
            if (!w.LawEffects.TryGetValue(lid, out var llist)) w.LawEffects[lid] = llist = new();
            llist.Add(((string)r["stat_key"]!, Convert.ToSingle(r["value"])));
        }
        foreach (var r in _static.Query("SELECT id,name,stat_key,per_unit,cap FROM resource"))
            w.ResourceDefs[(string)r["id"]!] = new ResourceDef((string)r["id"]!, (string)r["name"]!,
                (string)r["stat_key"]!, Convert.ToSingle(r["per_unit"]), Convert.ToSingle(r["cap"]));
        foreach (var r in _static.Query("SELECT id,name,cost,days,stat_key,per_level,max_level FROM building"))
            w.BuildingDefs[(string)r["id"]!] = new BuildingDef((string)r["id"]!, (string)r["name"]!,
                Convert.ToSingle(r["cost"]), Convert.ToSingle(r["days"]), (string)r["stat_key"]!,
                Convert.ToSingle(r["per_level"]), Convert.ToInt32(r["max_level"]));
        foreach (var r in _static.Query("SELECT id,name,sort FROM difficulty ORDER BY sort"))
            w.DifficultyDefs[(string)r["id"]!] = new DifficultyDef((string)r["id"]!, (string)r["name"]!,
                Convert.ToInt32(r["sort"]), new Dictionary<string, float>());
        foreach (var r in _static.Query("SELECT difficulty_id,rule_key,value FROM difficulty_effect"))
            if (w.DifficultyDefs.TryGetValue((string)r["difficulty_id"]!, out var dd))
                dd.Effects[(string)r["rule_key"]!] = Convert.ToSingle(r["value"]);
        foreach (var r in _static.Query("SELECT id,name,stat_key,mult,cost FROM general"))
            w.GeneralDefs[(string)r["id"]!] = new GeneralDef((string)r["id"]!, (string)r["name"]!,
                (string)r["stat_key"]!, Convert.ToSingle(r["mult"]), Convert.ToSingle(r["cost"]));
        foreach (var r in _static.Query("SELECT id,name,cost,days,cooldown,stat_key,mult FROM decision"))
            w.DecisionDefs[(string)r["id"]!] = new DecisionDef((string)r["id"]!, (string)r["name"]!,
                Convert.ToSingle(r["cost"]), Convert.ToInt32(r["days"]), Convert.ToInt32(r["cooldown"]),
                (string)r["stat_key"]!, Convert.ToSingle(r["mult"]));
        foreach (var r in _static.Query("SELECT id,name,description,cost,days,effect,magnitude FROM spy_op"))
            w.SpyOps[(string)r["id"]!] = new SpyOp((string)r["id"]!, (string)r["name"]!, r["description"] as string ?? "",
                Convert.ToSingle(r["cost"]), Convert.ToInt32(r["days"]), (string)r["effect"]!, Convert.ToSingle(r["magnitude"]));
        foreach (var r in _static.Query("SELECT id,country_tag,name,description,days,requires,sort FROM focus"))
            if (byTag.TryGetValue((string)r["country_tag"]!, out var fc))
                w.Focuses[(string)r["id"]!] = new Focus((string)r["id"]!, fc.Id, (string)r["name"]!, (string)r["description"]!,
                    Convert.ToInt32(r["days"]), r["requires"] as string, Convert.ToInt32(r["sort"]));
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

        foreach (var r in _static.Query("SELECT id,name,owner_id,terrain,river,population,infrastructure,centroid_x,centroid_y,coastal FROM region"))
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
        foreach (var r in _static.Query("SELECT region_id,resource,amount FROM region_resource"))
            if (w.Regions.TryGetValue(Convert.ToInt32(r["region_id"]), out var rr))
                rr.Resources[(string)r["resource"]!] = Convert.ToSingle(r["amount"]);
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
        ("s_country", "war_exhaustion", "REAL NOT NULL DEFAULT 0"),
        ("s_division", "xp", "REAL NOT NULL DEFAULT 0"),
        ("s_division", "auto_advance", "INTEGER NOT NULL DEFAULT 0"),
        ("s_country", "air_power", "REAL NOT NULL DEFAULT 0"),
        ("s_country", "nukes", "INTEGER NOT NULL DEFAULT 0"),
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
        foreach (var r in save.Query("SELECT id,is_player,money,research_tech,research_progress,capitulated,capitulated_day,manpower,focus,focus_progress,stability,justify_target,justify_progress,war_exhaustion,air_power,nukes FROM s_country"))
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
            if (r["air_power"] is not null) c.AirPower = Convert.ToSingle(r["air_power"]);
            if (r["nukes"] is not null) c.Nukes = Convert.ToInt32(r["nukes"]);
        }
        foreach (var r in save.Query("SELECT country_id,tech_id FROM s_country_tech"))
            w.Countries[Convert.ToInt32(r["country_id"])].Techs.Add((string)r["tech_id"]!);
        foreach (var r in save.Query("SELECT country_id,focus_id FROM s_focus"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var cf)) cf.FocusesDone.Add((string)r["focus_id"]!);
        foreach (var r in save.Query("SELECT event_id,option_id FROM s_news_choice"))
            w.NewsChoices[(string)r["event_id"]!] = (string)r["option_id"]!;
        foreach (var r in save.Query("SELECT country_id,grp,law_id FROM s_country_law"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var cl)) cl.Laws[(string)r["grp"]!] = (string)r["law_id"]!;
        foreach (var r in save.Query("SELECT country_id,general FROM s_general"))
            if (w.Countries.TryGetValue(Convert.ToInt32(r["country_id"]), out var gc)) gc.Generals.Add((string)r["general"]!);
        foreach (var c in w.Countries.Values) World.ApplyGenerals(w, c);
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
        foreach (var r in save.Query("SELECT day,country_id,money,divisions,regions FROM s_history ORDER BY day"))
            w.History.Add(new HistorySample(Convert.ToInt32(r["day"]), Convert.ToInt32(r["country_id"]),
                Convert.ToSingle(r["money"]), Convert.ToInt32(r["divisions"]), Convert.ToInt32(r["regions"])));
        foreach (var r in save.Query("SELECT buyer_id,seller_id,resource,units FROM s_trade_deal"))
            w.TradeDeals.Add(new TradeDeal { BuyerId = Convert.ToInt32(r["buyer_id"]), SellerId = Convert.ToInt32(r["seller_id"]),
                ResourceId = (string)r["resource"]!, Units = Convert.ToSingle(r["units"]) });
        foreach (var r in save.Query("SELECT country_id,target_id,op_id,days_left FROM s_spy_op"))
            w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = Convert.ToInt32(r["country_id"]), TargetCountryId = Convert.ToInt32(r["target_id"]),
                OpId = (string)r["op_id"]!, DaysLeft = Convert.ToSingle(r["days_left"]) });
        foreach (var r in save.Query("SELECT country_id,target_id,until_day FROM s_intel"))
            w.Intel[(Convert.ToInt32(r["country_id"]), Convert.ToInt32(r["target_id"]))] = Convert.ToInt32(r["until_day"]);
        foreach (var r in save.Query("SELECT a,b,until_day FROM s_pact"))
            w.Pacts[(Convert.ToInt32(r["a"]), Convert.ToInt32(r["b"]))] = Convert.ToInt32(r["until_day"]);
        foreach (var c in w.Countries.Values) w.ApplyTechs(c);
        foreach (var r in save.Query("SELECT id,controller_id,infrastructure,owner_id,building,build_progress,fort,fort_building,fort_progress,resistance,project,project_progress,integration FROM s_region"))
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
        }
        foreach (var r in save.Query("SELECT id,country_id,template_id,region_id,hp,org,supply,move_progress,path,name,xp,auto_advance FROM s_division ORDER BY id"))
        {
            var d = new Division
            {
                Id = Convert.ToInt32(r["id"]), CountryId = Convert.ToInt32(r["country_id"]), TemplateId = Convert.ToInt32(r["template_id"]),
                RegionId = Convert.ToInt32(r["region_id"]), Name = r["name"] as string,
                Hp = Convert.ToSingle(r["hp"]), Org = Convert.ToSingle(r["org"]), Supply = Convert.ToSingle(r["supply"]),
            };
            if (r["xp"] is not null) d.Xp = Convert.ToSingle(r["xp"]);
            if (r["auto_advance"] is not null) d.AutoAdvance = Convert.ToInt32(r["auto_advance"]) != 0;
            if (r["path"] is string p && p.Length > 0) d.SetPath(p.Split(',').Select(int.Parse));
            d.MoveProgress = Convert.ToSingle(r["move_progress"]);
            w.AddDivision(d);
        }
        foreach (var r in save.Query("SELECT a,b,since_day,last_progress_day FROM s_war"))
        {
            int a = Convert.ToInt32(r["a"]), b = Convert.ToInt32(r["b"]);
            w.StartWar(a, b, Convert.ToInt32(r["since_day"]));
            w.Wars[World.WarKey(a, b)].LastProgressDay = r["last_progress_day"] is null ? Convert.ToInt32(r["since_day"]) : Convert.ToInt32(r["last_progress_day"]);
        }
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
        foreach (var t in new[] { "s_region", "s_division", "s_war", "save_meta", "s_country", "s_country_tech", "s_focus", "s_production_queue", "s_battle", "s_battle_division", "template_unit", "template", "s_news_choice", "s_faction_member", "s_faction", "s_country_law", "s_spy_op", "s_intel", "s_pact", "s_trade_deal", "s_history", "s_region_building", "s_decision", "s_general" })
            save.Execute("DELETE FROM " + t);
        save.Execute("INSERT INTO save_meta VALUES ('day',?)", w.Clock.Day);
        save.Execute("INSERT INTO save_meta VALUES ('saved_at',?)", DateTime.UtcNow.ToString("o"));
        if (w.Difficulty is string diff) save.Execute("INSERT INTO save_meta VALUES ('difficulty',?)", diff);
        foreach (var (eventId, optionId) in w.NewsChoices)
            save.Execute("INSERT INTO s_news_choice VALUES (?,?)", eventId, optionId);
        foreach (var c in w.Countries.Values)
            foreach (var g in c.Generals)
                save.Execute("INSERT INTO s_general (country_id,general) VALUES (?,?)", c.Id, g);
        foreach (var c in w.Countries.Values)
            foreach (var (did, cd) in c.DecisionCooldownUntil)
                save.Execute("INSERT INTO s_decision (country_id,decision,until_day,cooldown_until) VALUES (?,?,?,?)",
                    c.Id, did, w.ActiveDecisions.FirstOrDefault(a => a.CountryId == c.Id && a.DecisionId == did)?.UntilDay ?? -1, cd);
        foreach (var h in w.History)
            save.Execute("INSERT INTO s_history VALUES (?,?,?,?,?)", h.Day, h.CountryId, h.Money, h.Divisions, h.Regions);
        foreach (var d in w.TradeDeals)
            save.Execute("INSERT INTO s_trade_deal VALUES (?,?,?,?)", d.BuyerId, d.SellerId, d.ResourceId, d.Units);
        foreach (var o in w.ActiveSpyOps)
            save.Execute("INSERT INTO s_spy_op VALUES (?,?,?,?)", o.CountryId, o.TargetCountryId, o.OpId, o.DaysLeft);
        foreach (var ((ia, ib), until) in w.Intel)
            if (until >= w.Clock.Day) save.Execute("INSERT INTO s_intel VALUES (?,?,?)", ia, ib, until);
        foreach (var ((pa, pb), until) in w.Pacts)
            if (until >= w.Clock.Day) save.Execute("INSERT INTO s_pact VALUES (?,?,?)", pa, pb, until);
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
            if (c.IsPlayer || c.Money != 0f || c.ResearchTech is not null || c.Capitulated || c.CurrentFocus is not null || c.JustifyTarget is not null)
                save.Execute("INSERT INTO s_country (id,is_player,money,research_tech,research_progress,capitulated,capitulated_day,manpower,focus,focus_progress,stability,justify_target,justify_progress,war_exhaustion,air_power,nukes) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    c.Id, c.IsPlayer ? 1 : 0, c.Money, c.ResearchTech, c.ResearchProgress, c.Capitulated ? 1 : 0, c.CapitulatedDay, c.Manpower, c.CurrentFocus, c.FocusProgress, c.Stability, c.JustifyTarget, c.JustifyProgress, c.WarExhaustion, c.AirPower, c.Nukes);
            foreach (var t in c.Techs) save.Execute("INSERT INTO s_country_tech VALUES (?,?)", c.Id, t);
            foreach (var (grp, lawId) in c.Laws) save.Execute("INSERT INTO s_country_law VALUES (?,?,?)", c.Id, grp, lawId);
            foreach (var f in c.FocusesDone) save.Execute("INSERT INTO s_focus VALUES (?,?)", c.Id, f);
            foreach (var o in c.Queue) save.Execute("INSERT INTO s_production_queue (country_id,template_id,progress) VALUES (?,?,?)", c.Id, o.TemplateId, o.Progress);
            foreach (var e in c.AtWarWith)
                if (c.Id < e)
                {
                    var info = w.Wars.GetValueOrDefault(World.WarKey(c.Id, e));
                    save.Execute("INSERT INTO s_war (a,b,since_day,last_progress_day) VALUES (?,?,?,?)",
                        c.Id, e, info?.StartDay ?? w.Clock.Day, info?.LastProgressDay ?? w.Clock.Day);
                }
        }
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId != r.OwnerId || r.Infrastructure != 1f || r.OwnerId != r.InitialOwnerId || r.Building || r.Fort > 0 || r.FortBuilding || r.Resistance > 0f || r.Project is not null || r.Integration > 0f)
                save.Execute("INSERT INTO s_region (id,controller_id,infrastructure,owner_id,building,build_progress,fort,fort_building,fort_progress,resistance,project,project_progress,integration) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    r.Id, r.ControllerId, r.Infrastructure, r.OwnerId == r.InitialOwnerId ? null : r.OwnerId, r.Building ? 1 : 0, r.BuildProgress,
                    r.Fort, r.FortBuilding ? 1 : 0, r.FortProgress, r.Resistance, r.Project, r.ProjectProgress, r.Integration);
            foreach (var (bid, lvl) in r.Buildings)
                if (lvl > 0) save.Execute("INSERT INTO s_region_building (region_id,building,level) VALUES (?,?,?)", r.Id, bid, lvl);
        }
        foreach (var d in w.Divisions.Values)
            save.Execute("INSERT INTO s_division VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)", d.Id, d.CountryId, d.TemplateId, d.RegionId, d.TargetRegionId,
                d.Hp, d.Org, d.Supply, d.MoveProgress, d.Path.Count == 0 ? null : string.Join(',', d.Path), d.Name, d.Xp, d.AutoAdvance ? 1 : 0);
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
