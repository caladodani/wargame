using WarGame.Core.Update;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Actualização automática: o jogo pergunta ao servidor que versão há e, se for mais nova do que a
/// instalada, começa logo a descarregá-la. A parte que se pode pôr à prova sem rede é esta — ler o
/// manifesto e decidir se vale a pena — e é onde se erra: "0.10.0" é mais nova do que "0.9.0" e a
/// comparação de texto diz o contrário.</summary>
public class UpdateCheckTests
{
    private const string Good = """
        {"version":"0.2.0","code":2,"apk":"https://vilarongacalado.com/downloads/wargame.apk",
         "size":41406062,"notes":"asas e esquadras com nome"}
        """;

    [Fact]
    public void ReadsTheManifest()
    {
        var info = UpdateCheck.Parse(Good);
        Assert.NotNull(info);
        Assert.Equal("0.2.0", info!.Version);
        Assert.Equal(2, info.Code);
        Assert.Equal("https://vilarongacalado.com/downloads/wargame.apk", info.Url);
        Assert.Equal(41406062L, info.Size);
        Assert.Equal("asas e esquadras com nome", info.Notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<html><body>404</body></html>")]          // o servidor a devolver a página de erro
    [InlineData("[1,2,3]")]
    [InlineData("""{"version":"0.2.0"}""")]                 // sem APK não há o que descarregar
    [InlineData("""{"apk":"https://x/y.apk"}""")]           // sem versão não se sabe se é nova
    [InlineData("""{"version":"0.2.0","apk":"http://vilarongacalado.com/downloads/wargame.apk"}""")]
    public void RefusesWhatIsNotAManifest(string? json) => Assert.Null(UpdateCheck.Parse(json));

    [Theory]
    [InlineData("0.1.0", "0.2.0", true)]
    [InlineData("0.9.0", "0.10.0", true)]                   // dez é mais do que nove
    [InlineData("0.10.0", "0.9.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]                   // a mesma versão não se descarrega outra vez
    [InlineData("1.0", "1.0.1", true)]
    [InlineData("1.0.1", "1.0", false)]
    [InlineData("", "0.1.0", true)]                         // sem versão instalada, o que lá está serve
    [InlineData("0.1.0", "", false)]
    [InlineData("0.1.0-beta", "0.1.0", false)]
    public void ComparesVersionsByNumber(string local, string remote, bool newer) =>
        Assert.Equal(newer, UpdateCheck.IsNewer(local, remote));

    [Fact]
    public void OnlyDownloadsANewerVersionWithARealFile()
    {
        var info = UpdateCheck.Parse(Good)!;
        Assert.True(UpdateCheck.ShouldDownload("0.1.0", info));
        Assert.False(UpdateCheck.ShouldDownload("0.2.0", info));
        Assert.False(UpdateCheck.ShouldDownload("0.3.0", info));
        Assert.False(UpdateCheck.ShouldDownload("0.1.0", null));
        // manifesto publicado antes de o APK acabar de subir: tamanho zero, não se descarrega
        Assert.False(UpdateCheck.ShouldDownload("0.1.0", info with { Size = 0 }));
    }

    [Fact]
    public void HalfADownloadIsNotAnInstall()
    {
        var info = UpdateCheck.Parse(Good)!;
        Assert.True(UpdateCheck.IsComplete(info, 41406062L));
        Assert.False(UpdateCheck.IsComplete(info, 41406061L));
        Assert.False(UpdateCheck.IsComplete(info, 0L));
    }
}
