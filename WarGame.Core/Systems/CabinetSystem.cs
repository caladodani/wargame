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
/// Este sistema só trata da folha de salários, da rodagem e das saídas; nomear e demitir é dos comandos.</summary>
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

    /// <summary>Conselheiros que este país pode nomear para uma pasta: os de toda a gente e os dele.</summary>
    public static List<AdvisorDef> Candidates(World w, Country c, string slot) =>
        w.AdvisorDefs.Values
            .Where(a => a.Slot == slot && (a.CountryTag is null || a.CountryTag == c.Tag))
            .OrderBy(a => a.Cost).ThenBy(a => a.Id).ToList();
}
