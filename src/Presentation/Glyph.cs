using Godot;

namespace WarGame.Presentation;

/// <summary>Chapas desenhadas à mão, no lugar dos emoji.
///
/// O jogo mostrava um emoji em todo o sítio onde o HoI4 mostra uma chapa gravada: 🏭 na fábrica, 🔬 no
/// laboratório, 🪖 no ramo da infantaria. Um emoji é a única coisa no ecrã que não é nossa — vem do
/// telemóvel, muda de aparência de aparelho para aparelho, é redondo e colorido no meio de um jogo de
/// tinta e metal, e no S24 sai desenhado pela Noto Color Emoji com um brilho de autocolante. O
/// UnitSymbol já provava que se desenha melhor à mão: os símbolos NATO do mapa são linhas nossas.
///
/// Estas chapas são silhuetas, ao traço, na cor que a moldura pedir. Nenhuma delas é arte da Paradox nem
/// copiada de lado nenhum: são as formas que qualquer manual desenha — um capacete, uma lagarta, um obus,
/// uma asa, uma âncora, um telhado de fábrica, um frasco, uma bigorna, um livro, um camião, um átomo.
///
/// Qual é a chapa de quê não está aqui: vem da coluna `glyph` (building.glyph, tech_branch.glyph). Este
/// ficheiro só sabe desenhar; a base de dados é que sabe o que se desenha onde. O que não conhecer sai
/// como roda dentada — a peça neutra, que é melhor do que um buraco.</summary>
public static partial class Glyph        // partial: leva um nó Godot lá dentro (GD0002)
{
    /// <summary>Todos os nomes que este ficheiro sabe desenhar. É público para o contador do smoke poder
    /// perguntar quantos dos que a base de dados pede é que existem mesmo.</summary>
    public static readonly string[] Known =
    {
        "capacete", "lagarta", "obus", "asa", "ancora", "drone", "camiao",
        "fabrica", "livro", "frasco", "atomo", "bigorna", "estrada", "escudo", "roda",
    };

    public static bool Knows(string name) => System.Array.IndexOf(Known, name) >= 0;

    /// <summary>Desenha a chapa dentro do rectângulo, na cor dada. `r` é o espaço todo: o desenho encolhe
    /// para dentro dele, porque uma chapa colada à borda de um botão lê-se mal.</summary>
    public static void Draw(CanvasItem ci, Rect2 r, string name, Color ink, float thick = 1.8f)
    {
        // caixa útil quadrada e centrada: a chapa nunca se deforma com a moldura
        float side = Mathf.Min(r.Size.X, r.Size.Y) * 0.86f;
        var box = new Rect2(r.Position + (r.Size - new Vector2(side, side)) / 2f, side, side);
        float u = side;                                   // unidade: tudo abaixo é fracção do lado
        Vector2 P(float x, float y) => box.Position + new Vector2(x * u, y * u);
        void Line(float x1, float y1, float x2, float y2, float w = 1f) => ci.DrawLine(P(x1, y1), P(x2, y2), ink, thick * w);
        void Poly(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
            ci.DrawPolyline(pts, ink, thick);
        }
        void Fill(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
            ci.DrawColoredPolygon(pts, ink);
        }
        void Ring(float cx, float cy, float rad, float w = 1f) => ci.DrawArc(P(cx, cy), rad * u, 0f, Mathf.Tau, 24, ink, thick * w);
        void Arc(float cx, float cy, float rad, float a0, float a1, float w = 1f)
            => ci.DrawArc(P(cx, cy), rad * u, a0, a1, 16, ink, thick * w);
        void Dot(float cx, float cy, float rad) => ci.DrawCircle(P(cx, cy), rad * u, ink);

        switch (name)
        {
            // Capacete de aço visto de lado: a calote e a aba, que é o que o faz ler como capacete.
            case "capacete":
                Arc(0.5f, 0.60f, 0.30f, Mathf.Pi, Mathf.Tau, 1.15f);
                Line(0.14f, 0.60f, 0.86f, 0.60f, 1.15f);
                Line(0.10f, 0.66f, 0.90f, 0.66f);
                Line(0.10f, 0.66f, 0.14f, 0.60f);
                Line(0.90f, 0.66f, 0.86f, 0.60f);
                break;

            // Carro de combate de perfil: casco, torre, cano e as rodas da lagarta.
            case "lagarta":
                Poly(0.10f, 0.56f, 0.90f, 0.56f, 0.90f, 0.42f, 0.66f, 0.42f, 0.60f, 0.30f, 0.36f, 0.30f, 0.32f, 0.42f, 0.10f, 0.42f, 0.10f, 0.56f);
                Line(0.60f, 0.36f, 0.94f, 0.36f, 1.1f);       // cano
                Arc(0.30f, 0.66f, 0.10f, 0f, Mathf.Pi);
                Arc(0.70f, 0.66f, 0.10f, 0f, Mathf.Pi);
                Line(0.20f, 0.66f, 0.80f, 0.66f);
                foreach (float x in new[] { 0.36f, 0.50f, 0.64f }) Dot(x, 0.66f, 0.045f);
                break;

            // Obus: a granada a subir em diagonal com o rasto da carga.
            case "obus":
                Poly(0.34f, 0.72f, 0.34f, 0.44f, 0.50f, 0.20f, 0.66f, 0.44f, 0.66f, 0.72f, 0.34f, 0.72f);
                Line(0.34f, 0.56f, 0.66f, 0.56f);
                Line(0.40f, 0.80f, 0.34f, 0.94f);
                Line(0.50f, 0.80f, 0.50f, 0.96f);
                Line(0.60f, 0.80f, 0.66f, 0.94f);
                break;

            // Asa em delta vista de cima: fuselagem, asas para trás e os estabilizadores.
            case "asa":
                Fill(0.50f, 0.10f, 0.60f, 0.44f, 0.94f, 0.66f, 0.94f, 0.74f, 0.56f, 0.66f, 0.54f, 0.84f, 0.68f, 0.92f, 0.68f, 0.96f,
                     0.32f, 0.96f, 0.32f, 0.92f, 0.46f, 0.84f, 0.44f, 0.66f, 0.06f, 0.74f, 0.06f, 0.66f, 0.40f, 0.44f);
                break;

            // Âncora: haste, cepo e as unhas.
            case "ancora":
                Ring(0.50f, 0.18f, 0.09f);
                Line(0.50f, 0.27f, 0.50f, 0.84f, 1.15f);
                Line(0.26f, 0.38f, 0.74f, 0.38f, 1.15f);
                Arc(0.50f, 0.56f, 0.30f, Mathf.Pi * 0.18f, Mathf.Pi * 0.82f, 1.15f);
                Line(0.20f, 0.60f, 0.13f, 0.52f);
                Line(0.80f, 0.60f, 0.87f, 0.52f);
                break;

            // Drone de quatro braços visto de cima: cruz, quatro anéis de hélice, corpo no meio.
            case "drone":
                Line(0.22f, 0.22f, 0.78f, 0.78f);
                Line(0.78f, 0.22f, 0.22f, 0.78f);
                foreach (var (x, y) in new[] { (0.20f, 0.20f), (0.80f, 0.20f), (0.20f, 0.80f), (0.80f, 0.80f) })
                    Ring(x, y, 0.13f);
                Fill(0.40f, 0.42f, 0.60f, 0.42f, 0.60f, 0.58f, 0.40f, 0.58f);
                break;

            // Camião de perfil: caixa, cabina e as duas rodas.
            case "camiao":
                Poly(0.06f, 0.34f, 0.54f, 0.34f, 0.54f, 0.66f, 0.06f, 0.66f, 0.06f, 0.34f);
                Poly(0.54f, 0.46f, 0.72f, 0.46f, 0.84f, 0.58f, 0.94f, 0.58f, 0.94f, 0.66f, 0.54f, 0.66f);
                Dot(0.24f, 0.72f, 0.085f);
                Dot(0.78f, 0.72f, 0.085f);
                break;

            // Fábrica: o telhado de dentes de serra e a chaminé a fumar, que é como se desenha desde sempre.
            case "fabrica":
                Poly(0.06f, 0.84f, 0.06f, 0.52f, 0.28f, 0.66f, 0.28f, 0.52f, 0.50f, 0.66f, 0.50f, 0.52f, 0.72f, 0.66f, 0.72f, 0.84f, 0.06f, 0.84f);
                Poly(0.78f, 0.84f, 0.78f, 0.24f, 0.90f, 0.24f, 0.90f, 0.84f);
                Arc(0.84f, 0.16f, 0.07f, Mathf.Pi, Mathf.Tau, 0.8f);
                break;

            // Livro aberto: as duas folhas e a lombada.
            case "livro":
                Poly(0.50f, 0.30f, 0.12f, 0.24f, 0.12f, 0.74f, 0.50f, 0.80f, 0.88f, 0.74f, 0.88f, 0.24f, 0.50f, 0.30f);
                Line(0.50f, 0.30f, 0.50f, 0.80f, 1.15f);
                Line(0.20f, 0.40f, 0.42f, 0.44f, 0.7f);
                Line(0.58f, 0.44f, 0.80f, 0.40f, 0.7f);
                Line(0.20f, 0.54f, 0.42f, 0.58f, 0.7f);
                Line(0.58f, 0.58f, 0.80f, 0.54f, 0.7f);
                break;

            // Frasco de laboratório: gargalo, corpo cónico e o líquido lá dentro.
            case "frasco":
                Line(0.38f, 0.14f, 0.62f, 0.14f, 1.15f);
                Poly(0.42f, 0.14f, 0.42f, 0.40f, 0.20f, 0.82f, 0.80f, 0.82f, 0.58f, 0.40f, 0.58f, 0.14f);
                Fill(0.29f, 0.62f, 0.71f, 0.62f, 0.80f, 0.82f, 0.20f, 0.82f);
                break;

            // Átomo: núcleo e as três órbitas cruzadas.
            case "atomo":
                Dot(0.50f, 0.50f, 0.08f);
                for (int i = 0; i < 3; i++)
                {
                    float a = Mathf.Pi * i / 3f;
                    var c = P(0.50f, 0.50f);
                    var pts = new Vector2[33];
                    for (int k = 0; k < pts.Length; k++)
                    {
                        float t = Mathf.Tau * k / (pts.Length - 1);
                        var e = new Vector2(Mathf.Cos(t) * 0.42f * u, Mathf.Sin(t) * 0.17f * u);
                        pts[k] = c + e.Rotated(a);
                    }
                    ci.DrawPolyline(pts, ink, thick * 0.85f);
                }
                break;

            // Bigorna: a mesa, o corno e o pé — a chapa do arsenal.
            case "bigorna":
                Fill(0.10f, 0.34f, 0.78f, 0.34f, 0.92f, 0.42f, 0.72f, 0.46f, 0.62f, 0.46f, 0.58f, 0.62f,
                     0.72f, 0.78f, 0.72f, 0.84f, 0.28f, 0.84f, 0.28f, 0.78f, 0.42f, 0.62f, 0.38f, 0.46f, 0.10f, 0.46f);
                break;

            // Estrada a fugir para o horizonte: as duas bermas a fechar e o tracejado do meio.
            case "estrada":
                Poly(0.06f, 0.90f, 0.38f, 0.14f, 0.62f, 0.14f, 0.94f, 0.90f);
                Line(0.50f, 0.20f, 0.50f, 0.34f, 0.8f);
                Line(0.50f, 0.44f, 0.50f, 0.62f, 0.8f);
                Line(0.50f, 0.72f, 0.50f, 0.94f, 0.8f);
                break;

            // Escudo com a barra do reforço: a chapa da fortificação.
            case "escudo":
                Poly(0.50f, 0.10f, 0.88f, 0.24f, 0.88f, 0.54f, 0.50f, 0.90f, 0.12f, 0.54f, 0.12f, 0.24f, 0.50f, 0.10f);
                Line(0.24f, 0.42f, 0.76f, 0.42f, 0.85f);
                break;

            // Roda dentada: a peça neutra de quem não tem chapa própria.
            default:
                Ring(0.50f, 0.50f, 0.28f, 1.1f);
                Ring(0.50f, 0.50f, 0.12f);
                for (int i = 0; i < 8; i++)
                {
                    float a = Mathf.Tau * i / 8f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(P(0.5f, 0.5f) + dir * 0.28f * u, P(0.5f, 0.5f) + dir * 0.42f * u, ink, thick * 1.2f);
                }
                break;
        }
    }

    /// <summary>Varre uma sub-árvore de nós e conta as chapas — e quantas dessas saíram como roda dentada
    /// por o nome pedido não existir. É o que os contadores do --smoke usam para não dizerem que desenharam
    /// o que não desenharam: um nome mal escrito na tabela não dá erro, dá uma roda calada.</summary>
    public static (int Drawn, int FellBack) Count(Node root)
    {
        int drawn = 0, fell = 0;
        Walk(root);
        return (drawn, fell);

        void Walk(Node n)
        {
            if (n is Plate p) { drawn++; if (p.FellBack) fell++; }
            foreach (var ch in n.GetChildren()) Walk(ch);
        }
    }

    /// <summary>Um nó que desenha a chapa e mais nada. Entra onde antes entrava um Label com um emoji.</summary>
    public static Plate Make(string name, float size, Color? ink = null, string? tip = null)
    {
        var p = new Plate { GlyphName = name, Ink = ink ?? Ui.Accent, CustomMinimumSize = new Vector2(size, size) };
        if (tip is not null) p.TooltipText = tip;
        return p;
    }

    /// <summary>O nó. Guarda o nome para o contador do smoke poder varrer o ecrã e dizer quantas chapas
    /// estão mesmo desenhadas naquele instante — e quantas dessas caíram na roda por falta de desenho.</summary>
    public sealed partial class Plate : Control
    {
        public string GlyphName { get; set; } = "roda";
        public Color Ink { get; set; } = Colors.White;
        public bool FellBack => !Knows(GlyphName);

        public override void _Draw() => Glyph.Draw(this, new Rect2(Vector2.Zero, Size), GlyphName, Ink);
    }
}
