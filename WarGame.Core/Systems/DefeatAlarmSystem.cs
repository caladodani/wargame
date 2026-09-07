using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>O alarme de derrota (HoI4: o aviso de batalha perdida, com sirene). Até aqui perder uma batalha
/// era uma linha de toast igual a todas as outras: passava em quatro segundos e mais nada acontecia. Quem
/// estivesse a olhar para a fila de produção não dava por nada — nem da primeira derrota, nem da terceira
/// seguida, que é quando a frente está mesmo a ceder.
///
/// Agora cada batalha perdida conta: a série de derrotas do país sobe (uma vitória põe-na a zero) e, quando
/// chega à regra defeat_streak_alarm, o país entra em alarme — o desgaste de guerra sobe defeat_exhaustion,
/// a crónica escreve o revés e a UI toca o klaxon. Só ouve o barramento; quem manda nas batalhas é o
/// CombatSystem.</summary>
public sealed class DefeatAlarmSystem : ISystem
{
    public string Name => "DefeatAlarm";
    private World? _bound;

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) Bind(w);
    }

    /// <summary>Liga-se ao barramento. Idempotente por mundo: um save carregado traz um World novo e volta a
    /// subscrever; o mesmo mundo nunca subscreve duas vezes.</summary>
    public void Bind(World w)
    {
        _bound = w;
        w.Events.Subscribe<BattleEnded>(e => OnBattle(w, e));
    }

    private static void OnBattle(World w, BattleEnded e)
    {
        int loserId = e.AttackerWon ? e.DefenderCountryId : e.AttackerCountryId;
        int winnerId = e.AttackerWon ? e.AttackerCountryId : e.DefenderCountryId;
        // Quem ganha corta a série de derrotas: o alarme é de quem está a perder agora, não de quem perdeu.
        if (w.Countries.TryGetValue(winnerId, out var won)) won.DefeatStreak = 0;
        if (!w.Countries.TryGetValue(loserId, out var c)) return;

        c.DefeatStreak++;
        c.LastDefeatDay = w.Clock.Day;
        c.LastDefeatRegion = e.RegionId;
        // Perder a defender é perder o chão: o evento sai antes de a região mudar de dono (CombatSystem),
        // por isso o que se pergunta é quem estava a atacar e não quem manda lá agora.
        bool ground = e.AttackerWon;

        int need = Math.Max(1, (int)w.Rule("defeat_streak_alarm", 3f));
        bool alarm = c.DefeatStreak >= need;
        if (alarm)
            c.WarExhaustion = MathF.Min(w.Rule("exhaustion_max", 30f),
                                        c.WarExhaustion + w.Rule("defeat_exhaustion", 1.5f));
        w.Events.Publish(new BattleLost(loserId, e.RegionId, c.DefeatStreak, ground, alarm));
    }

    /// <summary>A derrota ainda está fresca? (regra alert_defeat_days a contar do dia em que foi.)</summary>
    public static bool Fresh(World w, Country c) =>
        c.LastDefeatDay >= 0 && w.Clock.Day - c.LastDefeatDay <= (int)w.Rule("alert_defeat_days", 7f);
}
