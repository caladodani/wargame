using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Tests;

/// <summary>Mundo pequeno para testes de sistemas: DB em memória com schema + seed (regras, terrenos, unidades)
/// e templates fixos; regiões/países criados à mão.</summary>
public static class TestWorld
{
    public const int Inf = 1, Armor = 2, InfAt = 3;          // templates do país 1
    public const int Inf2 = 11, Armor2 = 12;                 // templates do país 2

    public static (World w, MsSqliteDatabase db) Build(int seed = 0)
    {
        var db = new MsSqliteDatabase();
        db.ExecuteScript(File.ReadAllText("data/schema.sql"));
        db.ExecuteScript(File.ReadAllText("data/seed_units.sql"));
        db.ExecuteScript(File.ReadAllText("data/seed_tech.sql"));
        db.ExecuteScript(File.ReadAllText("data/seed_world.sql"));
        db.ExecuteScript(@"
            INSERT INTO template VALUES (1,1,'Inf'),(2,1,'Blind'),(3,1,'Inf+AT'),(11,2,'Inf'),(12,2,'Blind');
            INSERT INTO template_unit VALUES (1,1,6),(1,4,2), (2,3,4),(2,2,3),(2,4,1), (3,1,5),(3,4,1),(3,6,2),
                                             (11,1,6),(11,4,2), (12,3,4),(12,2,3),(12,4,1);");
        var units = new SqlUnitRepository(db);
        var w = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units), new ModifierEngine(units.GetModifiers()), seed);
        new SqlWorldRepository(db).LoadStatic(w);   // só regras + move_cost (country/region estão vazias)
        // Tempo neutro por omissão: quem testa marcha ou recomposição conta dias, e uma estação carregada
        // mudava-os por baixo do teste. Quem quer tempo põe-no à mão com TestWorld.Season(w, "inverno").
        w.SeasonMonths.Clear();
        // Pela mesma razão, céu limpo: o tempo local sorteia chuva por região e por semana, e um teste de
        // marcha não pode ter a estrada a mudar-lhe debaixo dos pés. Quem quer céu chama TestWorld.Sky(w).
        foreach (var kv in w.WeatherDefs) Skies[kv.Key] = kv.Value;
        w.WeatherDefs.Clear();
        // E pela mesma razão sem tácticas: elas multiplicam a força dos dois lados e mudam de quatro em
        // quatro dias, e um teste de combate que conta baixas exactas não pode ter isso por baixo. Quem
        // quer tácticas chama TestWorld.Tactic(w).
        foreach (var kv in w.TacticDefs) Plans[kv.Key] = kv.Value;
        w.TacticDefs.Clear();
        return (w, db);
    }

    /// <summary>Os céus da tabela weather, guardados na primeira construção. São registos imutáveis e iguais
    /// em todas as construções — o que se guarda aqui é a tabela, não o estado de um mundo.</summary>
    private static readonly Dictionary<string, WeatherDef> Skies = new();

    /// <summary>Devolve o tempo local ao mundo de teste (o Build limpa-o, como às estações).</summary>
    public static void Sky(World w)
    {
        foreach (var kv in Skies) w.WeatherDefs[kv.Key] = kv.Value;
    }

    /// <summary>As tácticas da tabela tactic, guardadas na primeira construção, pela mesma razão dos céus.</summary>
    private static readonly Dictionary<string, TacticDef> Plans = new();

    /// <summary>Devolve as tácticas de combate ao mundo de teste (o Build limpa-as, como aos céus).</summary>
    public static void Tactic(World w)
    {
        foreach (var kv in Plans) w.TacticDefs[kv.Key] = kv.Value;
    }

    /// <summary>Põe o mundo dentro de uma estação: manda todos os meses para ela, para o calendário do teste
    /// não interessar. Sem isto o mundo de teste anda com tempo neutro.</summary>
    public static void Season(World w, string seasonId)
    {
        for (int m = 1; m <= 12; m++) w.SeasonMonths[m] = seasonId;
    }

    /// <summary>Mapa em linha 1-2-…-n. Regiões 1..split são do país 1 (capital 1), o resto do país 2 (capital n).
    /// População 10M por região (→ 1 ponto/dia com points_per_million=0.1).</summary>
    public static void LinearMap(World w, int n = 6, int split = 3, string terrain = "plain", int population = 10_000_000)
    {
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = n, Manpower = 1e9f };
        for (int i = 1; i <= n; i++)
        {
            int owner = i <= split ? 1 : 2;
            var r = new Region { Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner, Terrain = terrain, Population = population, CenterX = i * 100, CenterY = 0 };
            if (i > 1) r.Neighbours.Add(i - 1);
            if (i < n) r.Neighbours.Add(i + 1);
            w.Regions[i] = r;
        }
    }

    public static Division AddDivision(World w, int id, int country, int template, int region, float org = 100f, float hp = 100f) =>
        w.AddDivision(new Division { Id = id, CountryId = country, TemplateId = template, RegionId = region, Org = org, Hp = hp });

    public static void Days(World w, int n) { for (int i = 0; i < n; i++) w.Tick(); }
}
