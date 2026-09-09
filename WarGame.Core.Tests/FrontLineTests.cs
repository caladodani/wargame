using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A linha da frente como linha. O que aqui se prova não é o desenho (isso é do Godot) mas o
/// encadeamento: que os encostos de uma frente saem por ordem, num fio só sempre que a vizinhança dá, e que
/// partem exactamente onde a frente parte mesmo — um enclave, uma ilha, uma bifurcação.</summary>
public class FrontLineTests
{
    /// <summary>Mapa em grelha de w×h: a metade de cima é do país 1, a de baixo do país 2. A frente é a
    /// linha do meio, com w encostos que se seguem uns aos outros.</summary>
    private static World Grid(int w, int h, int split)
    {
        var (world, _) = TestWorld.Build();
        world.Countries[1] = new Country { Id = 1, Tag = "A", Name = "Alfa", CapitalRegionId = 1 };
        world.Countries[2] = new Country { Id = 2, Tag = "B", Name = "Beta", CapitalRegionId = w * h };
        int Id(int x, int y) => y * w + x + 1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int owner = y < split ? 1 : 2;
                var r = new Region
                {
                    Id = Id(x, y), Name = $"R{x},{y}", OwnerId = owner, InitialOwnerId = owner, ControllerId = owner,
                    Terrain = "plain", Population = 1_000_000, CenterX = x * 100, CenterY = y * 100,
                };
                if (x > 0) r.Neighbours.Add(Id(x - 1, y));
                if (x < w - 1) r.Neighbours.Add(Id(x + 1, y));
                if (y > 0) r.Neighbours.Add(Id(x, y - 1));
                if (y < h - 1) r.Neighbours.Add(Id(x, y + 1));
                world.Regions[r.Id] = r;
            }
        world.Countries[1].AtWarWith.Add(2);
        world.Countries[2].AtWarWith.Add(1);
        return world;
    }

    private static Theatre Front(World w) => Assert.Single(TheatreSystem.Of(w, 1));

    [Fact]
    public void UmaFrenteDireitaSaiNumFioSo()
    {
        var w = Grid(6, 4, 2);
        var strands = FrontLine.Strands(w, Front(w));
        var fio = Assert.Single(strands);
        Assert.Equal(6, fio.Count);                       // seis regiões encostadas, seis encostos
        // e por ordem: cada encosto encosta ao seguinte
        for (int i = 0; i + 1 < fio.Count; i++)
            Assert.True(Math.Abs(fio[i].X - fio[i + 1].X) <= 100.1f, $"salto de {fio[i].X} para {fio[i + 1].X}");
    }








}
