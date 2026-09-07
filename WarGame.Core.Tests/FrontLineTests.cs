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

    [Fact]
    public void OsEncostosApontamParaATerraDele()
    {
        var w = Grid(4, 4, 2);
        foreach (var c in FrontLine.Contacts(w, Front(w)))
        {
            Assert.Equal(1, w.Regions[c.MineId].ControllerId);
            Assert.Equal(2, w.Regions[c.FoeId].ControllerId);
            Assert.True(c.NormalY > 0.9f, "a terra dele está para baixo na grelha");
        }
    }

    [Fact]
    public void FrenteSemTropaVemMarcadaComoBuraco()
    {
        var w = Grid(3, 4, 2);
        TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 4);   // só a região do canto esquerdo da linha é guarnecida
        var cs = FrontLine.Contacts(w, Front(w));
        Assert.False(cs.Single(c => c.MineId == 4).Hole);
        Assert.True(cs.Where(c => c.MineId != 4).All(c => c.Hole));
    }

    [Fact]
    public void UmaIlhaSoltaNaoSeCoseAFrenteDoContinente()
    {
        var w = Grid(4, 4, 2);
        // duas regiões novas, longe e só vizinhas uma da outra: uma minha, outra dele
        var mine = new Region { Id = 100, Name = "Ilha", OwnerId = 1, InitialOwnerId = 1, ControllerId = 1, Terrain = "plain", CenterX = 5000, CenterY = 5000 };
        var foe = new Region { Id = 101, Name = "Ilhota", OwnerId = 2, InitialOwnerId = 2, ControllerId = 2, Terrain = "plain", CenterX = 5100, CenterY = 5000 };
        mine.Neighbours.Add(101); foe.Neighbours.Add(100);
        w.Regions[100] = mine; w.Regions[101] = foe;

        // a ilha é um teatro à parte (não toca no continente), e o continente continua num fio só
        var teatros = TheatreSystem.Of(w, 1);
        Assert.Equal(2, teatros.Count);
        foreach (var t in teatros)
        {
            var fios = FrontLine.Strands(w, t);
            Assert.Single(fios);
        }
        Assert.Single(FrontLine.Strands(w, teatros.Single(t => t.RegionIds.Contains(100)))[0]);
    }

    [Fact]
    public void UmaRegiaoEncostadaADoisLadosEntraNaLinhaDuasVezes()
    {
        var w = Grid(3, 4, 2);
        w.Regions[3].ControllerId = 2;                      // canto direito da minha fila passa para o inimigo
        var t = Front(w);
        var cs = FrontLine.Contacts(w, t);
        // a região 6 fica com inimigo por baixo (9) e por cima (3): a frente dobra-lhe a esquina
        Assert.Contains(cs, c => c.MineId == 6 && c.FoeId == 9);
        Assert.Contains(cs, c => c.MineId == 6 && c.FoeId == 3);
        Assert.Contains(cs, c => c.MineId == 2 && c.FoeId == 3);
        var fio = Assert.Single(FrontLine.Strands(w, t));
        Assert.Equal(cs.Count, fio.Count);                  // tudo cosido, saliente incluído: um fio só
    }

    [Fact]
    public void SemGuerraNaoHaLinhaNenhuma()
    {
        var w = Grid(4, 4, 2);
        w.Countries[1].AtWarWith.Clear();
        Assert.Empty(TheatreSystem.Of(w, 1));
    }

    [Fact]
    public void OsFiosNaoRepetemNemPerdemEncostos()
    {
        var w = Grid(7, 5, 3);
        var t = Front(w);
        var cs = FrontLine.Contacts(w, t);
        var fios = FrontLine.Strands(w, t);
        var vistos = fios.SelectMany(f => f).ToList();
        Assert.Equal(cs.Count, vistos.Count);
        Assert.Equal(cs.Count, vistos.Distinct().Count());
    }

    [Fact]
    public void OPontoDoEncostoFicaEntreAsDuasRegioes()
    {
        var w = Grid(4, 4, 2);
        foreach (var c in FrontLine.Contacts(w, Front(w)))
        {
            var mine = w.Regions[c.MineId]; var foe = w.Regions[c.FoeId];
            Assert.Equal((mine.CenterX + foe.CenterX) / 2f, c.X, 3);
            Assert.Equal((mine.CenterY + foe.CenterY) / 2f, c.Y, 3);
        }
    }

    [Fact]
    public void QuemDesenhaPodeAcertarOPontoAntesDeEncadear()
    {
        var w = Grid(4, 4, 2);
        // é o que o FrontOverlay faz: troca o ponto pelo da fronteira verdadeira e só depois encadeia
        var cs = FrontLine.Contacts(w, Front(w)).Select(c => c with { X = c.X + 7f }).ToList();
        var fio = Assert.Single(FrontLine.Chain(w, cs));
        Assert.Equal(cs.Count, fio.Count);
        Assert.All(fio, c => Assert.Contains(cs, x => x.MineId == c.MineId && x.FoeId == c.FoeId && x.X == c.X));
    }
}
