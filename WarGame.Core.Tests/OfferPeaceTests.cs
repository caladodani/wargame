using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Propor paz branca: recusa com guerra fresca, aceita estagnada ou inimigo desarmado; uti possidetis.</summary>
public class OfferPeaceTests
{
    [Fact]
    public void FreshWar_Rejected_StaleAccepted_UtiPossidetis()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        TestWorld.AddDivision(w, 1, 2, TestWorld.Inf2, 6);   // inimigo armado
        w.Regions[4].ControllerId = 1;                       // país 1 ocupa a região 4 do país 2

        var rejected = new List<PeaceOfferRejected>();
        var white = new List<WhitePeaceSigned>();
        w.Events.Subscribe<PeaceOfferRejected>(rejected.Add);
        w.Events.Subscribe<WhitePeaceSigned>(white.Add);

        var cmd = new OfferPeaceCommand(1, 2);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);                                       // fresca → recusa
        Assert.Single(rejected);
        Assert.True(w.AreAtWar(1, 2));

        TestWorld.Days(w, 70);                                // 70 dias sem progresso > peace_stale_days
        cmd.Execute(w);
        Assert.Single(white);
        Assert.False(w.AreAtWar(1, 2));
        Assert.Equal(1, w.Regions[4].OwnerId);                // anexou o que controlava
    }

    [Fact]
    public void DisarmedEnemy_AcceptsImmediately()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);                                     // país 2 sem divisões
        new OfferPeaceCommand(1, 2).Execute(w);
        Assert.False(w.AreAtWar(1, 2));
    }

    [Fact]
    public void Validate_RequiresWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        Assert.NotNull(new OfferPeaceCommand(1, 2).Validate(w));
    }
}
