using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Planos de batalha: um exército com frente atribuída que fica quieto prepara o terreno, e o que
/// preparou vale força de combate. Marchar e bater-se gasta o plano. Mapa em linha 1-2-3 (país 1) | 4-5-6
/// (país 2), como nos testes de grupos.</summary>
public class BattlePlanTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new BattlePlanSystem());
        return w;
    }

    /// <summary>Grupo do país 1 com frente no país 2 e `n` divisões na região 1.</summary>
    private static (ArmyGroup g, List<Division> divs) Group(World w, GroupStance stance = GroupStance.Defend, int n = 1)
    {
        new CreateArmyGroupCommand(1).Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var divs = new List<Division>();
        for (int i = 0; i < n; i++)
        {
            var d = TestWorld.AddDivision(w, 100 + i, 1, TestWorld.Inf, 1);
            new AssignDivisionCommand(1, d.Id, g.Id).Execute(w);
            divs.Add(d);
        }
        new SetArmyGroupFrontCommand(1, g.Id, 2).Execute(w);
        new SetArmyGroupStanceCommand(1, g.Id, stance).Execute(w);
        return (g, divs);
    }









    [Fact]
    public void ThePlanIsWorthForceInTheField()
    {
        var w = Build();
        var (g, divs) = Group(w);
        var loose = TestWorld.AddDivision(w, 300, 1, TestWorld.Inf, 1);

        Assert.Equal(1f, BattlePlanSystem.Bonus(w, loose), 3);   // divisão sem grupo não tem plano nenhum
        Assert.Equal(1f, BattlePlanSystem.Bonus(w, divs[0]), 3);

        g.Planning = 1f;
        Assert.Equal(1f + w.Rule("planning_bonus"), BattlePlanSystem.Bonus(w, divs[0]), 3);
        g.Planning = 0.5f;
        Assert.Equal(1f + w.Rule("planning_bonus") / 2f, BattlePlanSystem.Bonus(w, divs[0]), 3);
        Assert.Equal(1f, BattlePlanSystem.Bonus(w, loose), 3);
    }



}
