using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Nomes próprios de asas e esquadras (formation_name). As divisões têm nome e honras de batalha
/// desde sempre; a aviação e a marinha eram "4 asas em Braga" e mais nada — não havia ninguém naquele céu.
///
/// Agora cada país traz o fundo de nomes dele (três de asa, três de esquadra) e uma formação destacada pega
/// no primeiro que ainda não esteja no ar; esgotado o fundo, é a região que lhe dá o nome. O nome não muda
/// uma única conta do jogo: serve para se saber quem está lá.</summary>
public class FormationNameTests
{
    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    /// <summary>Três regiões quaisquer do mundo a sério, para destacar formações sem repetir o céu.</summary>
    private static List<int> SomeRegions(World w, int n) => w.Regions.Keys.OrderBy(x => x).Take(n).ToList();

    private static World Small()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return w;
    }

    [Fact]
    public void EveryCountryThatBringsNamesBringsBothArms()
    {
        var w = FactionTests.BuildReal();
        var tags = w.FormationNames.Where(n => n.CountryTag is not null).Select(n => n.CountryTag!).Distinct().ToList();
        Assert.Equal(28, tags.Count);

        var common = w.FormationNames.Where(n => n.CountryTag is null).Select(n => n.Name).ToHashSet();
        foreach (string tag in tags)
        {
            var c = ByTag(w, tag);
            foreach (string domain in new[] { World.Air, World.Sea })
            {
                Assert.True(w.HasOwnFormations(c, domain), $"{tag} não tem nomes de {domain}");
                var pool = w.Formations(domain, c);
                Assert.Equal(3, pool.Count);
                Assert.All(pool, n => Assert.Equal(tag, n.CountryTag));
                Assert.All(pool, n => Assert.StartsWith(tag + "_", n.Id));
                Assert.Equal(new[] { 1, 2, 3 }, pool.Select(n => n.Sort));
                Assert.Equal(pool.Count, pool.Select(n => n.Name).Distinct().Count());
                Assert.All(pool, n => Assert.DoesNotContain(n.Name, common));   // nome de casa é nome de casa
            }
        }
    }

    [Fact]
    public void TheFormationTakesTheNextNameFromTheHomePool()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var pool = w.Formations(World.Air, prt).Select(n => n.Name).ToList();
        var skies = SomeRegions(w, 3);

        foreach (int r in skies) AirMissionSystem.Assign(w, prt.Id, r, "superioridade", 2f);
        Assert.Equal(pool, w.AirMissions.Where(m => m.CountryId == prt.Id).Select(m => m.Name));
        Assert.All(w.AirMissions, m => Assert.True(w.IsHomeFormationName(prt, World.Air, m.Name)));
    }

    [Fact]
    public void TheSeaDrawsFromItsOwnPool()
    {
        var w = FactionTests.BuildReal();
        var gbr = ByTag(w, "GBR");
        var seas = SomeRegions(w, 2);

        foreach (int r in seas) NavalMissionSystem.Assign(w, gbr.Id, r, "bloqueio", 3f);
        var names = w.NavalMissions.Where(m => m.CountryId == gbr.Id).Select(m => m.Name).ToList();
        Assert.Equal(w.Formations(World.Sea, gbr).Take(2).Select(n => n.Name), names);
        Assert.All(names, n => Assert.True(w.IsHomeFormationName(gbr, World.Sea, n)));
        // e não vai buscar nomes ao céu: uma esquadra não se chama No. 617 Squadron
        Assert.All(names, n => Assert.False(w.IsHomeFormationName(gbr, World.Air, n)));
    }

    [Fact]
    public void WhenThePoolRunsOutTheRegionNamesTheFormation()
    {
        var w = Small();
        // o país A não traz fundo próprio: pega nos três nomes comuns e depois é a região que baptiza
        var common = w.Formations(World.Air).Select(n => n.Name).ToList();
        Assert.Equal(3, common.Count);

        for (int r = 1; r <= 4; r++) AirMissionSystem.Assign(w, 1, r, "superioridade", 1f);
        var names = w.AirMissions.Select(m => m.Name).ToList();
        Assert.Equal(common, names.Take(3));
        Assert.Equal("Asa de R4", names[3]);
        Assert.False(w.IsHomeFormationName(w.Countries[1], World.Air, names[0]));

        NavalMissionSystem.Assign(w, 1, 5, "bloqueio", 1f);
        Assert.Equal(w.Formations(World.Sea)[0].Name, w.NavalMissions[0].Name);
    }

    [Fact]
    public void ChangingTheTaskKeepsTheName()
    {
        var w = Small();
        AirMissionSystem.Assign(w, 1, 2, "superioridade", 3f);
        string was = w.AirMissions[0].Name;

        AirMissionSystem.Assign(w, 1, 2, "bombardeamento", 1f);       // mesma gente, outra ordem
        Assert.Single(w.AirMissions);
        Assert.Equal(4f, w.AirMissions[0].Wings, 3);
        Assert.Equal(was, w.AirMissions[0].Name);

        NavalMissionSystem.Assign(w, 1, 3, "bloqueio", 2f);
        string sea = w.NavalMissions[0].Name;
        NavalMissionSystem.Assign(w, 1, 3, "escolta", 1f);
        Assert.Equal(sea, w.NavalMissions[0].Name);
    }

    [Fact]
    public void RecallingTheFormationFreesTheNameForTheNextOne()
    {
        var w = Small();
        AirMissionSystem.Assign(w, 1, 1, "superioridade", 2f);
        AirMissionSystem.Assign(w, 1, 2, "superioridade", 2f);
        string first = w.AirMissions[0].Name, second = w.AirMissions[1].Name;
        Assert.NotEqual(first, second);                              // dois céus, dois nomes

        Assert.True(AirMissionSystem.Recall(w, 1, 1));
        AirMissionSystem.Assign(w, 1, 3, "superioridade", 2f);       // o nome do primeiro voltou ao fundo
        Assert.Equal(first, w.AirMissions.Single(m => m.RegionId == 3).Name);
    }

    [Fact]
    public void TwoCountriesInTheSameSkyKeepTheirOwnNames()
    {
        var w = FactionTests.BuildReal();
        var prt = ByTag(w, "PRT");
        var esp = ByTag(w, "ESP");
        int sky = SomeRegions(w, 1)[0];

        AirMissionSystem.Assign(w, prt.Id, sky, "superioridade", 2f);
        AirMissionSystem.Assign(w, esp.Id, sky, "superioridade", 2f);
        string ours = w.AirMissions.Single(m => m.CountryId == prt.Id).Name;
        string theirs = w.AirMissions.Single(m => m.CountryId == esp.Id).Name;
        Assert.NotEqual(ours, theirs);
        Assert.True(w.IsHomeFormationName(prt, World.Air, ours));
        Assert.False(w.IsHomeFormationName(prt, World.Air, theirs));
        Assert.True(w.IsHomeFormationName(esp, World.Air, theirs));
    }

    [Fact]
    public void TheNameSurvivesTheSave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        AirMissionSystem.Assign(w, 1, 2, "superioridade", 3f);
        NavalMissionSystem.Assign(w, 1, 3, "bloqueio", 2f);
        string air = w.AirMissions[0].Name, sea = w.NavalMissions[0].Name;
        Assert.NotEqual("", air);
        Assert.NotEqual("", sea);

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(air, w2.AirMissions.Single().Name);
        Assert.Equal(sea, w2.NavalMissions.Single().Name);
    }

    [Fact]
    public void AnOldSaveGetsItsFormationsBaptisedOnLoad()
    {
        // save feito antes de haver nomes: as missões vêm com a coluna vazia (é o DEFAULT '' da migração)
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        AirMissionSystem.Assign(w, 1, 2, "superioridade", 3f);
        NavalMissionSystem.Assign(w, 1, 3, "bloqueio", 2f);

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);
        save.Execute("UPDATE s_air_mission SET name=''");
        save.Execute("UPDATE s_naval_mission SET name=''");

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(w2.Formations(World.Air)[0].Name, w2.AirMissions.Single().Name);
        Assert.Equal(w2.Formations(World.Sea)[0].Name, w2.NavalMissions.Single().Name);
    }
}
