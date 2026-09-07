using System.Text.Json;

namespace WarGame.Core.Update;

/// <summary>O que o servidor diz que há para descarregar. É o conteúdo de
/// https://vilarongacalado.com/downloads/wargame.json, escrito ao lado do APK quando se publica.</summary>
/// <param name="Version">Versão publicada ("0.2.0"), comparada com a que está instalada.</param>
/// <param name="Code">Número de compilação (version/code do APK), que só cresce.</param>
/// <param name="Url">De onde se puxa o APK.</param>
/// <param name="Size">Tamanho em bytes: serve para saber se o ficheiro que já cá está veio inteiro.</param>
/// <param name="Notes">Uma linha sobre o que mudou, para o jogador saber o que vai instalar.</param>
public sealed record UpdateInfo(string Version, int Code, string Url, long Size, string Notes);

/// <summary>Decidir se vale a pena descarregar uma versão nova. Está aqui, e não no lado do Godot, porque
/// é a única parte disto que se pode pôr à prova sem rede nem telemóvel: a comparação de versões é onde
/// se erra (uma "0.10.0" é mais nova do que uma "0.9.0", e a comparação de texto diz o contrário).</summary>
public static class UpdateCheck
{
    /// <summary>Lê o manifesto. Devolve null a tudo o que não seja um manifesto inteiro — um servidor a
    /// devolver a página de erro em HTML não pode pôr o jogo a descarregar coisa nenhuma.</summary>
    public static UpdateInfo? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string version = Str(root, "version");
            string url = Str(root, "apk");
            if (version.Length == 0 || url.Length == 0) return null;
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;   // só por canal cifrado
            return new UpdateInfo(version, Num(root, "code"), url, Bytes(root, "size"), Str(root, "notes"));
        }
        catch (JsonException) { return null; }
    }

    /// <summary>A versão remota é mais recente do que a instalada? Compara número a número (0.10.0 &gt; 0.9.0)
    /// e trata o que não for número como zero; versões iguais não dão descarga nenhuma.</summary>
    public static bool IsNewer(string? local, string? remote)
    {
        if (string.IsNullOrWhiteSpace(remote)) return false;
        if (string.IsNullOrWhiteSpace(local)) return true;      // sem versão instalada, o que lá está serve
        var a = Parts(local);
        var b = Parts(remote);
        for (int i = 0; i < Math.Max(a.Count, b.Count); i++)
        {
            int x = i < a.Count ? a[i] : 0, y = i < b.Count ? b[i] : 0;
            if (x != y) return y > x;
        }
        return false;
    }

    /// <summary>Vale a pena descarregar? Só se a versão for mais nova E o manifesto trouxer um APK a sério.
    /// Um manifesto com tamanho zero é um APK que ainda está a ser copiado para o servidor.</summary>
    public static bool ShouldDownload(string? local, UpdateInfo? info) =>
        info is not null && info.Size > 0 && IsNewer(local, info.Version);

    /// <summary>O ficheiro que já está no telemóvel é esta versão inteira? Meio download não se instala.</summary>
    public static bool IsComplete(UpdateInfo info, long bytesOnDisk) => bytesOnDisk == info.Size;

    private static List<int> Parts(string v) => v.Split('.', '-', '+')
        .Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out int n) ? n : 0).ToList();

    private static string Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";

    private static int Num(JsonElement o, string name) =>
        o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int n) ? n : 0;

    private static long Bytes(JsonElement o, string name) =>
        o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out long n) ? n : 0L;
}
