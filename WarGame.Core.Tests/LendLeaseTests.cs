using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Empréstimo de material. Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2), três regiões de 10M
/// cada — o país 1 ganha 3 pontos por dia (points_per_million = 0.1). Emprestar 20% é mandar 0,6 por dia,
/// dos quais chega o que a regra lend_lease_waste deixar chegar. Os números vêm todos de w.Rule, para o
/// teste não repetir a tabela.</summary>
public class LendLeaseTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EconomySystem());       // é o rendimento dele que se reparte
        w.Register(new LendLeaseSystem());
        return w;
    }

    private static float Waste(World w) => w.Rule("lend_lease_waste", 0.2f);
    private static float Max(World w) => w.Rule("lend_lease_max_share", 0.35f);
    private static float Min(World w) => w.Rule("lend_lease_min_share", 0.05f);

    /// <summary>Sem acordo nenhum o sistema não mexe em cofre nenhum.</summary>
    [Fact]
    public void SemAcordoNadaSeMove()
    {
        var w = Build();
        w.Countries[1].Money = 100f; w.Countries[2].Money = 50f;
        float income = EconomySystem.Income(w, 1);
        TestWorld.Days(w, 1);
        Assert.Equal(100f + income, w.Countries[1].Money, 2);
        Assert.Equal(50f + EconomySystem.Income(w, 2), w.Countries[2].Money, 2);
    }

    /// <summary>A fatia sai do benfeitor inteira e chega ao aliado descontadas as perdas de caminho.</summary>
    [Fact]
    public void OQueSaiEAFatiaOQueChegaEMenos()
    {
        var w = Build();
        w.Countries[1].Money = 100f; w.Countries[2].Money = 0f;
        Assert.Null(new LendLeaseCommand(1, 2, 0.2f).Validate(w));
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);

        float income1 = EconomySystem.Income(w, 1), income2 = EconomySystem.Income(w, 2);
        float sent = income1 * 0.2f, landed = sent * (1f - Waste(w));
        TestWorld.Days(w, 1);

        Assert.Equal(100f + income1 - sent, w.Countries[1].Money, 2);
        Assert.Equal(income2 + landed, w.Countries[2].Money, 2);
        Assert.Equal(landed, Assert.Single(w.LendLeases).SentTotal, 2);
    }

    /// <summary>O total entregue acumula dia a dia — é o que a interface mostra ao fim de uma campanha.</summary>
    [Fact]
    public void OTotalEntregueAcumula()
    {
        var w = Build();
        w.Countries[1].Money = 1000f;
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);
        float landed = LendLeaseSystem.Landed(w, w.LendLeases[0]);
        TestWorld.Days(w, 5);
        Assert.Equal(landed * 5f, w.LendLeases[0].SentTotal, 1);
    }

    /// <summary>A fatia é do rendimento e não do cofre: perder metade das regiões corta o empréstimo
    /// sozinho, sem ninguém o rever.</summary>
    [Fact]
    public void AFatiaEDoRendimentoENaoDoCofre()
    {
        var w = Build();
        w.Countries[1].Money = 1000f;
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);
        float rich = LendLeaseSystem.Daily(w, w.LendLeases[0]);
        w.Regions[3].ControllerId = 2;                 // perdeu uma das três regiões
        float poor = LendLeaseSystem.Daily(w, w.LendLeases[0]);
        Assert.True(poor < rich);
        Assert.Equal(rich * 2f / 3f, poor, 2);
    }

    /// <summary>Contas do país: o que sai por dia, o que entra por dia e a fatia ainda por prometer.</summary>
    [Fact]
    public void ContasDeQuemDaEDeQuemRecebe()
    {
        var w = Build();
        w.Countries[1].Money = 1000f;
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);
        float daily = EconomySystem.Income(w, 1) * 0.2f;
        Assert.Equal(daily, LendLeaseSystem.Out(w, 1), 2);
        Assert.Equal(daily * (1f - Waste(w)), LendLeaseSystem.In(w, 2), 2);
        Assert.Equal(0.2f, LendLeaseSystem.Given(w, 1), 3);
        Assert.Equal(Max(w) - 0.2f, LendLeaseSystem.FreeShare(w, 1), 3);
        Assert.True(LendLeaseSystem.Benefactor(w, 1, 2));
        Assert.False(LendLeaseSystem.Benefactor(w, 2, 1));
    }

    /// <summary>Assinar ao mesmo país outra vez revê a fatia; não faz um segundo acordo.</summary>
    [Fact]
    public void AssinarOutraVezReveAFatia()
    {
        var w = Build();
        new LendLeaseCommand(1, 2, 0.1f).Execute(w);
        Assert.Null(new LendLeaseCommand(1, 2, 0.3f).Validate(w));
        new LendLeaseCommand(1, 2, 0.3f).Execute(w);
        var l = Assert.Single(w.LendLeases);
        Assert.Equal(0.3f, l.Share, 3);
    }

    /// <summary>Fatias fora do intervalo da tabela não passam, e a soma de todas tem tecto.</summary>
    [Fact]
    public void OTectoDaTabelaEQueManda()
    {
        var w = Build();
        Assert.NotNull(new LendLeaseCommand(1, 2, Min(w) / 2f).Validate(w));
        Assert.NotNull(new LendLeaseCommand(1, 2, Max(w) + 0.01f).Validate(w));
        Assert.NotNull(new LendLeaseCommand(1, 1, 0.1f).Validate(w));       // a si próprio, não

        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama" };
        new LendLeaseCommand(1, 2, Max(w) - Min(w)).Execute(w);
        Assert.NotNull(new LendLeaseCommand(1, 3, Max(w)).Validate(w));      // já não há fatia para isso
        Assert.Null(new LendLeaseCommand(1, 3, Min(w)).Validate(w));         // o que sobra, sim
    }

    /// <summary>Nada a inimigos: material entregue a quem nos combate volta apontado a nós.</summary>
    [Fact]
    public void AInimigoNaoSeEmpresta()
    {
        var w = Build();
        w.StartWar(1, 2);
        Assert.NotNull(new LendLeaseCommand(1, 2, 0.2f).Validate(w));
    }

    /// <summary>Guerra entre os dois fecha a torneira no dia seguinte, com aviso.</summary>
    [Fact]
    public void GuerraEntreOsDoisFechaOEmprestimo()
    {
        var w = Build();
        w.Countries[1].Money = 1000f;
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);
        LendLeaseEnded? ended = null;
        w.Events.Subscribe<LendLeaseEnded>(e => ended = e);
        w.StartWar(1, 2);
        TestWorld.Days(w, 1);
        Assert.Empty(w.LendLeases);
        Assert.NotNull(ended);
        Assert.Equal(1, ended!.FromCountryId);
        Assert.Equal(2, ended.ToCountryId);
    }

    /// <summary>Quem capitula deixa de receber (e de mandar).</summary>
    [Fact]
    public void CapitulacaoFechaOEmprestimo()
    {
        var w = Build();
        w.Countries[1].Money = 1000f;
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);
        w.Countries[2].Capitulated = true;
        TestWorld.Days(w, 1);
        Assert.Empty(w.LendLeases);
    }

    /// <summary>Cofre no vermelho fecha a torneira: não se empresta material a descoberto, por muito que
    /// o rendimento do dia dissesse que sim.</summary>
    [Fact]
    public void CofreNoVermelhoFechaATorneira()
    {
        var w = Build();
        new LendLeaseCommand(1, 2, Max(w)).Execute(w);
        w.Countries[1].Money = -100f;                  // dívida acima do que um dia de rendimento tapa
        TestWorld.Days(w, 1);
        Assert.Empty(w.LendLeases);
    }

    /// <summary>Cancelar fecha o acordo dos dois lados: quem dá cansa-se, quem recebe também dispensa.</summary>
    [Fact]
    public void QualquerUmDosDoisPodeFechar()
    {
        var w = Build();
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);
        Assert.Null(new CancelLendLeaseCommand(2, 1).Validate(w));   // pelo lado de quem recebe
        new CancelLendLeaseCommand(2, 1).Execute(w);
        Assert.Empty(w.LendLeases);
        Assert.NotNull(new CancelLendLeaseCommand(1, 2).Validate(w));
    }

    /// <summary>A IA de um país em paz abre a torneira ao aliado de facção em guerra, e fecha-a quando a
    /// guerra dele acaba.</summary>
    [Fact]
    public void IaEmprestaAoAliadoEmGuerra()
    {
        var w = Build();
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", Money = 500f };
        w.Factions["aliados"] = new Faction("aliados", "Aliados", "", new List<int> { 1, 2 });
        w.Countries[1].Money = 500f;
        w.StartWar(2, 3);                            // o aliado 2 é que está a arder

        w.Register(new AiSystem());
        TestWorld.Days(w, 1);                        // dia 0: a ronda da IA cai já no primeiro dia
        var l = Assert.Single(w.LendLeases);
        Assert.Equal(1, l.FromId);
        Assert.Equal(2, l.ToId);
        Assert.Equal(w.Rule("lend_lease_ai_share", 0.15f), l.Share, 3);

        w.Countries[2].AtWarWith.Clear(); w.Countries[3].AtWarWith.Clear();
        TestWorld.Days(w, (int)w.Rule("ai_period_days", 3f));   // a IA só volta a olhar de ai_period_days em ai_period_days
        Assert.Empty(w.LendLeases);                  // acabada a guerra dele, acaba o esforço
    }

    /// <summary>Os empréstimos em vigor sobrevivem ao save, com fatia, dia e total entregue.</summary>
    [Fact]
    public void OEmprestimoSobreviveAoSave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        new LendLeaseCommand(1, 2, 0.2f).Execute(w);
        w.LendLeases[0].SentTotal = 42.5f;

        using var save = new MsSqliteDatabase();
        var schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);

        var back = Assert.Single(w2.LendLeases);
        Assert.Equal(1, back.FromId);
        Assert.Equal(2, back.ToId);
        Assert.Equal(0.2f, back.Share, 3);
        Assert.Equal(42.5f, back.SentTotal, 2);
    }
}
