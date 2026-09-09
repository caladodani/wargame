using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A aviação por modelos de avião (plane_class): o céu deixa de ser um número e passa a ser uma
/// composição. O caça ganha o céu e salva quem vai com ele, o bombardeiro deita abaixo o que sustenta a
/// guerra e não se defende de nada, o transporte não faz guerra nenhuma e é o único que larga
/// pára-quedistas — e um mundo sem modelos nenhuns luta exactamente como dantes.
///
/// Mapa: a mesma linha 1-2-3 (país 1) | 4-5-6 (país 2) dos outros testes do ar.</summary>
public class AirClassTests
{
    private static World Build(float money = 5000f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.StartWar(1, 2);
        // países de mão humana: a IA aérea tem testes só para ela e não anda a engrossar estes
        foreach (var c in w.Countries.Values) { c.Money = money; c.IsPlayer = true; }
        return w;
    }

    [Fact]
    public void OsModelosVemDaBaseDeDados()
    {
        var (w, _) = TestWorld.Build();
        Assert.True(w.PlaneClasses.Count >= 8);
        var carga = w.PlaneClasses["transporte"];
        Assert.Equal(0f, carga.Support, 3);                                 // não bate em ninguém...
        Assert.Equal(0f, carga.Bombing, 3);
        Assert.True(carga.Transport > 0f);                                  // ...e é o único que leva gente
        Assert.True(w.PlaneClasses["caca_pesado"].Air > w.PlaneClasses["caca"].Air);
        Assert.True(w.PlaneClasses["estrategico"].Bombing > w.PlaneClasses["bombardeiro"].Bombing);
        Assert.True(w.PlaneClasses["estrategico"].Air < w.PlaneClasses["caca_leve"].Air);
        Assert.Equal("caca", Air.Basic(w));                                 // é o que o botão de sempre compra
    }

    [Fact]
    public void AAsaCustaOQueOSeuModeloVale()
    {
        var w = Build(money: 1000f);
        float before = w.Countries[1].Money;
        Assert.Null(new BuyPlaneCommand(1, "caca_pesado").Validate(w));
        new BuyPlaneCommand(1, "caca_pesado").Execute(w);
        Assert.Equal(before - w.Rule("air_wing_cost") * w.PlaneClasses["caca_pesado"].Cost, w.Countries[1].Money, 2);
        Assert.Equal(1f, w.Countries[1].Planes["caca_pesado"], 3);
        Assert.Equal(1f, w.Countries[1].AirPower, 3);
        Assert.Contains("desconhecido", new BuyPlaneCommand(1, "caca_furtivo").Validate(w)!);
    }

    [Fact]
    public void AAsaLevaOsMelhoresAvioesParaATarefa()
    {
        var w = Build();
        var c = w.Countries[1];
        c.Planes["caca_pesado"] = 3f; c.Planes["estrategico"] = 3f;
        AirMissionSystem.Assign(w, 1, 4, "superioridade", 3f);              // varrer o céu pede caças
        var sky = Assert.Single(w.AirMissions);
        Assert.Equal(3f, sky.Squadron["caca_pesado"], 3);
        Assert.False(sky.Squadron.ContainsKey("estrategico"));

        AirMissionSystem.Assign(w, 1, 5, "bombardeamento", 3f);             // e bombardear pede bombardeiros
        var bomb = w.AirMissions.Single(m => m.RegionId == 5);
        Assert.Equal(3f, bomb.Squadron["estrategico"], 3);
    }

    [Fact]
    public void OCampoNaoDaOMesmoAviaoADuasMissoes()
    {
        var w = Build();
        w.Countries[1].Planes["caca"] = 4f;
        AirMissionSystem.Assign(w, 1, 4, "superioridade", 4f);
        AirMissionSystem.Assign(w, 1, 5, "superioridade", 4f);              // já não há caças livres nenhuns
        Assert.Equal(0f, Air.Free(w, 1, "caca"), 3);
        Assert.Equal(4f, w.AirMissions.Sum(m => m.Wings), 3);
    }

    [Fact]
    public void OCacaNaoServeDeTransporte()
    {
        var w = Build();
        var c = w.Countries[1];
        c.Planes["caca"] = 10f;
        Assert.Equal(10f, AirMissionSystem.Free(w, 1), 3);                  // dez asas em casa...
        Assert.Equal(0f, AirMissionSystem.Free(w, 1, "transport"), 3);      // ...e nenhuma que largue um homem
        c.Planes["transporte"] = 4f;
        Assert.Equal(4f, AirMissionSystem.Free(w, 1, "transport"), 3);
    }

    [Fact]
    public void CaiPrimeiroQuemNaoSabeLutarNoCeu()
    {
        var w = Build();
        var squadron = new Dictionary<string, float> { ["caca_pesado"] = 5f, ["estrategico"] = 5f };
        var gone = Air.Down(w, squadron, 4f);
        Assert.True(gone["estrategico"] > gone["caca_pesado"],
                    "o bombardeiro tinha de cair primeiro do que o caça que vai com ele");
        Assert.Equal(6f, squadron.Values.Sum(), 2);                         // abateram-se 4 das 10
    }

    [Fact]
    public void SemCacaOBombardeiroLevaTudo()
    {
        var w = Build();
        var squadron = new Dictionary<string, float> { ["estrategico"] = 5f };
        var gone = Air.Down(w, squadron, 2f);
        Assert.Equal(2f, gone["estrategico"], 2);
        Assert.Equal(3f, squadron["estrategico"], 2);
    }

    [Fact]
    public void OCeuMelhorPerdeMenosNoMesmoCombate()
    {
        var w = Build();
        w.Countries[1].Planes["caca_pesado"] = 4f;                          // feito para ganhar o céu
        w.Countries[2].Planes["estrategico"] = 4f;                          // caro e indefeso lá em cima
        AirMissionSystem.Assign(w, 1, 4, "superioridade", 4f);
        AirMissionSystem.Assign(w, 2, 4, "superioridade", 4f);
        w.Register(new AirMissionSystem());
        w.Tick();
        float lost1 = 4f - w.Countries[1].AirPower, lost2 = 4f - w.Countries[2].AirPower;
        Assert.True(lost2 > lost1, $"a aviação pior tinha de perder mais ({lost2:0.###} contra {lost1:0.###})");
    }

    [Fact]
    public void AsAsasSemModeloGanhamModeloNoHangar()
    {
        var w = Build();
        w.Countries[1].AirPower = 20f;                                      // aviação de partida: asas sem modelo
        Assert.Equal(20f, w.Countries[1].Planes[""], 3);
        w.Register(new AirMissionSystem());
        w.Tick();
        Assert.False(w.Countries[1].Planes.ContainsKey(""), "o hangar tinha de dar modelo às asas soltas");
        Assert.Equal(20f, w.Countries[1].AirPower, 2);                      // sem inventar nem perder aviões
        Assert.True(w.Countries[1].Planes.Count >= 2, "a aviação de partida tem caça e quem bate no chão");
    }

    [Fact]
    public void OQueSeCompraSaiDaTabelaENaoDeUmaListaEscrita()
    {
        var w = Build();
        Assert.Equal("caca_pesado", Air.Choose(w, 10_000f, "air"));         // cofre à vontade: o melhor caça
        Assert.Equal("transporte", Air.Choose(w, 10_000f, "transport"));
        Assert.Equal("", Air.Choose(w, 1f, "air"));                         // sem dinheiro não se compra nada
        w.PlaneClasses.Clear();
        Assert.Equal("", Air.Choose(w, 10_000f, "air"));
    }

    [Fact]
    public void UmMundoSemModelosLutaComoDantes()
    {
        var w = Build();
        w.PlaneClasses.Clear();                                             // sem tabela, sem modelos: o jogo antigo
        w.Countries[1].AirPower = 5f;
        AirMissionSystem.Assign(w, 1, 4, "bombardeamento", 5f);
        var m = Assert.Single(w.AirMissions);
        Assert.Equal(5f, m.Squadron[""], 3);
        Assert.Equal(5f, Air.Power(w, m.Squadron), 3);                      // sem modelo, cada asa vale 1
        Assert.Equal("Esquadrão ×5", Air.Describe(w, m.Squadron));
        w.Countries[2].AirPower = 5f;
        Assert.Equal(5f, AirMissionSystem.Free(w, 2, "transport"), 3);      // e serve para tudo, como dantes
    }
}
