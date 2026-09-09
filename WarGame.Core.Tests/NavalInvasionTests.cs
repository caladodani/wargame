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





    // ── o que a operação exige para largar ──────────────────────────────────────────────────────────








    // ── o desembarque, que já cá estava e continua a valer ──────────────────────────────────────────








}
