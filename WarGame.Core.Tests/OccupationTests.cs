using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Políticas de ocupação: o que se faz ao povo da terra tomada. Cada política mexe ao mesmo tempo
/// na resistência, no rendimento e nos recrutas, e trocar de política tem de esperar.
///
/// Mapa dos testes: linha 1-2-3 (país 1) | 4-5-6 (país 2), com o país 1 a ocupar a região 4.</summary>
public class OccupationTests
{
    private const int Taken = 4;

    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[Taken].ControllerId = 1;                 // tomada, mas continua a ser deles
        foreach (var c in w.Countries.Values) c.IsPlayer = true;
        w.Register(new OccupationSystem());
        return w;
    }

    [Fact]
    public void ThePoliciesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(5, w.OccupationPolicyDefs.Count);

        var start = OccupationSystem.Default(w);
        Assert.Equal("supervisao_civil", start.Id);
        Assert.Equal(1f, start.Resistance, 3);
        Assert.Equal(1f, start.Yield, 3);
        Assert.Equal(1f, start.Manpower, 3);               // quem não decide nada fica com o jogo de sempre

        Assert.Equal(1.8f, w.OccupationPolicyDefs["trabalho_forcado"].Yield, 3);
        Assert.Equal(0.55f, w.OccupationPolicyDefs["policia_local"].Resistance, 3);
        Assert.Equal(30f, w.Rule("occupation_switch_days"), 3);
    }

    [Fact]
    public void OwnLandHasNoOccupationAtAll()
    {
        var w = Build();
        OccupationSystem.Set(w, 1, 2, "trabalho_forcado");
        Assert.Equal(1f, OccupationSystem.YieldMult(w, w.Regions[1]), 3);       // casa é casa
        Assert.Equal(1.8f, OccupationSystem.YieldMult(w, w.Regions[Taken]), 3);
        Assert.Equal(1, OccupationSystem.Regions(w, 1, 2));
        Assert.Equal(new[] { 2 }, OccupationSystem.Occupied(w, 1));
    }

    [Fact]
    public void AHarshPolicyPaysMoreAndABlandOneLess()
    {
        var w = Build();
        float plain = EconomySystem.Income(w, 1);

        OccupationSystem.Set(w, 1, 2, "quotas_duras");
        float squeezed = EconomySystem.Income(w, 1);
        OccupationSystem.Set(w, 1, 2, "policia_local");
        float gentle = EconomySystem.Income(w, 1);

        Assert.True(squeezed > plain, $"apertar tinha de render mais: {squeezed:0.00} contra {plain:0.00}");
        Assert.True(gentle < plain, $"aliviar tinha de render menos: {gentle:0.00} contra {plain:0.00}");
    }

    [Fact]
    public void TheStreetBoilsAtTheSpeedThePolicyDeserves()
    {
        static float After(string policy, int days)
        {
            var w = Build();
            w.Register(new ResistanceSystem());
            OccupationSystem.Set(w, 1, 2, policy);
            TestWorld.Days(w, days);
            return w.Regions[Taken].Resistance;
        }

        float calm = After("policia_local", 10), plain = After("supervisao_civil", 10), hard = After("trabalho_forcado", 10);
        Assert.True(calm < plain, $"a polícia local tinha de acalmar: {calm:0.000} contra {plain:0.000}");
        Assert.True(hard > plain, $"o trabalho forçado tinha de fazer ferver: {hard:0.000} contra {plain:0.000}");
    }

    [Fact]
    public void OccupiedPeopleOnlyGiveTheRecruitsThePolicyAllows()
    {
        static float Pool(string policy)
        {
            var w = Build();
            w.Register(new ManpowerSystem());
            OccupationSystem.Set(w, 1, 2, policy);
            w.Countries[1].Manpower = -1f;                 // por inicializar: o primeiro tick põe o pool no tecto
            w.Tick();
            return w.Countries[1].Manpower;
        }

        float few = Pool("policia_local"), plain = Pool("supervisao_civil"), many = Pool("trabalho_forcado");
        Assert.True(few < plain, $"a terra em paz dá menos homens: {few:0} contra {plain:0}");
        Assert.True(many > plain, $"o trabalho forçado dá mais: {many:0} contra {plain:0}");
    }

    [Fact]
    public void APolicyNeedsLandAndANameThatExists()
    {
        var w = Build();
        Assert.Null(new SetOccupationPolicyCommand(1, 2, "quotas_duras").Validate(w));
        Assert.Contains("não ocupamos terra nenhuma", new SetOccupationPolicyCommand(2, 1, "quotas_duras").Validate(w)!);
        Assert.Contains("política desconhecida", new SetOccupationPolicyCommand(1, 2, "campos").Validate(w)!);
        Assert.Contains("povo desconhecido", new SetOccupationPolicyCommand(1, 99, "quotas_duras").Validate(w)!);

        new SetOccupationPolicyCommand(1, 2, "quotas_duras").Execute(w);
        Assert.Contains("já lá está", new SetOccupationPolicyCommand(1, 2, "quotas_duras").Validate(w)!);
    }

    [Fact]
    public void ChangingHandsOverAPeopleHasToWait()
    {
        var w = Build();
        new SetOccupationPolicyCommand(1, 2, "quotas_duras").Execute(w);
        Assert.Equal(w.Clock.Day, OccupationSystem.Since(w, 1, 2));

        Assert.Contains("faltam 30 dias", new SetOccupationPolicyCommand(1, 2, "policia_local").Validate(w)!);
        TestWorld.Days(w, 10);
        Assert.Contains("faltam 20 dias", new SetOccupationPolicyCommand(1, 2, "policia_local").Validate(w)!);

        TestWorld.Days(w, 20);
        Assert.Null(new SetOccupationPolicyCommand(1, 2, "policia_local").Validate(w));
        new SetOccupationPolicyCommand(1, 2, "policia_local").Execute(w);
        Assert.Equal("policia_local", OccupationSystem.Policy(w, 1, 2).Id);
    }

    [Fact]
    public void LandGivenBackTakesItsPolicyWithIt()
    {
        var w = Build();
        OccupationSystem.Set(w, 1, 2, "trabalho_forcado");
        Assert.Single(w.Occupations);

        w.Regions[Taken].ControllerId = 2;                 // devolvida: já não há povo nenhum debaixo de nós
        w.Tick();
        Assert.Empty(w.Occupations);
        Assert.Equal("supervisao_civil", OccupationSystem.Policy(w, 1, 2).Id);
    }

    [Fact]
    public void TheAiSoftensWhatIsBoilingAndSqueezesWhatItNeeds()
    {
        var w = Build();
        w.Countries[1].IsPlayer = false;
        w.Countries[1].Money = 10f;                        // cofre curto
        w.StartWar(1, 2);

        w.Tick();
        Assert.Equal("trabalho_forcado", OccupationSystem.Policy(w, 1, 2).Id);   // em guerra e a arder o cofre: espreme

        w.Regions[Taken].Resistance = 0.9f;                                      // agora a terra está a ferver
        TestWorld.Days(w, (int)w.Rule("occupation_switch_days") + 1);
        Assert.Equal("policia_local", OccupationSystem.Policy(w, 1, 2).Id);      // e a IA alivia
    }

    [Fact]
    public void ThePolicySurvivesSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[Taken].ControllerId = 1;
        OccupationSystem.Set(w, 1, 2, "governo_militar");

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = Assert.Single(w2.Occupations);
        Assert.Equal(1, back.CountryId);
        Assert.Equal(2, back.TargetId);
        Assert.Equal("governo_militar", back.PolicyId);
        Assert.Equal(1.15f, OccupationSystem.Policy(w2, 1, 2).Yield, 3);
    }
}
