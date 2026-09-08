using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O balanço da batalha (CombatSystem.Explain): a força com que um lado se bate hoje, aberta nas
/// parcelas que a fazem. O que aqui se guarda é o que faz dela uma explicação e não uma segunda conta —
/// que as parcelas multiplicam de volta à força que o combate usa mesmo, e que o chão em que se combate
/// aparece lá dentro. Um balanço que dissesse números bonitos sem relação com o dano seria pior do que não
/// haver balanço nenhum: ensinaria o jogador a jogar um jogo que não existe.</summary>
public class CombatExplainTests
{
    private static World Build()
    {
        var db = new MsSqliteDatabase();
        db.ExecuteScript(File.ReadAllText("data/schema.sql"));
        db.ExecuteScript(File.ReadAllText("data/seed_units.sql"));
        db.ExecuteScript(@"
            INSERT INTO template VALUES (1,1,'Inf');
            INSERT INTO template_unit VALUES (1,1,6),(1,4,2);");
        var units = new SqlUnitRepository(db);
        var w = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units),
                          new ModifierEngine(units.GetModifiers()), 1);
        w.Countries[1] = new Country { Id = 1, Tag = "A" };
        w.Countries[2] = new Country { Id = 2, Tag = "B" };
        w.Regions[1] = new Region
        {
            Id = 1, Name = "Chão", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1_000_000,
        };
        return w;
    }

    private static List<Division> Line(World w, int country)
    {
        var d = new Division { Id = w.NewDivisionId(), CountryId = country, TemplateId = 1, RegionId = 1 };
        w.AddDivision(d);
        return new List<Division> { d };
    }

    /// <summary>As parcelas multiplicam de volta à força: é isto que faz do balanço uma explicação do dia e
    /// não um painel decorativo. Se alguém acrescentar um multiplicador ao combate e se esquecer de o
    /// contar aqui, as duas contas afastam-se e este teste cai.</summary>
    [Fact]
    public void As_parcelas_multiplicam_de_volta_a_forca()
    {
        var w = Build();
        var line = Line(w, 1);
        var bal = CombatSystem.Explain(w, w.Regions[1], line, attacking: true, 1, 2);

        Assert.NotEmpty(bal.Factors);
        float product = 1f;
        foreach (var f in bal.Factors) product *= f.Mult;
        Assert.Equal(bal.Strength, product, 3);
    }

    /// <summary>A força do balanço é a mesma que o combate usa: com uma divisão na linha e nada do lado
    /// (sem forte, sem informações, sem aviação), Explain tem de dar exactamente o SideStrength dela.</summary>
    [Fact]
    public void A_forca_do_balanco_e_a_do_combate()
    {
        var w = Build();
        var line = Line(w, 1);
        var ctx = CombatSystem.BuildContext(w, w.Regions[1], 1);
        float real = CombatSystem.SideStrength(w, line, ctx, attacking: true, w.Regions[1])[0];
        var bal = CombatSystem.Explain(w, w.Regions[1], line, attacking: true, 1, 2);
        Assert.Equal(real, bal.Strength, 3);
    }

    /// <summary>O forte é do defensor e mais ninguém: aparece na lista dele com o número de níveis, e não
    /// aparece na do atacante nem lhe mexe na força.</summary>
    [Fact]
    public void O_forte_conta_para_o_defensor_e_nao_para_o_atacante()
    {
        var w = Build();
        var att = Line(w, 1);
        var def = Line(w, 2);

        var semForte = CombatSystem.Explain(w, w.Regions[1], def, attacking: false, 2, 1);
        var atacaSem = CombatSystem.Explain(w, w.Regions[1], att, attacking: true, 1, 2);
        w.Regions[1].Fort = 3;
        var comForte = CombatSystem.Explain(w, w.Regions[1], def, attacking: false, 2, 1);
        var atacaCom = CombatSystem.Explain(w, w.Regions[1], att, attacking: true, 1, 2);

        Assert.True(comForte.Strength > semForte.Strength);
        Assert.Contains(comForte.Factors, f => f.Name.Contains("fortificações"));
        Assert.DoesNotContain(semForte.Factors, f => f.Name.Contains("fortificações"));
        Assert.DoesNotContain(atacaCom.Factors, f => f.Name.Contains("fortificações"));
        Assert.Equal(atacaSem.Strength, atacaCom.Strength, 4);
    }

    /// <summary>Um lado sem ninguém na linha não tem balanço nenhum: força zero e lista vazia, e não uma
    /// divisão por zero nem uma lista de multiplicadores inventados.</summary>
    [Fact]
    public void Linha_vazia_nao_tem_balanco()
    {
        var w = Build();
        var bal = CombatSystem.Explain(w, w.Regions[1], new List<Division>(), attacking: true, 1, 2);
        Assert.Equal(0f, bal.Strength);
        Assert.Empty(bal.Factors);
    }

    /// <summary>O abastecimento entra na lista e pesa: uma divisão a seco bate-se pior do que a mesma
    /// divisão abastecida, e o balanço tem de o dizer pelo nome.</summary>
    [Fact]
    public void O_abastecimento_aparece_no_balanco()
    {
        var w = Build();
        var line = Line(w, 1);
        line[0].Supply = 1f;
        var cheio = CombatSystem.Explain(w, w.Regions[1], line, attacking: true, 1, 2);
        line[0].Supply = 0f;
        var seco = CombatSystem.Explain(w, w.Regions[1], line, attacking: true, 1, 2);

        Assert.True(seco.Strength < cheio.Strength);
        var f = Assert.Single(seco.Factors, x => x.Name == "abastecimento");
        Assert.True(f.Mult < 1f);
    }
}
