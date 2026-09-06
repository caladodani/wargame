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

    [Fact]
    public void Capture_TransfersEffect()
    {
        var w = Setup();
        TestWorld.Days(w, 1);
        float p1 = w.Countries[1].Stat("production_speed");
        w.Regions[4].ControllerId = 1;   // país 1 toma a região com 2 de aço
        TestWorld.Days(w, 1);
        Assert.Equal(p1 / 1.06f * 1.10f, w.Countries[1].Stat("production_speed"), 0.001f);   // 5 unidades
        Assert.Equal(1f, w.Countries[2].ResourceMult.GetValueOrDefault("production_speed", 1f), 0.001f);
    }

    [Fact]
    public void Cap_Limits()
    {
        var w = Setup();
        w.Regions[2].Resources["aco"] = 20f;
        TestWorld.Days(w, 1);
        Assert.Equal(1.20f, w.Countries[1].ResourceMult["production_speed"], 0.001f);   // tecto 10 unidades
    }

    [Fact]
    public void RealDb_HasDeposits()
    {
        var w = FactionTests.BuildReal();
        Assert.Equal(3, w.ResourceDefs.Count);
        Assert.True(w.Regions.Values.Count(r => r.Resources.Count > 0) > 300);
    }
}
