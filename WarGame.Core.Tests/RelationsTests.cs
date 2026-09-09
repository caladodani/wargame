using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A opinião entre países: o contrato é que o número É a soma das razões escritas, e que as portas
/// da diplomacia (pacto, voluntários) abrem e fecham por ele.</summary>
public class RelationsTests
{
    [Fact]
    public void O_numero_e_exactamente_a_soma_das_razoes()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2);
        w.LendLeases.Add(new LendLease { FromId = 1, ToId = 2, Share = 0.1f, SinceDay = 0 });

        var lines = Relations.Lines(w, 2, 1);
        Assert.NotEmpty(lines);                                       // vizinhança, ameaça e o material que recebe
        Assert.Equal(lines.Sum(l => l.Value), Relations.Opinion(w, 2, 1), 0.001f);
        // e a conta é dirigida: quem dá o material não fica agradecido a si próprio
        Assert.Contains(lines, l => l.Kind == "emprestimo" && l.Value > 0f);
        Assert.DoesNotContain(Relations.Lines(w, 1, 2), l => l.Kind == "emprestimo");
    }

    [Fact]
    public void Ideologia_oposta_afunda_a_opiniao_e_o_pacto_e_recusado()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Political = 100f;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);              // 1 é o maior: 2 tem-lhe medo
        new ProposeNonAggressionCommand(1, 2).Execute(w);
        Assert.True(w.HasPact(1, 2));                                  // vizinhos indiferentes assinam

        var (o, _) = TestWorld.Build();                                // o mesmo mundo, governos nas pontas
        TestWorld.LinearMap(o);
        o.Countries[1].Political = 100f;
        o.Countries[1].Party = "autoritarios";
        o.Countries[2].Party = "socialistas";
        TestWorld.AddDivision(o, 1, 1, TestWorld.Inf, 1);
        float feel = Relations.Opinion(o, 2, 1);
        Assert.True(feel < o.Rule("nap_min_opinion", -35f), $"opinião {feel:0.0} devia estar no fundo");
        new ProposeNonAggressionCommand(1, 2).Execute(o);
        Assert.False(o.HasPact(1, 2));                                 // não se assina com quem nos detesta
    }

    [Fact]
    public void Um_anfitriao_que_nos_detesta_nao_recebe_os_nossos_voluntarios()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        w.Regions[6].OwnerId = 3; w.Regions[6].ControllerId = 3;
        w.Countries[2].CapitalRegionId = 4;
        w.StartWar(2, 3);
        for (int i = 1; i <= 10; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 1);
        w.Rules["volunteer_min_tension"] = 0f;                          // a porta da tensão tem teste próprio
        Assert.Null(w.VolunteerBlock(1, 2));                            // com governos parecidos entram

        w.Countries[1].Party = "autoritarios";
        w.Countries[2].Party = "socialistas";
        string? no = w.VolunteerBlock(1, 2);
        Assert.NotNull(no);
        Assert.Contains("não nos quer lá", no);
    }
}
