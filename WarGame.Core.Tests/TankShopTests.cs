using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A prancheta dos carros: desenhar um carro de combate à peça (HoI4: tank designer).
///
/// O exército era o único braço sem prancheta. A ladeira das marcas de material subia igual para toda a
/// gente — quatro gerações da tabela, as mesmas em todo o mundo — e a única decisão do jogador era quando
/// investigar. O que a prancheta traz é o COMO: o país desenha a geração seguinte com as mãos dele.
///
/// O que estes testes guardam: que os cascos, as ranhuras e as peças vêm da tabela e não do código; que cada
/// peça só soma a sua coluna e só na ranhura certa; que a ranhura obrigatória vazia e a peça por investigar
/// travam a assinatura; que assinar cria uma marca de material com dono e paga experiência de exército; que
/// a marca de casa não se vê da casa do vizinho; que a aresta (tank_design_edge) recusa um carro que não
/// bata o que a fábrica já faz; que redesenhar não abre outra ladeira; que não se risca o que a fábrica está
/// a fazer; e que o desenho atravessa o save e volta a viver como marca.</summary>
public class TankShopTests
{
    /// <summary>Linha do costume com experiência de exército de sobra: o que se mede aqui é a prancheta, não
    /// a falta de experiência.</summary>
    private static World Build(float xp = 999f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (var c in w.Countries.Values) { c.Money = 5000f; c.IsPlayer = true; c.ArmyXp = xp; }
        return w;
    }

    /// <summary>O casco aberto com mais ranhuras: é nele que se vê a prancheta toda.</summary>
    private static TankChassisDef Big(World w, int countryId = 1) =>
        TankShop.Chassis(w).Where(d => TankShop.ChassisLocked(w, countryId, d.Id).Length == 0)
            .OrderByDescending(d => TankShop.Slots(w, d.Id).Count).ThenBy(d => d.Sort).First();

    /// <summary>Enche todas as ranhuras deste casco com a peça mais cara que o país já tem.</summary>
    private static List<string> Full(World w, int countryId, string chassis) =>
        TankShop.Slots(w, chassis)
            .Select(s => TankShop.Fit(w, countryId, s).OrderByDescending(m => m.Cost).ThenBy(m => m.Sort)
                          .FirstOrDefault()?.Id ?? "")
            .ToList();

    /// <summary>Os cascos e as ranhuras são linhas da tabela: tira-se a coluna slots e o casco deixa de se
    /// desenhar, sem tocar numa linha de código.</summary>
    [Fact]
    public void OsCascosEAsRanhurasVemDaTabelaENaoDeCodigo()
    {
        var w = Build();
        Assert.NotEmpty(TankShop.Chassis(w));
        Assert.All(TankShop.Chassis(w), d => Assert.NotEmpty(TankShop.Slots(w, d.Id)));
        Assert.NotEmpty(w.TankModules);
        Assert.All(w.TankModules.Values, m => Assert.True(w.TankSlotDefs.ContainsKey(m.Slot)));

        var one = Big(w);
        w.TankChassis[one.Id] = one with { Slots = "" };
        Assert.Empty(TankShop.Slots(w, one.Id));
        Assert.DoesNotContain(TankShop.Chassis(w), d => d.Id == one.Id);
    }

    /// <summary>Cada peça soma a sua coluna ao casco e mais nada: a ficha do carro é a base mais as peças, e
    /// são estas três colunas que a ladeira das marcas usa.</summary>
    [Fact]
    public void CadaPecaSomaASuaColunaEMaisNada()
    {
        var w = Build();
        var hull = Big(w);
        var mods = Full(w, 1, hull.Id);
        var made = TankShop.Build(w, new TankDesign { Id = 1, CountryId = 1, Chassis = hull.Id, Modules = mods });

        var fitted = TankShop.Fitted(w, new TankDesign { Chassis = hull.Id, Modules = mods });
        Assert.NotEmpty(fitted);
        Assert.Equal(hull.Cost + fitted.Sum(m => m.Cost), made.Cost, 3);
        Assert.Equal(hull.Power + fitted.Sum(m => m.Power), made.Power, 3);
        Assert.Equal(hull.Wear + fitted.Sum(m => m.Wear), made.Wear, 3);
        Assert.Equal(TankShop.MarkId(1), made.Id);
        Assert.Equal(hull.UnitTypeId, made.UnitTypeId);
        Assert.Equal(1, made.OwnerId);                      // é da casa que o desenhou, e de mais ninguém
    }

    /// <summary>Peça na ranhura errada não conta e a prancheta recusa: uma tabela mexida ou um save velho não
    /// podem dar números falsos.</summary>
    [Fact]
    public void PecaNaRanhuraErradaNaoContaEAPranchetaRecusa()
    {
        var w = Build();
        var hull = Big(w);
        var slots = TankShop.Slots(w, hull.Id);
        var estranha = w.TankModules.Values.First(m => m.Slot != slots[0]);
        var mods = Enumerable.Repeat("", slots.Count).ToList();
        mods[0] = estranha.Id;

        Assert.Empty(TankShop.Fitted(w, new TankDesign { Chassis = hull.Id, Modules = mods }));
        Assert.Contains("não entra", TankShop.Check(w, 1, hull.Id, mods) ?? "");
    }

    /// <summary>Sem canhão é um tractor com chapa: a ranhura obrigatória vazia não assina, e o conselho da
    /// margem di-lo antes de se carregar no botão.</summary>
    [Fact]
    public void RanhuraObrigatoriaVaziaNaoAssina()
    {
        var w = Build();
        var hull = Big(w);
        var slots = TankShop.Slots(w, hull.Id);
        var mods = Full(w, 1, hull.Id);
        int req = slots.FindIndex(s => w.TankSlotDefs.TryGetValue(s, out var d) && d.Required);
        Assert.True(req >= 0);

        Assert.Null(TankShop.Check(w, 1, hull.Id, mods));
        mods[req] = "";
        Assert.Contains("vazia", TankShop.Check(w, 1, hull.Id, mods) ?? "");
        Assert.Contains(TankShop.Advice(w, 1, hull.Id, mods), n => n.Bad);
    }

    /// <summary>Peça fechada pede investigação e abre com ela: a prancheta mostra-a apagada em vez de a
    /// esconder, e é o Locked que diz o que falta.</summary>
    [Fact]
    public void PecaFechadaPedeInvestigacaoEAbreComEla()
    {
        var w = Build();
        var locked = w.TankModules.Values.First(m => m.TechId.Length > 0);
        var hull = TankShop.Chassis(w).First(d => TankShop.ChassisLocked(w, 1, d.Id).Length == 0
                                                  && TankShop.Slots(w, d.Id).Contains(locked.Slot));
        var slots = TankShop.Slots(w, hull.Id);
        var mods = Full(w, 1, hull.Id);
        mods[slots.IndexOf(locked.Slot)] = locked.Id;

        Assert.NotEmpty(TankShop.Locked(w, 1, locked.Id));
        Assert.Contains("precisa de", TankShop.Check(w, 1, hull.Id, mods) ?? "");
        Assert.DoesNotContain(TankShop.Fit(w, 1, locked.Slot), m => m.Id == locked.Id);

        w.Countries[1].Techs.Add(locked.TechId);
        Assert.Empty(TankShop.Locked(w, 1, locked.Id));
        Assert.Contains(TankShop.Fit(w, 1, locked.Slot), m => m.Id == locked.Id);
    }

    /// <summary>Assinar põe o carro na ladeira das marcas como geração seguinte e paga experiência de
    /// exército. É esta a ideia toda da prancheta: o resto do jogo não sabe que aquilo foi desenhado em casa.</summary>
    [Fact]
    public void AssinarPoeOCarroNaLadeiraEPagaExperiencia()
    {
        var w = Build(200f);
        var hull = Big(w);
        var mods = Full(w, 1, hull.Id);
        float price = TankShop.Price(w, false);
        int next = TankShop.NextMark(w, hull.UnitTypeId);

        Assert.Null(new DesignTankCommand(1, "Lince", hull.Id, mods).Validate(w));
        new DesignTankCommand(1, "Lince", hull.Id, mods).Execute(w);

        var design = Assert.Single(TankShop.Of(w, 1));
        var mark = w.EquipmentMarks[TankShop.MarkId(design.Id)];
        Assert.Equal("Lince", mark.Name);
        Assert.Equal(next, mark.Mark);
        Assert.Equal(200f - price, w.Countries[1].ArmyXp, 3);

        // e o resto do jogo trata-a como marca da tabela: é a melhor que a casa sabe fazer
        Assert.Equal(mark.Mark, Marks.Open(w, w.Countries[1], hull.UnitTypeId), 3);
        Assert.Equal(mark.Power, Marks.Power(w, hull.UnitTypeId, mark.Mark, w.Countries[1]), 3);
    }

    /// <summary>O carro de casa não se vê da casa do vizinho: a marca tem dono, e sem isso o desenho de um
    /// país aparecia na ladeira de toda a gente.</summary>
    [Fact]
    public void OCarroDeCasaNaoSeVeDaCasaDoVizinho()
    {
        var w = Build();
        var hull = Big(w);
        new DesignTankCommand(1, "Lince", hull.Id, Full(w, 1, hull.Id)).Execute(w);
        string id = TankShop.MarkId(TankShop.Of(w, 1)[0].Id);
        var mark = w.EquipmentMarks[id];
        foreach (var t in w.Techs.Keys) w.Countries[2].Techs.Add(t);   // o vizinho investigou tudo o que há

        Assert.Contains(Marks.All(w, hull.UnitTypeId, w.Countries[1]), m => m.Id == id);
        Assert.DoesNotContain(Marks.All(w, hull.UnitTypeId, w.Countries[2]), m => m.Id == id);
        Assert.DoesNotContain(Marks.All(w, hull.UnitTypeId), m => m.Id == id);
        Assert.True(Marks.Open(w, w.Countries[2], hull.UnitTypeId) < mark.Mark);
    }

    /// <summary>A aresta: um carro que não bata o que a fábrica já faz não se assina. Sem ela a ladeira das
    /// marcas deixava de subir e a tropa acordava com material pior do que o que lhe prometeram.</summary>
    [Fact]
    public void NaoSeAssinaOCarroQueNaoBateAFabrica()
    {
        var w = Build();
        var hull = Big(w);
        var mods = Full(w, 1, hull.Id);
        Assert.Null(TankShop.Check(w, 1, hull.Id, mods));

        w.Rules["tank_design_edge"] = 9f;                   // a fábrica de hoje passa a pedir nove vezes mais
        Assert.True(TankShop.Bar(w, 1, hull.UnitTypeId) > 0f);
        Assert.Contains("Não bate", TankShop.Check(w, 1, hull.Id, mods) ?? "");
        Assert.Contains(TankShop.Advice(w, 1, hull.Id, mods), n => n.Bad);
        Assert.Contains("Não bate", new DesignTankCommand(1, "Lince", hull.Id, mods).Validate(w) ?? "");
    }

    /// <summary>Redesenhar mexe no carro que lá está sem abrir outra ladeira: a marca é a mesma, o número do
    /// desenho é o mesmo, e a ficha desce com a peça que saiu.</summary>
    [Fact]
    public void RedesenharMantemAMesmaMarca()
    {
        var w = Build();
        var hull = Big(w);
        var slots = TankShop.Slots(w, hull.Id);
        var mods = Full(w, 1, hull.Id);
        new DesignTankCommand(1, "Lince", hull.Id, mods).Execute(w);
        var design = Assert.Single(TankShop.Of(w, 1));
        string id = TankShop.MarkId(design.Id);
        float before = w.EquipmentMarks[id].Cost;
        int mark = w.EquipmentMarks[id].Mark;

        // com um carro de casa nesta ladeira, um segundo não se assina: mexe-se nesse
        Assert.Contains("Já há um carro de casa", new DesignTankCommand(1, "Outro", hull.Id, mods).Validate(w) ?? "");

        int drop = slots.FindIndex(s => !(w.TankSlotDefs.TryGetValue(s, out var d) && d.Required));
        mods[drop] = "";
        Assert.Null(new DesignTankCommand(1, "Lince II", hull.Id, mods, design.Id).Validate(w));
        new DesignTankCommand(1, "Lince II", hull.Id, mods, design.Id).Execute(w);

        Assert.Single(TankShop.Of(w, 1));                   // continua a ser um desenho só
        Assert.Equal(mark, w.EquipmentMarks[id].Mark);      // e a mesma geração da ladeira
        Assert.True(w.EquipmentMarks[id].Cost < before);    // com a ficha a descer com a peça que saiu
    }

    /// <summary>Riscar: só sai o carro que a fábrica não está a fazer. O material já feito não desaparece —
    /// fica no armazém e volta a valer a última marca da tabela.</summary>
    [Fact]
    public void RiscarSoSaiOQueAFabricaNaoEstaAFazer()
    {
        var w = Build();
        var hull = Big(w);
        new DesignTankCommand(1, "Lince", hull.Id, Full(w, 1, hull.Id)).Execute(w);
        var design = Assert.Single(TankShop.Of(w, 1));
        var mark = w.EquipmentMarks[TankShop.MarkId(design.Id)];

        w.Countries[1].Queue.Add(new ProductionOrder { UnitTypeId = hull.UnitTypeId, Mark = mark.Mark });
        Assert.Contains("ainda está a fazer", new ScrapTankDesignCommand(1, design.Id).Validate(w) ?? "");

        w.Countries[1].Queue.Clear();
        Assert.Null(new ScrapTankDesignCommand(1, design.Id).Validate(w));
        new ScrapTankDesignCommand(1, design.Id).Execute(w);
        Assert.Empty(TankShop.Of(w, 1));
        Assert.False(w.EquipmentMarks.ContainsKey(TankShop.MarkId(design.Id)));
    }

    /// <summary>O desenho atravessa o save e volta a viver como marca: grava-se a ESCOLHA e os números voltam
    /// a sair da tabela, para uma peça reafinada valer logo em todos os carros que a levam.</summary>
    [Fact]
    public void ODesenhoAtravessaOSaveEVoltaAViverComoMarca()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (var c in w.Countries.Values) { c.Money = 5000f; c.ArmyXp = 500f; }
        var hull = Big(w);
        var mods = Full(w, 1, hull.Id);
        new DesignTankCommand(1, "Lince", hull.Id, mods).Execute(w);
        var design = Assert.Single(TankShop.Of(w, 1));
        string id = TankShop.MarkId(design.Id);
        float power = w.EquipmentMarks[id].Power;

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (back, _) = TestWorld.Build();
        TestWorld.LinearMap(back);
        repo.LoadSave(back, save);

        var again = Assert.Single(TankShop.Of(back, 1));
        Assert.Equal("Lince", again.Name);
        Assert.Equal(hull.Id, again.Chassis);
        Assert.Equal(mods, again.Modules);
        Assert.True(back.EquipmentMarks.ContainsKey(id));
        Assert.Equal(power, back.EquipmentMarks[id].Power, 3);
        Assert.Equal(1, back.EquipmentMarks[id].OwnerId);
    }
}
