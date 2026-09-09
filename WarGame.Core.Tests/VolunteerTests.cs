using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Voluntários: divisões nossas que se batem na guerra de outro sem nos meterem nela. O que aqui se
/// defende é a linha que separa as duas coisas que uma divisão voluntária é ao mesmo tempo — de quem ela
/// obedece hoje (o anfitrião) e de quem ela é (nós, que a pagamos e a enterramos).</summary>
public class VolunteerTests
{
    /// <summary>Três países: 1 é a casa, 2 o anfitrião em guerra, 3 o inimigo dele. A casa não está em
    /// guerra com ninguém — é esse o ponto de mandar voluntários.</summary>
    private static World Setup(int army = 10)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        w.Regions[6].OwnerId = 3; w.Regions[6].ControllerId = 3;
        w.Countries[2].CapitalRegionId = 4;
        w.StartWar(2, 3);
        for (int i = 1; i <= army; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 1);
        w.Register(new VolunteerSystem());
        // a porta da tensão mundial tem teste próprio (OMundoCalmoNaoDeixaMandarVoluntarios); aqui mede-se
        // o que os voluntários fazem depois de a porta estar aberta, e um mundo de seis regiões nunca
        // aquece o bastante para a abrir sozinho
        w.Rules["volunteer_min_tension"] = 0f;
        return w;
    }


    [Fact]
    public void MandarVoluntarios_TrocamDeBandeira_MasContinuamNossas()
    {
        var w = Setup();
        Assert.Null(new SendVolunteersCommand(1, 2, 2).Validate(w));
        new SendVolunteersCommand(1, 2, 2).Execute(w);

        var away = w.Divisions.Values.Where(d => d.IsVolunteer).ToList();
        Assert.Equal(2, away.Count);
        Assert.All(away, d => Assert.Equal(2, d.CountryId));      // obedecem ao anfitrião
        Assert.All(away, d => Assert.Equal(1, d.HomeId));         // e continuam a ser nossas
        Assert.All(away, d => Assert.Equal(4, d.RegionId));       // desembarcam na capital dele
        Assert.All(away, d => Assert.Contains(d.Id, w.Regions[4].DivisionIds));
        Assert.False(w.AreAtWar(1, 3));                           // e nós continuamos fora da guerra
    }













}
