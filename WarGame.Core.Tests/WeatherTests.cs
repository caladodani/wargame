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




}
