using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Como acabou (ou como vai) uma campanha: o balanço de tudo o que o país fez, tirado do estado
/// que já existe — regiões, guerras arquivadas, divisões, condecorações, investigação. Até aqui o fim de
/// jogo era uma caixa de texto de duas linhas; isto é a folha de serviço que dá sentido a uma partida de
/// centenas de dias. Nada disto é guardado: recalcula-se a partir do mundo, por isso um save antigo dá
/// relatório na mesma.</summary>
public static class CampaignReport
{
    public const string Domination = "domination";   // controla o mundo
    public const string Defeat = "defeat";           // capitulou
    public const string Ongoing = "ongoing";         // ainda a jogar

    /// <summary>Balanço da campanha do país. `verdict` é uma das constantes acima.</summary>
    public static Report Build(World w, int countryId, string verdict = Ongoing)
    {
        var c = w.Countries.TryGetValue(countryId, out var found) ? found : null;
        var mine = w.Regions.Values.Where(r => r.ControllerId == countryId).ToList();
        long pop = mine.Sum(r => (long)r.Population);
        long world = w.Regions.Values.Sum(r => (long)r.Population);

        var wars = w.WarHistory.Where(r => r.Involves(countryId)).ToList();
        int won = wars.Count(r => r.Winner == countryId);
        int lost = wars.Count(r => r.Winner is int x && x != countryId);
        int taken = wars.Sum(r => r.Regions(countryId));
        int given = wars.Sum(r => r.Regions(r.A == countryId ? r.B : r.A));
        int losses = wars.Sum(r => r.Losses(countryId));
        int battles = wars.Sum(r => r.Battles(countryId));

        var divisions = w.Divisions.Values.Where(d => d.CountryId == countryId).ToList();
        var best = divisions.OrderByDescending(d => d.Medals.Count).ThenByDescending(d => d.Battles)
            .ThenByDescending(d => d.Xp).FirstOrDefault();

        // territórios ganhos por conquista: nossos agora, de outrem no primeiro dia
        var conquered = mine
            .Where(r => r.InitialOwnerId != countryId && r.OwnerId == countryId)
            .GroupBy(r => r.InitialOwnerId)
            .Select(g => new Conquest(g.Key, w.Countries.TryGetValue(g.Key, out var o) ? o.Name : "país " + g.Key,
                                      g.Count(), g.Sum(r => (long)r.Population)))
            .OrderByDescending(x => x.Regions).ThenBy(x => x.CountryId)
            .ToList();

        var report = new Report
        {
            CountryId = countryId,
            CountryName = c?.Name ?? "país " + countryId,
            Verdict = verdict,
            Days = w.Clock.Day,
            Regions = mine.Count,
            RegionsAtStart = w.Regions.Values.Count(r => r.InitialOwnerId == countryId),
            Population = pop,
            PopulationShare = world == 0 ? 0f : (float)pop / world,
            WarsFought = wars.Count + w.Wars.Values.Count(x => x.Involves(countryId)),
            WarsWon = won,
            WarsLost = lost,
            RegionsTaken = taken,
            RegionsGivenUp = given,
            DivisionsLost = losses,
            BattlesWon = battles,
            Divisions = divisions.Count,
            Strength = divisions.Sum(d => d.Org * d.Hp / 100f),
            Medals = divisions.Sum(d => d.Medals.Count),
            Techs = c?.Techs.Count ?? 0,
            Focuses = c?.FocusesDone.Count ?? 0,
            BestDivisionId = best?.Id,
            BestDivisionMedals = best?.Medals.Count ?? 0,
            BestDivisionBattles = best?.Battles ?? 0,
            Conquests = conquered,
        };
        report.Score = Score(w, report);
        return report;
    }

    /// <summary>Pontuação da campanha. Os pesos são regras da base de dados — mudar o equilíbrio é mudar
    /// linhas em rule, não código.</summary>
    private static int Score(World w, Report r)
    {
        float s = r.Regions * w.Rule("score_per_region", 4f)
                + r.Population / 1_000_000f * w.Rule("score_per_million", 0.5f)
                + r.WarsWon * w.Rule("score_per_war_won", 120f)
                - r.WarsLost * w.Rule("score_per_war_lost", 90f)
                + r.BattlesWon * w.Rule("score_per_battle", 3f)
                - r.DivisionsLost * w.Rule("score_per_division_lost", 2f)
                + (r.Techs + r.Focuses) * w.Rule("score_per_advance", 8f);
        if (r.Verdict == Domination) s *= w.Rule("score_domination_bonus", 2f);
        if (r.Verdict == Defeat) s *= w.Rule("score_defeat_penalty", 0.4f);
        return (int)MathF.Max(0f, s);
    }

    /// <summary>Título honorífico do desempenho — o que se conta a alguém em duas palavras.</summary>
    public static string Rank(World w, Report r) => r.Verdict switch
    {
        Defeat => "Nação ocupada",
        Domination => "Hegemonia mundial",
        _ when r.Score >= w.Rule("rank_legend_score", 4000f) => "Grande potência",
        _ when r.Score >= w.Rule("rank_power_score", 1500f) => "Potência regional",
        _ when r.RegionsTaken > r.RegionsGivenUp => "Nação em expansão",
        _ when r.WarsFought == 0 => "Paz armada",
        _ => "Nação em pé",
    };

    public sealed record Conquest(int CountryId, string Name, int Regions, long Population);

    public sealed class Report
    {
        public int CountryId { get; init; }
        public string CountryName { get; init; } = "";
        public string Verdict { get; init; } = Ongoing;
        public int Days { get; init; }
        public int Regions { get; init; }
        public int RegionsAtStart { get; init; }
        public long Population { get; init; }
        public float PopulationShare { get; init; }
        public int WarsFought { get; init; }
        public int WarsWon { get; init; }
        public int WarsLost { get; init; }
        public int RegionsTaken { get; init; }
        public int RegionsGivenUp { get; init; }
        public int DivisionsLost { get; init; }
        public int BattlesWon { get; init; }
        public int Divisions { get; init; }
        public float Strength { get; init; }
        public int Medals { get; init; }
        public int Techs { get; init; }
        public int Focuses { get; init; }
        public int? BestDivisionId { get; init; }
        public int BestDivisionMedals { get; init; }
        public int BestDivisionBattles { get; init; }
        public IReadOnlyList<Conquest> Conquests { get; init; } = Array.Empty<Conquest>();
        public int Score { get; set; }

        /// <summary>Regiões ganhas (ou perdidas) desde o primeiro dia.</summary>
        public int NetRegions => Regions - RegionsAtStart;
    }
}
