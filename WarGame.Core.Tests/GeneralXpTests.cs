using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Experiência de campanha dos comandantes destacados: o que se mede é a promoção pelas
/// batalhas do grupo e o que ela vale em campo, não o combate em si — por isso os acontecimentos do
/// combate entram pelo barramento, que é exactamente o que o GeneralXpSystem ouve.</summary>
public class GeneralXpTests
{
    private const int Player = 1, Foe = 2;

    private static (World w, ArmyGroup g, GeneralDef def, Division d) Build(string statKey = "defense")
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[Player].Money = 10_000f;
        w.Countries[Player].AtWarWith.Add(Foe);
        w.Countries[Foe].AtWarWith.Add(Player);
        var def = w.GeneralDefs.Values.First(x => x.StatKey == statKey);
        new HireGeneralCommand(Player, def.Id).Execute(w);
        new CreateArmyGroupCommand(Player, "1.º Exército").Execute(w);
        var g = w.ArmyGroups.Values.Single();
        var d = TestWorld.AddDivision(w, 1, Player, TestWorld.Inf, 3);
        new AssignDivisionCommand(Player, d.Id, g.Id).Execute(w);
        new AssignGeneralCommand(Player, g.Id, def.Id).Execute(w);
        w.Register(new GeneralXpSystem());
        w.Tick();                       // liga o sistema ao barramento
        return (w, g, def, d);
    }

    private static void Battle(World w, int regionId, bool weWon) =>
        w.Events.Publish(new BattleEnded(regionId, weWon, Player, Foe));

    [Fact]
    public void TheGroupsBattles_FeedTheCommanderAndOnlyHim()
    {
        var (w, _, def, _) = Build();
        var other = w.GeneralDefs.Values.First(x => x.Id != def.Id);

        Battle(w, 3, weWon: true);

        Assert.Equal(w.Rule("general_xp_battle", 2f) + w.Rule("general_xp_win", 3f), w.Countries[Player].GeneralXp[def.Id], 3);
        Assert.False(w.Countries[Player].GeneralXp.ContainsKey(other.Id));
    }

    [Fact]
    public void ALostBattleStillTeachesSomething_ButLessThanAWonOne()
    {
        var (w, _, def, _) = Build();
        Battle(w, 3, weWon: false);
        float lost = w.Countries[Player].GeneralXp[def.Id];

        Battle(w, 3, weWon: true);
        float after = w.Countries[Player].GeneralXp[def.Id];

        Assert.Equal(w.Rule("general_xp_battle", 2f), lost, 3);
        Assert.True(after - lost > lost, $"perdida {lost}, ganha {after - lost}");
    }

    [Fact]
    public void ABattleWhereTheGroupHasNoDivisions_GivesNothing()
    {
        var (w, _, def, _) = Build();
        Battle(w, 5, weWon: true);       // a divisão do grupo está na região 3
        Assert.False(w.Countries[Player].GeneralXp.ContainsKey(def.Id));
    }

    [Fact]
    public void OneGroupCountsOnce_HoweverManyDivisionsItHasThere()
    {
        var (w, g, def, _) = Build();
        var second = TestWorld.AddDivision(w, 2, Player, TestWorld.Inf, 3);
        var third = TestWorld.AddDivision(w, 3, Player, TestWorld.Inf, 3);
        new AssignDivisionCommand(Player, second.Id, g.Id).Execute(w);
        new AssignDivisionCommand(Player, third.Id, g.Id).Execute(w);

        Battle(w, 3, weWon: true);

        Assert.Equal(w.Rule("general_xp_battle", 2f) + w.Rule("general_xp_win", 3f), w.Countries[Player].GeneralXp[def.Id], 3);
    }

    [Fact]
    public void TakingARegion_CountsForTheCommanderOfWhoTookIt()
    {
        var (w, _, def, d) = Build();
        d.RegionId = 4; w.Regions[3].DivisionIds.Remove(d.Id); w.Regions[4].DivisionIds.Add(d.Id);

        w.Events.Publish(new RegionCaptured(4, Foe, Player));

        Assert.Equal(w.Rule("general_xp_capture", 4f), w.Countries[Player].GeneralXp[def.Id], 3);
    }

    [Fact]
    public void EnoughCampaigning_PromotesHim_AndThePromotionIsNews()
    {
        var (w, _, def, _) = Build();
        var ranks = new List<GeneralPromoted>();
        w.Events.Subscribe<GeneralPromoted>(ranks.Add);

        var first = w.RankOf(Player, def.Id);
        Assert.NotNull(first);
        Assert.Equal(1, first!.Level);           // contratado nasce no posto mais baixo

        var second = w.Ranks(World.Land)[1];      // a escada da arma dele: o mar e o ar têm as suas
        while (w.Countries[Player].GeneralXp.GetValueOrDefault(def.Id) < second.Xp) Battle(w, 3, weWon: true);

        Assert.Equal(second.Level, w.RankOf(Player, def.Id)!.Level);
        var promo = Assert.Single(ranks);
        Assert.Equal(second.Level, promo.Level);
        Assert.Equal(second.Name, promo.RankName);
        Assert.Equal(def.Id, promo.GeneralId);
    }

    [Fact]
    public void ThePostGrowsWithTheRank_AndTheCountryNeverSeesIt()
    {
        var (w, _, def, d) = Build();
        float before = w.CommandMult(d, def.StatKey);

        var second = w.Ranks(World.Land)[1];      // a escada da arma dele: o mar e o ar têm as suas
        while (w.Countries[Player].GeneralXp.GetValueOrDefault(def.Id) < second.Xp) Battle(w, 3, weWon: true);

        Assert.True(w.CommandMult(d, def.StatKey) > before, $"antes {before}, depois {w.CommandMult(d, def.StatKey)}");
        Assert.Equal(1f + (def.Mult - 1f) * (w.Rule("general_command_bonus", 2f) + second.Bonus), w.CommandMult(d, def.StatKey), 3);
        Assert.Equal(1f, w.Countries[Player].Stat(def.StatKey), 3);   // destacado: o país continua sem ele
    }

    [Fact]
    public void ExperienceStopsAtTheCeiling()
    {
        var (w, _, def, _) = Build();
        w.Rules["general_xp_max"] = 12f;
        for (int i = 0; i < 30; i++) Battle(w, 3, weWon: true);
        Assert.Equal(12f, w.Countries[Player].GeneralXp[def.Id], 3);
    }

    [Fact]
    public void TheRankSurvivesASave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[Player].Money = 10_000f;
        var def = w.GeneralDefs.Values.First(x => x.StatKey == "defense");
        new HireGeneralCommand(Player, def.Id).Execute(w);
        w.Countries[Player].GeneralXp[def.Id] = 150f;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        WarGame.Core.Data.SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new WarGame.Core.Data.SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(150f, w2.Countries[Player].GeneralXp[def.Id], 3);
        Assert.Equal(w.RankOf(Player, def.Id)!.Level, w2.RankOf(Player, def.Id)!.Level);
    }
}
