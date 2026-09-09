using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Decisões nacionais: o preço em várias moedas, a porta (decision_req), os efeitos todos de uma
/// decisão (decision_effect) e as MISSÕES com prazo — cumpridas pagam, falhadas castigam, e ambas
/// atravessam o save. Mais a prova de que as decisões semeadas estão inteiras.</summary>
public class DecisionTests
{
    private static DecisionDef Def(string id, string cat = "industria", float cost = 30f, float money = 0f,
                                   float manpower = 0f, float stability = 0f, int days = 3, int cooldown = 5,
                                   int missionDays = 0, string goalKey = "", float goalValue = 0f,
                                   float reward = 40f, float rewardStab = 3f, float fail = 20f, float failStab = 4f) =>
        new(id, "Mobilização", cat, "nota", cost, money, manpower, stability, days, cooldown, missionDays,
            goalKey, goalValue, reward, rewardStab, fail, failStab, "fabrica", 1);

    private static (World w, MsSqliteDatabase db) Setup(DecisionDef def, params (string Key, float Mult)[] effects)
    {
        var (w, db) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.DecisionDefs.Clear(); w.DecisionEffects.Clear(); w.DecisionReqs.Clear();
        w.DecisionDefs[def.Id] = def;
        w.DecisionEffects[def.Id] = effects.Length == 0
            ? new List<(string, float)> { ("industry", 1.15f) }
            : effects.ToList();
        w.Register(new DecisionSystem());
        w.Countries[1].Political = 200f;
        w.Countries[1].Money = 1000f;
        w.Countries[1].Manpower = 5000f;
        w.Countries[1].Stability = 60f;
        return (w, db);
    }

    [Fact]
    public void Assinar_paga_todas_as_moedas_e_aplica_os_efeitos_todos()
    {
        var (w, _) = Setup(Def("mob", money: 250f, manpower: 400f, stability: 5f),
                           ("industry", 1.15f), ("production_speed", 1.2f));
        var c = w.Countries[1];
        float ind = c.Stat("industry"), prod = c.Stat("production_speed");

        var cmd = new ActivateDecisionCommand(1, "mob");
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);

        Assert.Equal(170f, c.Political, 0.01f);
        Assert.Equal(750f, c.Money, 0.01f);
        Assert.Equal(4600f, c.Manpower, 0.01f);
        Assert.Equal(55f, c.Stability, 0.01f);
        Assert.Equal(ind * 1.15f, c.Stat("industry"), 0.01f);
        Assert.Equal(prod * 1.2f, c.Stat("production_speed"), 0.01f);
        Assert.NotNull(new ActivateDecisionCommand(1, "mob").Validate(w));   // já a correr
    }








}
