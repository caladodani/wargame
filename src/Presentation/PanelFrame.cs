using Godot;

namespace WarGame.Presentation;

/// <summary>A moldura de metal dos painéis: cantoneiras de latão nos quatro cantos, um sulco escuro por
/// dentro da chapa e rebites espaçados ao longo das arestas — a caixilharia que os painéis do HoI4 têm e que
/// aqui faltava. É o que separa uma chapa metálica de um rectângulo cinzento com texto dentro.
///
/// Desenha por cima do painel mas não lhe toca: `MouseFilter = Ignore` deixa passar o dedo, e como se
/// estende ao rectângulo inteiro do pai acompanha qualquer tamanho que o painel venha a ter. Por isso se
/// veste um painel com uma linha (`PanelFrame.Dress(p)`) e não se mexe em mais nada lá dentro.
///
/// Só desenha: não sabe nada do mundo nem despacha comandos.</summary>
public partial class PanelFrame : Control
{
    /// <summary>Comprimento do braço da cantoneira e recuo dos rebites face à aresta.</summary>
    private const float Arm = 26f, Inset = 7f, RivetGap = 76f, Rivet = 3.2f;

    private bool _loud = true;

    /// <summary>Veste um painel com esta moldura e devolve-a. Entra como último filho para ficar por cima do
    /// conteúdo — as cantoneiras vivem na margem da chapa, que é onde não há letra nenhuma.</summary>
    public static PanelFrame Dress(Control panel, bool loud = true)
    {
        var f = new PanelFrame { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore, _loud = loud };
        f.SetAnchorsPreset(LayoutPreset.FullRect);
        var g = new PanelGrain { Name = "Grain", MouseFilter = MouseFilterEnum.Ignore, ZIndex = -1 };
        g.SetAnchorsPreset(LayoutPreset.FullRect);
        f.AddChild(g);        // ZIndex −1 é relativo ao pai: o grão fica por baixo das cantoneiras
        panel.AddChild(f);
        return f;
    }

    /// <summary>Veste todos os painéis flutuantes que forem filhos deste nó (o Hud chama-o uma vez, depois de
    /// os criar). Devolve quantos vestiu, que é o que o --smoke conta.</summary>
    public static int DressAll(Node parent, params Control[] skip)
    {
        int n = 0;
        var skipped = new HashSet<Control>(skip);
        foreach (var child in parent.GetChildren())
            if (child is PanelContainer p && !skipped.Contains(p) && p.GetNodeOrNull("Frame") is null)
            { Dress(p); n++; }
        return n;
    }

    public override void _Ready()
    {
        Resized += QueueRedraw;
        ZIndex = 1;
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        if (r.Size.X < 40f || r.Size.Y < 40f) return;

        var brass = Ui.Accent;
        var deep = Ui.Ink with { A = 0.55f };

        // sulco escuro por dentro da chapa: dá espessura à borda sem pintar mais uma linha clara
        DrawRect(new Rect2(r.Position + Vector2.One * 3f, r.Size - Vector2.One * 6f), deep, false, 1f);

        // cantoneiras: dois braços de latão em cada canto, mais grossos por fora
        foreach (var (corner, dx, dy) in new[]
        {
            (r.Position, 1f, 1f),
            (new Vector2(r.End.X, r.Position.Y), -1f, 1f),
            (new Vector2(r.Position.X, r.End.Y), 1f, -1f),
            (r.End, -1f, -1f),
        })
        {
            var o = corner + new Vector2(dx, dy) * 2f;
            DrawLine(o, o + new Vector2(dx * Arm, 0f), brass, 3f);
            DrawLine(o, o + new Vector2(0f, dy * Arm), brass, 3f);
            var i = corner + new Vector2(dx, dy) * Inset;
            DrawLine(i, i + new Vector2(dx * Arm * 0.55f, 0f), brass.Darkened(0.35f), 1.4f);
            DrawLine(i, i + new Vector2(0f, dy * Arm * 0.55f), brass.Darkened(0.35f), 1.4f);
            Bolt(corner + new Vector2(dx, dy) * (Inset + 2f));
        }

        if (!_loud) return;

        // rebites ao longo das arestas de cima e de baixo, espaçados a olho e sempre simétricos
        int steps = Mathf.Max(1, Mathf.RoundToInt((r.Size.X - 2f * Arm) / RivetGap));
        for (int i = 1; i < steps; i++)
        {
            float x = r.Position.X + Arm + (r.Size.X - 2f * Arm) * i / steps;
            Bolt(new Vector2(x, r.Position.Y + Inset));
            Bolt(new Vector2(x, r.End.Y - Inset));
        }
        int down = Mathf.Max(1, Mathf.RoundToInt((r.Size.Y - 2f * Arm) / RivetGap));
        for (int i = 1; i < down; i++)
        {
            float y = r.Position.Y + Arm + (r.Size.Y - 2f * Arm) * i / down;
            Bolt(new Vector2(r.Position.X + Inset, y));
            Bolt(new Vector2(r.End.X - Inset, y));
        }
    }

    /// <summary>Um rebite: sombra em baixo, cabeça de latão e o brilho no canto de cima — três círculos que
    /// dão volume a uma coisa que é plana.</summary>
    private void Bolt(Vector2 at)
    {
        DrawCircle(at + new Vector2(0.6f, 0.9f), Rivet, new Color(0, 0, 0, 0.55f));
        DrawCircle(at, Rivet, Ui.Accent.Darkened(0.28f));
        DrawCircle(at - new Vector2(0.9f, 1f), Rivet * 0.42f, Ui.Accent.Lightened(0.55f));
    }
}
