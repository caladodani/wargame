using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O porta-aviões e o ataque naval: o mar ganhou céu e o céu ganhou o mar.
///
/// O casco `porta_avioes` estava na tabela desde que a marinha deixou de ser um número, mas não levava
/// avião nenhum — era um cruzador caro com outro nome. E a aviação, do outro lado, nunca tinha afundado um
/// navio: um país sem marinha não tinha maneira nenhuma de disputar o mar.
///
/// O que estes testes guardam: o convés é campo de aviação a flutuar (só recebe aviões que caibam num
/// convés, e com o alcance curto de bordo), e as asas em ataque naval mandam aço ao fundo — travadas pela
/// superioridade aérea de quem lá está no mar, que é a razão de se mandarem caças com os torpedeiros.</summary>
public class CarrierTests
{
    private const int Alto = 7;      // alto-mar longe de toda a terra: só lá chega quem leva o campo consigo
    private const int Perto = 8;     // a 556 km do alto-mar: cabe no alcance de bordo
    private const int Longe = 9;     // a 778 km: já não cabe de bordo, mas cabia no avião se ele tivesse chão

    /// <summary>Linha do costume mais três mares ao largo, e os dois países de mão humana (a IA tem o seu
    /// próprio teste). As asas do país 1 são de ataque: valem no mar e cabem num convés.</summary>
    private static World Build(float wings = 20f, float ships = 12f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        TestWorld.Fields(w);
        Mares(w);
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values)
        {
            c.Money = 5000f; c.IsPlayer = true;
            c.Planes.Clear(); c.Planes["ataque"] = wings;
            c.Ships.Clear(); c.Ships["porta_avioes"] = 2f; c.Ships["destroier"] = ships - 2f;
        }
        w.Register(new AirMissionSystem());
        w.Register(new NavalMissionSystem());
        return w;
    }

    /// <summary>Três regiões de mar ao largo da linha: o alto-mar a 60° de longitude (mais de 5 000 km da
    /// província nossa mais próxima) e duas vizinhas dele a 5° e a 7°, para medir o alcance de bordo.</summary>
    private static void Mares(World w)
    {
        foreach (var (id, lon) in new[] { (Alto, 60f), (Perto, 65f), (Longe, 67f) })
            w.Regions[id] = new Region
            {
                Id = id, Name = "M" + id, OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
                Terrain = "plain", Population = 1000, Lon = lon, CenterX = id * 100, CenterY = 500,
                Coastal = true,
            };
    }

    /// <summary>Esquadra deste país naquele mar, escrita à mão para o teste não depender da escolha de
    /// cascos: é a composição que interessa, não quem a escolheu.</summary>
    private static NavalMission Esquadra(World w, int countryId, int regionId, string missionId,
                                        params (string Class, float N)[] squadron)
    {
        var m = new NavalMission { CountryId = countryId, RegionId = regionId, MissionId = missionId, Name = "E" };
        foreach (var (cls, n) in squadron) m.Squadron[cls] = n;
        w.NavalMissions.Add(m);
        return m;
    }

    /// <summary>O convés vem da tabela: só o porta-aviões leva asas ao mar, e só os aviões pequenos lá
    /// cabem. Nada disto está escrito em código — são colunas de ship_class e plane_class.</summary>
    [Fact]
    public void OConvesEODaTabelaEOAviaoGrandeNaoCabe()
    {
        var w = Build();
        Assert.True(w.ShipClasses["porta_avioes"].IsCarrier);
        Assert.False(w.ShipClasses["destroier"].IsCarrier);
        Assert.Equal(w.ShipClasses["porta_avioes"].Deck, Navy.Deck(w, "porta_avioes"), 3);
        Assert.Equal(0f, Navy.Deck(w, "destroier"), 3);
        Assert.Equal(0f, Navy.Deck(w, ""), 3);                                  // navio sem classe não tem convés

        float deck = w.ShipClasses["porta_avioes"].Deck;
        Assert.Equal(2f * deck, Navy.Decks(w, new Dictionary<string, float>
            { ["porta_avioes"] = 2f, ["destroier"] = 5f }), 3);

        Assert.True(w.PlaneClasses["ataque"].Deck);
        Assert.False(w.PlaneClasses["caca_pesado"].Deck);                       // grande de mais para um convés
        Assert.Equal(3f, AirBases.DeckWings(w, new Dictionary<string, float>
            { ["ataque"] = 3f, ["caca_pesado"] = 9f, [""] = 4f }), 3);          // a asa sem modelo também não cabe
    }








}
