using WarGame.Core.Model;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Dificuldade data-driven: cada nível reescreve as regras da tabela difficulty_effect, trocar
/// de nível desfaz o anterior e voltar a null repõe as regras de origem.</summary>
public class DifficultyTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        w.Rules["build_min_days"] = 10f;
        w.DifficultyDefs["facil"] = new DifficultyDef("facil", "Fácil", 0,
            new Dictionary<string, float> { ["build_min_days"] = 5f, ["novo"] = 2f });
        w.DifficultyDefs["dificil"] = new DifficultyDef("dificil", "Difícil", 1,
            new Dictionary<string, float> { ["build_min_days"] = 15f });
        return w;
    }

    [Fact]
    public void Apply_RewritesTheRules()
    {
        var w = Setup();
        w.ApplyDifficulty("facil");
        Assert.Equal("facil", w.Difficulty);
        Assert.Equal(5f, w.Rule("build_min_days"));
        Assert.Equal(2f, w.Rule("novo"));
    }

    [Fact]
    public void SwitchingLevel_UndoesThePreviousOne()
    {
        var w = Setup();
        w.ApplyDifficulty("facil");
        w.ApplyDifficulty("dificil");
        Assert.Equal(15f, w.Rule("build_min_days"));
        Assert.Equal(7f, w.Rule("novo", 7f));   // regra que só o fácil criava desaparece
    }

    [Fact]
    public void BackToNull_RestoresTheOriginalRules()
    {
        var w = Setup();
        w.ApplyDifficulty("facil");
        w.ApplyDifficulty(null);
        Assert.Null(w.Difficulty);
        Assert.Equal(10f, w.Rule("build_min_days"));
        Assert.Equal(7f, w.Rule("novo", 7f));
    }

    [Fact]
    public void UnknownLevel_LeavesRulesAtBase()
    {
        var w = Setup();
        w.ApplyDifficulty("facil");
        w.ApplyDifficulty("nao_existe");
        Assert.Equal(10f, w.Rule("build_min_days"));
    }

    [Fact]
    public void SeededLevels_CoverOnlyRulesTheCodeReads()
    {
        var (w, _) = TestWorld.Build();
        Assert.Equal(4, w.DifficultyDefs.Count);   // muito_facil, facil, normal, dificil do seed
        var known = new[] { "build_min_days", "new_division_org", "points_per_million",
                            "ai_general_reserve", "manpower_per_million_daily" };
        foreach (var def in w.DifficultyDefs.Values)
        {
            Assert.NotEmpty(def.Effects);
            foreach (var key in def.Effects.Keys) Assert.Contains(key, known);
        }
    }
}
