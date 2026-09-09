using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>
/// A opinião do país (HoI4: popularidade dos partidos, eleições e golpes).
///
/// Até aqui um país só tinha poder político, leis e gabinete — a população não tinha voz nenhuma e o
/// governo era eterno. Agora cada país tem uma opinião que se mexe sozinha todos os dias: a guerra
/// enche os cartazes dos nacionalistas, a instabilidade e o desgaste enchem os dos socialistas e dos
/// autoritários, e quando nada puxa cada partido volta devagar à sua base (rule party_settle). A soma
/// anda sempre em 100 — a opinião é uma só.
///
/// O governo muda de duas maneiras. Nas urnas, de election_years em election_years, se o partido no
/// poder as marca: ganha quem for mais popular nesse dia. E pelo golpe, quando um partido da oposição
/// passa coup_popularity com a estabilidade abaixo de coup_stability — que é a saída dos regimes que
/// não marcam eleições nenhumas. Ambas as mudanças custam estabilidade.
///
/// Quem governa dá o seu multiplicador ao país (World.ApplyParty → Country.PartyMult → Country.Stat).
/// </summary>
public sealed class PartySystem : ISystem
{
    public string Name => "Party";

    /// <summary>Dias de um mandato (rule election_years em anos). Nunca menos de um dia, para o relógio
    /// das eleições não parar em cima de si próprio.</summary>
    public static int Term(World w) => Math.Max(1, (int)MathF.Round(w.Rule("election_years", 4f) * 365f));

    /// <summary>O puxão de hoje neste partido, em pontos: a guerra, a instabilidade abaixo de 50 e o
    /// desgaste de guerra, mais o regresso devagar à base. É o que a UI mostra por baixo da barra.</summary>
    public static float Drift(World w, Country c, PartyDef p)
    {
        float day = w.Rule("party_drift_day", 1f);
        float unstable = MathF.Max(0f, 50f - c.Stability) / 50f;   // 0 = país calmo, 1 = país pelas ruas
        float pull = (c.AtWarWith.Count > 0 ? p.DriftWar : 0f) + unstable * p.DriftUnstable
                   + c.WarExhaustion * p.DriftExhaustion;
        float now = c.Parties.GetValueOrDefault(p.Id);
        float settle = Math.Clamp(p.Base - now, -1f, 1f) * w.Rule("party_settle", 0.02f);
        return pull * day + settle;
    }

    /// <summary>O partido da oposição que hoje está em condições de dar o golpe, ou null. Precisa de
    /// passar coup_popularity com o país abaixo de coup_stability — e de não ser já o governo.</summary>
    public static string? CoupCandidate(World w, Country c)
    {
        if (c.Stability >= w.Rule("coup_stability", 25f)) return null;
        float need = w.Rule("coup_popularity", 60f);
        string? best = null; float top = need;
        foreach (var (id, pop) in c.Parties)
            if (id != c.Party && pop >= top) { top = pop; best = id; }
        return best;
    }

    public void Tick(World w)
    {
        if (w.PartyDefs.Count == 0) return;
        int term = Term(w);

        // cópia da lista: um golpe pode partir o país em dois e acrescentar um país ao mundo a meio da volta
        foreach (var c in w.Countries.Values.ToList())
        {
            if (c.Parties.Count == 0) World.SettleParties(w, c);
            if (c.Capitulated) continue;   // um país capitulado não vota nem se revolta: não tem casa própria

            foreach (var p in w.PartyDefs.Values)
                c.Parties[p.Id] = MathF.Max(0f, c.Parties.GetValueOrDefault(p.Id) + Drift(w, c, p));
            World.NormalizeParties(c);

            var ruling = w.PartyDefs.GetValueOrDefault(c.Party);

            // Golpe primeiro: quem toma o poder pela rua não espera pela data das urnas. Num país com terra
            // que chegue para dois governos, o golpe não muda de cadeiras — parte o país em dois (CivilWar).
            if (CoupCandidate(w, c) is string usurper)
            {
                if (CivilWar.Erupt(w, c, usurper) is Country rebel)
                {
                    w.Events.Publish(new CivilWarBroke(c.Id, rebel.Id, usurper));
                    continue;
                }
                string from = c.Party;
                c.Party = usurper;
                c.Stability = MathF.Max(0f, c.Stability - w.Rule("coup_stability_hit", 15f));
                Schedule(w, c, term);
                World.ApplyParty(w, c);
                w.Events.Publish(new CoupHappened(c.Id, from, usurper));
                continue;
            }

            if (ruling is null || !ruling.Elections) { c.NextElection = 0; continue; }
            if (c.NextElection <= 0) { Schedule(w, c, term); continue; }
            if (w.Clock.Day < c.NextElection) continue;

            // Urnas: ganha o mais popular do dia. Empate resolve-se pelo id, para o mesmo save dar sempre
            // o mesmo governo.
            string winner = c.Parties.OrderByDescending(x => x.Value).ThenBy(x => x.Key).First().Key;
            bool changed = winner != c.Party;
            string was = c.Party;
            c.Party = winner;
            if (changed) c.Stability = MathF.Max(0f, c.Stability - w.Rule("election_stability_hit", 5f));
            Schedule(w, c, term);
            World.ApplyParty(w, c);
            w.Events.Publish(new ElectionHeld(c.Id, was, winner, changed));
        }
    }

    /// <summary>Marca (ou apaga) o relógio das próximas eleições conforme o governo as faça ou não.</summary>
    private static void Schedule(World w, Country c, int term) =>
        c.NextElection = w.PartyDefs.TryGetValue(c.Party, out var p) && p.Elections ? w.Clock.Day + term : 0;
}
