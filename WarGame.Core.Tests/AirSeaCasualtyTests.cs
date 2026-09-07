using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Baixas no comando do ar e do mar. O CommandCasualtySystem só ouvia batalhas de terra: um
/// almirante entrava na guerra e morria de velho, por muito que a esquadra dele fosse ao fundo.
///
/// Agora a guerra aérea e a naval anunciam os combates (AirCombatEnded/SeaCombatEnded) e o estado-maior
/// dessa arma arrisca-se com ela. Não há exército para entregar a um interino — o que se perde é o que o
/// homem dava ao país inteiro enquanto está fora. A probabilidade vem das regras (wound_chance_air,
/// wound_chance_sea), que aqui se põem a 1 ou a 0 para o sorteio deixar de mandar.</summary>
public class AirSeaCasualtyTests
{
    private const string Wing = "gen_ar_caca", Fleet = "gen_mar_esquadra", Land = "gen_ofensiva";

    /// <summary>Dois países em guerra com asas (ou navios) no mesmo sítio, e o sistema de baixas já no
    /// barramento — que o combate do ar corre antes dele na ordem do dia.</summary>
    private static World Fight(float mine, float theirs, bool sea = false, params string[] staff)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values)
        {
            c.AirPower = 100f; c.Warships = 100f; c.Money = 100_000f; c.IsPlayer = true;
        }
        foreach (var id in staff) w.Countries[1].Generals.Add(id);
        World.ApplyGenerals(w, w.Countries[1]);

        if (sea)
        {
            w.NavalMissions.Add(new NavalMission { CountryId = 1, RegionId = 1, MissionId = "bloqueio", Ships = mine });
            w.NavalMissions.Add(new NavalMission { CountryId = 2, RegionId = 1, MissionId = "bloqueio", Ships = theirs });
        }
        else
        {
            w.AirMissions.Add(new AirMission { CountryId = 1, RegionId = 1, MissionId = "superioridade", Wings = mine });
            w.AirMissions.Add(new AirMission { CountryId = 2, RegionId = 1, MissionId = "superioridade", Wings = theirs });
        }

        new CommandCasualtySystem().Bind(w);
        w.Register(sea ? new NavalMissionSystem() : (ISystem)new AirMissionSystem());
        return w;
    }

    [Fact]
    public void TheAirAndSeaSeveritiesComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        var air = w.WoundKinds.Values.Single(k => k.Domain == World.Air);
        var sea = w.WoundKinds.Values.Single(k => k.Domain == World.Sea);
        Assert.True(air.Days > 0 && sea.Days > 0);

        // as gravidades sem arma servem toda a gente; as de arma são só de lá
        foreach (var k in w.WoundKinds.Values.Where(k => k.Domain is null))
            Assert.All(World.Domains, d => Assert.True(World.WoundIsFor(k, d)));
        Assert.True(World.WoundIsFor(air, World.Air));
        Assert.False(World.WoundIsFor(air, World.Land));
        Assert.False(World.WoundIsFor(sea, World.Air));

        Assert.Equal("wound_chance", World.WoundChanceRule(World.Land));
        Assert.Equal("wound_chance_air", World.WoundChanceRule(World.Air));
        Assert.Equal("wound_chance_sea", World.WoundChanceRule(World.Sea));
        Assert.True(w.Rule("wound_chance_air") > 0f && w.Rule("wound_chance_sea") > 0f);
    }

    [Fact]
    public void ADogfightIsAnnouncedToBothSides()
    {
        var w = Fight(4f, 40f);                                  // quatro asas contra quarenta
        var seen = new List<AirCombatEnded>();
        w.Events.Subscribe<AirCombatEnded>(seen.Add);
        w.Tick();

        Assert.Equal(2, seen.Count);
        Assert.Equal(new[] { 1, 2 }, seen.Select(e => e.CountryId).OrderBy(x => x));
        Assert.All(seen, e => Assert.Equal(1, e.RegionId));
        Assert.All(seen, e => Assert.True(e.Lost > 0f));
        // levou a pior quem deixou lá a maior fatia do que tinha — os dois perderam o mesmo, mas um
        // tinha dez vezes menos
        Assert.True(seen.Single(e => e.CountryId == 1).Worse);
        Assert.False(seen.Single(e => e.CountryId == 2).Worse);
    }

    [Fact]
    public void ASeaBattleIsAnnouncedToBothSides()
    {
        var w = Fight(40f, 5f, sea: true);
        var seen = new List<SeaCombatEnded>();
        w.Events.Subscribe<SeaCombatEnded>(seen.Add);
        w.Tick();

        Assert.Equal(2, seen.Count);
        Assert.False(seen.Single(e => e.CountryId == 1).Worse);
        Assert.True(seen.Single(e => e.CountryId == 2).Worse);   // a esquadra pequena é que se desfaz
        Assert.Equal(2, seen.Single(e => e.CountryId == 1).EnemyCountryId);
    }

    [Fact]
    public void TheWingCommanderFallsInTheSkyAndTheGroundOneDoesNot()
    {
        var w = Fight(20f, 20f, false, Wing, Land);
        w.Rules["wound_chance_air"] = 1f;
        w.Rules["wound_chance"] = 1f;                            // e mesmo assim o de terra não é dali
        var hurt = new List<GeneralWounded>();
        var dead = new List<GeneralKilled>();
        w.Events.Subscribe<GeneralWounded>(hurt.Add);
        w.Events.Subscribe<GeneralKilled>(dead.Add);
        w.Tick();

        var c = w.Countries[1];
        Assert.True(hurt.Count + dead.Count == 1);
        Assert.All(hurt, e => Assert.Equal(Wing, e.GeneralId));
        Assert.All(dead, e => Assert.Equal(Wing, e.GeneralId));
        Assert.Contains(Land, c.Generals);
        Assert.False(w.IsWounded(1, Land));                      // o combate no céu não lhe toca
    }

    [Fact]
    public void TheFleetCommanderFallsAtSea()
    {
        var w = Fight(20f, 20f, true, Fleet);
        w.Rules["wound_chance_sea"] = 1f;
        var seen = new List<IGameEvent>();
        w.Events.Subscribe<GeneralWounded>(seen.Add);
        w.Events.Subscribe<GeneralKilled>(seen.Add);
        w.Tick();

        Assert.Single(seen);
        Assert.True(w.IsWounded(1, Fleet) || !w.Countries[1].Generals.Contains(Fleet));
    }

    [Fact]
    public void WithTheChanceAtZero_TheStaffFliesHomeWhole()
    {
        var w = Fight(20f, 20f, false, Wing);
        w.Rules["wound_chance_air"] = 0f;
        for (int i = 0; i < 30; i++) w.Tick();

        Assert.Empty(w.Countries[1].GeneralWound);
        Assert.Contains(Wing, w.Countries[1].Generals);
    }

    [Fact]
    public void TheSideThatCameOffWorseRisksMore()
    {
        // 0.6 sozinho nunca chega para ferir de certeza; com o multiplicador da derrota passa de 1 e o
        // comandante do lado que se desfez cai sempre. Sem multiplicador, esse mesmo lado escapa sempre.
        var w = Fight(3f, 60f, false, Wing);
        w.Rules["wound_chance_air"] = 0.6f;
        w.Rules["wound_loss_mult"] = 2f;
        w.Tick();
        Assert.True(w.IsWounded(1, Wing) || !w.Countries[1].Generals.Contains(Wing));

        var w2 = Fight(3f, 60f, false, Wing);
        w2.Rules["wound_chance_air"] = 0f;
        w2.Rules["wound_loss_mult"] = 2f;
        w2.Tick();
        Assert.Contains(Wing, w2.Countries[1].Generals);
        Assert.False(w2.IsWounded(1, Wing));
    }

    [Fact]
    public void AWoundedWingCommanderStopsCountingForTheCountry()
    {
        var w = Fight(20f, 20f, false, Wing);
        var c = w.Countries[1];
        float whole = c.Stat("air_losses", 1f);
        Assert.True(whole < 1f);                                 // o comandante de caça faz perder menos

        w.Rules["wound_chance_air"] = 1f;
        w.Rules["wound_loss_mult"] = 1f;
        w.Tick();

        Assert.Equal(1f, c.Stat("air_losses", 1f), 3);           // ferido ou morto, não conta a ninguém
        if (!c.Generals.Contains(Wing)) return;                  // ficou lá: já não há nada para esperar

        int days = w.WoundDaysLeft(1, Wing);
        w.Rules["wound_chance_air"] = 0f;
        w.Register(new CommandCasualtySystem());                 // agora sim, o Recover a correr todos os dias
        var back = new List<GeneralRecovered>();
        w.Events.Subscribe<GeneralRecovered>(back.Add);
        for (int i = 0; i <= days; i++) w.Tick();

        Assert.Single(back);
        Assert.Equal(whole, c.Stat("air_losses", 1f), 3);
        Assert.Empty(c.GeneralWoundKind);
    }

    [Fact]
    public void TheParachuteIsOnlyForTheSkyAndTheWaterOnlyForTheSea()
    {
        var w = Fight(20f, 20f, false, Wing);
        w.Rules["wound_chance_air"] = 1f;
        w.Rules["air_dogfight_loss"] = 0.0001f;                  // o céu tem de durar as 400 voltas
        NoDeaths(w);                                             // e o homem também
        var c = w.Countries[1];
        var drawn = new HashSet<string>();
        for (int i = 0; i < 400; i++)
        {
            c.GeneralWound.Clear(); c.GeneralWoundKind.Clear();
            w.Tick();
            foreach (var k in c.GeneralWoundKind.Values) drawn.Add(k);
        }
        // o comandante de asa apanha as gravidades comuns E a do ar; nunca a do mar
        Assert.Contains("abatido", drawn);
        Assert.DoesNotContain("afundado", drawn);

        // e o de terra nunca apanha nenhuma das duas, por muito que a batalha se repita
        var (lw, lc, lg) = LandSetup();
        NoDeaths(lw);
        var landKinds = new HashSet<string>();
        for (int i = 0; i < 400; i++)
        {
            lc.GeneralWound.Clear(); lc.GeneralWoundKind.Clear();
            lg.GeneralId = Land;
            lw.Events.Publish(new BattleEnded(3, false, 2, 1));
            foreach (var k in lc.GeneralWoundKind.Values) landKinds.Add(k);
        }
        Assert.DoesNotContain("abatido", landKinds);
        Assert.DoesNotContain("afundado", landKinds);
        Assert.NotEmpty(landKinds);
    }

    /// <summary>Tira o peso às gravidades fatais: quem cai volta sempre, e o sorteio pode repetir-se as
    /// vezes que forem precisas sem o homem desaparecer da folha a meio da contagem.</summary>
    private static void NoDeaths(World w)
    {
        foreach (var k in w.WoundKinds.Values.Where(k => k.Fatal).ToList())
            w.WoundKinds[k.Id] = k with { Weight = 0f };
    }

    /// <summary>Um exército de terra com comandante destacado, para comparar o sorteio dele.</summary>
    private static (World w, Country c, ArmyGroup g) LandSetup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Generals.Add(Land);
        var g = new ArmyGroup { Id = 1, CountryId = 1, Name = "1.º Exército", GeneralId = Land };
        w.ArmyGroups[1] = g;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        w.JoinGroup(g, 1);
        World.ApplyGenerals(w, c);
        w.Rules["wound_chance"] = 1f;
        new CommandCasualtySystem().Bind(w);
        return (w, c, g);
    }

    [Fact]
    public void TheSeverityOfTheWoundSurvivesTheSave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        c.Generals.Add(Fleet);
        c.GeneralWound[Fleet] = w.Clock.Day + 20;
        c.GeneralWoundKind[Fleet] = "afundado";

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(c.GeneralWound[Fleet], w2.Countries[1].GeneralWound[Fleet]);
        Assert.Equal("afundado", w2.Countries[1].GeneralWoundKind[Fleet]);
    }
}
