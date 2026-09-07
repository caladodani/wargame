using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Escadas de postos nacionais (general_rank.country_tag). A tabela de postos era uma só para o
/// mundo inteiro: um comandante alemão chamava-se "Marechal do Reino" e um americano "General de Divisão",
/// que é nome que nenhum dos dois exércitos usa.
///
/// Agora cada país traz a escada dele — três armas, cinco degraus cada, com os nomes da tradição — e sobe
/// só por ela; quem não trouxer nenhuma continua na comum. O que a escada nacional muda é o NOME: os
/// limiares e os bónus são os mesmos, para nenhum país subir mais depressa por ter melhores palavras.
///
/// Lêem a base a sério (data/static.db), que é onde as escadas vivem.</summary>
public class NationalRankTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    /// <summary>Um comandante daquela arma, contratado ou não — o posto mede-se pela experiência dele.</summary>
    private static string SomeGeneral(World w, string domain) =>
        w.GeneralDefs.Values.First(g => g.Domain == domain && g.CountryTag is null).Id;

    [Fact]
    public void EveryCountryThatBringsALadderBringsTheThreeArms()
    {
        var w = FactionTests.BuildReal();
        var tags = w.GeneralRanks.Where(r => r.CountryTag is not null).Select(r => r.CountryTag!).Distinct().ToList();
        Assert.Equal(28, tags.Count);
        foreach (string tag in tags)
        {
            var c = ByTag(w, tag);
            foreach (string domain in World.Domains)
            {
                Assert.True(w.HasOwnRanks(c, domain), $"{tag} não tem escada de {domain}");
                var ranks = w.Ranks(domain, c);
                Assert.Equal(5, ranks.Count);
                Assert.All(ranks, r => Assert.Equal(tag, r.CountryTag));
                Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ranks.Select(r => r.Level));
                Assert.Equal(ranks.Count, ranks.Select(r => r.Name).Distinct().Count());
            }
        }
        Assert.Equal(28 * 15, w.GeneralRanks.Count(r => r.CountryTag is not null));
    }

    [Fact]
    public void TheHomeLadderChangesTheNameAndNotTheBalance()
    {
        var w = FactionTests.BuildReal();
        foreach (string tag in w.GeneralRanks.Where(r => r.CountryTag is not null).Select(r => r.CountryTag!).Distinct())
        {
            var c = ByTag(w, tag);
            foreach (string domain in World.Domains)
            {
                var own = w.Ranks(domain, c);
                var common = w.Ranks(domain);
                Assert.Equal(common.Count, own.Count);
                for (int i = 0; i < own.Count; i++)
                {
                    Assert.Equal(common[i].Xp, own[i].Xp, 3);          // pede o mesmo
                    Assert.Equal(common[i].Bonus, own[i].Bonus, 3);    // e vale o mesmo
                }
                // e tem alguma coisa de nacional: uma escada com os nomes da comum não era escada nenhuma
                Assert.NotEqual(common.Select(r => r.Name), own.Select(r => r.Name));
            }
        }
    }

    [Fact]
    public void NobodyWearsAnotherCountrysRank()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var deu = ByTag(w, "DEU");

        foreach (string domain in World.Domains)
            Assert.All(w.Ranks(domain, prt), r => Assert.True(World.RankIsFor(r, prt)));

        var german = w.GeneralRanks.First(r => r.CountryTag == "DEU");
        Assert.False(World.RankIsFor(german, prt));
        Assert.True(World.RankIsFor(german, deu));
        Assert.DoesNotContain(w.Ranks(german.Domain, prt), r => r.Name == german.Name);
    }

    [Fact]
    public void TheSameCampaigningGivesEachCountryItsOwnTitle()
    {
        var w = FactionTests.BuildReal();
        string sea = SomeGeneral(w, World.Sea), land = SomeGeneral(w, World.Land);
        foreach (var (tag, atSea, onLand) in new[]
                 {
                     ("DEU", "Vizeadmiral", "Generalleutnant"),
                     ("GBR", "Vice-Admiral", "Lieutenant-General"),
                     ("TUR", "Koramiral", "Korgeneral"),
                     ("POL", "Admirał floty", "Generał broni"),
                 })
        {
            var c = ByTag(w, tag);
            c.GeneralXp[sea] = c.GeneralXp[land] = 120f;               // o mesmo homem, a mesma campanha
            Assert.Equal(atSea, w.RankOf(c.Id, sea)!.Name);
            Assert.Equal(onLand, w.RankOf(c.Id, land)!.Name);
            Assert.Equal(3, w.RankOf(c.Id, land)!.Level);              // o degrau é o mesmo: muda o nome
        }
    }

    [Fact]
    public void TheNextRankIsTheHomeOneToo()
    {
        var w = FactionTests.BuildReal();
        var ita = ByTag(w, "ITA");
        string land = SomeGeneral(w, World.Land);
        ita.GeneralXp[land] = 10f;

        Assert.Equal("Generale di Brigata", w.RankOf(ita.Id, land)!.Name);
        Assert.Equal("Generale di Divisione", w.NextRank(ita.Id, land)!.Name);
        Assert.Equal(w.Ranks(World.Land)[1].Xp, w.NextRank(ita.Id, land)!.Xp, 3);
    }

    [Fact]
    public void ACountryWithoutItsOwnLadderClimbsTheCommonOne()
    {
        var (w, _) = TestWorld.Build();                                 // sem data/countries: só a escada comum
        TestWorld.LinearMap(w);
        var c = w.Countries[1];
        Assert.False(w.HasOwnRanks(c, World.Land));
        Assert.Equal(w.Ranks(World.Land).Select(r => r.Name), w.Ranks(World.Land, c).Select(r => r.Name));

        string land = SomeGeneral(w, World.Land);
        c.GeneralXp[land] = 120f;
        Assert.Equal("General de Exército", w.RankOf(c.Id, land)!.Name);
    }

    [Fact]
    public void ThePromotionNewsCarriesTheHomeName()
    {
        var w = FactionTests.BuildReal();
        w.Register(new GeneralXpSystem());
        w.Tick();                                                       // liga o sistema ao barramento
        var esp = ByTag(w, "ESP");
        string air = w.GeneralDefs.Values.First(g => g.Domain == World.Air && g.CountryTag is null).Id;
        esp.Generals.Add(air);
        var promotions = new List<GeneralPromoted>();
        w.Events.Subscribe<GeneralPromoted>(promotions.Add);
        w.AirMissions.Add(new AirMission { CountryId = esp.Id, RegionId = 1, MissionId = "superioridade", Wings = 40f });

        var second = w.Ranks(World.Air, esp)[1];
        for (int i = 0; i < 60 && esp.GeneralXp.GetValueOrDefault(air) < second.Xp; i++) w.Tick();

        Assert.Equal("General de División del Aire", second.Name);
        Assert.Equal(second.Name, Assert.Single(promotions, p => p.GeneralId == air).RankName);
    }
}
