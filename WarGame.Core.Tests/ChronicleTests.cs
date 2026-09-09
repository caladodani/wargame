using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Crónica da campanha: o que fica escrito, o que não chega a ser escrito e o que sobrevive ao save.
/// Os géneros e os pesos vêm da tabela chronicle_kind — o que se mede aqui é a regra, não o texto.</summary>
public class ChronicleTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new ChronicleSystem());
        w.Tick();                      // o Tick é que liga o sistema ao barramento
        return w;
    }


    [Fact]
    public void ADeclarationOfWar_IsWrittenDown()
    {
        var w = Setup();
        w.Events.Publish(new WarDeclared(1, 2));

        var e = Assert.Single(w.Chronicle);
        Assert.Equal("guerra", e.Kind);
        Assert.Contains("Alfa", e.Text);
        Assert.Contains("Beta", e.Text);
        Assert.Equal(1, e.CountryId);
    }









}
