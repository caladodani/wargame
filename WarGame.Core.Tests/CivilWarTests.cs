using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O dia em que o país se parte em dois. O golpe era mudo — trocava o partido no poder e o mapa
/// nem piscava; agora, num país com terra que chegue para dois governos, metade das províncias levanta
/// outra bandeira, a tropa que lá está muda de lado e nasce um país com nome, cor e guerra própria.</summary>
public class CivilWarTests
{
    /// <summary>Um país grande (6 províncias) e pelas ruas, com um partido da oposição a passar o limiar.</summary>
    private static (World w, Country c) Ripe(string party = "socialistas", int n = 10, int split = 6)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, n, split);
        var c = w.Countries[1];
        World.SettleParties(w, c);
        c.Party = "liberais";
        foreach (var p in w.PartyDefs.Values) c.Parties[p.Id] = p.Id == party ? 65f : 5f;
        World.NormalizeParties(c);
        c.Stability = 10f;
        return (w, c);
    }

    [Fact]
    public void Cada_partido_que_se_levanta_tem_cara_na_tabela()
    {
        var (w, _) = Ripe();
        Assert.NotEmpty(w.RebelStyles);
        foreach (var s in w.RebelStyles.Values)
        {
            Assert.Contains("{pais}", s.Name);
            Assert.Contains(s.Glyph, GlyphDataTests.Desenhados);
            Assert.StartsWith("#", s.Colour);
            Assert.NotEqual("", s.Cry);
            Assert.Contains(s.PartyId, w.PartyDefs.Keys);
        }
    }

    [Fact]
    public void Num_pais_pequeno_o_golpe_continua_a_ser_so_uma_troca_de_cadeiras()
    {
        var (w, c) = Ripe(n: 4, split: 2);          // duas províncias: não há casa para dois governos
        w.Register(new PartySystem());
        int before = w.Countries.Count;
        string coup = "";
        w.Events.Subscribe<CoupHappened>(e => coup = e.To);
        w.Tick();

        Assert.Equal(before, w.Countries.Count);
        Assert.Equal("socialistas", coup);
        Assert.Equal("socialistas", c.Party);
        Assert.Empty(c.AtWarWith);
    }

    [Fact]
    public void Num_pais_grande_o_golpe_parte_o_pais_e_os_dois_ficam_em_guerra()
    {
        var (w, c) = Ripe();
        w.Register(new PartySystem());
        CivilWarBroke? broke = null;
        w.Events.Subscribe<CivilWarBroke>(e => broke = e);
        int day = w.Clock.Day;
        w.Tick();

        Assert.NotNull(broke);
        var rebel = w.Countries[broke!.RebelId];
        Assert.Equal(c.Id, rebel.RebelOf);
        Assert.Equal("socialistas", rebel.Party);
        Assert.Equal("liberais", c.Party);                       // o governo aguentou-se na capital
        Assert.True(w.AreAtWar(c.Id, rebel.Id));
        Assert.Contains("República Popular", rebel.Name);
        Assert.Contains("Alfa", rebel.Name);
        Assert.Equal("#a52a2a", rebel.Colour);
        Assert.Equal(day, rebel.BornDay);
        Assert.Equal(3, rebel.Tag.Length);
        Assert.Equal(1, w.Countries.Values.Count(x => x.Tag == rebel.Tag));
    }

    [Fact]
    public void A_terra_que_se_levanta_e_a_de_longe_da_capital_e_o_governo_fica_sempre_com_casa()
    {
        var (w, c) = Ripe();
        var rebel = CivilWar.Erupt(w, c, "socialistas")!;
        Assert.NotNull(rebel);

        var mine = w.Regions.Values.Where(r => r.OwnerId == rebel.Id).Select(r => r.Id).OrderBy(x => x).ToList();
        var his = w.Regions.Values.Where(r => r.OwnerId == c.Id).Select(r => r.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 3, 4, 5, 6 }, mine);                // 65% de 6 = 4 províncias, as mais longe de R1
        Assert.Equal(new[] { 1, 2 }, his);
        Assert.All(mine, id => Assert.Equal(rebel.Id, w.Regions[id].ControllerId));
        Assert.Equal(3, rebel.CapitalRegionId);                  // a mais povoada das suas; empate resolve-se pelo id
    }

    [Fact]
    public void A_tropa_que_estava_na_terra_levantada_muda_de_bandeira()
    {
        var (w, c) = Ripe();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);        // fica na capital: continua do governo
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 5);        // está no interior: passa para os rebeldes
        TestWorld.AddDivision(w, 3, 1, TestWorld.Inf, 6);
        var grupo = new ArmyGroup { Id = w.NewArmyGroupId(), CountryId = 1, Name = "Norte" };
        w.ArmyGroups[grupo.Id] = grupo;
        w.JoinGroup(grupo, 2);

        var rebel = CivilWar.Erupt(w, c, "socialistas")!;
        Assert.Equal(c.Id, w.Divisions[1].CountryId);
        Assert.Equal(rebel.Id, w.Divisions[2].CountryId);
        Assert.Equal(rebel.Id, w.Divisions[3].CountryId);
        Assert.Null(w.Divisions[2].GroupId);                     // saiu do exército do governo
        Assert.DoesNotContain(2, grupo.Divisions);
    }

    [Fact]
    public void A_fatia_que_se_levanta_sai_da_popularidade_travada_pelas_regras()
    {
        var (w, c) = Ripe();
        c.Parties["socialistas"] = 100f; World.NormalizeParties(c);
        Assert.Equal(w.Rule("civil_war_share_max"), CivilWar.Share(w, c, "socialistas"), 3);

        foreach (var p in w.PartyDefs.Values) c.Parties[p.Id] = p.Id == "socialistas" ? 5f : 30f;
        World.NormalizeParties(c);
        Assert.Equal(w.Rule("civil_war_share_min"), CivilWar.Share(w, c, "socialistas"), 3);
    }

    [Fact]
    public void O_pais_novo_leva_a_ciencia_as_escolas_e_a_sua_fatia_do_pais()
    {
        var (w, c) = Ripe();
        var tech = w.Techs.Values.First();
        c.Techs.Add(tech.Id);
        c.Doctrines.Add("qualquer_escola");
        c.Stats["industry"] = 3f;
        c.Money = 1000f; c.Manpower = 600f;
        c.Stock[1] = 100f;

        var rebel = CivilWar.Erupt(w, c, "socialistas")!;
        Assert.Contains(tech.Id, rebel.Techs);
        Assert.Contains("qualquer_escola", rebel.Doctrines);
        Assert.Equal(3f, rebel.Stats["industry"], 3);
        // 4 das 6 províncias levantaram-se: dois terços do cofre, dos homens e do armazém vão com elas
        Assert.Equal(1000f * 4f / 6f, rebel.Money, 1);
        Assert.Equal(1000f - rebel.Money, c.Money, 1);
        Assert.Equal(600f * 4f / 6f, rebel.Manpower, 1);
        Assert.Equal(100f * 4f / 6f, rebel.Stock[1], 1);
        Assert.Equal(w.Rule("civil_war_rebel_stability"), rebel.Stability, 3);
    }

    [Fact]
    public void O_pais_mae_paga_a_ruptura_em_estabilidade_e_expurga_quem_se_levantou()
    {
        var (w, c) = Ripe();
        float pop = c.Parties["socialistas"], stab = c.Stability;
        CivilWar.Erupt(w, c, "socialistas");

        Assert.Equal(MathF.Max(0f, stab - w.Rule("civil_war_stability_hit")), c.Stability, 3);
        Assert.True(c.Parties["socialistas"] < pop, "o partido levantado tinha de perder chão na parte que ficou");
        Assert.Equal(100f, c.Parties.Values.Sum(), 1);
        Assert.Equal(100f, w.Countries.Values.First(x => x.RebelOf == c.Id).Parties.Values.Sum(), 1);
    }

    [Fact]
    public void Um_pais_ja_partido_nao_se_parte_outra_vez_no_mesmo_dia()
    {
        var (w, c) = Ripe();
        Assert.NotNull(CivilWar.Erupt(w, c, "socialistas"));
        Assert.False(CivilWar.Erupts(w, c, "nacionalistas"));
        Assert.Null(CivilWar.Erupt(w, c, "nacionalistas"));
    }

    [Fact]
    public void O_mesmo_mundo_parte_se_sempre_da_mesma_maneira()
    {
        var (w1, c1) = Ripe();
        var (w2, c2) = Ripe();
        var a = CivilWar.Erupt(w1, c1, "socialistas")!;
        var b = CivilWar.Erupt(w2, c2, "socialistas")!;
        Assert.Equal(a.Tag, b.Tag);
        Assert.Equal(a.CapitalRegionId, b.CapitalRegionId);
        Assert.Equal(w1.Regions.Values.Where(r => r.OwnerId == a.Id).Select(r => r.Id).OrderBy(x => x),
                     w2.Regions.Values.Where(r => r.OwnerId == b.Id).Select(r => r.Id).OrderBy(x => x));
    }

    [Fact]
    public void O_pais_nascido_da_guerra_civil_sobrevive_ao_save_e_ao_carregamento()
    {
        var (w, c) = Ripe();
        TestWorld.AddDivision(w, 7, 1, TestWorld.Inf, 5);
        var rebel = CivilWar.Erupt(w, c, "socialistas")!;
        rebel.Money = 42f;

        using var save = new MsSqliteDatabase();
        var staticDb = new MsSqliteDatabase();
        staticDb.ExecuteScript(File.ReadAllText("data/schema.sql"));
        SqlWorldRepository.EnsureSaveSchema(save, File.ReadAllText("data/schema.sql"));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2, 10, 6);
        repo.LoadSave(w2, save);

        var back = w2.Countries[rebel.Id];
        Assert.Equal(rebel.Name, back.Name);
        Assert.Equal(rebel.Tag, back.Tag);
        Assert.Equal(rebel.Colour, back.Colour);
        Assert.Equal(c.Id, back.RebelOf);
        Assert.Equal(rebel.CapitalRegionId, back.CapitalRegionId);
        Assert.Equal(42f, back.Money, 1);
        Assert.Equal(rebel.Stats["industry"], back.Stats["industry"], 3);
        Assert.True(w2.AreAtWar(c.Id, back.Id));
        Assert.Equal(back.Id, w2.Divisions[7].CountryId);
        Assert.Equal(back.Id, w2.Regions[6].OwnerId);
    }

    [Fact]
    public void A_manchete_e_a_cronica_contam_o_dia()
    {
        var (w, c) = Ripe();
        var cronica = new ChronicleSystem();
        cronica.Bind(w);                    // liga-se antes: o golpe rebenta logo no primeiro dia
        w.Register(new PartySystem());
        w.Register(cronica);
        w.Tick();

        var rebel = w.Countries.Values.First(x => x.RebelOf == c.Id);
        Assert.Contains(rebel.Name, CivilWar.Headline(w, c, rebel, "socialistas"));
        Assert.Contains(w.Chronicle, e => e.Kind == "guerracivil" && e.Text.Contains("Guerra civil em Alfa"));
        Assert.Contains("partidos com bandeira de levantamento", CivilWar.Smoke(w));
        Assert.Contains(rebel.Name, CivilWar.Smoke(w));
        Assert.Single(CivilWar.Live(w));
    }
}
