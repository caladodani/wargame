using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Condecorações: limiares da tabela medal, bónus de força somado e o que sobrevive ao save.
/// As medalhas vêm da base de dados — os testes usam os ids do seed (baptismo, assalto, campanha, aco, imortais).</summary>
public class MedalTests
{
    private static (World w, Division d) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        return (w, d);
    }

    /// <summary>Avança até um dia múltiplo de medal_check_days e corre o sistema.</summary>
    private static void Award(World w)
    {
        var sys = new MedalSystem();
        int period = Math.Max(1, (int)w.Rule("medal_check_days", 2f));
        for (int i = 0; i <= period; i++) { sys.Tick(w); TestWorld.Days(w, 1); }
    }

    [Fact]
    public void Catalogue_ComesFromTheDatabase()
    {
        var (w, _) = Build();
        Assert.True(w.MedalDefs.Count >= 5);
        var m = w.MedalDefs["baptismo"];
        Assert.Equal("battles", m.Metric);
        Assert.Equal(1f, m.Threshold);
        Assert.True(m.Bonus > 0f);
    }

    [Fact]
    public void FirstBattle_EarnsTheFirstMedal()
    {
        var (w, d) = Build();
        Award(w);
        Assert.Empty(d.Medals);      // sem batalhas não há condecoração

        d.Battles = 1;
        Award(w);
        Assert.Contains("baptismo", d.Medals);
        Assert.DoesNotContain("aco", d.Medals);
    }

    [Fact]
    public void ThresholdsStack_AndNothingIsAwardedTwice()
    {
        var (w, d) = Build();
        var seen = new List<MedalAwarded>();
        w.Events.Subscribe<MedalAwarded>(seen.Add);
        d.Battles = 12; d.Captures = 5; d.Xp = 95f;
        Award(w);
        Assert.Equal(new[] { "aco", "assalto", "baptismo", "campanha", "imortais" }, d.Medals.OrderBy(x => x));
        Assert.Equal(5, seen.Count);

        Award(w);
        Assert.Equal(5, seen.Count);   // segunda passagem não repete nada
        Assert.All(seen, e => Assert.Equal(1, e.DivisionId));
    }

    [Fact]
    public void Bonus_IsTheSumButNeverAboveTheCap()
    {
        var (w, d) = Build();
        d.Medals.Add("baptismo");
        Assert.Equal(w.MedalDefs["baptismo"].Bonus, MedalSystem.Bonus(w, d), 4);

        d.Medals.Add("assalto");
        Assert.Equal(w.MedalDefs["baptismo"].Bonus + w.MedalDefs["assalto"].Bonus, MedalSystem.Bonus(w, d), 4);

        w.Rules["medal_bonus_max"] = 0.02f;
        Assert.Equal(0.02f, MedalSystem.Bonus(w, d), 4);
    }

    [Fact]
    public void UnknownMedalOnADivision_IsWorthNothing()
    {
        var (w, d) = Build();
        d.Medals.Add("medalha-que-nao-existe");
        Assert.Equal(0f, MedalSystem.Bonus(w, d), 4);
    }

    [Fact]
    public void WalkingIntoAnEmptyEnemyRegion_CountsAsACapture()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new MovementSystem());
        w.StartWar(1, 2);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        d.SetPath(new[] { 4 });
        for (int i = 0; i < 200 && d.Captures == 0; i++) w.Tick();

        Assert.Equal(1, d.Captures);
        Assert.Equal(1, w.Regions[4].ControllerId);
    }

    [Fact]
    public void WinningABattle_CountsBothTheBattleAndTheCapture()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new MovementSystem()); w.Register(new CombatSystem());
        w.StartWar(1, 2);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var att2 = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 3);
        var def = TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 4, org: 12f, hp: 8f);
        att.SetPath(new[] { 4 }); att2.SetPath(new[] { 4 });
        for (int i = 0; i < 400 && att.Captures == 0; i++) w.Tick();

        Assert.Equal(1, att.Captures);
        Assert.True(att.Battles >= 1);
        Assert.True(att2.Battles >= 1);
        Assert.DoesNotContain(def.Id, w.Divisions.Keys.Where(id => id == 9 && w.Divisions[9].Captures > 0));
    }

    [Fact]
    public void Medal_MakesTheDivisionFightHarder()
    {
        // Dois mundos com a mesma semente: só muda a condecoração, logo só o bónus explica a diferença.
        Assert.True(Dealt(withMedal: true) > Dealt(withMedal: false));
    }

    /// <summary>Estrago feito ao defensor num dia de batalha, num mundo montado de raiz.</summary>
    private static float Dealt(bool withMedal)
    {
        var (w, _) = TestWorld.Build(seed: 7);
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var def = TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 4);
        if (withMedal)
        {
            // medalha de teste com um bónus grande: a diferença tem de sair do ruído do combate
            w.MedalDefs["teste"] = new MedalDef("teste", "Teste", "", "xp", 0f, 5f, 99);
            w.Rules["medal_bonus_max"] = 5f;
            att.Medals.Add("teste");
        }
        w.ActiveBattles.Add(new Battle { RegionId = 4, AttackerCountryId = 1, Attackers = { att.Id }, Defenders = { def.Id } });
        new CombatSystem().Tick(w);
        return 100f - def.Hp;
    }

    [Fact]
    public void MedalsAndCounters_SurviveSaveAndLoad()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Battles = 11; d.Captures = 4; d.Xp = 50f;
        new MedalSystem().Tick(w);
        Assert.NotEmpty(d.Medals);

        using var save = new MsSqliteDatabase();
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(
            save, WarGame.Core.Data.SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var d2 = w2.Divisions[1];
        Assert.Equal(11, d2.Battles);
        Assert.Equal(4, d2.Captures);
        Assert.Equal(d.Medals.OrderBy(x => x), d2.Medals.OrderBy(x => x));
    }
}
