using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

public class CombatTests
{
    private static (World w, CombatSystem combat) Build(int seed)
    {
        var db = new MsSqliteDatabase();
        db.ExecuteScript(File.ReadAllText("data/schema.sql"));
        db.ExecuteScript(File.ReadAllText("data/seed_units.sql"));
        db.ExecuteScript(@"
            INSERT INTO template VALUES (1,1,'Inf'),(2,1,'Blind'),(3,1,'Inf+AT');
            INSERT INTO template_unit VALUES (1,1,6),(1,4,2), (2,3,4),(2,2,3),(2,4,1), (3,1,5),(3,4,1),(3,6,2);");
        var units = new SqlUnitRepository(db);
        var w = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units), new ModifierEngine(units.GetModifiers()), seed);
        w.Countries[1] = new Country { Id = 1, Tag = "A" }; w.Countries[2] = new Country { Id = 2, Tag = "B" };
        return (w, new CombatSystem());
    }

    private static Division Div(int id, int country, int tmpl) => new() { Id = id, CountryId = country, TemplateId = tmpl };

    private static float AttackerWinRate(int attTmpl, int defTmpl, string terrain, int n = 100)
    {
        int wins = 0;
        for (int seed = 0; seed < n; seed++)
        {
            var (w, c) = Build(seed);
            var att = new List<Division> { Div(1, 1, attTmpl), Div(2, 1, attTmpl) };
            var def = new List<Division> { Div(3, 2, defTmpl), Div(4, 2, defTmpl) };
            var ctx = new ModContext().With("terrain", terrain);
            for (int day = 0; day < 60; day++)
            {
                c.ResolveTick(w, att.Where(d => d.CanFight).ToList(), def.Where(d => d.CanFight).ToList(), ctx, ctx);
                if (!def.Any(d => d.CanFight)) { wins++; break; }
                if (!att.Any(d => d.CanFight)) break;
            }
        }
        return wins / (float)n;
    }

    [Fact] public void EqualInfantry_DefenderUsuallyWins() => Assert.InRange(AttackerWinRate(1, 1, "plain"), 0f, 0.3f);
    [Fact] public void Armor_BeatsInfantry_OnPlain() => Assert.InRange(AttackerWinRate(2, 1, "plain"), 0.9f, 1f);
    [Fact] public void Armor_LosesToAntiTank_InMountain() => Assert.InRange(AttackerWinRate(2, 3, "mountain"), 0f, 0.1f);

    [Fact]
    public void ModifierEngine_AppliesTagCondition()
    {
        var eng = new ModifierEngine(new[] {
            new Modifier(1, "terrain", "terrain", "urban", "str_attacker", "armored", ModOp.Mul, 0.6f) });
        var armored = new StatBlock(); armored.Tags.Add("armored");
        var inf = new StatBlock();
        var ctx = new ModContext().With("terrain", "urban");
        Assert.Equal(0.6f, eng.Evaluate("str_attacker", armored, ctx).mul);
        Assert.Equal(1f, eng.Evaluate("str_attacker", inf, ctx).mul);
    }
}
