using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A ficha do que a região dá (RegionYield). Como a do chão, é explicação e não conta nova: o
/// dinheiro tem de ser o do EconomySystem e os homens têm de ser os mesmos que o pool ganha nesse dia. Uma
/// ficha que dissesse 0,42 por dia enquanto o cofre subia outra coisa ensinava a jogar um jogo que não
/// existe — e é isso que estes testes recusam.</summary>
public class RegionYieldTests
{
    private static World Build(int pop = 1_000_000, bool occupied = false)
    {
        var db = new MsSqliteDatabase();
        db.ExecuteScript(File.ReadAllText("data/schema.sql"));
        db.ExecuteScript(File.ReadAllText("data/seed_units.sql"));
        db.ExecuteScript("INSERT INTO template VALUES (1,1,'Inf'); INSERT INTO template_unit VALUES (1,1,6);");
        var units = new SqlUnitRepository(db);
        var w = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units),
                          new ModifierEngine(units.GetModifiers()), 1);
        w.Countries[1] = new Country { Id = 1, Tag = "A" };
        w.Countries[2] = new Country { Id = 2, Tag = "B" };
        w.Regions[1] = new Region
        {
            Id = 1, Name = "Terra", OwnerId = occupied ? 2 : 1, InitialOwnerId = occupied ? 2 : 1, ControllerId = 1,
            Terrain = "plain", Population = pop, Infrastructure = 1f, BaseInfrastructure = 1f,
        };
        return w;
    }

    [Fact]
    public void A_parcela_do_dinheiro_e_o_rendimento_do_EconomySystem()
    {
        var w = Build();
        var r = w.Regions[1];
        var parts = RegionYield.Parts(w, r);
        Assert.Equal($"{EconomySystem.RegionIncome(w, r):0.00}", parts[0].Value);
    }

    /// <summary>Os homens da ficha são a parcela desta região na soma do ManpowerSystem: com uma região só,
    /// o pool tem de subir exactamente o que a ficha promete.</summary>
    [Fact]
    public void Os_homens_da_ficha_sao_os_que_o_pool_ganha_nesse_dia()
    {
        var w = Build();
        var c = w.Countries[1];
        c.Manpower = 0f;                       // -1 seria "por inicializar" e o primeiro tick punha-o em tecto
        float prometido = RegionYield.MenPerDay(w, w.Regions[1]);
        new ManpowerSystem().Tick(w);
        Assert.Equal(prometido, c.Manpower, 2);
    }

    /// <summary>Um país capitulado não recruta ninguém — e a ficha da terra dele não pode prometer homens
    /// que o pool nunca vê.</summary>
    [Fact]
    public void Pais_capitulado_nao_da_homens_nenhuns()
    {
        var w = Build();
        w.Countries[1].Capitulated = true;
        Assert.Equal(0f, RegionYield.MenPerDay(w, w.Regions[1]));
    }

    /// <summary>Terra ocupada rende menos e recruta menos do que a mesma terra em casa. A ficha tem de
    /// mostrar a diferença, que é a razão de haver políticas de ocupação.</summary>
    [Fact]
    public void Terra_ocupada_da_menos_do_que_terra_de_casa()
    {
        var casa = Build();
        var fora = Build(occupied: true);
        Assert.True(EconomySystem.RegionIncome(fora, fora.Regions[1]) < EconomySystem.RegionIncome(casa, casa.Regions[1]));
        Assert.True(RegionYield.MenPerDay(fora, fora.Regions[1]) <= RegionYield.MenPerDay(casa, casa.Regions[1]));
        Assert.Contains("ocupada", RegionYield.MoneyNote(fora, fora.Regions[1]));
    }

    /// <summary>Sem depósitos nem obras a ficha são duas parcelas — o dinheiro e os homens estão sempre lá,
    /// mesmo a zero: uma terra que não dá nada é informação e não é um cartão vazio.</summary>
    [Fact]
    public void Sem_depositos_nem_obras_a_ficha_tem_duas_parcelas()
    {
        var w = Build(pop: 0);
        var parts = RegionYield.Parts(w, w.Regions[1]);
        Assert.Equal(2, parts.Count);
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Glyph)));
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Note)));
    }

    /// <summary>Depósito na região = mais uma parcela, com a chapa e o nome que a tabela resource dá.</summary>
    [Fact]
    public void Cada_deposito_e_obra_da_uma_parcela_com_a_chapa_da_tabela()
    {
        var w = FactionTests.BuildReal();
        var r = w.Regions.Values.First(x => x.Resources.Count > 0);
        var parts = RegionYield.Parts(w, r);
        Assert.Equal(2 + r.Resources.Count(kv => w.ResourceDefs.ContainsKey(kv.Key)), parts.Count);
        foreach (var (res, _) in r.Resources)
            Assert.Contains(parts, p => p.Glyph == w.ResourceDefs[res].Glyph);
    }
}
