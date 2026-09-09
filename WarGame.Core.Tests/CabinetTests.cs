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

    /// <summary>O ministro tem cor: sentado, faz campanha de dentro do Estado e a barra do partido dele
    /// anda todos os dias mais depressa do que andava com a cadeira vazia.</summary>
    [Fact]
    public void O_ministro_faz_campanha_pelo_partido_dele()
    {
        var w = Build();
        var c = w.Countries[1];
        World.SettleParties(w, c);
        var nac = w.PartyDefs["nacionalistas"];
        float antes = PartySystem.Drift(w, c, nac);

        new AppointAdvisorCommand(1, "adv_orador").Execute(w);      // orador do regime: nacionalistas, 0.07/dia

        Assert.Equal(0.07f, CabinetSystem.PartyPull(w, c, "nacionalistas"), 3);
        Assert.Equal(0f, CabinetSystem.PartyPull(w, c, "socialistas"), 3);
        Assert.Equal(antes + 0.07f, PartySystem.Drift(w, c, nac), 3);
    }

    /// <summary>O técnico é a escolha de quem quer os números e não quer mexer na rua: não puxa por
    /// partido nenhum nem mexe na estabilidade.</summary>
    [Fact]
    public void O_tecnico_nao_mexe_na_rua()
    {
        var w = Build();
        var c = w.Countries[1];
        World.SettleParties(w, c);

        new AppointAdvisorCommand(1, "adv_planeador").Execute(w);   // planeador de guerra: sem partido

        Assert.Empty(CabinetSystem.Ministers(w, c));
        Assert.Equal(0f, CabinetSystem.StabilityShift(w, c), 4);
        foreach (var p in w.PartyDefs.Values) Assert.Equal(0f, CabinetSystem.PartyPull(w, c, p.Id), 4);
    }

    /// <summary>Gente da oposição à mesa do governo abana a casa; gente do governo segura-a. É a conta que
    /// o país paga em estabilidade todos os dias.</summary>
    [Fact]
    public void Ministro_da_oposicao_custa_estabilidade()
    {
        var w = Build();
        var c = w.Countries[1];
        World.SettleParties(w, c);
        c.Party = "nacionalistas";
        c.Stability = 50f;

        new AppointAdvisorCommand(1, "adv_orador").Execute(w);      // do governo
        Assert.Equal(0, CabinetSystem.Rivals(w, c));
        Assert.Equal(0.01f, CabinetSystem.StabilityShift(w, c), 4);

        new AppointAdvisorCommand(1, "adv_sindicalista").Execute(w); // socialista: rival
        Assert.Equal(1, CabinetSystem.Rivals(w, c));
        Assert.Equal(0.01f - 0.02f, CabinetSystem.StabilityShift(w, c), 4);

        new CabinetSystem().Tick(w);
        Assert.Equal(49.99f, c.Stability, 3);
    }







}
