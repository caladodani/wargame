using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Sabotagem na retaguarda: operações de espionagem que caem numa região do inimigo em vez de
/// caírem no país inteiro. O que estragam e quanto vem da tabela spy_op — aqui mede-se a mecânica.</summary>
public class SabotageTests
{
    private static (World w, Country a, Country b) Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var a = w.Countries[1]; var b = w.Countries[2];
        a.AtWarWith.Add(2); b.AtWarWith.Add(1);
        a.Money = 1000f;
        return (w, a, b);
    }

    /// <summary>A operação de sabotagem que produz este efeito, tal como está na base de dados.</summary>
    private static SpyOp Op(World w, string effect) => w.SpyOps.Values.First(o => o.Effect == effect);

    /// <summary>Lança a operação e deixa-a correr até ao fim.</summary>
    private static void Run(World w, string effect, int regionId, int actor = 1, int target = 2)
    {
        var op = Op(w, effect);
        var cmd = new StartSpyOpCommand(actor, target, op.Id, regionId);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        w.Register(new EspionageSystem());
        for (int i = 0; i < op.Days * 3 && w.ActiveSpyOps.Count > 0; i++) w.Tick();
    }

    [Fact]
    public void TheRegionalOpsComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        var regional = w.SpyOps.Values.Where(o => o.IsRegional).ToList();
        Assert.True(regional.Count >= 3);
        Assert.All(regional, o => Assert.True(o.Cost > 0f && o.Days > 0));
        Assert.Contains(w.SpyOps.Values, o => !o.IsRegional);      // as antigas continuam contra o país
    }

    [Fact]
    public void APortGoesDownALevel()
    {
        var (w, _, _) = Setup();
        w.Regions[5].Buildings["porto"] = 2;

        Run(w, "sabotage_port", 5);

        Assert.Equal(1, w.Regions[5].Buildings["porto"]);
    }

    [Fact]
    public void TheLastLevelTakesThePortOffTheMap()
    {
        var (w, _, _) = Setup();
        w.Regions[5].Buildings["porto"] = 1;

        Run(w, "sabotage_port", 5);

        Assert.False(w.Regions[5].Buildings.ContainsKey("porto"));
    }

    [Fact]
    public void TheRoadsGoDown_AndTheRegionKnowsIt()
    {
        var (w, _, _) = Setup();
        float before = w.Regions[5].Infrastructure;

        Run(w, "sabotage_infra", 5);

        Assert.True(w.Regions[5].Infrastructure < before);
        Assert.Equal(before * (1f - Op(w, "sabotage_infra").Magnitude), w.Regions[5].Infrastructure, 3);
    }

    [Fact]
    public void TheCasematesFall()
    {
        var (w, _, _) = Setup();
        w.Regions[5].Fort = 3;

        Run(w, "sabotage_fort", 5);

        Assert.Equal(2, w.Regions[5].Fort);
    }

    [Fact]
    public void OurOwnGroundIsNotAValidTarget()
    {
        var (w, _, _) = Setup();
        var cmd = new StartSpyOpCommand(1, 2, Op(w, "sabotage_port").Id, 1);   // região 1 é nossa
        Assert.Equal("essa região não é dele", cmd.Validate(w));
    }

    [Fact]
    public void WithoutAWar_NobodyBlowsUpBridges()
    {
        var (w, a, b) = Setup();
        a.AtWarWith.Clear(); b.AtWarWith.Clear();
        var cmd = new StartSpyOpCommand(1, 2, Op(w, "sabotage_infra").Id, 5);
        Assert.Equal("sabotagem só em guerra", cmd.Validate(w));
    }

    [Fact]
    public void WithoutARegion_TheOpIsRefused()
    {
        var (w, _, _) = Setup();
        Assert.Equal("escolhe uma região", new StartSpyOpCommand(1, 2, Op(w, "sabotage_fort").Id).Validate(w));
    }

    [Fact]
    public void ARegionRetakenBeforeTheTeamArrives_IsSparedAndSoAreWe()
    {
        var (w, _, _) = Setup();
        w.Regions[5].Fort = 2;
        var op = Op(w, "sabotage_fort");
        var cmd = new StartSpyOpCommand(1, 2, op.Id, 5);
        cmd.Execute(w);
        w.Regions[5].ControllerId = 1;                       // tomámos a região entretanto
        w.Register(new EspionageSystem());
        for (int i = 0; i < op.Days * 3 && w.ActiveSpyOps.Count > 0; i++) w.Tick();

        Assert.Empty(w.ActiveSpyOps);                        // a operação morreu
        Assert.Equal(2, w.Regions[5].Fort);                  // sem estragar o que já é nosso
    }

    [Fact]
    public void TheDamageMakesTheChronicle()
    {
        var (w, _, _) = Setup();
        w.Regions[5].Fort = 1;
        w.Register(new ChronicleSystem());
        w.Tick();

        Run(w, "sabotage_fort", 5);

        var line = Assert.Single(w.Chronicle, x => x.Kind == "sabotagem");
        Assert.Equal(5, line.RegionId);
        Assert.Contains("Alfa", line.Text);
    }

    [Fact]
    public void TheTargetRegionSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        w.Countries[1].Money = 1000f;
        new StartSpyOpCommand(1, 2, Op(w, "sabotage_port").Id, 5).Execute(w);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(5, Assert.Single(w2.ActiveSpyOps).RegionId);
    }

    [Fact]
    public void TheAiBlowsUpTheEnemyQuay()
    {
        var (w, a, b) = Setup();
        a.IsPlayer = false;
        a.Money = 5000f;
        w.Regions[5].Buildings["porto"] = 2;
        TestWorld.AddDivision(w, 1, 2, TestWorld.Inf2, 5);          // o alvo tem tropa: entra na escolha da IA
        w.Register(new AiSystem());
        for (int i = 0; i < (int)w.Rule("ai_period_days", 7f) * 3 && w.ActiveSpyOps.Count == 0; i++) w.Tick();

        var op = Assert.Single(w.ActiveSpyOps);
        Assert.Equal("sabotage_port", w.SpyOps[op.OpId].Effect);
        Assert.Equal(5, op.RegionId);
    }
}
