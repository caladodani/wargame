using Godot;

namespace WarGame.Presentation;

/// <summary>As teclas de metal: a chapa de 9 fatias que dá relevo aos botões todos do jogo.
///
/// Existe porque `Ui.Fill` é um StyleBoxFlat — um rectângulo de cor lisa com uma linha à volta. Os
/// painéis já ganharam superfície no 0.3.15 (PanelGrain), mas os botões continuavam a ser rectângulos
/// pintados por cima de chapa de metal, que é justamente o contrário do que o olho espera: num painel de
/// aço, o que sobressai são as teclas.
///
/// A imagem vem em cinzentos e a cor entra pela multiplicação (ModulateColor), um tom por papel — aço no
/// botão comum, latão no principal, vermelho no perigoso. É de propósito: assim há uma imagem e não
/// três, e o degradê, a aresta de luz e o contorno são os mesmos em todos, que é o que faz um conjunto
/// de teclas parecer o mesmo painel de metal.
///
/// São nove fatias e não um ladrilho porque uma tecla tem partes que não podem esticar: o contorno, a
/// linha de luz por baixo dele e a sombra da base. Estica só o miolo — e o miolo é um degradê vertical,
/// que estica sem se dar por isso. As margens aqui têm de bater certo com as do `tools/make_ui_art.py`.
///
/// A premida não é a solta mais escura: troca as pontas do degradê e passa a luz para baixo. Escurecer
/// só diz "desligado"; virar a luz diz "afundado".
///
/// Se as imagens faltarem, não devolve estilo nenhum e o `Ui` fica com os StyleBoxFlat de sempre — uma
/// textura que não carrega não levanta erro, por isso quem conta as teclas é o --smoke.</summary>
public static class MetalButton
{
    private const string Up = "res://assets/ui/button.png";
    private const string Down = "res://assets/ui/button_pressed.png";

    /// <summary>As fatias: esquerda, direita, cima, baixo. A de baixo é maior porque leva a sombra
    /// interior além do contorno. Iguais às FATIA da ferramenta que coze a imagem.</summary>
    private const int Ml = 6, Mr = 6, Mt = 6, Mb = 8;

    private static readonly Dictionary<string, Texture2D?> Cache = new();

    private static Texture2D? Tex(string path)
    {
        if (Cache.TryGetValue(path, out var got)) return got;
        Texture2D? t = ResourceLoader.Exists(path) && GD.Load<Texture2D>(path) is Texture2D x && x.GetWidth() > 0
            ? x : null;
        Cache[path] = t;
        return t;
    }

    /// <summary>Uma tecla na cor pedida. `pressed` vira a luz para baixo. Devolve null se a imagem faltar,
    /// e quem chama fica com o estilo liso de antes.</summary>
    public static StyleBox? Style(Color tint, bool pressed = false, int padX = 14, int padY = 8)
    {
        if (Tex(pressed ? Down : Up) is not Texture2D tex) return null;
        var s = new StyleBoxTexture
        {
            Texture = tex,
            ModulateColor = tint,
            TextureMarginLeft = Ml, TextureMarginRight = Mr, TextureMarginTop = Mt, TextureMarginBottom = Mb,
            ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
        };
        return s;
    }

    /// <summary>Aresta de luz, miolo e contorno da imagem que está mesmo no jogo.
    ///
    /// O 0.3.15 foi para o telemóvel com a imagem errada e o smoke não deu por nada, porque contava que a
    /// imagem existia e não olhava para o que ela tinha lá dentro. Estes três números são o que separa uma
    /// tecla de um rectângulo cinzento: numa imagem lisa saem os três iguais.</summary>
    private static (float Edge, float Face, float Line)? _bands;

    private static (float Edge, float Face, float Line)? Bands()
    {
        if (_bands is not null) return _bands;
        if (Tex(Up)?.GetImage() is not Image img) return null;
        int w = img.GetWidth(), h = img.GetHeight();
        float Row(int y)
        {
            float s = 0f;
            for (int x = 0; x < w; x++) s += img.GetPixel(x, y).Luminance;
            return s / w;
        }
        float face = 0f;
        int n = 0;
        for (int y = Mt; y < h - Mb; y++)
            for (int x = Ml; x < w - Mr; x++) { face += img.GetPixel(x, y).Luminance; n++; }
        return _bands = (Row(1), n == 0 ? 0f : face / n, Row(h - 1));
    }

    /// <summary>O que o --smoke diz das teclas: o que a imagem tem lá dentro e quantos botões da árvore
    /// estão mesmo a usá-la. Conta pelo estilo que o botão devolve — tema e sobreposições resolvidos —
    /// que é o que ele vai desenhar, e não pelo que julgamos ter posto no tema.</summary>
    public static string Report(Node root)
    {
        if (Tex(Up) is not Texture2D t) return "teclas em falta";
        string img = $"tecla de {t.GetWidth()}×{t.GetHeight()}"
                   + (Bands() is (float e, float f, float l)
                       ? $" (aresta {e:0.00}, miolo {f:0.00}, contorno {l:0.00})" : "");
        int all = 0, metal = 0;
        foreach (var b in Descendants(root))
        {
            all++;
            if (b.GetThemeStylebox("normal") is StyleBoxTexture) metal++;
        }
        return all == 0 ? $"{img}, sem botões" : $"{img}, {metal} de {all} botões em chapa";
    }

    private static IEnumerable<Button> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button hit) yield return hit;
            foreach (var deep in Descendants(child)) yield return deep;
        }
    }
}
