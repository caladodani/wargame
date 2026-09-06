using WarGame.Core.Commands;
using WarGame.Core.Model;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Estado-maior: contratar paga e multiplica o stat, os lugares são limitados por
/// general_slots, dispensar devolve o stat ao normal e os comandantes sobrevivem a um save.</summary>
public class GeneralTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.GeneralDefs["gen_of"] = new GeneralDef("gen_of", "Ofensiva", "attack", 1.10f, 120f);
        w.GeneralDefs["gen_def"] = new GeneralDef("gen_def", "Defesa", "defense", 1.10f, 120f);
        w.GeneralDefs["gen_ind"] = new GeneralDef("gen_ind", "Indústria", "industry", 1.08f, 140f);
        w.Rules["general_slots"] = 2f;
        w.Countries[1].Money = 1000f;
        return w;
    }

    [Fact]
    public void Hire_PaysAndBuffsStat()
    {
        var w = Setup();
        float before = w.Countries[1].Stat("attack");
        var cmd = new HireGeneralCommand(1, "gen_of");
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);
        Assert.Equal(880f, w.Countries[1].Money, 0.01f);
        Assert.Equal(before * 1.10f, w.Countries[1].Stat("attack"), 0.001f);
        Assert.NotNull(new HireGeneralCommand(1, "gen_of").Validate(w));   // não repete
    }

    [Fact]
    public void Slots_LimitTheStaff()
    {
        var w = Setup();
        new HireGeneralCommand(1, "gen_of").Execute(w);
        new HireGeneralCommand(1, "gen_def").Execute(w);
        Assert.Equal("estado-maior completo", new HireGeneralCommand(1, "gen_ind").Validate(w));
        new DismissGeneralCommand(1, "gen_def").Execute(w);
        Assert.Null(new HireGeneralCommand(1, "gen_ind").Validate(w));
    }

    [Fact]
    public void Dismiss_DropsTheBuff_WithoutRefund()
    {
        var w = Setup();
        float before = w.Countries[1].Stat("attack");
        new HireGeneralCommand(1, "gen_of").Execute(w);
        new DismissGeneralCommand(1, "gen_of").Execute(w);
        Assert.Equal(before, w.Countries[1].Stat("attack"), 0.001f);
        Assert.Equal(880f, w.Countries[1].Money, 0.01f);
        Assert.NotNull(new DismissGeneralCommand(1, "gen_of").Validate(w));
    }

    [Fact]
    public void Validate_RejectsPoorAndUnknown()
    {
        var w = Setup();
        Assert.NotNull(new HireGeneralCommand(1, "nao_existe").Validate(w));
        w.Countries[1].Money = 10f;
        Assert.NotNull(new HireGeneralCommand(1, "gen_of").Validate(w));
    }

    [Fact]
    public void Ai_FillsStaff_AndPrefersCombatWhenAtWar()
    {
        var w = Setup();
        w.Rules["ai_general_reserve"] = 0f;
        w.Register(new WarGame.Core.Systems.AiSystem());
        w.StartWar(1, 2);
        TestWorld.Days(w, 1);                       // dia 0: a IA corre
        Assert.Single(w.Countries[1].Generals);
        Assert.Contains(w.GeneralDefs[w.Countries[1].Generals[0]].StatKey, new[] { "attack", "defense" });
        TestWorld.Days(w, 6);                       // rondas seguintes enchem o estado-maior
        Assert.Equal(2, w.Countries[1].Generals.Count);   // general_slots = 2
    }

    [Fact]
    public void ApplyGenerals_RebuildsMultipliersAfterLoad()
    {
        var w = Setup();
        w.Countries[1].Generals.Add("gen_of");   // como sai de um save
        float before = w.Countries[1].Stat("attack");
        World.ApplyGenerals(w, w.Countries[1]);
        Assert.Equal(before * 1.10f, w.Countries[1].Stat("attack"), 0.001f);
    }
}
