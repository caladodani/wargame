using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>As sondas do mundo: o que um evento noticioso pode ficar à espera que aconteça.
///
/// A crítica que mais se repete a um jogo destes é a do tempo morto — semanas de relógio a andar sem que
/// nada bata à porta. No HoI4 o que quebra esse silêncio são os eventos, e os que se sentem não são os do
/// calendário (esses vêem-se chegar): são os que reagem ao que se está a passar connosco — a guerra que
/// rebentou, a capital que caiu, a bolsa que se fechou, os cofres que secaram.
///
/// Aqui não há eventos nenhuns: há sondas. Cada uma é uma pergunta ao mundo com um número ao lado, e
/// quem faz a pergunta é a linha `news_event.watch` da base de dados. Assim um evento novo é uma linha
/// de SQL, e o C# só cresce quando se quer perguntar ao mundo uma coisa que ainda não se sabia perguntar.
///
/// Derivado: não guarda nada, não é ISystem, e responde sempre sobre o estado de hoje.</summary>
public static class WorldWatch
{
    /// <summary>As perguntas que se sabem fazer. A ordem é a que a prova headless usa.</summary>
    public static readonly string[] Keys =
    {
        "guerra", "guerra_longa", "paz", "capital_perdida", "terra_perdida", "terra_tomada",
        "estabilidade", "desgaste", "cofre_vazio", "sem_homens", "sem_combustivel", "revolta",
        "cerco", "derrotas", "prisioneiros", "inimigo_caiu", "aliado_caiu",
    };

    public static bool Known(string watch) => Array.IndexOf(Keys, watch) >= 0;

    /// <summary>A sonda está a dar? `arg` é o número da linha do evento: limiar, contagem ou dias.</summary>
    public static bool Fires(World w, Country c, string watch, float arg) => watch switch
    {
        "guerra" => c.AtWarWith.Count >= Least(arg),
        "guerra_longa" => WarSince(w, c) is int day && w.Clock.Day - day >= arg,
        "paz" => c.AtWarWith.Count == 0 && w.Clock.Day >= arg,
        "capital_perdida" => w.Regions.TryGetValue(c.CapitalRegionId, out var cap) && cap.ControllerId != c.Id,
        "terra_perdida" => w.Regions.Values.Count(r => r.OwnerId == c.Id && r.ControllerId != c.Id) >= Least(arg),
        "terra_tomada" => w.Regions.Values.Count(r => r.ControllerId == c.Id && r.OwnerId != c.Id) >= Least(arg),
        "estabilidade" => c.Stability <= arg,
        "desgaste" => c.WarExhaustion >= arg,
        "cofre_vazio" => c.Money < arg,
        "sem_homens" => c.Manpower >= 0f && c.Manpower <= arg,
        "sem_combustivel" => c.FuelCap > 0f && c.Fuel < arg,
        "revolta" => w.Regions.Values.Any(r => r.ControllerId == c.Id && r.OwnerId != c.Id && r.Resistance >= arg),
        "cerco" => w.Divisions.Values.Count(d => d.CountryId == c.Id && d.Cut) >= Least(arg),
        "derrotas" => c.DefeatStreak >= Least(arg),
        "prisioneiros" => c.Prisoners.Values.Sum() >= arg,
        "inimigo_caiu" => w.Countries.Values.Any(o => o.Capitulated && c.AtWarWith.Contains(o.Id)),
        "aliado_caiu" => w.Countries.Values.Any(o => o.Id != c.Id && o.Capitulated && w.SameFaction(c.Id, o.Id)),
        _ => false,
    };

    /// <summary>Contagens não fazem sentido a zero: quem escreve 0 numa sonda de contar quer dizer uma.</summary>
    private static int Least(float arg) => Math.Max(1, (int)MathF.Round(arg));

    /// <summary>Dia em que começou a guerra mais antiga deste país, ou null se está em paz.</summary>
    private static int? WarSince(World w, Country c)
    {
        int? first = null;
        foreach (var war in w.Wars.Values)
            if (war.Involves(c.Id) && (first is null || war.StartDay < first)) first = war.StartDay;
        return first;
    }

    /// <summary>O que a sonda está a ver, em palavras — para a prova headless e para o diário.</summary>
    public static string Line(World w, Country c, string watch, float arg) => watch switch
    {
        "guerra" => $"{c.AtWarWith.Count} guerra(s)",
        "guerra_longa" => WarSince(w, c) is int d ? $"{w.Clock.Day - d} dias de guerra" : "em paz",
        "paz" => c.AtWarWith.Count == 0 ? $"{w.Clock.Day} dias de calendário em paz" : "em guerra",
        "capital_perdida" => w.Regions.TryGetValue(c.CapitalRegionId, out var cap) && cap.ControllerId != c.Id ? "capital ocupada" : "capital nossa",
        "terra_perdida" => $"{w.Regions.Values.Count(r => r.OwnerId == c.Id && r.ControllerId != c.Id)} regiões ocupadas",
        "terra_tomada" => $"{w.Regions.Values.Count(r => r.ControllerId == c.Id && r.OwnerId != c.Id)} regiões tomadas",
        "estabilidade" => $"estabilidade {c.Stability:0}",
        "desgaste" => $"desgaste {c.WarExhaustion:0}",
        "cofre_vazio" => $"{c.Money:0} em caixa",
        "sem_homens" => $"{c.Manpower:0} homens",
        "sem_combustivel" => $"{c.Fuel:0} de combustível",
        "revolta" => $"revolta máxima {w.Regions.Values.Where(r => r.ControllerId == c.Id && r.OwnerId != c.Id).Select(r => r.Resistance).DefaultIfEmpty(0f).Max():0.00}",
        "cerco" => $"{w.Divisions.Values.Count(d => d.CountryId == c.Id && d.Cut)} divisões cortadas",
        "derrotas" => $"{c.DefeatStreak} derrotas seguidas",
        "prisioneiros" => $"{c.Prisoners.Values.Sum()} prisioneiros",
        "inimigo_caiu" => w.Countries.Values.Count(o => o.Capitulated && c.AtWarWith.Contains(o.Id)) + " inimigos caídos",
        "aliado_caiu" => w.Countries.Values.Count(o => o.Id != c.Id && o.Capitulated && w.SameFaction(c.Id, o.Id)) + " aliados caídos",
        _ => "sonda desconhecida",
    };
}
