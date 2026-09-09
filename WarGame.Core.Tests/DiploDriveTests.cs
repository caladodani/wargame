using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Campanhas diplomáticas: a diplomacia que se FAZ. O que aqui se defende é que ela custa poder
/// político todos os dias, que o que rende entra na opinião como mais uma razão, que fechar a embaixada
/// desfaz o que ela ganhou, e que a garantia tem dentes.</summary>
public class DiploDriveTests
{
    private static (World w, DiploActionDef def) Setup(string actionId = "melhorar_relacoes", float purse = 1000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Political = purse;
        return (w, w.DiploActions[actionId]);
    }

    [Fact]
    public void A_embaixada_custa_todos_os_dias_e_o_que_rende_entra_na_opiniao()
    {
        var (w, def) = Setup();
        float before = Relations.Opinion(w, 2, 1);
        Assert.Null(new StartDiploDriveCommand(1, 2, def.Id).Validate(w));
        new StartDiploDriveCommand(1, 2, def.Id).Execute(w);

        var sys = new DiploDriveSystem();
        for (int i = 0; i < 10; i++) sys.Tick(w);

        var drive = DiploDriveSystem.Find(w, 1, 2, def.Id)!;
        Assert.Equal(10f * def.Magnitude, drive.Progress, 0.001f);
        Assert.Equal(1000f - def.CostStart - 10f * def.CostDay, w.Countries[1].Political, 0.001f);
        // a opinião subiu exactamente o que a campanha rendeu, e há uma razão nova a dizê-lo
        Assert.Equal(before + drive.Progress, Relations.Opinion(w, 2, 1), 0.001f);
        Assert.Contains(Relations.Lines(w, 2, 1), l => l.Kind == "esforco");
    }

    [Fact]
    public void Fechar_a_embaixada_desfaz_o_que_ela_ganhou()
    {
        var (w, def) = Setup();
        float before = Relations.Opinion(w, 2, 1);
        new StartDiploDriveCommand(1, 2, def.Id).Execute(w);
        var sys = new DiploDriveSystem();
        for (int i = 0; i < 10; i++) sys.Tick(w);
        Assert.True(Relations.Opinion(w, 2, 1) > before);

        new StopDiploDriveCommand(1, 2, def.Id).Execute(w);
        float purse = w.Countries[1].Political;
        for (int i = 0; i < 20; i++) sys.Tick(w);

        Assert.Empty(w.DiploDrives);                                   // desfez-se e desapareceu
        Assert.Equal(purse, w.Countries[1].Political, 0.001f);         // e uma embaixada fechada não custa nada
        Assert.Equal(before, Relations.Opinion(w, 2, 1), 0.001f);
        Assert.DoesNotContain(Relations.Lines(w, 2, 1), l => l.Kind == "esforco");
    }

    [Fact]
    public void A_garantia_faz_a_IA_desistir_do_alvo()
    {
        var (w, def) = Setup("garantir_independencia");
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        for (int i = 1; i <= 10; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 1);       // o garante
        for (int i = 11; i <= 15; i++) TestWorld.AddDivision(w, i, 2, TestWorld.Inf2, 4);     // o agressor

        Assert.False(DiploDriveSystem.Deterred(w, 2, 3));               // sem padrinho, o alvo é presa fácil
        new StartDiploDriveCommand(1, 3, def.Id).Execute(w);
        new DiploDriveSystem().Tick(w);
        Assert.True(DiploDriveSystem.Deterred(w, 2, 3));
        Assert.Contains(Relations.Lines(w, 3, 1), l => l.Kind == "garantia");
    }

    [Fact]
    public void A_propaganda_paga_mexe_no_partido_do_alvo_e_o_governo_dele_percebe()
    {
        var (w, def) = Setup("impulsionar_partido");
        w.Countries[1].Party = "autoritarios";
        foreach (var p in w.PartyDefs.Keys) w.Countries[2].Parties[p] = 25f;
        float before = w.Countries[2].Parties["autoritarios"];

        new StartDiploDriveCommand(1, 2, def.Id).Execute(w);
        var sys = new DiploDriveSystem();
        for (int i = 0; i < 20; i++) sys.Tick(w);

        Assert.True(w.Countries[2].Parties["autoritarios"] > before);   // o partido mais parecido connosco sobe
        Assert.Equal(100f, w.Countries[2].Parties.Values.Sum(), 0.01f); // e a opinião continua a somar 100
        Assert.Contains(Relations.Lines(w, 2, 1), l => l.Kind == "interferencia" && l.Value < 0f);
    }
}
