using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Operações anfíbias: marcar a praia, preparar a operação durante semanas com a tropa presa no
/// cais, largar só com mercantes livres e mar nosso — e depois o desembarque de sempre (custo de
/// organização, organização mínima para assaltar, lotação da praia e desvantagem de quem bate do mar).
///
/// A porta nova é a mais importante: uma ordem de marcha já não atravessa o mar para dentro de uma praia
/// inimiga. Sem operação não há assalto.
///
/// Mapa: ilha A (1-2, país 1) e ilha B (3-4, país 2), travessia 2↔3.</summary>
public class NavalInvasionTests
{
    private static World Islands(float km = 800f)
    {
        var (w, _) = TestWorld.Build();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 4, Manpower = 1e9f };
        for (int i = 1; i <= 4; i++)
        {
            int owner = i <= 2 ? 1 : 2;
            w.Regions[i] = new Region { Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner, Terrain = "plain", Population = 10_000_000, Coastal = i is 2 or 3 };
        }
        w.Regions[1].Neighbours.Add(2); w.Regions[2].Neighbours.Add(1);
        w.Regions[3].Neighbours.Add(4); w.Regions[4].Neighbours.Add(3);
        w.Regions[2].SeaNeighbours[3] = km; w.Regions[3].SeaNeighbours[2] = km;
        w.StartWar(1, 2);
        return w;
    }

    private static void Sail(World w, MovementSystem sys, int days = 3) { for (int i = 0; i < days; i++) sys.Tick(w); }

    /// <summary>Marca a operação e adianta-a até ao dia da largada: é o que o jogador faz em doze dias de
    /// jogo, e o que estes testes precisam de ter feito antes de haver praia nenhuma para assaltar.</summary>
    private static NavalInvasion Assault(World w, int target, params int[] ids)
    {
        var cmd = new PlanNavalInvasionCommand(1, target, ids.ToList());
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        var inv = w.NavalInvasions.Single(i => i.CountryId == 1 && i.TargetId == target);
        inv.Prep = 1f;
        new NavalInvasionSystem().Tick(w);
        return inv;
    }

    // ── a porta nova ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AMarchaJaNaoAtravessaOMarParaUmaPraiaInimiga()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        Assert.Contains("operação anfíbia", new MoveDivisionCommand(1, 1, 3).Validate(w)!);

        // e mesmo pondo a rota à mão, a coluna não sai do cais
        var events = new List<IGameEvent>();
        w.Events.Subscribe<LandingAborted>(events.Add);
        d.SetPath(new[] { 3 });
        Sail(w, new MovementSystem());
        Assert.Equal(2, d.RegionId);
        Assert.Empty(d.Path);
        Assert.Equal(new IGameEvent[] { new LandingAborted(1, 3) }, events);
    }

    [Fact]
    public void AMarchaPorMarParaCasaContinuaALivre()
    {
        var w = Islands();
        w.Regions[3].ControllerId = 1;                                  // a outra margem passou a ser nossa
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        Assert.Null(new MoveDivisionCommand(1, 1, 3).Validate(w));
        new MoveDivisionCommand(1, 1, 3).Execute(w);
        Sail(w, new MovementSystem());
        Assert.Equal(3, d.RegionId);
    }

    [Fact]
    public void AOperacaoLevaAsSuasSemanasEDepoisLarga()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        var planned = new List<IGameEvent>();
        w.Events.Subscribe<NavalInvasionLaunched>(planned.Add);
        new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Execute(w);
        var inv = Assert.Single(w.NavalInvasions);
        float days = NavalInvasionSystem.Days(w, 1);
        Assert.Equal(w.Rule("invasion_prep_days"), days, 3);

        var sys = new NavalInvasionSystem();
        for (int i = 0; i < (int)days - 1; i++) sys.Tick(w);
        Assert.Single(w.NavalInvasions);                                 // ainda a preparar-se
        Assert.Empty(d.Path);
        Assert.True(inv.Prep > 0.8f && inv.Prep < 1f, $"preparação a meio, não {inv.Prep:0.###}");

        sys.Tick(w);
        Assert.Empty(w.NavalInvasions);                                  // largou: sai da mesa
        Assert.True(d.Seaborne);
        Assert.Equal(new[] { 3 }, d.Path);
        Assert.Equal(new IGameEvent[] { new NavalInvasionLaunched(1, 3, 2, 1, inv.Name) }, planned);
    }

    [Fact]
    public void MaisDivisoesMaisDiasDePreparacao()
    {
        var w = Islands();
        Assert.Equal(w.Rule("invasion_prep_days"), NavalInvasionSystem.Days(w, 1), 3);
        Assert.Equal(w.Rule("invasion_prep_days") + 2f * w.Rule("invasion_prep_per_div"),
                     NavalInvasionSystem.Days(w, 3), 3);
        Assert.True(NavalInvasionSystem.Days(w, 3) > NavalInvasionSystem.Days(w, 1));
    }

    [Fact]
    public void ATropaEmbarcadaFicaNoCais()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        d.AutoAdvance = true;
        new MoveDivisionCommand(1, 1, 1).Execute(w);                     // ia a caminho de casa...
        Assert.NotEmpty(d.Path);
        new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Execute(w);

        Assert.Empty(d.Path);                                            // ...e embarcar corta-lhe a marcha
        Assert.False(d.AutoAdvance);
        Assert.True(NavalInvasionSystem.Embarked(w, 1));
        Assert.Equal("Embarcada numa operação anfíbia", new MoveDivisionCommand(1, 1, 1).Validate(w));
    }

    [Fact]
    public void DesmarcarDevolveATropaAoExercito()
    {
        var w = Islands();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Execute(w);
        var gone = new List<IGameEvent>();
        w.Events.Subscribe<NavalInvasionCancelled>(gone.Add);

        Assert.Null(new CancelNavalInvasionCommand(1, 3).Validate(w));
        new CancelNavalInvasionCommand(1, 3).Execute(w);

        Assert.Empty(w.NavalInvasions);
        Assert.False(NavalInvasionSystem.Embarked(w, 1));
        Assert.Null(new MoveDivisionCommand(1, 1, 1).Validate(w));
        Assert.Single(gone);
        Assert.NotNull(new CancelNavalInvasionCommand(1, 3).Validate(w));
    }

    [Fact]
    public void ATropaQueSaiDoCaisDeixaAOperacao()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Execute(w);
        var gone = new List<IGameEvent>();
        w.Events.Subscribe<NavalInvasionCancelled>(gone.Add);

        w.PlaceDivision(d, 1);                                           // recuada à força para a retaguarda
        new NavalInvasionSystem().Tick(w);

        Assert.Empty(w.NavalInvasions);
        Assert.Single(gone);
    }

    // ── o que a operação exige para largar ──────────────────────────────────────────────────────────

    [Fact]
    public void SemMercantesNaoSeMarcaPraiaNenhuma()
    {
        var w = Islands();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        w.Countries[1].Convoys = -w.Rule("convoy_base");                 // marinha mercante toda ao fundo
        Assert.Equal(0f, NavalInvasionSystem.FreeConvoys(w, 1), 3);
        Assert.Contains("mercantes", new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Validate(w)!);
    }

    [Fact]
    public void OsMercantesFicamPresosEnquantoAOperacaoExiste()
    {
        var w = Islands();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        float before = NavalInvasionSystem.FreeConvoys(w, 1);
        new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Execute(w);

        float per = w.Rule("invasion_convoys_per_div");
        Assert.Equal(per, NavalInvasionSystem.Booked(w, 1), 3);
        Assert.Equal(before - per, NavalInvasionSystem.FreeConvoys(w, 1), 3);

        new CancelNavalInvasionCommand(1, 3).Execute(w);
        Assert.Equal(before, NavalInvasionSystem.FreeConvoys(w, 1), 3);  // desmarcada, os navios voltam ao comércio
    }

    [Fact]
    public void OperacaoProntaEsperaEnquantoOMarForDeles()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Execute(w);
        var inv = w.NavalInvasions.Single();
        inv.Prep = 1f;
        var theirs = new NavalMission { CountryId = 2, RegionId = 3, MissionId = "patrulha" };
        theirs.Ships = 10f;                                              // esquadra deles em cima da praia
        w.NavalMissions.Add(theirs);

        Assert.Equal(0f, NavalInvasionSystem.SeaShare(w, 1, 3), 3);
        Assert.Contains("mar da zona", NavalInvasionSystem.Hold(w, inv)!);
        new NavalInvasionSystem().Tick(w);
        Assert.Single(w.NavalInvasions);                                 // não largou
        Assert.Empty(d.Path);

        w.NavalMissions.Clear();                                         // o mar ficou livre
        Assert.Equal(1f, NavalInvasionSystem.SeaShare(w, 1, 3), 3);
        Assert.Null(NavalInvasionSystem.Hold(w, inv));
        new NavalInvasionSystem().Tick(w);
        Assert.Empty(w.NavalInvasions);
        Assert.True(d.Seaborne);
    }

    [Fact]
    public void OMarDisputadoDivideSePelaForcaDeCadaLado()
    {
        var w = Islands();
        var theirs = new NavalMission { CountryId = 2, RegionId = 3, MissionId = "patrulha" };
        theirs.Ships = 4f; w.NavalMissions.Add(theirs);
        var ours = new NavalMission { CountryId = 1, RegionId = 3, MissionId = "patrulha" };
        ours.Ships = 4f; w.NavalMissions.Add(ours);
        Assert.Equal(0.5f, NavalInvasionSystem.SeaShare(w, 1, 3), 3);
        ours.Ships = 12f;
        Assert.Equal(0.75f, NavalInvasionSystem.SeaShare(w, 1, 3), 3);
    }

    [Fact]
    public void NaoSeAssaltaCostaDeQuemNaoCombatemos()
    {
        var w = Islands();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        w.Countries[1].AtWarWith.Clear(); w.Countries[2].AtWarWith.Clear();
        Assert.Contains("combatemos", new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Validate(w)!);
    }

    [Fact]
    public void NaoCabemMaisDoQueNavalInvasionMaxDivsNaMesmaPraia()
    {
        var w = Islands();
        w.Rules["naval_invasion_max_divs"] = 2;
        for (int i = 1; i <= 3; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 2);
        Assert.Contains("cabem 2", new PlanNavalInvasionCommand(1, 3, new List<int> { 1, 2, 3 }).Validate(w)!);

        new PlanNavalInvasionCommand(1, 3, new List<int> { 1, 2 }).Execute(w);
        // e engrossar a que já lá está também não passa a lotação
        Assert.Contains("cabem 2", new PlanNavalInvasionCommand(1, 3, new List<int> { 3 }).Validate(w)!);
    }

    [Fact]
    public void ATropaTodaEmbarcaDoMesmoCais()
    {
        var w = Islands();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1);                // esta está na retaguarda
        Assert.Contains("mesmo cais", new PlanNavalInvasionCommand(1, 3, new List<int> { 1, 2 }).Validate(w)!);
    }

    // ── o desembarque, que já cá estava e continua a valer ──────────────────────────────────────────

    [Fact]
    public void Landing_CostsOrganisation()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        Assault(w, 3, 1);
        Sail(w, new MovementSystem());
        Assert.Equal(3, d.RegionId);
        Assert.Equal(100f - w.Rule("naval_invasion_org_cost"), d.Org, 3);
        Assert.False(d.Seaborne, "a marca do assalto é de uma viagem só");
    }

    [Fact]
    public void TiredDivision_TurnsBackInsteadOfLanding()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2, org: 30f);   // abaixo de naval_invasion_min_org
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 3);                    // praia defendida
        var events = new List<IGameEvent>();
        w.Events.Subscribe<LandingAborted>(events.Add);
        Assault(w, 3, 1);

        Sail(w, new MovementSystem());

        Assert.Equal(2, d.RegionId);
        Assert.Empty(d.Path);
        Assert.Empty(w.ActiveBattles);
        Assert.Equal(new IGameEvent[] { new LandingAborted(1, 3) }, events);
    }

    [Fact]
    public void OnlyMaxDivsAssaultTheSameBeach()
    {
        var w = Islands();
        w.Rules["naval_invasion_max_divs"] = 2;
        for (int i = 1; i <= 2; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 2);
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);
        Assault(w, 3, 1, 2);

        Sail(w, new MovementSystem());

        var b = Assert.Single(w.ActiveBattles);
        Assert.Equal(2, b.Attackers.Count);
    }

    [Fact]
    public void WaitingDivisionsLandAfterTheBeachClears()
    {
        var w = Islands();
        for (int i = 1; i <= 2; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 2);
        var def = TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);
        Assault(w, 3, 1, 2);
        w.Rules["naval_invasion_max_divs"] = 1;   // a praia estreitou depois de a operação largar
        var sys = new MovementSystem();
        Sail(w, sys);
        Assert.Single(w.ActiveBattles[0].Attackers);

        w.Regions[3].DivisionIds.Remove(def.Id); w.Divisions.Remove(def.Id);   // defensor sai de cena
        w.ActiveBattles.Clear();
        for (int i = 0; i < 3; i++) sys.Tick(w);

        Assert.All(w.Divisions.Values.Where(d => d.CountryId == 1), d => Assert.Equal(3, d.RegionId));
    }

    [Fact]
    public void AttackerFromTheSea_FightsWeaker()
    {
        var w = Islands();
        var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);   // ainda do outro lado da travessia
        Assert.Equal(w.Rule("naval_invasion_penalty"), CombatSystem.AmphibiousMult(w, att, w.Regions[3]), 3);
    }

    [Fact]
    public void DefenderAndLandAttacker_KeepFullStrength()
    {
        var w = Islands();
        var def = TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 3);
        Assert.Equal(1f, CombatSystem.AmphibiousMult(w, def, w.Regions[3]), 3);   // quem defende a praia
        var land = TestWorld.AddDivision(w, 8, 2, TestWorld.Inf2, 4);
        Assert.Equal(1f, CombatSystem.AmphibiousMult(w, land, w.Regions[3]), 3);  // e quem ataca por terra
    }

    [Fact]
    public void LandMove_IsNotTreatedAsALanding()
    {
        var w = Islands();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        new MoveDivisionCommand(1, 1, 2).Execute(w);
        var sys = new MovementSystem();
        for (int i = 0; i < 40 && d.RegionId != 2; i++) sys.Tick(w);
        Assert.Equal(2, d.RegionId);
        Assert.Equal(100f, d.Org, 3);   // atravessar por terra não desembarca ninguém
    }

    [Fact]
    public void OMundoGuardaAOperacaoEmPreparacao()
    {
        var w = Islands();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        new PlanNavalInvasionCommand(1, 3, new List<int> { 1 }).Execute(w);
        var inv = w.NavalInvasions.Single();
        Assert.Equal(2, inv.FromId);
        Assert.Equal(w.Clock.Day, inv.SinceDay);
        Assert.Contains("R3", inv.Name);
        Assert.Contains("R3", NavalInvasionSystem.Short(w, 1));
        Assert.Equal("nenhuma operação anfíbia", NavalInvasionSystem.Short(w, 2));
    }
}
