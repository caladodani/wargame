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
    public void PetroleoControlado_Refina()
    {
        var w = Setup();
        TestWorld.Days(w, 1);
        var c = w.Countries[1];
        Assert.Equal(6f, c.FuelIn, 0.001f);       // 3 unidades × 2
        Assert.Equal(6f, c.Fuel, 0.001f);         // sem nada a beber, tudo vai para o depósito
        Assert.False(c.FuelOut);
    }

    [Fact]
    public void Deposito_Tem_Tecto()
    {
        var w = Setup();
        TestWorld.Days(w, 400);
        var c = w.Countries[1];
        float cap = w.Rule("fuel_cap_base") + 6f * w.Rule("fuel_cap_days");
        Assert.Equal(cap, c.FuelCap, 0.01f);
        Assert.Equal(cap, c.Fuel, 0.01f);         // enche e pára de encher
    }

    [Fact]
    public void SoAsMaquinasBebem()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 1);
        Assert.Equal(0f, w.Countries[1].FuelUse, 0.001f);   // infantaria anda a pé

        var w2 = Setup();
        TestWorld.AddDivision(w2, 1, 1, TestWorld.Armor, 1);
        TestWorld.Days(w2, 1);
        Assert.Equal(6.6f, w2.Countries[1].FuelUse, 0.01f); // 4 blindados × 1.2 + 3 mecanizadas × 0.6
    }

    [Fact]
    public void EmGuerra_GastaMais()
    {
        var w = Setup();
        TestWorld.AddDivision(w, 1, 1, TestWorld.Armor, 1);
        TestWorld.Days(w, 1);
        float peace = w.Countries[1].FuelUse;
        w.StartWar(1, 2);
        TestWorld.Days(w, 1);
        Assert.Equal(peace * w.Rule("fuel_war_mult"), w.Countries[1].FuelUse, 0.01f);
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

    [Fact]
    public void PerderOPoco_EncolheODeposito_MasAindaSobraParaUnsDias()
    {
        var w = Setup();
        TestWorld.Days(w, 60);                         // enche até ao tecto (60 de fundo + 6/dia × 30 dias)
        Assert.Equal(240f, w.Countries[1].Fuel, 0.01f);
        w.Regions[1].ControllerId = 2;                 // e agora perde-se o poço
        TestWorld.AddDivision(w, 1, 1, TestWorld.Armor, 2);
        TestWorld.Days(w, 1);
        var c = w.Countries[1];
        Assert.Equal(0f, c.FuelIn, 0.001f);
        // o depósito encolhe com a produção que o justificava: sem poço só resta o fundo, e o resto do que
        // lá estava não tem onde ficar. Perder os campos de petróleo é uma catástrofe, não um contratempo.
        Assert.Equal(w.Rule("fuel_cap_base"), c.FuelCap, 0.01f);
        Assert.Equal(w.Rule("fuel_cap_base"), c.Fuel, 0.01f);
        Assert.False(c.FuelOut);                       // ainda não está a seco: o fundo dá para uns dias
        Assert.True(FuelSystem.DaysLeft(c) > 5f);
    }

    [Fact]
    public void ComprarPetroleo_EnchePorQuemCompra()
    {
        var w = Setup(deposit: 0f);
        w.Regions[6].Resources["petroleo"] = 4f;       // o poço é do país 2
        w.TradeDeals.Add(new TradeDeal { BuyerId = 1, SellerId = 2, ResourceId = "petroleo", Units = 4f });
        TestWorld.Days(w, 1);
        Assert.Equal(8f, w.Countries[1].FuelIn, 0.001f);   // 4 unidades compradas × 2
        Assert.Equal(0f, w.Countries[2].FuelIn, 0.001f);   // e quem vendeu ficou sem elas
    }

    [Fact]
    public void Capitulado_NaoTemDeposito()
    {
        var w = Setup();
        TestWorld.Days(w, 5);
        w.Countries[1].Capitulated = true;
        TestWorld.Days(w, 1);
        Assert.Equal(0f, w.Countries[1].Fuel, 0.001f);
        Assert.False(w.Countries[1].FuelOut);
    }

    [Fact]
    public void Faixa_Avisa_Antes_De_Acabar()
    {
        var w = Setup(deposit: 6f);                    // 12/dia refinados: dá e sobra para uma divisão
        TestWorld.AddDivision(w, 1, 1, TestWorld.Armor, 1);
        TestWorld.Days(w, 20);                         // tempo de encher o depósito
        Assert.DoesNotContain(Alerts.For(w, 1), a => a.Id == "fuel");   // o dia paga o dia: nada a avisar

        w.Regions[1].ControllerId = 2;                 // perde-se o poço: a conta regressiva começa
        TestWorld.Days(w, 1);
        var warn = Assert.Single(Alerts.For(w, 1), a => a.Id == "fuel");
        Assert.Equal(AlertLevel.Warn, warn.Level);
        Assert.Contains("dias", warn.Text);

        w.Countries[1].Fuel = 2f;                      // menos de um dia
        var doom = Assert.Single(Alerts.For(w, 1), a => a.Id == "fuel");
        Assert.Equal(AlertLevel.Danger, doom.Level);
    }

    [Fact]
    public void RealDb_TemPetroleoARefinarEBlindadosASeco()
    {
        var w = FactionTests.BuildReal();
        Assert.True(w.ResourceDefs["petroleo"].FuelPerUnit > 0f);
        Assert.All(w.ResourceDefs.Values.Where(d => d.Id != "petroleo"), d => Assert.Equal(0f, d.FuelPerUnit));
        // e há pelo menos uma linha modifier a cobrar a seca a quem tem lagartas
        var st = w.Stats.Get(TestWorld.Armor);
        Assert.True(st["fuel_use"] > 0f);
    }
}
