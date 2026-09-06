using WarGame.Core.Model;

namespace WarGame.Core.Commands;

/// <summary>Toda a acção do jogador (e da IA) passa aqui: valida, depois muta. Facilita replay e log.</summary>
public interface ICommand
{
    int CountryId { get; }
    string? Validate(World w);   // null = OK, senão mensagem de erro para a UI
    void Execute(World w);
}

public sealed class CommandDispatcher
{
    private readonly List<ICommand> _log = new();
    public IReadOnlyList<ICommand> Log => _log;

    public string? Dispatch(World w, ICommand c)
    {
        var err = c.Validate(w);
        if (err is not null) return err;
        c.Execute(w); _log.Add(c);
        return null;
    }
}

/// <summary>Escolhe a tecnologia a investigar (uma de cada vez; trocar perde o progresso, como em HoI4 sem slots).</summary>
public sealed record ResearchTechCommand(int CountryId, string TechId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c)) return "País inexistente";
        if (!w.Techs.TryGetValue(TechId, out var t)) return "Tecnologia inexistente";
        if (c.Techs.Contains(TechId)) return "Já investigada";
        if (t.Requires is not null && !c.Techs.Contains(t.Requires)) return $"Precisa de {w.Techs[t.Requires].Name}";
        if (c.ResearchTech == TechId) return "Já em investigação";
        return null;
    }
    public void Execute(World w) { var c = w.Countries[CountryId]; c.ResearchTech = TechId; c.ResearchProgress = 0f; }
}

/// <summary>Manda uma divisão para uma região (qualquer distância): caminho por BFS através de regiões
/// controladas pelo país ou por um inimigo em guerra. O MovementSystem anda salto a salto.</summary>
public sealed record MoveDivisionCommand(int CountryId, int DivisionId, int TargetRegionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Divisions.TryGetValue(DivisionId, out var d)) return "Divisão inexistente";
        if (d.CountryId != CountryId) return "Divisão não é tua";
        if (!w.Regions.ContainsKey(TargetRegionId)) return "Região inexistente";
        if (d.RegionId == TargetRegionId) return "Já está lá";
        if (w.InBattle(DivisionId)) return "Em combate";
        if (FindPath(w, d.RegionId, TargetRegionId, CountryId) is null) return "Sem caminho: só por território próprio ou inimigo";
        return null;
    }

    public void Execute(World w) => w.Divisions[DivisionId].SetPath(FindPath(w, w.Divisions[DivisionId].RegionId, TargetRegionId, CountryId)!);

    /// <summary>BFS. Devolve os saltos (sem a origem, com o destino) ou null. Transitável = controlada por
    /// `countryId` ou por país com quem está em guerra.</summary>
    public static List<int>? FindPath(World w, int from, int to, int countryId, int maxHops = 80)
    {
        if (from == to) return new List<int>();
        var prev = new Dictionary<int, int> { [from] = from };
        var queue = new Queue<(int id, int depth)>(); queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            var (cur, depth) = queue.Dequeue();
            if (depth >= maxHops) continue;
            foreach (var n in w.Regions[cur].Neighbours)
            {
                if (prev.ContainsKey(n)) continue;
                var r = w.Regions[n];
                if (r.ControllerId != countryId && !w.AreAtWar(countryId, r.ControllerId)) continue;
                prev[n] = cur;
                if (n == to)
                {
                    var path = new List<int>();
                    for (int x = to; x != from; x = prev[x]) path.Add(x);
                    path.Reverse(); return path;
                }
                queue.Enqueue((n, depth + 1));
            }
        }
        return null;
    }
}

public sealed record StopDivisionCommand(int CountryId, int DivisionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Divisions.TryGetValue(DivisionId, out var d)) return "Divisão inexistente";
        if (d.CountryId != CountryId) return "Divisão não é tua";
        return null;
    }
    public void Execute(World w) => w.Divisions[DivisionId].ClearPath();
}

public sealed record DeclareWarCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (CountryId == TargetCountryId) return "Não podes declarar guerra a ti próprio";
        if (!w.Countries.ContainsKey(TargetCountryId)) return "País inexistente";
        return w.Countries[CountryId].AtWarWith.Contains(TargetCountryId) ? "Já em guerra" : null;
    }
    public void Execute(World w)
    {
        w.Countries[CountryId].AtWarWith.Add(TargetCountryId);
        w.Countries[TargetCountryId].AtWarWith.Add(CountryId);
        w.Events.Publish(new Events.WarDeclared(CountryId, TargetCountryId));
    }
}

/// <summary>Encomenda uma divisão de um template do próprio país. ProductionSystem gasta Country.Money nela.</summary>
public sealed record BuildDivisionCommand(int CountryId, int TemplateId) : ICommand
{
    public string? Validate(World w)
    {
        DivisionTemplate t;
        try { t = w.Units.GetTemplate(TemplateId); } catch (InvalidOperationException) { return "Template inexistente"; }
        if (t.CountryId != CountryId) return "Template não é teu";
        if (w.Countries[CountryId].Queue.Count >= 30) return "Fila cheia";
        return null;
    }
    public void Execute(World w) => w.Countries[CountryId].Queue.Add(new ProductionOrder { TemplateId = TemplateId });
}

/// <summary>Cancela a encomenda na posição `Index`; devolve os pontos já gastos.</summary>
public sealed record CancelProductionCommand(int CountryId, int Index) : ICommand
{
    public string? Validate(World w) =>
        Index < 0 || Index >= w.Countries[CountryId].Queue.Count ? "Encomenda inexistente" : null;
    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.Money += c.Queue[Index].Progress; c.Queue.RemoveAt(Index);
    }
}

/// <summary>O jogador escolhe o país que controla (uma vez por partida).</summary>
public sealed record ChoosePlayerCommand(int CountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.ContainsKey(CountryId)) return "País inexistente";
        if (w.Countries.Values.Any(c => c.IsPlayer)) return "Já escolheste um país";
        return null;
    }
    public void Execute(World w) => w.Countries[CountryId].IsPlayer = true;
}
