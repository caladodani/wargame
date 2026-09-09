using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O chão do céu: campos de aviação, camas e alcance (AirBases). Até aqui a aviação não vivia em
/// sítio nenhum — bastava a província fazer fronteira com terra nossa para a força aérea inteira aparecer
/// no céu dela, sem campo, sem lotação e sem distância. O que estes testes guardam é a regra nova: uma asa
/// dorme num campo nosso, o campo tem lotação, e o céu tem de caber no raio dos aviões que lá dormem.
///
/// Os outros testes de aviação correm com a lotação aberta de par em par (TestWorld abre-a, porque um país
/// de três províncias com cem asas ficava metade em terra a meio de um teste de baixas); estes chamam
/// TestWorld.Fields para a pôr como está na tabela.</summary>
public class AirBaseTests
{
    /// <summary>Dois países em guerra na linha do costume, com a lotação a sério. O lonStep é a geografia:
    /// dois graus dão ~222 km entre vizinhas, e a linha toda cabe no raio do caça ligeiro com que estes
    /// países acabam a voar (Air.Classify reparte o pool de partida por caça e ataque no primeiro dia).</summary>
    private static World Build(float lonStep = 2f, float wings = 20f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, lonStep: lonStep);
        TestWorld.Fields(w);
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values) { c.AirPower = wings; c.Money = 5000f; c.IsPlayer = true; }
        w.Register(new AirMissionSystem());
        return w;
    }

    [Fact]
    public void OCampoDeAviacaoTrazCamasEDepositos()
    {
        var w = Build();
        var d = w.BuildingDefs["aerodromo"];
        Assert.True(d.IsAirfield);
        Assert.True(d.AirSlots > 0f && d.AirRange > 0f);
        Assert.False(w.BuildingDefs["fabrica"].IsAirfield);                 // uma fábrica não assenta aviões

        var r = w.Regions[3];
        Assert.Equal(0, AirBases.Level(w, r));
        Assert.Equal(w.Rule("air_base_free"), AirBases.Slots(w, r), 3);     // pista improvisada: a de sempre
        Assert.Equal(0f, AirBases.Extra(w, r), 3);

        r.Buildings["aerodromo"] = 2;
        Assert.Equal(2, AirBases.Level(w, r));
        Assert.Equal(w.Rule("air_base_free") + 2f * d.AirSlots, AirBases.Slots(w, r), 3);
        Assert.Equal(2f * d.AirRange, AirBases.Extra(w, r), 3);
    }

    /// <summary>O raio é o do avião que chega menos longe: uma formação não se parte a meio do caminho.</summary>
    [Fact]
    public void ORaioEODoAviaoQueChegaMenosLonge()
    {
        var w = Build();
        float caca = w.PlaneClasses["caca_leve"].RangeKm, longe = w.PlaneClasses["estrategico"].RangeKm;
        Assert.True(caca > 0f && longe > caca);

        Assert.Equal(w.Rule("air_range_default"), AirBases.Range(w, new Dictionary<string, float>()), 3);
        Assert.Equal(caca, AirBases.Range(w, new Dictionary<string, float> { ["caca_leve"] = 2f, ["estrategico"] = 5f }), 3);
        // o modelo sem aviões nenhuns não conta: já não está lá ninguém a atrasar a formação
        Assert.Equal(longe, AirBases.Range(w, new Dictionary<string, float> { ["estrategico"] = 5f, ["caca_leve"] = 0f }), 3);
        // asa sem modelo (save antigo, mundo de teste) voa com o alcance de omissão
        Assert.Equal(w.Rule("air_range_default"), AirBases.Range(w, new Dictionary<string, float> { [""] = 3f }), 3);
    }

    /// <summary>A distância manda: o caça ligeiro não chega ao céu do vizinho a mil quilómetros, e é a pista
    /// comprida do campo de aviação que lho dá. É a obra a fazer antes da guerra.</summary>
    [Fact]
    public void APistaCompridaEQueLevaOCacaAoCeuDoVizinho()
    {
        var w = Build(lonStep: 9f);                                          // ~1 000 km entre vizinhas
        var c = w.Countries[1];
        c.Planes.Clear(); c.Planes["caca_leve"] = 20f;                       // 800 km de raio
        Assert.Contains("fora do alcance", AirMissionSystem.Block(w, 1, 4, "superioridade", 2f)!);
        Assert.False(AirBases.Covers(w, 1, 4, w.PlaneClasses["caca_leve"].RangeKm));

        w.Regions[3].Buildings["aerodromo"] = 1;                             // mais depósitos na pista
        Assert.Null(AirMissionSystem.Block(w, 1, 4, "superioridade", 2f));
        Assert.True(AirBases.Covers(w, 1, 4, w.PlaneClasses["caca_leve"].RangeKm));
    }

    /// <summary>Camas contadas por campo e do mais perto para o mais longe: a conta não depende de sementes
    /// nem da ordem por que as missões entraram na lista.</summary>
    [Fact]
    public void AsCamasSaoDoCampoMaisPertoParaOMaisLonge()
    {
        var w = Build();
        float free = w.Rule("air_base_free");
        AirMissionSystem.Assign(w, 1, 4, "superioridade", free + 1f);        // não cabe só na região 3

        var mine = Assert.Single(AirBases.Beds(w, 1)).Value;
        Assert.Equal(new[] { 3, 2 }, mine.Select(b => b.RegionId).ToArray());
        Assert.Equal(free, mine[0].Wings, 3);                                // a 3 é a mais perto do céu 4
        Assert.Equal(1f, mine[1].Wings, 3);
        Assert.Equal(3, AirBases.Home(w, w.AirMissions[0]));                 // dorme mais gente na 3

        // e as camas que sobram para outro céu já contam as que estas asas ocuparam
        Assert.Equal(3f * free - (free + 1f), AirBases.Room(w, 1, 4, w.Rule("air_range_default")), 3);
    }

    /// <summary>O que não tem cama fica em terra: volta ao pool no mesmo dia, sem perder um avião.</summary>
    [Fact]
    public void OQueNaoTemCamaFicaEmTerra()
    {
        var w = Build();
        float camas = 3f * w.Rule("air_base_free");                          // três províncias, todas ao alcance
        AirMissionSystem.Assign(w, 1, 4, "superioridade", camas + 5f);
        float grounded = 0f;
        w.Events.Subscribe<AirWingsGrounded>(e => grounded += e.Wings);

        w.Tick();
        Assert.Equal(5f, grounded, 3);
        Assert.Equal(camas, Assert.Single(w.AirMissions).Wings, 3);
        Assert.Equal(20f, w.Countries[1].AirPower, 3);                       // não caiu nenhum: só não levantou
        Assert.Equal(20f - camas, AirMissionSystem.Free(w, 1), 3);
    }

    /// <summary>Perder o chão é perder o céu: sem província nossa ao alcance daquele céu, a missão inteira
    /// vem para casa. É o que acontece quando o campo cai em mãos inimigas.</summary>
    [Fact]
    public void PerderOChaoTiraOCeu()
    {
        var w = Build();
        AirMissionSystem.Assign(w, 1, 4, "superioridade", 6f);
        w.Tick();
        Assert.Equal(6f, Assert.Single(w.AirMissions).Wings, 3);

        foreach (int id in new[] { 1, 2, 3 }) w.Regions[id].ControllerId = 2;
        float grounded = 0f;
        w.Events.Subscribe<AirWingsGrounded>(e => grounded += e.Wings);

        w.Tick();
        Assert.Empty(w.AirMissions);
        Assert.Equal(6f, grounded, 3);
        Assert.Equal(20f, AirMissionSystem.Free(w, 1), 3);                   // todas em casa, nenhuma abatida
    }

    /// <summary>A IA não manda para o ar o que não tem onde dormir: destaca até à cama e guarda o resto.</summary>
    [Fact]
    public void AIaNaoDestacaMaisDoQueACama()
    {
        var w = Build();
        w.Countries[2].IsPlayer = false;                                     // só a IA do país 2 despacha
        float camas = 3f * w.Rule("air_base_free");                          // as três províncias dela

        w.Tick();
        var m = Assert.Single(w.AirMissions);
        Assert.Equal(2, m.CountryId);
        Assert.Equal(camas, m.Wings, 3);                                     // e não 20 - air_ai_reserve
        Assert.True(AirMissionSystem.Free(w, 2) > 0f);
    }

    /// <summary>A linha do hangar conta o chão: campos levantados, camas ocupadas e a asa que ficou em terra.</summary>
    [Fact]
    public void ALinhaDoHangarContaCamposECamas()
    {
        var w = Build();
        Assert.Contains("0 campos de aviação", AirBases.Short(w, 1));

        w.Regions[2].Buildings["aerodromo"] = 1;
        AirMissionSystem.Assign(w, 1, 4, "superioridade", 3f);
        string linha = AirBases.Short(w, 1);
        Assert.Contains("1 campo de aviação", linha);
        Assert.Contains("3/", linha);
        Assert.DoesNotContain("em terra", linha);

        AirMissionSystem.Assign(w, 1, 6, "superioridade", 17f);              // mais do que a terra aguenta
        Assert.Contains("em terra por falta de campo", AirBases.Short(w, 1));
    }
}
