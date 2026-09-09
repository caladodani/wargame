using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A ordem antes de ser dada: o que a seta arrastada do contador promete ao jogador. O que aqui se
/// defende é que ela conta a tropa e os dias certos, que diz combate quando o destino é de quem combatemos,
/// e que nunca promete uma marcha que o comando depois recusa.</summary>
public class OrdersTests
{
    [Fact]
    public void A_seta_diz_quantas_divisoes_partem_e_quantos_dias_leva()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2);
        TestWorld.AddDivision(w, 3, 2, TestWorld.Inf2, 2);              // tropa alheia na mesma província

        var p = Orders.Look(w, 1, 2, 1);
        Assert.True(p.Legal);
        Assert.Equal(2, p.Divisions);                                    // só a nossa parte
        Assert.Equal(1, p.Hops);
        Assert.True(p.Days > 0f);
        Assert.Equal("marcha", p.State);
        Assert.Equal(w.StackStates["marcha"].Name, p.Name);              // nome e chapa saem da tabela
        Assert.Contains(w.Regions[1].Name, Orders.Line(w, p, w.Regions[1].Name));
    }

    [Fact]
    public void Arrastar_para_terra_de_quem_combatemos_e_combate()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);

        var p = Orders.Look(w, 1, 3, 4);                                 // 4 é do país 2
        Assert.True(p.Legal);
        Assert.True(p.Hostile);
        Assert.Equal("combate", p.State);
        Assert.Equal(w.StackStates["combate"].Glyph, p.Glyph);
    }

    [Fact]
    public void A_seta_nao_promete_o_que_o_comando_recusa()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);

        var vazia = Orders.Look(w, 1, 5, 4);                             // província sem tropa nossa
        Assert.False(vazia.Legal);
        Assert.NotEqual("", vazia.Why);
        Assert.Equal(0, vazia.Divisions);

        var mesma = Orders.Look(w, 1, 2, 2);                             // arrastar para onde ela já está
        Assert.False(mesma.Legal);
        Assert.Equal("", mesma.State);                                   // sem ordem não há chapa nenhuma
    }
}
