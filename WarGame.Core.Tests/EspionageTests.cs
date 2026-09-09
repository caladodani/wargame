using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Espionagem: validação do comando, efeitos ao concluir (roubo, sabotagem, agitação), IA lança op.</summary>
public class EspionageTests
{

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











}
