using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Decisões nacionais: o preço em várias moedas, a porta (decision_req), os efeitos todos de uma
/// decisão (decision_effect) e as MISSÕES com prazo — cumpridas pagam, falhadas castigam, e ambas
/// atravessam o save. Mais a prova de que as decisões semeadas estão inteiras.</summary>
public class DecisionTests
{
    private static DecisionDef Def(string id, string cat = "industria", float cost = 30f, float money = 0f,
                                   float manpower = 0f, float stability = 0f, int days = 3, int cooldown = 5,
                                   int missionDays = 0, string goalKey = "", float goalValue = 0f,
                                   float reward = 40f, float rewardStab = 3f, float fail = 20f, float failStab = 4f) =>
        new(id, "Mobilização", cat, "nota", cost, money, manpower, stability, days, cooldown, missionDays,
            goalKey, goalValue, reward, rewardStab, fail, failStab, "fabrica", 1);

    private static (World w, MsSqliteDatabase db) Setup(DecisionDef def, params (string Key, float Mult)[] effects)
    {
        var (w, db) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.DecisionDefs.Clear(); w.DecisionEffects.Clear(); w.DecisionReqs.Clear();
        w.DecisionDefs[def.Id] = def;
        w.DecisionEffects[def.Id] = effects.Length == 0
            ? new List<(string, float)> { ("industry", 1.15f) }
            : effects.ToList();
        w.Register(new DecisionSystem());
        w.Countries[1].Political = 200f;
        w.Countries[1].Money = 1000f;
        w.Countries[1].Manpower = 5000f;
        w.Countries[1].Stability = 60f;
        return (w, db);
    }

    [Fact]
    public void Assinar_paga_todas_as_moedas_e_aplica_os_efeitos_todos()
    {
        var (w, _) = Setup(Def("mob", money: 250f, manpower: 400f, stability: 5f),
                           ("industry", 1.15f), ("production_speed", 1.2f));
        var c = w.Countries[1];
        float ind = c.Stat("industry"), prod = c.Stat("production_speed");

        var cmd = new ActivateDecisionCommand(1, "mob");
        Assert.Null(cmd.Validate(w)); cmd.Execute(w);

        Assert.Equal(170f, c.Political, 0.01f);
        Assert.Equal(750f, c.Money, 0.01f);
        Assert.Equal(4600f, c.Manpower, 0.01f);
        Assert.Equal(55f, c.Stability, 0.01f);
        Assert.Equal(ind * 1.15f, c.Stat("industry"), 0.01f);
        Assert.Equal(prod * 1.2f, c.Stat("production_speed"), 0.01f);
        Assert.NotNull(new ActivateDecisionCommand(1, "mob").Validate(w));   // já a correr
    }

    [Fact]
    public void Expira_e_so_depois_da_espera_e_que_volta()
    {
        var (w, _) = Setup(Def("mob"));
        var c = w.Countries[1];
        float baseVal = c.Stat("industry");
        new ActivateDecisionCommand(1, "mob").Execute(w);
        var expired = new List<DecisionExpired>();
        w.Events.Subscribe<DecisionExpired>(expired.Add);
        TestWorld.Days(w, 4);
        Assert.Empty(expired);
        TestWorld.Days(w, 1);
        Assert.Single(expired);
        Assert.Equal(baseVal, c.Stat("industry"), 0.01f);
        Assert.Contains("espera", new ActivateDecisionCommand(1, "mob").Validate(w));
        TestWorld.Days(w, 4);
        Assert.Null(new ActivateDecisionCommand(1, "mob").Validate(w));
    }

    /// <summary>A porta não é um "não" mudo: diz a métrica, quanto temos e quanto é preciso. É o mesmo
    /// texto que o cartão cinzento mostra, porque comando e ecrã perguntam ao mesmo sítio.</summary>
    [Fact]
    public void A_porta_fechada_diz_o_que_falta()
    {
        var (w, _) = Setup(Def("mob"));
        var c = w.Countries[1];
        w.DecisionReqs["mob"] = new List<DecisionReq>
        {
            new("mob", "war", 1f, null),
            new("mob", "stability", null, 90f),
        };
        string? why = new ActivateDecisionCommand(1, "mob").Validate(w);
        Assert.NotNull(why);
        Assert.Contains("guerras", why);

        c.AtWarWith.Add(2);
        Assert.Null(new ActivateDecisionCommand(1, "mob").Validate(w));

        c.Stability = 95f;
        Assert.Contains("estabilidade", new ActivateDecisionCommand(1, "mob").Validate(w));
    }

    [Fact]
    public void Missao_cumprida_no_prazo_paga_o_premio()
    {
        var (w, _) = Setup(Def("plano", days: 20, missionDays: 10, goalKey: "stability", goalValue: 10f));
        var c = w.Countries[1];
        new ActivateDecisionCommand(1, "plano").Execute(w);
        var act = Decisions.Active(w, 1, "plano")!;
        Assert.Equal(60f, act.GoalBase, 0.01f);
        float pp = c.Political;

        var ended = new List<DecisionMissionEnded>();
        w.Events.Subscribe<DecisionMissionEnded>(ended.Add);
        TestWorld.Days(w, 3);
        Assert.Empty(ended);                                  // ainda há prazo e a meta não chegou
        c.Stability = 72f;                                    // subiu 12: passou os 10 pedidos
        TestWorld.Days(w, 1);
        Assert.Single(ended);
        Assert.True(ended[0].Met);
        Assert.Equal(pp + 40f, c.Political, 0.01f);
        Assert.Equal(75f, c.Stability, 0.01f);                // 72 + 3 de prémio
        Assert.NotNull(Decisions.Active(w, 1, "plano"));       // o efeito continua até ao fim dos dias
        Assert.Equal(-1, Decisions.Active(w, 1, "plano")!.MissionUntil);
    }

    [Fact]
    public void Missao_falhada_no_prazo_castiga()
    {
        var (w, _) = Setup(Def("plano", days: 30, missionDays: 5, goalKey: "stability", goalValue: 10f));
        var c = w.Countries[1];
        new ActivateDecisionCommand(1, "plano").Execute(w);
        float pp = c.Political;
        var ended = new List<DecisionMissionEnded>();
        w.Events.Subscribe<DecisionMissionEnded>(ended.Add);

        TestWorld.Days(w, 6);
        Assert.Single(ended);
        Assert.False(ended[0].Met);
        Assert.Equal(pp - 20f, c.Political, 0.01f);
        Assert.Equal(56f, c.Stability, 0.01f);                // 60 − 4 de castigo
    }

    /// <summary>A meta é um DELTA guardado no save: o dia do prazo e a leitura de partida têm de voltar,
    /// senão uma missão a meio recomeçava a contar do zero ao abrir o jogo.</summary>
    [Fact]
    public void A_missao_atravessa_o_save()
    {
        var (w, staticDb) = Setup(Def("plano", days: 40, missionDays: 20, goalKey: "stability", goalValue: 10f));
        new ActivateDecisionCommand(1, "plano").Execute(w);
        var antes = Decisions.Active(w, 1, "plano")!;

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = Setup(Def("plano", days: 40, missionDays: 20, goalKey: "stability", goalValue: 10f));
        repo.LoadSave(w2, save);
        var depois = w2.ActiveDecisions.Single(a => a.DecisionId == "plano");
        Assert.Equal(antes.UntilDay, depois.UntilDay);
        Assert.Equal(antes.MissionUntil, depois.MissionUntil);
        Assert.Equal(antes.GoalBase, depois.GoalBase, 0.01f);
    }

    /// <summary>As decisões semeadas: cada uma tem categoria da tabela, chapa desenhada, e ou muda alguma
    /// coisa ou é missão. Nada aqui pode ficar mudo — um cartão sem efeito nenhum é um botão que engana.</summary>
    [Fact]
    public void Cada_decisao_semeada_esta_inteira()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.DecisionDefs.Count >= 30, $"só {w.DecisionDefs.Count} decisões — o quadro voltou a encolher");
        Assert.True(w.DecisionCategories.Count >= 5);

        foreach (var def in w.DecisionDefs.Values)
        {
            Assert.True(w.DecisionCategories.ContainsKey(def.Category), $"{def.Id}: categoria {def.Category} não existe");
            Assert.NotEmpty(def.Glyph);
            Assert.NotEmpty(def.Note);
            var eff = Decisions.Effects(w, def.Id);
            // "faz alguma coisa" = multiplica, é missão, ou DEVOLVE moeda (um custo negativo é uma prenda:
            // o aumento de salários paga estabilidade do cofre e não multiplica nada)
            bool gives = def.Stability < 0f || def.Money < 0f || def.Manpower < 0f;
            Assert.True(eff.Count > 0 || def.IsMission || gives, $"{def.Id} não muda nada nem é missão");
            foreach (var (key, mult) in eff)
            {
                Assert.True(w.CountryStatDefs.ContainsKey(key), $"{def.Id} mexe em {key}, que não tem nome na tabela");
                Assert.NotEqual(1f, mult);
            }
            foreach (var r in Decisions.Reqs(w, def.Id))
            {
                Assert.True(r.Min is not null || r.Max is not null, $"{def.Id}: porta {r.Key} sem limite nenhum");
                Assert.NotEqual(r.Key, Decisions.MetricName(w, r.Key));   // métrica que ninguém sabe ler
            }
        }
        // cada categoria tem cartões para mostrar
        foreach (var cat in w.DecisionCategories.Values)
            Assert.True(w.DecisionDefs.Values.Any(d => d.Category == cat.Id), $"a pasta {cat.Id} está vazia");
    }

    /// <summary>As missões pagam e castigam de verdade: prazo, meta legível e prémio maior do que zero.</summary>
    [Fact]
    public void As_missoes_semeadas_tem_prazo_meta_e_premio()
    {
        var (w, _) = TestWorld.Build();
        var missions = w.DecisionDefs.Values.Where(d => d.IsMission).ToList();
        Assert.True(missions.Count >= 4, $"só {missions.Count} missões — era o que dava vida ao quadro");
        foreach (var m in missions)
        {
            Assert.True(m.MissionDays >= 30, $"{m.Id}: prazo de {m.MissionDays} dias é curto de mais");
            Assert.NotEqual(0f, m.GoalValue);
            Assert.NotEqual(m.GoalKey, Decisions.MetricName(w, m.GoalKey));
            Assert.True(m.RewardPolitical > 0f, $"{m.Id} não paga nada a quem a cumpre");
            Assert.True(m.FailPolitical > 0f || m.FailStability > 0f, $"{m.Id} falhada não custa nada");
        }
    }

    /// <summary>Todas as características que o jogo lê têm nome na tabela: sem isso o cartão mostrava a
    /// chave crua ("occupied_yield") na cara do jogador.</summary>
    [Fact]
    public void As_caracteristicas_de_pais_tem_nome()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.CountryStatDefs.Count >= 20);
        foreach (var d in w.CountryStatDefs.Values)
        {
            Assert.NotEmpty(d.Name);
            Assert.NotEmpty(d.Glyph);
        }
    }
}
