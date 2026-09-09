using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A conta aberta de cada característica do país: quem a multiplica, com nome próprio e por
/// quanto. O contrato que aqui se guarda é o mais importante de todos — base × todas as linhas TEM de dar
/// exactamente `Country.Stat(key)`. Um multiplicador novo no país que ninguém conte aqui fica a mentir ao
/// jogador, e é isso que estes testes recusam.</summary>
public class StatLedgerTests
{
    private static (World w, Country c) Small()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return (w, w.Countries[1]);
    }

    [Fact]
    public void As_familias_de_fonte_vem_da_tabela_com_nome_e_chapa()
    {
        var (w, _) = Small();
        Assert.NotEmpty(w.StatSourceDefs);
        foreach (var s in w.StatSourceDefs.Values)
        {
            Assert.NotEqual("", s.Name);
            Assert.Contains(s.Glyph, GlyphDataTests.Desenhados);
        }
    }

    [Fact]
    public void Uma_tecnologia_e_uma_lei_dao_cada_uma_a_sua_linha_com_nome()
    {
        var (w, c) = Small();
        var tech = w.Techs.Values.First();
        w.TechEffects[tech.Id] = new List<(string, float)> { ("industry", 1.2f) };
        c.Techs.Add(tech.Id);
        w.ApplyTechs(c);

        var lines = StatLedger.Lines(w, c, "industry");
        var line = Assert.Single(lines, l => l.Kind == "tecnologia");
        Assert.Equal(tech.Name, line.Name);
        Assert.Equal(1.2f, line.Mult, 0.001f);
        Assert.Equal(20f, line.Percent, 0.01f);
        Assert.True(line.Helps);
    }

    [Fact]
    public void Uma_fonte_que_corta_diz_que_corta()
    {
        var (w, c) = Small();
        var tech = w.Techs.Values.First();
        w.TechEffects[tech.Id] = new List<(string, float)> { ("industry", 0.85f) };
        c.Techs.Add(tech.Id);
        w.ApplyTechs(c);

        var line = Assert.Single(StatLedger.Lines(w, c, "industry"));
        Assert.False(line.Helps);
        Assert.Equal(-15f, line.Percent, 0.01f);
    }

    [Fact]
    public void A_conta_fecha_exactamente_com_o_que_o_pais_usa()
    {
        var (w, c) = Small();
        // um pouco de cada família: tecnologia, decisão, gabinete, governo e prisioneiros
        var tech = w.Techs.Values.First();
        w.TechEffects[tech.Id] = new List<(string, float)> { ("industry", 1.15f) };
        c.Techs.Add(tech.Id);
        w.ApplyTechs(c);

        w.DecisionDefs["d"] = new DecisionDef("d", "Turnos dobrados", "industria", "", 0f, 0f, 0f, 0f, 10, 0,
                                              0, "", 0f, 0f, 0f, 0f, 0f, "fabrica", 1);
        w.DecisionEffects["d"] = new List<(string, float)> { ("industry", 1.1f) };
        w.ActiveDecisions.Add(new ActiveDecision { CountryId = 1, DecisionId = "d", UntilDay = 10 });
        w.Register(new DecisionSystem());
        DecisionSystem.Recompute(w);

        c.PrisonerMult["industry"] = 1.05f;
        c.Stats["industry"] = 2f;

        Assert.Equal(c.Stat("industry"), StatLedger.Total(w, c, "industry"), 0.0001f);
        Assert.Equal(2f, StatLedger.Base(c, "industry"), 0.0001f);
        var kinds = StatLedger.Lines(w, c, "industry").Select(l => l.Kind).ToList();
        Assert.Contains("tecnologia", kinds);
        Assert.Contains("decisao", kinds);
        Assert.Contains("prisioneiro", kinds);
    }

    [Fact]
    public void Um_edificio_levantado_na_nossa_terra_entra_na_conta_pelo_nome()
    {
        var (w, c) = Small();
        var def = w.BuildingDefs.Values.First(b => b.PerLevel != 0f && b.StatKey.Length > 0);
        w.Regions[1].Buildings[def.Id] = 2;
        new ConstructionSystem().Tick(w);

        var line = Assert.Single(StatLedger.Lines(w, c, def.StatKey), l => l.Kind == "edificio");
        Assert.Equal(def.Name, line.Name);
        Assert.Equal(1f + def.PerLevel * 2f, line.Mult, 0.0001f);
        Assert.Equal(c.Stat(def.StatKey), StatLedger.Total(w, c, def.StatKey), 0.0001f);

        // levantado em terra de outro dono não é nosso
        w.Regions[1].Buildings.Clear();
        w.Regions[6].Buildings[def.Id] = 2;
        new ConstructionSystem().Tick(w);
        Assert.DoesNotContain(StatLedger.Lines(w, c, def.StatKey), l => l.Kind == "edificio");
    }

    [Fact]
    public void As_linhas_vem_pela_ordem_das_familias_da_tabela()
    {
        var (w, c) = Small();
        var tech = w.Techs.Values.First();
        w.TechEffects[tech.Id] = new List<(string, float)> { ("industry", 1.2f) };
        c.Techs.Add(tech.Id);
        w.ApplyTechs(c);
        c.PrisonerMult["industry"] = 1.05f;

        var lines = StatLedger.Lines(w, c, "industry");
        int Rank(string kind) => w.StatSourceDefs[kind].Sort;
        for (int i = 1; i < lines.Count; i++)
            Assert.True(Rank(lines[i - 1].Kind) <= Rank(lines[i].Kind), "famílias fora da ordem da tabela");
    }

    [Fact]
    public void Uma_caracteristica_que_ninguem_mexe_nao_tem_linhas_nenhumas()
    {
        var (w, c) = Small();
        Assert.Empty(StatLedger.Lines(w, c, "industry"));
        Assert.Equal(StatLedger.Base(c, "industry"), StatLedger.Total(w, c, "industry"), 0.0001f);
        Assert.DoesNotContain("industry", StatLedger.Touched(w, c));
    }

    [Fact]
    public void As_caracteristicas_listadas_sao_as_da_tabela_mais_as_que_alguem_mexe()
    {
        var (w, c) = Small();
        c.TechMult["chave_nova"] = 1.5f;
        var keys = StatLedger.Keys(w, c);
        Assert.Contains("chave_nova", keys);
        foreach (var k in w.CountryStatDefs.Keys) Assert.Contains(k, keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        // e cada uma sabe dizer como se chama
        Assert.Equal("chave_nova", StatLedger.Name(w, "chave_nova"));
        if (w.CountryStatDefs.Count > 0)
        {
            var first = w.CountryStatDefs.Values.First();
            Assert.Equal(first.Name, StatLedger.Name(w, first.Key));
        }
    }

    [Fact]
    public void No_mundo_a_serio_a_conta_fecha_para_todas_as_caracteristicas_de_todos_os_paises()
    {
        var w = FactionTests.BuildReal();
        // um mundo acabado de abrir ainda não correu os sistemas que enchem os dicionários do país (os
        // recursos e os edifícios são recontados todos os dias, os comandantes e o governo ao carregar).
        // A conta do ledger é a de um mundo A ANDAR, que é o único que o jogador vê — põe-se cá em pé.
        new ResourceSystem().Tick(w);
        new ConstructionSystem().Tick(w);
        foreach (var country in w.Countries.Values)
        {
            World.ApplyGenerals(w, country); World.ApplyCabinet(w, country);
            World.ApplyParty(w, country); w.ApplyTechs(country);
        }
        Assert.Equal(12, w.StatSourceDefs.Count);
        int checkedKeys = 0;
        foreach (var c in w.Countries.Values)
            foreach (var k in StatLedger.Keys(w, c))
            {
                Assert.Equal(c.Stat(k), StatLedger.Total(w, c, k), 0.001f);
                checkedKeys++;
            }
        Assert.True(checkedKeys > 100, $"conta pequena de mais para valer como prova: {checkedKeys}");
        Assert.Contains("famílias de fonte", StatLedger.Smoke(w, w.Countries.Values.First().Id));
    }
}
