using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Faixa de avisos: o jogo levanta a mão sozinho quando o cofre seca, a tropa fica sem
/// abastecimento, a fronteira está aberta, a terra ocupada ferve ou as fábricas param.</summary>
public class AlertsTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = true;
        w.Countries[1].Money = 1000f;
        Fill(w);                                    // laboratórios cheios: um aviso a menos no caminho
        return w;
    }

    /// <summary>Ocupa todas as ranhuras de investigação do jogador (o aviso das ranhuras livres é Info e
    /// aparecia em todos os testes se ficasse alguma por preencher).</summary>
    private static void Fill(World w)
    {
        var c = w.Countries[1];
        c.Research.Clear();
        for (int i = 0; i < ResearchSystem.Slots(w, c); i++) c.Research["tech_" + i] = 0f;
    }

    private static Alert? Find(World w, string id) => Alerts.For(w, 1).FirstOrDefault(a => a.Id == id);

    [Fact]
    public void TheThresholdsComeFromTheDatabase()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(15f, w.Rule("alert_money_days"));
        Assert.Equal(0.6f, w.Rule("alert_supply"), 3);
        Assert.Equal(0.5f, w.Rule("alert_resistance"), 3);
        Assert.Equal(150f, w.Rule("alert_idle_money"));
    }

    [Fact]
    public void AQuietCountryHasNothingBurning()
    {
        var w = Setup();
        Assert.DoesNotContain(Alerts.For(w, 1), a => a.Level == AlertLevel.Danger);
    }

    [Fact]
    public void TroopsDrinkingSandAreTheLoudestAlert()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 2);
        w.Divisions[10].Supply = 0.2f;

        var a = Find(w, "supply");
        Assert.NotNull(a);
        Assert.Equal(AlertLevel.Danger, a!.Level);
        Assert.Equal(2, a.RegionId);                        // leva o mapa ao sítio do problema
        Assert.Equal(AlertLevel.Danger, Alerts.For(w, 1)[0].Level);
    }

    [Fact]
    public void AWellFedArmyRaisesNoHand()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 2);
        w.Divisions[10].Supply = 1f;
        Assert.Null(Find(w, "supply"));
    }

    [Fact]
    public void AnOpenBorderIsOnlyOpenInWar()
    {
        var w = Setup();
        Assert.Null(Find(w, "border"));                     // em paz, a fronteira não é problema

        w.StartWar(1, 2);
        var a = Find(w, "border");
        Assert.NotNull(a);
        Assert.Equal(3, a!.RegionId);                       // R3 encosta ao R4 deles e está vazia
    }

    [Fact]
    public void AGarrisonedBorderStopsTheWarning()
    {
        var w = Setup();
        w.StartWar(1, 2);
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 3);
        Assert.Null(Find(w, "border"));
    }

    [Fact]
    public void OccupiedGroundThatBoilsIsNamed()
    {
        var w = Setup();
        var r = w.Regions[4];
        r.ControllerId = 1;                                  // tomada, mas ainda deles
        r.Resistance = 0.8f;

        var a = Find(w, "resistance");
        Assert.NotNull(a);
        Assert.Contains(r.Name, a!.Text);
        Assert.Equal(AlertLevel.Danger, a.Level);
        Assert.Equal(4, a.RegionId);
    }

    [Fact]
    public void AnEmptyQueueWithMoneyInTheChestIsWaste()
    {
        var w = Setup();
        var a = Find(w, "queue");
        Assert.NotNull(a);
        Assert.Equal(AlertLevel.Warn, a!.Level);

        w.Countries[1].Money = 10f;                          // sem dinheiro, a fila vazia não é desperdício
        Assert.Null(Find(w, "queue"));
    }

    [Fact]
    public void IdleLaboratoriesAreJustANote()
    {
        var w = Setup();
        Assert.Null(Find(w, "research"));                    // ranhuras cheias: nada a dizer

        var c = w.Countries[1];
        c.Research.Remove(c.Research.Keys.First());          // uma linha largada
        Assert.Equal("1 ranhura de investigação livre", Find(w, "research")!.Text);
        Assert.Equal(AlertLevel.Info, Find(w, "research")!.Level);

        c.Research.Clear();
        Assert.Equal("ninguém está a investigar nada", Find(w, "research")!.Text);
    }

    [Fact]
    public void OffersOnTheTableAreOnTheStrip()
    {
        var w = Setup();
        w.Offers.Add(new PendingOffer { FromId = 2, ToId = 1, Kind = "paz", Day = w.Clock.Day, ExpiresDay = w.Clock.Day + 10 });
        Assert.Equal("1 proposta à espera de resposta", Find(w, "offers")!.Text);
    }

    [Fact]
    public void TheStripIsSortedByHowMuchItBurns()
    {
        var w = Setup();
        w.Countries[1].Research.Clear();                      // Info
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 2);
        w.Divisions[10].Supply = 0.1f;                        // Danger

        var list = Alerts.For(w, 1);
        Assert.Equal(AlertLevel.Danger, list[0].Level);
        Assert.Equal(AlertLevel.Info, list[^1].Level);
    }

    [Fact]
    public void TheKeyChangesOnlyWhenTheStripChanges()
    {
        var w = Setup();
        string before = Alerts.Key(Alerts.For(w, 1));
        Assert.Equal(before, Alerts.Key(Alerts.For(w, 1)));

        w.Countries[1].Research.Clear();
        Assert.NotEqual(before, Alerts.Key(Alerts.For(w, 1)));
    }

    [Fact]
    public void ALostFrontRaisesItsOwnAlert()
    {
        var w = Setup();
        w.Rules["front_width_plain"] = 1f;                 // uma divisão de cada lado é o que a frente leva
        var battle = new Battle { RegionId = 4, AttackerCountryId = 1 };
        battle.Attackers.Add(TestWorld.AddDivision(w, 101, 1, TestWorld.Inf, 4).Id);
        battle.Defenders.Add(TestWorld.AddDivision(w, 201, 2, TestWorld.Inf2, 4).Id);
        w.ActiveBattles.Add(battle);
        Assert.Null(Find(w, "battle"));                    // um contra um: nada a dizer

        w.Rules["front_width_plain"] = 3f;
        battle.Defenders.Add(TestWorld.AddDivision(w, 202, 2, TestWorld.Inf2, 4).Id);
        var alert = Find(w, "battle");
        Assert.NotNull(alert);
        Assert.Equal(AlertLevel.Warn, alert!.Level);
        Assert.Equal(4, alert.RegionId);
        Assert.Contains("1 contra 2", alert.Text);
    }

    [Fact]
    public void ABattleBetweenOthersIsNotOurProblem()
    {
        var w = Setup();
        w.Countries[3] = new Country { Id = 3, Name = "Gama", Tag = "GAM" };
        var battle = new Battle { RegionId = 4, AttackerCountryId = 3 };
        battle.Attackers.Add(TestWorld.AddDivision(w, 301, 3, TestWorld.Inf, 4).Id);
        battle.Defenders.Add(TestWorld.AddDivision(w, 201, 2, TestWorld.Inf2, 4).Id);
        battle.Defenders.Add(TestWorld.AddDivision(w, 202, 2, TestWorld.Inf2, 4).Id);
        w.ActiveBattles.Add(battle);
        Assert.Null(Find(w, "battle"));
    }

    [Fact]
    public void ACapitulatedCountryHasNoStrip()
    {
        var w = Setup();
        w.Countries[1].Capitulated = true;
        Assert.Empty(Alerts.For(w, 1));
    }
}
