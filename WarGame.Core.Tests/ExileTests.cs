using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Governos no exílio: quem embarca, o que a legitimidade faz, quem muda de asilo, quem se dissolve
/// e o que volta com o governo no dia em que a capital é libertada. O que se mede é a mecânica — os números
/// são todos da tabela rule.</summary>
public class ExileTests
{
    /// <summary>Mundo em linha: país 1 (regiões 1-3, capital 1) contra o país 2 (regiões 4-6), com o país 3
    /// na mesma facção do 1 e com casa própria (região 7) — é ele quem dá asilo. A terra dele não é
    /// enfeite: um país sem região nenhuma capitula no dia em que entra em guerra, e o anfitrião tem de
    /// poder bater-se sem cair.</summary>
    private static (World w, Country loser, Country winner, Country host) Setup(bool faction = true)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 7, Manpower = 1e9f };
        var home = new Region { Id = 7, Name = "R7", OwnerId = 3, InitialOwnerId = 3, ControllerId = 3,
                                Terrain = "plain", Population = 10_000_000, CenterX = 700, CenterY = 0 };
        home.Neighbours.Add(6); w.Regions[6].Neighbours.Add(7); w.Regions[7] = home;
        if (faction) w.Factions["aliados"] = new Faction("aliados", "Aliados", "", new List<int> { 1, 3 });
        var (a, b, c) = (w.Countries[1], w.Countries[2], w.Countries[3]);
        w.StartWar(1, 2);
        return (w, a, b, c);
    }

    /// <summary>Põe os sistemas a correr pela ordem verdadeira: o ExileSystem liga-se ao barramento antes
    /// de o PeaceSystem publicar a capitulação, como faz o Game.</summary>
    private static ExileSystem Systems(World w)
    {
        var exile = new ExileSystem();
        exile.Bind(w);
        w.Register(new PeaceSystem());
        w.Register(exile);
        return exile;
    }

    /// <summary>Faz cair o país 1: o 2 passa a mandar em tudo o que era dele e o PeaceSystem faz o resto.</summary>
    private static void Fall(World w)
    {
        foreach (var r in w.Regions.Values) if (r.OwnerId == 1) r.ControllerId = 2;
        w.Tick();
    }

    [Fact]
    public void AsRegrasVemDaBaseDeDados()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.Rule("exile_legitimacy_start") > 0f);
        Assert.True(w.Rule("exile_legitimacy_per_day") > 0f);
        Assert.True(w.Rule("exile_legitimacy_decay") > 0f);
        Assert.True(w.Rule("exile_return_legitimacy") > 0f);
        Assert.True(w.Rule("exile_return_divisions") >= 1f);
    }

    [Fact]
    public void QuemCaiComAliadoDePeEmbarcaParaOExilio()
    {
        var (w, loser, _, host) = Setup();
        Systems(w);
        var seen = new List<GovernmentExiled>();
        w.Events.Subscribe<GovernmentExiled>(seen.Add);

        Fall(w);

        Assert.True(loser.Capitulated);
        Assert.True(loser.InExile);
        Assert.Equal(host.Id, loser.ExileHostId);
        Assert.Equal(w.Rule("exile_legitimacy_start"), loser.ExileLegitimacy, 3);
        Assert.Equal(loser.Id, Assert.Single(seen).CountryId);
    }

    [Fact]
    public void SemAliadoDePeNaoHaGovernoNenhum()
    {
        var (w, loser, _, _) = Setup(faction: false);
        Systems(w);

        Fall(w);

        Assert.True(loser.Capitulated);
        Assert.False(loser.InExile);
        Assert.Null(loser.ExileHostId);
    }

    /// <summary>A legitimidade é a guerra de quem acolhe: sobe enquanto ele se bate contra quem ocupa a
    /// capital, desce no dia em que essa guerra não existe.</summary>
    [Fact]
    public void ALegitimidadeSobeComAGuerraDoAnfitriaoEDesceSemEla()
    {
        var (w, loser, _, host) = Setup();
        Systems(w);
        Fall(w);
        w.StartWar(host.Id, 2);

        float start = loser.ExileLegitimacy;
        w.Tick();
        Assert.Equal(start + w.Rule("exile_legitimacy_per_day"), loser.ExileLegitimacy, 3);

        float fighting = loser.ExileLegitimacy;
        w.EndWar(host.Id, 2);
        w.Tick();
        Assert.Equal(fighting - w.Rule("exile_legitimacy_decay"), loser.ExileLegitimacy, 3);
    }

    [Fact]
    public void SemLegitimidadeNenhumaOGovernoDissolveSe()
    {
        var (w, loser, _, _) = Setup();
        Systems(w);
        Fall(w);
        var seen = new List<ExileEnded>();
        w.Events.Subscribe<ExileEnded>(seen.Add);

        TestWorld.Days(w, (int)(loser.ExileLegitimacy / w.Rule("exile_legitimacy_decay")) + 1);

        Assert.False(loser.InExile);
        Assert.True(loser.Capitulated);          // o país continua caído: o que se apagou foi o governo
        Assert.Equal(loser.Id, Assert.Single(seen).CountryId);
    }

    [Fact]
    public void AnfitriaoQueCaiPassaOGovernoAOutroAliado()
    {
        var (w, loser, _, host) = Setup();
        w.Countries[4] = new Country { Id = 4, Tag = "D", Name = "Delta", CapitalRegionId = 8, Manpower = 1e9f };
        w.Regions[8] = new Region { Id = 8, Name = "R8", OwnerId = 4, InitialOwnerId = 4, ControllerId = 4,
                                    Terrain = "plain", Population = 10_000_000, CenterX = 800, CenterY = 0 };
        w.Factions["aliados"].Members.Add(4);
        Systems(w);
        Fall(w);
        var seen = new List<ExileMoved>();
        w.Events.Subscribe<ExileMoved>(seen.Add);

        host.Capitulated = true;
        w.Tick();

        Assert.Equal(4, loser.ExileHostId);
        Assert.Equal(host.Id, Assert.Single(seen).OldHostId);
    }

    /// <summary>O regresso: capital libertada por mão amiga e legitimidade feita. As regiões de origem que a
    /// facção tem na mão voltam ao dono e o governo traz o exército de exílio.</summary>
    [Fact]
    public void CapitalLibertadaComLegitimidadeDevolveOPaisEOExercito()
    {
        var (w, loser, _, host) = Setup();
        Systems(w);
        Fall(w);
        loser.ExileLegitimacy = 1f;
        foreach (var r in w.Regions.Values) if (r.InitialOwnerId == 1) r.ControllerId = host.Id;
        var seen = new List<GovernmentReturned>();
        w.Events.Subscribe<GovernmentReturned>(seen.Add);

        w.Tick();

        Assert.False(loser.Capitulated);
        Assert.False(loser.InExile);
        Assert.Null(loser.CapitulatedDay);
        Assert.Equal(3, w.Regions.Values.Count(r => r.OwnerId == 1 && r.ControllerId == 1));
        var e = Assert.Single(seen);
        Assert.Equal(host.Id, e.LiberatorId);
        Assert.Equal(3, e.Regions);
        Assert.Equal((int)w.Rule("exile_return_divisions"), e.Divisions);
        Assert.Equal(e.Divisions, w.Divisions.Values.Count(d => d.CountryId == 1));
    }

    [Fact]
    public void SemLegitimidadeFeitaACapitalLibertadaNaoDevolveNada()
    {
        var (w, loser, _, host) = Setup();
        Systems(w);
        Fall(w);
        loser.ExileLegitimacy = w.Rule("exile_return_legitimacy") - 0.1f;
        foreach (var r in w.Regions.Values) if (r.InitialOwnerId == 1) r.ControllerId = host.Id;

        w.Tick();

        Assert.True(loser.Capitulated);
        Assert.True(loser.InExile);
    }

    /// <summary>Só volta o que a facção tem na mão: a região de origem que o inimigo ainda ocupa fica com
    /// ele — libertar essa é outro dia de guerra.</summary>
    [Fact]
    public void ARegiaoQueOInimigoAindaOcupaNaoVolta()
    {
        var (w, loser, winner, host) = Setup();
        Systems(w);
        Fall(w);
        loser.ExileLegitimacy = 1f;
        foreach (var r in w.Regions.Values) if (r.InitialOwnerId == 1) r.ControllerId = host.Id;
        w.Regions[3].ControllerId = winner.Id;

        w.Tick();

        Assert.False(loser.Capitulated);
        Assert.Equal(2, w.Regions.Values.Count(r => r.OwnerId == 1));
        Assert.Equal(winner.Id, w.Regions[3].ControllerId);
    }

    /// <summary>Um governo no exílio não volta em guerra: as guerras dele acabaram no dia em que caiu, e o
    /// regresso não reabre nenhuma — senão o país voltava e capitulava outra vez no dia seguinte.</summary>
    [Fact]
    public void OGovernoVoltaEmPaz()
    {
        var (w, loser, _, host) = Setup();
        Systems(w);
        Fall(w);
        loser.ExileLegitimacy = 1f;
        foreach (var r in w.Regions.Values) if (r.InitialOwnerId == 1) r.ControllerId = host.Id;

        w.Tick();
        w.Tick();

        Assert.Empty(loser.AtWarWith);
        Assert.False(loser.Capitulated);
    }

    /// <summary>O exílio vai ao save: um governo fora de casa é estado que se perde se ninguém o escrever, e
    /// quem o carrega tem de encontrar a mesma casa, o mesmo dia e a mesma legitimidade.</summary>
    [Fact]
    public void OExilioSobreviveAoSaveEAoCarregamento()
    {
        var (w, loser, _, host) = Setup();
        Systems(w);
        Fall(w);
        loser.ExileLegitimacy = 0.42f;

        using var save = new MsSqliteDatabase();
        var staticDb = new MsSqliteDatabase();
        staticDb.ExecuteScript(File.ReadAllText("data/schema.sql"));
        SqlWorldRepository.EnsureSaveSchema(save, File.ReadAllText("data/schema.sql"));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        w2.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 7 };
        repo.LoadSave(w2, save);

        var back = w2.Countries[1];
        Assert.True(back.InExile);
        Assert.Equal(host.Id, back.ExileHostId);
        Assert.Equal(loser.ExileDay, back.ExileDay);
        Assert.Equal(0.42f, back.ExileLegitimacy, 3);
    }
}
