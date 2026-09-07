using Godot;

namespace WarGame.Presentation;

/// <summary>A superfície da chapa: aço escovado ladrilhado por cima do painel, e a sombra da aresta que o
/// mete embutido na caixilharia.
///
/// Existe porque `Ui.Box` é um StyleBoxFlat — uma cor lisa, a mesma do primeiro ao último pixel. O
/// PanelFrame já punha cantoneiras e rebites, mas por baixo deles não havia chapa nenhuma: a caixilharia
/// estava agarrada a um rectângulo pintado. É esta camada que faz a diferença entre um painel de metal e um
/// rectângulo cinzento com texto dentro — o que os painéis do HoI4 têm e aqui faltava.
///
/// Desenha em MULTIPLICAÇÃO e nunca em cor própria. É de propósito: os 98 sítios que chamam `Ui.Box`
/// escolhem cada um a sua cor, e uma camada com cor própria atropelava-os a todos. Multiplicar por um
/// cinzento que ronda o branco deixa a cor de cada painel como está e só lhe acrescenta a superfície.
///
/// A sombra da aresta é vectorial e não vem na imagem. Uma imagem de 9 fatias com o miolo ladrilhado eram
/// ~88 chamadas de desenho por painel, vezes 18 painéis; assim são cinco, e a sombra fica nítida em
/// qualquer tamanho de ecrã. Nos cantos as duas sombras multiplicam-se uma pela outra, que é exactamente o
/// que uma chapa embutida faz.
///
/// Só desenha: não sabe nada do mundo nem despacha comandos.</summary>
public partial class PanelGrain : Control
{
    private const string Path = "res://assets/ui/plate.png";

    /// <summary>Fundura da aresta, em pixels, e quanto ela escurece no rebordo. 0.62 dá 0.38 nos cantos,
    /// onde as duas sombras se multiplicam — fundo que chegue para se ver a chapa metida na moldura sem a
    /// tornar um buraco preto.</summary>
    private const float Deep = 12f, Edge = 0.62f;

    private static Texture2D? _plate;
    private static bool _looked;


    /// <summary>A chapa, carregada uma vez para todos os painéis. Se faltar, esta camada não desenha nada e
    /// o jogo fica como estava — uma textura que não carrega não levanta erro nenhum, por isso quem a conta
    /// é o --smoke.</summary>
    public static Texture2D? Plate()
    {
        if (_looked) return _plate;
        _looked = true;
        if (ResourceLoader.Exists(Path) && GD.Load<Texture2D>(Path) is Texture2D t && t.GetWidth() > 0) _plate = t;
        return _plate;
    }

    /// <summary>O rect que esta camada pinta, em coordenadas de ecrã, e o do painel que ela devia cobrir.
    ///
    /// O rect é deduzido da árvore — o pai é a moldura, o avô é o painel — e uma dedução errada pinta na
    /// mesma: encolhida para o rect de conteúdo, sem nevoeiro nas arestas e sem levantar erro nenhum. Daí
    /// haver quem confira.</summary>
    public (Rect2 Mine, Rect2 Plate) Cover()
    {
        if (GetParent() is not Control frame || frame.GetParent() is not Control chapa) return (default, default);
        var r = new Rect2(-frame.Position, chapa.Size);
        return (new Rect2(GetGlobalTransform() * r.Position, r.Size), chapa.GetGlobalRect());
    }

    /// <summary>Valores mais escuro e mais claro do ladrilho. O 0.3.15 foi para o telemóvel com a imagem da
    /// primeira versão da ferramenta, que ainda trazia uma moldura escura de 1 px à volta — e um ladrilho com
    /// moldura, ladrilhado, é uma grelha de células por cima do mundo. O smoke dizia "chapa de 128×128" e a
    /// chapa lá estava: contava o que existe e não olhava para o que se vê. Um grão de superfície não desce
    /// muito abaixo de 0.9; uma moldura desce a 0.4, e é isso que este número denuncia.</summary>
    private static (float Lo, float Hi)? _range;

    private static (float Lo, float Hi)? Range()
    {
        if (_range is not null) return _range;
        if (Plate()?.GetImage() is not Image img) return null;
        float lo = 1f, hi = 0f;
        for (int y = 0; y < img.GetHeight(); y++)
            for (int x = 0; x < img.GetWidth(); x++)
            {
                float v = img.GetPixel(x, y).Luminance;
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
        return _range = (lo, hi);
    }

    /// <summary>O que o --smoke diz da chapa: tamanho do ladrilho, o que ele escurece e quantas camadas
    /// acertam no painel todo. Conta as escondidas também — quem não desenhou no arranque desenha à primeira
    /// vez que o painel abre, e aí já não há smoke a ver.</summary>
    public static string Report(Node root)
    {
        string chapa = Plate() is Texture2D t
            ? $"chapa de {t.GetWidth()}×{t.GetHeight()}"
              + (Range() is (float lo, float hi) ? $" a {lo:0.00}–{hi:0.00}" : "")
            : "chapa em falta";
        var all = Descendants(root).Select(g => g.Cover()).ToList();
        int ok = all.Count(c => c.Mine.Position.DistanceTo(c.Plate.Position) < 0.5f
                             && c.Mine.Size.DistanceTo(c.Plate.Size) < 0.5f);
        return all.Count == 0 ? $"{chapa}, sem camadas" : $"{chapa}, {ok} de {all.Count} a cobrir o painel todo";
    }

    private static IEnumerable<PanelGrain> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is PanelGrain hit) yield return hit;
            foreach (var deep in Descendants(child)) yield return deep;
        }
    }

    public override void _Ready()
    {
        Resized += QueueRedraw;
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mul };
        TextureRepeat = TextureRepeatEnum.Enabled;   // sem isto o ladrilho sai esticado em vez de repetido
    }

    public override void _Draw()
    {
        if (Plate() is not Texture2D tex) return;

        // O rect é o da chapa inteira, não o desta camada: o PanelContainer encolhe os filhos para o rect de
        // conteúdo (a moldura do Ui.Box), e o grão tem de chegar à aresta do painel e não parar na margem.
        if (GetParent() is not Control frame || frame.GetParent() is not Control chapa) return;
        var r = new Rect2(-frame.Position, chapa.Size);
        if (r.Size.X < 24f || r.Size.Y < 24f) return;

        DrawTextureRect(tex, r, tile: true);

        float d = MathF.Min(Deep, MathF.Min(r.Size.X, r.Size.Y) / 3f);
        var dark = new Color(Edge, Edge, Edge);
        var none = Colors.White;                                  // branco = multiplicar por 1 = não mexe
        Band(new Vector2(r.Position.X, r.Position.Y), new Vector2(r.End.X, r.Position.Y), new Vector2(0f, d));
        Band(new Vector2(r.Position.X, r.End.Y), new Vector2(r.End.X, r.End.Y), new Vector2(0f, -d));
        Band(new Vector2(r.Position.X, r.Position.Y), new Vector2(r.Position.X, r.End.Y), new Vector2(d, 0f));
        Band(new Vector2(r.End.X, r.Position.Y), new Vector2(r.End.X, r.End.Y), new Vector2(-d, 0f));

        void Band(Vector2 a, Vector2 b, Vector2 inwards) => DrawPolygon(
            new[] { a, b, b + inwards, a + inwards },
            new[] { dark, dark, none, none });
    }
}
