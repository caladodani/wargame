using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A ficha do chão (GroundSystem): o que o terreno tira a quem assalta, paga a quem espera e cobra
/// a quem marcha. É explicação, não conta nova — e o que aqui se guarda é exactamente isso: que os números
/// da ficha são os mesmos que o combate e o movimento usam. Uma ficha que dissesse que a montanha custa
/// metade da força enquanto o combate cobrasse outra coisa ensinaria o jogador a jogar um jogo que não
/// existe.</summary>
public class GroundTests
{
    private static World Build(string terrain = "plain", bool river = false, string tag = "A", string extra = "")
    {
        var db = new MsSqliteDatabase();
        db.ExecuteScript(File.ReadAllText("data/schema.sql"));
        db.ExecuteScript(File.ReadAllText("data/seed_units.sql"));
        db.ExecuteScript(@"
            INSERT INTO template VALUES (1,1,'Inf');
            INSERT INTO template_unit VALUES (1,1,6),(1,4,2);" + extra);
        var units = new SqlUnitRepository(db);
        var w = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units),
                          new ModifierEngine(units.GetModifiers()), 1);
        w.Countries[1] = new Country { Id = 1, Tag = tag };
        w.Countries[2] = new Country { Id = 2, Tag = "B" };
        w.Regions[1] = new Region
        {
            Id = 1, Name = "Chão", OwnerId = 1, InitialOwnerId = 1, ControllerId = 1,
            Terrain = terrain, River = river, Population = 1_000_000, Infrastructure = 1f, BaseInfrastructure = 1f,
        };
        return w;
    }

    private static Division Troop(World w)
    {
        var d = new Division { Id = w.NewDivisionId(), CountryId = 1, TemplateId = 1, RegionId = 1 };
        w.AddDivision(d);
        return d;
    }

    /// <summary>A montanha é o caso que se sente no jogo: quem assalta paga, quem espera cobra. Se alguém
    /// mexer nas linhas de terreno da tabela modifier e as inverter, isto cai.</summary>
    [Fact]
    public void A_montanha_castiga_quem_assalta_e_paga_a_quem_espera()
    {
        var w = Build("mountain");
        var linhas = GroundSystem.Explain(w, w.Regions[1], 1);
        Assert.NotEmpty(linhas);
        Assert.True(linhas[0].Attack < 1f, $"assalto na montanha devia custar: ×{linhas[0].Attack}");
        Assert.True(linhas[0].Defend > 1f, $"defesa na montanha devia pagar: ×{linhas[0].Defend}");
    }

    /// <summary>A planície é o chão neutro: não dá nem tira. Serve de fio-de-prumo — se aqui aparecer um
    /// número diferente de 1, entrou na tabela um modificador de terreno que se aplica em todo o lado.</summary>
    [Fact]
    public void A_planicie_sem_rio_nao_da_nem_tira()
    {
        var w = Build();
        var linhas = GroundSystem.Explain(w, w.Regions[1], 1);
        Assert.Equal(1f, linhas[0].Attack, 3);
        Assert.Equal(1f, linhas[0].Defend, 3);
    }

    /// <summary>O rio só castiga quem o atravessa: quem já está do outro lado não sente nada.</summary>
    [Fact]
    public void O_rio_so_castiga_quem_o_atravessa()
    {
        var seco = GroundSystem.Explain(Build(), Build().Regions[1], 1);
        var w = Build(river: true);
        var molhado = GroundSystem.Explain(w, w.Regions[1], 1);
        Assert.True(molhado[0].Attack < seco[0].Attack, "o rio devia custar ao assalto");
        Assert.Equal(seco[0].Defend, molhado[0].Defend, 3);
    }

    /// <summary>O número da ficha é o número do combate: o balanço da batalha abre a força na parcela
    /// «terreno, rio e tecnologia», e é essa, à letra, que a ficha do chão mostra.</summary>
    /// <summary>A primeira linha é só o terreno: os espíritos e as tecnologias de um país não entram nela.
    /// Sem isto, dois países a olhar para a mesma montanha liam «montanha» com números diferentes e nenhum
    /// deles era o do chão.</summary>
    [Fact]
    public void A_primeira_linha_nao_leva_espiritos_de_pais_nenhum()
    {
        // PRT tem espíritos com linha na tabela modifier (a defesa atlântica paga na montanha): a segunda
        // linha da ficha sente-os, a primeira não pode sentir
        var w = Build("mountain", tag: "PRT", extra: @"
            INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id)
            VALUES (900,'spirit','terrain','mountain','str_defender',NULL,'mul',1.2,'PRT','teste_defesa_da_serra');");
        var linhas = GroundSystem.Explain(w, w.Regions[1], 1);
        Assert.Equal(GroundSystem.Terrain(w, w.Regions[1], true), linhas[0].Attack, 4);
        Assert.Equal(GroundSystem.Terrain(w, w.Regions[1], false), linhas[0].Defend, 4);
        Assert.True(linhas.Count > 1, "os espíritos de PRT deviam dar uma segunda linha");
        Assert.True(linhas[1].Defend > linhas[0].Defend, "a defesa atlântica paga na montanha");
    }

    /// <summary>Sem país nenhum ainda há ficha: o terreno é do terreno. A região de um país que já não
    /// existe continua a poder abrir-se.</summary>
    [Fact]
    public void Sem_pais_a_ficha_fica_so_com_o_terreno()
    {
        var w = Build("forest");
        var linhas = GroundSystem.Explain(w, w.Regions[1], 999);
        Assert.Single(linhas);
        Assert.True(linhas[0].Attack < 1f);
    }

    [Fact]
    public void A_ficha_do_chao_e_a_parcela_do_terreno_no_combate()
    {
        var w = Build("mountain");
        var d = Troop(w);
        var bal = CombatSystem.Explain(w, w.Regions[1], new List<Division> { d }, attacking: true, 1, 2);
        var parcela = bal.Factors.First(f => f.Name == "terreno, rio e tecnologia");
        float ficha = GroundSystem.Fight(w, w.Regions[1], 1, attacking: true, w.Stats.Get(d.TemplateId));
        Assert.Equal(parcela.Mult, ficha, 4);
    }

    /// <summary>Os dias de marcha da ficha são os do movimento — a mesma chamada, não uma estimativa
    /// paralela que envelhecia mal.</summary>
    [Fact]
    public void Os_dias_de_marcha_sao_os_do_movimento()
    {
        var w = Build("mountain");
        var d = Troop(w);
        Assert.Equal(MovementSystem.HopDays(w, d, w.Regions[1], w.Regions[1]),
                     GroundSystem.MarchDays(w, w.Regions[1], d), 4);
        Assert.True(GroundSystem.MarchDays(w, w.Regions[1], d) > 0f);
    }

    /// <summary>O forte é do defensor e cresce com o nível — a mesma conta que o combate aplica à linha de
    /// defesa antes de se trocarem golpes.</summary>
    [Fact]
    public void O_forte_paga_ao_defensor_por_nivel()
    {
        var w = Build();
        Assert.Equal(1f, GroundSystem.FortDefence(w, w.Regions[1]), 4);
        w.Regions[1].Fort = 3;
        Assert.Equal(1f + 3f * w.Rule("fort_defense_per_level", 0.15f), GroundSystem.FortDefence(w, w.Regions[1]), 4);
    }
}
