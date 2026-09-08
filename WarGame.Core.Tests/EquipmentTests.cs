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

    [Fact]
    public void ArmazemVazioNaoRepoeNada()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Kit = 0.4f;

        new EquipmentSystem().Tick(w);

        Assert.Equal(0.4f, d.Kit, 4);
    }

    [Fact]
    public void OTipoMaisEscassoMandaEmTodosOsOutros()
    {
        var w = Build();
        var c = w.Countries[1];
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Kit = 0.5f;
        Fill(w, 1, TestWorld.Inf, 10f);
        var need = w.KitNeed(TestWorld.Inf);
        c.Stock[need[0].UnitTypeId] = 0f;                       // falta um tipo só

        Assert.Equal(0f, EquipmentSystem.Refill(w, c, d, 0.1f), 4);
        Assert.Equal(0.5f, d.Kit, 4);
        Assert.Equal(need[1].Qty * 10f, c.Stocked(need[1].UnitTypeId), 3);   // o resto não se toca

        // meio conjunto daquele tipo dá exactamente meia parte de reposição
        c.Stock[need[0].UnitTypeId] = need[0].Qty * 0.02f;
        Assert.Equal(0.02f, EquipmentSystem.Refill(w, c, d, 0.1f), 4);
    }

    [Fact]
    public void EmCombateRepoeMenosEDivisaoCercadaNaoRepoe()
    {
        var w = Build();
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        var fighting = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        var cut = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 2);
        fighting.Kit = 0.5f; cut.Kit = 0.5f; cut.Cut = true;
        Fill(w, 1, TestWorld.Inf, 20f);
        var b = new Battle { RegionId = 4, AttackerCountryId = 1 };
        b.Attackers.Add(fighting.Id);
        w.ActiveBattles.Add(b);

        new EquipmentSystem().Tick(w);

        Assert.Equal(0.5f + w.Rule("kit_refill_day", 0.06f) * w.Rule("kit_refill_battle", 0.35f), fighting.Kit, 4);
        Assert.Equal(0.5f, cut.Kit, 4);                          // o material está do outro lado do cerco
    }

    [Fact]
    public void CombateGastaMaterialComOsHomens()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);

        EquipmentSystem.Damage(w, d, 20f);

        Assert.Equal(1f - 20f * w.Rule("kit_loss_per_hp", 0.006f), d.Kit, 4);
        EquipmentSystem.Damage(w, d, 10_000f);
        Assert.Equal(0f, d.Kit);                                 // nunca desce abaixo de zero
    }

    [Fact]
    public void MaterialPoeTectoNaRecuperacao()
    {
        var w = Build();
        w.Register(new RecoverySystem());
        w.Countries[1].Money = 10_000f;                          // os reforços pagam-se
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1, hp: 50f);
        d.Kit = 0.6f;

        TestWorld.Days(w, 30);

        Assert.Equal(60f, d.Hp, 2);                              // 100 × Kit e nem mais um ponto
    }

    [Fact]
    public void MaterialPesaNaForcaMasTemChao()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        float floor = w.Rule("kit_power_floor", 0.45f);

        Assert.Equal(1f, EquipmentSystem.PowerMult(w, d), 4);
        d.Kit = 0f;
        Assert.Equal(floor, EquipmentSystem.PowerMult(w, d), 4);
        d.Kit = 0.5f;
        Assert.Equal(floor + (1f - floor) * 0.5f, EquipmentSystem.PowerMult(w, d), 4);
    }

    [Fact]
    public void LinhaDeMaterialEnchePrateleiraESeguraALinha()
    {
        var w = Build();
        w.Register(new ProductionSystem());
        var c = w.Countries[1];
        c.Money = 100_000f;
        var cmd = new BuildKitCommand(1, 1);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        var order = Assert.Single(c.Queue);
        Assert.True(order.IsKit);
        Assert.Equal(w.Units.GetUnitType(1).Cost, w.OrderCost(order), 3);

        TestWorld.Days(w, (int)w.Rule("build_min_days", 10f) + 1);

        Assert.True(c.Stocked(1) >= 1f);                         // saíram conjuntos para o armazém
        Assert.Empty(w.Divisions);                               // e não saiu divisão nenhuma
        Assert.Single(c.Queue);                                  // a torneira fica aberta
    }

    [Fact]
    public void DepositoUsaAsFabricasQueAFilaNaoUsa()
    {
        var w = Build();
        w.Register(new ProductionSystem());
        var c = w.Countries[1];
        c.Money = 100_000f;
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Kit = 0.5f;
        Assert.Empty(c.Queue);
        int type = Assert.IsType<int>(Warehouse.Neediest(w, c));

        w.Tick();

        Assert.True(c.Stocked(type) > 0f);
        Assert.True(c.Money < 100_000f);
        // e com o depósito desligado pela tabela, as fábricas paradas ficam mesmo paradas
        var w2 = Build();
        w2.Register(new ProductionSystem());
        w2.Rules["depot_idle_lines"] = 0f;
        w2.Countries[1].Money = 100_000f;
        TestWorld.AddDivision(w2, 1, 1, TestWorld.Inf, 1).Kit = 0.5f;
        w2.Tick();
        Assert.DoesNotContain(w2.Countries[1].Stock, kv => kv.Value > 0f);
    }

    [Fact]
    public void AFolhaDoArmazemContaOQueFaltaEOQueAguenta()
    {
        var w = Build();
        var c = w.Countries[1];
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Kit = 0.5f;
        var need = w.KitNeed(TestWorld.Inf);
        c.Stock[need[0].UnitTypeId] = need[0].Qty;               // um conjunto na prateleira

        Assert.Equal(need[0].Qty * 0.5f, Warehouse.Missing(w, c, need[0].UnitTypeId), 3);
        Assert.Equal(need[0].Qty, Warehouse.Fleet(w, c, need[0].UnitTypeId), 3);
        Assert.Equal(0.5f, Warehouse.Average(w, c), 3);
        Assert.Same(d, Warehouse.Worst(w, c));
        var rows = Warehouse.Rows(w, c);
        Assert.Equal(need.Count, rows.Count);
        Assert.All(rows, r => Assert.True(r.UnitTypeId > 0));
    }

    [Fact]
    public void OArmazemEOMaterialSobrevivemAoSave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.Kit = 0.42f;
        w.Countries[1].Stock[3] = 7.5f;
        new BuildKitCommand(1, 2).Execute(w);

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        Assert.Equal(0.42f, w2.Divisions[1].Kit, 3);
        Assert.Equal(7.5f, w2.Countries[1].Stocked(3), 3);
        var back = Assert.Single(w2.Countries[1].Queue);
        Assert.True(back.IsKit);
        Assert.Equal(2, back.UnitTypeId);
    }

    [Fact]
    public void ComandoDeMaterialRecusaTipoQueNaoExiste()
    {
        var w = Build();
        Assert.NotNull(new BuildKitCommand(1, 9999).Validate(w));
        Assert.NotNull(new BuildKitCommand(99, 1).Validate(w));
        w.Rules["production_queue_max"] = 0f;
        Assert.NotNull(new BuildKitCommand(1, 1).Validate(w));
    }
}
