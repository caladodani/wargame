using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Facções (alianças defensivas). Membros e regra de guerra usam o static.db a sério (tags reais, ex.
/// RUS/EST) — só a IA (dissuasão) precisa de um mapa sintético, feito com TestWorld.LinearMap.</summary>
public class FactionTests
{
    /// <summary>Mundo carregado do static.db real (mesmo LoadStatic do jogo) — só para ler países/facções, sem mapa jogável.</summary>
    internal static World BuildReal()
    {
        var db = new MsSqliteDatabase("Data Source=data/static.db;Mode=ReadOnly");
        var units = new SqlUnitRepository(db);
        var w = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units), new ModifierEngine(units.GetModifiers()));
        new SqlWorldRepository(db).LoadStatic(w);
        return w;
    }

    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    [Fact]
    public void Membros_reais_carregados_em_NATO_e_OTSC()
    {
        var w = BuildReal();
        var nato = w.Factions["nato"];
        Assert.Contains(ByTag(w, "PRT").Id, nato.Members);
        Assert.Contains(ByTag(w, "USA").Id, nato.Members);
        var otsc = w.Factions["otsc"];
        Assert.Contains(ByTag(w, "RUS").Id, otsc.Members);
    }

    [Fact]
    public void Declarar_guerra_a_aliado_da_mesma_faccao_e_recusado()
    {
        var w = BuildReal();
        var cmd = new DeclareWarCommand(ByTag(w, "USA").Id, ByTag(w, "GBR").Id);
        Assert.Equal("Aliados na mesma facção", cmd.Validate(w));
    }

    [Fact]
    public void Guerra_a_membro_da_NATO_chama_os_outros_membros_contra_o_agressor()
    {
        var w = BuildReal();
        var rus = ByTag(w, "RUS"); var est = ByTag(w, "EST"); var prt = ByTag(w, "PRT");
        var joined = new List<FactionJoinedWar>();
        w.Events.Subscribe<FactionJoinedWar>(e => joined.Add(e));

        var cmd = new DeclareWarCommand(rus.Id, est.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.True(w.AreAtWar(rus.Id, est.Id));
        Assert.True(w.AreAtWar(rus.Id, prt.Id));   // PRT é NATO: entra contra a Rússia, sem ter sido atacado
        Assert.Contains(joined, e => e.FactionId == "nato" && e.MemberCountryId == prt.Id && e.AgainstCountryId == rus.Id);
    }

    [Fact]
    public void Membros_da_faccao_do_agressor_nao_entram_na_guerra()
    {
        var w = BuildReal();
        var rus = ByTag(w, "RUS"); var est = ByTag(w, "EST"); var blr = ByTag(w, "BLR");   // BLR é OTSC, como a RUS
        new DeclareWarCommand(rus.Id, est.Id).Execute(w);
        Assert.False(w.AreAtWar(blr.Id, rus.Id));
    }

    [Fact]
    public void Aliado_do_alvo_ja_em_guerra_com_o_agressor_nao_duplica_evento()
    {
        var w = BuildReal();
        var rus = ByTag(w, "RUS"); var est = ByTag(w, "EST"); var lva = ByTag(w, "LVA");   // LVA também é NATO
        rus.AtWarWith.Add(lva.Id); lva.AtWarWith.Add(rus.Id);                              // já em guerra antes
        var joined = new List<FactionJoinedWar>();
        w.Events.Subscribe<FactionJoinedWar>(e => joined.Add(e));

        new DeclareWarCommand(rus.Id, est.Id).Execute(w);

        Assert.DoesNotContain(joined, e => e.MemberCountryId == lva.Id);   // já estava — sem novo evento
        Assert.True(w.AreAtWar(rus.Id, lva.Id));                          // continua em guerra, sem duplicar
    }

    [Fact]
    public void WarGoal_da_IA_desiste_por_forca_conjunta_do_alvo_com_aliados()
    {
        // Mapa sintético (TestWorld): país 1 (regiões 1-3) fraco sozinho | país 2 (regiões 4-6), agressivo.
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["ai_war_chance"] = 1f; w.Rules["ai_war_min_day"] = 0; w.Rules["ai_war_ratio"] = 1f;
        w.Countries[2].Stats["aggression"] = 1f;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);                        // país 1: 1 divisão só
        for (int i = 0; i < 4; i++) TestWorld.AddDivision(w, 10 + i, 2, TestWorld.Inf2, 4);   // país 2: 4 divisões

        // País 3, aliado do país 1 na mesma facção, tem força suficiente para dissuadir (sem precisar de fronteira).
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 100 };
        w.Regions[100] = new Region { Id = 100, Name = "Longe", OwnerId = 3, ControllerId = 3 };
        for (int i = 0; i < 5; i++) TestWorld.AddDivision(w, 20 + i, 3, TestWorld.Inf, 100);
        w.Factions["aliança"] = new Faction("aliança", "Aliança de teste", "", new List<int> { 1, 3 });

        w.Register(new AiSystem());
        int period = (int)w.Rule("ai_period_days", 3);
        TestWorld.Days(w, period * 5);

        Assert.False(w.AreAtWar(2, 1));   // 1+3 = 6 divisões > 4 (ai_war_ratio=1): país 2 desiste
    }
}
