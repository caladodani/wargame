using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A oficina de aviões: a prancheta onde a fuselagem ganha peças e vira modelo do hangar.
///
/// O que aqui se guarda é o contrato inteiro: as ranhuras vêm da coluna e não de código, cada peça soma a
/// sua coluna e nada mais, uma peça na ranhura errada não conta nunca, a porta (Check) é a mesma para o
/// comando e para o ecrã, o desenho assinado passa a ser um PlaneClassDef como os da tabela, e o que o save
/// leva é a escolha — reafinar uma peça na tabela revaloriza todos os desenhos que a levam.</summary>
public class PlaneShopTests
{
    private static (World w, MsSqliteDatabase db) Build(float xp = 999f)
    {
        var (w, db) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (var c in w.Countries.Values) { c.IsPlayer = true; c.AirXp = xp; }
        return (w, db);
    }

    /// <summary>A fuselagem com mais ranhuras: é a que dá mais para provar numa prancheta só.</summary>
    private static PlaneClassDef Big(World w) =>
        PlaneShop.Chassis(w).OrderByDescending(d => PlaneShop.Slots(w, d.Id).Count).ThenBy(d => d.Sort).First();

    /// <summary>Um rascunho cheio: a primeira peça que entra em cada ranhura e que a casa já investigou.</summary>
    private static List<string> Full(World w, int pid, string chassis) =>
        PlaneShop.Slots(w, chassis).Select(s => PlaneShop.Fit(w, pid, s).FirstOrDefault()?.Id ?? "").ToList();

    [Fact]
    public void AsFuselagensEAsRanhurasVemDaTabelaENaoDeCodigo()
    {
        var (w, _) = Build();
        var chassis = PlaneShop.Chassis(w);
        Assert.NotEmpty(chassis);
        foreach (var d in chassis)
        {
            var slots = PlaneShop.Slots(w, d.Id);
            Assert.Equal(d.Slots.Split(',', StringSplitOptions.RemoveEmptyEntries).Length, slots.Count);
            Assert.All(slots, s => Assert.True(w.PlaneSlotDefs.ContainsKey(s)));
        }
        // e o que não se desenha não tem ranhuras: um modelo pronto continua a comprar-se como sempre
        Assert.Empty(PlaneShop.Slots(w, "nao_existe"));
        var feito = w.PlaneClasses.Values.FirstOrDefault(d => !d.Designable);
        if (feito is not null) Assert.Empty(PlaneShop.Slots(w, feito.Id));
    }

    [Fact]
    public void CadaPecaSomaASuaColunaEMaisNada()
    {
        var (w, _) = Build();
        var b = Big(w);
        var vazio = new PlaneDesign { Chassis = b.Id, Modules = PlaneShop.Slots(w, b.Id).Select(_ => "").ToList() };
        var nu = PlaneShop.Build(w, vazio);
        Assert.Equal(b.Cost, nu.Cost, 3);
        Assert.Equal(b.Air, nu.Air, 3);

        string slot = PlaneShop.Slots(w, b.Id)[0];
        var peca = PlaneShop.Fit(w, 1, slot).First();
        var com = new PlaneDesign { Chassis = b.Id, Modules = vazio.Modules.ToList() };
        com.Modules[0] = peca.Id;
        var cheio = PlaneShop.Build(w, com);
        Assert.Equal(b.Cost + peca.Cost, cheio.Cost, 3);
        Assert.Equal(b.Air + peca.Air, cheio.Air, 3);
        Assert.Equal(b.RangeKm + peca.RangeKm, cheio.RangeKm, 3);
        Assert.Equal(PlaneShop.ClassId(0), cheio.Id);
    }

    [Fact]
    public void PecaNaRanhuraErradaNaoContaEOChecheRecusa()
    {
        var (w, _) = Build();
        var b = Big(w);
        var slots = PlaneShop.Slots(w, b.Id);
        var estranha = w.PlaneModules.Values.First(m => m.Slot != slots[0]);
        var draft = Full(w, 1, b.Id);
        draft[0] = estranha.Id;

        var d = new PlaneDesign { Chassis = b.Id, Modules = draft };
        Assert.DoesNotContain(estranha, PlaneShop.Fitted(w, d));
        Assert.Contains("não entra", PlaneShop.Check(w, 1, b.Id, draft)!);
    }

    [Fact]
    public void RanhuraObrigatoriaVaziaNaoAssina()
    {
        var (w, _) = Build();
        var b = Big(w);
        var slots = PlaneShop.Slots(w, b.Id);
        int must = slots.FindIndex(s => w.PlaneSlotDefs[s].Required);
        Assert.True(must >= 0, "nenhuma fuselagem com ranhura obrigatória na tabela");

        var draft = Full(w, 1, b.Id);
        Assert.Null(PlaneShop.Check(w, 1, b.Id, draft));
        draft[must] = "";
        Assert.Contains("vazia", PlaneShop.Check(w, 1, b.Id, draft)!);
        Assert.Contains(PlaneShop.Advice(w, 1, b.Id, draft), n => n.Bad);
    }

    [Fact]
    public void PecaFechadaPedeInvestigacaoEAbreComEla()
    {
        var (w, _) = Build();
        var fechada = w.PlaneModules.Values.First(m => m.TechId.Length > 0);
        var c = w.Countries[1];
        c.Techs.Remove(fechada.TechId);

        Assert.NotEqual("", PlaneShop.Locked(w, 1, fechada.Id));
        Assert.DoesNotContain(fechada, PlaneShop.Fit(w, 1, fechada.Slot));

        var b = PlaneShop.Chassis(w).First(d => PlaneShop.Slots(w, d.Id).Contains(fechada.Slot));
        var draft = Full(w, 1, b.Id);
        draft[PlaneShop.Slots(w, b.Id).IndexOf(fechada.Slot)] = fechada.Id;
        Assert.Contains("precisa de", PlaneShop.Check(w, 1, b.Id, draft)!);

        c.Techs.Add(fechada.TechId);
        Assert.Equal("", PlaneShop.Locked(w, 1, fechada.Id));
        Assert.Contains(fechada, PlaneShop.Fit(w, 1, fechada.Slot));
        Assert.Null(PlaneShop.Check(w, 1, b.Id, draft));
    }

    [Fact]
    public void AssinarPoeOAviaoNoHangarEPagaHorasDeVoo()
    {
        var (w, _) = Build(xp: 40f);
        var b = Big(w);
        var draft = Full(w, 1, b.Id);
        int modelos = w.PlaneClasses.Count;

        var cmd = new DesignPlaneCommand(1, "Falcão", b.Id, draft);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Equal(modelos + 1, w.PlaneClasses.Count);
        var feito = PlaneShop.Of(w, 1).Single();
        var cls = w.PlaneClasses[PlaneShop.ClassId(feito.Id)];
        Assert.Equal("Falcão", cls.Name);
        Assert.Equal(40f - w.Rule("plane_design_xp"), w.Countries[1].AirXp, 3);
        Assert.True(cls.Cost > b.Cost);      // as peças pagam-se

        // e sem horas de voo não se assina outro
        Assert.Contains("horas de voo", new DesignPlaneCommand(1, "Outro", b.Id, draft).Validate(w)!);
    }

    [Fact]
    public void RedesenharMudaOModeloSemCriarOutro()
    {
        var (w, _) = Build();
        var b = Big(w);
        var draft = Full(w, 1, b.Id);
        new DesignPlaneCommand(1, "Falcão", b.Id, draft).Execute(w);
        var feito = PlaneShop.Of(w, 1).Single();
        string cls = PlaneShop.ClassId(feito.Id);
        float antes = w.PlaneClasses[cls].Cost;
        float xp = w.Countries[1].AirXp;

        var slots = PlaneShop.Slots(w, b.Id);
        int livre = slots.FindIndex(s => !w.PlaneSlotDefs[s].Required);
        Assert.True(livre >= 0);
        var menos = draft.ToList(); menos[livre] = "";

        var cmd = new DesignPlaneCommand(1, "Falcão II", b.Id, menos, feito.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Single(PlaneShop.Of(w, 1));
        Assert.Equal("Falcão II", w.PlaneClasses[cls].Name);
        Assert.True(w.PlaneClasses[cls].Cost < antes);
        Assert.Equal(xp - w.Rule("plane_design_edit_xp"), w.Countries[1].AirXp, 3);
        // mexer no desenho do vizinho não se faz
        Assert.Contains("desta casa", new DesignPlaneCommand(2, "Roubado", b.Id, menos, feito.Id).Validate(w)!);
    }

    [Fact]
    public void RiscarSoSaiOQueNaoAndaNoAr()
    {
        var (w, _) = Build();
        var b = Big(w);
        new DesignPlaneCommand(1, "Falcão", b.Id, Full(w, 1, b.Id)).Execute(w);
        var feito = PlaneShop.Of(w, 1).Single();
        string cls = PlaneShop.ClassId(feito.Id);

        w.Countries[1].Planes[cls] = 3f;
        Assert.Contains("asas deste desenho", new ScrapPlaneDesignCommand(1, feito.Id).Validate(w)!);

        w.Countries[1].Planes[cls] = 0f;
        var cmd = new ScrapPlaneDesignCommand(1, feito.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);
        Assert.Empty(PlaneShop.Of(w, 1));
        Assert.False(w.PlaneClasses.ContainsKey(cls));
    }

    [Fact]
    public void OTetoDeDesenhosVemDaRegra()
    {
        var (w, _) = Build();
        w.Rules["plane_design_max"] = 2f;
        var b = Big(w);
        var draft = Full(w, 1, b.Id);
        new DesignPlaneCommand(1, "Um", b.Id, draft).Execute(w);
        new DesignPlaneCommand(1, "Dois", b.Id, draft).Execute(w);
        Assert.Contains("já tem 2 desenhos", new DesignPlaneCommand(1, "Três", b.Id, draft).Validate(w)!);
        // e o teto é por casa: o vizinho tem a prancheta dele
        Assert.Null(new DesignPlaneCommand(2, "Deles", b.Id, draft).Validate(w));
    }

    /// <summary>O save leva a escolha, não os números: ao abrir, o desenho volta a ser modelo do hangar com
    /// a ficha somada de novo pela tabela do dia. É o que deixa reafinar uma peça sem tocar em save nenhum.</summary>
    [Fact]
    public void ODesenhoAtravessaOSaveEVoltaAViverComoModelo()
    {
        var (w, staticDb) = Build();
        var b = Big(w);
        var draft = Full(w, 1, b.Id);
        new DesignPlaneCommand(1, "Falcão", b.Id, draft).Execute(w);
        var feito = PlaneShop.Of(w, 1).Single();
        string cls = PlaneShop.ClassId(feito.Id);
        float custo = w.PlaneClasses[cls].Cost;

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (w2, _) = Build();
        repo.LoadSave(w2, save);
        var voltou = PlaneShop.Of(w2, 1).Single();
        Assert.Equal(feito.Id, voltou.Id);
        Assert.Equal(b.Id, voltou.Chassis);
        Assert.Equal(draft, voltou.Modules);
        Assert.True(w2.PlaneClasses.ContainsKey(cls));
        Assert.Equal(custo, w2.PlaneClasses[cls].Cost, 3);
        Assert.Equal("Falcão", w2.PlaneClasses[cls].Name);
    }
}
