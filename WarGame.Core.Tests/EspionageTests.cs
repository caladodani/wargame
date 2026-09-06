using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Espionagem: validação do comando, efeitos ao concluir (roubo, sabotagem, agitação), IA lança op.</summary>
public class EspionageTests
{
    [Fact]
    public void Validate_Money_Self_Duplicate()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1]; c.Money = 500f;

        Assert.NotNull(new StartSpyOpCommand(1, 1, "roubo_fundos").Validate(w));          // contra si próprio
        Assert.NotNull(new StartSpyOpCommand(1, 2, "op_que_nao_existe").Validate(w));     // op inválida
        c.Money = 0f;
        Assert.NotNull(new StartSpyOpCommand(1, 2, "roubo_fundos").Validate(w));          // sem dinheiro
        c.Money = 500f;
        var cmd = new StartSpyOpCommand(1, 2, "roubo_fundos");
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(500f - w.SpyOps["roubo_fundos"].Cost, c.Money, 0.01f);
        Assert.NotNull(new StartSpyOpCommand(1, 2, "agitacao").Validate(w));              // já corre uma contra o 2
    }

    [Fact]
    public void StealMoney_TransfersFraction_OnCompletion()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EspionageSystem());
        var op = w.SpyOps["roubo_fundos"];
        var c = w.Countries[1]; c.Money = op.Cost;
        var t = w.Countries[2]; t.Money = 200f;
        var cmd = new StartSpyOpCommand(1, 2, op.Id);
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);
        TestWorld.Days(w, op.Days - 1);
        Assert.Single(w.ActiveSpyOps);
        float targetBefore = t.Money;                       // a economia pode ter mexido — medir mesmo antes
        float actorBefore = c.Money;
        TestWorld.Days(w, 1);
        Assert.Empty(w.ActiveSpyOps);
        float loot = actorBefore == c.Money ? 0f : 1f;      // sanity: houve transferência
        Assert.True(t.Money < targetBefore, "alvo devia perder dinheiro");
        Assert.True(c.Money > actorBefore, "autor devia ganhar dinheiro");
    }

    [Fact]
    public void Sabotage_CutsQueueProgress()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var op = w.SpyOps["sabotagem_fabrica"];
        var t = w.Countries[2];
        t.Queue.Add(new ProductionOrder { TemplateId = 1, Progress = 100f });
        w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = 1, TargetCountryId = 2, OpId = op.Id, DaysLeft = 1f });
        w.Register(new EspionageSystem());
        TestWorld.Days(w, 1);
        Assert.Equal(100f * (1f - op.Magnitude), t.Queue[0].Progress, 0.01f);
    }

    [Fact]
    public void StabilityHit_LowersStability()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var op = w.SpyOps["agitacao"];
        var t = w.Countries[2]; t.Stability = 50f;
        w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = 1, TargetCountryId = 2, OpId = op.Id, DaysLeft = 1f });
        new EspionageSystem().Tick(w);   // só o sistema: o StabilitySystem não anda a puxar de volta
        Assert.Equal(50f - op.Magnitude, t.Stability, 0.01f);
        Assert.Empty(w.ActiveSpyOps);
    }

    [Fact]
    public void ResearchBoost_AdvancesActiveResearch()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var op = w.SpyOps["roubo_tech"];
        var c = w.Countries[1];
        c.ResearchTech = w.Techs.Keys.First(); c.ResearchProgress = 5f;
        w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = 1, TargetCountryId = 2, OpId = op.Id, DaysLeft = 1f });
        new EspionageSystem().Tick(w);
        Assert.Equal(5f + op.Magnitude, c.ResearchProgress, 0.01f);

        // sem investigação activa: não acumula nada
        var c2 = w.Countries[2]; c2.ResearchTech = null; float before = c2.ResearchProgress;
        w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = 2, TargetCountryId = 1, OpId = op.Id, DaysLeft = 1f });
        new EspionageSystem().Tick(w);
        Assert.Equal(before, c2.ResearchProgress, 0.01f);
    }

    [Fact]
    public void CounterIntel_Law_SlowsEnemyOps()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var t = w.Countries[2];
        t.Laws["security"] = "seg_policial"; w.ApplyTechs(t);
        var c = w.Countries[1]; c.Money = 500f;
        var op = w.SpyOps["roubo_fundos"];
        var cmd = new StartSpyOpCommand(1, 2, op.Id);
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);
        Assert.Equal(op.Days * 2f, w.ActiveSpyOps[0].DaysLeft, 0.01f);
    }

    [Fact]
    public void Intel_GrantsVisibility_UntilExpiry()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var op = w.SpyOps["rede_info"];
        w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = 1, TargetCountryId = 2, OpId = op.Id, DaysLeft = 1f });
        w.Register(new EspionageSystem());
        TestWorld.Days(w, 1);
        Assert.True(w.HasIntel(1, 2));
        Assert.False(w.HasIntel(2, 1));   // só numa direcção
        Assert.Equal((int)op.Magnitude, w.Intel[(1, 2)]);   // aplicado no dia 0, antes do Advance

        while (w.Clock.Day < w.Intel[(1, 2)]) w.Clock.Advance();
        Assert.True(w.HasIntel(1, 2));   // último dia ainda vê
        w.Clock.Advance();
        Assert.False(w.HasIntel(1, 2));
    }

    [Fact]
    public void Ai_StartsOp_WhenRichAndAtWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new AiSystem());
        var c = w.Countries[2]; c.Money = 1000f;
        w.StartWar(1, 2);
        TestWorld.Days(w, 1);   // IA corre no dia 0
        Assert.Contains(w.ActiveSpyOps, o => o.CountryId == 2 && o.TargetCountryId == 1);
    }
}
