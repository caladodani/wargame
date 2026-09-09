using System.Diagnostics;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>SupplySystem no mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2). Expectativas derivadas de w.Rule.</summary>
public class SupplyTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new SupplySystem());
        return w;
    }

    private static float Pocket(World w) => w.Rule("supply_pocket", 0.5f);
    private static float Stack(World w) => w.Rule("supply_stack", 6f);

    [Fact]
    public void AtHome_FullSupply()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Supply = 0.2f;                                   // o sistema tem de escrever, não só manter o default
        TestWorld.Days(w, 1);
        Assert.Equal(1f, d.Supply);
    }


    [Fact]
    public void OccupiedRegion_CutOff_Pocket()
    {
        var w = Build();
        w.Regions[5].ControllerId = 1;                     // controla 5 mas não 4: bolsa
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 5);
        TestWorld.Days(w, 1);
        Assert.Equal(Pocket(w), d.Supply);
    }




    [Fact]
    public void Stacking_AboveLimit_DividesSupply()
    {
        var w = Build();
        int n = (int)Stack(w) + 2;                         // 8 com supply_stack=6 → 6/8 = 0,75
        for (int i = 1; i <= n; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 1);
        float expected = Stack(w) / n;
        Assert.Equal(0.75f, expected);
        foreach (var d in w.Divisions.Values) Assert.Equal(expected, d.Supply, 5);
    }





    /// <summary>Escala do mapa real: grelha 60×50 (3000 regiões), 250 países em blocos contíguos de 12,
    /// uma divisão em cada região. Um tick tem de ficar em poucos ms (tecto folgado por causa de builds paralelos).</summary>
    [Fact]
    public void FullScaleMap_TickIsFast()
    {
        var (w, _) = TestWorld.Build();
        const int cols = 60, rows = 50, perCountry = 12;
        for (int c = 1; c <= cols * rows / perCountry; c++)
            w.Countries[c] = new Country { Id = c, Tag = "C" + c, Name = "C" + c };
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                int id = y * cols + x + 1, owner = (id - 1) / perCountry + 1;
                var r = new Region { Id = id, Name = "R" + id, OwnerId = owner, ControllerId = owner };
                if (x > 0) r.Neighbours.Add(id - 1);
                if (x < cols - 1) r.Neighbours.Add(id + 1);
                if (y > 0) r.Neighbours.Add(id - cols);
                if (y < rows - 1) r.Neighbours.Add(id + cols);
                w.Regions[id] = r;
            }
        // metade das regiões ocupadas pelo país vizinho de baixo na grelha: mistura de ligadas e bolsas
        foreach (var r in w.Regions.Values) if (r.Id % 2 == 0) r.ControllerId = (r.OwnerId % w.Countries.Count) + 1;
        foreach (var r in w.Regions.Values)
            TestWorld.AddDivision(w, r.Id, r.ControllerId, TestWorld.Inf, r.Id);
        w.Register(new SupplySystem());

        TestWorld.Days(w, 1);                                // aquecimento
        var sw = Stopwatch.StartNew();
        TestWorld.Days(w, 10);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 1000, $"10 ticks demoraram {sw.ElapsedMilliseconds} ms");
        Assert.All(w.Divisions.Values, d => Assert.InRange(d.Supply, Pocket(w), 1f));
        Assert.Contains(w.Divisions.Values, d => d.Supply == 1f);
        Assert.Contains(w.Divisions.Values, d => d.Supply == Pocket(w));
    }
}
