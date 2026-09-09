using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Empréstimo de material. Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2), três regiões de 10M
/// cada — o país 1 ganha 3 pontos por dia (points_per_million = 0.1). Emprestar 20% é mandar 0,6 por dia,
/// dos quais chega o que a regra lend_lease_waste deixar chegar. Os números vêm todos de w.Rule, para o
/// teste não repetir a tabela.</summary>
public class LendLeaseTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EconomySystem());       // é o rendimento dele que se reparte
        w.Register(new LendLeaseSystem());
        return w;
    }

    private static float Waste(World w) => w.Rule("lend_lease_waste", 0.2f);
    private static float Max(World w) => w.Rule("lend_lease_max_share", 0.35f);
    private static float Min(World w) => w.Rule("lend_lease_min_share", 0.05f);


    /// <summary>A fatia sai do benfeitor inteira e chega ao aliado descontadas as perdas de caminho.</summary>
    [Fact]
    public void OQueSaiEAFatiaOQueChegaEMenos()
    {
        var w = Build();
        w.Countries[1].Money = 100f; w.Countries[2].Money = 0f;
        Assert.Null(new LendLeaseCommand(1, 2, 0.2f).Validate(w));
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);

        float income1 = EconomySystem.Income(w, 1), income2 = EconomySystem.Income(w, 2);
        float sent = income1 * 0.2f, landed = sent * (1f - Waste(w));
        TestWorld.Days(w, 1);

        Assert.Equal(100f + income1 - sent, w.Countries[1].Money, 2);
        Assert.Equal(income2 + landed, w.Countries[2].Money, 2);
        Assert.Equal(landed, Assert.Single(w.LendLeases).SentTotal, 2);
    }












}
