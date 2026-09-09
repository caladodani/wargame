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
        if (!World.TechIsFor(t, c)) return "essa tecnologia é de outro país";
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
        if (d.InFlight) return "Em voo — só depois de aterrar";
        if (w.InBattle(DivisionId)) return "Em combate";
        if (NavalInvasionSystem.Embarked(w, DivisionId)) return "Embarcada numa operação anfíbia";
        var path = FindPath(w, d.RegionId, TargetRegionId, CountryId);
        if (path is null) return "Sem caminho por terra ou mar: só por território próprio, aliado ou inimigo";
        // Uma ordem de marcha atravessa o mar para terra nossa ou de aliado; assaltar uma praia inimiga é
        // outra coisa e tem comando próprio (PlanNavalInvasionCommand).
        int at = d.RegionId;
        foreach (int hop in path)
        {
            if (w.IsSeaHop(at, hop) && w.IsHostile(CountryId, w.Regions[hop]))
                return "Praia inimiga: isso é uma operação anfíbia, não uma ordem de marcha";
            at = hop;
        }
        return null;
    }

    public void Execute(World w)
    {
        var d = w.Divisions[DivisionId];
        d.AutoAdvance = false;   // ordem manual manda: desliga o avanço automático
        Redeploy.Stop(d);        // marchar é marchar: uma ordem de marcha tira a tropa do comboio
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
/// <summary>Abre uma justificação de guerra. Custa poder político (justify_cost) — o pretexto fabrica-se
/// nas câmaras e não nas fábricas — e contra quem não faz fronteira connosco só passa com o mundo já
/// inquieto (justify_far_tension): em pleno sossego ninguém manda um exército para o outro hemisfério.</summary>
public sealed record JustifyWarCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (CountryId == TargetCountryId) return "Não podes justificar contra ti próprio";
        if (!w.Countries.TryGetValue(CountryId, out var me) || me.Capitulated) return "País inexistente";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t)) return "País inexistente";
        if (t.Capitulated) return "Já capitulou";
        if (w.SameFaction(CountryId, TargetCountryId)) return "Aliados na mesma facção";
        if (w.HasPact(CountryId, TargetCountryId)) return "Pacto de não-agressão em vigor";
        if (me.AtWarWith.Contains(TargetCountryId)) return "Já em guerra";
        if (me.JustifyTarget == TargetCountryId) return "Já a justificar";
        float cost = w.Rule("justify_cost", 25f);
        if (me.Political < cost) return $"falta poder político ({cost:0})";
        float far = w.Rule("justify_far_tension", 25f);
        if (far > 0f && WorldTension.Of(w) < far && !w.SharesBorder(CountryId, TargetCountryId))
            return $"longe demais para o mundo aceitar (tensão {WorldTension.Of(w):0} de {far:0})";
        return null;
    }
    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.Political -= w.Rule("justify_cost", 25f);
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
        // Quem escolhe é o país em que o evento caiu. Nos de data marcada isso está na tabela; nos de
        // estado só se sabe no dia (World.NewsFired) — a notícia não tinha dono antes de acontecer.
        int? landed = w.NewsFired.TryGetValue(EventId, out var hit) ? (hit.CountryId == 0 ? null : hit.CountryId) : e.CountryId;
        if (landed != CountryId) return "Evento não é teu";
        if (!w.NewsFired.ContainsKey(EventId) && e.Day > w.Clock.Day) return "Evento ainda não aconteceu";
        if (e.IsWatch && !w.NewsFired.ContainsKey(EventId)) return "Evento ainda não aconteceu";
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
    /// <summary>As contas que valem para desenhar e para redesenhar, num sítio só: nome, tipos, e o que
    /// cabe numa divisão. Os tectos de linha e de apoio são os da prancheta (regras design_*), para o
    /// comando nunca recusar aquilo que o desenhador deixou montar.</summary>
    public static string? Check(World w, int countryId, string? name, IReadOnlyList<(int UnitTypeId, int Qty)>? units)
    {
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "País inválido";
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length is < 1 or > 40) return "Nome: 1 a 40 caracteres";
        if (units is null || units.Count is < 1 or > 10) return "1 a 10 tipos de unidade";
        if (units.Select(u => u.UnitTypeId).Distinct().Count() != units.Count) return "Tipo de unidade repetido";
        int total = 0, line = 0, support = 0;
        foreach (var (unitId, qty) in units)
        {
            if (qty is < 1 or > 30) return "Quantidade: 1 a 30 por tipo";
            total += qty;
            UnitType u;
            try { u = w.Units.GetUnitType(unitId); } catch (InvalidOperationException) { return "Tipo de unidade inexistente"; }
            if (u.Category == "support") support += qty; else line += qty;
        }
        if (total > 60) return "Máximo 60 unidades por divisão";
        int lineMax = Systems.TemplateDesign.LineMax(w), supMax = Systems.TemplateDesign.SupportMax(w);
        if (line > lineMax) return $"Máximo {lineMax} batalhões de linha";
        if (support > supMax) return $"Máximo {supMax} companhias de apoio";
        return null;
    }

    public string? Validate(World w) => Check(w, CountryId, Name, Units);

    public void Execute(World w)
    {
        int id = w.CustomTemplateIds.Count == 0 ? World.CustomTemplateBase : w.CustomTemplateIds.Max() + 1;
        w.Units.AddCustomTemplate(id, CountryId, Name.Trim(), Units);
        w.CustomTemplateIds.Add(id);
        w.Events.Publish(new Events.TemplateCreated(CountryId, id));
    }
}

/// <summary>Redesenha um modelo já criado em jogo (HoI4: editar o template em vez de fazer outro).
///
/// Desenhar era só de uma vez: um modelo com um batalhão a mais do que devia ficava na lista para sempre e
/// a única saída era desenhar outro ao lado, com outro nome. Agora abre-se a prancheta com o desenho lá
/// dentro e muda-se. Só se mexe nos modelos desenhados em jogo — os que vêm da tabela são a doutrina do
/// país e não se apagam à mão.
///
/// As divisões que já estão no mapa passam a ler o modelo novo: é a mesma ficha para todas, como no HoI4
/// (lá pede-se equipamento novo para a mudança pegar; aqui o armazém já faz esse papel — uma divisão
/// re-armada com um modelo mais caro fica com menos material do que o modelo pede, e vê-se na chapa).</summary>
public sealed record EditTemplateCommand(int CountryId, int TemplateId, string Name,
    IReadOnlyList<(int UnitTypeId, int Qty)> Units) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.CustomTemplateIds.Contains(TemplateId)) return "Só se redesenham modelos feitos em jogo";
        DivisionTemplate t;
        try { t = w.Units.GetTemplate(TemplateId); } catch (InvalidOperationException) { return "Modelo inexistente"; }
        if (t.CountryId != CountryId) return "Modelo não é teu";
        return CreateTemplateCommand.Check(w, CountryId, Name, Units);
    }

    public void Execute(World w)
    {
        w.Units.AddCustomTemplate(TemplateId, CountryId, Name.Trim(), Units);
        w.Stats.Invalidate(TemplateId);     // a ficha de combate é outra a partir de hoje
        w.Events.Publish(new Events.TemplateEdited(CountryId, TemplateId));
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

/// <summary>Abre uma linha de material (HoI4: linha de produção de equipamento): a fábrica deixa de montar
/// divisões e passa a entregar conjuntos daquele tipo ao armazém do país, sem parar. É de lá que saem os
/// reforços das divisões gastas — sem armazém, uma divisão batida fica batida.</summary>
public sealed record BuildKitCommand(int CountryId, int UnitTypeId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c)) return "país inválido";
        try { w.Units.GetUnitType(UnitTypeId); } catch (InvalidOperationException) { return "Tipo de unidade inexistente"; }
        if (c.Queue.Count >= w.Rule("production_queue_max", 30f)) return "Fila cheia";
        return null;
    }
    public void Execute(World w) =>
        w.Countries[CountryId].Queue.Add(new ProductionOrder { UnitTypeId = UnitTypeId, Repeat = true });
}

/// <summary>Liga/desliga a produção em série de uma encomenda: entregue, volta ao fim da fila.</summary>
public sealed record SetProductionRepeatCommand(int CountryId, int Index, bool On) : ICommand
{
    public string? Validate(World w) =>
        !w.Countries.TryGetValue(CountryId, out var c) ? "país inválido"
        : Index < 0 || Index >= c.Queue.Count ? "Encomenda inexistente" : null;
    public void Execute(World w) => w.Countries[CountryId].Queue[Index].Repeat = On;
}

/// <summary>Dedica fábricas militares a uma encomenda (HoI4: linhas de produção atribuídas).
///
/// As fábricas militares sempre serviram a fila de cima para baixo, uma por encomenda: com três fábricas
/// andavam as três primeiras encomendas ao mesmo ritmo e mais nada se podia fazer. Uma coluna blindada
/// urgente demorava o mesmo que a infantaria ao lado, e as fábricas de um império industrial ficavam a
/// dividir-se por encomendas que ninguém tinha pressa nenhuma em receber.
///
/// Agora concentram-se: cada encomenda diz quantas fábricas quer, cada fábrica vale um dia de trabalho por
/// dia, e o que se dá a uma tira-se ao resto da fila. O tecto é o número de fábricas militares do país e a
/// regra order_factories_max, o que for menor.</summary>
public sealed record SetOrderFactoriesCommand(int CountryId, int Index, int Factories) : ICommand
{
    /// <summary>Quantas fábricas se podem dedicar a uma só encomenda: as que o país tem, sem passar o tecto
    /// da regra order_factories_max.</summary>
    public static int Cap(World w, int countryId) =>
        Math.Max(1, Math.Min(Industry.Of(w, countryId).Military, (int)w.Rule("order_factories_max", 8f)));

    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c)) return "país inválido";
        if (Index < 0 || Index >= c.Queue.Count) return "Encomenda inexistente";
        if (Factories < 1) return "Uma encomenda leva pelo menos uma fábrica";
        int cap = Cap(w, CountryId);
        if (Factories > cap) return $"Só podes dedicar {cap} fábricas a uma encomenda";
        if (c.Queue[Index].Factories == Factories) return "A encomenda já tem essas fábricas";
        return null;
    }
    public void Execute(World w) => w.Countries[CountryId].Queue[Index].Factories = Factories;
}

/// <summary>Muda uma encomenda de lugar na fila de produção (de `Index` para `ToIndex`).
///
/// A ordem da fila é a prioridade: o ProductionSystem gasta o cofre de cima para baixo e só as primeiras
/// encomendas por acabar têm linha de montagem. Até aqui essa ordem era a de chegada e não havia maneira
/// nenhuma de a mudar — quem encomendasse cinco divisões de infantaria antes da coluna blindada de que
/// precisava esta semana tinha de cancelar tudo e voltar a encomendar, perdendo o lugar na fila e a
/// papelada toda. Agora arrasta-se e o progresso vai com a encomenda.</summary>
public sealed record MoveProductionOrderCommand(int CountryId, int Index, int ToIndex) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c)) return "país inválido";
        if (Index < 0 || Index >= c.Queue.Count) return "Encomenda inexistente";
        if (ToIndex < 0 || ToIndex >= c.Queue.Count) return "Lugar inexistente na fila";
        if (ToIndex == Index) return "A encomenda já está nesse lugar";
        return null;
    }

    public void Execute(World w)
    {
        var q = w.Countries[CountryId].Queue;
        var order = q[Index];
        q.RemoveAt(Index);
        q.Insert(ToIndex, order);
    }
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

/// <summary>Assentar carril numa região própria (paga rail_cost já; o ConstructionSystem conclui ao fim de
/// rail_days e sobe Region.Rail). A via férrea é a metade visível da logística do HoI4: baixa o que cada
/// salto da rede custa (SupplySystem.StepCost) e desenha-se no mapa. Ocupa uma fábrica civil, como as
/// outras obras — uma linha de comboio não se assenta com boa vontade.</summary>
public sealed record BuildRailCommand(int CountryId, int RegionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Regions.TryGetValue(RegionId, out var r)) return "região inválida";
        if (r.OwnerId != CountryId || r.ControllerId != CountryId) return "a região não é tua";
        if (r.RailBuilding) return "já há carril a ser assente";
        if (Math.Max(0, r.Rail) >= (int)w.Rule("rail_max", 4f)) return "via férrea no máximo";
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (c.Money < w.Rule("rail_cost", 25f)) return $"faltam pontos de produção ({w.Rule("rail_cost", 25f):0})";
        if (Industry.Of(w, CountryId).FreeCivil <= 0) return "fábricas civis todas ocupadas";
        return null;
    }

    public void Execute(World w)
    {
        w.Countries[CountryId].Money -= w.Rule("rail_cost", 25f);
        var r = w.Regions[RegionId];
        r.RailBuilding = true; r.RailProgress = 0f;
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

/// <summary>Mudar a lei activa do grupo dela. Paga-se com poder político — uma lei não se compra com
/// fábricas — e as leis que mexem fundo na vida da gente só passam com o mundo já inquieto (min_tension).</summary>
public sealed record ChangeLawCommand(int CountryId, string LawId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Laws.TryGetValue(LawId, out var law)) return "lei desconhecida";
        if (!World.LawIsFor(law, c)) return "essa lei é de outro país";
        if (w.ActiveLaw(c, law.Group)?.Id == LawId) return "já é a lei activa";
        if (law.MinTension > 0f && WorldTension.Of(w) < law.MinTension)
            return $"o país não aprova isto em tempo de paz (tensão {WorldTension.Of(w):0} de {law.MinTension:0})";
        if (c.Political < w.Rule("law_change_cost", 30f)) return $"falta poder político ({w.Rule("law_change_cost", 30f):0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        var law = w.Laws[LawId];
        c.Political -= w.Rule("law_change_cost", 30f);
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

/// <summary>Exigir que o derrotado se torne estado-fantoche em vez de ser esquartejado.
///
/// É a paz que não se vê no mapa como uma cor a comer outra: o país fica de pé, com bandeira e terra, mas
/// passa a pagar-nos tributo e a mandar-nos homens. Custa mais pressão do que qualquer exigência de
/// regiões (regra puppet_price) porque não se pede um pedaço — pede-se o país inteiro.</summary>
public sealed record PuppetCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (w.SubjectTypeDefs.Count == 0) return "vassalagem indisponível";
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Countries.TryGetValue(TargetCountryId, out var t)) return "país inválido";
        if (CountryId == TargetCountryId) return "não podes ser fantoche de ti próprio";
        if (c.IsSubject) return "um vassalo não faz vassalos";
        if (t.IsSubject) return t.OverlordId == CountryId ? "já é teu fantoche" : "já é fantoche de outro";
        if (!w.AreAtWar(CountryId, TargetCountryId)) return "não estás em guerra com ele";
        return null;
    }

    public void Execute(World w)
    {
        if (Systems.PeaceTerms.PuppetVerdict(w, CountryId, TargetCountryId).Accepted)
            Systems.PeaceTerms.Puppet(w, CountryId, TargetCountryId);
        else
            w.Events.Publish(new PeaceOfferRejected(CountryId, TargetCountryId));
    }
}

/// <summary>Libertar um estado-fantoche por vontade própria — a decisão de quem prefere um aliado a um
/// devedor. Sem preço: quem larga, larga.</summary>
public sealed record ReleaseSubjectCommand(int CountryId, int SubjectCountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(SubjectCountryId, out var s)) return "país inválido";
        if (s.OverlordId != CountryId) return "não é teu fantoche";
        return null;
    }

    public void Execute(World w)
    {
        var s = w.Countries[SubjectCountryId];
        s.OverlordId = 0; s.Autonomy = 0f;
        w.Events.Publish(new SubjectFreed(SubjectCountryId, CountryId));
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

/// <summary>Manda divisões nossas como voluntárias para a guerra de outro país, sem entrarmos nela. Passam
/// a combater sob a bandeira dele e continuam a custar-nos homens e material (VolunteerSystem).</summary>
public sealed record SendVolunteersCommand(int CountryId, int HostId, int Count = 1) : ICommand
{
    public string? Validate(World w) => Count < 1 ? "nem meia divisão" : w.VolunteerBlock(CountryId, HostId);

    public void Execute(World w) => VolunteerSystem.Send(w, CountryId, HostId, Count);
}

/// <summary>Chama os voluntários de volta: voltam à capital e à nossa bandeira no próprio dia.</summary>
public sealed record RecallVolunteersCommand(int CountryId, int? HostId = null) : ICommand
{
    public string? Validate(World w) =>
        !w.Countries.ContainsKey(CountryId) ? "país inválido"
        : !w.Divisions.Values.Any(d => d.VolunteerFrom == CountryId && (HostId is null || d.CountryId == HostId))
            ? "não temos voluntários lá fora" : null;

    public void Execute(World w) => VolunteerSystem.RecallAll(w, CountryId, HostId);
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
        if (c.Political < w.Rule("nap_cost", 20f)) return $"falta poder político ({w.Rule("nap_cost", 20f):0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId]; var t = w.Countries[TargetCountryId];
        c.Political -= w.Rule("nap_cost", 20f);
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
        float free = TradeSystem.Free(w, SellerId, ResourceId);
        if (free < Units - 1e-3f)
            return seller.Stat("export_share", 1f) < 0.999f
                ? $"a lei de comércio dele só deixa vender {free:0.#} unidades"
                : "o vendedor não tem unidades livres";
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


/// <summary>Abre (ou revê) um empréstimo de material a outro país: Share do rendimento diário passa a sair
/// todos os dias do cofre de quem assina e a entrar no de quem recebe, com as perdas de caminho pelo meio
/// (LendLeaseSystem). Assinar duas vezes ao mesmo país não faz dois acordos — muda a fatia do que já lá
/// está, que é o que a barra da interface faz quando se arrasta.
///
/// Um país só pode prometer até lend_lease_max_share do que ganha, somando todos os empréstimos: emprestar
/// tudo era ficar sem exército por generosidade. Nada a inimigos — material entregue a quem nos combate é
/// material que volta apontado a nós.</summary>
public sealed record LendLeaseCommand(int CountryId, int ToId, float Share) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var from) || from.Capitulated) return "país inválido";
        if (!w.Countries.TryGetValue(ToId, out var to) || to.Capitulated) return "destinatário inválido";
        if (CountryId == ToId) return "não podes emprestar a ti próprio";
        if (w.AreAtWar(CountryId, ToId)) return "estão em guerra";
        float min = w.Rule("lend_lease_min_share", 0.05f), max = w.Rule("lend_lease_max_share", 0.35f);
        if (Share < min - 1e-4f) return $"a fatia mínima é de {min * 100f:0}% do rendimento";
        if (Share > max + 1e-4f) return $"a fatia máxima é de {max * 100f:0}% do rendimento";
        float mine = LendLeaseSystem.Between(w, CountryId, ToId)?.Share ?? 0f;   // rever não soma: substitui
        if (LendLeaseSystem.Given(w, CountryId) - mine + Share > max + 1e-4f)
            return $"já tens {LendLeaseSystem.Given(w, CountryId) * 100f:0}% do rendimento prometido";
        return null;
    }

    public void Execute(World w)
    {
        var l = LendLeaseSystem.Between(w, CountryId, ToId);
        if (l is null) w.LendLeases.Add(new LendLease { FromId = CountryId, ToId = ToId, Share = Share, SinceDay = w.Clock.Day });
        else l.Share = Share;
        w.Events.Publish(new LendLeaseSigned(CountryId, ToId, Share));
    }
}

/// <summary>Fecha um empréstimo de material em que o país participa (de qualquer um dos lados: quem dá
/// cansa-se, quem recebe também pode dispensar).</summary>
public sealed record CancelLendLeaseCommand(int CountryId, int OtherId) : ICommand
{
    public string? Validate(World w) => Find(w) is null ? "não há empréstimo com esse país" : null;

    public void Execute(World w)
    {
        var l = Find(w)!;
        w.LendLeases.Remove(l);
        w.Events.Publish(new LendLeaseEnded(l.FromId, l.ToId, l.SentTotal));
    }

    private LendLease? Find(World w) =>
        w.LendLeases.FirstOrDefault(l => (l.FromId == CountryId && l.ToId == OtherId)
                                      || (l.ToId == CountryId && l.FromId == OtherId));
}

/// <summary>Começa a obra de um edifício da tabela building numa região própria e controlada.
/// Ocupa uma fábrica civil (Industry) enquanto a obra durar.</summary>
public sealed record BuildBuildingCommand(int CountryId, int RegionId, string BuildingId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.Regions.TryGetValue(RegionId, out var r)) return "região inválida";
        if (!w.BuildingDefs.TryGetValue(BuildingId, out var def)) return "edifício desconhecido";
        // o depósito é a única obra que se levanta em terra tomada: é ele que leva a rede atrás da ofensiva
        if (r.ControllerId != CountryId || (r.OwnerId != CountryId && !def.IsHub)) return "a região não é tua";
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
        r.Project = BuildingId; r.ProjectProgress = 0f; r.ProjectOwner = CountryId;
    }
}

/// <summary>Comprar um esquadrão aéreo: +1 asa do modelo que a tabela marca como básico (plane_class.basic),
/// por air_wing_cost pontos. O poder aéreo relativo dos dois lados modula a força no combate terrestre
/// (CombatSystem, air_combat_weight). É o botão de sempre; escolher o modelo é o BuyPlaneCommand.</summary>
public sealed record BuyAirWingCommand(int CountryId) : ICommand
{
    public string? Validate(World w) => new BuyPlaneCommand(CountryId, Air.Basic(w)).Validate(w);

    public void Execute(World w) => new BuyPlaneCommand(CountryId, Air.Basic(w)).Execute(w);
}

/// <summary>Encomendar uma asa de um modelo (plane_class): custa air_wing_cost × o custo do modelo e entra
/// no campo. É o botão do hangar — o antigo "comprar esquadrão" é este comando com o modelo básico.</summary>
public sealed record BuyPlaneCommand(int CountryId, string ClassId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (ClassId.Length > 0 && !w.PlaneClasses.ContainsKey(ClassId)) return "modelo de avião desconhecido";
        float cost = Air.Cost(w, ClassId);
        if (c.Money < cost) return $"faltam pontos de produção ({cost:0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        Air.Buy(w, c, ClassId);
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
/// <summary>Larga uma divisão de pára-quedistas numa região a poucos saltos daqui (ParadropSystem): os
/// transportes levantam hoje e a tropa cai daqui a paradrop_days dias, se o chão continuar livre. Toda a
/// razão para não poder saltar vem do ParadropSystem.Block — a barra de selecção mostra a mesma frase.</summary>
public sealed record ParadropCommand(int CountryId, int DivisionId, int TargetRegionId) : ICommand
{
    public string? Validate(World w) => ParadropSystem.Block(w, CountryId, DivisionId, TargetRegionId);

    public void Execute(World w) => ParadropSystem.Launch(w, w.Divisions[DivisionId], TargetRegionId);
}

/// <summary>Mandar uma divisão atravessar a retaguarda de comboio (Redeploy): chega em redeploy_speed do
/// tempo de marcha, mas paga organização ao embarcar e quase não se recompõe pelo caminho. Toda a razão
/// para não poder embarcar vem do Redeploy.Block — a barra de selecção mostra a mesma frase.</summary>
public sealed record RedeployCommand(int CountryId, int DivisionId, int TargetRegionId) : ICommand
{
    public string? Validate(World w) => Redeploy.Block(w, CountryId, DivisionId, TargetRegionId);

    public void Execute(World w) => Redeploy.Launch(w, w.Divisions[DivisionId], TargetRegionId);
}

public sealed record BuyWarshipCommand(int CountryId) : ICommand
{
    public string? Validate(World w) => new BuyShipCommand(CountryId, Navy.Basic(w)).Validate(w);

    public void Execute(World w) => new BuyShipCommand(CountryId, Navy.Basic(w)).Execute(w);
}

/// <summary>Encomendar um casco de uma classe (ship_class): custa naval_ship_cost × o custo da classe e
/// entra no porto. É o botão do estaleiro — o antigo "comprar navio" é este comando com a classe que a
/// tabela marca como básica.</summary>
public sealed record BuyShipCommand(int CountryId, string ClassId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (ClassId.Length > 0 && !w.ShipClasses.ContainsKey(ClassId)) return "classe de navio desconhecida";
        float cost = Navy.Cost(w, ClassId);
        if (c.Money < cost) return $"faltam pontos de produção ({cost:0})";
        return null;
    }

    public void Execute(World w) => Navy.Buy(w, w.Countries[CountryId], ClassId);
}

/// <summary>Assinar a política de ocupação que se aplica ao povo de um país ocupado (occupation_policy).
/// Vale para toda a terra que lhe tomámos e só se pode trocar passados occupation_switch_days.</summary>
public sealed record SetOccupationPolicyCommand(int CountryId, int TargetId, string PolicyId) : ICommand
{
    public string? Validate(World w) => OccupationSystem.Block(w, CountryId, TargetId, PolicyId);

    public void Execute(World w) => OccupationSystem.Set(w, CountryId, TargetId, PolicyId);
}

/// <summary>Mandar construir um comboio mercante. Não vai para o mar nem se destaca: engrossa a marinha
/// mercante que carrega o abastecimento por mar e as importações, e que a guerra ao comércio vai afundando.</summary>
public sealed record BuyConvoyCommand(int CountryId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        float cost = w.Rule("convoy_cost", 25f);
        if (c.Money < cost) return $"faltam pontos de produção ({cost:0})";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        c.Money -= w.Rule("convoy_cost", 25f);
        c.Convoys += 1f;
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
        if (c.Political < def.Cost) return "poder político insuficiente";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId]; var def = w.DecisionDefs[DecisionId];
        c.Political -= def.Cost;
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
/// <summary>Atribui a frente de um grupo: o país inimigo, e opcionalmente um troço específico dela
/// (RegionId — uma região do inimigo, o Theatre.FacingId de um teatro do painel Exércitos). Sem RegionId o
/// grupo marcha para onde lhe ficar mais perto em toda a fronteira com esse país, como sempre fez; com ele,
/// dedica-se só àquele troço — é a diferença entre "defende a Ucrânia" e "defende o Norte da Ucrânia".</summary>
public sealed record SetArmyGroupFrontCommand(int CountryId, int GroupId, int? FrontCountryId, int? RegionId = null) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.ArmyGroups.TryGetValue(GroupId, out var g)) return "grupo inexistente";
        if (g.CountryId != CountryId) return "grupo não é teu";
        if (FrontCountryId is not int foe) return null;
        if (!w.Countries.ContainsKey(foe)) return "país inexistente";
        if (!w.AreAtWar(CountryId, foe)) return "só se atribui uma frente contra quem estás em guerra";
        if (RegionId is int rid && (!w.Regions.TryGetValue(rid, out var r) || r.ControllerId != foe))
            return "esse troço já não é do inimigo";
        return null;
    }

    public void Execute(World w)
    {
        var g = w.ArmyGroups[GroupId];
        g.FrontCountryId = FrontCountryId;
        g.FrontRegionId = FrontCountryId is null ? null : RegionId;
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
        if (!World.DoctrineIsFor(d, c)) return "essa escola é de outro país";
        if (c.Doctrines.Contains(DoctrineId)) return "já é doutrina do exército";
        if (w.DoctrineBlock(c, DoctrineId) is string block)
            return block.StartsWith('!')
                ? $"Escola fechada por {w.ArmyDoctrines[block[1..]].Name}"
                : $"Precisa de {(w.ArmyDoctrines.TryGetValue(block, out var need) ? need.Name : block)}";
        string domain = w.DomainOf(d);
        float have = World.Xp(c, domain);
        if (have < d.Cost) return $"faltam {d.Cost - have:0} de {World.XpName(domain)}";
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
        if (!World.GeneralIsFor(def, c)) return "esse comandante é de outro país";
        if (c.Generals.Contains(GeneralId)) return "já serve neste exército";
        if (w.GeneralsInService(c, def.Domain) >= w.GeneralSlots(def.Domain)) return "estado-maior completo";
        if (c.Money < def.Cost) return "pontos de produção insuficientes";
        // um comandante de asa ou de esquadra também se paga com a experiência da arma dele: é a mesma
        // moeda das escolas de guerra, e é por isso que ter os dois obriga a escolher
        float have = World.Xp(c, def.Domain);
        if (def.Xp > 0f && have < def.Xp) return $"faltam {def.Xp - have:0} de {World.XpName(def.Domain)}";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        var hired = w.GeneralDefs[GeneralId];
        c.Money -= hired.Cost;
        if (hired.Xp > 0f) World.SpendXp(c, hired.Domain, hired.Xp);
        c.Generals.Add(GeneralId);
        World.ApplyGenerals(w, c);
        w.Events.Publish(new GeneralHired(CountryId, GeneralId));
    }
}

/// <summary>Nomeia um conselheiro civil para a pasta dele. Sentar alguém numa cadeira ocupada é demitir o
/// que lá está — e a nomeação nova paga-se por inteiro, como no HoI4.</summary>
public sealed record AppointAdvisorCommand(int CountryId, string AdvisorId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Countries.TryGetValue(CountryId, out var c) || c.Capitulated) return "país inválido";
        if (!w.AdvisorDefs.TryGetValue(AdvisorId, out var def)) return "conselheiro desconhecido";
        if (def.CountryTag is string tag && tag != c.Tag) return "não serve este país";
        if (c.Cabinet.GetValueOrDefault(def.Slot) == AdvisorId) return "já está no gabinete";
        if (c.Political < def.Cost) return $"faltam {def.Cost - c.Political:0} de poder político";
        return null;
    }

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        var def = w.AdvisorDefs[AdvisorId];
        c.Political -= def.Cost;
        c.Cabinet[def.Slot] = AdvisorId;
        c.CabinetSince[def.Slot] = w.Clock.Day;
        World.ApplyCabinet(w, c);
        w.Events.Publish(new AdvisorAppointed(CountryId, AdvisorId, def.Slot));
    }
}

/// <summary>Demite quem está numa pasta: o multiplicador cai no dia e a nomeação não se devolve.</summary>
public sealed record DismissAdvisorCommand(int CountryId, string Slot) : ICommand
{
    public string? Validate(World w) =>
        !w.Countries.TryGetValue(CountryId, out var c) ? "país inválido"
        : !c.Cabinet.ContainsKey(Slot) ? "pasta vazia" : null;

    public void Execute(World w)
    {
        var c = w.Countries[CountryId];
        string id = c.Cabinet[Slot];
        c.Cabinet.Remove(Slot); c.CabinetSince.Remove(Slot);
        World.ApplyCabinet(w, c);
        w.Events.Publish(new AdvisorLeft(CountryId, id, Slot, false));
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

/// <summary>Marca uma operação anfíbia: escolhe a praia inimiga e embarca a tropa que está no cais. A partir
/// daqui a tropa não marcha nem aceita ordens de marcha — fica a preparar-se, e larga sozinha no dia em que
/// a preparação, os mercantes e o mar deixarem (NavalInvasionSystem). Chamar outra vez com divisões novas
/// engrossa a operação que já lá está (e atrasa-a, que juntar gente demora).</summary>
public sealed record PlanNavalInvasionCommand(int CountryId, int TargetRegionId, List<int> DivisionIds) : ICommand
{
    public string? Validate(World w) => NavalInvasionSystem.Block(w, CountryId, TargetRegionId, DivisionIds);

    public void Execute(World w) => NavalInvasionSystem.Plan(w, CountryId, TargetRegionId, DivisionIds);
}

/// <summary>Desmarca a operação: a tropa fica onde está, livre para marchar, e a preparação perde-se toda.</summary>
public sealed record CancelNavalInvasionCommand(int CountryId, int TargetRegionId) : ICommand
{
    public string? Validate(World w) =>
        w.NavalInvasions.Any(i => i.CountryId == CountryId && i.TargetId == TargetRegionId)
            ? null : "não há operação marcada sobre essa praia";

    public void Execute(World w)
    {
        var inv = w.NavalInvasions.First(i => i.CountryId == CountryId && i.TargetId == TargetRegionId);
        w.NavalInvasions.Remove(inv);
        w.Events.Publish(new NavalInvasionCancelled(CountryId, TargetRegionId, "desmarcada pelo comando"));
    }
}
