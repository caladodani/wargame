using Godot;

namespace WarGame.Presentation;

/// <summary>O nome do país escrito a acompanhar a curva do território, letra a letra — o que os mapas de
/// verdade fazem e o que o HoI4 faz: "ESTADOS UNIDOS" deitado sobre o continente, "CHILE" a descer com a
/// costa. Até aqui era um Label direito pousado no centro de gravidade do país, que num território comprido
/// ou torto ficava atravessado por cima de tudo e não dizia nada da forma da terra.
///
/// Como se acha a curva, só com os centros das regiões e a área de cada uma como peso:
/// 1. média pesada → o centro do país;
/// 2. matriz de covariância pesada → o eixo principal (a direcção em que o país é comprido) por um ângulo
///    fechado, sem iterações;
/// 3. nesse referencial rodado, mínimos quadrados pesados de uma parábola v = a·u² + b·u + c — é a espinha
///    do território, e é ela que curva o nome;
/// 4. as letras espalham-se pela espinha entre os extremos, cada uma rodada pela tangente da curva no
///    ponto onde caiu.
///
/// A curvatura leva um travão (MaxBend) para um país em forma de arco não escrever o nome em ferradura, e o
/// eixo é sempre virado para a direita/baixo, para o texto nunca sair de cabeça para baixo.
///
/// Só desenha: não sabe nada do mundo. Quem lhe dá os pontos é o RegionRenderer.</summary>
public partial class CountryLabel : Node2D
{
    /// <summary>Desvio máximo da parábola, em fracção do comprimento do nome: acima disto o nome deixava de
    /// se ler como uma linha.</summary>
    private const float MaxBend = 0.18f;

    /// <summary>Espaçamento das letras, em fracção do tamanho da letra (as maiúsculas de um mapa respiram).</summary>
    private const float Advance = 0.66f;

    private string _text = "";
    private int _size;
    private Color _color = Colors.White;

    /// <summary>Comprimento aparente do país (raiz da área somada): o RegionRenderer usa-o para decidir se o
    /// nome cabe no ecrã a este zoom.</summary>
    public float Span { get; private set; }

    /// <summary>Escreve o nome sobre a espinha dos pontos dados (centro da região, área como peso). Só
    /// refaz as letras quando o texto, o tamanho ou a cor mudam — a cada dia o que muda é a posição.</summary>
    public void Set(string name, IReadOnlyList<(Vector2 At, float Weight)> points, Color color, int size)
    {
        string text = name.ToUpperInvariant();
        bool rebuild = text != _text || size != _size || color != _color;
        _text = text; _size = size; _color = color;
        if (points.Count == 0) return;

        // 1. centro de gravidade
        float sw = 0f; var mean = Vector2.Zero;
        foreach (var (at, weight) in points) { sw += weight; mean += at * weight; }
        if (sw <= 0f) return;
        mean /= sw;
        Position = mean;
        Span = Mathf.Sqrt(sw);

        // 2. eixo principal: o ângulo que maximiza a variância, fechado a partir da covariância
        float sxx = 0f, syy = 0f, sxy = 0f;
        foreach (var (at, weight) in points)
        {
            var d = at - mean;
            sxx += weight * d.X * d.X; syy += weight * d.Y * d.Y; sxy += weight * d.X * d.Y;
        }
        float angle = 0.5f * Mathf.Atan2(2f * sxy, sxx - syy);
        var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        if (dir.X < 0f) { dir = -dir; angle = dir.Angle(); }   // o nome lê-se sempre da esquerda para a direita
        var up = new Vector2(-dir.Y, dir.X);

        // 3. parábola pesada na espinha do território (v = a·u² + b·u + c)
        float u0 = float.MaxValue, u1 = float.MinValue;
        double s0 = 0, s1 = 0, s2 = 0, s3 = 0, s4 = 0, t0 = 0, t1 = 0, t2 = 0;
        foreach (var (at, weight) in points)
        {
            var d = at - mean;
            double u = d.Dot(dir), v = d.Dot(up), ww = weight;
            u0 = Mathf.Min(u0, (float)u); u1 = Mathf.Max(u1, (float)u);
            double u2 = u * u;
            s0 += ww; s1 += ww * u; s2 += ww * u2; s3 += ww * u2 * u; s4 += ww * u2 * u2;
            t0 += ww * v; t1 += ww * u * v; t2 += ww * u2 * v;
        }
        var (a, b, c) = Solve3(s4, s3, s2, s3, s2, s1, s2, s1, s0, t2, t1, t0);

        // 4. as letras, espalhadas pelo troço da espinha que o nome ocupa
        if (rebuild) Build();
        float step = _size * Advance;
        float width = MathF.Max(step, step * MathF.Max(1, _text.Length - 1));
        float half = MathF.Min(width, MathF.Max(width * 0.35f, (u1 - u0) * 0.72f)) / 2f;
        float bend = MaxBend * width;
        int n = Mathf.Max(1, GetChildCount());
        for (int i = 0; i < GetChildCount(); i++)
        {
            if (GetChild(i) is not Node2D glyph) continue;
            float u = n == 1 ? 0f : Mathf.Lerp(-half, half, (float)i / (n - 1));
            float v = Mathf.Clamp((float)(a * u * u + b * u + c), -bend, bend);
            float slope = Mathf.Clamp((float)(2f * a * u + b), -1.2f, 1.2f);
            glyph.Position = dir * u + up * v;
            glyph.Rotation = angle + Mathf.Atan(slope);
        }
    }

    /// <summary>Uma letra por nó, cada uma com o seu Label centrado — é o que permite rodá-las à parte.</summary>
    private void Build()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        foreach (char ch in _text)
        {
            var glyph = new Node2D();
            var lbl = new Label
            {
                Text = ch.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Size = new Vector2(_size * 1.6f, _size * 1.6f),
                Position = new Vector2(-_size * 0.8f, -_size * 0.8f),
                LabelSettings = new LabelSettings
                {
                    FontSize = _size, FontColor = _color,
                    OutlineSize = Mathf.Max(4, _size / 3), OutlineColor = new Color(0, 0, 0, 0.85f),
                },
            };
            glyph.AddChild(lbl);
            AddChild(glyph);
        }
    }

    /// <summary>Sistema 3×3 por eliminação de Gauss com pivô: os mínimos quadrados da parábola. Devolve
    /// (0,0,0) quando o sistema é degenerado (país de uma região só, ou pontos em cima uns dos outros) —
    /// nesse caso o nome fica direito, que é o que se quer.</summary>
    private static (double A, double B, double C) Solve3(
        double a11, double a12, double a13, double a21, double a22, double a23,
        double a31, double a32, double a33, double b1, double b2, double b3)
    {
        var m = new[,] { { a11, a12, a13, b1 }, { a21, a22, a23, b2 }, { a31, a32, a33, b3 } };
        for (int col = 0; col < 3; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < 3; row++)
                if (Math.Abs(m[row, col]) > Math.Abs(m[pivot, col])) pivot = row;
            if (Math.Abs(m[pivot, col]) < 1e-9) return (0, 0, 0);
            if (pivot != col)
                for (int k = col; k < 4; k++) (m[col, k], m[pivot, k]) = (m[pivot, k], m[col, k]);
            for (int row = 0; row < 3; row++)
            {
                if (row == col) continue;
                double f = m[row, col] / m[col, col];
                for (int k = col; k < 4; k++) m[row, k] -= f * m[col, k];
            }
        }
        return (m[0, 3] / m[0, 0], m[1, 3] / m[1, 1], m[2, 3] / m[2, 2]);
    }
}
