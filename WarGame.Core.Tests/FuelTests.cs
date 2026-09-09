using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Combustível: o petróleo controlado refina-se, o depósito tem fundo e tecto, as máquinas bebem
/// e um país a seco bate pior com os blindados. O que aqui se defende é sobretudo que nada disto está em
/// código — o recurso é combustível porque a coluna o diz, e a divisão bebe porque a ficha dela o diz.</summary>
public class FuelTests
{
    private static World Setup(float fuelPerUnit = 2f, float deposit = 3f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.ResourceDefs["petroleo"] = new ResourceDef("petroleo", "Petróleo", "industry", 0.015f, 10f, fuelPerUnit);
        if (deposit > 0f) w.Regions[1].Resources["petroleo"] = deposit;
        w.Register(new ResourceSystem());
        w.Register(new FuelSystem());
        return w;
    }





    [Fact]
    public void SemPetroleo_Seca_E_O_Combate_Cobra()
    {
        var w = Setup(deposit: 0f);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Armor, 1);
        TestWorld.Days(w, 1);
        var c = w.Countries[1];
        Assert.Equal(0f, c.FuelIn, 0.001f);
        Assert.True(c.FuelOut);
        Assert.Equal(0f, c.Fuel, 0.001f);

        // e a seca dói onde tem de doer: a mesma divisão avaliada com e sem `fuel_out` no contexto
        var st = w.Stats.Get(TestWorld.Armor);
        var (wetF, wetM) = w.Modifiers.Evaluate("str", st, new ModContext());
        var (dryF, dryM) = w.Modifiers.Evaluate("str", st, new ModContext().With("fuel_out", "true"));
        Assert.True(dryM + dryF < wetM + wetF);
    }





}
