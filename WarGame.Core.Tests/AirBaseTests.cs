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






}
