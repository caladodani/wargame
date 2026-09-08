using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A ficha de como a região está (RegionState). O painel dizia tudo isto num parágrafo corrido —
/// "🔧 danificada · 🏰 Forte 2 · ⚓ cais 1 · ✊ resistência 34%" — e o que aqui se guarda é que a grelha que
/// o substituiu não inventa contas: o forte é o do GroundSystem, os dias de obra são os do
/// ConstructionSystem, a integração é a do IntegrationSystem e o cais é o mesmo que o abastecimento usa.</summary>
public class RegionStateTests
{
    private static (World w, Region r) Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return (w, w.Regions[1]);
    }

    /// <summary>Gente e estrada estão sempre lá: uma terra vazia ou sem estrada é informação, não é ausência
    /// de informação. O resto só aparece quando existe.</summary>
    [Fact]
    public void A_gente_e_a_estrada_estao_sempre_na_ficha()
    {
        var (w, r) = Build();
        var parts = RegionState.Parts(w, r);
        // gente e estrada primeiro; a terceira é o que esta terra vale na guerra — dez milhões de gente e a
        // capital do país contam pontos de vitória, e isso é ficha como o forte ou o cais
        Assert.Equal(3, parts.Count);
        Assert.Equal("habitantes", parts[0].Name);
        Assert.Equal("estrada", parts[1].Name);
        Assert.Equal("vitória", parts[2].Name);
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Note)));
        // uma vila não se lê "0,0 M": aos milhares diz-se em milhares
        Assert.Equal($"{10f:0.0} M", RegionState.People(10_000_000f));
        Assert.Equal("30 k", RegionState.People(30_000f));
        Assert.Equal("400", RegionState.People(400f));
    }

    /// <summary>O forte da ficha é o do GroundSystem, à vírgula. Se um dia a regra mudar no combate, muda
    /// aqui no mesmo dia — que é o ponto de não haver duas contas.</summary>
    [Fact]
    public void O_forte_da_ficha_e_o_do_combate()
    {
        var (w, r) = Build();
        Assert.DoesNotContain(RegionState.Parts(w, r), p => p.Name == "forte");
        r.Fort = 3;
        var forte = Assert.Single(RegionState.Parts(w, r), p => p.Name == "forte");
        Assert.Equal("3", forte.Value);
        Assert.Contains($"×{GroundSystem.FortDefence(w, r):0.00}", forte.Note);
    }

    /// <summary>O cais da ficha é o mesmo que o abastecimento por mar conta: os edifícios com alcance de mar
    /// da tabela, e não uma lista escrita à mão.</summary>
    [Fact]
    public void O_cais_da_ficha_e_o_dos_edificios_com_alcance_de_mar()
    {
        var (w, r) = Build();
        var porto = w.BuildingDefs.Values.FirstOrDefault(d => d.SupplyRange > 0f);
        Assert.NotNull(porto);
        Assert.Equal(0, RegionState.PortLevels(w, r));
        r.Buildings[porto!.Id] = 2;
        Assert.Equal(2, RegionState.PortLevels(w, r));
        Assert.Equal(porto.SupplyRange * 2f, RegionState.PortReach(w, r), 3);
        var cais = Assert.Single(RegionState.Parts(w, r), p => p.Name == "cais");
        Assert.Equal("2", cais.Value);
        Assert.Contains($"{2 * w.Rule("port_capacity_per_level", 6f):0} divisões", cais.Note);
    }

    /// <summary>A resistência só se conta em terra ocupada, e a ficha diz o que ela tira ao rendimento — que
    /// é o mesmo número que o EconomySystem tira.</summary>
    [Fact]
    public void A_resistencia_diz_o_que_tira_ao_rendimento()
    {
        var (w, r) = Build();
        Assert.DoesNotContain(RegionState.Parts(w, r), p => p.Name == "resistência");
        r.ControllerId = 2; r.Resistance = 0.4f;
        var res = Assert.Single(RegionState.Parts(w, r), p => p.Name == "resistência");
        Assert.Equal($"{0.4f:P0}", res.Value);
        Assert.Contains($"{0.4f * w.Rule("resistance_output_hit", 0.5f):P0}", res.Note);
    }

    /// <summary>A integração é uma fracção dos dias que o IntegrationSystem exige, e a ficha diz quantos
    /// dias faltam ao ritmo de agora.</summary>
    [Fact]
    public void A_integracao_conta_os_dias_que_o_sistema_exige()
    {
        var (w, r) = Build();
        float need = w.Rule("integration_days", 150f);
        r.ControllerId = 2; r.Integration = need / 2f;
        var integ = Assert.Single(RegionState.Parts(w, r), p => p.Name == "integração");
        Assert.Equal($"{0.5f:P0}", integ.Value);
        float speed = w.Countries[2].Stat("integration_speed");
        Assert.Contains($"faltam {(int)MathF.Ceiling((need - r.Integration) / speed)} dias", integ.Note);
    }

    /// <summary>As fábricas civis só aparecem a quem é dono e senhor da terra — é a explicação de uma obra
    /// recusada com o cofre cheio — e o número é o do Industry.</summary>
    [Fact]
    public void As_fabricas_civis_so_aparecem_a_quem_manda_na_terra()
    {
        var (w, r) = Build();
        Assert.DoesNotContain(RegionState.Parts(w, r, viewerId: 2), p => p.Name == "fábricas civis");
        var yards = Industry.Of(w, 1);
        var fab = Assert.Single(RegionState.Parts(w, r, viewerId: 1), p => p.Name == "fábricas civis");
        Assert.Equal($"{yards.FreeCivil} de {yards.Civil}", fab.Value);
    }

    /// <summary>As obras a andar contam os dias como o ConstructionSystem os gasta: um por dia, até aos dias
    /// da regra (ou aos dias do edifício).</summary>
    [Fact]
    public void As_obras_contam_os_dias_do_sistema_de_construcao()
    {
        var (w, r) = Build();
        Assert.Empty(RegionState.Works(w, r));

        r.Building = true; r.BuildProgress = 10f;
        r.FortBuilding = true; r.FortProgress = 5f;
        var def = w.BuildingDefs.Values.First();
        r.Project = def.Id; r.ProjectProgress = 1f;

        var works = RegionState.Works(w, r);
        Assert.Equal(3, works.Count);
        Assert.Equal((int)MathF.Ceiling(w.Rule("infra_build_days", 30f) - 10f), works[0].DaysLeft);
        Assert.Equal((int)MathF.Ceiling(w.Rule("fort_build_days", 20f) - 5f), works[1].DaysLeft);
        Assert.Equal((int)MathF.Ceiling(def.Days - 1f), works[2].DaysLeft);
        Assert.All(works, o => Assert.InRange(o.Progress, 0f, 1f));

        // e os dias que a ficha diz são os dias que o mundo leva mesmo: um dia de obra por dia
        w.Register(new ConstructionSystem());
        int faltam = works[1].DaysLeft;
        TestWorld.Days(w, faltam);
        Assert.False(r.FortBuilding);
    }

    /// <summary>A estrada partida diz-se partida, e com os dias que leva a repor-se sozinha ao ritmo do
    /// InfrastructureRepairSystem.</summary>
    [Fact]
    public void A_estrada_partida_diz_quantos_dias_leva_a_repor_se()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var r = new Region { Id = 9, Name = "R9", OwnerId = 1, InitialOwnerId = 1, ControllerId = 1, Terrain = "plain",
                             Population = 1_000_000, BaseInfrastructure = 1.5f, Infrastructure = 1.0f };
        w.Regions[9] = r;
        var estrada = Assert.Single(RegionState.Parts(w, r), p => p.Name == "estrada");
        Assert.Equal($"×{1.0f:0.00}", estrada.Value);
        int dias = (int)MathF.Ceiling(0.5f / w.Rule("infra_repair_per_day", 0.002f));
        Assert.Contains($"em {dias} dias", estrada.Note);
    }

    /// <summary>Com o mapa pintado por uma conta, a ficha diz o número exacto daquela região — e diz o mesmo
    /// que o MapModes diz, porque é ele que o escreve.</summary>
    [Fact]
    public void O_modo_de_mapa_traz_o_numero_exacto_da_regiao()
    {
        var (w, r) = Build();
        var modo = w.MapModeDefs.Values.First(m => m.Metric == "population");
        var parte = Assert.Single(RegionState.Parts(w, r, viewerId: 1, mapMode: modo.Id), p => p.Name == modo.Name.ToLowerInvariant());
        Assert.Equal(MapModes.Text(w, 1, r, modo.Metric), parte.Value);
        // o modo político não é conta nenhuma: não põe parcela
        Assert.DoesNotContain(RegionState.Parts(w, r, 1, MapModes.Political), p => p.Name == "político");
    }

    /// <summary>A linha da batalha traz a organização dos dois lados — que é o que decide o combate — e o
    /// forte quando o há. Sem batalha, não há linha.</summary>
    [Fact]
    public void A_linha_da_batalha_traz_a_organizacao_dos_dois_lados()
    {
        var (w, r) = Build();
        Assert.Null(RegionState.BattleLine(w, r));
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 2);
        w.Divisions[2].RegionId = 1;
        r.DivisionIds.Add(2);
        w.ActiveBattles.Add(new Battle { RegionId = r.Id, AttackerCountryId = 2, Attackers = { 2 }, Defenders = { 1 }, Days = 3 });
        var line = RegionState.BattleLine(w, r);
        Assert.NotNull(line);
        Assert.Contains("batalha (3 dias)", line);
        Assert.Contains("B ataca", line);
        Assert.Contains("org 100 vs 100", line);
    }

    /// <summary>Toda a chapa que a ficha pede é uma chapa que o Glyph sabe desenhar — a mesma guarda que
    /// vale para as tabelas, agora para as parcelas escritas em código.</summary>
    [Fact]
    public void Todas_as_chapas_da_ficha_existem()
    {
        var w = FactionTests.BuildReal();
        TestWorld.Season(w, w.SeasonDefs.Keys.First());
        foreach (var r in w.Regions.Values.Take(200))
        {
            foreach (var p in RegionState.Parts(w, r, r.ControllerId, MapModes.Political))
                Assert.Contains(p.Glyph, GlyphDataTests.Desenhados);
            foreach (var o in RegionState.Works(w, r))
                Assert.Contains(o.Glyph, GlyphDataTests.Desenhados);
        }
        foreach (var m in w.MapModeDefs.Values)
        {
            var r = w.Regions.Values.First();
            foreach (var p in RegionState.Parts(w, r, r.ControllerId, m.Id))
                Assert.Contains(p.Glyph, GlyphDataTests.Desenhados);
        }
    }

    /// <summary>Quem manda na terra: o controlador quando é dele, e "ocupada, de X" quando não é.</summary>
    [Fact]
    public void O_controlo_diz_de_quem_e_a_terra_ocupada()
    {
        var (w, r) = Build();
        Assert.Equal("Alfa", RegionState.Control(w, r));
        r.ControllerId = 2;
        Assert.Equal("Beta · ocupada, de Alfa", RegionState.Control(w, r));
    }
}
