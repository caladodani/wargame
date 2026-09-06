using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>FocusSystem + SelectFocusCommand. Focos inseridos à mão no World (TestWorld não
/// carrega countries/*.sql); Tick chamado directo.</summary>
public class FocusTests
{
    private static World Setup(out Country c)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        c = w.Countries[1];
        w.Focuses["a"] = new Focus("a", 1, "Primeiro", "", 3, null, 1);
        w.Focuses["b"] = new Focus("b", 1, "Segundo", "", 2, "a", 2);
        w.FocusEffects["a"] = new() { ("industry", 1.5f) };
        return w;
    }

    [Fact]
    public void Ai_PicksCompletesAndChains_EffectApplies()
    {
        var w = Setup(out var c);
        var done = new List<FocusCompleted>();
        w.Events.Subscribe<FocusCompleted>(done.Add);
        var sys = new FocusSystem();
        sys.Tick(w);
        Assert.Equal("a", c.CurrentFocus);
        sys.Tick(w); sys.Tick(w);   // 3 dias
        Assert.Contains("a", c.FocusesDone);
        Assert.Equal(new FocusCompleted(1, "a"), Assert.Single(done));
        Assert.Equal(1.5f, c.Stat("industry"), 0.001f);   // Stats sem industry → 1 × mult
        sys.Tick(w);
        Assert.Equal("b", c.CurrentFocus);   // encadeia no que ficou disponível
    }

    [Fact]
    public void Player_MustChoose_AndCommandValidates()
    {
        var w = Setup(out var c);
        c.IsPlayer = true;
        new FocusSystem().Tick(w);
        Assert.Null(c.CurrentFocus);   // IA não escolhe pelo jogador
        Assert.NotNull(new SelectFocusCommand(1, "b").Validate(w));    // falta o pré-requisito
        Assert.NotNull(new SelectFocusCommand(2, "a").Validate(w));    // foco de outro país
        Assert.Null(new SelectFocusCommand(1, "a").Validate(w));
        new SelectFocusCommand(1, "a").Execute(w);
        Assert.Equal("a", c.CurrentFocus);
    }

    [Fact]
    public void ExtraPrerequisitesAllHaveToBeDone()
    {
        var w = Setup(out var c);
        w.Focuses["c"] = new Focus("c", 1, "Terceiro", "", 2, "a", 3);
        w.FocusLinks["c"] = new List<string> { "b" };            // exige o "a" (requires) e ainda o "b"

        c.FocusesDone.Add("a");
        Assert.False(w.CanFocus(c, "c"));
        Assert.Equal("b", w.FocusBlock(c, "c"));
        c.FocusesDone.Add("b");
        Assert.True(w.CanFocus(c, "c"));
        Assert.Null(w.FocusBlock(c, "c"));
    }

    [Fact]
    public void ChoosingOneBranchClosesTheOther()
    {
        var w = Setup(out var c);
        w.Focuses["c"] = new Focus("c", 1, "Outro ramo", "", 2, "a", 3);
        w.FocusRivals["b"] = new List<string> { "c" };
        w.FocusRivals["c"] = new List<string> { "b" };

        c.FocusesDone.Add("a");
        Assert.True(w.CanFocus(c, "b"));
        Assert.True(w.CanFocus(c, "c"));

        c.FocusesDone.Add("b");                                  // o ramo escolhido fecha o rival
        Assert.False(w.CanFocus(c, "c"));
        Assert.Equal("!b", w.FocusBlock(c, "c"));
        Assert.NotNull(new SelectFocusCommand(1, "c").Validate(w));
    }

    [Fact]
    public void TheAiNeverPicksAClosedBranch()
    {
        var w = Setup(out var c);
        w.Focuses["c"] = new Focus("c", 1, "Outro ramo", "", 2, "a", 3);
        w.FocusRivals["b"] = new List<string> { "c" };
        w.FocusRivals["c"] = new List<string> { "b" };
        c.FocusesDone.Add("a"); c.FocusesDone.Add("c");

        Assert.Null(FocusSystem.NextFocus(w, c));                // só restava o rival fechado
        new FocusSystem().Tick(w);
        Assert.Null(c.CurrentFocus);
    }

    [Fact]
    public void RealDb_TheTreeBranchesAndConverges()
    {
        var w = FactionTests.BuildReal();
        var prt = w.Countries.Values.First(x => x.Tag == "PRT");
        // Atlântico abre dois ramos que se excluem: NATO ou Lusofonia
        Assert.Contains("prt_lusofonia", w.FocusRivals["prt_nato"]);
        Assert.Contains("prt_nato", w.FocusRivals["prt_lusofonia"]);   // a rivalidade vale nos dois sentidos
        // e o topo da árvore exige as duas raízes
        Assert.Contains("prt_atlantico", w.FocusLinks["prt_comandos"]);

        prt.FocusesDone.Add("prt_servico_militar");
        Assert.False(w.CanFocus(prt, "prt_comandos"));                 // falta a outra raiz
        prt.FocusesDone.Add("prt_atlantico");
        Assert.True(w.CanFocus(prt, "prt_comandos"));

        prt.FocusesDone.Add("prt_nato");
        Assert.False(w.CanFocus(prt, "prt_lusofonia"));
    }

    [Fact]
    public void RealDb_PortugalAndBrazil_HaveTrees()
    {
        var w = FactionTests.BuildReal();
        Assert.True(w.Focuses.Values.Count(f => w.Countries[f.CountryId].Tag == "PRT") >= 5);
        Assert.True(w.Focuses.Values.Count(f => w.Countries[f.CountryId].Tag == "BRA") >= 5);
        var prtRoot = w.Focuses.Values.First(f => f.Id == "prt_atlantico");
        Assert.Null(prtRoot.Requires);
        Assert.True(w.FocusEffects.ContainsKey("prt_atlantico"));
    }
}
