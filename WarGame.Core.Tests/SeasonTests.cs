using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Estações do ano: marcha, recomposição e desgaste. O mundo de teste anda com tempo neutro, por
/// isso cada teste põe a estação que quer (TestWorld.Season). Os números vêm do seed — o que se mede é a
/// direcção: no Inverno anda-se pior, refaz-se pior e gasta-se em campo aberto.</summary>
public class SeasonTests
{
    private static World Setup(string? season = null)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        if (season is not null) TestWorld.Season(w, season);
        return w;
    }

    [Fact]
    public void TheCalendarComesFromTheDatabase()
    {
        var w = Setup();
        Assert.True(w.SeasonDefs.Count >= 4);
        var winter = w.SeasonDefs["inverno"];
        Assert.True(winter.MoveMult < 1f);
        Assert.True(winter.Attrition > 0f);
        Assert.NotEqual("", winter.Icon);
    }

    [Fact]
    public void WithoutSeasons_NothingChanges()
    {
        var w = Setup();
        Assert.Null(w.Season);
        Assert.Equal(1f, w.SeasonMove);
        Assert.Equal(1f, w.SeasonOrg);
        Assert.Equal(0f, WeatherSystem.BiteOn(w, "tundra"));
    }

    [Fact]
    public void TheSeasonComesFromTheMonth()
    {
        var (w, _) = TestWorld.Build();          // arranca a 1 de Janeiro
        TestWorld.LinearMap(w);
        for (int m = 1; m <= 12; m++) w.SeasonMonths[m] = m is >= 6 and <= 8 ? "verao" : "inverno";

        Assert.Equal("inverno", w.Season!.Id);
        TestWorld.Days(w, 160);                  // meados de Junho
        Assert.Equal("verao", w.Season!.Id);
    }

    [Fact]
    public void WinterSlowsTheColumns_SummerSpeedsThemUp()
    {
        int Hops(string season)
        {
            var w = Setup(season);
            var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
            d.SetPath(new[] { 2 });
            w.Register(new MovementSystem());
            int days = 0;
            while (d.RegionId != 2 && days < 400) { w.Tick(); days++; }
            return days;
        }

        int winter = Hops("inverno"), summer = Hops("verao");
        Assert.True(winter > summer, $"inverno {winter} dias, verão {summer}");
    }

    [Fact]
    public void WinterEatsOrganisationInTheOpen()
    {
        var w = Setup("inverno");
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 5);   // nossa, em terreno inimigo: sem telhado
        d.Org = 100f;
        w.Register(new WeatherSystem());
        w.Tick();

        float bite = w.SeasonDefs["inverno"].Attrition * w.SeasonBite("inverno", w.Regions[5].Terrain);
        Assert.Equal(100f - bite, d.Org, 3);
    }

    [Fact]
    public void AtHome_TheTroopIsSheltered()
    {
        var w = Setup("inverno");
        var home = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);      // região nossa
        var field = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 5);     // região do inimigo
        home.Org = field.Org = 100f;
        w.Register(new WeatherSystem());
        w.Tick();

        Assert.True(home.Org > field.Org, $"em casa {home.Org}, em campo {field.Org}");
        Assert.True(home.Org < 100f);                                     // mas nem em casa sai de graça
    }

    [Fact]
    public void TheTerrainDecidesHowHardTheSeasonBites()
    {
        var w = Setup("inverno");
        Assert.True(w.SeasonBite("inverno", "tundra") > w.SeasonBite("inverno", "urban"));
        Assert.True(WeatherSystem.BiteOn(w, "tundra") > WeatherSystem.BiteOn(w, "urban"));
        Assert.Equal(1f, w.SeasonBite("inverno", "terreno_que_nao_existe"));   // sem linha, castiga como a média
    }

    [Fact]
    public void ADivisionInBattlePaysTheBattle_NotTheWeather()
    {
        var w = Setup("inverno");
        w.Countries[1].AtWarWith.Add(2);
        w.Countries[2].AtWarWith.Add(1);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);
        var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
        att.Org = def.Org = 100f;
        w.ActiveBattles.Add(new Battle { RegionId = 4, AttackerCountryId = 1, Attackers = { att.Id }, Defenders = { def.Id } });

        w.Register(new WeatherSystem());
        w.Tick();
        Assert.Equal(100f, att.Org, 3);
        Assert.Equal(100f, def.Org, 3);
    }

    [Fact]
    public void WinterHurtsTheRecovery_AndSummerHelpsIt()
    {
        float After(string season)
        {
            var w = Setup(season);
            var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
            d.Org = 20f;
            w.Register(new RecoverySystem());
            w.Tick();
            return d.Org;
        }

        Assert.True(After("verao") > After("inverno"));
    }

    [Fact]
    public void TheChangeOfSeasonIsNews()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        for (int m = 1; m <= 12; m++) w.SeasonMonths[m] = m is >= 6 and <= 8 ? "verao" : "inverno";
        var seen = new List<SeasonChanged>();
        w.Events.Subscribe<SeasonChanged>(seen.Add);

        w.Register(new WeatherSystem());
        TestWorld.Days(w, 200);                 // Janeiro → Junho → nada mais até Setembro

        Assert.Equal(new[] { "inverno", "verao" }, seen.Select(e => e.SeasonId));
        Assert.Equal("Verão", seen[1].Name);
    }
}
