using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O armazém de material: as fábricas enchem-no, a guerra esvazia-o e é dele que sai a reposição
/// das divisões gastas. Aqui prova-se o ciclo todo — quem tira, quem põe, o que estrangula, e o que a falta
/// de material faz à tropa (tecto na resistência, chão na força).</summary>
public class EquipmentTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return w;
    }

    /// <summary>Enche o armazém do país com `sets` conjuntos de cada tipo que o modelo pede.</summary>
    private static void Fill(World w, int country, int template, float sets)
    {
        var c = w.Countries[country];
        foreach (var (type, qty) in w.KitNeed(template)) c.Stock[type] = qty * sets;
    }

    [Fact]
    public void ReporMaterialTiraDoArmazem()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Kit = 0.5f;
        Fill(w, 1, TestWorld.Inf, 1f);
        float step = w.Rule("kit_refill_day", 0.06f);

        new EquipmentSystem().Tick(w);

        Assert.Equal(0.5f + step, d.Kit, 4);
        foreach (var (type, qty) in w.KitNeed(TestWorld.Inf))
            Assert.Equal(qty * (1f - step), w.Countries[1].Stocked(type), 3);
    }











}
