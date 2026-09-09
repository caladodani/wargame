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

    /// <summary>O casco leva o campo com ele: enquanto o porta-aviões está no mar, há camas naquele mar; de
    /// volta ao porto, o céu de lá fecha-se outra vez. É a razão de existir um porta-aviões.</summary>
    [Fact]
    public void OPortaAvioesPoeCamasEmMarOndeNaoHaTerraNossa()
    {
        var w = Build();
        float raio = w.PlaneClasses["ataque"].RangeKm;
        Assert.True(w.Km(3, Alto) > raio);                                      // nem o campo mais perto lá chega
        Assert.False(AirBases.Covers(w, 1, Alto, raio));
        Assert.Equal(0f, AirBases.Room(w, 1, Alto, raio), 3);
        Assert.Empty(AirBases.Decks(w, 1));

        var esq = Esquadra(w, 1, Alto, "patrulha", ("porta_avioes", 2f), ("destroier", 4f));
        float camas = 2f * w.ShipClasses["porta_avioes"].Deck;
        Assert.Equal(camas, AirBases.Decks(w, 1)[Alto], 3);
        Assert.True(AirBases.Covers(w, 1, Alto, raio));
        Assert.Equal(camas, AirBases.Room(w, 1, Alto, raio, camas), 3);

        esq.Squadron.Remove("porta_avioes");                                    // os conveses voltaram ao porto
        Assert.Empty(AirBases.Decks(w, 1));
        Assert.False(AirBases.Covers(w, 1, Alto, raio));
    }

    /// <summary>O alcance de bordo é curto de propósito: quem vai perto é o casco, não o avião. Um avião de
    /// 1 100 km levanta do convés com os 600 km da regra e não com os dele.</summary>
    [Fact]
    public void DoConvesLevantaSeComOAlcanceDeBordo()
    {
        var w = Build();
        float raio = w.PlaneClasses["ataque"].RangeKm, bordo = w.Rule("air_carrier_range");
        Assert.True(raio > bordo);
        Esquadra(w, 1, Alto, "patrulha", ("porta_avioes", 2f));

        Assert.True(w.Km(Alto, Perto) < bordo && w.Km(Alto, Longe) > bordo);
        Assert.True(w.Km(Alto, Longe) < raio);                                  // do chão, o avião lá chegava
        Assert.True(AirBases.Covers(w, 1, Perto, raio));
        Assert.False(AirBases.Covers(w, 1, Longe, raio));                       // do convés, não

        var berco = Assert.Single(AirBases.Berths(w, 1, Alto, raio), b => b.Carrier);
        Assert.Equal(bordo, berco.Reach, 3);
    }

    /// <summary>No convés só assenta quem cabe num convés: uma missão de caças pesados não dorme lá, e a
    /// cama que o painel promete conta com isso.</summary>
    [Fact]
    public void NoConvesSoAssentaQuemCabeNumConves()
    {
        var w = Build();
        w.Countries[1].Planes["caca_pesado"] = 10f;
        Esquadra(w, 1, Alto, "patrulha", ("porta_avioes", 2f));
        float camas = 2f * w.ShipClasses["porta_avioes"].Deck, raio = w.PlaneClasses["ataque"].RangeKm;

        Assert.Equal(camas, AirBases.Room(w, 1, Alto, raio, camas), 3);
        Assert.Equal(0f, AirBases.Room(w, 1, Alto, raio, 0f), 3);               // nada que caiba: convés vazio

        // e a missão a sério: as asas de ataque dormem no convés, o caça pesado fica em terra
        AirMissionSystem.Assign(w, 1, Alto, "ataque_naval", camas);
        var m = Assert.Single(w.AirMissions);
        Assert.Equal(camas, m.Squadron["ataque"], 3);
        Assert.Equal(camas, AirBases.Seated(w, m), 3);
        Assert.Equal(Alto, AirBases.Home(w, m));
    }

    /// <summary>O céu deita aço ao fundo. Era a arma que faltava: uma potência sem marinha não tinha maneira
    /// nenhuma de disputar o mar, e um bombardeiro torpedeiro nunca tinha afundado um navio.</summary>
    [Fact]
    public void AsAsasAfundamAEsquadraDeles()
    {
        var w = Build();
        Assert.Equal("naval", w.AirMissionDefs["ataque_naval"].Effect);
        Esquadra(w, 1, Alto, "patrulha", ("porta_avioes", 2f));
        var deles = Esquadra(w, 2, Alto, "bloqueio", ("destroier", 10f));
        AirMissionSystem.Assign(w, 1, Alto, "ataque_naval", 8f);

        float afundado = 0f; int quem = 0;
        w.Events.Subscribe<AirNavalStrike>(e => { afundado += e.Ships; quem = e.TargetCountryId; });

        w.Tick();
        Assert.True(afundado > 0f, "o céu não afundou nada");
        Assert.Equal(2, quem);
        // no mesmo mar a nossa esquadra também lhes bate: o que o céu deitou ao fundo é parte do que falta
        Assert.True(deles.Ships <= 10f - afundado + 0.001f, $"{deles.Ships} navios com {afundado} pelo ar");
        Assert.True(w.Countries[1].AirXp > 0f);                                  // afundar aço ensina
    }

    /// <summary>Quem tem o céu por cima da esquadra defende-a: a superioridade aérea deles trava o ataque
    /// naval. É a razão de se mandarem caças com os torpedeiros.</summary>
    [Fact]
    public void OCeuDelesPorCimaDoMarTravaOTorpedo()
    {
        float Afundado(bool escudo)
        {
            var w = Build();
            Esquadra(w, 1, Alto, "patrulha", ("porta_avioes", 2f));
            var deles = Esquadra(w, 2, Alto, "bloqueio", ("destroier", 10f));
            AirMissionSystem.Assign(w, 1, Alto, "ataque_naval", 8f);
            if (escudo)
            {
                w.Countries[2].Planes["caca_pesado"] = 30f;
                AirMissionSystem.Assign(w, 2, Alto, "superioridade", 30f);
            }
            float ido = 0f;
            w.Events.Subscribe<AirNavalStrike>(e => ido += e.Ships);
            w.Tick();
            return ido;
        }

        float sozinho = Afundado(false), defendido = Afundado(true);
        Assert.True(sozinho > 0f);
        Assert.True(defendido < sozinho, $"o escudo não travou nada: {defendido} contra {sozinho}");
    }

    /// <summary>Onde não há mar não se manda um torpedo, e o resto das recusas continua de pé.</summary>
    [Fact]
    public void OTorpedoPedeMarPorBaixo()
    {
        var w = Build();
        Esquadra(w, 1, Alto, "patrulha", ("porta_avioes", 2f));
        Assert.Equal("aquilo não tem mar", AirMissionSystem.Block(w, 1, 4, "ataque_naval", 2f));
        Assert.Null(AirMissionSystem.Block(w, 1, Alto, "ataque_naval", 2f));

        // a nossa própria costa vale: a esquadra que nos bloqueia está no nosso mar
        w.Regions[Alto].ControllerId = 1;
        Assert.Null(AirMissionSystem.Block(w, 1, Alto, "ataque_naval", 2f));
    }

    /// <summary>A IA manda uma fatia ao aço deles quando há esquadra à vista, e o resto continua a ir à
    /// frente — o mar não come a aviação toda.</summary>
    [Fact]
    public void AIaMandaUmaFatiaAoAcoDeles()
    {
        var w = Build();
        w.Countries[1].IsPlayer = false;
        Esquadra(w, 1, Alto, "patrulha", ("porta_avioes", 2f));
        Esquadra(w, 2, Alto, "bloqueio", ("destroier", 10f));
        Assert.Equal(Alto, AirMissionSystem.NavalTarget(w, 1));

        w.Tick();
        var mar = w.AirMissions.SingleOrDefault(m => m.MissionId == "ataque_naval");
        Assert.NotNull(mar);
        Assert.Equal(Alto, mar!.RegionId);
        Assert.Contains(w.AirMissions, m => m.MissionId == "superioridade");     // a frente não ficou às moscas
    }

    /// <summary>A linha do hangar conta os mares com convés: o campo que flutua também é chão do céu.</summary>
    [Fact]
    public void ALinhaDoHangarContaOsConveses()
    {
        var w = Build();
        Assert.DoesNotContain("convés", AirBases.Short(w, 1));

        Esquadra(w, 1, Alto, "patrulha", ("porta_avioes", 2f));
        string linha = AirBases.Short(w, 1);
        Assert.Contains("1 mar com convés", linha);
        Assert.Contains($"{2f * w.ShipClasses["porta_avioes"].Deck:0.#} camas a flutuar", linha);
    }
}
