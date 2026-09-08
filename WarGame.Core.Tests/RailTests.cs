using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A rede: carris e depósitos. A logística do jogo era uma travessia por regiões e mais nada — a
/// via férrea não existia e um exército a 1000 km de casa tinha exactamente o mesmo alcance que um em casa,
/// sem forma de o melhorar. No HoI4 a rede lê-se no mapa e trabalha-se: repara-se a linha, sobe-se-lhe o
/// nível e leva-se um depósito atrás da ofensiva. Estes testes fixam as duas metades — a linha barata e o
/// depósito que é rede nova onde está — e o que as mata (a terra que muda de mãos, o cerco).</summary>
public class RailTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new SupplySystem());
        return w;
    }

    [Fact]
    public void CarrilBarateiaOSaltoDaRede()
    {
        var w = Build();
        var r = w.Regions[4];
        float sem = SupplySystem.StepCost(w, r);
        r.Rail = 2;
        float com = SupplySystem.StepCost(w, r);
        Assert.True(com < sem);
        // a conta é a da regra, não um número à mão: infraestrutura + rail_step por nível
        Assert.Equal(1f / (r.Infrastructure + 2f * w.Rule("rail_step", 0.5f)), com, 4);
    }

    [Fact]
    public void CarrilEncurtaADistanciaDaTropaEmTerraTomada()
    {
        var w = Build();
        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].ControllerId = 1;   // ocupámos o país 2 todo
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 6);
        TestWorld.Days(w, 1);
        float aPe = d.SupplyDepth;

        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].Rail = (int)w.Rule("rail_max", 4f);
        TestWorld.Days(w, 1);
        Assert.True(d.SupplyDepth < aPe);
    }

    [Fact]
    public void DepositoLigadoEhRedeNovaOndeEsta()
    {
        var w = Build();
        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].ControllerId = 1;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 6);
        TestWorld.Days(w, 1);
        float sem = d.SupplyDepth;
        Assert.True(sem > 0f);

        var hub = w.BuildingDefs.Values.First(b => b.IsHub);
        w.Regions[6].Buildings[hub.Id] = 1;
        TestWorld.Days(w, 1);
        Assert.Equal(0f, d.SupplyDepth, 3);                 // o depósito está ali: a rede recomeça no sítio
    }

    [Fact]
    public void DepositoCercadoNaoConta()
    {
        var w = Build();
        foreach (int id in new[] { 4, 5, 6 }) w.Regions[id].ControllerId = 1;
        var hub = w.BuildingDefs.Values.First(b => b.IsHub);
        w.Regions[6].Buildings[hub.Id] = 1;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 6);
        TestWorld.Days(w, 1);
        Assert.False(d.Cut);

        w.Regions[5].ControllerId = 2;                      // cortaram a estrada de casa ao depósito
        TestWorld.Days(w, 1);
        Assert.True(d.Cut);
        Assert.Equal(w.Rule("supply_pocket", 0.5f), d.Supply, 3);
    }

    [Fact]
    public void DepositoLevanTaSeEmTerraTomadaMasOsOutrosNao()
    {
        var w = Build();
        var c = w.Countries[1];
        c.Money = 10_000f;
        w.Regions[4].ControllerId = 1;                      // tomada, dono continua a ser o país 2
        var hub = w.BuildingDefs.Values.First(b => b.IsHub);
        var outro = w.BuildingDefs.Values.First(b => !b.IsHub && !b.Coastal);

        Assert.Null(new BuildBuildingCommand(1, 4, hub.Id).Validate(w));
        Assert.Equal("a região não é tua", new BuildBuildingCommand(1, 4, outro.Id).Validate(w));
    }

    [Fact]
    public void ObraDeDepositoMorreSeATerraMudaDeMaos()
    {
        var w = Build();
        w.Register(new ConstructionSystem());
        var c = w.Countries[1]; c.Money = 10_000f;
        w.Regions[4].ControllerId = 1;
        var hub = w.BuildingDefs.Values.First(b => b.IsHub);
        Assert.Null(new CommandDispatcher().Dispatch(w, new BuildBuildingCommand(1, 4, hub.Id)));
        Assert.Equal(1, w.Regions[4].ProjectOwner);

        TestWorld.Days(w, 1);
        Assert.Equal(hub.Id, w.Regions[4].Project);         // ainda de pé enquanto a terra for nossa
        w.Regions[4].ControllerId = 2;                      // reconquistaram-na
        TestWorld.Days(w, 1);
        Assert.Null(w.Regions[4].Project);
        Assert.Equal(0, w.Regions[4].ProjectOwner);
    }

    [Fact]
    public void ObraDeCarrilSobeUmNivelEAvisaOMapa()
    {
        var w = Build();
        w.Register(new ConstructionSystem());
        var c = w.Countries[1]; c.Money = 10_000f;
        w.Regions[2].Rail = 0;
        int avisos = 0, nivel = -1;
        w.Events.Subscribe<WarGame.Core.Events.RailBuilt>(e => { avisos++; nivel = e.Level; });

        Assert.Null(new CommandDispatcher().Dispatch(w, new BuildRailCommand(1, 2)));
        Assert.True(w.Regions[2].RailBuilding);
        Assert.Equal(10_000f - w.Rule("rail_cost", 25f), c.Money, 2);

        TestWorld.Days(w, (int)w.Rule("rail_days", 20f));
        Assert.False(w.Regions[2].RailBuilding);
        Assert.Equal(1, w.Regions[2].Rail);
        Assert.Equal(1, avisos);
        Assert.Equal(1, nivel);
    }

    [Fact]
    public void CarrilRecusaSeNaoEhNossaOuJaEstaNoMaximo()
    {
        var w = Build();
        var c = w.Countries[1]; c.Money = 10_000f;
        Assert.Equal("a região não é tua", new BuildRailCommand(1, 5).Validate(w));

        w.Regions[2].Rail = (int)w.Rule("rail_max", 4f);
        Assert.Equal("via férrea no máximo", new BuildRailCommand(1, 2).Validate(w));

        w.Regions[2].Rail = 0;
        w.Regions[2].RailBuilding = true;
        Assert.Equal("já há carril a ser assente", new BuildRailCommand(1, 2).Validate(w));

        w.Regions[2].RailBuilding = false;
        c.Money = 0f;
        Assert.Contains("faltam pontos", new BuildRailCommand(1, 2).Validate(w));
    }

    [Fact]
    public void CarrilOcupaUmaFabricaCivilComoAsOutrasObras()
    {
        var w = Build();
        var c = w.Countries[1]; c.Money = 10_000f;
        int livres = Industry.Of(w, 1).FreeCivil;
        Assert.True(livres > 0);
        w.Regions[1].RailBuilding = true;
        Assert.Equal(livres - 1, Industry.Of(w, 1).FreeCivil);
    }

    [Fact]
    public void MenuDeConstruirTemOCarrilComOTectoDaRegra()
    {
        var w = Build();
        var offer = Assert.Single(BuildPlan.Offers(w), o => o.Id == BuildPlan.Rail);
        Assert.Equal(w.Rule("rail_cost", 25f), offer.Cost);
        Assert.Equal(w.Rule("rail_days", 20f), offer.Days);
        Assert.Contains($"tecto {(int)w.Rule("rail_max", 4f)}", offer.Gives);
        // e a razão do "não podes" é a do próprio comando, palavra por palavra
        Assert.Equal(new BuildRailCommand(1, 5).Validate(w), BuildPlan.Blocked(w, 1, 5, BuildPlan.Rail));
    }

    [Fact]
    public void CarrisNascemDaPopulacaoDaRegiao()
    {
        var (w, _) = TestWorld.Build();
        int bas = (int)w.Rule("rail_pop_base", 500_000f);
        // a população é de leitura só depois de nascer a região: fazem-se as três à mão, uma por degrau
        w.Regions[1] = new Region { Id = 1, Name = "Aldeia", Population = 1_000 };
        w.Regions[2] = new Region { Id = 2, Name = "Vila", Population = bas * 2 };
        w.Regions[3] = new Region { Id = 3, Name = "Metrópole", Population = 1_000_000_000 };
        w.SeedRails();
        Assert.Equal(0, w.Regions[1].Rail);
        Assert.Equal(1, w.Regions[2].Rail);
        Assert.Equal((int)w.Rule("rail_max", 4f), w.Regions[3].Rail);
        Assert.Equal(w.Regions[3].Rail, w.Regions[3].BaseRail);                // a base é a que o save compara
    }

    [Fact]
    public void ACarrilEAObraDoDepositoSobrevivemAoSave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[2].Rail = 3;
        w.Regions[3].RailBuilding = true; w.Regions[3].RailProgress = 5f;
        w.Regions[4].ControllerId = 1;
        var hub = w.BuildingDefs.Values.First(b => b.IsHub);
        w.Regions[4].Project = hub.Id; w.Regions[4].ProjectProgress = 2f; w.Regions[4].ProjectOwner = 1;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(3, w2.Regions[2].Rail);
        Assert.True(w2.Regions[3].RailBuilding);
        Assert.Equal(5f, w2.Regions[3].RailProgress, 3);
        Assert.Equal(hub.Id, w2.Regions[4].Project);
        Assert.Equal(1, w2.Regions[4].ProjectOwner);
    }

    [Fact]
    public void ARedeDoPaisContaAsRegioesLigadasEOsDepositos()
    {
        var w = Build();
        foreach (int id in new[] { 4, 5 }) w.Regions[id].ControllerId = 1;
        var hub = w.BuildingDefs.Values.First(b => b.IsHub);
        w.Regions[5].Buildings[hub.Id] = 2;

        var net = SupplySystem.Network(w, 1);
        Assert.Equal(5, net.Linked);                        // 1,2,3 de casa + 4 e 5 tomadas
        Assert.Equal(1, net.Hubs);
        Assert.Equal(2f * hub.HubRange, net.Credit, 3);
    }
}
