using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Os símbolos NATO que o contador do mapa já desenhava, agora desenháveis em qualquer sítio.
///
/// Estavam presos dentro do UnitCounter e não saíam do mapa: a fila de produção e a lista de modelos
/// mostravam o nome da divisão em texto puro, e no HoI4 nenhuma linha de produção é só texto — leva
/// sempre a silhueta do que se está a fabricar. Era desenho a mais para ficar num sítio só.</summary>
public static class NatoSymbol
{
    /// <summary>Marcas de tropa especial que se desenham por cima do símbolo, por ordem de precedência:
    /// a especialidade sai das etiquetas do modelo (unit_tag), nunca de uma lista no código — quem quiser
    /// uma tropa nova põe-lhe a etiqueta na tabela e ela aparece aqui desenhada.</summary>
    private static readonly string[] Specialties = { "montanha", "anfibio", "selva", "artico", "deserto" };

    /// <summary>A especialidade de terreno deste modelo, ou "" se for tropa de linha.</summary>
    public static string SpecialtyOf(IEnumerable<string> tags)
    {
        var t = new HashSet<string>(tags);
        foreach (string s in Specialties) if (t.Contains(s)) return s;
        return "";
    }

    /// <summary>Símbolo do tipo dado dentro da moldura, no canvas dado: as mesmas linhas que se desenham
    /// à mão num mapa de estado-maior (✕ infantaria, oval blindado, oval com ✕ mecanizada, ponto apoio,
    /// ✕ com asas pára-quedistas).
    ///
    /// `spec` acrescenta a marca da tropa especial na faixa de cima, como no HoI4: o símbolo diz de que
    /// arma ela é, a marca diz onde é que ela sabe bater-se. São coisas diferentes e por isso desenham-se
    /// as duas — um Gebirgsjäger é infantaria E é de montanha.</summary>
    public static void Draw(CanvasItem ci, Rect2 r, string kind, float thick = 2.6f, string spec = "")
    {
        var ink = Colors.White with { A = 0.92f };
        ci.DrawRect(r, new Color(0, 0, 0, 0.35f));
        ci.DrawRect(r, ink with { A = 0.65f }, false, 1.6f);

        void Cross()
        {
            ci.DrawLine(r.Position, r.End, ink, thick);
            ci.DrawLine(new Vector2(r.End.X, r.Position.Y), new Vector2(r.Position.X, r.End.Y), ink, thick);
        }
        void Oval()
        {
            var c = r.Position + r.Size / 2f;
            var pts = new Vector2[25];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = Mathf.Tau * i / (pts.Length - 1);
                pts[i] = c + new Vector2(Mathf.Cos(a) * r.Size.X * 0.42f, Mathf.Sin(a) * r.Size.Y * 0.40f);
            }
            ci.DrawPolyline(pts, ink, thick);
        }

        switch (kind)
        {
            case "armor": Oval(); break;
            case "mech": Oval(); Cross(); break;
            case "support": ci.DrawCircle(r.Position + r.Size / 2f, MathF.Min(r.Size.X, r.Size.Y) * 0.22f, ink); break;
            case "airborne":
                Cross();
                ci.DrawArc(r.Position + new Vector2(r.Size.X * 0.25f, r.Size.Y * 0.5f), r.Size.Y * 0.3f, Mathf.Pi, Mathf.Tau, 8, ink, thick * 0.7f);
                ci.DrawArc(r.Position + new Vector2(r.Size.X * 0.75f, r.Size.Y * 0.5f), r.Size.Y * 0.3f, Mathf.Pi, Mathf.Tau, 8, ink, thick * 0.7f);
                break;
            default: Cross(); break;
        }
        if (spec.Length > 0) Mark(ci, r, spec, ink, thick);
    }

    /// <summary>A marca da especialidade, na faixa de cima do símbolo: pico de montanha, vaga de fuzileiro,
    /// fronde de selva, cristal de ártico, duna de deserto. Linhas finas para não roubarem o símbolo — quem
    /// olha vê primeiro a arma e só depois onde ela se bate.</summary>
    private static void Mark(CanvasItem ci, Rect2 r, string spec, Color ink, float thick)
    {
        float w = r.Size.X, h = r.Size.Y;
        var c = new Vector2(r.Position.X + w / 2f, r.Position.Y + h * 0.18f);
        float s = MathF.Min(w, h) * 0.22f, t = MathF.Max(1.2f, thick * 0.6f);
        var pen = ink with { A = 0.95f };
        switch (spec)
        {
            case "montanha":                                     // dois picos, como se desenham na carta
                ci.DrawPolyline(new[] { c + new Vector2(-s * 1.3f, s * 0.55f), c + new Vector2(-s * 0.5f, -s * 0.6f),
                                        c + new Vector2(0.3f * s, s * 0.55f) }, pen, t);
                ci.DrawPolyline(new[] { c + new Vector2(-0.1f * s, s * 0.55f), c + new Vector2(0.7f * s, -s * 0.6f),
                                        c + new Vector2(1.5f * s, s * 0.55f) }, pen, t);
                break;
            case "anfibio":                                      // a vaga que se atravessa a pé
                var wave = new Vector2[9];
                for (int i = 0; i < wave.Length; i++)
                {
                    float u = i / (float)(wave.Length - 1);
                    wave[i] = c + new Vector2((u - 0.5f) * s * 2.6f, MathF.Sin(u * Mathf.Tau) * s * 0.42f);
                }
                ci.DrawPolyline(wave, pen, t);
                break;
            case "selva":                                        // fronde: haste com duas folhas
                ci.DrawLine(c + new Vector2(0f, s * 0.6f), c + new Vector2(0f, -s * 0.6f), pen, t);
                ci.DrawLine(c, c + new Vector2(-s * 0.9f, -s * 0.5f), pen, t);
                ci.DrawLine(c, c + new Vector2(s * 0.9f, -s * 0.5f), pen, t);
                break;
            case "artico":                                       // cristal de gelo: três traços cruzados
                for (int i = 0; i < 3; i++)
                {
                    float a = Mathf.Pi * i / 3f;
                    var d = new Vector2(MathF.Cos(a), MathF.Sin(a)) * s * 0.75f;
                    ci.DrawLine(c - d, c + d, pen, t);
                }
                break;
            case "deserto":                                      // duna e sol
                ci.DrawArc(c + new Vector2(0f, s * 0.45f), s * 0.9f, Mathf.Pi, Mathf.Tau, 10, pen, t);
                ci.DrawCircle(c + new Vector2(s * 1.1f, -s * 0.3f), t * 1.1f, pen);
                break;
        }
    }

    /// <summary>Nome por extenso do tipo — o símbolo diz-se sozinho a quem já leu mapas, e a quem não leu
    /// diz-se pela dica. Com especialidade, diz as duas coisas: "infantaria de montanha".</summary>
    public static string Name(string kind, string spec = "") => KindName(kind) + (spec.Length > 0 ? " " + SpecName(spec) : "");

    private static string KindName(string kind) => kind switch
    {
        "armor" => "blindada",
        "mech" => "mecanizada",
        "airborne" => "pára-quedista",
        "support" => "apoio",
        _ => "infantaria",
    };

    /// <summary>Marca da especialidade em texto, para as linhas dos painéis (o desenho é o Mark()).</summary>
    public static string SpecMark(string spec) => spec switch
    {
        "montanha" => "⛰",
        "anfibio" => "⚓",
        "selva" => "🌿",
        "artico" => "❄",
        "deserto" => "🏜",
        _ => "★",
    };

    /// <summary>Nome da especialidade, para a dica.</summary>
    public static string SpecName(string spec) => spec switch
    {
        "montanha" => "de montanha",
        "anfibio" => "de fuzileiros",
        "selva" => "de selva",
        "artico" => "do ártico",
        "deserto" => "do deserto",
        _ => spec,
    };
}

/// <summary>Chapa com o símbolo NATO, para pôr em linhas de painel ao lado do nome. É Control e não
/// Node2D porque o lugar dela é dentro de HBoxContainer, não no mapa.
///
/// Ignora o rato de propósito: as linhas da fila de produção arrastam-se, e um filho que apanhasse o
/// toque partia o arrasto da encomenda.</summary>
public partial class UnitSymbol : Control
{
    private string _kind = "infantry", _spec = "";

    /// <summary>Etiquetas de um modelo, ou nenhumas se o modelo estiver partido — o painel não rebenta por
    /// causa de uma ficha em falta.</summary>
    private static List<string> TagsOf(World w, int templateId)
    {
        var tags = new List<string>();
        try { tags.AddRange(w.Stats.Get(templateId).Tags); } catch { /* modelo sem etiquetas */ }
        return tags;
    }

    /// <summary>Tipo de um modelo de divisão: sai das etiquetas do modelo (unit_tag), como no mapa. Modelo
    /// sem etiquetas dá o símbolo de apoio, que é o que o contador já fazia — não rebenta.</summary>
    public static string KindFor(World w, int templateId) => UnitCounter.KindOf(TagsOf(w, templateId));

    /// <summary>Especialidade de terreno do modelo (montanha, fuzileiros, selva, ártico, deserto), da mesma
    /// tabela — "" quando é tropa de linha.</summary>
    public static string SpecFor(World w, int templateId) => NatoSymbol.SpecialtyOf(TagsOf(w, templateId));

    public static UnitSymbol Of(string kind, float wide = 30f, float tall = 22f, string spec = "")
    {
        var s = new UnitSymbol
        {
            CustomMinimumSize = new Vector2(wide, tall),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        s.Set(kind, spec);
        return s;
    }

    public static UnitSymbol For(World w, int templateId, float wide = 30f, float tall = 22f)
        => Of(KindFor(w, templateId), wide, tall, SpecFor(w, templateId));

    public void Set(string kind, string spec = "")
    {
        _kind = kind; _spec = spec;
        TooltipText = NatoSymbol.Name(kind, spec);
        QueueRedraw();
    }

    /// <summary>Tipo desenhado agora — a prova do smoke conta-os por aqui.</summary>
    public string Kind => _kind;

    /// <summary>Especialidade desenhada agora ("" se nenhuma).</summary>
    public string Spec => _spec;

    public override void _Draw() => NatoSymbol.Draw(this, new Rect2(Vector2.Zero, Size), _kind, 2f, _spec);
}
