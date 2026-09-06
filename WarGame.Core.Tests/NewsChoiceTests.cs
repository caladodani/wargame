using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Eventos com escolhas (news_event_option): IA fica com a primeira, jogador escolhe por comando,
/// e o efeito só existe depois da escolha.</summary>
public class NewsChoiceTests
{
    private static World Setup(bool playerTarget)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].IsPlayer = playerTarget;
        w.NewsEvents["evt"] = new NewsEvent("evt", 3, 1, "Decisão", "Corpo");
        w.NewsOptions["evt"] = new List<NewsOption> { new("opt_a", "evt", "A", 0), new("opt_b", "evt", "B", 1) };
        w.NewsOptionEffects["opt_a"] = new List<(string, float)> { ("industry", 1.5f) };
        w.NewsOptionEffects["opt_b"] = new List<(string, float)> { ("conscription", 2f) };
        w.Register(new NewsSystem());
        return w;
    }

    [Fact]
    public void Ai_TakesFirstOption_OnFireDay()
    {
        var w = Setup(playerTarget: false);
        float before = w.Countries[1].Stat("industry");
        TestWorld.Days(w, 4);
        Assert.Equal("opt_a", w.NewsChoices["evt"]);
        Assert.Equal(before * 1.5f, w.Countries[1].Stat("industry"), 0.01f);
    }

    [Fact]
    public void Player_ChoosesByCommand_EffectOnlyAfter()
    {
        var w = Setup(playerTarget: true);
        var asked = new List<NewsChoiceRequired>();
        w.Events.Subscribe<NewsChoiceRequired>(asked.Add);
        float before = w.Countries[1].Stat("conscription");
        TestWorld.Days(w, 4);
        Assert.Single(asked);
        Assert.False(w.NewsChoices.ContainsKey("evt"));
        Assert.Equal(before, w.Countries[1].Stat("conscription"), 0.01f);   // sem efeito antes da escolha

        var cmd = new ChooseNewsOptionCommand(1, "evt", "opt_b");
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(before * 2f, w.Countries[1].Stat("conscription"), 0.01f);
        Assert.NotNull(new ChooseNewsOptionCommand(1, "evt", "opt_a").Validate(w));   // já escolhido
    }

    [Fact]
    public void Validate_RejectsWrongCountryAndFuture()
    {
        var w = Setup(playerTarget: true);
        Assert.NotNull(new ChooseNewsOptionCommand(2, "evt", "opt_a").Validate(w));   // não é dele
        Assert.NotNull(new ChooseNewsOptionCommand(1, "evt", "opt_a").Validate(w));   // ainda no futuro (dia 3)
        TestWorld.Days(w, 4);
        Assert.Null(new ChooseNewsOptionCommand(1, "evt", "opt_a").Validate(w));
    }
}
