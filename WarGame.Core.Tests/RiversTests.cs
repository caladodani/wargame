using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O guarda da água: por onde é que o rio se desenha.
///
/// O dono viu "as linhas dos rios muito gordas". Parte disso era a largura do traço, que é da apresentação;
/// a outra parte estava aqui: uma região de rio sem vizinha de rio nenhuma mandava desenhar a orla de terra
/// TODA, e uma província inteira contornada a azul não lê como rio nenhum — lê como mancha.</summary>
public class RiversTests
{
    private static Region Land(int id, bool river = false, bool coast = false)
    {
        var r = new Region { Id = id, Name = "R" + id, OwnerId = 1, ControllerId = 1, Terrain = "plain", River = river };
        if (coast) r.SeaNeighbours[90] = 1f;
        return r;
    }

    [Fact]
    public void A_regiao_de_rio_sozinha_mostra_uma_margem_so_e_a_que_vai_dar_ao_mar()
    {
        var (w, _) = TestWorld.Build();
        for (int i = 1; i <= 4; i++)
        {
            var r = Land(i, river: i == 3, coast: i == 4);
            if (i > 1) r.Neighbours.Add(i - 1);
            if (i < 4) r.Neighbours.Add(i + 1);
            w.Regions[i] = r;
        }

        // sem vizinha de rio: uma margem só, e a que dá para o mar — não as duas nem a orla toda
        Assert.Equal(new[] { 4 }, Rivers.Banks(w, w.Regions[3]));

        // com vizinha de rio, a água volta a ser o traço comum das duas margens
        w.Regions[2] = Land(2, river: true);
        w.Regions[2].Neighbours.Add(1); w.Regions[2].Neighbours.Add(3);
        Assert.Equal(new[] { 2 }, Rivers.Banks(w, w.Regions[3]));
    }
}
