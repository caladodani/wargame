using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A conta da obra (BuildPlan). O menu Construir dizia "Arsenal (120, 60 d)" e mais nada — nem o
/// que o Arsenal faz, nem que as fábricas civis estavam todas ocupadas, nem que aquela região já tinha o
/// nível máximo. O que aqui se guarda é que o menu não decide nada por sua conta: o "não podes" tem de ser,
/// palavra por palavra, o do comando que manda fazer a obra.</summary>
public class BuildPlanTests
{
    private static World Build(float money = 10000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = money;
        return w;
    }

    /// <summary>Tudo o que a tabela `building` traz, mais a estrada, o forte e o carril — que são regras e
    /// não linhas, e que o menu tinha escritos à mão.</summary>
    [Fact]
    public void As_obras_sao_as_da_tabela_mais_a_estrada_e_o_forte()
    {
        var w = Build();
        var offers = BuildPlan.Offers(w);
        Assert.Equal(w.BuildingDefs.Count + 3, offers.Count);
        foreach (var d in w.BuildingDefs.Values)
            Assert.Contains(offers, o => o.Id == d.Id && o.Name == d.Name && o.Cost == d.Cost && o.Days == d.Days);
        Assert.Contains(offers, o => o.Id == BuildPlan.Infra);
        Assert.Contains(offers, o => o.Id == BuildPlan.Fort);
        Assert.Contains(offers, o => o.Id == BuildPlan.Rail);
        Assert.All(offers, o => Assert.False(string.IsNullOrWhiteSpace(o.Glyph)));
        Assert.All(offers, o => Assert.False(string.IsNullOrWhiteSpace(o.Gives)));
    }

    /// <summary>O "não podes" do menu é o do comando, à letra. Se um dia a regra mudar no comando, muda no
    /// menu no mesmo dia — e é este teste que obriga a isso.</summary>
    [Fact]
    public void O_travao_do_menu_e_o_do_comando()
    {
        var w = Build(money: 0f);                       // cofre vazio: toda a gente recusa
        int r = w.Regions.Values.First(x => x.OwnerId == 1 && x.ControllerId == 1).Id;
        foreach (var o in BuildPlan.Offers(w))
        {
            string? doComando = o.Id == BuildPlan.Infra ? new BuildInfrastructureCommand(1, r).Validate(w)
                              : o.Id == BuildPlan.Fort ? new BuildFortCommand(1, r).Validate(w)
                              : o.Id == BuildPlan.Rail ? new BuildRailCommand(1, r).Validate(w)
                              : new BuildBuildingCommand(1, r, o.Id).Validate(w);
            Assert.Equal(doComando, BuildPlan.Blocked(w, 1, r, o.Id));
        }
    }

    /// <summary>Com cofre e fábricas, a obra pode-se mandar fazer — e o menu diz que sim.</summary>
    [Fact]
    public void Com_cofre_e_fabrica_a_obra_pode_se_mandar_fazer()
    {
        var w = Build();
        int r = w.Regions.Values.First(x => x.OwnerId == 1 && x.ControllerId == 1).Id;
        Assert.Null(BuildPlan.Blocked(w, 1, r, BuildPlan.Infra));
        Assert.Null(BuildPlan.Blocked(w, 1, null, BuildPlan.Infra));
    }

    /// <summary>Sem fábrica civil livre nenhuma obra começa, por muito dinheiro que haja — e o menu diz
    /// isso antes de o dedo tocar no mapa, em vez de a ordem ser recusada depois.</summary>
    [Fact]
    public void Sem_fabrica_civil_livre_o_menu_avisa_antes_do_toque()
    {
        var w = Build();
        // pôr obra em todas as regiões próprias: as fábricas civis ficam todas ocupadas
        foreach (var r in w.Regions.Values.Where(x => x.OwnerId == 1 && x.ControllerId == 1)) r.Building = true;
        Assert.True(Industry.Of(w, 1).FreeCivil <= 0);
        Assert.Equal("fábricas civis todas ocupadas", BuildPlan.Blocked(w, 1, null, BuildPlan.Fort));
    }

    /// <summary>Sem região escolhida, a ficha diz o tecto; com região, diz em que pé aquela terra está.</summary>
    [Fact]
    public void A_ficha_diz_o_nivel_da_regiao_quando_ha_regiao()
    {
        var w = Build();
        var reg = w.Regions.Values.First(x => x.OwnerId == 1 && x.ControllerId == 1);
        reg.Fort = 2;
        var semRegiao = BuildPlan.Parts(w, 1, null, BuildPlan.Fort);
        var comRegiao = BuildPlan.Parts(w, 1, reg.Id, BuildPlan.Fort);
        Assert.Equal(4, semRegiao.Count);
        Assert.Equal(4, comRegiao.Count);
        Assert.StartsWith("até", semRegiao[3].Value);
        Assert.StartsWith("2 de", comRegiao[3].Value);
    }

    /// <summary>As parcelas de uma obra: custo, dias, fábricas civis e nível — todas com chapa e com uma
    /// explicação, que é o que faz delas ficha e não quatro números soltos.</summary>
    [Fact]
    public void As_parcelas_da_obra_tem_chapa_e_explicacao()
    {
        var w = Build();
        foreach (var o in BuildPlan.Offers(w))
        {
            var parts = BuildPlan.Parts(w, 1, null, o.Id);
            Assert.Equal(4, parts.Count);
            Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Glyph)));
            Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Note)));
            Assert.Contains(o.Name, BuildPlan.Why(w, 1, null, o.Id));
        }
    }

    /// <summary>O que cada edifício dá sai da tabela: o multiplicador por nível, a fila de fábricas que
    /// abre e o alcance de abastecimento. Nada disto está escrito em C#.</summary>
    [Fact]
    public void O_que_a_obra_da_sai_da_tabela()
    {
        var w = Build();
        foreach (var d in w.BuildingDefs.Values)
        {
            string gives = BuildPlan.Gives(w, d);
            if (d.StatKey.Length > 0 && MathF.Abs(d.PerLevel) > 1e-4f) Assert.Contains(d.StatKey, gives);
            if (d.SupplyRange > 0f) Assert.Contains("km", gives);
            if (d.Coastal) Assert.Contains("costa", gives);
            Assert.Contains(d.MaxLevel.ToString(), gives);
        }
    }

    /// <summary>Uma obra que a tabela não conhece não inventa ficha nenhuma.</summary>
    [Fact]
    public void Obra_desconhecida_nao_tem_ficha()
    {
        var w = Build();
        Assert.Null(BuildPlan.Find(w, "nao_existe"));
        Assert.Empty(BuildPlan.Parts(w, 1, null, "nao_existe"));
        Assert.Equal("obra desconhecida", BuildPlan.Blocked(w, 1, null, "nao_existe"));
    }
}
