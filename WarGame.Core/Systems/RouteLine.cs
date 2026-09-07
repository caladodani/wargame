using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma paragem da rota: a região e o sítio dela no mapa.</summary>
public readonly record struct RouteStop(int RegionId, float X, float Y);

/// <summary>A marcha de uma divisão, do sítio onde está até ao destino: as paragens por onde passa, quanto já
/// andou do primeiro salto e quantos dias faltam para chegar.</summary>
public readonly record struct DivisionRoute(int DivisionId, string Name, IReadOnlyList<RouteStop> Stops,
                                            float Progress, int Days, bool BySea, bool Fighting)
{
    public RouteStop From => Stops[0];
    public RouteStop To => Stops[^1];
    /// <summary>Onde vai a coluna neste momento: entre a paragem de onde saiu e a próxima. Em batalha a
    /// marcha está suspensa e a divisão continua fisicamente na origem, por muito que o progresso do salto
    /// tenha ficado parado a meio.</summary>
    public (float X, float Y) Head =>
        Stops.Count < 2 || Fighting
            ? (From.X, From.Y)
            : (From.X + (Stops[1].X - From.X) * Progress, From.Y + (Stops[1].Y - From.Y) * Progress);
}

/// <summary>A rota de uma divisão em marcha, pronta a desenhar.
///
/// O jogo já sabia para onde cada divisão ia — Division.Path guarda os saltos que faltam — mas isso só
/// existia dentro do painel da região, em texto. No mapa uma coluna a marchar era igual a uma coluna parada.
/// No HoI4 a rota vê-se: uma linha que sai de onde a tropa está, passa pelas terras do caminho e acaba numa
/// seta em cima do destino. Isto é a parte que se pode contar sem abrir o Godot — as paragens, o ponto onde
/// vai a coluna e os dias que faltam — e o RouteOverlay só a desenha.
///
/// Os dias vêm do MovementSystem.HopDays, o mesmo cálculo que faz andar o mundo: uma estimativa que não
/// batesse certo com a marcha era pior do que não haver estimativa.</summary>
public static class RouteLine
{
    /// <summary>A rota desta divisão, ou nada se estiver parada. Quem chama isto para muitas divisões passa
    /// o conjunto de quem está em batalha feito de uma vez (como o MovementSystem faz), que perguntar batalha
    /// a batalha por divisão custa uma varredura de todas as batalhas de cada vez.</summary>
    public static DivisionRoute? Of(World w, Division d, HashSet<int>? inBattle = null)
    {
        if (d.Path.Count == 0 || !w.Regions.TryGetValue(d.RegionId, out var here)) return null;

        var stops = new List<RouteStop> { new(here.Id, here.CenterX, here.CenterY) };
        int days = 0;
        bool sea = false;
        var from = here;
        foreach (int hop in d.Path)
        {
            if (!w.Regions.TryGetValue(hop, out var to)) break;
            if (w.IsSeaHop(from.Id, to.Id)) sea = true;
            // um salto gasta ceil(dias) dias, e o que sobra do último dia perde-se no AdvanceHop: contam-se
            // os saltos um a um, senão a conta ficava sempre abaixo do que a marcha demora mesmo
            float hopDays = MovementSystem.HopDays(w, d, from, to);
            if (stops.Count == 1) hopDays *= 1f - Math.Clamp(d.MoveProgress, 0f, 1f);   // o primeiro já vai a meio
            days += Math.Max(1, (int)MathF.Ceiling(hopDays));
            stops.Add(new RouteStop(to.Id, to.CenterX, to.CenterY));
            from = to;
        }
        if (stops.Count < 2) return null;                 // caminho com regiões que já não existem

        bool fighting = inBattle?.Contains(d.Id) ?? w.InBattle(d.Id);
        return new DivisionRoute(d.Id, d.WarName ?? d.Name ?? $"Divisão {d.Id}", stops, Math.Clamp(d.MoveProgress, 0f, 1f),
                                 days, sea, fighting);
    }

    /// <summary>As rotas destas divisões, sem as que estão paradas, pela ordem das divisões.</summary>
    public static List<DivisionRoute> For(World w, IEnumerable<int> divisionIds)
    {
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));
        var list = new List<DivisionRoute>();
        foreach (int id in divisionIds.Distinct().OrderBy(x => x))
            if (w.Divisions.TryGetValue(id, out var d) && Of(w, d, inBattle) is DivisionRoute r) list.Add(r);
        return list;
    }
}
