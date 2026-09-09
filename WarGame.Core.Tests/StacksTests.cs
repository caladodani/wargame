using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A pilha do mapa: o que uma província tem, lido como uma coisa só. O que aqui se defende é que a
/// leitura conta só a tropa daquele país, que o estado sai da tabela stack_state pela ordem dela (o cerco
/// tapa a marcha, não ao contrário) e que uma província sem tropa nossa não tem pilha nenhuma.</summary>
public class StacksTests
{
    [Fact]
    public void A_pilha_conta_so_a_tropa_daquele_pais_e_diz_como_ela_esta()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        for (int i = 1; i <= 3; i++) TestWorld.AddDivision(w, i, 1, TestWorld.Inf, 2);
        TestWorld.AddDivision(w, 9, 2, TestWorld.Inf2, 2);          // tropa do vizinho na mesma província
        w.Divisions[1].Org = 50f; w.Divisions[2].Org = 100f; w.Divisions[3].Org = 90f;
        w.Divisions[1].Kit = 0.5f;

        var s = Stacks.In(w, 2, 1)!.Value;
        Assert.Equal(3, s.Count);                                    // a do país 2 não entra
        Assert.Equal(0.8f, s.Org, 0.001f);
        Assert.Equal((0.5f + 1f + 1f) / 3f, s.Kit, 0.001f);
        Assert.Null(Stacks.In(w, 5, 1));                             // província sem tropa nossa: pilha nenhuma
    }

    [Fact]
    public void O_estado_da_pilha_segue_a_ordem_da_tabela()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        var d = w.Divisions[1];

        Assert.Equal("parada", Stacks.In(w, 2, 1)!.Value.State);
        d.Entrench = 2f;
        Assert.Equal("cavada", Stacks.In(w, 2, 1)!.Value.State);
        d.SetPath(new[] { 3 });
        Assert.Equal("marcha", Stacks.In(w, 2, 1)!.Value.State);     // andar tapa estar cavada
        d.Redeploying = true;
        Assert.Equal("comboio", Stacks.In(w, 2, 1)!.Value.State);
        d.Cut = true;
        Assert.Equal("cercada", Stacks.In(w, 2, 1)!.Value.State);    // o cerco é a notícia maior de todas
    }

    [Fact]
    public void A_batalha_na_provincia_manda_no_contador_e_o_nome_vem_da_tabela()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 2);
        w.Divisions[1].SetPath(new[] { 3 });
        w.ActiveBattles.Add(new Battle { RegionId = 2, AttackerCountryId = 2 });

        var s = Stacks.In(w, 2, 1)!.Value;
        Assert.Equal("combate", s.State);                            // bater-se tapa marchar
        Assert.Equal(w.StackStates["combate"].Name, s.Name);         // nome e chapa são da tabela, não do C#
        Assert.Equal(w.StackStates["combate"].Glyph, s.Glyph);
    }
}
