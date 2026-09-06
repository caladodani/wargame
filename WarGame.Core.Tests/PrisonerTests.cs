using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Prisioneiros de guerra: quem se rende, o que rendem a trabalhar, quem foge e quem volta a
/// casa na paz. Os números vêm das regras — o que se mede é a mecânica.</summary>
public class PrisonerTests
{
    private static (World w, Country a, Country b) Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var a = w.Countries[1]; var b = w.Countries[2];
        a.AtWarWith.Add(2); b.AtWarWith.Add(1);
        a.Manpower = b.Manpower = 1_000_000f;
        return (w, a, b);
    }

    /// <summary>Desfaz uma divisão do país dono em terreno de quem lá manda, como faz o combate.</summary>
    private static Division Lose(World w, int owner, int regionId, int id = 1)
    {
        var d = TestWorld.AddDivision(w, id, owner, owner == 1 ? TestWorld.Inf : TestWorld.Inf2, regionId);
        w.Events.Publish(new DivisionDestroyed(d.Id));
        w.RemoveDivision(d.Id);
        return d;
    }

    [Fact]
    public void TheRulesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.Rule("prisoner_share") > 0f);
        Assert.True(w.Rule("prisoner_return") > 0f);
        Assert.True(w.Rule("prisoner_work_men") > 0f);
    }

    [Fact]
    public void ADivisionLostInEnemyGroundSurrenders()
    {
        var (w, _, b) = Setup();
        w.Register(new PrisonerSystem());
        w.Tick();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 5);   // região do país 2
        int expected = PrisonerSystem.Men(w, d);
        var seen = new List<PrisonersTaken>();
        w.Events.Subscribe<PrisonersTaken>(seen.Add);

        w.Events.Publish(new DivisionDestroyed(d.Id));
        w.RemoveDivision(d.Id);

        Assert.True(expected > 0);
        Assert.Equal(expected, b.Prisoners[1]);
        Assert.Equal(5, Assert.Single(seen).RegionId);
    }

    [Fact]
    public void AtHome_NobodyIsTakenPrisoner()
    {
        var (w, _, b) = Setup();
        w.Register(new PrisonerSystem());
        w.Tick();
        Lose(w, 1, 1);                       // desfeita em casa: não há quem os apanhe

        Assert.Empty(b.Prisoners);
    }

    [Fact]
    public void WithoutAWar_NobodyIsTakenPrisoner()
    {
        var (w, a, b) = Setup();
        a.AtWarWith.Clear(); b.AtWarWith.Clear();
        w.Register(new PrisonerSystem());
        w.Tick();
        Lose(w, 1, 5);

        Assert.Empty(b.Prisoners);
    }

    [Fact]
    public void PrisonersWorkForWhoeverGuardsThem()
    {
        var (w, a, _) = Setup();
        w.Register(new PrisonerSystem());
        a.Prisoners[2] = (int)w.Rule("prisoner_work_men", 400000f);
        w.Tick();

        float max = w.Rule("prisoner_work_max", 0.2f);
        Assert.Equal(1f + max, a.PrisonerMult["industry"], 3);
        Assert.True(a.Stat("industry") > 0f);
    }

    [Fact]
    public void TheWorkBonusHasACeiling()
    {
        var (w, a, _) = Setup();
        w.Register(new PrisonerSystem());
        a.Prisoners[2] = (int)(w.Rule("prisoner_work_men", 400000f) * 50f);
        w.Tick();

        Assert.Equal(1f + w.Rule("prisoner_work_max", 0.2f), a.PrisonerMult["industry"], 3);
    }

    [Fact]
    public void AnEmptyCampIsWorthNothing()
    {
        var (w, a, _) = Setup();
        w.Register(new PrisonerSystem());
        w.Tick();

        Assert.False(a.PrisonerMult.ContainsKey("industry"));
        Assert.Equal(1f, a.PrisonerMult.GetValueOrDefault("industry", 1f));
    }

    [Fact]
    public void EveryDaySomeEscapeAndGoHome()
    {
        var (w, a, b) = Setup();
        w.Rules["prisoner_escape"] = 0.1f;
        a.Prisoners[2] = 10_000;
        float before = b.Manpower;
        w.Register(new PrisonerSystem());
        w.Tick();

        Assert.Equal(9_000, a.Prisoners[2]);
        Assert.Equal(before + 1_000, b.Manpower, 0);
    }

    [Fact]
    public void PeaceOpensTheCamps_ButNotEverybodyComesBack()
    {
        var (w, a, b) = Setup();
        w.Rules["prisoner_escape"] = 0f;
        a.Prisoners[2] = 100_000;
        b.Prisoners[1] = 40_000;
        float manA = a.Manpower, manB = b.Manpower;
        var back = new List<PrisonersReturned>();
        w.Events.Subscribe<PrisonersReturned>(back.Add);
        w.Register(new PrisonerSystem());
        w.Tick();

        w.Events.Publish(new PeaceSigned(1, 2, 3));

        float share = w.Rule("prisoner_return", 0.6f);
        Assert.Empty(a.Prisoners);
        Assert.Empty(b.Prisoners);
        Assert.Equal(manB + 100_000 * share, b.Manpower, 0);
        Assert.Equal(manA + 40_000 * share, a.Manpower, 0);
        Assert.Equal(2, back.Count);
    }

    [Fact]
    public void ACapitulationAlsoSendsThemHome()
    {
        var (w, a, b) = Setup();
        a.Prisoners[2] = 50_000;
        w.Register(new PrisonerSystem());
        w.Tick();

        w.Events.Publish(new CountryCapitulated(2, 1));
        Assert.Empty(a.Prisoners);
    }

    [Fact]
    public void TheWorkStopsWhenTheCampEmpties()
    {
        var (w, a, b) = Setup();
        a.Prisoners[2] = 200_000;
        w.Register(new PrisonerSystem());
        w.Tick();
        Assert.True(a.PrisonerMult["industry"] > 1f);

        w.Events.Publish(new PeaceSigned(1, 2, 0));
        w.Tick();

        Assert.Equal(1f, a.PrisonerMult.GetValueOrDefault("industry", 1f));
    }

    [Fact]
    public void TheCampsSurviveASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Prisoners[2] = 123_456;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);
        repo.WriteSave(w, save);                 // segundo save não pode duplicar a chave

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(123_456, w2.Countries[1].Prisoners[2]);
    }

    [Fact]
    public void SendingThemHomeMakesTheChronicle()
    {
        var (w, a, _) = Setup();
        a.Prisoners[2] = 30_000;
        w.Register(new PrisonerSystem());
        w.Register(new ChronicleSystem());
        w.Tick();

        w.Events.Publish(new PeaceSigned(1, 2, 1));

        // a paz em si também faz história: o que se procura aqui é a linha dos campos
        var line = Assert.Single(w.Chronicle, x => x.Kind == "prisioneiros");
        Assert.Contains("Alfa devolve", line.Text);
        Assert.Contains("a Beta", line.Text);
        Assert.Equal(2, line.CountryId);                 // a linha é escrita do lado de quem os recebe
    }
}
