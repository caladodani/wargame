using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma parcela do poder político do dia: de onde vem (ou para onde foge) e quanto.</summary>
public readonly record struct PoliticalPart(string Label, float Points);

/// <summary>Poder político: a segunda moeda do país, e a que paga tudo o que não é aço.
///
/// Até aqui uma lei, um conselheiro, um pacto e uma decisão saíam do mesmo bolso que as divisões e as
/// fábricas — pontos de produção. Isso é o contrário do que faz o HoI4 funcionar: lá o poder político é
/// escasso de propósito e nunca se converte em material, por isso mudar a lei do serviço militar é uma
/// escolha e não uma compra. Um país rico comprava aqui o gabinete inteiro no primeiro mês; agora não
/// compra, porque o cofre não serve para isso.
///
/// A conta do dia: base (regra political_base) × o que o país sabe fazer de política (stat
/// political_gain: leis, conselheiros, espíritos e tecnologias entram por aqui) × a estabilidade — um
/// governo que não se aguenta em pé não legisla — e mais um pouco em guerra, que é quando as câmaras
/// aprovam o que em paz não passava. Tecto na regra political_max: o que não se gasta perde-se, e é isso
/// que obriga a decidir.</summary>
public sealed class PoliticsSystem : ISystem
{
    public string Name => "Politics";

    public void Tick(World w)
    {
        float max = w.Rule("political_max", 1500f);
        foreach (var c in w.Countries.Values)
        {
            if (c.Capitulated) { c.Political = 0f; continue; }
            c.Political = MathF.Min(max, c.Political + Gain(w, c.Id));
        }
    }

    /// <summary>O poder político que este país ganha por dia.</summary>
    public static float Gain(World w, int countryId) => Parts(w, countryId).Sum(p => p.Points);

    /// <summary>De onde vem o poder político do dia — as parcelas que o mostrador abre ao dedo.</summary>
    public static List<PoliticalPart> Parts(World w, int countryId)
    {
        var parts = new List<PoliticalPart>();
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return parts;

        float basePp = w.Rule("political_base", 2f);
        parts.Add(new PoliticalPart("gabinete em funções", basePp));

        // o que o país sabe fazer de política: leis, conselheiros, espíritos e tecnologia, todos pelo
        // mesmo stat — nenhum deles precisa de linha de código sua
        float mult = c.Stat("political_gain");
        if (MathF.Abs(mult - 1f) > 0.001f)
            parts.Add(new PoliticalPart($"leis e conselheiros ×{mult:0.00}", basePp * (mult - 1f)));

        // estabilidade: 50 é neutro, e o desvio dá ou tira. Um governo em colapso não faz aprovar nada
        float stab = (c.Stability - 50f) / 100f * w.Rule("political_stability_weight", 1f) * basePp;
        if (MathF.Abs(stab) > 0.001f)
            parts.Add(new PoliticalPart($"estabilidade {c.Stability:0}%", stab));

        // guerra: as câmaras aprovam em três dias o que em paz levava três anos
        if (c.AtWarWith.Count > 0)
        {
            float war = basePp * w.Rule("political_war_bonus", 0.5f);
            if (MathF.Abs(war) > 0.001f) parts.Add(new PoliticalPart("país em guerra", war));
        }

        return parts;
    }

    /// <summary>Tem com que pagar? A pergunta que todos os comandos políticos fazem antes de agir.</summary>
    public static bool CanPay(World w, int countryId, float cost) =>
        w.Countries.TryGetValue(countryId, out var c) && c.Political >= cost;

    /// <summary>A recusa em palavras, com o que falta — a mesma frase em todo o lado.</summary>
    public static string Short(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "sem país";
        return $"{c.Political:0} de poder político (+{Gain(w, countryId):0.0}/dia, tecto {w.Rule("political_max", 1500f):0})";
    }
}
