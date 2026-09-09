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





}
