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
    /// <summary>Símbolo do tipo dado dentro da moldura, no canvas dado: as mesmas linhas que se desenham
    /// à mão num mapa de estado-maior (✕ infantaria, oval blindado, oval com ✕ mecanizada, ponto apoio,
    /// ✕ com asas pára-quedistas).</summary>
    public static void Draw(CanvasItem ci, Rect2 r, string kind, float thick = 2.6f)
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
    }

    /// <summary>Nome por extenso do tipo — o símbolo diz-se sozinho a quem já leu mapas, e a quem não leu
    /// diz-se pela dica.</summary>
    public static string Name(string kind) => kind switch
    {
        "armor" => "blindada",
        "mech" => "mecanizada",
        "airborne" => "pára-quedista",
        "support" => "apoio",
        _ => "infantaria",
    };
}

/// <summary>Chapa com o símbolo NATO, para pôr em linhas de painel ao lado do nome. É Control e não
/// Node2D porque o lugar dela é dentro de HBoxContainer, não no mapa.
///
/// Ignora o rato de propósito: as linhas da fila de produção arrastam-se, e um filho que apanhasse o
/// toque partia o arrasto da encomenda.</summary>
public partial class UnitSymbol : Control
{
    private string _kind = "infantry";

    /// <summary>Tipo de um modelo de divisão: sai das etiquetas do modelo (unit_tag), como no mapa. Modelo
    /// sem etiquetas dá o símbolo de apoio, que é o que o contador já fazia — não rebenta.</summary>
    public static string KindFor(World w, int templateId)
    {
        var tags = new List<string>();
        try { tags.AddRange(w.Stats.Get(templateId).Tags); } catch { /* modelo sem etiquetas */ }
        return UnitCounter.KindOf(tags);
    }

    public static UnitSymbol Of(string kind, float wide = 30f, float tall = 22f)
    {
        var s = new UnitSymbol
        {
            CustomMinimumSize = new Vector2(wide, tall),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        s.Set(kind);
        return s;
    }

    public static UnitSymbol For(World w, int templateId, float wide = 30f, float tall = 22f)
        => Of(KindFor(w, templateId), wide, tall);

    public void Set(string kind)
    {
        _kind = kind;
        TooltipText = NatoSymbol.Name(kind);
        QueueRedraw();
    }

    /// <summary>Tipo desenhado agora — a prova do smoke conta-os por aqui.</summary>
    public string Kind => _kind;

    public override void _Draw() => NatoSymbol.Draw(this, new Rect2(Vector2.Zero, Size), _kind, 2f);
}
