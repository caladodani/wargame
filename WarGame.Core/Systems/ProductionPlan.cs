using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma parcela da conta de uma encomenda: a chapa que a mostra, o número como se lê, o nome e a
/// frase que diz o que aquele número faz à data de entrega.</summary>
public readonly record struct ProductionPart(string Glyph, string Value, string Name, string Note);

/// <summary>Porque é que a encomenda demora o que demora. O painel da produção já dizia "~14 dias", mas o
/// número saía de uma conta escrita no próprio painel — a mesma que o ProductionSystem faz, copiada. Duas
/// cópias da mesma conta afastam-se, e quando se afastam é o jogador que aprende a jogar um jogo que não
/// existe.
///
/// Aqui a conta é uma só: o ProductionSystem gasta o que o <see cref="DayOutput"/> diz, o painel mostra o
/// que o <see cref="DayOutput"/> diz, e as parcelas do <see cref="Parts"/> são os factores dessa mesma
/// multiplicação, um a um. É a mesma ideia do GroundSystem (o que o chão tira) e do RegionYield (o que a
/// terra dá), agora do lado da fábrica.</summary>
public static class ProductionPlan
{
    /// <summary>Trabalho de um dia nesta encomenda com `lines` fábricas: custo ÷ dias mínimos, vezes a
    /// indústria do país, vezes as fábricas dedicadas, vezes o jeito que a linha já tem. Sem fábrica não há
    /// trabalho nenhum — o ProductionSystem nem lhe toca.</summary>
    public static float DayOutput(World w, Country c, ProductionOrder o, int lines)
    {
        if (lines <= 0) return 0f;
        float cost = Cost(w, o);
        return cost / MathF.Max(1f, w.Rule("build_min_days", 10f)) * c.Stat("production_speed") * lines * o.Efficiency;
    }

    /// <summary>Fábricas que a encomenda do lugar `index` tem hoje. As fábricas repartem-se de cima para
    /// baixo — cada encomenda por acabar leva as que pediu enquanto houver — por isso o que sobra para esta
    /// depende de quem vem à frente. Zero = está na fila mas hoje não anda.</summary>
    public static int LinesFor(World w, Country c, int index)
    {
        if (index < 0 || index >= c.Queue.Count) return 0;
        var o = c.Queue[index];
        if (o.Progress >= Cost(w, o) - 1e-3f) return 0;      // pronta: espera homens, não linha
        int free = Industry.Of(w, c.Id).Military;
        for (int i = 0; i < index; i++)
        {
            var ahead = c.Queue[i];
            if (ahead.Progress >= Cost(w, ahead) - 1e-3f) continue;
            free -= Math.Max(1, ahead.Factories);
        }
        return free <= 0 ? 0 : Math.Clamp(o.Factories, 1, free);
    }

    /// <summary>Dias que faltam ao ritmo de hoje. −1 = parada (sem fábrica, sem indústria ou sem cofre);
    /// 0 = já feita, à espera de recrutas.</summary>
    public static float Days(World w, Country c, ProductionOrder o, int lines)
    {
        float left = Cost(w, o) - o.Progress;
        if (left <= 1e-3f) return 0f;
        float perDay = DayOutput(w, c, o, lines);
        return perDay <= 0f ? -1f : left / perDay;
    }

    /// <summary>O que trava esta encomenda hoje, se alguma coisa a trava — pela ordem por que o
    /// ProductionSystem tropeça nelas. Null = anda.</summary>
    public static string? Blocked(World w, Country c, ProductionOrder o, int lines)
    {
        float cost = Cost(w, o);
        if (!o.IsKit && o.Progress >= cost - 1e-3f)
            return c.Manpower < cost * w.Rule("manpower_per_cost", 500f) ? "à espera de homens" : null;
        if (lines <= 0) return "à espera de fábrica";
        if (c.Money <= 0f) return "sem cofre";
        return null;
    }

    /// <summary>As parcelas da conta, pela ordem por que se multiplicam. O que aqui está multiplicado dá
    /// exactamente o trabalho de um dia — e o teste que o guarda compara com o que a fábrica gasta.</summary>
    public static List<ProductionPart> Parts(World w, Country c, ProductionOrder o, int lines)
    {
        float cost = Cost(w, o);
        float minDays = MathF.Max(1f, w.Rule("build_min_days", 10f));
        float dias = Days(w, c, o, lines);
        var parts = new List<ProductionPart>
        {
            new("cofre", $"{cost:0.0}", "custo",
                (o.IsKit ? "O que um conjunto de material custa ao cofre — o que arma um batalhão."
                         : "O que a divisão inteira custa ao cofre.")
                + $"\nJá pago: {o.Progress:0.0} ({Percent(o, cost):P0})"),
            new("roda", $"{cost / minDays:0.00}", "por dia à partida",
                $"Uma fábrica sozinha faz {cost / minDays:0.00} por dia: o custo repartido pelos"
              + $" {minDays:0} dias mínimos de montagem (regra build_min_days)."),
            new("fabrica", lines <= 0 ? "0" : $"×{lines}", "fábricas",
                lines <= 0 ? "Sem linha de montagem hoje: as encomendas à frente na fila levaram todas as fábricas."
                           : $"Cada fábrica dedicada vale um dia de trabalho por dia — e sai da conta de quem vem atrás na fila."),
            new("bigorna", $"×{c.Stat("production_speed"):0.00}", "indústria",
                "A indústria do país (production_speed): tecnologia, espíritos e leis entram por aqui."),
            new("estrada", $"×{o.Efficiency:0.00}", "ritmo da linha",
                o.Delivered <= 0
                    ? $"Protótipo: a primeira unidade sai sempre ao ritmo de origem. Da segunda em diante a linha ganha {w.Rule("line_efficiency_gain", 0.03f):P0} por dia de trabalho, até {w.Rule("line_efficiency_max", 1.5f):P0}."
                    : $"{o.Delivered} entregue{(o.Delivered == 1 ? "" : "s")} nesta linha. Ganha {w.Rule("line_efficiency_gain", 0.03f):P0} por dia de trabalho até {w.Rule("line_efficiency_max", 1.5f):P0}; parada, arrefece {w.Rule("line_efficiency_decay", 0.02f):P0} por dia."),
            o.IsKit
                ? new("caixa", $"{Cost(w, o):0.0}", "por conjunto",
                    "Uma linha de material não leva homens nenhuns: entrega conjuntos ao armazém e volta a começar."
                  + " São eles que repõem as divisões gastas (EquipmentSystem).")
                : new("gente", $"{cost * w.Rule("manpower_per_cost", 500f) / 1000f:0.0}k", "homens",
                    "Os recrutas que a divisão leva ao sair da fábrica. Sem eles no pool, a encomenda fica feita à espera."),
            new("sol", dias < 0f ? "—" : dias <= 0f ? "pronta" : $"{MathF.Ceiling(dias):0}", "dias",
                dias < 0f ? "Parada: ao ritmo de hoje não sai nunca — dar-lhe uma data seria mentir."
                          : $"O que falta ({cost - o.Progress:0.0}) dividido pelo trabalho de um dia ({DayOutput(w, c, o, lines):0.00})."),
        };
        return parts;
    }

    /// <summary>A mesma conta em texto corrido, para o tooltip de uma linha da fila.</summary>
    public static string Why(World w, Country c, ProductionOrder o, int lines)
    {
        var parts = Parts(w, c, o, lines);
        string head = Blocked(w, c, o, lines) is string b ? b + "\n" : "";
        return head + string.Join("\n", parts.Select(p => $"· {p.Name}: {p.Value}"));
    }

    private static float Percent(ProductionOrder o, float cost) => cost <= 0f ? 0f : Math.Clamp(o.Progress / cost, 0f, 1f);

    private static float Cost(World w, ProductionOrder o) => w.OrderCost(o);
}
