using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Recursos estratégicos: controlar depósitos multiplica a stat do tipo (com tecto),
/// e capturar a região transfere o efeito para o novo controlador.</summary>
public class ResourceTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.ResourceDefs["aco"] = new ResourceDef("aco", "Aço", "production_speed", 0.02f, 10f);
        w.Regions[1].Resources["aco"] = 3f;
        w.Regions[4].Resources["aco"] = 2f;
        w.Register(new ResourceSystem());
        return w;
    }

    [Fact]
    public void ControlledDeposits_BoostStat()
    {
        var w = Setup();
        float before = w.Countries[1].Stat("production_speed");
        TestWorld.Days(w, 1);
        Assert.Equal(before * 1.06f, w.Countries[1].Stat("production_speed"), 0.001f);   // 3 unidades × 0.02
        Assert.Equal(3f, ResourceSystem.Controlled(w, 1, "aco"), 0.001f);
    }



}
