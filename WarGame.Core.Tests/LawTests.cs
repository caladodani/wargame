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



}
