using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A carreira do ar e do mar. Os postos existiam só para a infantaria: a tabela general_rank
/// tinha uma escada única e o GeneralXpSystem só dava experiência a quem comandava um grupo de exércitos,
/// por isso um almirante comprado ficava Capitão-Tenente até ao fim do mundo e um chefe de caça chamava-se
/// "Brigadeiro" na folha do estado-maior.
///
/// Agora cada arma tem a sua escada (general_rank.domain) e a sua maneira de ganhar o posto: em terra ao
/// acontecimento (batalhas e regiões tomadas), no ar e no mar ao dia e pelo que está destacado — cada asa
/// no céu e cada navio no mar contam para quem manda naquela arma. Comprar aviões e deixá-los no hangar
/// não faz marechais.</summary>
public class CommanderCareerTests
{
    private const int Player = 1;

    private static (World w, Country c) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[Player];
        c.Money = 10_000f; c.AirXp = 400f; c.NavyXp = 400f; c.ArmyXp = 400f;
        w.Register(new GeneralXpSystem());
        w.Tick();                                  // liga o sistema ao barramento
        return (w, c);
    }

    private static string Hire(World w, Country c, string generalId)
    {
        var cmd = new HireGeneralCommand(c.Id, generalId);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        return generalId;
    }

    private static void Fly(World w, Country c, float wings) =>
        w.AirMissions.Add(new AirMission { CountryId = c.Id, RegionId = 1, MissionId = "superioridade", Wings = wings });

    private static void Sail(World w, Country c, float ships) =>
        w.NavalMissions.Add(new NavalMission { CountryId = c.Id, RegionId = 1, MissionId = "patrulha", Ships = ships });

    [Fact]
    public void EachArmHasItsOwnLadder()
    {
        var (w, _) = Build();
        var names = new List<string>();
        foreach (string domain in World.Domains)
        {
            var ranks = w.Ranks(domain);
            Assert.Equal(5, ranks.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ranks.Select(r => r.Level));
            Assert.Equal(0f, ranks[0].Xp);
            Assert.Equal(0f, ranks[0].Bonus);              // contratado nasce no degrau de baixo, sem bónus
            for (int i = 1; i < ranks.Count; i++)
            {
                Assert.True(ranks[i].Xp > ranks[i - 1].Xp, $"{domain}: {ranks[i].Name} não pede mais do que o anterior");
                Assert.True(ranks[i].Bonus > ranks[i - 1].Bonus, $"{domain}: {ranks[i].Name} não vale mais do que o anterior");
            }
            names.AddRange(ranks.Select(r => r.Name));
        }
        Assert.Equal(15, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());   // um posto do mar não se chama como um de terra
    }

    [Fact]
    public void AnAdmiralIsNotABrigadier()
    {
        var (w, c) = Build();
        string sea = Hire(w, c, "gen_mar_esquadra");
        string air = Hire(w, c, "gen_ar_caca");
        string land = Hire(w, c, w.GeneralDefs.Values.First(g => g.Domain == World.Land && g.CountryTag is null).Id);
        c.GeneralXp[sea] = c.GeneralXp[air] = c.GeneralXp[land] = 120f;

        Assert.Equal("Contra-Almirante", w.RankOf(Player, sea)!.Name);
        Assert.Equal("Vice-Almirante", w.NextRank(Player, sea)!.Name);
        Assert.Equal("Comandante de Grupo", w.RankOf(Player, air)!.Name);
        Assert.Equal("General de Exército", w.RankOf(Player, land)!.Name);
        // a escada é outra, mas o degrau é o mesmo: o bónus do comando não muda com a arma
        Assert.Equal(w.RankBonus(Player, land), w.RankBonus(Player, sea), 3);
    }

    [Fact]
    public void ADayOfMissionsFeedsTheWingCommanderByTheWingsInTheSky()
    {
        var (w, c) = Build();
        string air = Hire(w, c, "gen_ar_caca");
        Fly(w, c, 6f);

        w.Tick();

        Assert.Equal(6f * w.Rule("general_xp_air_day", 0.15f), c.GeneralXp[air], 3);
    }

    [Fact]
    public void WhatFliesForOneArmDoesNotPromoteTheOther()
    {
        var (w, c) = Build();
        string air = Hire(w, c, "gen_ar_caca");
        string sea = Hire(w, c, "gen_mar_esquadra");
        string land = Hire(w, c, w.GeneralDefs.Values.First(g => g.Domain == World.Land && g.CountryTag is null).Id);
        Fly(w, c, 4f);
        Sail(w, c, 10f);

        w.Tick();

        Assert.Equal(4f * w.Rule("general_xp_air_day", 0.15f), c.GeneralXp[air], 3);
        Assert.Equal(10f * w.Rule("general_xp_sea_day", 0.15f), c.GeneralXp[sea], 3);
        Assert.False(c.GeneralXp.ContainsKey(land));      // a guerra aérea não faz marechais de infantaria
    }

    [Fact]
    public void PlanesLeftAtHomeMakeNobodyAMarshal()
    {
        var (w, c) = Build();
        string air = Hire(w, c, "gen_ar_caca");
        c.AirPower = 40f; c.Warships = 40f;               // comprado e no hangar: não é guerra nenhuma

        for (int i = 0; i < 20; i++) w.Tick();

        Assert.False(c.GeneralXp.ContainsKey(air));
        Assert.Equal(1, w.RankOf(Player, air)!.Level);
    }

    [Fact]
    public void EnoughDaysAtSeaPromoteTheAdmiral_AndThatIsNews()
    {
        var (w, c) = Build();
        string sea = Hire(w, c, "gen_mar_esquadra");
        var promotions = new List<GeneralPromoted>();
        w.Events.Subscribe<GeneralPromoted>(promotions.Add);
        Sail(w, c, 20f);

        var second = w.Ranks(World.Sea)[1];
        for (int i = 0; i < 60 && c.GeneralXp.GetValueOrDefault(sea) < second.Xp; i++) w.Tick();

        Assert.Equal(second.Level, w.RankOf(Player, sea)!.Level);
        var promo = Assert.Single(promotions);
        Assert.Equal(sea, promo.GeneralId);
        Assert.Equal(second.Name, promo.RankName);        // "Capitão de Mar e Guerra", não "General de Divisão"
    }

    [Fact]
    public void ACommanderInHospitalDoesNotCountTheDay()
    {
        var (w, c) = Build();
        string air = Hire(w, c, "gen_ar_caca");
        c.GeneralWound[air] = w.Clock.Day + 10;
        Fly(w, c, 8f);

        w.Tick();

        Assert.False(c.GeneralXp.ContainsKey(air));
    }

    [Fact]
    public void TheCareerStopsAtTheCeiling()
    {
        var (w, c) = Build();
        string sea = Hire(w, c, "gen_mar_esquadra");
        w.Rules["general_xp_max"] = 5f;
        Sail(w, c, 30f);

        for (int i = 0; i < 15; i++) w.Tick();

        Assert.Equal(5f, c.GeneralXp[sea], 3);
    }
}
