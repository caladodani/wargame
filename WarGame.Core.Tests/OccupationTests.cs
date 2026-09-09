using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Políticas de ocupação: o que se faz ao povo da terra tomada. Cada política mexe ao mesmo tempo
/// na resistência, no rendimento e nos recrutas, e trocar de política tem de esperar.
///
/// Mapa dos testes: linha 1-2-3 (país 1) | 4-5-6 (país 2), com o país 1 a ocupar a região 4.</summary>
public class OccupationTests
{
    private const int Taken = 4;

    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[Taken].ControllerId = 1;                 // tomada, mas continua a ser deles
        foreach (var c in w.Countries.Values) c.IsPlayer = true;
        w.Register(new OccupationSystem());
        return w;
    }




    [Fact]
    public void TheStreetBoilsAtTheSpeedThePolicyDeserves()
    {
        static float After(string policy, int days)
        {
            var w = Build();
            w.Register(new ResistanceSystem());
            OccupationSystem.Set(w, 1, 2, policy);
            TestWorld.Days(w, days);
            return w.Regions[Taken].Resistance;
        }

        float calm = After("policia_local", 10), plain = After("supervisao_civil", 10), hard = After("trabalho_forcado", 10);
        Assert.True(calm < plain, $"a polícia local tinha de acalmar: {calm:0.000} contra {plain:0.000}");
        Assert.True(hard > plain, $"o trabalho forçado tinha de fazer ferver: {hard:0.000} contra {plain:0.000}");
    }






}
