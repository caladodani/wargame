using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Tempo local: a estação manda no ano e no mundo inteiro, o céu manda na semana e na região.
/// Nos trópicos nunca neva, no círculo polar em Janeiro nem sempre se voa, a sul as estações andam
/// trocadas e a areia só se levanta no deserto. O sorteio é por célula do mapa — o mau tempo vem em
/// frentes largas — e é determinista: o mesmo dia no mesmo mundo dá sempre o mesmo céu.</summary>
public class WeatherTests
{
    /// <summary>Mapa em linha com latitude à escolha. `spread` afasta as regiões: com 10 cabem todas na
    /// mesma célula de tempo, com 3000 cada uma apanha a sua.</summary>
    private static World Map(float lat, string terrain = "plain", int n = 6, float spread = 3000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.Sky(w);
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f, Money = 1e6f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = n, Manpower = 1e9f, Money = 1e6f };
        for (int i = 1; i <= n; i++)
        {
            int owner = i <= n / 2 ? 1 : 2;
            var r = new Region
            {
                Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner,
                Terrain = terrain, Population = 10_000_000, CenterX = i * spread, CenterY = 0, Lat = lat,
            };
            if (i > 1) r.Neighbours.Add(i - 1);
            if (i < n) r.Neighbours.Add(i + 1);
            w.Regions[i] = r;
        }
        return w;
    }

    /// <summary>Todos os céus que este mundo viu, em todas as regiões, ao longo de tantos dias.</summary>
    private static HashSet<string> Over(World w, int days)
    {
        var seen = new HashSet<string>();
        for (int d = 0; d < days; d++)
        {
            foreach (var r in w.Regions.Values) seen.Add(Weather.Id(w, r));
            w.Tick();
        }
        return seen;
    }

    /// <summary>Anda com o calendário até àquele céu cair naquela região; devolve o dia, ou -1 se não caiu.</summary>
    private static int Wait(World w, int regionId, string sky, int max = 600)
    {
        for (int d = 0; d < max; d++)
        {
            if (Weather.Id(w, w.Regions[regionId]) == sky) return d;
            w.Tick();
        }
        return -1;
    }

    [Fact]
    public void TheSkiesComeFromTheDatabase()
    {
        var w = Map(0f);
        foreach (string id in new[] { "limpo", "chuva", "tempestade", "neve", "nevao", "areia" })
            Assert.True(w.WeatherDefs.ContainsKey(id), $"falta o céu {id}");
        Assert.Equal(1f, w.WeatherDefs["limpo"].MoveMult, 3);
        Assert.True(w.WeatherDefs["nevao"].AirMult < w.WeatherDefs["chuva"].AirMult);
        Assert.Equal(5f, w.Rule("weather_days", 0f), 3);
        Assert.Equal(900f, w.Rule("weather_cell", 0f), 3);
        Assert.Equal(0.35f, w.Rule("weather_cold_floor", 0f), 3);
    }

    [Fact]
    public void TheWeatherComesInWideFrontsAndHoldsForDays()
    {
        var tight = Map(20f, spread: 10f);                      // tudo dentro da mesma célula
        Assert.Equal(Weather.Id(tight, tight.Regions[1]), Weather.Id(tight, tight.Regions[6]));

        string today = Weather.Id(tight, tight.Regions[1]);
        TestWorld.Days(tight, 1);
        Assert.Equal(today, Weather.Id(tight, tight.Regions[1]));   // o bloco dura weather_days

        // e ao fim de umas semanas o céu já mudou: não é um sorteio que fica preso
        var seen = new HashSet<string>();
        for (int d = 0; d < 120; d++) { seen.Add(Weather.Id(tight, tight.Regions[1])); tight.Tick(); }
        Assert.True(seen.Count > 1, "o céu nunca mudou em 120 dias");
    }

    [Fact]
    public void FarApartTheSkyIsNotTheSame()
    {
        var w = Map(20f, spread: 3000f);
        int apart = 0;
        for (int d = 0; d < 60; d++)
        {
            if (Weather.Id(w, w.Regions[1]) != Weather.Id(w, w.Regions[6])) apart++;
            w.Tick();
        }
        Assert.True(apart > 0, "duas pontas do mapa com o mesmo céu 60 dias seguidos");
    }

    [Fact]
    public void ItNeverSnowsInTheTropics()
    {
        var w = Map(0f);
        Assert.Equal(0f, Weather.Cold(w, w.Regions[1]), 3);
        var seen = Over(w, 400);
        Assert.DoesNotContain("neve", seen);
        Assert.DoesNotContain("nevao", seen);
        Assert.Contains("chuva", seen);
    }

    [Fact]
    public void AndAtThePoleInWinterItSnowsAndNothingElse()
    {
        var w = Map(70f);
        TestWorld.Season(w, "inverno");
        Assert.Equal(1f, Weather.Cold(w, w.Regions[1]), 3);
        var seen = Over(w, 400);
        Assert.True(seen.Contains("neve") || seen.Contains("nevao"), "nunca nevou no círculo polar em Janeiro");
        Assert.DoesNotContain("chuva", seen);
        Assert.DoesNotContain("areia", seen);
    }

    [Fact]
    public void SouthOfTheEquatorTheSeasonsAreUpsideDown()
    {
        var north = Map(60f);
        var south = Map(-60f);
        TestWorld.Season(north, "inverno"); TestWorld.Season(south, "inverno");
        Assert.True(Weather.Cold(south, south.Regions[1]) < Weather.Cold(north, north.Regions[1]),
            "Janeiro na Patagónia estava tão frio como na Carélia");

        TestWorld.Season(north, "verao"); TestWorld.Season(south, "verao");
        Assert.True(Weather.Cold(south, south.Regions[1]) > Weather.Cold(north, north.Regions[1]),
            "Julho na Patagónia não estava mais frio do que na Carélia");
    }

    [Fact]
    public void SandOnlyBlowsWhereThereIsSand()
    {
        Assert.Contains("areia", Over(Map(15f, "desert"), 400));
        Assert.DoesNotContain("areia", Over(Map(15f, "plain"), 400));
    }

    [Fact]
    public void TheRainSlowsTheColumn()
    {
        var w = Map(0f);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        Assert.True(Wait(w, 2, "limpo") >= 0, "nunca houve céu limpo");
        float clear = MovementSystem.HopDays(w, d, w.Regions[1], w.Regions[2]);
        Assert.True(Wait(w, 2, "chuva") >= 0, "nunca choveu");
        float rain = MovementSystem.HopDays(w, d, w.Regions[1], w.Regions[2]);
        Assert.True(rain > clear, $"chuva {rain:0.000} dias, céu limpo {clear:0.000}");
        Assert.Equal(clear / w.WeatherDefs["chuva"].MoveMult, rain, 3);
    }

    [Fact]
    public void TheStormHitsTheAssaultAndLeavesTheGroundAlone()
    {
        var w = Map(30f);
        float clear = GroundSystem.Terrain(w, "plain", river: false, attacking: true);
        float storm = GroundSystem.Terrain(w, "plain", river: false, attacking: true, "tempestade");
        Assert.True(storm < clear, $"tempestade {storm:0.00} não custou nada a quem assalta ({clear:0.00})");
        Assert.Equal(clear * 0.80f, storm, 3);
        // e a quem espera não tira nem dá: a tabela só fala do assalto
        Assert.Equal(GroundSystem.Terrain(w, "plain", river: false, attacking: false),
                     GroundSystem.Terrain(w, "plain", river: false, attacking: false, "tempestade"), 3);
    }

    [Fact]
    public void TroopsTrainedForTheColdShrugOffTheBlizzard()
    {
        var (w, db) = TestWorld.Build();
        TestWorld.Sky(w);
        db.ExecuteScript(@"
            INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
                (93,'Caçadores árticos','ground',1.4,40,1.0,30);
            INSERT INTO unit_stat VALUES (93,'soft_atk',8),(93,'hard_atk',2),(93,'defense',22),(93,'breakthrough',8),
                                        (93,'armor',0),(93,'piercing',6),(93,'hardness',0.1),(93,'hp',22);
            INSERT INTO unit_tag VALUES (93,'infantry'),(93,'ground'),(93,'artico');
            INSERT INTO template VALUES (93,1,'Caçadores árticos');
            INSERT INTO template_unit VALUES (93,93,6);");
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1 };
        w.Regions[1] = new Region { Id = 1, Name = "Gelo", OwnerId = 1, InitialOwnerId = 1, ControllerId = 1, Terrain = "tundra", Lat = 75f };

        var ctx = CombatSystem.BuildContext(w, w.Regions[1], 1);
        ctx["weather"] = "nevao";
        float Str(int tid) { var (f, m) = w.Modifiers.Evaluate("str_attacker", w.Stats.Get(tid), ctx); return m + f; }
        Assert.True(Str(93) > Str(TestWorld.Inf),
            $"os árticos assaltaram o nevão a {Str(93):0.000} e a tropa da estrada a {Str(TestWorld.Inf):0.000}");
    }

    [Fact]
    public void NobodyRestsWellInASnowstorm()
    {
        var w = Map(70f);
        TestWorld.Season(w, "inverno");
        w.Register(new RecoverySystem());
        Assert.True(Wait(w, 1, "nevao") >= 0, "nunca houve nevão");
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, org: 50f);

        w.Tick();
        float snowed = d.Org - 50f;
        w.WeatherDefs.Clear();          // o mesmo dia, a mesma estação, sem tempo local nenhum
        d.Org = 50f;
        w.Tick();
        float sheltered = d.Org - 50f;
        Assert.True(snowed < sheltered, $"nevão +{snowed:0.000} org, sem tempo +{sheltered:0.000}");
    }

    [Fact]
    public void TheSkyClosesOverTheAirfields()
    {
        var w = Map(70f);
        TestWorld.Season(w, "inverno");
        Assert.True(Wait(w, 3, "nevao") >= 0, "nunca houve nevão");
        w.AirMissions.Add(new AirMission { CountryId = 1, RegionId = 3, MissionId = "apoio", Wings = 10f });
        float shut = AirMissionSystem.Support(w, 3, 1);
        w.WeatherDefs.Clear();
        float open = AirMissionSystem.Support(w, 3, 1);
        Assert.True(shut < open, $"nevão deu {shut:0.000} de apoio próximo e o céu limpo {open:0.000}");
        Assert.Equal(open * 0.20f, shut, 3);
    }
}
