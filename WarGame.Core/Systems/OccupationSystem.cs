using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Políticas de ocupação (HoI4: occupation laws). Até aqui, tomar terra alheia era sempre a mesma
/// coisa: rendia metade (occupied_yield), criava resistência ao mesmo ritmo em toda a parte e dava homens ao
/// ocupante como se a população fosse dele. Não havia decisão nenhuma — e a ocupação é uma das decisões mais
/// caras que há: espremer a terra ocupada dá aço hoje e revolta amanhã.
///
/// Agora escolhe-se, por país ocupado (não por região: quem manda numa terra manda naquele povo), uma
/// política da tabela occupation_policy. Cada uma mexe em três coisas ao mesmo tempo:
/// · resistência — quão depressa a população se organiza (ResistanceSystem);
/// · rendimento — quanto é que aquela terra rende ao ocupante (EconomySystem);
/// · recrutas — que parte daquela população entra no nosso pool de homens (ManpowerSystem).
/// A política de partida é a de sort mais baixo, e é neutra nas três (multiplicadores a 1): quem não escolhe
/// nada fica exactamente com o jogo de antes.
///
/// Trocar de política não é de graça: passa a valer no dia e só se pode voltar a mexer passados
/// occupation_switch_days — uma ocupação não muda de cara todas as semanas.
///
/// O sistema em si só arruma: deita fora as políticas de ocupações que já acabaram e deixa a IA escolher a
/// sua (aperta quando está em guerra e com o cofre curto, alivia quando a terra está a ferver). Quem lê os
/// multiplicadores são os sistemas de sempre, pelas estáticas daqui.</summary>
public sealed class OccupationSystem : ISystem
{
    public string Name => "Occupation";

    public void Tick(World w)
    {
        if (w.OccupationPolicyDefs.Count == 0) return;

        // 1. ocupações que acabaram (região devolvida, país anexado, dono capitulado): a política vai com elas
        w.Occupations.RemoveAll(o => !w.Countries.ContainsKey(o.CountryId) || !w.Countries.ContainsKey(o.TargetId)
                                     || !w.OccupationPolicyDefs.ContainsKey(o.PolicyId)
                                     || Regions(w, o.CountryId, o.TargetId) == 0);

        // 2. a IA escolhe: aperta a terra ocupada quando a guerra lhe come o cofre, alivia quando ela ferve
        foreach (var c in w.Countries.Values.OrderBy(x => x.Id))
        {
            if (c.IsPlayer || c.Capitulated) continue;
            foreach (int target in Occupied(w, c.Id))
            {
                string want = Heat(w, c.Id, target) >= w.Rule("occupation_ai_calm", 0.5f) ? Softest(w).Id
                    : c.AtWarWith.Count > 0 && c.Money < w.Rule("occupation_ai_broke", 200f) ? Harshest(w).Id
                    : Default(w).Id;
                if (Block(w, c.Id, target, want) is null) Set(w, c.Id, target, want);
            }
        }
    }

    /// <summary>Política em vigor sobre o povo de um país ocupado (a de partida quando ninguém escolheu).</summary>
    public static OccupationPolicyDef Policy(World w, int occupierId, int ownerId)
    {
        var o = w.Occupations.FirstOrDefault(x => x.CountryId == occupierId && x.TargetId == ownerId);
        return o is not null && w.OccupationPolicyDefs.TryGetValue(o.PolicyId, out var def) ? def : Default(w);
    }

    /// <summary>Política que pesa nesta região: a que o controlador aplica ao dono. Terra própria não tem
    /// ocupação nenhuma e leva a de partida, que é neutra.</summary>
    public static OccupationPolicyDef For(World w, Region r) =>
        r.ControllerId == r.OwnerId ? Default(w) : Policy(w, r.ControllerId, r.OwnerId);

    /// <summary>Multiplicador do rendimento desta região (EconomySystem).</summary>
    public static float YieldMult(World w, Region r) => r.ControllerId == r.OwnerId ? 1f : For(w, r).Yield;

    /// <summary>Fatia da população desta região que dá recrutas ao controlador (ManpowerSystem).</summary>
    public static float ManpowerMult(World w, Region r) => r.ControllerId == r.OwnerId ? 1f : For(w, r).Manpower;

    /// <summary>Multiplicador do crescimento da resistência nesta região (ResistanceSystem).</summary>
    public static float ResistanceMult(World w, Region r) => r.ControllerId == r.OwnerId ? 1f : For(w, r).Resistance;

    /// <summary>A política de partida: a de sort mais baixo, neutra nas três contas.</summary>
    public static OccupationPolicyDef Default(World w) =>
        w.OccupationPolicyDefs.Values.OrderBy(d => d.Sort).FirstOrDefault()
        ?? new OccupationPolicyDef("", "Sem política", "", 1f, 1f, 1f, "", 0);

    /// <summary>A mais branda (menos resistência) e a mais dura (mais rendimento): é o que a IA escolhe.</summary>
    private static OccupationPolicyDef Softest(World w) =>
        w.OccupationPolicyDefs.Values.OrderBy(d => d.Resistance).ThenBy(d => d.Sort).FirstOrDefault() ?? Default(w);

    private static OccupationPolicyDef Harshest(World w) =>
        w.OccupationPolicyDefs.Values.OrderByDescending(d => d.Yield).ThenBy(d => d.Sort).FirstOrDefault() ?? Default(w);

    /// <summary>Regiões que este país ocupa a outro (controla mas não são dele).</summary>
    public static int Regions(World w, int occupierId, int ownerId) =>
        w.Regions.Values.Count(r => r.ControllerId == occupierId && r.OwnerId == ownerId && r.ControllerId != r.OwnerId);

    /// <summary>Povos que este país tem debaixo de ocupação, por id.</summary>
    public static List<int> Occupied(World w, int occupierId) =>
        w.Regions.Values.Where(r => r.ControllerId == occupierId && r.OwnerId != occupierId)
            .Select(r => r.OwnerId).Distinct().Where(w.Countries.ContainsKey).OrderBy(x => x).ToList();

    /// <summary>Quanto ferve a terra ocupada de um povo: a resistência média das regiões que lhe tomámos.</summary>
    public static float Heat(World w, int occupierId, int ownerId)
    {
        var mine = w.Regions.Values.Where(r => r.ControllerId == occupierId && r.OwnerId == ownerId
                                               && r.ControllerId != r.OwnerId).ToList();
        return mine.Count == 0 ? 0f : mine.Average(r => r.Resistance);
    }

    /// <summary>Dia em que a política daquele povo foi assinada (−1 = nunca se escolheu nada).</summary>
    public static int Since(World w, int occupierId, int ownerId) =>
        w.Occupations.FirstOrDefault(x => x.CountryId == occupierId && x.TargetId == ownerId)?.SinceDay ?? -1;

    /// <summary>Porque é que este país não pode assinar esta política sobre aquele povo (null = pode). É a
    /// razão que o comando devolve e que o painel mostra por baixo do botão desligado.</summary>
    public static string? Block(World w, int countryId, int targetId, string policyId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Countries.ContainsKey(targetId)) return "povo desconhecido";
        if (!w.OccupationPolicyDefs.ContainsKey(policyId)) return "política desconhecida";
        if (Regions(w, countryId, targetId) == 0) return "não ocupamos terra nenhuma desse povo";
        var have = w.Occupations.FirstOrDefault(x => x.CountryId == countryId && x.TargetId == targetId);
        if (have is null) return null;
        if (have.PolicyId == policyId) return "é a política que já lá está";
        int wait = (int)w.Rule("occupation_switch_days", 30f) - (w.Clock.Day - have.SinceDay);
        return wait > 0 ? $"a ocupação mudou de mãos há pouco (faltam {wait} dias)" : null;
    }

    /// <summary>Assina a política. Único sítio que escreve World.Occupations — o comando do jogador e a IA
    /// passam os dois por aqui.</summary>
    public static void Set(World w, int countryId, int targetId, string policyId)
    {
        if (!w.OccupationPolicyDefs.ContainsKey(policyId)) return;
        var have = w.Occupations.FirstOrDefault(x => x.CountryId == countryId && x.TargetId == targetId);
        if (have is null)
            w.Occupations.Add(new Occupation { CountryId = countryId, TargetId = targetId, PolicyId = policyId, SinceDay = w.Clock.Day });
        else if (have.PolicyId != policyId) { have.PolicyId = policyId; have.SinceDay = w.Clock.Day; }
    }
}
