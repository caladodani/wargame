using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Grupos de exércitos com frente atribuída. Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2):
/// um grupo do país 1 posto na região 1, com frente no país 2, tem de atravessar 2 e 3 até dar de caras
/// com a região 4. Os limites vêm das regras army_group_* da base de dados.</summary>
public class ArmyGroupTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ArmyGroupSystem());
        w.Register(new MovementSystem());          // sem ele o w.Tick() não anda com as divisões
        w.Rules["army_group_order_days"] = 1f;    // uma ordem por dia: os testes andam em poucos ticks
        return w;
    }

    /// <summary>Grupo do país 1 com a frente no país 2 e uma divisão na região `at`.</summary>
    private static (ArmyGroup g, Division d) Group(World w, int at, GroupStance stance = GroupStance.Advance)
    {
        new CreateArmyGroupCommand(1).Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, at);
        new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
        new SetArmyGroupFrontCommand(1, g.Id, 2).Execute(w);
        if (stance != GroupStance.Hold) new SetArmyGroupStanceCommand(1, g.Id, stance).Execute(w);
        return (g, d);
    }




    [Fact]
    public void AnAdvancingGroup_MarchesAcrossTheMapToTheFront()
    {
        var w = Build();
        w.StartWar(1, 2);
        var (_, d) = Group(w, at: 1);
        for (int i = 0; i < 400 && d.RegionId != 4; i++) w.Tick();

        Assert.Equal(4, d.RegionId);   // atravessou 2 e 3 até entrar em território inimigo
    }












    /// <summary>Mapa em Y a partir do hub 1: um braço curto 2→3 (inimigo a 2 saltos) e um braço comprido
    /// 4→5→6 (inimigo a 4 saltos). Serve só para testar a âncora — TheatreSystem já tem os seus próprios
    /// testes de segmentação.</summary>
    private static World YMap()
    {
        var (w, _) = TestWorld.Build();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 6, Manpower = 1e9f };
        Region R(int id, int owner) => new Region
        {
            Id = id, Name = "R" + id, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner,
            Terrain = "plain", Population = 10_000_000, CenterX = id * 100, CenterY = 0,
        };
        var r1 = R(1, 1); var r2 = R(2, 1); var r3 = R(3, 2);
        var r4 = R(4, 1); var r5 = R(5, 1); var r6 = R(6, 2);
        r1.Neighbours.AddRange(new[] { 2, 4 });
        r2.Neighbours.AddRange(new[] { 1, 3 });
        r3.Neighbours.Add(2);
        r4.Neighbours.AddRange(new[] { 1, 5 });
        r5.Neighbours.AddRange(new[] { 4, 6 });
        r6.Neighbours.Add(5);
        foreach (var r in new[] { r1, r2, r3, r4, r5, r6 }) w.Regions[r.Id] = r;
        w.Register(new ArmyGroupSystem());
        w.Register(new MovementSystem());
        w.Rules["army_group_order_days"] = 1f;
        w.StartWar(1, 2);
        return w;
    }





}
