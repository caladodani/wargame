using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma razão, com nome e chapa, e quanto ela vale neste par de países. Positivo aproxima,
/// negativo afasta.</summary>
public readonly record struct OpinionLine(string Kind, string Name, string Glyph, float Value);

/// <summary>A OPINIÃO ENTRE PAÍSES: o que um governo sente por outro.
///
/// O mundo tinha tensão mundial — um número só, igual para toda a gente — e nenhuma relação bilateral: a
/// Rússia estava exactamente tão longe da NATO como Portugal de Espanha, e a diplomacia toda cabia em três
/// perguntas de sim ou não (há guerra? há pacto? há facção?). No HoI4 cada par de países tem uma opinião, e
/// é ela que abre e fecha as portas: quem assina um pacto, quem entra numa aliança, quem aceita voluntários
/// nossos, e a quem é que a IA vai bater.
///
/// A opinião NÃO se guarda. Refaz-se do estado do mundo, razão a razão, como a folha dos números do país
/// (<see cref="StatLedger"/>) refaz cada característica pela origem — por isso não há save novo, não há
/// migração e não pode haver desacordo entre o número e a lista que o explica: o número É a soma da lista.
///
/// A conta é DIRIGIDA: o que A sente por B não tem de ser o que B sente por A. Quem recebe material todos
/// os dias é que fica agradecido; quem tem o vizinho maior à porta é que tem medo.
///
/// Os pesos, os nomes e as chapas vêm da tabela `opinion_source` — o C# só mede QUANTAS vezes é que cada
/// razão conta neste par. Uma razão nova é uma linha de SQL mais o caso que a mede; uma tabela vazia devolve
/// opinião zero para toda a gente, e o jogo comporta-se exactamente como antes de isto existir.
///
/// Estado derivado: não guarda nada, não entra no save e não é ISystem.</summary>
public static class Relations
{
    /// <summary>O tecto da opinião, para cima e para baixo.</summary>
    public static float Cap(World w) => MathF.Max(1f, w.Rule("opinion_cap", 100f));

    /// <summary>A morada do governo deste país no eixo político, ou null se não tem governo com linha na
    /// tabela dos partidos (um mundo de teste, um país acabado de nascer).</summary>
    public static float? Axis(World w, Country c) =>
        c.Party.Length > 0 && w.PartyDefs.TryGetValue(c.Party, out var p) ? p.Axis : null;

    private static void Add(World w, List<OpinionLine> lines, string id, float times)
    {
        if (times == 0f || !w.OpinionSources.TryGetValue(id, out var s)) return;
        float v = s.Weight * times;
        if (MathF.Abs(v) < 0.05f) return;                 // razão que não move o ponteiro não enche a lista
        lines.Add(new OpinionLine(s.Id, s.Name, s.Glyph, v));
    }

    /// <summary>Todas as razões por que `from` sente o que sente por `to`, pela ordem da tabela.</summary>
    public static List<OpinionLine> Lines(World w, int fromId, int toId)
    {
        var lines = new List<OpinionLine>();
        if (fromId == toId || !w.Countries.TryGetValue(fromId, out var a)
                           || !w.Countries.TryGetValue(toId, out var b)) return lines;

        if (w.AreAtWar(fromId, toId)) Add(w, lines, "guerra", 1f);

        // ideologia: +1 quando os dois governos moram no mesmo sítio do eixo, -1 nas pontas opostas
        if (Axis(w, a) is float ax && Axis(w, b) is float bx) Add(w, lines, "ideologia", 1f - MathF.Abs(ax - bx));

        if (w.SameFaction(fromId, toId)) Add(w, lines, "faccao", 1f);
        if (a.AtWarWith.Any(e => e != toId && w.AreAtWar(toId, e))) Add(w, lines, "inimigo_comum", 1f);
        if (w.HasPact(fromId, toId)) Add(w, lines, "pacto", 1f);
        if (a.OverlordId == toId || b.OverlordId == fromId) Add(w, lines, "vassalagem", 1f);

        // o material que ELE nos manda: é quem recebe que fica agradecido, e é por isto que a conta é dirigida
        if (w.LendLeases.Any(l => l.FromId == toId && l.ToId == fromId)) Add(w, lines, "emprestimo", 1f);

        int deals = w.TradeDeals.Count(t => (t.BuyerId == fromId && t.SellerId == toId)
                                         || (t.BuyerId == toId && t.SellerId == fromId));
        if (deals > 0) Add(w, lines, "comercio", MathF.Min(deals, MathF.Max(0f, w.Rule("opinion_trade_max", 3f))));

        // sangue deles a morrer na nossa guerra
        if (w.Divisions.Values.Any(d => d.VolunteerFrom == toId && d.CountryId == fromId)) Add(w, lines, "voluntarios", 1f);

        // a diplomacia que ELES fazem por nós: embaixadas abertas e independência garantida (DiploDriveSystem).
        // Aqui o "times" já são pontos feitos — o peso da tabela serve para afinar toda a diplomacia de uma vez.
        Add(w, lines, "esforco", DiploDriveSystem.Earned(w, toId, fromId, "opiniao"));
        Add(w, lines, "garantia", DiploDriveSystem.Earned(w, toId, fromId, "garantia"));
        if (DiploDriveSystem.Meddling(w, toId, fromId)) Add(w, lines, "interferencia", 1f);

        if (b.JustifyTarget == fromId) Add(w, lines, "justificacao", 1f);
        if (w.ActiveSpyOps.Any(o => o.CountryId == toId && o.TargetCountryId == fromId)) Add(w, lines, "espionagem", 1f);

        // terra nossa com a tropa deles em cima, pela fatia que é
        int mine = 0, taken = 0;
        foreach (var r in w.Regions.Values)
        {
            if (r.OwnerId != fromId) continue;
            mine++;
            if (r.ControllerId == toId) taken++;
        }
        if (taken > 0) Add(w, lines, "terra_ocupada", (float)taken / MathF.Max(1, mine));

        if (w.SharesBorder(fromId, toId)) Add(w, lines, "vizinhanca", 1f);

        // o medo do que o vizinho tem em pé: só conta o exército que é maior do que o nosso
        int ours = 0, theirs = 0;
        foreach (var d in w.Divisions.Values)
        {
            if (d.CountryId == fromId) ours++;
            else if (d.CountryId == toId) theirs++;
        }
        if (theirs > ours) Add(w, lines, "ameaca", Math.Clamp((theirs - ours) / (float)Math.Max(1, ours), 0f, 1f));

        return lines;
    }

    /// <summary>O que `from` sente por `to`, de -cap a +cap. É a soma das razões e nada mais.</summary>
    public static float Opinion(World w, int fromId, int toId)
    {
        float sum = 0f;
        foreach (var l in Lines(w, fromId, toId)) sum += l.Value;
        float cap = Cap(w);
        return Math.Clamp(sum, -cap, cap);
    }

    /// <summary>A opinião de cada país do mundo sobre este — de uma vez, para o mapa não ter de perguntar
    /// província a província. Só países vivos e nunca ele próprio.</summary>
    public static Dictionary<int, float> Board(World w, int aboutId)
    {
        var board = new Dictionary<int, float>();
        foreach (var c in w.Countries.Values)
            if (c.Id != aboutId && !c.Capitulated) board[c.Id] = Opinion(w, c.Id, aboutId);
        return board;
    }

    /// <summary>A palavra que se põe ao lado do número: o jogador lê "hostil" antes de ler "-38".</summary>
    public static string Word(World w, float opinion)
    {
        float cap = Cap(w);
        return opinion switch
        {
            _ when opinion >= cap * 0.6f => "aliado natural",
            _ when opinion >= cap * 0.25f => "amigável",
            _ when opinion > -cap * 0.25f => "indiferente",
            _ when opinion > -cap * 0.6f => "hostil",
            _ => "inimigo",
        };
    }

    /// <summary>Quem gosta mais e quem gosta menos de nós, do melhor para o pior.</summary>
    public static List<(Country Other, float Opinion)> Ranked(World w, int aboutId) =>
        Board(w, aboutId).Where(p => w.Countries.ContainsKey(p.Key))
                         .Select(p => (w.Countries[p.Key], p.Value))
                         .OrderByDescending(p => p.Value).ThenBy(p => p.Item1.Id).ToList();

    /// <summary>Uma linha para o --smoke: quantas razões a tabela conhece, e quem gosta e quem desgosta mais
    /// deste país hoje.</summary>
    public static string Smoke(World w, int aboutId)
    {
        var rank = Ranked(w, aboutId);
        if (rank.Count == 0) return $"{w.OpinionSources.Count} razões na tabela, mundo sem outros países";
        var (best, bestOp) = rank[0];
        var (worst, worstOp) = rank[^1];
        int why = Lines(w, worst.Id, aboutId).Count;
        return $"{w.OpinionSources.Count} razões na tabela, {rank.Count} países com opinião; melhor "
             + $"{best.Name} {bestOp:+0;-0} ({Word(w, bestOp)}), pior {worst.Name} {worstOp:+0;-0} "
             + $"({Word(w, worstOp)}) por {why} razões";
    }
}
