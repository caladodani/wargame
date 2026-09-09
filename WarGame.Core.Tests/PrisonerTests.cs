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











}
