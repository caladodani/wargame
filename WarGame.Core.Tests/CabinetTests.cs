using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Gabinete civil: quatro pastas, um conselheiro em cada, nomeação paga de uma vez e salário todos
/// os dias. Quem não tem com que pagar fica sem governo.</summary>
public class CabinetTests
{
    private static World Build(float money = 1000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = money;          // salários: saem do cofre de produção, todos os dias
        w.Countries[1].Political = money;      // nomeação: paga-se com poder político, uma vez (0.3.61)
        return w;
    }


    [Fact]
    public void AppointingSeatsTheManPaysHimAndLiftsTheStat()
    {
        var w = Build();
        Assert.Equal(1f, w.Countries[1].Stat("industry"), 3);

        Assert.Null(new AppointAdvisorCommand(1, "adv_industrial").Validate(w));
        new AppointAdvisorCommand(1, "adv_industrial").Execute(w);

        Assert.Equal("adv_industrial", w.Countries[1].Cabinet["economia"]);
        Assert.Equal(850f, w.Countries[1].Political, 3);          // 1000 menos os 150 da nomeação
        Assert.Equal(1000f, w.Countries[1].Money, 3);             // e o cofre de produção fica intacto: aço não compra ministros
        Assert.Equal(1.10f, w.Countries[1].Stat("industry"), 3);
    }







}
