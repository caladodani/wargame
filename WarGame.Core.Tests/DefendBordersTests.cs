using WarGame.Core.Commands;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>DefendBordersCommand: divisões paradas vão para as regiões de fronteira, equilibradas.</summary>
public class DefendBordersTests
{
    [Fact]
    public void IdleDivisions_SpreadAcrossFront()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, 6, 3);   // 1-3 país 1, 4-6 país 2; fronteira do 1 = região 3
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        var a = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var b = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2);
        Assert.Null(new DefendBordersCommand(1).Validate(w));
        new DefendBordersCommand(1).Execute(w);
        Assert.Equal(3, a.DestinationRegionId);
        Assert.Equal(3, b.DestinationRegionId);
    }

    [Fact]
    public void NoWar_Rejected_AndFrontDivisionsStay()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, 6, 3);
        Assert.NotNull(new DefendBordersCommand(1).Validate(w));
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);   // já na fronteira
        new DefendBordersCommand(1).Execute(w);
        Assert.Empty(d.Path);
    }
}
