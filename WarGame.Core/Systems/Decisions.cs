using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// A folha de decisões de um país (HoI4: o ecrã das decisões e das missões).
///
/// Até aqui uma decisão era um multiplicador com preço em poder político, e havia três. O que faltava era
/// o que no HoI4 lhes dá vida: a PORTA (uma decisão só aparece quando faz sentido — bloqueio naval sem
/// marinha não é decisão nenhuma), o PREÇO em várias moedas, e a MISSÃO com prazo, que se ganha ou se
/// perde. Este ficheiro é a leitura toda: o que se pode assinar hoje, quanto custa, o que falta, e como
/// vai a missão que está a correr.
///
/// Estado derivado: não guarda nada e não é ISystem. Quem guarda são o World.ActiveDecisions (com o dia
/// do prazo e a leitura de partida da meta) e o DecisionSystem, que resolve os prazos.
///
/// O vocabulário das métricas (Metric) serve os dois lados — as portas da decision_req e as metas das
/// missões —, por isso uma métrica nova serve logo para as duas coisas.
/// </summary>
public static class Decisions
{
    /// <summary>Leitura de uma métrica do país. É a mesma linguagem das portas e das metas: quem semeia
    /// uma decisão nova escolhe daqui, e nenhuma decisão precisa de código.</summary>
    public static float Metric(World w, Country c, string key)
    {
        if (key.StartsWith("party:", StringComparison.Ordinal))
            return c.Parties.GetValueOrDefault(key[6..]);
        if (key.StartsWith("ruling:", StringComparison.Ordinal))
            return c.Party == key[7..] ? 1f : 0f;

        switch (key)
        {
            case "war": return c.AtWarWith.Count;
            case "tension": return WorldTension.Of(w);
            case "stability": return c.Stability;
            case "political": return c.Political;
            case "money": return c.Money;
            case "manpower": return c.Manpower;
            case "exhaustion": return c.WarExhaustion;
            case "techs": return c.Techs.Count;
            case "nukes": return c.Nukes;
            case "air": return c.AirPower;
            case "navy": return c.Warships;
            case "divisions": return w.Divisions.Values.Count(d => d.CountryId == c.Id);
            case "regions": return w.Regions.Values.Count(r => r.ControllerId == c.Id);
            case "occupied": return w.Regions.Values.Count(r => r.ControllerId == c.Id && r.OwnerId != c.Id);
            case "factories_civil": return Industry.Of(w, c.Id).Civil;
            case "factories_mil": return Industry.Of(w, c.Id).Military;
            // a revolta média da terra que administramos, em pontos de 0 a 100 (a Region.Resistance é 0..1);
            // sem terra ocupada não há revolta nenhuma para medir
            case "resistance":
            {
                float sum = 0f; int n = 0;
                foreach (var r in w.Regions.Values)
                    if (r.ControllerId == c.Id && r.OwnerId != c.Id) { sum += r.Resistance * 100f; n++; }
                return n == 0 ? 0f : sum / n;
            }
            default: return 0f;
        }
    }

    /// <summary>Nome curto da métrica, para o cartão e para o motivo do bloqueio poderem falar por
    /// palavras em vez de mostrarem a chave crua.</summary>
    public static string MetricName(World w, string key)
    {
        if (key.StartsWith("party:", StringComparison.Ordinal))
            return w.PartyDefs.TryGetValue(key[6..], out var p) ? $"apoio a {p.Name}" : "apoio ao partido";
        if (key.StartsWith("ruling:", StringComparison.Ordinal))
            return w.PartyDefs.TryGetValue(key[7..], out var g) ? $"governo de {g.Name}" : "o governo";
        return key switch
        {
            "war" => "guerras a decorrer", "tension" => "tensão mundial", "stability" => "estabilidade",
            "political" => "poder político", "money" => "cofre", "manpower" => "homens",
            "exhaustion" => "desgaste de guerra", "techs" => "tecnologias", "nukes" => "ogivas",
            "air" => "aviões", "navy" => "navios", "divisions" => "divisões", "regions" => "regiões",
            "occupied" => "terra ocupada", "factories_civil" => "fábricas civis",
            "factories_mil" => "fábricas militares", "resistance" => "revolta na retaguarda",
            _ => key,
        };
    }

    /// <summary>O que esta decisão multiplica enquanto corre.</summary>
    public static IReadOnlyList<(string Key, float Mult)> Effects(World w, string decisionId) =>
        w.DecisionEffects.TryGetValue(decisionId, out var list) ? list : Array.Empty<(string, float)>();

    /// <summary>A decisão que este país tem a correr, ou null.</summary>
    public static ActiveDecision? Active(World w, int countryId, string decisionId) =>
        w.ActiveDecisions.FirstOrDefault(a => a.CountryId == countryId && a.DecisionId == decisionId);

    /// <summary>Porque é que não se pode assinar hoje, ou null se se pode. É a MESMA porta para o comando
    /// e para o ecrã: o botão nunca oferece o que o comando recusa, e o cartão cinzento diz porquê.</summary>
    public static string? Check(World w, Country c, DecisionDef def)
    {
        if (c.Capitulated) return "o país está capitulado";
        if (Active(w, c.Id, def.Id) is not null) return "já está a correr";
        if (c.DecisionCooldownUntil.TryGetValue(def.Id, out var until) && until > w.Clock.Day)
            return $"em espera mais {until - w.Clock.Day} dias";
        if (c.Political < def.Cost) return $"faltam {def.Cost - c.Political:0} de poder político";
        if (def.Money > 0f && c.Money < def.Money) return $"faltam {def.Money - c.Money:0} do cofre";
        if (def.Manpower > 0f && c.Manpower < def.Manpower) return $"faltam {def.Manpower - c.Manpower:0} homens";
        if (def.Stability > 0f && c.Stability < def.Stability) return "o país não aguenta o custo em estabilidade";

        foreach (var r in Reqs(w, def.Id))
        {
            float have = Metric(w, c, r.Key);
            if (r.Min is float min && have < min) return $"{MetricName(w, r.Key)}: {have:0} de {min:0} precisos";
            if (r.Max is float max && have > max) return $"{MetricName(w, r.Key)}: {have:0}, e o limite é {max:0}";
        }
        return null;
    }

    /// <summary>As portas desta decisão.</summary>
    public static IReadOnlyList<DecisionReq> Reqs(World w, string decisionId) =>
        w.DecisionReqs.TryGetValue(decisionId, out var list) ? list : Array.Empty<DecisionReq>();

    /// <summary>As decisões de uma categoria, pela ordem da tabela, com o motivo do bloqueio ao lado
    /// (null = pode assinar-se). Mostram-se TODAS, incluindo as fechadas: esconder uma decisão é esconder
    /// uma razão para jogar de outra maneira — a mesma lei das peças fechadas das pranchetas.</summary>
    public static List<(DecisionDef Def, string? Blocked)> InCategory(World w, Country c, string category) =>
        w.DecisionDefs.Values.Where(d => d.Category == category)
            .OrderBy(d => d.Sort).ThenBy(d => d.Id)
            .Select(d => (d, Check(w, c, d))).ToList();

    /// <summary>Quantas se podem assinar hoje neste país.</summary>
    public static int Ready(World w, Country c) => w.DecisionDefs.Values.Count(d => Check(w, c, d) is null);

    /// <summary>As missões deste país a decorrer.</summary>
    public static List<ActiveDecision> Missions(World w, int countryId) =>
        w.ActiveDecisions.Where(a => a.MissionUntil >= 0 && a.CountryId == countryId).ToList();

    /// <summary>Quanto já andou a meta desta missão, de 0 a 1. Serve a barra do cartão; uma meta a descer
    /// (goal_value negativo) conta na mesma para a frente.</summary>
    public static float GoalProgress(World w, Country c, DecisionDef def, ActiveDecision a)
    {
        if (!def.IsMission || MathF.Abs(def.GoalValue) < 1e-4f) return 1f;
        float moved = Metric(w, c, def.GoalKey) - a.GoalBase;
        return Math.Clamp(moved / def.GoalValue, 0f, 1f);
    }

    /// <summary>A meta já está cumprida? Uma missão cumpre-se no dia em que a métrica lá chega, mesmo
    /// antes do prazo — quem cumpre cedo não fica à espera do castigo.</summary>
    public static bool GoalMet(World w, Country c, DecisionDef def, ActiveDecision a) =>
        def.IsMission && (Metric(w, c, def.GoalKey) - a.GoalBase) >= def.GoalValue - 1e-4f;

    /// <summary>A meta desta missão por palavras, com o andamento: "fábricas civis +2 (vai em +1)".</summary>
    public static string GoalText(World w, Country c, DecisionDef def, ActiveDecision a)
    {
        float moved = Metric(w, c, def.GoalKey) - a.GoalBase;
        return $"{MetricName(w, def.GoalKey)} {def.GoalValue:+0.#;-0.#} (vai em {moved:+0.#;-0.#;0})";
    }

    /// <summary>O preço por palavras, só com as moedas que esta decisão pede.</summary>
    public static string CostText(DecisionDef def)
    {
        var parts = new List<string>();
        if (def.Cost > 0f) parts.Add($"{def.Cost:0} pp");
        if (def.Money > 0f) parts.Add($"{def.Money:0} do cofre");
        if (def.Manpower > 0f) parts.Add($"{def.Manpower:0} homens");
        if (def.Stability > 0f) parts.Add($"{def.Stability:0} de estabilidade");
        if (def.Stability < 0f) parts.Add($"dá {-def.Stability:0} de estabilidade");
        return parts.Count == 0 ? "de graça" : string.Join(" + ", parts);
    }

    /// <summary>O efeito por palavras, para o cartão: "indústria ×1,15 · produção ×1,05".</summary>
    public static string EffectText(World w, DecisionDef def)
    {
        var parts = Effects(w, def.Id).Select(e => $"{StatName(w, e.Key)} ×{e.Mult:0.00}");
        return string.Join(" · ", parts);
    }

    /// <summary>Nome de uma característica de país; cai para a chave crua se a tabela não a nomear.</summary>
    public static string StatName(World w, string key) =>
        w.CountryStatDefs.TryGetValue(key, out var d) ? d.Name : key;

    /// <summary>Uma linha de prova para o headless: quantas decisões há, em quantas categorias, quantas
    /// estão abertas hoje ao jogador e quantas missões correm no mundo.</summary>
    public static string Smoke(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "sem país";
        int missions = w.ActiveDecisions.Count(a => a.MissionUntil >= 0);
        int timed = w.DecisionDefs.Values.Count(d => d.IsMission);
        return $"{w.DecisionDefs.Count} decisões em {w.DecisionCategories.Count} categorias "
             + $"({Ready(w, c)} ao alcance, {timed} com prazo, {w.ActiveDecisions.Count(a => a.CountryId == countryId)} a correr, "
             + $"{missions} missões no mundo)";
    }
}
