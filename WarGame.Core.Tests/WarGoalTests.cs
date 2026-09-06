using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Objectivos de guerra da IA: país 2 (aggression) contra o vizinho 1; regras da tabela sobrepostas
/// nos testes. Desde a justificação (DiplomacySystem) a IA primeiro justifica (war_justify_days, aqui 2) e a
/// guerra declara-se sozinha ao fim.</summary>
public class WarGoalTests
{
    private static World Setup(float aggression, float chance = 1f, int minDay = 0)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Rules["ai_war_chance"] = chance; w.Rules["ai_war_min_day"] = minDay; w.Rules["ai_war_ratio"] = 1f;
        w.Rules["war_justify_days"] = 2f;
        w.Countries[2].Stats["aggression"] = aggression;
        for (int i = 0; i < 4; i++) TestWorld.AddDivision(w, 10 + i, 2, TestWorld.Inf2, 4);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
        w.Register(new DiplomacySystem()); w.Register(new AiSystem());
        return w;
    }

    private static void Rounds(World w, int n) { int p = Math.Max(1, (int)w.Rule("ai_period_days", 3)); for (int i = 0; i < n * p; i++) w.Tick(); }

    [Fact]
    public void Agressivo_declara_guerra_ao_vizinho_fraco()
    {
        var w = Setup(1f);
        WarDeclared? evt = null; w.Events.Subscribe<WarDeclared>(e => evt = e);
        Rounds(w, 2);
        Assert.True(w.AreAtWar(2, 1));
        Assert.NotNull(evt); Assert.Equal(2, evt!.Aggressor);
    }

    [Fact]
    public void Pacifico_nunca_declara()
    {
        var w = Setup(0f);
        Rounds(w, 10);
        Assert.False(w.AreAtWar(2, 1));
    }

    [Fact]
    public void Nao_ataca_quem_e_forte_demais()
    {
        var w = Setup(1f);
        for (int i = 0; i < 6; i++) TestWorld.AddDivision(w, 20 + i, 1, TestWorld.Inf, 2);   // 7 vs 4 com ratio 1
        Rounds(w, 5);
        Assert.False(w.AreAtWar(2, 1));
    }

    [Fact]
    public void Dissuasao_nuclear_trava_quem_nao_tem_ogivas()
    {
        var w = Setup(1f);
        w.Countries[1].Nukes = 1;   // alvo é potência nuclear, agressor não
        Rounds(w, 5);
        Assert.False(w.AreAtWar(2, 1));
        w.Countries[2].Nukes = 1;   // paridade nuclear: dissuasão deixa de travar
        Rounds(w, 3);
        Assert.True(w.AreAtWar(2, 1));
    }

    [Fact]
    public void Jogador_so_depois_de_ai_war_player_min_day()
    {
        var w = Setup(1f); w.Countries[1].IsPlayer = true; w.Rules["ai_war_player_min_day"] = 30;
        Rounds(w, 3);
        Assert.False(w.AreAtWar(2, 1));
        Assert.Null(w.Countries[2].JustifyTarget);   // nem sequer justifica antes do dia mínimo
        while (w.Clock.Day < 30) w.Tick();
        Rounds(w, 2);
        Assert.True(w.AreAtWar(2, 1));
    }

    [Fact]
    public void Guerras_iniciais_carregadas_da_tabela()
    {
        var (w, db) = TestWorld.Build();
        w.Countries[1] = new Country { Id = 1, Tag = "RUS" }; w.Countries[2] = new Country { Id = 2, Tag = "UKR" }; w.Countries[3] = new Country { Id = 3, Tag = "PRT" };
        new WarGame.Core.Data.SqlWorldRepository(db).LoadStartWars(w);
        Assert.True(w.AreAtWar(1, 2)); Assert.False(w.AreAtWar(1, 3));
    }
}
