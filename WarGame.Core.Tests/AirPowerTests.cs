using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Poder aéreo abstrato: compra de esquadrões, efeito no combate, IA compra quando atrás.</summary>
public class AirPowerTests
{
    [Fact]
    public void BuyAirWing_CostsMoney_AddsSquadron()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1]; c.Money = 100f;
        var cmd = new BuyAirWingCommand(1);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(1f, c.AirPower);
        Assert.Equal(100f - w.Rule("air_wing_cost", 60f), c.Money, 0.01f);
        c.Money = 0f;
        Assert.NotNull(new BuyAirWingCommand(1).Validate(w));
    }

    [Fact]
    public void AirSuperiority_TiltsBattle()
    {
        float DefOrgAfter(float attAir)
        {
            var (w, _) = TestWorld.Build();
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            w.Countries[1].AirPower = attAir;
            w.Register(new CombatSystem());
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
            var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
            b.Attackers.Add(att.Id); b.Defenders.Add(def.Id);
            w.ActiveBattles.Add(b);
            TestWorld.Days(w, 3);
            return def.Org;
        }
        Assert.True(DefOrgAfter(5f) < DefOrgAfter(0f));
    }

    [Fact]
    public void Ai_BuysAir_WhenOutgunned()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        w.Countries[1].Money = 1000f;
        w.Countries[2].AirPower = 3f;
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        w.Register(new AiSystem());
        TestWorld.Days(w, 1);
        Assert.True(w.Countries[1].AirPower >= 1f);
    }
}
