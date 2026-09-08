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

    [Fact]
    public void EmCasaNaoHaCercoNenhum()
    {
        var w = Build();
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        TestWorld.Days(w, 10);
        Assert.False(d.Cut);
        Assert.Equal(0, d.PocketDays);
        Assert.Equal(100f, d.Hp);
    }

    [Fact]
    public void OsPrimeirosDiasDeCercoSaoDeRespiro()
    {
        var w = Build();
        var d = Encircled(w);
        TestWorld.Days(w, (int)Grace(w));
        Assert.True(d.Cut);
        Assert.Equal((int)Grace(w), d.PocketDays);
        Assert.Equal(100f, d.Hp);                       // ainda não se paga nada
        Assert.Equal(100f, d.Org);
    }

    [Fact]
    public void PassadoORespiroABolsaComeAquiloQueLaEsta()
    {
        var w = Build();
        var d = Encircled(w);
        int dias = (int)Grace(w) + 3;
        TestWorld.Days(w, dias);
        float pago = 3f;                                 // três dias para lá do respiro
        Assert.Equal(100f - pago * w.Rule("pocket_attrition", 4f), d.Hp, 3);
        Assert.Equal(100f - pago * w.Rule("pocket_org", 8f), d.Org, 3);
    }

    [Fact]
    public void AbrirOCorredorZeraOContadorEParaAConta()
    {
        var w = Build();
        var d = Encircled(w);
        TestWorld.Days(w, (int)Grace(w) + 2);
        float feridos = d.Hp;
        Assert.True(feridos < 100f);

        w.Regions[4].ControllerId = 1;                   // corredor aberto: a cadeia até casa voltou
        TestWorld.Days(w, 5);
        Assert.False(d.Cut);
        Assert.Equal(0, d.PocketDays);
        Assert.Equal(feridos, d.Hp, 3);                  // o cerco deixa de cobrar no dia em que se abre
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
    public void ComSaidaPelosNossosNaoSeRendeMesmoCortada()
    {
        var w = Build();
        // a 5 é nossa e continua sem cadeia até casa (a 4 é do inimigo), mas ao lado está um aliado
        Corridor(w);
        var d = Encircled(w);
        TestWorld.Days(w, Doom(w) + 5);
        Assert.True(d.Cut);
        Assert.Null(PocketSystem.DaysToSurrender(w, d));
        Assert.True(w.Divisions.ContainsKey(1));         // definha, mas não baixa as armas
    }

    /// <summary>O caldeirão a sério: duas regiões nossas fechadas dentro do anel, uma divisão em cada. Cada
    /// uma tem a outra ao lado — e era isso que antes as salvava a todas, porque a pergunta "tenho vizinho
    /// amigo?" era feita divisão a divisão. Agora quem responde é a bolsa, e a bolsa capitula inteira.</summary>
    [Fact]
    public void OCaldeiraoDeVariasRegioesCapitulaTodoDeUmaVez()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, 8, 3);
        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        w.Register(new SupplySystem()); w.Register(new PocketSystem());
        w.Regions[6].ControllerId = 1; w.Regions[7].ControllerId = 1;   // tomámos a 6 e a 7 e ficámos lá
        var a = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 6);
        var b = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 7);

        TestWorld.Days(w, 1);
        Assert.True(a.Cut); Assert.True(b.Cut);
        Assert.Equal(Doom(w) - 1, PocketSystem.DaysToSurrender(w, a));

        TestWorld.Days(w, Doom(w));
        Assert.False(w.Divisions.ContainsKey(1));
        Assert.False(w.Divisions.ContainsKey(2));
        Assert.Equal(2, w.Regions[6].ControllerId);      // e as duas praças com elas
        Assert.Equal(2, w.Regions[7].ControllerId);
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

    [Fact]
    public void OContadorDizQuantosDiasFaltamAteAsArmasBaixarem()
    {
        var w = Build();
        var d = Encircled(w);
        TestWorld.Days(w, 4);
        Assert.Equal(Doom(w) - 4, PocketSystem.DaysToSurrender(w, d));
    }

    [Fact]
    public void SemAnelNinguemAceitaARendicao()
    {
        var w = Build();
        var d = Encircled(w);
        w.Countries[1].AtWarWith.Clear(); w.Countries[2].AtWarWith.Clear();   // a guerra acabou, o cerco não
        TestWorld.Days(w, Doom(w) + 3);
        Assert.True(w.Divisions.ContainsKey(1));          // não há a quem entregar as armas
        Assert.Null(PocketSystem.Ring(w, d));
    }

    [Fact]
    public void ACercadaMorreDeExaustaoAntesDoPrazoSeALaJaEstavaGasta()
    {
        var w = Build();
        var d = Encircled(w);
        d.Hp = w.Rule("pocket_attrition", 4f);            // um dia de cerco é quanto lhe resta
        TestWorld.Days(w, (int)Grace(w) + 1);
        Assert.False(w.Divisions.ContainsKey(1));
    }

    [Fact]
    public void ALinhaDeAvisosPoeOCercoAntesDaFronteiraAberta()
    {
        var w = Build();
        Encircled(w);
        TestWorld.Days(w, (int)Grace(w) + 1);
        var avisos = Alerts.For(w, 1);
        var cerco = Assert.Single(avisos, a => a.Id == "pocket");
        Assert.Equal(AlertLevel.Danger, cerco.Level);
        Assert.Equal(5, cerco.RegionId);
        Assert.Contains("rende-se", cerco.Text);
    }

    [Fact]
    public void OsDiasDeCercoSobrevivemAoSaveEAoLoad()
    {
        var (w, staticDb) = Made();
        var d = Encircled(w);
        TestWorld.Days(w, (int)Grace(w) + 2);
        Assert.True(d.PocketDays > 0);

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        repo.LoadSave(w2, save);
        Assert.Equal(d.PocketDays, w2.Divisions[1].PocketDays);
    }
}
