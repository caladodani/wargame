using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma parcela do saldo de uma guerra, de um dos lados: a chapa, o número como se lê, o nome e a
/// frase que diz o que ele conta.</summary>
public readonly record struct WarPart(string Glyph, string Value, string Name, string Note);

/// <summary>O saldo de uma guerra — quem está a ganhar, quanto já custou a cada lado e o que se decidiu até
/// hoje. O jogo contava isto tudo desde sempre (o WarStatsSystem escreve regiões tomadas, divisões perdidas
/// e batalhas ganhas a cada evento) e não mostrava nada: o painel Mundo dizia "⚔ Alfa vs Beta (12 dias)" e
/// por baixo uma barra de blocos de texto com o número de divisões de cada lado. Um jogador de HoI4 abre o
/// ecrã da guerra para ver as baixas dos dois lados e quem tomou o quê — aqui os números existiam e morriam
/// dentro do save.
///
/// Como no NationSheet, no RegionState e no BattleField, aqui não se faz conta nova nenhuma: os contadores
/// são os do WarStatsSystem, o veredicto é a mesma regra do WarRecord.Winner (quem tomou mais terra) e a
/// força de combate é a fórmula única do jogo — organização × saúde —, que antes disto estava copiada em
/// seis sítios diferentes.</summary>
public static class WarLedger
{
    /// <summary>A força de combate de uma divisão: a organização que tem, pesada pela saúde. É a fórmula que
    /// o PowerIndex usa para a tabela mundial, a que o ArmyGroupSystem usa para os exércitos e a que as
    /// balanças de guerra usam — uma só, para não haver duas ideias de "força" no mesmo jogo.</summary>
    public static float Strength(Division d) => d.Org * d.Hp / 100f;

    /// <summary>A força de combate de tudo o que um país tem em campo.</summary>
    public static float Strength(World w, int countryId)
    {
        float sum = 0f;
        foreach (var d in w.Divisions.Values) if (d.CountryId == countryId) sum += Strength(d);
        return sum;
    }

    /// <summary>Fracção da força total desta guerra que está do lado A (0..1). É o que a balança desenha:
    /// meio a meio é empate de músculo, não de terreno.</summary>
    public static float Balance(World w, WarInfo war)
    {
        float a = Strength(w, war.A), b = Strength(w, war.B);
        return a + b <= 0f ? 0.5f : a / (a + b);
    }

    /// <summary>A fotografia dos contadores desta guerra até hoje, na mesma forma em que ela entra no
    /// arquivo quando a paz é assinada — assim o saldo a meio da guerra e o saldo do fim são o mesmo
    /// objecto, com a mesma regra de quem ganhou.</summary>
    public static WarRecord Snapshot(World w, WarInfo war) => new(
        war.A, war.B, war.StartDay, w.Clock.Day,
        war.SideA.RegionsTaken, war.SideB.RegionsTaken,
        war.SideA.DivisionsLost, war.SideB.DivisionsLost,
        war.SideA.BattlesWon, war.SideB.BattlesWon);

    /// <summary>Quem está por cima nesta guerra, pela mesma regra com que o arquivo decide quem ganhou:
    /// quem tomou mais terra ao outro. Sem terra trocada não há vencedor — há uma guerra parada.</summary>
    public static int? Ahead(World w, WarInfo war) => Snapshot(w, war).Winner;

    /// <summary>Batalhas a decorrer hoje entre estes dois.</summary>
    public static int Battles(World w, WarInfo war) =>
        w.ActiveBattles.Count(b => war.Involves(b.AttackerCountryId)
                                   && w.Regions.TryGetValue(b.RegionId, out var r)
                                   && r.ControllerId == war.EnemyOf(b.AttackerCountryId));

    /// <summary>O saldo de um dos lados, parcela a parcela: o que tem em campo e o que a guerra já lhe deu
    /// e lhe tirou. As cinco parcelas estão sempre lá — zero baixas é notícia tão boa como muitas é má.</summary>
    public static List<WarPart> Parts(World w, WarInfo war, int countryId)
    {
        var side = war.Side(countryId);
        int divs = w.Divisions.Values.Count(d => d.CountryId == countryId);
        float str = Strength(w, countryId);
        int days = Math.Max(1, w.Clock.Day - war.StartDay);
        return new List<WarPart>
        {
            new("capacete", $"{divs}", "divisões",
                $"Tropa em campo hoje, em todo o mundo.\nForça de combate {str:0}, de organização pesada pela saúde."),
            new("punho", $"{str:0}", "força",
                $"Organização × saúde de tudo o que tem em pé.\nÉ isto que a balança desta guerra pesa, não a contagem de divisões."),
            new("bandeira", $"{side.RegionsTaken}", "terra tomada",
                $"Regiões tiradas ao inimigo desde o primeiro dia.\nÉ por esta conta que se sabe quem está a ganhar."),
            new("caveira", $"{side.DivisionsLost}", "divisões perdidas",
                $"Divisões desfeitas em combate.\nDá {side.DivisionsLost / (float)days:0.00} por dia, ao longo de {days} dias de guerra."),
            new("medalha", $"{side.BattlesWon}", "batalhas ganhas",
                "Combates ganhos, a atacar ou a defender."),
        };
    }

    /// <summary>O veredicto em palavras: quem está por cima e por quanto, ou que ninguém mexeu a frente.</summary>
    public static string Verdict(World w, WarInfo war)
    {
        var snap = Snapshot(w, war);
        int stale = w.Clock.Day - war.LastProgressDay;
        if (Ahead(w, war) is not int lead)
            return snap.ARegions == 0
                ? $"frente parada há {stale} dias — ninguém tomou terra nenhuma"
                : $"empate a {snap.ARegions} regiões tomadas de cada lado";
        string name = w.Countries.TryGetValue(lead, out var c) ? c.Name : "#" + lead;
        return $"{name} à frente por {Math.Abs(snap.ARegions - snap.BRegions)} região(ões) tomada(s)";
    }

    /// <summary>O saldo dito por palavras, para o rodapé e para o --smoke.</summary>
    public static string Line(World w, WarInfo war)
    {
        string na = w.Countries.TryGetValue(war.A, out var a) ? a.Name : "#" + war.A;
        string nb = w.Countries.TryGetValue(war.B, out var b) ? b.Name : "#" + war.B;
        return $"{na} vs {nb} ({w.Clock.Day - war.StartDay} dias): "
             + string.Join(" · ", Parts(w, war, war.A).Select(p => $"{p.Name} {p.Value}")) + " | "
             + string.Join(" · ", Parts(w, war, war.B).Select(p => $"{p.Name} {p.Value}"))
             + $" — {Verdict(w, war)}";
    }
}
