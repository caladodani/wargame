using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>O estaleiro: desenhar um navio à peça (HoI4: ship designer).
///
/// A marinha tinha classes desde que deixou de ser um número, mas eram sete para toda a gente: um país com
/// trinta anos de investigação naval navegava na mesma fragata do vizinho que nunca investiu nada, e a única
/// decisão do mar era quantos comprar.
///
/// O que estes testes guardam: que o casco e as ranhuras vêm da tabela e não do código, que cada peça só
/// soma a sua coluna e só na ranhura certa, que a ranhura obrigatória e a peça por investigar travam a
/// assinatura, que assinar põe o casco no estaleiro e paga milhas, que redesenhar não cria outra classe,
/// que só se risca o que não anda no mar, e que o desenho atravessa o save e volta a viver como classe.</summary>
public class ShipShopTests
{
    /// <summary>Linha do costume com o cofre cheio e milhas de sobra: o que se mede aqui é a prancheta, não
    /// a falta de experiência naval.</summary>
    private static World Build(float xp = 999f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (var c in w.Countries.Values) { c.Money = 5000f; c.IsPlayer = true; c.NavyXp = xp; }
        return w;
    }

    /// <summary>O casco com mais ranhuras: é nele que se vê a prancheta toda.</summary>
    private static ShipClassDef Big(World w) =>
        ShipShop.Chassis(w).OrderByDescending(d => ShipShop.Slots(w, d.Id).Count).ThenBy(d => d.Sort).First();

    /// <summary>Enche todas as ranhuras deste casco com a peça mais cara que o país já tem.</summary>
    private static List<string> Full(World w, int countryId, string chassis) =>
        ShipShop.Slots(w, chassis)
            .Select(s => ShipShop.Fit(w, countryId, s).OrderByDescending(m => m.Cost).ThenBy(m => m.Sort)
                          .FirstOrDefault()?.Id ?? "")
            .ToList();

    /// <summary>Os cascos e as ranhuras são linhas da tabela: tira-se a coluna slots e o casco deixa de se
    /// desenhar, sem tocar numa linha de código.</summary>
    [Fact]
    public void OsCascosEAsRanhurasVemDaTabelaENaoDeCodigo()
    {
        var w = Build();
        Assert.NotEmpty(ShipShop.Chassis(w));
        Assert.All(ShipShop.Chassis(w), d => Assert.NotEmpty(ShipShop.Slots(w, d.Id)));

        var one = Big(w);
        w.ShipClasses[one.Id] = one with { Slots = "" };
        Assert.Empty(ShipShop.Slots(w, one.Id));
        Assert.DoesNotContain(ShipShop.Chassis(w), d => d.Id == one.Id);
    }

    /// <summary>Cada peça soma a sua coluna ao casco e mais nada: a ficha do desenho é a base mais as peças.</summary>
    [Fact]
    public void CadaPecaSomaASuaColunaEMaisNada()
    {
        var w = Build();
        var hull = Big(w);
        var mods = Full(w, 1, hull.Id);
        var made = ShipShop.Build(w, new ShipDesign { Id = 1, Chassis = hull.Id, Modules = mods });

        var fitted = ShipShop.Fitted(w, new ShipDesign { Chassis = hull.Id, Modules = mods });
        Assert.NotEmpty(fitted);
        Assert.Equal(hull.Battle + fitted.Sum(m => m.Battle), made.Battle, 3);
        Assert.Equal(hull.Asw + fitted.Sum(m => m.Asw), made.Asw, 3);
        Assert.Equal(hull.Cost + fitted.Sum(m => m.Cost), made.Cost, 3);
        Assert.Equal(ShipShop.ClassId(1), made.Id);
        Assert.False(made.Basic);                       // um desenho nunca é o casco que o botão antigo compra
    }

    /// <summary>Peça na ranhura errada não conta e o estaleiro recusa: uma tabela mexida ou um save velho
    /// não podem dar números falsos.</summary>
    [Fact]
    public void PecaNaRanhuraErradaNaoContaEOEstaleiroRecusa()
    {
        var w = Build();
        var hull = Big(w);
        var slots = ShipShop.Slots(w, hull.Id);
        var estranha = w.ShipModules.Values.First(m => m.Slot != slots[0]);
        var mods = Enumerable.Repeat("", slots.Count).ToList();
        mods[0] = estranha.Id;

        Assert.Empty(ShipShop.Fitted(w, new ShipDesign { Chassis = hull.Id, Modules = mods }));
        Assert.Contains("não entra", ShipShop.Check(w, 1, hull.Id, mods) ?? "");
    }

    /// <summary>Sem máquinas não larga do cais: a ranhura obrigatória vazia não assina.</summary>
    [Fact]
    public void RanhuraObrigatoriaVaziaNaoAssina()
    {
        var w = Build();
        var hull = Big(w);
        var slots = ShipShop.Slots(w, hull.Id);
        var mods = Full(w, 1, hull.Id);
        int req = slots.FindIndex(s => w.ShipSlotDefs.TryGetValue(s, out var d) && d.Required);
        Assert.True(req >= 0);

        Assert.Null(ShipShop.Check(w, 1, hull.Id, mods));
        mods[req] = "";
        Assert.Contains("vazia", ShipShop.Check(w, 1, hull.Id, mods) ?? "");
        Assert.Contains(ShipShop.Advice(w, 1, hull.Id, mods), n => n.Bad);
    }

    /// <summary>Peça fechada pede investigação e abre com ela: a prancheta mostra-a apagada em vez de a
    /// esconder, e é o Locked que diz o que falta.</summary>
    [Fact]
    public void PecaFechadaPedeInvestigacaoEAbreComEla()
    {
        var w = Build();
        var locked = w.ShipModules.Values.First(m => m.TechId.Length > 0);
        var hull = ShipShop.Chassis(w).First(d => ShipShop.Slots(w, d.Id).Contains(locked.Slot));
        var slots = ShipShop.Slots(w, hull.Id);
        var mods = Full(w, 1, hull.Id);
        mods[slots.IndexOf(locked.Slot)] = locked.Id;

        Assert.NotEmpty(ShipShop.Locked(w, 1, locked.Id));
        Assert.Contains("precisa de", ShipShop.Check(w, 1, hull.Id, mods) ?? "");
        Assert.DoesNotContain(ShipShop.Fit(w, 1, locked.Slot), m => m.Id == locked.Id);

        w.Countries[1].Techs.Add(locked.TechId);
        Assert.Empty(ShipShop.Locked(w, 1, locked.Id));
        Assert.Contains(ShipShop.Fit(w, 1, locked.Slot), m => m.Id == locked.Id);
    }

    /// <summary>Assinar põe o casco no estaleiro como classe e paga milhas navegadas.</summary>
    [Fact]
    public void AssinarPoeOCascoNoEstaleiroEPagaMilhas()
    {
        var w = Build(200f);
        var hull = Big(w);
        var mods = Full(w, 1, hull.Id);
        float price = ShipShop.Price(w, false);

        Assert.Null(new DesignShipCommand(1, "Classe Douro", hull.Id, mods).Validate(w));
        new DesignShipCommand(1, "Classe Douro", hull.Id, mods).Execute(w);

        var design = Assert.Single(ShipShop.Of(w, 1));
        string cls = ShipShop.ClassId(design.Id);
        Assert.True(w.ShipClasses.ContainsKey(cls));
        Assert.Equal("Classe Douro", w.ShipClasses[cls].Name);
        Assert.Equal(200f - price, w.Countries[1].NavyXp, 3);

        // e o resto do jogo trata-o como casco normal: compra-se e vai para o porto
        w.Countries[1].Money = 9999f;
        Assert.Null(new BuyShipCommand(1, cls).Validate(w));
        new BuyShipCommand(1, cls).Execute(w);
        Assert.True(w.Countries[1].Ships.GetValueOrDefault(cls) > 0f);
    }

    /// <summary>Redesenhar muda a classe sem criar outra: o aço que já anda no mar passa a valer o que o
    /// desenho novo diz, como acontece aos modelos de divisão.</summary>
    [Fact]
    public void RedesenharMudaAClasseSemCriarOutra()
    {
        var w = Build();
        var hull = Big(w);
        var slots = ShipShop.Slots(w, hull.Id);
        var mods = Full(w, 1, hull.Id);
        new DesignShipCommand(1, "Classe Tejo", hull.Id, mods).Execute(w);
        var design = Assert.Single(ShipShop.Of(w, 1));
        string cls = ShipShop.ClassId(design.Id);
        float before = w.ShipClasses[cls].Cost;

        int drop = slots.FindIndex(s => !(w.ShipSlotDefs.TryGetValue(s, out var d) && d.Required));
        mods[drop] = "";
        Assert.Null(new DesignShipCommand(1, "Classe Tejo", hull.Id, mods, design.Id).Validate(w));
        new DesignShipCommand(1, "Classe Tejo", hull.Id, mods, design.Id).Execute(w);

        Assert.Single(ShipShop.Of(w, 1));                       // continua a ser um desenho só
        Assert.True(w.ShipClasses[cls].Cost < before);          // e a ficha desceu com a peça que saiu
    }

    /// <summary>Riscar: só sai o desenho que não tem aço nenhum no mar.</summary>
    [Fact]
    public void RiscarSoSaiOQueNaoAndaNoMar()
    {
        var w = Build();
        var hull = Big(w);
        new DesignShipCommand(1, "Classe Sado", hull.Id, Full(w, 1, hull.Id)).Execute(w);
        var design = Assert.Single(ShipShop.Of(w, 1));
        string cls = ShipShop.ClassId(design.Id);

        w.Countries[1].Ships[cls] = 2f;
        Assert.Contains("no mar", new ScrapShipDesignCommand(1, design.Id).Validate(w) ?? "");

        w.Countries[1].Ships[cls] = 0f;
        Assert.Null(new ScrapShipDesignCommand(1, design.Id).Validate(w));
        new ScrapShipDesignCommand(1, design.Id).Execute(w);
        Assert.Empty(ShipShop.Of(w, 1));
        Assert.False(w.ShipClasses.ContainsKey(cls));
    }

    /// <summary>O tecto de desenhos vem da regra, não do código.</summary>
    [Fact]
    public void OTectoDeDesenhosVemDaRegra()
    {
        var w = Build();
        w.Rules["ship_design_max"] = 2f;
        var hull = Big(w);
        var mods = Full(w, 1, hull.Id);
        new DesignShipCommand(1, "A", hull.Id, mods).Execute(w);
        new DesignShipCommand(1, "B", hull.Id, mods).Execute(w);

        Assert.Contains("já tem 2 desenhos", new DesignShipCommand(1, "C", hull.Id, mods).Validate(w) ?? "");
        // mexer num que já existe continua a poder-se: o tecto é de desenhos, não de prancheta
        Assert.Null(new DesignShipCommand(1, "A", hull.Id, mods, ShipShop.Of(w, 1)[0].Id).Validate(w));
    }

    /// <summary>Um casco que se esconde continua a esconder-se depois de desenhado, e o revestimento soma-lhe
    /// mais: é a mesma conta do Subs, sem uma linha de caso especial para desenhos.</summary>
    [Fact]
    public void ODesenhoLevaOEsconderijoParaOMar()
    {
        var w = Build();
        var sub = ShipShop.Chassis(w).FirstOrDefault(d => d.IsSub);
        Assert.NotNull(sub);
        var quieter = w.ShipModules.Values.Where(m => m.Stealth > 0f && ShipShop.Slots(w, sub!.Id).Contains(m.Slot))
                        .OrderByDescending(m => m.Stealth).First();
        w.Countries[1].Techs.Add(quieter.TechId);
        var slots = ShipShop.Slots(w, sub!.Id);
        var mods = Enumerable.Repeat("", slots.Count).ToList();
        mods[slots.IndexOf(ShipShop.Slots(w, sub.Id).First(s => w.ShipSlotDefs[s].Required))] =
            ShipShop.Fit(w, 1, slots.First(s => w.ShipSlotDefs[s].Required)).First().Id;
        mods[slots.IndexOf(quieter.Slot)] = quieter.Id;
        new DesignShipCommand(1, "Classe Silêncio", sub.Id, mods).Execute(w);

        string cls = ShipShop.ClassId(ShipShop.Of(w, 1)[0].Id);
        Assert.True(w.ShipClasses[cls].Stealth > sub.Stealth);
        Assert.Equal(w.ShipClasses[cls].Stealth, Subs.Stealth(w, cls), 3);
    }

    /// <summary>O desenho atravessa o save e volta a viver como classe: grava-se a ESCOLHA e os números
    /// voltam a sair da tabela, para uma peça reafinada valer logo em todos os desenhos que a levam.</summary>
    [Fact]
    public void ODesenhoAtravessaOSaveEVoltaAViverComoClasse()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (var c in w.Countries.Values) { c.Money = 5000f; c.NavyXp = 500f; }
        var hull = Big(w);
        var mods = Full(w, 1, hull.Id);
        new DesignShipCommand(1, "Classe Mondego", hull.Id, mods).Execute(w);
        string cls = ShipShop.ClassId(ShipShop.Of(w, 1)[0].Id);
        w.Countries[1].Ships[cls] = 3f;
        float battle = w.ShipClasses[cls].Battle;

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (back, _) = TestWorld.Build();
        TestWorld.LinearMap(back);
        repo.LoadSave(back, save);

        var design = Assert.Single(ShipShop.Of(back, 1));
        Assert.Equal("Classe Mondego", design.Name);
        Assert.Equal(mods, design.Modules);
        Assert.True(back.ShipClasses.ContainsKey(cls));
        Assert.Equal(battle, back.ShipClasses[cls].Battle, 3);
        Assert.Equal(3f, back.Countries[1].Ships.GetValueOrDefault(cls), 3);
    }
}
