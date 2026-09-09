using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Zonas estratégicas (HoI4): o céu e o mar deixaram de ser da província e passaram a ser da zona.
/// Uma asa destacada vale em toda a zona, duas aviações na mesma zona encontram-se, e um bloqueio fecha
/// todos os cais daquele mar.</summary>
public class ZoneTests
{
    /// <summary>Mundo de duas zonas: regiões 1-2 na zona "oeste", 3-4 na zona "leste"; 2 e 3 são costa do
    /// mesmo mar "golfo". País 1 dono de 1-2, país 2 dono de 3-4, em guerra.</summary>
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        w.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1, Manpower = 1e9f, AirPower = 20f, Warships = 20f, Money = 5000f, IsPlayer = true };
        w.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = 4, Manpower = 1e9f, AirPower = 20f, Warships = 20f, Money = 5000f, IsPlayer = true };
        for (int i = 1; i <= 4; i++)
        {
            int owner = i <= 2 ? 1 : 2;
            w.Regions[i] = new Region
            {
                Id = i, Name = "R" + i, OwnerId = owner, InitialOwnerId = owner, ControllerId = owner,
                Terrain = "plain", Population = 10_000_000, Coastal = i is 2 or 3,
                ZoneId = i <= 2 ? "oeste" : "leste", SeaZoneId = i is 2 or 3 ? "golfo" : "",
            };
        }
        w.Regions[1].Neighbours.Add(2); w.Regions[2].Neighbours.Add(1);
        w.Regions[2].Neighbours.Add(3); w.Regions[3].Neighbours.Add(2);
        w.Regions[3].Neighbours.Add(4); w.Regions[4].Neighbours.Add(3);
        w.Regions[2].SeaNeighbours[3] = 100f; w.Regions[3].SeaNeighbours[2] = 100f;
        w.StartWar(1, 2);
        return w;
    }

    [Fact]
    public void OsMaresVemDaBaseDeDados()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.Zones.Values.Count(z => z.Kind == "mar") >= 20);
        Assert.Equal("Mediterrâneo Ocidental", w.Zones["mediterraneo_oeste"].Name);
        Assert.Equal("onda", w.Zones["mediterraneo_oeste"].Glyph);
    }

    [Fact]
    public void AsAsasDaZonaValemNoCeuDaRegiaoAoLado()
    {
        var w = Build();
        AirMissionSystem.Assign(w, 1, 1, "superioridade", 4f);
        // a missão está na região 1, mas o céu da 2 é o mesmo céu: a zona é a mesma
        Assert.True(AirMissionSystem.Superiority(w, 2, 1) > 0f);
        // a região 3 é outra zona: as asas de lá não chegam
        Assert.Equal(0f, AirMissionSystem.Superiority(w, 3, 1), 0.001f);
    }

    [Fact]
    public void SemZonaCadaRegiaoEOSeuCeu()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);          // mundo de teste sem zonas nenhumas
        w.StartWar(1, 2);
        w.Countries[1].AirPower = 10f;
        AirMissionSystem.Assign(w, 1, 1, "superioridade", 4f);
        Assert.True(AirMissionSystem.Superiority(w, 1, 1) > 0f);
        Assert.Equal(0f, AirMissionSystem.Superiority(w, 2, 1), 0.001f);
    }

    [Fact]
    public void ADuasAviacoesNaMesmaZonaEncontramSe()
    {
        var w = Build();
        AirMissionSystem.Assign(w, 1, 1, "superioridade", 6f);   // sobre casa nossa
        AirMissionSystem.Assign(w, 2, 2, "superioridade", 6f);   // outra região, mesma zona
        w.Register(new AirMissionSystem());
        float before = w.Countries[1].AirPower;
        w.Tick();
        Assert.True(w.Countries[1].AirPower < before);            // houve combate: caíram asas dos dois lados
        Assert.True(w.AirMissions.Where(m => m.CountryId == 2).Sum(m => m.Wings) < 6f);
    }

    [Fact]
    public void OBloqueioFechaOMarInteiroDaZona()
    {
        var w = Build();
        NavalMissionSystem.Assign(w, 2, 2, "bloqueio", 5f);       // bloqueio à costa 2 (do país 1)
        Assert.True(NavalMissionSystem.Blockaded(w, 2));
        // a região 3 é do país 2, dono do bloqueio: o mar dele não se fecha a ele próprio
        Assert.False(NavalMissionSystem.Blockaded(w, 3));
        // e a escolta do dono, posta na outra costa do mesmo mar, desfaz o bloqueio
        NavalMissionSystem.Assign(w, 1, 2, "escolta", 9f);
        Assert.False(NavalMissionSystem.Blockaded(w, 2));
    }

    [Fact]
    public void OQuadroDasZonasDizDeQuemEOCeu()
    {
        var w = Build();
        AirMissionSystem.Assign(w, 1, 1, "superioridade", 6f);
        AirMissionSystem.Assign(w, 2, 2, "superioridade", 6f);
        var board = Zones.AirBoard(w, 1);
        var line = Assert.Single(board);
        Assert.Equal("oeste", line.Id);
        Assert.Equal(6f, line.Mine, 0.01f);
        Assert.Equal(6f, line.Theirs, 0.01f);
        Assert.Equal("disputado", line.Who);
        Assert.Equal(2, line.Regions);
        Assert.Contains("zonas de céu", Zones.Short(w, 1));
    }

    [Fact]
    public void AZonaFechadaNaoAlcancaAOutra()
    {
        var w = Build();
        w.Rules["air_zone_share"] = 0f;    // regra a zero: volta a valer só a província de baixo
        AirMissionSystem.Assign(w, 1, 1, "superioridade", 4f);
        Assert.Equal(0f, AirMissionSystem.Superiority(w, 2, 1), 0.001f);
        Assert.True(AirMissionSystem.Superiority(w, 1, 1) > 0f);
    }
}
