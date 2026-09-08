using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O chão desenhado no mapa político (Relief): que regiões levam sinal, qual, e quantos. A regra é
/// a do atlas — só se marca o que custa a atravessar, e o desenho é o da linha da tabela e não outro.</summary>
public class ReliefTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return w;
    }

    /// <summary>A planície é o normal, e o normal não leva sinal: um mapa todo carimbado não se lê.</summary>
    [Fact]
    public void A_planicie_nao_leva_sinal()
    {
        var w = Setup();
        Ground(w, 1, "plain");
        Assert.Null(Relief.Mark(w, w.Regions[1]));
        Assert.Equal("", Relief.Colour(w, w.Regions[1]));
    }

    /// <summary>O que custa a atravessar leva o desenho e a cor da sua linha da tabela — os mesmos da chave
    /// do modo Terreno. Um chão só pode ter uma cara.</summary>
    [Fact]
    public void O_chao_duro_leva_o_desenho_da_sua_linha()
    {
        var w = Setup();
        Ground(w, 1, "mountain");
        var def = w.TerrainDefs["mountain"];
        Assert.Equal(def.Glyph, Relief.Mark(w, w.Regions[1]));
        Assert.Equal(def.Color, Relief.Colour(w, w.Regions[1]));

        var k = MapModes.Of(w, w.Regions[1], "terrain");
        Assert.NotNull(k);
        Assert.Equal(k!.Value.Glyph, Relief.Mark(w, w.Regions[1]));   // mapa e chave dizem o mesmo
    }

    /// <summary>Chão que a tabela não conhece não inventa desenho nenhum.</summary>
    [Fact]
    public void Chao_desconhecido_nao_inventa_sinal()
    {
        var w = Setup();
        Ground(w, 1, "lua");
        Assert.Null(Relief.Mark(w, w.Regions[1]));
        Assert.Null(Relief.Of(w, w.Regions[1]));
    }

    /// <summary>Uma província grande leva mais sinais do que uma pequena — senão fica um símbolo perdido no
    /// meio do branco — mas nunca menos de um.</summary>
    [Fact]
    public void Provincia_grande_leva_mais_sinais()
    {
        Assert.Equal(1, Relief.Marks(40f));
        Assert.True(Relief.Marks(150f) > Relief.Marks(40f));
        Assert.True(Relief.Marks(400f) > Relief.Marks(150f));
    }

    /// <summary>O recenseamento traz só o chão marcado, do mais fácil de atravessar ao mais duro, e a conta
    /// bate certo com as regiões que têm sinal.</summary>
    [Fact]
    public void O_recenseamento_conta_so_o_chao_marcado()
    {
        var w = Setup();
        foreach (int id in w.Regions.Keys) Ground(w, id, "plain");
        Assert.Empty(Relief.Census(w));

        Ground(w, 1, "mountain"); Ground(w, 2, "forest"); Ground(w, 3, "forest");
        var census = Relief.Census(w);
        Assert.Equal(2, census.Count);
        Assert.Equal("forest", census[0].Ground.Id);          // floresta custa menos do que montanha
        Assert.Equal(2, census[0].Regions);
        Assert.Equal("mountain", census[1].Ground.Id);
        Assert.Equal(1, census[1].Regions);
        Assert.Equal(w.Regions.Values.Count(r => Relief.Mark(w, r) is not null), census.Sum(c => c.Regions));
    }

    /// <summary>Muda o chão de uma região (Region.Terrain é init: refaz-se a região).</summary>
    private static void Ground(World w, int id, string terrain)
    {
        var r = w.Regions[id];
        var copy = new Region
        {
            Id = r.Id, Name = r.Name, OwnerId = r.OwnerId, InitialOwnerId = r.InitialOwnerId,
            ControllerId = r.ControllerId, Terrain = terrain, River = r.River, Population = r.Population,
            CenterX = r.CenterX, CenterY = r.CenterY,
        };
        foreach (int n in r.Neighbours) copy.Neighbours.Add(n);
        w.Regions[id] = copy;
    }
}
