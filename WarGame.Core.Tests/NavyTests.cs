using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A marinha por classes de casco (ship_class): a esquadra deixa de ser um número e passa a ser
/// uma composição. A escolta leva os tiros primeiro, a linha pesa no combate, o submarino aperta o
/// bloqueio e não protege ninguém — e um mundo sem classes nenhumas continua a lutar como antes.
///
/// Mapa: a mesma linha 1-2-3 (país 1) | 4-5-6 (país 2) dos outros testes navais, com a costa em 3 e 4.</summary>
public class NavyTests
{
    private static World Build(float money = 5000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (int id in new[] { 3, 4 })
        {
            var r = w.Regions[id];
            w.Regions[id] = new Region
            {
                Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
                ControllerId = r.ControllerId, Terrain = r.Terrain, Population = r.Population, Coastal = true,
                SeaZoneId = "golfo",           // as duas costas dão para o mesmo mar: é lá que se encontram
            };
            foreach (int n in r.Neighbours) w.Regions[id].Neighbours.Add(n);
        }
        w.Regions[3].SeaNeighbours[4] = 200f; w.Regions[4].SeaNeighbours[3] = 200f;
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values) { c.Money = money; c.IsPlayer = true; }
        return w;
    }

    [Fact]
    public void AsClassesVemDaBaseDeDados()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.ShipClasses.Count >= 5);
        var sub = w.ShipClasses["submarino"];
        Assert.Equal(0f, sub.Screen, 3);                       // não protege ninguém, nem a si
        Assert.True(sub.Blockade > w.ShipClasses["destroier"].Blockade);
        Assert.True(w.ShipClasses["destroier"].Screen > w.ShipClasses["cruzador"].Screen);
        Assert.Equal("corveta", Navy.Basic(w));                // é a que o botão de sempre compra
    }

    [Fact]
    public void OCascoCustaOQueASuaClasseVale()
    {
        var w = Build(money: 1000f);
        float before = w.Countries[1].Money;
        Assert.Null(new BuyShipCommand(1, "cruzador").Validate(w));
        new BuyShipCommand(1, "cruzador").Execute(w);
        Assert.Equal(before - w.Rule("naval_ship_cost") * w.ShipClasses["cruzador"].Cost, w.Countries[1].Money, 2);
        Assert.Equal(1f, w.Countries[1].Ships["cruzador"], 3);
        Assert.Equal(1f, w.Countries[1].Warships, 3);
        Assert.Contains("desconhecida", new BuyShipCommand(1, "encouracado").Validate(w)!);
    }

    [Fact]
    public void AEsquadraLevaOsMelhoresCascosParaATarefa()
    {
        var w = Build();
        var c = w.Countries[1];
        c.Ships["submarino"] = 3f; c.Ships["destroier"] = 3f;
        NavalMissionSystem.Assign(w, 1, 4, "bloqueio", 3f);      // bloquear pede submarinos
        var m = Assert.Single(w.NavalMissions);
        Assert.Equal(3f, m.Squadron["submarino"], 3);
        Assert.False(m.Squadron.ContainsKey("destroier"));

        NavalMissionSystem.Assign(w, 1, 3, "escolta", 3f);       // escoltar pede contratorpedeiros
        var esc = w.NavalMissions.Single(x => x.RegionId == 3);
        Assert.Equal(3f, esc.Squadron["destroier"], 3);
    }

    [Fact]
    public void OPortoNaoDaOMesmoNavioADuasEsquadras()
    {
        var w = Build();
        w.Countries[1].Ships["fragata"] = 4f;
        NavalMissionSystem.Assign(w, 1, 4, "bloqueio", 4f);
        NavalMissionSystem.Assign(w, 1, 3, "escolta", 4f);       // já não há fragatas livres nenhumas
        Assert.Equal(0f, Navy.Free(w, 1, "fragata"), 3);
        Assert.Equal(4f, w.NavalMissions.Sum(m => m.Ships), 3);
    }

    [Fact]
    public void AEscoltaLevaOsTirosEPoupaALinha()
    {
        var w = Build();
        var squadron = new Dictionary<string, float> { ["destroier"] = 6f, ["cruzador"] = 4f };
        var gone = Navy.Sink(w, squadron, 4f);
        Assert.True(gone["destroier"] > gone.GetValueOrDefault("cruzador"),
                    "a escolta tem de levar mais pancada do que a linha que ela protege");
        Assert.Equal(6f, squadron.Values.Sum(), 2);              // afundaram-se 4 dos 10
    }

    [Fact]
    public void SemEscoltaALinhaLevaTudo()
    {
        var w = Build();
        var squadron = new Dictionary<string, float> { ["cruzador"] = 5f };
        var gone = Navy.Sink(w, squadron, 2f);
        Assert.Equal(2f, gone["cruzador"], 2);
        Assert.Equal(3f, squadron["cruzador"], 2);
    }

    [Fact]
    public void OSubmarinoApertaOBloqueioQueOContratorpedeiroNaoAperta()
    {
        var subs = Build(); subs.Countries[2].Ships["submarino"] = 4f;
        NavalMissionSystem.Assign(subs, 2, 3, "bloqueio", 4f);
        var tins = Build(); tins.Countries[2].Ships["destroier"] = 4f;
        NavalMissionSystem.Assign(tins, 2, 3, "bloqueio", 4f);

        // a mesma costa, os mesmos quatro cascos: com submarinos fecha, com contratorpedeiros a escolta chega
        subs.Countries[1].Ships["fragata"] = 3f; tins.Countries[1].Ships["fragata"] = 3f;
        NavalMissionSystem.Assign(subs, 1, 3, "escolta", 3f);
        NavalMissionSystem.Assign(tins, 1, 3, "escolta", 3f);
        Assert.True(NavalMissionSystem.Blockaded(subs, 3), "quatro submarinos têm de fechar aquele cais");
        Assert.False(NavalMissionSystem.Blockaded(tins, 3), "quatro contratorpedeiros não fecham cais nenhum");
    }

    [Fact]
    public void AEsquadraMaisPesadaPerdeMenosNoMesmoMar()
    {
        var w = Build();
        w.Countries[1].Ships["porta_avioes"] = 4f;   // peso de linha
        w.Countries[2].Ships["patrulha"] = 4f;       // cascos leves
        NavalMissionSystem.Assign(w, 1, 3, "patrulha", 4f);
        NavalMissionSystem.Assign(w, 2, 4, "patrulha", 4f);
        w.Register(new NavalMissionSystem());
        w.Tick();
        float lost1 = 4f - w.Countries[1].Warships, lost2 = 4f - w.Countries[2].Warships;
        Assert.True(lost2 > lost1, $"a esquadra leve tinha de perder mais ({lost2:0.###} contra {lost1:0.###})");
    }

    [Fact]
    public void OsCascosSemClasseGanhamClasseNoEstaleiro()
    {
        var w = Build();
        w.Countries[1].Warships = 20f;               // marinha de partida: cascos sem classe nenhuma
        Assert.Equal(20f, w.Countries[1].Ships[""], 3);
        w.Register(new NavalMissionSystem());
        w.Tick();
        Assert.False(w.Countries[1].Ships.ContainsKey(""), "o estaleiro tinha de dar classe aos cascos soltos");
        Assert.Equal(20f, w.Countries[1].Warships, 2);           // sem inventar nem perder aço
        Assert.True(w.Countries[1].Ships.Count >= 2, "a marinha de partida tem linha e escolta");
    }

    [Fact]
    public void UmMundoSemClassesLutaComoAntes()
    {
        var w = Build();
        w.ShipClasses.Clear();                        // sem tabela, sem classes: o jogo antigo
        w.Countries[1].Warships = 5f;
        NavalMissionSystem.Assign(w, 1, 4, "bloqueio", 5f);
        var m = Assert.Single(w.NavalMissions);
        Assert.Equal(5f, m.Squadron[""], 3);
        Assert.Equal(5f, Navy.Power(w, m.Squadron), 3);          // sem classe, cada casco vale 1
        Assert.Equal("Navio ×5", Navy.Describe(w, m.Squadron));
    }
}
