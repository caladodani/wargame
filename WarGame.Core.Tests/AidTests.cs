using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Apoio financeiro entre aliados: comando só dentro da facção; IA envia ao mais pobre em guerra.</summary>
public class AidTests
{
    [Fact]
    public void Transfer_OnlyBetweenFactionAllies()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 100f;
        Assert.NotNull(new TransferMoneyCommand(1, 2, 50f).Validate(w));   // sem facção comum
        new CreateFactionCommand(1, "Pacto").Execute(w);
        w.Factions[w.CustomFactionIds[0]].Members.Add(2);
        Assert.NotNull(new TransferMoneyCommand(1, 2, 200f).Validate(w));  // sem saldo
        var cmd = new TransferMoneyCommand(1, 2, 50f);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(50f, w.Countries[1].Money, 0.01f);
        Assert.Equal(50f, w.Countries[2].Money, 0.01f);
    }

    [Fact]
    public void Ai_SendsAidToPoorestAllyAtWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 1, Manpower = 1e9f };
        w.Register(new AiSystem());
        new CreateFactionCommand(1, "Pacto").Execute(w);
        var f = w.Factions[w.CustomFactionIds[0]];
        f.Members.Add(3);
        w.Countries[1].Money = 500f;   // rico, em paz
        w.Countries[3].Money = 10f;    // pobre, em guerra
        w.StartWar(3, 2);
        TestWorld.Days(w, 1);
        Assert.True(w.Countries[3].Money > 10f);
        Assert.True(w.Countries[1].Money < 500f);
    }
}
