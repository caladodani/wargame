using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Leis nacionais: default por grupo, mudança por comando (custo + efeitos), IA escala em guerra, save.</summary>
public class LawTests
{
    [Fact]
    public void Default_IsNeutral_ChangeAppliesEffects()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        var c = w.Countries[1]; c.Political = 100f; c.Money = 100f;   // o cofre está cheio de propósito
        w.ApplyTechs(c);
        Assert.Equal("consc_volunteer", w.ActiveLaw(c, "conscription")!.Id);
        float baseConsc = c.Stat("conscription");

        Assert.NotNull(new ChangeLawCommand(1, "consc_volunteer").Validate(w));   // já activa
        var cmd = new ChangeLawCommand(1, "consc_limited");                        // degrau sem exigência de tensão
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Equal(100f - w.Rule("law_change_cost", 30f), c.Political, 0.01f);
        Assert.Equal(100f, c.Money, 0.01f);                // o cofre de produção nem se mexeu
        Assert.Equal(baseConsc * 1.25f, c.Stat("conscription"), 0.01f);
        Assert.Equal(0.98f, c.Stat("industry"), 0.001f);   // penalização da lei (sem outras fontes)
    }

    [Fact]
    public void Change_NeedsPoliticalPower()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Money = 1e6f;      // o cofre cheio não compra lei nenhuma…
        w.Countries[1].Political = 0f;    // …e o bolso político vazio tranca-a
        Assert.NotNull(new ChangeLawCommand(1, "consc_limited").Validate(w));
    }

    [Fact]
    public void Ai_EscalatesOneStep_AtWar()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new AiSystem());
        // poder político para legislar e cofre com que a IA se ocupe: um país sem divisões e sem
        // dinheiro nenhum é saltado no início da ronda e nunca chega às leis
        var c = w.Countries[2]; c.Political = 500f; c.Money = 500f;
        var groups = w.Laws.Values.Select(l => l.Group).Distinct().ToList();
        int before = groups.Sum(g => w.ActiveLaw(c, g)!.Sort);
        w.StartWar(1, 2);
        TestWorld.Days(w, 1);   // a IA corre no dia 0 (0 % ai_period_days == 0)
        Assert.Equal(before + 1, groups.Sum(g => w.ActiveLaw(c, g)!.Sort));   // subiu exactamente um degrau num grupo
    }

    [Fact]
    public void SaveRoundTrip_RestoresLaws()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].Political = 100f;
        new ChangeLawCommand(1, "econ_war").Validate(w);
        w.Countries[1].Laws["economy"] = "econ_war";

        string schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal("econ_war", w2.ActiveLaw(w2.Countries[1], "economy")!.Id);
        Assert.True(w2.Countries[1].Stat("production_speed") > 1.2f);
    }
}
