using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A conta aberta de cada característica do país: quem a multiplica, com nome próprio e por
/// quanto. O contrato que aqui se guarda é o mais importante de todos — base × todas as linhas TEM de dar
/// exactamente `Country.Stat(key)`. Um multiplicador novo no país que ninguém conte aqui fica a mentir ao
/// jogador, e é isso que estes testes recusam.</summary>
public class StatLedgerTests
{
    private static (World w, Country c) Small()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return (w, w.Countries[1]);
    }









    [Fact]
    public void No_mundo_a_serio_a_conta_fecha_para_todas_as_caracteristicas_de_todos_os_paises()
    {
        var w = FactionTests.BuildReal();
        // um mundo acabado de abrir ainda não correu os sistemas que enchem os dicionários do país (os
        // recursos e os edifícios são recontados todos os dias, os comandantes e o governo ao carregar).
        // A conta do ledger é a de um mundo A ANDAR, que é o único que o jogador vê — põe-se cá em pé.
        new ResourceSystem().Tick(w);
        new ConstructionSystem().Tick(w);
        foreach (var country in w.Countries.Values)
        {
            World.ApplyGenerals(w, country); World.ApplyCabinet(w, country);
            World.ApplyParty(w, country); w.ApplyTechs(country);
        }
        Assert.Equal(12, w.StatSourceDefs.Count);
        int checkedKeys = 0;
        foreach (var c in w.Countries.Values)
            foreach (var k in StatLedger.Keys(w, c))
            {
                Assert.Equal(c.Stat(k), StatLedger.Total(w, c, k), 0.001f);
                checkedKeys++;
            }
        Assert.True(checkedKeys > 100, $"conta pequena de mais para valer como prova: {checkedKeys}");
        Assert.Contains("famílias de fonte", StatLedger.Smoke(w, w.Countries.Values.First().Id));
    }
}
