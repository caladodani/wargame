using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A rede: carris e depósitos. A logística do jogo era uma travessia por regiões e mais nada — a
/// via férrea não existia e um exército a 1000 km de casa tinha exactamente o mesmo alcance que um em casa,
/// sem forma de o melhorar. No HoI4 a rede lê-se no mapa e trabalha-se: repara-se a linha, sobe-se-lhe o
/// nível e leva-se um depósito atrás da ofensiva. Estes testes fixam as duas metades — a linha barata e o
/// depósito que é rede nova onde está — e o que as mata (a terra que muda de mãos, o cerco).</summary>
public class RailTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new SupplySystem());
        return w;
    }


    [Fact]
    public void CarrilEncurtaADistanciaDaTropaEmTerraTomada()
    {
        var w = Build();
        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].ControllerId = 1;   // ocupámos o país 2 todo
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 6);
        TestWorld.Days(w, 1);
        float aPe = d.SupplyDepth;

        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].Rail = (int)w.Rule("rail_max", 4f);
        TestWorld.Days(w, 1);
        Assert.True(d.SupplyDepth < aPe);
    }











}
