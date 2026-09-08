using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Os rios. A coluna region.river já pesava no combate (o assalto paga o chão molhado e a frente
/// aperta), mas o mapa não desenhava água nenhuma: quem olhava não via onde é que atravessar custava caro.
/// Aqui prova-se a regra de onde a água se vê — nas duas margens, nunca numa só — e que ela nunca aparece
/// onde o combate não cobra rio nenhum. É a mesma linha da tabela a mandar no desenho e na conta.
/// Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2).</summary>
public class RiverTests
{
    /// <summary>O mundo em linha com rio nas regiões pedidas (Region.River é init: refaz-se a região).</summary>
    private static World Wet(params int[] rivers)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, 6, 3);
        var wet = new HashSet<int>(rivers);
        foreach (int id in w.Regions.Keys.ToList())
        {
            var r = w.Regions[id];
            var copy = new Region
            {
                Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
                ControllerId = r.ControllerId, Terrain = r.Terrain, Population = r.Population,
                CenterX = r.CenterX, CenterY = r.CenterY, River = wet.Contains(id),
            };
            foreach (int n in r.Neighbours) copy.Neighbours.Add(n);
            w.Regions[id] = copy;
        }
        return w;
    }

    /// <summary>A água mora entre duas margens: uma região de rio encostada a terra seca não faz rio no
    /// traço comum. E a pergunta é a mesma venha de que lado vier.</summary>
    [Fact]
    public void A_agua_so_se_ve_onde_as_duas_margens_tem_rio()
    {
        var w = Wet(2);
        Assert.False(Rivers.Between(w, 1, 2));                 // só uma das margens é de rio
        Assert.False(Rivers.Between(w, 2, 3));

        var both = Wet(2, 3);
        Assert.True(Rivers.Between(both, 2, 3));
        Assert.True(Rivers.Between(both, 3, 2));               // simétrico
        Assert.False(Rivers.Between(both, 2, 2));              // consigo mesma não há traço nenhum
        var apart = Wet(1, 3);
        Assert.False(Rivers.Between(apart, 1, 3));             // duas margens de rio que não se tocam
    }

    /// <summary>Toda a região de rio tem onde mostrar água: as margens de rio quando as há, e a orla de
    /// terra toda quando não há nenhuma. Uma região que diz "rio" na ficha e não desenha nada em lado
    /// nenhum era o mapa a mentir por omissão.</summary>
    [Fact]
    public void Toda_a_regiao_de_rio_mostra_agua_nalgum_lado()
    {
        var w = Wet(2, 3, 5);
        foreach (var r in w.Regions.Values.Where(r => r.River))
            Assert.NotEmpty(Rivers.Banks(w, r));

        Assert.Equal(new[] { 3 }, Rivers.Banks(w, w.Regions[2]));        // 1 é seca, 3 é de rio
        Assert.Equal(new[] { 2 }, Rivers.Banks(w, w.Regions[3]));
        Assert.Equal(new[] { 4, 6 }, Rivers.Banks(w, w.Regions[5]));     // sem vizinha de rio: a orla toda
    }

    /// <summary>Quem não tem rio não tem margem nem conta: nem água desenhada, nem assalto mais caro.</summary>
    [Fact]
    public void Sem_rio_nao_ha_margem_nem_conta()
    {
        var w = Wet(2);
        Assert.Empty(Rivers.Banks(w, w.Regions[1]));
        Assert.False(Rivers.Crossing(w.Regions[1]));
        Assert.Equal(1f, BattleField.RiverBite(w, w.Regions[1]));
        Assert.Equal("sem rio", Rivers.Line(w, w.Regions[1]));
    }

    /// <summary>A regra que impede o mapa de discordar do combate: onde se desenha água, o assalto paga rio
    /// nos dois sentidos. O traço azul não é decoração — é o aviso de que aquela passagem custa.</summary>
    [Fact]
    public void A_agua_desenhada_nunca_aparece_onde_o_assalto_nao_paga_rio()
    {
        var w = Wet(2, 3, 4);
        int seen = 0;
        foreach (var r in w.Regions.Values)
            foreach (int n in Rivers.Banks(w, r))
            {
                if (!Rivers.Between(w, r.Id, n)) continue;
                seen++;
                Assert.True(Rivers.Crossing(r));
                Assert.True(Rivers.Crossing(w.Regions[n]));
                Assert.True(BattleField.RiverBite(w, r) < 1f, "o assalto para dentro da margem não paga rio");
                Assert.True(BattleField.RiverBite(w, w.Regions[n]) < 1f);
            }
        Assert.Equal(4, seen);                                  // 2↔3 e 3↔4, contados dos dois lados
        Assert.Contains("rio ×", Rivers.Line(w, w.Regions[3]));
    }
}
