using WarGame.Core.Events;
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

/// <summary>Escolhe o foco nacional (um de cada vez; trocar perde o progresso, como em HoI4).</summary>
public sealed record SelectFocusCommand(int CountryId, string FocusId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c)) return "País inexistente";
        if (!w.Focuses.TryGetValue(FocusId, out var f)) return "Foco inexistente";
        if (f.CountryId != CountryId) return "Foco de outro país";
        if (c.FocusesDone.Contains(FocusId)) return "Já concluído";
        if (f.Requires is not null && !c.FocusesDone.Contains(f.Requires)) return $"Precisa de {w.Focuses[f.Requires].Name}";
        if (c.CurrentFocus == FocusId) return "Já em curso";
        return null;
    }
    public void Execute(World w) { var c = w.Countries[CountryId]; c.CurrentFocus = FocusId; c.FocusProgress = 0f; }
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
        if (FindPath(w, d.RegionId, TargetRegionId, CountryId) is null) return "Sem caminho por terra ou mar: só por território próprio ou inimigo";
        return null;
    }

    public void Execute(World w) => w.Divisions[DivisionId].SetPath(FindPath(w, w.Divisions[DivisionId].RegionId, TargetRegionId, CountryId)!);

    /// <summary>BFS por terra e mar (sea_link conta como um salto). Devolve os saltos (sem a origem, com o
    /// destino) ou null. Transitável = controlada por `countryId` ou por país com quem está em guerra.</summary>
    public static List<int>? FindPath(World w, int from, int to, int countryId, int maxHops = 80)
    {
        if (from == to) return new List<int>();
        var prev = new Dictionary<int, int> { [from] = from };
        var queue = new Queue<(int id, int depth)>(); queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            var (cur, depth) = queue.Dequeue();
            if (depth >= maxHops) continue;
            var reg = w.Regions[cur];
            foreach (var n in reg.SeaNeighbours.Count == 0 ? reg.Neighbours : reg.Neighbours.Concat(reg.SeaNeighbours.Keys))
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

/// <summary>Guerra entre dois países. HoI4: aliados na mesma facção nunca se atacam; declarar guerra a um membro
/// de uma facção chama automaticamente os outros membros contra o agressor (só a facção do DEFENSOR — os aliados
/// do agressor não entram) — ver World.SameFaction/FactionsOf e Events.FactionJoinedWar.</summary>
public sealed record DeclareWarCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (CountryId == TargetCountryId) return "Não podes declarar guerra a ti próprio";
        if (!w.Countries.ContainsKey(TargetCountryId)) return "País inexistente";
        if (w.SameFaction(CountryId, TargetCountryId)) return "Aliados na mesma facção";
        return w.Countries[CountryId].AtWarWith.Contains(TargetCountryId) ? "Já em guerra" : null;
    }
    public void Execute(World w)
    {
        w.StartWar(CountryId, TargetCountryId);
        w.Events.Publish(new Events.WarDeclared(CountryId, TargetCountryId));

        // Facções do alvo (defensor): cada membro que não é da facção do agressor e ainda não está em guerra
        // com ele entra em guerra. Um HashSet evita chamar duas vezes quem está em mais que uma facção do alvo.
        var called = new HashSet<int>();
        foreach (var f in w.FactionsOf(TargetCountryId))
            foreach (var m in f.Members)
            {
                if (m == CountryId || m == TargetCountryId || !w.Countries.ContainsKey(m) || !called.Add(m)) continue;
                if (w.SameFaction(CountryId, m) || w.AreAtWar(CountryId, m)) continue;
                w.StartWar(m, CountryId);
                w.Events.Publish(new Events.FactionJoinedWar(f.Id, m, CountryId));
            }
    }
}

/// <summary>Começa a justificar um objectivo de guerra (HoI4): war_justify_days depois o
/// DiplomacySystem declara a guerra sozinho. Um alvo de cada vez; trocar recomeça do zero.</summary>
public sealed record JustifyWarCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (CountryId == TargetCountryId) return "Não podes justificar contra ti próprio";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t)) return "País inexistente";
        if (t.Capitulated) return "Já capitulou";
        if (w.SameFaction(CountryId, TargetCountryId)) return "Aliados na mesma facção";
        if (w.Countries[CountryId].AtWarWith.Contains(TargetCountryId)) return "Já em guerra";
        if (w.Countries[CountryId].JustifyTarget == TargetCountryId) return "Já a justificar";
        return null;
    }
    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.JustifyTarget = TargetCountryId; c.JustifyProgress = 0f;
        w.Events.Publish(new Events.WarJustifyStarted(CountryId, TargetCountryId));
    }
}

/// <summary>Plano de batalha simplificado (HoI4: frentes sem micro): distribui as divisões paradas
/// pelas regiões de fronteira com inimigos, equilibrando o número por região e escolhendo para cada
/// divisão a fronteira alcançável mais perto (menos saltos). Divisões em combate ou já em marcha ficam.</summary>
public sealed record DefendBordersCommand(int CountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c)) return "País inexistente";
        if (c.AtWarWith.Count == 0) return "Sem guerras — não há frente para guardar";
        return FrontRegions(w, CountryId).Count == 0 ? "Sem fronteira com o inimigo" : null;
    }

    public void Execute(World w)
    {
        var front = FrontRegions(w, CountryId);
        var load = front.ToDictionary(r => r, r => w.Regions[r].DivisionIds.Count(id =>
            w.Divisions.TryGetValue(id, out var d) && d.CountryId == CountryId));
        var idle = w.Divisions.Values
            .Where(d => d.CountryId == CountryId && d.Path.Count == 0 && !w.InBattle(d.Id) && !front.Contains(d.RegionId))
            .OrderBy(d => d.Id).ToList();
        foreach (var d in idle)
        {
            // fronteira menos guarnecida; empate = caminho mais curto a partir da divisão
            int best = -1; List<int>? bestPath = null;
            foreach (var r in front.OrderBy(r => load[r]))
            {
                if (best >= 0 && load[r] > load[best]) break;   // já só restam mais carregadas
                var path = MoveDivisionCommand.FindPath(w, d.RegionId, r, CountryId);
                if (path is null) continue;
                if (best < 0 || load[r] < load[best] || path.Count < bestPath!.Count) { best = r; bestPath = path; }
            }
            if (best < 0) continue;
            d.SetPath(bestPath!); load[best]++;
        }
    }

    /// <summary>Regiões controladas pelo país com pelo menos um vizinho controlado por um inimigo.</summary>
    internal static List<int> FrontRegions(World w, int countryId)
    {
        var list = new List<int>();
        foreach (var r in w.Regions.Values)
            if (r.ControllerId == countryId && r.Neighbours.Any(n => w.Regions.TryGetValue(n, out var nr) && w.AreAtWar(countryId, nr.ControllerId)))
                list.Add(r.Id);
        return list;
    }
}

/// <summary>Escolhe uma opção de um evento noticioso com escolhas (news_event_option).</summary>
public sealed record ChooseNewsOptionCommand(int CountryId, string EventId, string OptionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.NewsEvents.TryGetValue(EventId, out var e)) return "Evento inexistente";
        if (e.CountryId != CountryId) return "Evento não é teu";
        if (e.Day > w.Clock.Day) return "Evento ainda não aconteceu";
        if (w.NewsChoices.ContainsKey(EventId)) return "Já escolhido";
        if (!w.NewsOptions.TryGetValue(EventId, out var opts) || !opts.Any(o => o.Id == OptionId)) return "Opção inexistente";
        return null;
    }

    public void Execute(World w)
    {
        w.NewsChoices[EventId] = OptionId;
        if (w.Countries.TryGetValue(CountryId, out var c)) w.ApplyTechs(c);
        w.Events.Publish(new Events.NewsChoiceMade(CountryId, EventId, OptionId));
    }
}

/// <summary>Desenha um template novo (HoI4: division designer). Vai para o repositório em memória com
/// id ≥ World.CustomTemplateBase e persiste no save (template/template_unit).</summary>
public sealed record CreateTemplateCommand(int CountryId, string Name, IReadOnlyList<(int UnitTypeId, int Qty)> Units) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "País inválido";
        var name = Name?.Trim() ?? "";
        if (name.Length is < 1 or > 40) return "Nome: 1 a 40 caracteres";
        if (Units is null || Units.Count is < 1 or > 10) return "1 a 10 tipos de unidade";
        if (Units.Select(u => u.UnitTypeId).Distinct().Count() != Units.Count) return "Tipo de unidade repetido";
        int total = 0;
        foreach (var (unitId, qty) in Units)
        {
            if (qty is < 1 or > 30) return "Quantidade: 1 a 30 por tipo";
            total += qty;
            try { w.Units.GetUnitType(unitId); } catch (InvalidOperationException) { return "Tipo de unidade inexistente"; }
        }
        return total > 60 ? "Máximo 60 unidades por divisão" : null;
    }

    public void Execute(World w)
    {
        int id = w.CustomTemplateIds.Count == 0 ? World.CustomTemplateBase : w.CustomTemplateIds.Max() + 1;
        w.Units.AddCustomTemplate(id, CountryId, Name.Trim(), Units);
        w.CustomTemplateIds.Add(id);
        w.Events.Publish(new Events.TemplateCreated(CountryId, id));
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

/// <summary>Fundar uma facção nova (diplomacia). Id gerado (fx_N); o fundador é o primeiro membro.</summary>
public sealed record CreateFactionCommand(int CountryId, string Name) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        var name = Name?.Trim() ?? "";
        if (name.Length is < 1 or > 40) return "nome tem de ter 1-40 caracteres";
        if (w.Factions.Values.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
            return "já existe uma facção com esse nome";
        return null;
    }

    public void Execute(World w)
    {
        var f = w.CreateFaction(w.NewFactionId(), Name.Trim(), "");
        f.Members.Add(CountryId);
        w.Events.Publish(new FactionCreated(CountryId, f.Id));
    }
}

/// <summary>Convidar um país (IA) para uma facção de que se é membro. A IA aceita só com inimigo comum
/// (World.FactionWouldAccept); o resultado sai como FactionJoined ou FactionInviteRejected.</summary>
public sealed record InviteToFactionCommand(int CountryId, string FactionId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Factions.TryGetValue(FactionId, out var f)) return "facção desconhecida";
        if (!f.Members.Contains(CountryId)) return "não és membro dessa facção";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t) || t.Capitulated) return "país inválido";
        if (t.IsPlayer) return "o jogador decide sozinho se adere";
        if (f.Members.Contains(TargetCountryId)) return "já é membro";
        foreach (var m in f.Members) if (w.AreAtWar(TargetCountryId, m)) return "está em guerra com um membro";
        return null;
    }

    public void Execute(World w)
    {
        var f = w.Factions[FactionId];
        if (w.FactionWouldAccept(TargetCountryId, f))
        {
            f.Members.Add(TargetCountryId);
            w.Events.Publish(new FactionJoined(FactionId, TargetCountryId));
        }
        else w.Events.Publish(new FactionInviteRejected(FactionId, TargetCountryId));
    }
}

/// <summary>Pedir adesão a uma facção (jogador). A mesma regra da IA: só com inimigo comum.</summary>
public sealed record JoinFactionCommand(int CountryId, string FactionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Factions.TryGetValue(FactionId, out var f)) return "facção desconhecida";
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (f.Members.Contains(CountryId)) return "já és membro";
        foreach (var m in f.Members) if (w.AreAtWar(CountryId, m)) return "estás em guerra com um membro";
        if (!w.FactionWouldAccept(CountryId, f)) return "recusado: sem inimigo comum com a facção";
        return null;
    }

    public void Execute(World w)
    {
        w.Factions[FactionId].Members.Add(CountryId);
        w.Events.Publish(new FactionJoined(FactionId, CountryId));
    }
}

/// <summary>Sair de uma facção. Em guerra não se sai (HoI4).</summary>
public sealed record LeaveFactionCommand(int CountryId, string FactionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Factions.TryGetValue(FactionId, out var f)) return "facção desconhecida";
        if (!f.Members.Contains(CountryId)) return "não és membro";
        if (w.Countries.TryGetValue(CountryId, out var c) && c.AtWarWith.Count > 0) return "em guerra não se sai da facção";
        return null;
    }

    public void Execute(World w)
    {
        w.Factions[FactionId].Members.Remove(CountryId);
        w.Events.Publish(new FactionLeft(FactionId, CountryId));
    }
}

/// <summary>Iniciar obra de infraestrutura numa região própria (paga infra_build_cost já;
/// o ConstructionSystem conclui ao fim de infra_build_days).</summary>
public sealed record BuildInfrastructureCommand(int CountryId, int RegionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Regions.TryGetValue(RegionId, out var r)) return "região inválida";
        if (r.OwnerId != CountryId || r.ControllerId != CountryId) return "a região não é tua";
        if (r.Building) return "já há uma obra em curso";
        if (r.Infrastructure >= w.Rule("infra_max", 2f) - 1e-4f) return "infraestrutura no máximo";
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (c.Money < w.Rule("infra_build_cost", 40f)) return $"faltam pontos de produção ({w.Rule("infra_build_cost", 40f):0})";
        return null;
    }

    public void Execute(World w)
    {
        w.Countries[CountryId].Money -= w.Rule("infra_build_cost", 40f);
        var r = w.Regions[RegionId];
        r.Building = true; r.BuildProgress = 0f;
    }
}
