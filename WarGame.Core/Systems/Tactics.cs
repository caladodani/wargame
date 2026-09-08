using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>As tácticas com que os dois lados se batem hoje nesta região. Até aqui uma batalha era a soma das
/// fichas com o chão por cima: as mesmas divisões no mesmo terreno davam sempre o mesmo resultado, e não havia
/// nada entre a ordem de assaltar e a conta. No HoI4 há — de tempos a tempos cada lado escolhe uma táctica
/// (assalto frontal, flanco, infiltração de um lado; linha firme, emboscada, defesa elástica do outro) e a do
/// outro lado pode LER a nossa e desmontá-la. É o que faz duas batalhas iguais no papel acabarem ao contrário,
/// e é a razão por que se espera mais um dia antes de mandar avançar.
///
/// Como o tempo local, nada disto se guarda: a escolha é uma conta determinista sobre (região, lado, bloco de
/// dias), com o mesmo baralhar de inteiros do Weather. O mesmo dia no mesmo mundo dá sempre as mesmas
/// tácticas — com save ou sem ele, no telefone como no CI.
///
/// A tabela é que manda em tudo: que tácticas existem, de que lado, o que valem, qual lê qual e em que chão
/// só elas aparecem (a ponta de lança quer terreno aberto, a emboscada quer arvoredo). Não há aqui nome de
/// táctica nenhum.
///
/// Não é um ISystem: não tem estado nenhum para adiantar por tick.</summary>
public static class Tactics
{
    /// <summary>A táctica deste lado nesta região neste bloco de dias, ou null se a tabela tactic não veio
    /// carregada (mundos de teste que só carregam meia base de dados batem-se sem tácticas).</summary>
    public static TacticDef? Of(World w, Region r, bool attacking)
    {
        if (w.TacticDefs.Count == 0) return null;
        string side = attacking ? "attacker" : "defender";
        float total = 0f;
        foreach (var t in w.TacticDefs.Values)
            if (Fits(t, r, side)) total += MathF.Max(0f, t.Weight);
        if (total <= 0f) return null;

        float pick = Roll(w, r, attacking) * total, run = 0f;
        TacticDef? last = null;
        foreach (var t in w.TacticDefs.Values.OrderBy(t => t.Sort).ThenBy(t => t.Id))
        {
            if (!Fits(t, r, side)) continue;
            last = t;
            run += MathF.Max(0f, t.Weight);
            if (pick < run) return t;
        }
        return last;   // arredondamentos: a última elegível fica com a ponta do intervalo
    }

    private static bool Fits(TacticDef t, Region r, string side) =>
        t.Side == side && (t.Terrain.Length == 0 || t.Terrain == r.Terrain);

    /// <summary>O sorteio deste lado nesta região neste bloco de dias, entre 0 e 1. O lado entra no baralho
    /// (1 e 2) para os dois não escolherem sempre a mesma coisa ao mesmo tempo.</summary>
    private static float Roll(World w, Region r, bool attacking)
    {
        float days = MathF.Max(1f, w.Rule("tactic_days", 4f));
        int block = (int)MathF.Floor(w.Clock.Day / days);
        return Weather.Hash(r.Id, attacking ? 1 : 2, block);
    }

    /// <summary>Este lado foi lido? É quando a táctica do outro lado tem esta no counter_id — e é aí que a
    /// esperteza sai cara: o que ela valia acima de 1 fica reduzido a tactic_counter_keep.</summary>
    public static bool Countered(World w, Region r, bool attacking) =>
        Of(w, r, attacking) is TacticDef mine && Of(w, r, !attacking) is TacticDef foe && foe.CounterId == mine.Id;

    /// <summary>O que a táctica deste lado vale hoje à força dele: o multiplicador da tabela, já com a leitura
    /// do inimigo por cima. 1 quando não há tabela nenhuma carregada.</summary>
    public static float Mult(World w, Region r, bool attacking)
    {
        if (Of(w, r, attacking) is not TacticDef mine) return 1f;
        if (!Countered(w, r, attacking)) return mine.Mult;
        float keep = Math.Clamp(w.Rule("tactic_counter_keep", 0.25f), 0f, 1f);
        return 1f + (mine.Mult - 1f) * keep;
    }

    /// <summary>Quantos dias falta até os dois lados voltarem a escolher. Quem perde a troca de hoje sabe
    /// quanto tem de aguentar — e essa é a decisão que as tácticas trazem ao jogo.</summary>
    public static int DaysLeft(World w)
    {
        float days = MathF.Max(1f, w.Rule("tactic_days", 4f));
        return (int)MathF.Ceiling(days - w.Clock.Day % days);
    }

    /// <summary>A chapa deste lado para o ecrã de batalha: o desenho, o que a táctica vale já com a leitura do
    /// inimigo, o nome e a frase que explica ambos. Null sem tabela carregada.</summary>
    public static FieldPart? Plate(World w, Region r, bool attacking)
    {
        if (Of(w, r, attacking) is not TacticDef t) return null;
        float m = Mult(w, r, attacking);
        bool read = Countered(w, r, attacking);
        var foe = Of(w, r, !attacking);
        string note = $"{t.Name}: {t.Note}\n"
            + (read
                ? $"O inimigo leu-a com {foe!.Name}: dos ×{t.Mult:0.00} que valia sobram ×{m:0.00}.\n"
                : $"Vale ×{m:0.00} à força deste lado.\n"
                  + (foe is not null && t.CounterId == foe.Id
                      ? $"E lê a {foe.Name} do outro lado: a dele fica em ×{Mult(w, r, !attacking):0.00}.\n"
                      : ""))
            + $"Os dois lados voltam a escolher dentro de {DaysLeft(w)} dia{(DaysLeft(w) == 1 ? "" : "s")}.";
        return new FieldPart(t.Glyph.Length > 0 ? t.Glyph : "espadas", $"×{m:0.00}",
            read ? $"{t.Name.ToLowerInvariant()} (lida)" : t.Name.ToLowerInvariant(), note);
    }

    /// <summary>As duas tácticas numa linha, para a ficha da região e para o --smoke.</summary>
    public static string Line(World w, Region r)
    {
        if (Of(w, r, attacking: true) is not TacticDef a || Of(w, r, attacking: false) is not TacticDef d) return "";
        string Mark(bool attacking) => Countered(w, r, attacking) ? " (lida)" : "";
        return $"{a.Name} ×{Mult(w, r, true):0.00}{Mark(true)} contra {d.Name} ×{Mult(w, r, false):0.00}{Mark(false)}";
    }
}
