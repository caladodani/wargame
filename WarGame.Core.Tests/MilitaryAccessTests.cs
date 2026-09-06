using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Acesso militar entre aliados de facção: FindPath atravessa, MovementSystem entra,
/// recuo pode cair em região aliada. Mapa linear 1-2-3 (país 1) / 4-5-6 (país 2).</summary>
public class MilitaryAccessTests
{
    private static World Build(bool allied)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        if (allied)
        {
            w.CreateFaction("fx_1", "Pacto de Teste", "");
            w.Factions["fx_1"].Members.Add(1);
            w.Factions["fx_1"].Members.Add(2);
        }
        w.Register(new MovementSystem());
        return w;
    }

    [Fact]
    public void FindPath_CrossesAllyTerritory_OnlyWhenAllied()
    {
        var wNo = Build(allied: false);
        Assert.Null(MoveDivisionCommand.FindPath(wNo, 1, 6, 1));

        var wYes = Build(allied: true);
        var path = MoveDivisionCommand.FindPath(wYes, 1, 6, 1);
        Assert.NotNull(path);
        Assert.Equal(6, path![^1]);
    }

    [Fact]
    public void Division_EntersAllyRegion()
    {
        var w = Build(allied: true);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var cmd = new MoveDivisionCommand(1, 1, 4);
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);
        TestWorld.Days(w, 30);
        Assert.Equal(4, d.RegionId);
    }

    [Fact]
    public void NotAllied_StopsAtBorder()
    {
        var w = Build(allied: false);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        Assert.NotNull(new MoveDivisionCommand(1, 1, 4).Validate(w));   // sem caminho legal
        d.SetPath(new[] { 4 });   // forçado: o sistema pára à fronteira
        TestWorld.Days(w, 30);
        Assert.Equal(3, d.RegionId);
        Assert.Empty(d.Path);
    }

    [Fact]
    public void Supply_FlowsInAllyTerritory()
    {
        var w = Build(allied: true);
        w.Register(new SupplySystem());
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);   // em terra do aliado 2
        TestWorld.Days(w, 1);
        Assert.Equal(1f, d.Supply, 0.001f);

        var w2 = Build(allied: false);
        w2.Register(new SupplySystem());
        var d2 = TestWorld.AddDivision(w2, 1, 1, TestWorld.Inf, 4);
        TestWorld.Days(w2, 1);
        Assert.Equal(w2.Rule("supply_pocket", 0.5f), d2.Supply, 0.001f);
    }

    [Fact]
    public void Ai_SendsExpedition_ToAllyFront()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.CreateFaction("fx_1", "Pacto de Teste", "");
        w.Factions["fx_1"].Members.Add(1);
        w.Factions["fx_1"].Members.Add(2);
        // País 3 inimigo dos dois, controla a região 6: frente do aliado 2 é a 5. O país 1 não tem frente.
        var c3 = new Country { Id = 3, Tag = "EN3", Name = "Inimigo" };
        w.Countries[3] = c3;
        w.Countries[1].AtWarWith.Add(3); w.Countries[2].AtWarWith.Add(3);
        c3.AtWarWith.Add(1); c3.AtWarWith.Add(2);
        w.Regions[6].ControllerId = 3;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        w.Register(new AiSystem());
        TestWorld.Days(w, 1);   // a IA corre no dia 0
        Assert.Equal(5, d.DestinationRegionId);
    }

    [Fact]
    public void Retreat_FallsBackToAllyRegion()
    {
        var w = Build(allied: true);
        // País 3 (inimigo do 1) controla a região 3; divisão do 1 fica lá exposta,
        // sem vizinho próprio (2 também é do 3) — recua para a 4, do aliado 2.
        var c3 = new Country { Id = 3, Tag = "EN3", Name = "Inimigo" };
        w.Countries[3] = c3;
        w.Countries[1].AtWarWith.Add(3); c3.AtWarWith.Add(1);
        w.Regions[2].ControllerId = 3; w.Regions[3].ControllerId = 3;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        TestWorld.Days(w, 1);
        Assert.Equal(4, d.RegionId);
    }
}
