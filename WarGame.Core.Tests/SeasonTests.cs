using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Estações do ano: marcha, recomposição e desgaste. O mundo de teste anda com tempo neutro, por
/// isso cada teste põe a estação que quer (TestWorld.Season). Os números vêm do seed — o que se mede é a
/// direcção: no Inverno anda-se pior, refaz-se pior e gasta-se em campo aberto.</summary>
public class SeasonTests
{
    private static World Setup(string? season = null)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        if (season is not null) TestWorld.Season(w, season);
        return w;
    }




    [Fact]
    public void WinterSlowsTheColumns_SummerSpeedsThemUp()
    {
        int Hops(string season)
        {
            var w = Setup(season);
            var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
            d.SetPath(new[] { 2 });
            w.Register(new MovementSystem());
            int days = 0;
            while (d.RegionId != 2 && days < 400) { w.Tick(); days++; }
            return days;
        }

        int winter = Hops("inverno"), summer = Hops("verao");
        Assert.True(winter > summer, $"inverno {winter} dias, verão {summer}");
    }






}
