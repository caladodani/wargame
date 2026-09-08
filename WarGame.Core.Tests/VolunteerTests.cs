using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Voluntários: divisões nossas que se batem na guerra de outro sem nos meterem nela. O que aqui se
/// defende é a linha que separa as duas coisas que uma divisão voluntária é ao mesmo tempo — de quem ela
/// obedece hoje (o anfitrião) e de quem ela é (nós, que a pagamos e a enterramos).</summary>
public class VolunteerTests
{
    /// <summary>Três países: 1 é a casa, 2 o anfitrião em guerra, 3 o inimigo dele. A casa não está em
    /// guerra com ninguém — é esse o ponto de mandar voluntários.</summary>
    private static World Setup(int army = 10)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 6, Manpower = 1e9f };
        w.Regions[6].OwnerId = 3; w.Regions[6].ControllerId = 3;
        w.Countries[2].CapitalRegionId = 4;
        w.StartWar(2, 3);
        for (int i = 1; i <= army; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 1);
        w.Register(new VolunteerSystem());
        return w;
    }

    [Fact]
    public void MandarVoluntarios_TrocamDeBandeira_MasContinuamNossas()
    {
        var w = Setup();
        Assert.Null(new SendVolunteersCommand(1, 2, 2).Validate(w));
        new SendVolunteersCommand(1, 2, 2).Execute(w);

        var away = w.Divisions.Values.Where(d => d.IsVolunteer).ToList();
        Assert.Equal(2, away.Count);
        Assert.All(away, d => Assert.Equal(2, d.CountryId));      // obedecem ao anfitrião
        Assert.All(away, d => Assert.Equal(1, d.HomeId));         // e continuam a ser nossas
        Assert.All(away, d => Assert.Equal(4, d.RegionId));       // desembarcam na capital dele
        Assert.All(away, d => Assert.Contains(d.Id, w.Regions[4].DivisionIds));
        Assert.False(w.AreAtWar(1, 3));                           // e nós continuamos fora da guerra
    }

    [Fact]
    public void OTectoEUmaFatiaDoExercito()
    {
        var w = Setup(army: 10);
        Assert.Equal((int)(10 * w.Rule("volunteer_share")), VolunteerSystem.Cap(w, 1));   // 2 de 10

        new SendVolunteersCommand(1, 2, 5).Execute(w);        // pede 5, cabem 2
        Assert.Equal(2, VolunteerSystem.Away(w, 1));
        Assert.Contains("que é o limite", new SendVolunteersCommand(1, 2).Validate(w)!);
    }

    [Fact]
    public void ExercitoPequeno_NaoEmpresta()
    {
        var w = Setup(army: 3);                               // abaixo de volunteer_min_army
        Assert.Equal(0, VolunteerSystem.Cap(w, 1));
        Assert.Contains("não chega para emprestar", new SendVolunteersCommand(1, 2).Validate(w)!);
    }

    [Fact]
    public void SoParaGuerraAlheia_E_Nunca_Para_O_Inimigo()
    {
        var w = Setup();
        Assert.Contains("para a nossa guerra", new SendVolunteersCommand(1, 1).Validate(w)!);
        Assert.Contains("anfitrião inválido", new SendVolunteersCommand(1, 99).Validate(w)!);
        Assert.Null(new SendVolunteersCommand(1, 3).Validate(w));   // dos dois lados de uma guerra alheia

        w.StartWar(1, 2);                                     // passámos a inimigos de quem íamos ajudar
        Assert.Contains("estamos em guerra", new SendVolunteersCommand(1, 2).Validate(w)!);

        w.EndWar(1, 2); w.EndWar(2, 3);                       // e sem guerra nenhuma não há para onde ir
        Assert.Contains("não está em guerra", new SendVolunteersCommand(1, 2).Validate(w)!);
    }

    [Fact]
    public void QuemEstaEmBatalha_NaoEmbarca()
    {
        var w = Setup(army: 10);
        var battle = new Battle { RegionId = 1, AttackerCountryId = 3 };
        battle.Defenders.AddRange(w.Divisions.Values.Take(9).Select(d => d.Id));
        w.ActiveBattles.Add(battle);
        Assert.Equal(1, VolunteerSystem.Send(w, 1, 2, 2));     // só sobra uma livre
        Assert.All(w.Divisions.Values.Where(d => d.IsVolunteer), d => Assert.DoesNotContain(d.Id, battle.Defenders));
    }

    [Fact]
    public void LongeDeCasa_BatemSePior()
    {
        var w = Setup();
        var st = w.Stats.Get(TestWorld.Inf);
        var (homeF, homeM) = w.Modifiers.Evaluate("str", st, new ModContext());
        var (awayF, awayM) = w.Modifiers.Evaluate("str", st, new ModContext().With("volunteer", "true"));
        Assert.True(awayM + awayF < homeM + homeF);            // e quanto pior é a linha da BD que decide
    }

    [Fact]
    public void OsReforcosSaemDoNossoCofre()
    {
        var w = Setup();
        w.Register(new RecoverySystem());
        new SendVolunteersCommand(1, 2, 1).Execute(w);
        var d = w.Divisions.Values.First(x => x.IsVolunteer);
        d.Hp = 50f;
        w.Countries[1].Manpower = 100_000f; w.Countries[1].Money = 100f;
        w.Countries[2].Manpower = 100_000f; w.Countries[2].Money = 100f;

        TestWorld.Days(w, 1);

        Assert.True(d.Hp > 50f);                               // recompôs-se
        Assert.True(w.Countries[1].Manpower < 100_000f);       // à nossa custa
        Assert.Equal(100_000f, w.Countries[2].Manpower, 0.01f);// e não à do anfitrião
    }

    [Fact]
    public void AGuerraDeleAcaba_E_Eles_Voltam_Para_Casa()
    {
        var w = Setup();
        new SendVolunteersCommand(1, 2, 2).Execute(w);
        Assert.Equal(2, VolunteerSystem.Away(w, 1));

        w.EndWar(2, 3);
        TestWorld.Days(w, 1);

        Assert.Equal(0, VolunteerSystem.Away(w, 1));
        Assert.All(w.Divisions.Values, d => Assert.Equal(1, d.CountryId));
        Assert.All(w.Divisions.Values, d => Assert.Equal(1, d.RegionId));   // de volta à nossa capital
    }

    [Fact]
    public void GuerraEntreNos_TraLosNoProprioDia()
    {
        var w = Setup();
        new SendVolunteersCommand(1, 2, 2).Execute(w);
        w.StartWar(1, 2);                                      // ninguém deixa tropa dentro do inimigo
        TestWorld.Days(w, 1);
        Assert.Equal(0, VolunteerSystem.Away(w, 1));
        Assert.All(w.Divisions.Values, d => Assert.Equal(1, d.CountryId));
    }

    [Fact]
    public void AnfitriaoCapitula_E_Eles_Voltam()
    {
        var w = Setup();
        new SendVolunteersCommand(1, 2, 2).Execute(w);
        w.Countries[2].Capitulated = true;
        TestWorld.Days(w, 1);
        Assert.Equal(0, VolunteerSystem.Away(w, 1));
    }

    [Fact]
    public void ChamarDeVolta_Quando_Nos_Quisermos()
    {
        var w = Setup();
        Assert.Contains("não temos voluntários", new RecallVolunteersCommand(1).Validate(w)!);
        new SendVolunteersCommand(1, 2, 2).Execute(w);
        Assert.Null(new RecallVolunteersCommand(1, 2).Validate(w));
        new RecallVolunteersCommand(1, 2).Execute(w);
        Assert.Equal(0, VolunteerSystem.Away(w, 1));
        Assert.All(w.Divisions.Values, d => Assert.Equal(1, d.RegionId));
    }

    [Fact]
    public void AFaixa_Nao_Deixa_Esquecer_Os_Que_Estao_La_Fora()
    {
        var w = Setup();
        Assert.DoesNotContain(Alerts.For(w, 1), a => a.Id == "volunteers");
        new SendVolunteersCommand(1, 2, 2).Execute(w);
        var alert = Assert.Single(Alerts.For(w, 1), a => a.Id == "volunteers");
        Assert.Contains("2 divisões nossas", alert.Text);
        Assert.Contains("Beta", alert.Text);
    }

    [Fact]
    public void MorrerLaFora_Cansa_A_Nossa_Guerra_E_Nao_A_Dele()
    {
        var w = Setup();
        new SendVolunteersCommand(1, 2, 1).Execute(w);
        var d = w.Divisions.Values.First(x => x.IsVolunteer);
        d.Hp = 0.0001f;

        var enemy = TestWorld.AddDivision(w, 50, 3, TestWorld.Inf2, 4);
        new CombatSystem().ResolveTick(w, new List<Division> { enemy }, new List<Division> { d },
                                       new ModContext(), new ModContext());

        Assert.Equal(0f, d.Hp);
        Assert.True(w.Countries[1].WarExhaustion > 0f);        // os nossos mortos são nossos
        Assert.Equal(0f, w.Countries[2].WarExhaustion, 0.001f);
    }

    [Fact]
    public void RealDb_TemAsRegrasEAMordidaDeQuemVaiLonge()
    {
        var w = FactionTests.BuildReal();
        Assert.True(w.Rule("volunteer_share", 0f) > 0f);
        Assert.True(w.Rule("volunteer_min_army", 0f) >= 1f);
        var st = w.Stats.Get(TestWorld.Armor);
        var (homeF, homeM) = w.Modifiers.Evaluate("str", st, new ModContext());
        var (awayF, awayM) = w.Modifiers.Evaluate("str", st, new ModContext().With("volunteer", "true"));
        Assert.True(awayM + awayF < homeM + homeF);
    }
}
