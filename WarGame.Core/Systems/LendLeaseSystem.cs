using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Empréstimo de material (World.LendLeases): todos os dias uma fatia do rendimento de quem
/// empresta sai-lhe do cofre e entra no de quem recebe, sem contrapartida nenhuma e sem prazo.
///
/// O jogo já sabia mandar dinheiro a um aliado — o TransferMoneyCommand — mas era um gesto único: quem
/// queria segurar um aliado a arder tinha de se lembrar dele todos os dias. Uma guerra não se sustenta a
/// gestos avulsos; sustenta-se com uma torneira aberta que se abre num dia e se fecha noutro, e é isso
/// que aqui se assina. A fatia é do *rendimento*, e não do cofre: quem empresta 20% continua a poder
/// gastar o que já tinha, e o que dá cresce e encolhe com a economia dele — perder as minas corta o
/// empréstimo sozinho, sem ninguém ter de o rever.
///
/// Uma parte perde-se pelo caminho (lend_lease_waste): não há como pôr material do outro lado do mundo
/// sem deixar lá metade nos cais e nos comboios, e é isso que faz do empréstimo um esforço e não uma
/// mudança de bolso. O que se perde sai na mesma ao benfeitor — a conta dele é a fatia inteira.
///
/// Cai sozinho quando os dois vão à guerra um com o outro, quando um deles capitula, ou quando o
/// benfeitor fica sem cofre para o dia. Não tem prazo: fecha-se por ordem (CancelLendLeaseCommand).</summary>
public sealed class LendLeaseSystem : ISystem
{
    public string Name => "LendLease";

    public void Tick(World w)
    {
        if (w.LendLeases.Count == 0) return;
        for (int i = w.LendLeases.Count - 1; i >= 0; i--)
        {
            var l = w.LendLeases[i];
            w.Countries.TryGetValue(l.FromId, out var from);
            w.Countries.TryGetValue(l.ToId, out var to);
            if (l.Share <= 0f || w.AreAtWar(l.FromId, l.ToId)
                || from is null || from.Capitulated || to is null || to.Capitulated)
            { End(w, i); continue; }

            float due = Daily(w, l);
            if (due <= 0f) continue;                 // rendimento a zero ou negativo: hoje não parte nada
            if (from.Money < due) { End(w, i); continue; }   // sem cofre para o dia, a torneira fecha
            from.Money -= due;
            float landed = due * (1f - Math.Clamp(w.Rule("lend_lease_waste", 0.2f), 0f, 1f));
            to.Money += landed;
            l.SentTotal += landed;
        }
    }

    private static void End(World w, int index)
    {
        var l = w.LendLeases[index];
        w.LendLeases.RemoveAt(index);
        w.Events.Publish(new LendLeaseEnded(l.FromId, l.ToId, l.SentTotal));
    }

    /// <summary>O que este empréstimo tira hoje ao benfeitor (antes das perdas do caminho).</summary>
    public static float Daily(World w, LendLease l) =>
        MathF.Max(0f, EconomySystem.Income(w, l.FromId)) * l.Share;

    /// <summary>O que chega ao destinatário deste empréstimo, já descontado o que se perde a caminho.</summary>
    public static float Landed(World w, LendLease l) =>
        Daily(w, l) * (1f - Math.Clamp(w.Rule("lend_lease_waste", 0.2f), 0f, 1f));

    /// <summary>Fatia do rendimento que um país já tem prometida em empréstimos.</summary>
    public static float Given(World w, int countryId) =>
        w.LendLeases.Where(l => l.FromId == countryId).Sum(l => l.Share);

    /// <summary>Fatia que ainda pode prometer (o tecto lend_lease_max_share menos o já prometido).</summary>
    public static float FreeShare(World w, int countryId) =>
        MathF.Max(0f, w.Rule("lend_lease_max_share", 0.35f) - Given(w, countryId));

    /// <summary>Pontos por dia que este país está a mandar para fora.</summary>
    public static float Out(World w, int countryId) =>
        w.LendLeases.Where(l => l.FromId == countryId).Sum(l => Daily(w, l));

    /// <summary>Pontos por dia que este país recebe de benfeitores, já descontadas as perdas.</summary>
    public static float In(World w, int countryId) =>
        w.LendLeases.Where(l => l.ToId == countryId).Sum(l => Landed(w, l));

    /// <summary>O empréstimo em vigor deste benfeitor para este destinatário (null se não houver).</summary>
    public static LendLease? Between(World w, int fromId, int toId) =>
        w.LendLeases.FirstOrDefault(l => l.FromId == fromId && l.ToId == toId);

    /// <summary>Este país recebe material daquele? Serve à IA, que não morde a mão que a alimenta.</summary>
    public static bool Benefactor(World w, int benefactorId, int countryId) =>
        w.LendLeases.Any(l => l.FromId == benefactorId && l.ToId == countryId);
}
