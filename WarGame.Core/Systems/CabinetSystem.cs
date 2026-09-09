using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>O gabinete civil (HoI4: political advisors). O país já tinha estado-maior — comandantes
/// contratados que valem no campo — mas não tinha governo nenhum: a indústria, os laboratórios, a recruta e a
/// terra ocupada rendiam sempre o mesmo, e não havia uma única escolha civil para fazer entre guerras.
///
/// Agora há quatro pastas (tabela cabinet_slot) e uma cadeira em cada uma. Sentar um conselheiro custa a
/// nomeação de uma vez e, todos os dias, um salário: advisor_wage_share do que ele custou. Enquanto está
/// sentado, multiplica os stats do país (advisor_effect). Quem não tiver com que lhe pagar vê-o sair pela
/// porta — o gabinete de um país falido esvazia-se sozinho, por ordem de pasta, até as contas darem.
///
/// Um conselheiro também ganha rodagem: quanto mais tempo serve, mais vale o que faz (World.CabinetTenure,
/// regras advisor_tenure_days e advisor_tenure_bonus). Trocar de homem todos os meses é deitar isso fora.
///
/// E desde 0.3.87 o ministro tem COR POLÍTICA (advisor.party/advisor.drift). Quem se senta não traz só os
/// números da sua pasta — traz os cartazes do partido dele: todos os dias puxa a opinião do país para esse
/// lado (PartySystem.Drift pergunta aqui por PartyPull) e mexe na estabilidade conforme seja gente do
/// governo ou da oposição sentada à mesa dele. É o gabinete do HoI4: contratar um ideólogo é escolher para
/// onde o país vai derivar, e um gabinete cheio de rivais é um golpe a preparar-se em casa. Os técnicos
/// (party NULL) não puxam nada — são a escolha de quem quer os números e não quer mexer na rua.
///
/// Este sistema só trata da folha de salários, da rodagem, da política do gabinete e das saídas; nomear e
/// demitir é dos comandos.</summary>
public sealed class CabinetSystem : ISystem
{
    public string Name => "Cabinet";

    public void Tick(World w)
    {
        float share = MathF.Max(0f, w.Rule("advisor_wage_share", 0.01f));
        if (share <= 0f) return;

        foreach (var c in w.Countries.Values)
        {
            if (c.Cabinet.Count == 0) continue;
            World.ApplyCabinet(w, c);                 // a rodagem cresce com os dias de casa: recontar todos os dias
            if (!c.Capitulated)                       // um país ocupado não tem governo próprio para lhe fazer frente
                c.Stability = Math.Clamp(c.Stability + StabilityShift(w, c), 0f, 100f);
            float wages = Wages(w, c);
            c.Money -= wages;
            if (c.Money >= 0f) continue;

            // cofre no vermelho: os conselheiros vão-se embora, do mais caro para o mais barato, até dar
            foreach (var slot in c.Cabinet.Keys
                         .OrderByDescending(s => w.AdvisorDefs.TryGetValue(c.Cabinet[s], out var a) ? a.Cost : 0f)
                         .ThenBy(s => s).ToList())
            {
                if (c.Money >= 0f) break;
                string id = c.Cabinet[slot];
                c.Money += Wage(w, id, share);            // o dia que não se pagou também não se cobra
                c.Cabinet.Remove(slot); c.CabinetSince.Remove(slot);
                w.Events.Publish(new AdvisorLeft(c.Id, id, slot, true));
            }
            World.ApplyCabinet(w, c);
            if (c.Money < 0f) c.Money = 0f;               // gabinete vazio e ainda a dever: o resto é dívida perdoada
        }
    }

    /// <summary>Salário diário de um conselheiro: uma fatia do que custou nomeá-lo.</summary>
    public static float Wage(World w, string advisorId, float share) =>
        w.AdvisorDefs.TryGetValue(advisorId, out var a) ? a.Cost * share : 0f;

    /// <summary>Folha de salários do gabinete deste país, por dia.</summary>
    public static float Wages(World w, Country c)
    {
        float share = MathF.Max(0f, w.Rule("advisor_wage_share", 0.01f));
        return c.Cabinet.Values.Sum(id => Wage(w, id, share));
    }

    /// <summary>Os ministros sentados que têm cor política (os técnicos ficam de fora).</summary>
    public static IEnumerable<AdvisorDef> Ministers(World w, Country c) =>
        c.Cabinet.Values.Select(id => w.AdvisorDefs.GetValueOrDefault(id))
                        .OfType<AdvisorDef>()
                        .Where(a => !string.IsNullOrEmpty(a.Party));

    /// <summary>O puxão que o gabinete dá HOJE a este partido, em pontos de opinião por dia: soma do que
    /// cada ministro dessa cor puxa, pela regra advisor_drift_day. Zero num gabinete de técnicos.</summary>
    public static float PartyPull(World w, Country c, string partyId) =>
        string.IsNullOrEmpty(partyId) ? 0f
        : Ministers(w, c).Where(a => a.Party == partyId).Sum(a => a.Drift)
          * w.Rule("advisor_drift_day", 1f);

    /// <summary>Ministros da oposição sentados à mesa do governo: cada um é uma voz que manda no Estado
    /// sem ser do partido que ganhou. É esta conta que o país paga em estabilidade.</summary>
    public static int Rivals(World w, Country c) => Ministers(w, c).Count(a => a.Party != c.Party);

    /// <summary>Estabilidade por dia que o gabinete dá ou tira: os do partido do governo seguram a casa,
    /// os da oposição abanam-na. Um gabinete só de técnicos não mexe em nada.</summary>
    public static float StabilityShift(World w, Country c)
    {
        int rivals = Rivals(w, c), loyal = Ministers(w, c).Count() - rivals;
        return loyal * w.Rule("advisor_loyal_stability", 0.01f)
             - rivals * w.Rule("advisor_rival_stability", 0.02f);
    }

    /// <summary>Conselheiros que este país pode nomear para uma pasta: os de toda a gente e os dele.</summary>
    public static List<AdvisorDef> Candidates(World w, Country c, string slot) =>
        w.AdvisorDefs.Values
            .Where(a => a.Slot == slot && (a.CountryTag is null || a.CountryTag == c.Tag))
            .OrderBy(a => a.Cost).ThenBy(a => a.Id).ToList();
}
