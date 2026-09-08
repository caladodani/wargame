using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>As sondas do mundo (WorldWatch): as perguntas que um evento noticioso pode ficar à espera que
/// o mundo responda. É o que tira o jogo do silêncio entre uma ofensiva e a seguinte.</summary>
public class WorldWatchTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return w;
    }

    /// <summary>A sonda da guerra acende no dia em que a guerra é declarada, não antes.</summary>
    [Fact]
    public void A_guerra_acende_a_sonda_no_dia_em_que_e_declarada()
    {
        var w = Setup();
        var c = w.Countries[1];
        Assert.False(WorldWatch.Fires(w, c, "guerra", 1f));
        Assert.True(WorldWatch.Fires(w, c, "paz", 0f));
        w.StartWar(1, 2);
        Assert.True(WorldWatch.Fires(w, c, "guerra", 1f));
        Assert.False(WorldWatch.Fires(w, c, "paz", 0f));
    }

    /// <summary>A capital não é perdida por ser de outro no papel: é perdida quando quem lá manda é outro.</summary>
    [Fact]
    public void A_capital_perde_se_quando_quem_la_manda_e_outro()
    {
        var w = Setup();
        var c = w.Countries[1];
        Assert.False(WorldWatch.Fires(w, c, "capital_perdida", 0f));
        w.Regions[c.CapitalRegionId].ControllerId = 2;
        Assert.True(WorldWatch.Fires(w, c, "capital_perdida", 0f));
    }

    /// <summary>Numa sonda de contar, escrever 0 na linha do evento quer dizer uma — ninguém escreve um
    /// evento para quando estiverem zero divisões cercadas.</summary>
    [Fact]
    public void As_contagens_nunca_pedem_zero()
    {
        var w = Setup();
        var c = w.Countries[1];
        Assert.False(WorldWatch.Fires(w, c, "terra_perdida", 0f));
        w.Regions[2].ControllerId = 2;
        Assert.True(WorldWatch.Fires(w, c, "terra_perdida", 0f));
        Assert.False(WorldWatch.Fires(w, c, "terra_perdida", 2f));
    }

    /// <summary>A revolta é da terra ocupada: a nossa própria gente descontente não é uma revolta de
    /// ocupação, e a sonda não a confunde com uma.</summary>
    [Fact]
    public void A_revolta_e_da_terra_ocupada_e_nao_da_nossa()
    {
        var w = Setup();
        var c = w.Countries[1];
        w.Regions[1].Resistance = 0.9f;                       // terra nossa, gente nossa
        Assert.False(WorldWatch.Fires(w, c, "revolta", 0.6f));
        w.Regions[4].ControllerId = 1;                        // terra do 2 que passámos a mandar
        w.Regions[4].Resistance = 0.9f;
        Assert.True(WorldWatch.Fires(w, c, "revolta", 0.6f));
        Assert.False(WorldWatch.Fires(w, c, "revolta", 0.95f));
    }

    /// <summary>A guerra arrastada conta-se do dia em que começou, e só depois de passarem os dias pedidos.</summary>
    [Fact]
    public void A_guerra_arrastada_conta_se_do_dia_em_que_comecou()
    {
        var w = Setup();
        var c = w.Countries[1];
        w.StartWar(1, 2);
        Assert.False(WorldWatch.Fires(w, c, "guerra_longa", 30f));
        for (int i = 0; i < 30; i++) w.Clock.Advance();
        Assert.True(WorldWatch.Fires(w, c, "guerra_longa", 30f));
    }

    /// <summary>Uma sonda que não existe nunca acende: um erro de escrita na base de dados cala o evento,
    /// não o dispara todos os dias.</summary>
    [Fact]
    public void Sonda_desconhecida_nunca_acende()
    {
        var w = Setup();
        Assert.False(WorldWatch.Known("cavalgada_dos_ceus"));
        Assert.False(WorldWatch.Fires(w, w.Countries[1], "cavalgada_dos_ceus", 0f));
    }

    /// <summary>Todos os eventos semeados pedem uma sonda que existe. É o teste que apanha um evento novo
    /// escrito com uma pergunta que o C# ainda não sabe fazer.</summary>
    [Fact]
    public void Os_eventos_semeados_so_pedem_sondas_que_existem()
    {
        var w = Setup();
        var watch = w.NewsEvents.Values.Where(n => n.IsWatch).ToList();
        Assert.NotEmpty(watch);
        foreach (var n in watch)
            Assert.True(WorldWatch.Known(n.Watch), $"o evento {n.Id} pede a sonda desconhecida «{n.Watch}»");
    }
}
