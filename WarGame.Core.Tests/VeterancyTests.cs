using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Veterania: XP por dia de batalha (tecto xp_max), os graus da tabela veterancy — recruta,
/// treinada, veterana, elite — e o bónus de força que cada grau dá em combate. O grau não se guarda em lado
/// nenhum: lê-se do Xp da divisão, e é isso que aqui se prova.</summary>
public class VeterancyTests
{



    /// <summary>O grau lê-se do Xp, degrau a degrau: no XP exacto do degrau já se está nele, e um ponto
    /// abaixo ainda se está no de baixo. É a prova de que não há grau guardado nenhum.</summary>
    [Fact]
    public void O_grau_le_se_do_xp_e_muda_no_instante()
    {
        var (w, db) = TestWorld.Build(); using var _ = db;
        TestWorld.LinearMap(w);
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        var steps = w.VeterancyTiers.Values.OrderBy(t => t.MinXp).ToList();
        foreach (var t in steps)
        {
            d.Xp = t.MinXp;
            Assert.Equal(t.Id, Veterancy.Tier(w, d)!.Id);
            Assert.Equal(t.Bonus, Veterancy.Bonus(w, d), 4);
            Assert.Equal(t.Chevrons, Veterancy.Chevrons(w, d.Xp));
            if (t.MinXp > 0f)
            {
                d.Xp = t.MinXp - 0.5f;                                      // meio ponto abaixo ainda é o grau de baixo
                Assert.NotEqual(t.Id, Veterancy.Tier(w, d)!.Id);
            }
        }
        // e o que falta para o degrau seguinte diz-se; no topo não falta nada
        d.Xp = 0f;
        var (next, missing) = Veterancy.Ahead(w, d.Xp)!.Value;
        Assert.Equal(steps[1].Id, next.Id);
        Assert.Equal(steps[1].MinXp, missing, 3);
        Assert.Contains(next.Name, Veterancy.Line(w, d));
        d.Xp = steps[^1].MinXp;
        Assert.Null(Veterancy.Ahead(w, d.Xp));
    }



}
