using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O cerco. Mapa em linha 1-2-3 (país 1) | 4-5-6 (país 2): pôr uma divisão do país 1 na região 5
/// sem controlar a 4 é uma bolsa de manual — está em terreno que o país 1 tomou, mas não há caminho de
/// terra que a ligue a casa. Os números vêm todos de w.Rule, para o teste não repetir os valores da
/// tabela.</summary>
public class PocketTests
{
    private static (World w, MsSqliteDatabase db) Made()
    {
        var (w, db) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        w.Register(new SupplySystem());     // é quem marca o corte
        w.Register(new PocketSystem());     // e é a seguir que o cerco cobra
        return (w, db);
    }

    private static World Build() => Made().w;

    private static float Grace(World w) => w.Rule("pocket_grace", 3f);
    private static int Doom(World w) => (int)w.Rule("pocket_surrender", 21f);

    /// <summary>Bolsa fechada: o país 1 controla a 5, o inimigo tem a 4 e a 6 à volta.</summary>
    private static Division Encircled(World w, int id = 1)
    {
        w.Regions[5].ControllerId = 1;
        return TestWorld.AddDivision(w, id, 1, TestWorld.Inf, 5);
    }





    /// <summary>Um aliado de facção na 6: é para lá que a bolsa rompe. Ter mais terra nossa dentro do anel
    /// não é saída nenhuma — isso é só bolsa maior —, e por isso a saída tem de ser de outra gente.</summary>
    private static void Corridor(World w, int region = 6)
    {
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = region, Manpower = 1e9f };
        w.Factions["aliados"] = new Faction("aliados", "Aliados", "", new List<int> { 1, 3 });
        w.Regions[region].ControllerId = 3;
    }



    [Fact]
    public void ABolsaFechadaAcabaEmRendicaoEOsHomensVaoParaOsCampos()
    {
        var w = Build();
        var d = Encircled(w);
        int esperados = PrisonerSystem.MenSurrendered(w, d);
        new PrisonerSystem().Bind(w);
        DivisionSurrendered? rendicao = null;
        w.Events.Subscribe<DivisionSurrendered>(e => rendicao = e);

        TestWorld.Days(w, Doom(w));
        Assert.NotNull(rendicao);
        Assert.Equal(2, rendicao!.CaptorId);              // quem fechou o anel
        Assert.Equal(1, rendicao.CountryId);
        Assert.Equal(5, rendicao.RegionId);
        Assert.False(w.Divisions.ContainsKey(1));
        Assert.Equal(esperados, w.Countries[2].Prisoners.GetValueOrDefault(1));
        Assert.Equal(2, w.Regions[5].ControllerId);      // e a praça entrega-se com ela
    }





}
