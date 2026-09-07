using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;

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

/// <summary>Põe uma tecnologia numa ranhura de investigação livre (ResearchSystem.Slots). Com os
/// laboratórios cheios é preciso largar uma linha primeiro — largar perde o progresso, como no HoI4.</summary>
public sealed record ResearchTechCommand(int CountryId, string TechId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c)) return "País inexistente";
        if (!w.Techs.TryGetValue(TechId, out var t)) return "Tecnologia inexistente";
        if (c.Techs.Contains(TechId)) return "Já investigada";
        if (t.Requires is not null && !c.Techs.Contains(t.Requires)) return $"Precisa de {w.Techs[t.Requires].Name}";
        if (c.Research.ContainsKey(TechId)) return "Já em investigação";
        if (ResearchSystem.FreeSlots(w, c) == 0) return "Laboratórios cheios: larga uma investigação primeiro";
        return null;
    }
    public void Execute(World w) => w.Countries[CountryId].Research[TechId] = 0f;
}

/// <summary>Larga uma linha de investigação e liberta a ranhura. O progresso perde-se: é o preço de mudar
/// de ideias a meio, e é o que torna a escolha das ranhuras uma decisão e não uma lista.</summary>
public sealed record CancelResearchCommand(int CountryId, string TechId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c)) return "País inexistente";
        if (!c.Research.ContainsKey(TechId)) return "Essa tecnologia não está em investigação";
        return null;
    }
    public void Execute(World w) => w.Countries[CountryId].Research.Remove(TechId);
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
        if (c.CurrentFocus == FocusId) return "Já em curso";
        // pré-requisitos (o do próprio foco e os de focus_link) e ramos rivais já fechados
        if (w.FocusBlock(c, FocusId) is string block)
            return block.StartsWith('!')
                ? $"Fechado por {w.Focuses[block[1..]].Name}"
                : $"Precisa de {(w.Focuses.TryGetValue(block, out var need) ? need.Name : block)}";
        return null;
    }
    public void Execute(World w) { var c = w.Countries[CountryId]; c.CurrentFocus = FocusId; c.FocusProgress = 0f; }
}

/// <summary>Manda uma divisão para uma região (qualquer distância): caminho por BFS através de regiões
/// controladas pelo país ou por um inimigo em guerra. O MovementSystem anda salto a salto.</summary>
/// <summary>Liga ou desliga a ordem permanente de avanço (AutoFrontSystem) numa divisão.</summary>
public sealed record SetAutoAdvanceCommand(int CountryId, int DivisionId, bool On) : ICommand
{
    public string? Validate(World w) =>
        !w.Divisions.TryGetValue(DivisionId, out var d) ? "Divisão inexistente"
        : d.CountryId != CountryId ? "Divisão não é tua" : null;

    public void Execute(World w) => w.Divisions[DivisionId].AutoAdvance = On;
}

public sealed record MoveDivisionCommand(int CountryId, int DivisionId, int TargetRegionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Divisions.TryGetValue(DivisionId, out var d)) return "Divisão inexistente";
        if (d.CountryId != CountryId) return "Divisão não é tua";
        if (!w.Regions.ContainsKey(TargetRegionId)) return "Região inexistente";
        if (d.RegionId == TargetRegionId) return "Já está lá";
        if (w.InBattle(DivisionId)) return "Em combate";
        if (FindPath(w, d.RegionId, TargetRegionId, CountryId) is null) return "Sem caminho por terra ou mar: só por território próprio, aliado ou inimigo";
        return null;
    }

    public void Execute(World w)
    {
        var d = w.Divisions[DivisionId];
        d.AutoAdvance = false;   // ordem manual manda: desliga o avanço automático
        d.SetPath(FindPath(w, d.RegionId, TargetRegionId, CountryId)!);
    }

    /// <summary>BFS por terra e mar (sea_link conta como um salto). Devolve os saltos (sem a origem, com o
    /// destino) ou null. Transitável = controlada por `countryId`, por aliado de facção, ou por país com quem está em guerra.</summary>
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
                if (!w.CanTraverse(countryId, r) && !w.AreAtWar(countryId, r.ControllerId)) continue;
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
        if (w.HasPact(CountryId, TargetCountryId)) return "Pacto de não-agressão em vigor";
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
        if (w.HasPact(CountryId, TargetCountryId)) return "Pacto de não-agressão em vigor";
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
        if (w.Countries[CountryId].Queue.Count >= w.Rule("production_queue_max", 30f)) return "Fila cheia";
        return null;
    }
    public void Execute(World w) => w.Countries[CountryId].Queue.Add(new ProductionOrder { TemplateId = TemplateId });
}

/// <summary>Liga/desliga a produção em série de uma encomenda: entregue, volta ao fim da fila.</summary>
public sealed record SetProductionRepeatCommand(int CountryId, int Index, bool On) : ICommand
{
    public string? Validate(World w) =>
        !w.Countries.TryGetValue(CountryId, out var c) ? "país inválido"
        : Index < 0 || Index >= c.Queue.Count ? "Encomenda inexistente" : null;
    public void Execute(World w) => w.Countries[CountryId].Queue[Index].Repeat = On;
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
/// o ConstructionSystem conclui ao fim de infra_build_days). Ocupa uma fábrica civil (Industry): com todas
/// tomadas por outras obras, a ordem é recusada mesmo com o cofre cheio.</summary>
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
        if (Industry.Of(w, CountryId).FreeCivil <= 0) return "fábricas civis todas ocupadas";
        return null;
    }

    public void Execute(World w)
    {
        w.Countries[CountryId].Money -= w.Rule("infra_build_cost", 40f);
        var r = w.Regions[RegionId];
        r.Building = true; r.BuildProgress = 0f;
    }
}

/// <summary>Mudar a lei activa do grupo dela (custa law_change_cost pontos de produção).</summary>
public sealed record ChangeLawCommand(int CountryId, string LawId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Laws.TryGetValue(LawId, out var law)) return "lei desconhecida";
        if (w.ActiveLaw(c, law.Group)?.Id == LawId) return "já é a lei activa";
        if (c.Money < w.Rule("law_change_cost", 30f)) return $"faltam pontos de produção ({w.Rule("law_change_cost", 30f):0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        var law = w.Laws[LawId];
        c.Money -= w.Rule("law_change_cost", 30f);
        c.Laws[law.Group] = LawId;
        w.ApplyTechs(c);
        w.Events.Publish(new LawChanged(CountryId, LawId));
    }
}

/// <summary>Fortificar uma região própria (+1 nível; paga fort_build_cost, demora fort_build_days).
/// Ocupa uma fábrica civil (Industry) enquanto a obra durar.</summary>
public sealed record BuildFortCommand(int CountryId, int RegionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Regions.TryGetValue(RegionId, out var r)) return "região inválida";
        if (r.OwnerId != CountryId || r.ControllerId != CountryId) return "a região não é tua";
        if (r.FortBuilding) return "já há uma fortificação em curso";
        if (r.Fort >= (int)w.Rule("fort_max", 5f)) return "fortificação no máximo";
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (c.Money < w.Rule("fort_build_cost", 30f)) return $"faltam pontos de produção ({w.Rule("fort_build_cost", 30f):0})";
        if (Industry.Of(w, CountryId).FreeCivil <= 0) return "fábricas civis todas ocupadas";
        return null;
    }

    public void Execute(World w)
    {
        w.Countries[CountryId].Money -= w.Rule("fort_build_cost", 30f);
        var r = w.Regions[RegionId];
        r.FortBuilding = true; r.FortProgress = 0f;
    }
}

/// <summary>Enviar pontos de produção a um aliado de facção (lend-lease simplificado).</summary>
public sealed record TransferMoneyCommand(int CountryId, int TargetCountryId, float Amount) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t) || t.Capitulated) return "destinatário inválido";
        if (CountryId == TargetCountryId) return "não podes enviar a ti próprio";
        if (!w.SameFaction(CountryId, TargetCountryId)) return "só entre aliados de facção";
        if (Amount <= 0f) return "quantia inválida";
        if (c.Money < Amount) return "não tens pontos suficientes";
        return null;
    }

    public void Execute(World w)
    {
        w.Countries[CountryId].Money -= Amount;
        w.Countries[TargetCountryId].Money += Amount;
        w.Events.Publish(new MoneyTransferred(CountryId, TargetCountryId, Amount));
    }
}

/// <summary>Propor paz branca a um inimigo. A IA aceita se a guerra está parada há peace_stale_days
/// (sem capturas de nenhum lado) ou se já não tem divisões; senão recusa (PeaceOfferRejected).
/// Aceite = uti possidetis imediato (TruceSystem.MakeWhitePeace).</summary>
public sealed record OfferPeaceCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t)) return "país inválido";
        if (!w.AreAtWar(CountryId, TargetCountryId)) return "não estás em guerra com ele";
        return null;
    }

    public void Execute(World w)
    {
        var info = w.Wars.GetValueOrDefault(World.WarKey(CountryId, TargetCountryId));
        bool stale = info is not null
            && w.Clock.Day - Math.Max(info.StartDay, info.LastProgressDay) >= w.Rule("peace_stale_days", 60f);
        bool disarmed = !w.Divisions.Values.Any(d => d.CountryId == TargetCountryId);
        if (stale || disarmed) Systems.TruceSystem.MakeWhitePeace(w, CountryId, TargetCountryId);
        else w.Events.Publish(new PeaceOfferRejected(CountryId, TargetCountryId));
    }
}

/// <summary>Propor paz com exigências territoriais. O derrotado só assina se a pressão a que está
/// sujeito (ocupação, diferença de exércitos, desgaste, capital perdida) pagar o preço do que se lhe
/// pede — ver PeaceTerms. Aceite = as regiões exigidas mudam de dono e a guerra acaba; recusa publica
/// PeaceOfferRejected e nada muda.</summary>
public sealed record DemandPeaceCommand(int CountryId, int TargetCountryId, IReadOnlyList<int> RegionIds) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Countries.ContainsKey(TargetCountryId)) return "país inválido";
        if (!w.AreAtWar(CountryId, TargetCountryId)) return "não estás em guerra com ele";
        if (RegionIds.Count == 0) return "não exigiste nada (usa a paz branca)";
        foreach (var id in RegionIds)
            if (!w.Regions.TryGetValue(id, out var r) || r.OwnerId != TargetCountryId)
                return "só podes exigir regiões dele";
        return null;
    }

    public void Execute(World w)
    {
        if (Systems.PeaceTerms.Evaluate(w, CountryId, TargetCountryId, RegionIds).Accepted)
            Systems.PeaceTerms.Sign(w, CountryId, TargetCountryId, RegionIds);
        else
            w.Events.Publish(new PeaceOfferRejected(CountryId, TargetCountryId));
    }
}

/// <summary>Lançar uma operação de espionagem contra outro país (tabela spy_op).
/// Paga à partida; conclui passado op.Days e o EspionageSystem aplica o efeito.
/// Uma operação de cada vez por par (autor, alvo).
///
/// As operações de scope 'region' (sabotagem na retaguarda) levam RegionId: a região tem de ser
/// controlada pelo alvo e só se sabota quem já está em guerra connosco — mandar equipas explodir
/// pontes é acto de guerra, não espionagem de gabinete.</summary>
public sealed record StartSpyOpCommand(int CountryId, int TargetCountryId, string OpId, int RegionId = 0) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.SpyOps.TryGetValue(OpId, out var op)) return "operação inválida";
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t) || t.Capitulated) return "alvo inválido";
        if (CountryId == TargetCountryId) return "não podes espiar-te a ti próprio";
        if (w.SameFaction(CountryId, TargetCountryId)) return "não se espia um aliado de facção";
        if (w.ActiveSpyOps.Any(o => o.CountryId == CountryId && o.TargetCountryId == TargetCountryId))
            return "já tens uma operação em curso contra ele";
        if (c.Money < op.Cost) return $"faltam pontos de produção ({op.Cost:0})";
        if (op.IsRegional)
        {
            if (!w.Regions.TryGetValue(RegionId, out var r)) return "escolhe uma região";
            if (r.ControllerId != TargetCountryId) return "essa região não é dele";
            if (!w.AreAtWar(CountryId, TargetCountryId)) return "sabotagem só em guerra";
        }
        return null;
    }

    public void Execute(World w)
    {
        var op = w.SpyOps[OpId];
        w.Countries[CountryId].Money -= op.Cost;
        // contra-espionagem do alvo (lei de segurança): a operação demora × counter_intel
        float days = op.Days * w.Countries[TargetCountryId].Stat("counter_intel");
        w.ActiveSpyOps.Add(new ActiveSpyOp { CountryId = CountryId, TargetCountryId = TargetCountryId, OpId = OpId,
                                             DaysLeft = days, RegionId = op.IsRegional ? RegionId : 0 });
        w.Events.Publish(new SpyOpStarted(CountryId, TargetCountryId, OpId));
    }
}

/// <summary>Troca negociada de prisioneiros: homem por homem, com a guerra a decorrer. O mínimo dos dois
/// campos muda de mãos e exchange_return de cada leva chega a casa; o resto ficou pelo caminho. Quem
/// aceita ou recusa é o outro lado, pelas contas do PrisonerExchange — a UI mostra o veredicto antes de
/// se propor, para não haver clique às cegas.</summary>
public sealed record ExchangePrisonersCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t) || t.Capitulated) return "não há com quem negociar";
        if (CountryId == TargetCountryId) return "não se troca com o próprio";
        if (!w.AreAtWar(CountryId, TargetCountryId)) return "sem guerra não há campos para abrir";
        var offer = PrisonerExchange.Evaluate(w, CountryId, TargetCountryId);
        if (offer.Men <= 0) return offer.Reason;
        if (!offer.Accepted) return "recusam a troca: " + offer.Reason;
        return null;
    }

    public void Execute(World w)
    {
        var offer = PrisonerExchange.Evaluate(w, CountryId, TargetCountryId);
        float back = w.Rule("exchange_return", 0.85f);
        PrisonerSystem.Release(w, w.Countries[CountryId], TargetCountryId, offer.Men, back);
        PrisonerSystem.Release(w, w.Countries[TargetCountryId], CountryId, offer.Men, back);
        w.Events.Publish(new PrisonersExchanged(CountryId, TargetCountryId, offer.Men, offer.Home));
    }
}

/// <summary>Responder a uma proposta que o outro lado pôs em cima da mesa (OfferSystem). Aceitar uma
/// paz branca fecha a guerra em uti possidetis (cada um fica com o que ocupa nesse dia); aceitar uma
/// troca de prisioneiros abre os dois campos pelo tamanho que eles tiverem no dia da resposta — o número
/// da proposta é o que se viu quando ela chegou, e uma batalha entretanto pode tê-lo mudado. Recusar
/// tira-a da mesa e nada mais: quem propôs volta a insistir passado offer_period_days.</summary>
public sealed record AnswerOfferCommand(int CountryId, int FromCountryId, bool Accept, string Kind = "prisioneiros") : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.ContainsKey(CountryId)) return "país inválido";
        if (Find(w) is null) return "essa proposta já não está em cima da mesa";
        if (!Accept) return null;
        if (!w.AreAtWar(CountryId, FromCountryId)) return "a guerra acabou: já não há nada para assinar";
        if (Kind == "prisioneiros" && PrisonerExchange.Evaluate(w, FromCountryId, CountryId).Men <= 0)
            return "os campos mudaram: já não há homens dos dois lados";
        if (Kind == "regiao" && Find(w) is PendingOffer cede
            && (!w.Regions.TryGetValue(cede.RegionId, out var r) || r.OwnerId != FromCountryId))
            return "a região prometida já não é deles";
        return null;
    }

    public void Execute(World w)
    {
        var pending = Find(w);
        if (pending is null) return;
        w.Offers.Remove(pending);
        if (Accept)
        {
            if (pending.Kind == "regiao")
            {
                // primeiro a cedência, depois a paz: assim o uti possidetis já a encontra do nosso lado
                if (w.Regions.TryGetValue(pending.RegionId, out var ceded) && ceded.OwnerId == FromCountryId)
                {
                    ceded.OwnerId = CountryId;
                    ceded.ControllerId = CountryId;
                    ceded.Resistance = 0f;                  // entregue à mesa, não tomada à força
                    w.Events.Publish(new RegionCeded(FromCountryId, CountryId, ceded.Id));
                }
                TruceSystem.MakeWhitePeace(w, FromCountryId, CountryId);
            }
            else if (pending.Kind == "paz") TruceSystem.MakeWhitePeace(w, FromCountryId, CountryId);
            else OfferSystem.Exchange(w, FromCountryId, CountryId, PrisonerExchange.Evaluate(w, FromCountryId, CountryId));
        }
        w.Events.Publish(new OfferAnswered(FromCountryId, CountryId, pending.Kind, Accept));
    }

    private PendingOffer? Find(World w) =>
        w.Offers.FirstOrDefault(o => o.FromId == FromCountryId && o.ToId == CountryId && o.Kind == Kind);
}

/// <summary>Dissolver uma divisão fora de combate: devolve disband_manpower_refund dos homens
/// (proporcional ao HP) ao pool do país. HoI4: delete unit, com refund parcial.</summary>
public sealed record DisbandDivisionCommand(int CountryId, int DivisionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Divisions.TryGetValue(DivisionId, out var d)) return "Divisão inexistente";
        if (d.CountryId != CountryId) return "Divisão não é tua";
        if (w.InBattle(DivisionId)) return "Em combate";
        return null;
    }

    public void Execute(World w)
    {
        var d = w.Divisions[DivisionId];
        var c = w.Countries[CountryId];
        float men = w.TemplateCost(d.TemplateId) * w.Rule("manpower_per_cost", 500f)
                    * (d.Hp / 100f) * w.Rule("disband_manpower_refund", 0.5f);
        c.Manpower += men;
        w.Events.Publish(new DivisionDisbanded(DivisionId, CountryId));
        w.RemoveDivision(DivisionId);
    }
}

/// <summary>Propor pacto de não-agressão. A IA aceita se não está a justificar guerra contra o
/// proponente e (é mais fraca em divisões ou partilha um inimigo); senão PactRejected.
/// Aceite = sem DeclareWar entre os dois durante nap_days.</summary>
/// <summary>Destaca um adido militar junto de um país em guerra: paga-se todos os dias e traz experiência
/// de exército enquanto a guerra dele durar (AttacheSystem). Um país só tem um adido de cada vez.</summary>
public sealed record SendAttacheCommand(int CountryId, int HostId) : ICommand
{
    public string? Validate(World w)
    {
        if (w.AttacheBlock(CountryId, HostId) is string why) return why;
        float cost = w.Rule("attache_cost_per_day", 0.5f), days = w.Rule("attache_min_days", 10f);
        if (w.Countries[CountryId].Money < cost * days)
            return $"a missão precisa de {cost * days:0} no cofre ({days:0} dias de estadia)";
        return null;
    }

    public void Execute(World w) => AttacheSystem.Send(w, CountryId, HostId);
}

/// <summary>Chama o adido de volta: acaba a despesa e acaba a aprendizagem.</summary>
public sealed record RecallAttacheCommand(int CountryId) : ICommand
{
    public string? Validate(World w) =>
        !w.Countries.ContainsKey(CountryId) ? "país inválido"
        : !w.Attaches.ContainsKey(CountryId) ? "não há adido destacado" : null;

    public void Execute(World w) => w.Attaches.Remove(CountryId);
}

public sealed record ProposeNonAggressionCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t) || t.Capitulated) return "alvo inválido";
        if (CountryId == TargetCountryId) return "contigo próprio não";
        if (w.AreAtWar(CountryId, TargetCountryId)) return "estão em guerra — propõe paz";
        if (w.SameFaction(CountryId, TargetCountryId)) return "aliados de facção não precisam de pacto";
        if (w.HasPact(CountryId, TargetCountryId)) return "já há pacto em vigor";
        if (c.Money < w.Rule("nap_cost", 20f)) return $"faltam pontos de produção ({w.Rule("nap_cost", 20f):0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId]; var t = w.Countries[TargetCountryId];
        c.Money -= w.Rule("nap_cost", 20f);
        int myDivs = w.Divisions.Values.Count(d => d.CountryId == CountryId);
        int theirDivs = w.Divisions.Values.Count(d => d.CountryId == TargetCountryId);
        bool commonEnemy = t.AtWarWith.Any(c.AtWarWith.Contains);
        bool accepts = t.JustifyTarget != CountryId && (theirDivs < myDivs || commonEnemy);
        if (!accepts) { w.Events.Publish(new PactRejected(CountryId, TargetCountryId)); return; }
        int until = w.Clock.Day + (int)w.Rule("nap_days", 180f);
        w.Pacts[World.WarKey(CountryId, TargetCountryId)] = until;
        w.Events.Publish(new PactSigned(CountryId, TargetCountryId, until));
    }
}

/// <summary>Retirar as próprias divisões de uma batalha: saem das listas com organização
/// × retreat_org_penalty. Defensores precisam de região vizinha transitável (senão "cercado");
/// atacantes já estão fisicamente na origem. A batalha resolve-se sozinha se uma lista esvaziar.</summary>
public sealed record RetreatFromBattleCommand(int CountryId, int RegionId) : ICommand
{
    public string? Validate(World w)
    {
        var b = w.ActiveBattles.FirstOrDefault(x => x.RegionId == RegionId);
        if (b is null) return "não há batalha nesta região";
        bool mineAtt = b.Attackers.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == CountryId);
        bool mineDef = b.Defenders.Any(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == CountryId);
        if (!mineAtt && !mineDef) return "não tens divisões nesta batalha";
        if (mineDef && FindFallback(w, CountryId, RegionId) is null) return "cercado — sem região para onde retirar";
        return null;
    }

    public void Execute(World w)
    {
        var b = w.ActiveBattles.First(x => x.RegionId == RegionId);
        float pen = w.Rule("retreat_org_penalty", 0.5f);
        int n = 0;
        foreach (var id in b.Attackers.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == CountryId).ToList())
        {
            var d = w.Divisions[id];
            d.Org *= pen; d.ClearPath();
            b.Attackers.Remove(id); n++;
        }
        var fallback = FindFallback(w, CountryId, RegionId);
        foreach (var id in b.Defenders.Where(id => w.Divisions.TryGetValue(id, out var d) && d.CountryId == CountryId).ToList())
        {
            var d = w.Divisions[id];
            d.Org *= pen; d.ClearPath();
            b.Defenders.Remove(id);
            if (fallback is int dest) w.PlaceDivision(d, dest);
            n++;
        }
        if (n > 0) w.Events.Publish(new BattleRetreat(RegionId, CountryId, n));
    }

    /// <summary>Região vizinha transitável (própria ou aliada) com mais divisões próprias; null = cercado.</summary>
    private int? FindFallback(World w, int countryId, int regionId)
    {
        int? best = null; int bestOwn = -1;
        foreach (var nb in w.Regions[regionId].Neighbours)
        {
            var r = w.Regions[nb];
            if (!w.CanTraverse(countryId, r)) continue;
            int own = r.DivisionIds.Count(id => w.Divisions[id].CountryId == countryId);
            if (own > bestOwn || (own == bestOwn && (best is null || nb < best))) { best = nb; bestOwn = own; }
        }
        return best;
    }
}

/// <summary>Cria um acordo de comércio: o comprador aluga Units dos depósitos do vendedor e paga-lhas todos
/// os dias (TradeSystem). O preço do dia fica travado no contrato, e `Days` marca-lhe prazo — 0 mantém o
/// acordo aberto até alguém o cancelar, como eram todos antes dos tratados. Um contrato longo obriga o
/// comprador a mostrar cofre para o prazo inteiro (trade_deal_deposit_days de estadia), porque prender
/// preço barato sem ter com que o pagar era comprar mercado a crédito.
/// Vendedor precisa de unidades livres; nada entre inimigos.</summary>
public sealed record CreateTradeDealCommand(int CountryId, int SellerId, string ResourceId, float Units, int Days = 0) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var buyer) || buyer.Capitulated) return "comprador inválido";
        if (!w.Countries.TryGetValue(SellerId, out var seller) || seller.Capitulated) return "vendedor inválido";
        if (CountryId == SellerId) return "não podes comprar a ti próprio";
        if (Units <= 0f) return "unidades inválidas";
        if (Days < 0) return "prazo inválido";
        if (!w.ResourceDefs.ContainsKey(ResourceId)) return "recurso desconhecido";
        if (w.AreAtWar(CountryId, SellerId)) return "estão em guerra";
        if (w.TradeDeals.Any(d => d.BuyerId == CountryId && d.SellerId == SellerId && d.ResourceId == ResourceId))
            return "já há contrato desse recurso com esse país";
        float free = ResourceSystem.Controlled(w, SellerId, ResourceId) - TradeSystem.Sold(w, SellerId, ResourceId);
        if (free < Units - 1e-3f) return "o vendedor não tem unidades livres";
        float day = Units * TradeSystem.Price(w, SellerId, ResourceId);
        float need = day * (Days > 0 ? MathF.Min(Days, w.Rule("trade_deal_deposit_days", 10f)) : 1f);
        if (buyer.Money < need) return $"o contrato precisa de {need:0} no cofre ({day:0.0} por dia)";
        return null;
    }

    public void Execute(World w)
    {
        var deal = new TradeDeal
        {
            BuyerId = CountryId, SellerId = SellerId, ResourceId = ResourceId, Units = Units,
            PricePerUnit = TradeSystem.Price(w, SellerId, ResourceId),
            UntilDay = Days > 0 ? w.Clock.Day + Days : 0,
        };
        w.TradeDeals.Add(deal);
        w.Events.Publish(new TradeDealCreated(CountryId, SellerId, ResourceId, Units));
    }
}

/// <summary>Cancela um acordo de comércio em que o país participa (qualquer um dos lados).</summary>
public sealed record CancelTradeDealCommand(int CountryId, int OtherId, string ResourceId) : ICommand
{
    public string? Validate(World w) =>
        w.TradeDeals.Any(d => d.ResourceId == ResourceId
            && ((d.BuyerId == CountryId && d.SellerId == OtherId) || (d.SellerId == CountryId && d.BuyerId == OtherId)))
        ? null : "acordo não existe";

    public void Execute(World w)
    {
        var d = w.TradeDeals.First(t => t.ResourceId == ResourceId
            && ((t.BuyerId == CountryId && t.SellerId == OtherId) || (t.SellerId == CountryId && t.BuyerId == OtherId)));
        w.TradeDeals.Remove(d);
        w.Events.Publish(new TradeDealEnded(d.BuyerId, d.SellerId, d.ResourceId));
    }
}


/// <summary>Começa a obra de um edifício da tabela building numa região própria e controlada.
/// Ocupa uma fábrica civil (Industry) enquanto a obra durar.</summary>
public sealed record BuildBuildingCommand(int CountryId, int RegionId, string BuildingId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Regions.TryGetValue(RegionId, out var r)) return "região inválida";
        if (r.ControllerId != CountryId || r.OwnerId != CountryId) return "a região não é tua";
        if (!w.BuildingDefs.TryGetValue(BuildingId, out var def)) return "edifício desconhecido";
        if (def.Coastal && !r.Coastal) return "só na costa";
        if (r.Project is not null) return "já há uma obra de edifício em curso";
        if (r.Buildings.GetValueOrDefault(BuildingId) >= def.MaxLevel) return "nível máximo atingido";
        if (c.Money < def.Cost) return "pontos de produção insuficientes";
        if (Industry.Of(w, CountryId).FreeCivil <= 0) return "fábricas civis todas ocupadas";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId]; var r = w.Regions[RegionId];
        c.Money -= w.BuildingDefs[BuildingId].Cost;
        r.Project = BuildingId; r.ProjectProgress = 0f;
    }
}

/// <summary>Comprar um esquadrão aéreo: +1 AirPower por air_wing_cost pontos. O poder aéreo
/// relativo dos dois lados modula a força no combate terrestre (CombatSystem, air_combat_weight).</summary>
public sealed record BuyAirWingCommand(int CountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        float cost = w.Rule("air_wing_cost", 60f);
        if (c.Money < cost) return $"faltam pontos de produção ({cost:0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.Money -= w.Rule("air_wing_cost", 60f);
        c.AirPower += 1f;
        w.Events.Publish(new Events.AirWingBought(CountryId, (int)c.AirPower));
    }
}


/// <summary>Destacar esquadrões para o céu de uma região (AirMissionSystem). As asas saem do pool nacional
/// enquanto a missão durar, custam estadia todos os dias e podem ser abatidas onde o céu está disputado.
/// Mandar mais asas para o mesmo céu engrossa a missão que lá está; mandar com outra tarefa re-emprega as
/// que já lá estavam.</summary>
public sealed record AssignAirMissionCommand(int CountryId, int RegionId, string MissionId, float Wings) : ICommand
{
    public string? Validate(World w) => AirMissionSystem.Block(w, CountryId, RegionId, MissionId, Wings);

    public void Execute(World w) => AirMissionSystem.Assign(w, CountryId, RegionId, MissionId, Wings);
}

/// <summary>Chamar de volta os esquadrões que estão sobre uma região: as asas voltam ao pool no mesmo dia.</summary>
public sealed record RecallAirMissionCommand(int CountryId, int RegionId) : ICommand
{
    public string? Validate(World w) =>
        w.AirMissions.Any(m => m.CountryId == CountryId && m.RegionId == RegionId) ? null : "não há missão nesse céu";

    public void Execute(World w) => AirMissionSystem.Recall(w, CountryId, RegionId);
}

/// <summary>Comprar um navio de guerra: +1 Warships por naval_ship_cost pontos. O pool nacional é o que
/// se pode destacar para o mar; quem for ao fundo numa missão não volta ao pool.</summary>
public sealed record BuyWarshipCommand(int CountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        float cost = w.Rule("naval_ship_cost", 90f);
        if (c.Money < cost) return $"faltam pontos de produção ({cost:0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.Money -= w.Rule("naval_ship_cost", 90f);
        c.Warships += 1f;
    }
}

/// <summary>Destacar navios para o mar de uma costa com uma tarefa (bloqueio, escolta ou patrulha). Passa
/// pelo NavalMissionSystem, que é quem sabe quantos navios há no porto e se o mar está ao alcance.</summary>
public sealed record AssignNavalMissionCommand(int CountryId, int RegionId, string MissionId, float Ships) : ICommand
{
    public string? Validate(World w) => NavalMissionSystem.Block(w, CountryId, RegionId, MissionId, Ships);

    public void Execute(World w) => NavalMissionSystem.Assign(w, CountryId, RegionId, MissionId, Ships);
}

/// <summary>Chamar a esquadra de volta ao porto: os navios voltam ao pool no mesmo dia.</summary>
public sealed record RecallNavalMissionCommand(int CountryId, int RegionId) : ICommand
{
    public string? Validate(World w) =>
        w.NavalMissions.Any(m => m.CountryId == CountryId && m.RegionId == RegionId) ? null : "não há esquadra nesse mar";

    public void Execute(World w) => NavalMissionSystem.Recall(w, CountryId, RegionId);
}

/// <summary>Construir uma ogiva nuclear: exige a tecnologia que dá o multiplicador "nuclear" (> 1)
/// e nuke_cost pontos de produção. NuclearStrikeCommand gasta uma ogiva.</summary>
public sealed record BuildNukeCommand(int CountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (c.Stat("nuclear") <= 1f) return "requer o programa nuclear (tecnologia)";
        float cost = w.Rule("nuke_cost", 400f);
        if (c.Money < cost) return $"faltam pontos de produção ({cost:0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.Money -= w.Rule("nuke_cost", 400f);
        c.Nukes += 1;
        w.Events.Publish(new Events.NukeBuilt(CountryId, c.Nukes));
    }
}

/// <summary>Ataque nuclear a uma região controlada por um inimigo em guerra: as divisões lá
/// perdem quase tudo (nuke_div_hp_mult/nuke_div_org_mult), a infra-estrutura e o forte são
/// arrasados, o alvo perde estabilidade e ganha exaustão — e o atacante também paga em
/// estabilidade (nuke_self_stability_hit, opinião mundial).</summary>
public sealed record NuclearStrikeCommand(int CountryId, int RegionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (c.Nukes <= 0) return "sem ogivas prontas";
        if (!w.Regions.TryGetValue(RegionId, out var r)) return "região inválida";
        if (r.ControllerId == CountryId) return "a região é tua";
        if (!c.AtWarWith.Contains(r.ControllerId)) return "não estás em guerra com o controlador";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId]; var r = w.Regions[RegionId];
        c.Nukes -= 1;
        float hpMult = w.Rule("nuke_div_hp_mult", 0.3f), orgMult = w.Rule("nuke_div_org_mult", 0.2f);
        int hit = 0;
        foreach (var id in r.DivisionIds.ToList())
            if (w.Divisions.TryGetValue(id, out var d)) { d.Hp *= hpMult; d.Org *= orgMult; hit++; }
        r.Infrastructure = MathF.Max(0.1f, r.Infrastructure * w.Rule("nuke_infra_mult", 0.5f));
        r.Fort = Math.Max(0, r.Fort - (int)w.Rule("nuke_fort_damage", 2f));
        if (w.Countries.TryGetValue(r.ControllerId, out var t))
        {
            t.Stability = MathF.Max(0f, t.Stability - w.Rule("nuke_stability_hit", 10f));
            t.WarExhaustion = MathF.Min(w.Rule("exhaustion_max", 30f), t.WarExhaustion + w.Rule("nuke_exhaustion", 5f));
        }
        c.Stability = MathF.Max(0f, c.Stability - w.Rule("nuke_self_stability_hit", 4f));
        w.Events.Publish(new Events.NukeStruck(CountryId, RegionId, r.ControllerId, hit));
    }
}

/// <summary>Activa uma decisão nacional (tabela decision): paga Cost, efeito Mult no StatKey durante
/// Days, depois Cooldown dias de espera antes de repetir.</summary>
public sealed record ActivateDecisionCommand(int CountryId, string DecisionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.DecisionDefs.TryGetValue(DecisionId, out var def)) return "decisão desconhecida";
        if (w.ActiveDecisions.Any(a => a.CountryId == CountryId && a.DecisionId == DecisionId)) return "já está activa";
        if (c.DecisionCooldownUntil.TryGetValue(DecisionId, out var until) && until > w.Clock.Day)
            return $"em espera até ao dia {until}";
        if (c.Money < def.Cost) return "pontos de produção insuficientes";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId]; var def = w.DecisionDefs[DecisionId];
        c.Money -= def.Cost;
        w.ActiveDecisions.Add(new ActiveDecision { CountryId = CountryId, DecisionId = DecisionId, UntilDay = w.Clock.Day + def.Days });
        c.DecisionCooldownUntil[DecisionId] = w.Clock.Day + def.Days + def.Cooldown;
        DecisionSystem.Recompute(w);
        w.Events.Publish(new DecisionActivated(CountryId, DecisionId));
    }
}

/// <summary>Cria um grupo de exércitos vazio. Limitado a army_group_max por país; sem nome, dá-lhe
/// a ordinal seguinte ("3.º Exército").</summary>
public sealed record CreateArmyGroupCommand(int CountryId, string? Name = null) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (Count(w, CountryId) >= (int)w.Rule("army_group_max", 6f)) return "não há mais estados-maiores";
        return null;
    }

    public void Execute(World w)
    {
        int id = w.NewArmyGroupId();
        string name = string.IsNullOrWhiteSpace(Name) ? $"{Count(w, CountryId) + 1}.º Exército" : Name.Trim();
        w.ArmyGroups[id] = new ArmyGroup { Id = id, CountryId = CountryId, Name = name };
    }

    private static int Count(World w, int countryId) => w.ArmyGroups.Values.Count(g => g.CountryId == countryId);
}

/// <summary>Dissolve o grupo. As divisões ficam onde estão, com as ordens que tinham — só perdem o comando.</summary>
public sealed record DisbandArmyGroupCommand(int CountryId, int GroupId) : ICommand
{
    public string? Validate(World w) =>
        !w.ArmyGroups.TryGetValue(GroupId, out var g) ? "grupo inexistente"
        : g.CountryId != CountryId ? "grupo não é teu" : null;

    public void Execute(World w)
    {
        if (!w.ArmyGroups.Remove(GroupId, out var g)) return;
        foreach (int id in g.Divisions)
            if (w.Divisions.TryGetValue(id, out var d) && d.GroupId == GroupId) d.GroupId = null;
        if (g.GeneralId is not null && w.Countries.TryGetValue(g.CountryId, out var c))
            World.ApplyGenerals(w, c);   // o comandante volta ao estado-maior do país
    }
}

/// <summary>Atribui (ou tira) a frente do grupo: o país inimigo para onde ele marcha.</summary>
public sealed record SetArmyGroupFrontCommand(int CountryId, int GroupId, int? FrontCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.ArmyGroups.TryGetValue(GroupId, out var g)) return "grupo inexistente";
        if (g.CountryId != CountryId) return "grupo não é teu";
        if (FrontCountryId is not int foe) return null;
        if (!w.Countries.ContainsKey(foe)) return "país inexistente";
        if (!w.AreAtWar(CountryId, foe)) return "só se atribui uma frente contra quem estás em guerra";
        return null;
    }

    public void Execute(World w)
    {
        var g = w.ArmyGroups[GroupId];
        g.FrontCountryId = FrontCountryId;
        if (FrontCountryId is null) g.Stance = GroupStance.Hold;   // sem frente não há ordem que se cumpra
    }
}

/// <summary>Postura do grupo: avançar sobre a frente, segurar a linha do lado de cá dela ou ficar parado.</summary>
public sealed record SetArmyGroupStanceCommand(int CountryId, int GroupId, GroupStance Stance) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.ArmyGroups.TryGetValue(GroupId, out var g)) return "grupo inexistente";
        if (g.CountryId != CountryId) return "grupo não é teu";
        if (Stance != GroupStance.Hold && g.FrontCountryId is null) return "sem frente atribuída não há para onde avançar";
        return null;
    }

    public void Execute(World w) => w.ArmyGroups[GroupId].Stance = Stance;
}

/// <summary>Põe uma divisão às ordens de um grupo (GroupId null = tira-a de qualquer grupo).
/// Uma divisão só serve num grupo de cada vez.</summary>
/// <summary>Destaca um comandante contratado para um grupo de exércitos (ou chama-o de volta com null).
///
/// Enquanto está destacado, o bónus dele deixa de valer para o país inteiro e passa a valer só para as
/// divisões deste grupo, multiplicado por general_command_bonus. Concentrar ou espalhar o estado-maior
/// passa a ser uma decisão: o Muralha à frente do exército que segura a linha vale mais do que espalhado
/// por divisões que nem estão a combater.</summary>
public sealed record AssignGeneralCommand(int CountryId, int GroupId, string? GeneralId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.ArmyGroups.TryGetValue(GroupId, out var g)) return "grupo inexistente";
        if (g.CountryId != CountryId) return "grupo não é teu";
        if (GeneralId is not string id) return null;
        if (!w.Countries.TryGetValue(CountryId, out var c) || !c.Generals.Contains(id)) return "esse comandante não serve neste exército";
        if (!w.GeneralDefs.ContainsKey(id)) return "comandante desconhecido";
        var busy = w.ArmyGroups.Values.FirstOrDefault(x => x.Id != GroupId && x.GeneralId == id);
        return busy is null ? null : $"já comanda o {busy.Name}";
    }

    public void Execute(World w)
    {
        w.ArmyGroups[GroupId].GeneralId = GeneralId;
        if (w.Countries.TryGetValue(CountryId, out var c)) World.ApplyGenerals(w, c);
    }
}

public sealed record AssignDivisionCommand(int CountryId, int DivisionId, int? GroupId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Divisions.TryGetValue(DivisionId, out var d)) return "Divisão inexistente";
        if (d.CountryId != CountryId) return "Divisão não é tua";
        if (GroupId is not int gid) return null;
        if (!w.ArmyGroups.TryGetValue(gid, out var g)) return "grupo inexistente";
        if (g.CountryId != CountryId) return "grupo não é teu";
        return null;
    }

    public void Execute(World w)
    {
        if (GroupId is int gid) w.JoinGroup(w.ArmyGroups[gid], DivisionId);
        else w.LeaveGroup(DivisionId);
    }
}

/// <summary>Contrata um comandante (tabela general): paga o custo único e ganha o multiplicador dele
/// enquanto servir. Limitado a general_slots comandantes por país, sem repetir arquétipos.</summary>
/// <summary>Adopta um degrau de doutrina de exército, pago com a experiência de campanha (ArmyXpSystem).
/// A primeira doutrina escolhe a escola e fecha as outras — daí a mensagem dizer quem fechou o quê.</summary>
public sealed record AdoptDoctrineCommand(int CountryId, string DoctrineId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.ArmyDoctrines.TryGetValue(DoctrineId, out var d)) return "doutrina desconhecida";
        if (c.Doctrines.Contains(DoctrineId)) return "já é doutrina do exército";
        if (w.DoctrineBlock(c, DoctrineId) is string block)
            return block.StartsWith('!')
                ? $"Escola fechada por {w.ArmyDoctrines[block[1..]].Name}"
                : $"Precisa de {(w.ArmyDoctrines.TryGetValue(block, out var need) ? need.Name : block)}";
        if (c.ArmyXp < d.Cost) return $"faltam {d.Cost - c.ArmyXp:0} de experiência";
        return null;
    }

    public void Execute(World w) => ArmyXpSystem.Adopt(w, w.Countries[CountryId], DoctrineId);
}

public sealed record HireGeneralCommand(int CountryId, string GeneralId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.GeneralDefs.TryGetValue(GeneralId, out var def)) return "comandante desconhecido";
        if (c.Generals.Contains(GeneralId)) return "já serve neste exército";
        if (c.Generals.Count >= (int)w.Rule("general_slots", 3f)) return "estado-maior completo";
        if (c.Money < def.Cost) return "pontos de produção insuficientes";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.Money -= w.GeneralDefs[GeneralId].Cost;
        c.Generals.Add(GeneralId);
        World.ApplyGenerals(w, c);
        w.Events.Publish(new GeneralHired(CountryId, GeneralId));
    }
}

/// <summary>Dispensa um comandante: o multiplicador cai de imediato e o custo não volta.</summary>
public sealed record DismissGeneralCommand(int CountryId, string GeneralId) : ICommand
{
    public string? Validate(World w) =>
        !w.Countries.TryGetValue(CountryId, out var c) ? "país inválido"
        : !c.Generals.Contains(GeneralId) ? "não serve neste exército" : null;

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.Generals.Remove(GeneralId);
        foreach (var g in w.ArmyGroups.Values)
            if (g.CountryId == CountryId && g.GeneralId == GeneralId) g.GeneralId = null;   // dispensado não fica a comandar
        World.ApplyGenerals(w, c);
        w.Events.Publish(new GeneralDismissed(CountryId, GeneralId));
    }
}
