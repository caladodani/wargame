using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A rota de uma divisão em marcha. O que se prova aqui é a conta: as paragens por onde passa, onde
/// vai a coluna neste momento e os dias que faltam — e que os dias batem certo com os que o mundo gasta
/// mesmo a andar, senão a etiqueta do mapa mentia ao jogador.</summary>
public class RouteLineTests
{
    private static (World w, Division d) March(int to = 3)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, n: 6, split: 6);            // mapa todo do país 1: marcha sem guerra pelo meio
        w.Register(new MovementSystem());                  // o TestWorld não regista nada: sem isto ninguém anda
        var d = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 1);
        d.SetPath(Enumerable.Range(2, to - 1));
        return (w, d);
    }

    [Fact]
    public void UmaDivisaoParadaNaoTemRota()
    {
        var (w, d) = March();
        d.ClearPath();
        Assert.Null(RouteLine.Of(w, d));
    }

    [Fact]
    public void AsParagensComecamOndeATropaEstaEAcabamNoDestino()
    {
        var (w, d) = March(to: 4);
        var r = RouteLine.Of(w, d)!.Value;
        Assert.Equal(new[] { 1, 2, 3, 4 }, r.Stops.Select(s => s.RegionId));
        Assert.Equal(1, r.From.RegionId);
        Assert.Equal(4, r.To.RegionId);
        Assert.Equal(w.Regions[1].CenterX, r.From.X, 3);
        Assert.Equal(w.Regions[4].CenterX, r.To.X, 3);
    }

    [Fact]
    public void ACabecaDaColunaAndaComOProgressoDoSalto()
    {
        var (w, d) = March(to: 3);
        Assert.Equal(w.Regions[1].CenterX, RouteLine.Of(w, d)!.Value.Head.X, 3);
        d.MoveProgress = 0.5f;
        Assert.Equal(150f, RouteLine.Of(w, d)!.Value.Head.X, 3);       // meio caminho entre R1 (100) e R2 (200)
        d.MoveProgress = 1f;
        Assert.Equal(w.Regions[2].CenterX, RouteLine.Of(w, d)!.Value.Head.X, 3);
    }

    [Fact]
    public void OsDiasQueFaltamBatemCertoComOsDiasQueOMundoGasta()
    {
        var (w, d) = March(to: 3);
        int previsto = RouteLine.Of(w, d)!.Value.Days;
        int dias = 0;
        while (d.RegionId != 3 && dias < 100) { w.Tick(); dias++; }
        Assert.Equal(3, d.RegionId);
        Assert.Equal(previsto, dias);
    }

    [Fact]
    public void ODiaJaAndadoDescontaSeDaConta()
    {
        var (w, d) = March(to: 2);
        int inteiro = RouteLine.Of(w, d)!.Value.Days;
        d.MoveProgress = 0.9f;
        Assert.True(RouteLine.Of(w, d)!.Value.Days <= inteiro);
        Assert.True(RouteLine.Of(w, d)!.Value.Days >= 1);              // nunca zero: ainda falta chegar
    }

    [Fact]
    public void EmBatalhaACabecaFicaOndeADivisaoEstaMesmo()
    {
        var (w, d) = March(to: 3);
        d.MoveProgress = 0.6f;
        var b = new Battle { RegionId = 1, AttackerCountryId = 2 };
        b.Defenders.Add(d.Id);
        w.ActiveBattles.Add(b);

        var r = RouteLine.Of(w, d)!.Value;
        Assert.True(r.Fighting);
        Assert.Equal(w.Regions[1].CenterX, r.Head.X, 3);               // presa: não avança nem no desenho
    }

    [Fact]
    public void SoVemAsRotasDeQuemEstaMesmoAMarchar()
    {
        var (w, d) = March(to: 3);
        var parada = TestWorld.AddDivision(w, 2, 1, TestWorld.Inf, 1);
        var rotas = RouteLine.For(w, new[] { d.Id, parada.Id, 999 });
        Assert.Equal(d.Id, Assert.Single(rotas).DivisionId);
    }

    [Fact]
    public void ARotaTemONomeDeGuerraDaDivisao()
    {
        var (w, d) = March();
        d.Name = "1.ª Divisão"; d.HonourName = "Leões";
        Assert.Equal("1.ª Divisão «Leões»", RouteLine.Of(w, d)!.Value.Name);
    }

    [Fact]
    public void UmCaminhoParaUmaRegiaoQueDesapareceuNaoRebenta()
    {
        var (w, d) = March(to: 3);
        d.SetPath(new[] { 999 });
        Assert.Null(RouteLine.Of(w, d));
    }
}
