using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Systems;
using WarGame.Core.Stats;

namespace WarGame.Core.Model;

/// <summary>Raiz do estado da simulação. Sem Godot. Tick executa cada ISystem na ordem registada.</summary>
public sealed class World
{
    public Clock Clock { get; }
    public Dictionary<int, Region> Regions { get; } = new();
    public Dictionary<int, Country> Countries { get; } = new();
    public Dictionary<int, Division> Divisions { get; } = new();
    public List<Battle> ActiveBattles { get; } = new();
    /// <summary>Grupos de exércitos por id (ArmyGroupSystem). Poucos por país — rule army_group_max.</summary>
    public Dictionary<int, ArmyGroup> ArmyGroups { get; } = new();
    public EventBus Events { get; } = new();
    public DivisionStatCache Stats { get; }
    public ModifierEngine Modifiers { get; }
    public Random Rng { get; }
    /// <summary>Constantes de jogo da tabela `rule` (+ `move_cost:&lt;terreno&gt;` da tabela terrain). Nada em código.</summary>
    public Dictionary<string, float> Rules { get; } = new();
    public IUnitRepository Units => Stats.Units;
    /// <summary>Árvore tecnológica (tabela tech) e efeitos de país por tecnologia (tech_effect).</summary>
    public Dictionary<string, Tech> Techs { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> TechEffects { get; } = new();
    /// <summary>Focos nacionais (tabela focus) e efeitos (focus_effect), por id.</summary>
    public Dictionary<string, Focus> Focuses { get; } = new();
    /// <summary>Pré-requisitos além do `requires` do próprio foco (tabela focus_link): a árvore converge.</summary>
    public Dictionary<string, List<string>> FocusLinks { get; } = new();
    /// <summary>Focos que se excluem uns aos outros (tabela focus_rival, sempre nos dois sentidos): escolher
    /// um ramo fecha o outro para sempre.</summary>
    public Dictionary<string, List<string>> FocusRivals { get; } = new();
    /// <summary>Eventos noticiosos (news_event) e efeitos (news_event_effect), por id.</summary>
    public Dictionary<string, NewsEvent> NewsEvents { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> NewsEffects { get; } = new();
    /// <summary>Escolhas dos eventos (news_event_option, ordenadas por sort) e efeitos por opção.</summary>
    public Dictionary<string, List<NewsOption>> NewsOptions { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> NewsOptionEffects { get; } = new();
    /// <summary>Leis nacionais (tabelas law + law_effect); ActiveLaw resolve o default por grupo.</summary>
    /// <summary>Tipos de recurso (tabela resource); depósitos vivem em Region.Resources.</summary>
    public Dictionary<string, ResourceDef> ResourceDefs { get; } = new();
    /// <summary>Edifícios construíveis (tabela building).</summary>
    public Dictionary<string, BuildingDef> BuildingDefs { get; } = new();
    /// <summary>Modos de mapa (tabela map_mode): o mapa político e as pinturas por conta (MapModes).</summary>
    public Dictionary<string, MapModeDef> MapModeDefs { get; } = new();
    public Dictionary<string, MedalDef> MedalDefs { get; } = new();
    /// <summary>Honras de batalha (tabela division_honour), o nome próprio que uma divisão ganha em campanha.</summary>
    public Dictionary<string, HonourDef> HonourDefs { get; } = new();
    /// <summary>Estações do ano (tabela season) e o mês a que cada uma manda (tabela season_month).</summary>
    /// <summary>Géneros de acontecimento da crónica (tabela chronicle_kind).</summary>
    public Dictionary<string, ChronicleKind> ChronicleKinds { get; } = new();
    /// <summary>Crónica da campanha, do mais antigo para o mais recente (ChronicleSystem; save s_chronicle).</summary>
    public List<ChronicleEntry> Chronicle { get; } = new();
    public Dictionary<string, SeasonDef> SeasonDefs { get; } = new();
    public Dictionary<int, string> SeasonMonths { get; } = new();
    /// <summary>Quanto cada estação castiga cada terreno (tabela season_terrain): [estação][terreno] = factor
    /// do desgaste. Sem linha, o terreno vale 1 — a estação castiga-o como a média.</summary>
    public Dictionary<string, Dictionary<string, float>> SeasonTerrain { get; } = new();

    /// <summary>Factor do desgaste da estação neste terreno (1 = o desgaste raso da estação).</summary>
    public float SeasonBite(string seasonId, string terrain) =>
        SeasonTerrain.TryGetValue(seasonId, out var byTerrain) && byTerrain.TryGetValue(terrain, out var f) ? f : 1f;
    /// <summary>Decisões nacionais (tabela decision) e as activas.</summary>
    public Dictionary<string, DecisionDef> DecisionDefs { get; } = new();
    /// <summary>Comandantes contratáveis (tabela general).</summary>
    public Dictionary<string, GeneralDef> GeneralDefs { get; } = new();
    /// <summary>Postos de comandante (tabela general_rank), do mais baixo para o mais alto.</summary>
    public List<GeneralRank> GeneralRanks { get; } = new();
    /// <summary>Gravidades de baixa no comando (tabela wound_kind).</summary>
    public Dictionary<string, WoundKind> WoundKinds { get; } = new();
    /// <summary>Patamares de potência mundial (tabela power_tier).</summary>
    public List<PowerTier> PowerTiers { get; } = new();
    public List<ActiveDecision> ActiveDecisions { get; } = new();
    public Dictionary<string, Law> Laws { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> LawEffects { get; } = new();

    /// <summary>Escolas de doutrina de exército (tabela army_doctrine_branch), pela ordem em que se mostram.</summary>
    public Dictionary<string, DoctrineBranch> DoctrineBranches { get; } = new();
    /// <summary>Doutrinas de exército (tabela army_doctrine) e os multiplicadores de cada uma.</summary>
    public Dictionary<string, ArmyDoctrine> ArmyDoctrines { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> DoctrineEffects { get; } = new();

    /// <summary>O ramo em que este país se formou (a primeira doutrina que adoptou manda), ou null.</summary>
    public string? DoctrineBranchOf(Country c)
    {
        foreach (var id in c.Doctrines.OrderBy(x => x))
            if (ArmyDoctrines.TryGetValue(id, out var d)) return d.Branch;
        return null;
    }

    /// <summary>O que impede este país de adoptar esta doutrina, ou null se está à mão. Devolve o id da
    /// doutrina que falta, ou "!"+id da doutrina que fechou a escola — a mesma convenção do FocusBlock.
    /// A experiência não entra aqui: quem a conta é o AdoptDoctrineCommand, para a UI poder mostrar uma
    /// doutrina aberta mas ainda por pagar.</summary>
    public string? DoctrineBlock(Country c, string doctrineId)
    {
        if (!ArmyDoctrines.TryGetValue(doctrineId, out var d) || c.Doctrines.Contains(doctrineId)) return doctrineId;
        if (d.Requires is string req && !c.Doctrines.Contains(req)) return req;
        foreach (var id in c.Doctrines.OrderBy(x => x))
            if (ArmyDoctrines.TryGetValue(id, out var have) && have.Branch != d.Branch) return "!" + id;
        return null;
    }

    /// <summary>Doutrina à mão e já paga: o que a IA adopta e o que o botão do painel aceita.</summary>
    public bool CanAdopt(Country c, string doctrineId) =>
        DoctrineBlock(c, doctrineId) is null && ArmyDoctrines.TryGetValue(doctrineId, out var d) && c.ArmyXp >= d.Cost;

    public Dictionary<string, SpyOp> SpyOps { get; } = new();
    public List<ActiveSpyOp> ActiveSpyOps { get; } = new();
    /// <summary>Propostas à espera de resposta do jogador (OfferSystem). Uma por par e assunto.</summary>
    public List<PendingOffer> Offers { get; } = new();
    /// <summary>Acordos de comércio de recursos em vigor (TradeSystem).</summary>
    public List<TradeDeal> TradeDeals { get; } = new();
    /// <summary>Amostras dos gráficos (HistorySystem): jogador + maiores potências, de history_sample_days em history_sample_days.</summary>
    public List<HistorySample> History { get; } = new();
    /// <summary>Rede de informação activa: (autor, alvo) → último dia com visibilidade (efeito intel).</summary>
    public Dictionary<(int A, int B), int> Intel { get; } = new();
    public bool HasIntel(int a, int b) => Intel.TryGetValue((a, b), out var until) && until >= Clock.Day;

    /// <summary>Pactos de não-agressão: (a,b) com a&lt;b → último dia em vigor. Bloqueia DeclareWar.</summary>
    public Dictionary<(int A, int B), int> Pacts { get; } = new();
    public bool HasPact(int a, int b) => Pacts.TryGetValue(WarKey(a, b), out var until) && until >= Clock.Day;
    /// <summary>Adidos militares destacados: quem manda → missão. Um por país (AttacheSystem).</summary>
    public Dictionary<int, Attache> Attaches { get; } = new();

    /// <summary>País metido em alguma guerra a sério (é a guerra dele que ensina o adido).</summary>
    public bool AtWar(int countryId) =>
        Countries.TryGetValue(countryId, out var c) && !c.Capitulated && c.AtWarWith.Count > 0;

    /// <summary>Porque é que este país não pode receber um adido nosso (null = pode).</summary>
    public string? AttacheBlock(int countryId, int hostId)
    {
        if (!Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "país inválido";
        if (!Countries.TryGetValue(hostId, out var h) || h.Capitulated) return "anfitrião inválido";
        if (countryId == hostId) return "a nossa guerra já a vemos de dentro";
        if (AreAtWar(countryId, hostId)) return $"estamos em guerra com {h.Name}";
        if (!AtWar(hostId)) return $"{h.Name} não está em guerra";
        if (Attaches.TryGetValue(countryId, out var have))
            return $"o adido já está com {(Countries.TryGetValue(have.HostId, out var oh) ? oh.Name : have.HostId.ToString())}";
        return null;
    }

    /// <summary>Escolha feita por evento (s_news_choice no save): event_id → option_id.</summary>
    public Dictionary<string, string> NewsChoices { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> FocusEffects { get; } = new();
    /// <summary>Alianças defensivas (tabelas faction + faction_member). Ver FactionsOf/SameFaction/Allies.</summary>
    public Dictionary<string, Faction> Factions { get; } = new();

    private readonly List<ISystem> _systems = new();
    private int _nextDivisionId;
    public IReadOnlyList<ISystem> Systems => _systems;

    public World(DateOnly start, DivisionStatCache stats, ModifierEngine modifiers, int seed = 0)
    {
        Clock = new Clock(start); Stats = stats; Modifiers = modifiers; Rng = new Random(seed);
    }

    public void Register(ISystem s) => _systems.Add(s);

    /// <summary>Um dia. Chamar fora da main thread; a UI lê depois.</summary>
    public void Tick()
    {
        foreach (var s in _systems) s.Tick(this);
        Clock.Advance();
        Events.Publish(new DayPassed(Clock.Day));
    }

    public float Rule(string key, float fallback = 0f) => Rules.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>Current difficulty setting</summary>
    /// <summary>Dificuldade escolhida (tabela difficulty; null = por escolher, vale o normal).</summary>
    public string? Difficulty { get; set; }
    /// <summary>Dificuldades disponíveis e as regras que cada uma reescreve (tabelas difficulty/difficulty_effect).</summary>
    public Dictionary<string, DifficultyDef> DifficultyDefs { get; } = new();

    /// <summary>Aplica uma dificuldade: repõe as regras de origem e reescreve as que ela mexe. Chamar
    /// depois de LoadStatic e sempre que o jogador troca de nível.</summary>
    public void ApplyDifficulty(string? id)
    {
        foreach (var (key, value) in _baseRules)
        {
            if (value is float v) Rules[key] = v; else Rules.Remove(key);
        }
        Difficulty = id;
        if (id is null || !DifficultyDefs.TryGetValue(id, out var def)) return;
        foreach (var (key, value) in def.Effects)
        {
            if (!_baseRules.ContainsKey(key)) _baseRules[key] = Rules.TryGetValue(key, out var had) ? had : null;
            Rules[key] = value;
        }
    }

    /// <summary>Valor de origem das regras que alguma dificuldade já reescreveu (null = a regra nem existia),
    /// para se poder voltar atrás ao trocar de nível.</summary>
    private readonly Dictionary<string, float?> _baseRules = new();

    /// <summary>Recalcula Country.GeneralMult a partir dos comandantes contratados (após contratar,
    /// dispensar ou carregar um jogo).</summary>
    public static void ApplyGenerals(World w, Country c)
    {
        c.GeneralMult.Clear();
        var detached = w.ArmyGroups.Values.Where(x => x.CountryId == c.Id && x.GeneralId is not null)
                                          .Select(x => x.GeneralId!).ToHashSet();
        foreach (var id in c.Generals)
            if (!detached.Contains(id) && !w.IsWounded(c.Id, id) && w.GeneralDefs.TryGetValue(id, out var g))   // destacado manda no grupo, ferido não manda em nada
                c.GeneralMult[g.StatKey] = c.GeneralMult.GetValueOrDefault(g.StatKey, 1f) * g.Mult;
    }

    /// <summary>Recalcula Country.TechMult a partir das tecnologias concluídas (chamar após LoadSave e ao concluir uma).</summary>
    public void ApplyTechs(Country c)
    {
        c.TechMult.Clear();
        foreach (var t in c.Techs)
            if (TechEffects.TryGetValue(t, out var effs))
                foreach (var (key, mul) in effs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var f in c.FocusesDone)
            if (FocusEffects.TryGetValue(f, out var effs))
                foreach (var (key, mul) in effs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var d in c.Doctrines)          // escolas de guerra: entram no mesmo bolo das tecnologias
            if (DoctrineEffects.TryGetValue(d, out var deffs))
                foreach (var (key, mul) in deffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var grp in Laws.Values.Select(l => l.Group).Distinct())
            if (ActiveLaw(c, grp) is Law law && LawEffects.TryGetValue(law.Id, out var leffs))
                foreach (var (key, mul) in leffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var e in NewsEvents.Values)   // eventos noticiosos já disparados (NewsSystem)
        {
            if (e.Day > Clock.Day || (e.CountryId is not null && e.CountryId != c.Id)) continue;
            if (NewsEffects.TryGetValue(e.Id, out var neffs))
                foreach (var (key, mul) in neffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
            // Efeitos da opção escolhida (eventos com escolhas); sem escolha ainda → sem efeito.
            if (NewsChoices.TryGetValue(e.Id, out var opt) && NewsOptionEffects.TryGetValue(opt, out var oeffs))
                foreach (var (key, mul) in oeffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        }
    }

    /// <summary>Pode escolher o foco: é do país, não o tem, tem os anteriores todos (o `requires` e os de
    /// focus_link) e não fechou a porta ao escolher um ramo rival.</summary>
    public bool CanFocus(Country c, string focusId) => FocusBlock(c, focusId) is null;

    /// <summary>Porque é que o foco não está disponível — id do que falta, "!" + id do rival que o fechou,
    /// ou null quando se pode escolher. A UI traduz isto para nomes.</summary>
    public string? FocusBlock(Country c, string focusId)
    {
        if (!Focuses.TryGetValue(focusId, out var f) || f.CountryId != c.Id || c.FocusesDone.Contains(focusId))
            return focusId;
        if (f.Requires is string req && !c.FocusesDone.Contains(req)) return req;
        if (FocusLinks.TryGetValue(focusId, out var extra))
            foreach (var need in extra) if (!c.FocusesDone.Contains(need)) return need;
        if (FocusRivals.TryGetValue(focusId, out var rivals))
            foreach (var other in rivals) if (c.FocusesDone.Contains(other)) return "!" + other;
        return null;
    }

    /// <summary>Pode investigar: existe, não a tem, tem a anterior.</summary>
    public bool CanResearch(Country c, string techId) =>
        Techs.TryGetValue(techId, out var t) && !c.Techs.Contains(techId) && (t.Requires is null || c.Techs.Contains(t.Requires));
    public float MoveCost(string terrain) => Rule("move_cost:" + terrain, 1f);

    /// <summary>Estação em que o calendário anda, ou null se a tabela não estiver carregada (então nada muda).</summary>
    public SeasonDef? Season => SeasonMonths.TryGetValue(Clock.Date.Month, out var id) && SeasonDefs.TryGetValue(id, out var s) ? s : null;

    /// <summary>Quanto a estação atrasa a marcha (1 = nada). Sem estação carregada vale 1.</summary>
    public float SeasonMove => MathF.Max(0.1f, Season?.MoveMult ?? 1f);

    /// <summary>Quanto a estação estraga a recomposição de organização (1 = nada).</summary>
    public float SeasonOrg => MathF.Max(0.1f, Season?.OrgMult ?? 1f);

    public bool AreAtWar(int a, int b) => a != b && Countries.TryGetValue(a, out var c) && c.AtWarWith.Contains(b);

    /// <summary>Templates desenhados em jogo (CreateTemplateCommand); ids a partir de CustomTemplateBase,
    /// persistidos no save em template/template_unit (os da static.db têm ids pequenos e nunca se escrevem).</summary>
    public List<int> CustomTemplateIds { get; } = new();
    public const int CustomTemplateBase = 1_000_000;

    /// <summary>Estado por guerra (chave normalizada min,max): quando começou e o último dia com progresso
    /// (captura de região entre os dois). O TruceSystem fecha guerras estagnadas com paz branca.</summary>
    public Dictionary<(int A, int B), WarInfo> Wars { get; } = new();
    public static (int A, int B) WarKey(int a, int b) => (Math.Min(a, b), Math.Max(a, b));

    /// <summary>Guerras já terminadas, da mais recente para trás (WarStatsSystem escreve, corta em war_history_max).
    /// É o material do ecrã de resumo: quem tomou o quê a quem e quanto custou.</summary>
    public List<WarRecord> WarHistory { get; } = new();

    /// <summary>Começa (ou regista) uma guerra: AtWarWith dos dois + entrada em Wars.</summary>
    public void StartWar(int a, int b, int? sinceDay = null)
    {
        Countries[a].AtWarWith.Add(b); Countries[b].AtWarWith.Add(a);
        var key = WarKey(a, b);
        if (!Wars.ContainsKey(key)) Wars[key] = new WarInfo { A = key.A, B = key.B, StartDay = sinceDay ?? Clock.Day, LastProgressDay = sinceDay ?? Clock.Day };
    }

    /// <summary>Fim de guerra entre dois: AtWarWith + Wars. Não publica eventos (o chamador decide).</summary>
    public void EndWar(int a, int b)
    {
        if (Countries.TryGetValue(a, out var ca)) ca.AtWarWith.Remove(b);
        if (Countries.TryGetValue(b, out var cb)) cb.AtWarWith.Remove(a);
        Wars.Remove(WarKey(a, b));
    }

    /// <summary>Captura de região entre beligerantes: renova o relógio da paz branca dessa guerra.</summary>
    public void NoteWarProgress(int a, int b)
    { if (Wars.TryGetValue(WarKey(a, b), out var info)) info.LastProgressDay = Clock.Day; }
    /// <summary>Região controlada por alguém com quem `countryId` está em guerra.</summary>
    public bool IsHostile(int countryId, Region r) => AreAtWar(countryId, r.ControllerId);
    /// <summary>Salto por mar: as duas regiões só se ligam por sea_link, não por terra — quem o faz
    /// desembarca (custo de organização) e, se a costa for inimiga, assalta a praia em desvantagem.</summary>
    public bool IsSeaHop(int fromId, int toId) =>
        Regions.TryGetValue(fromId, out var f) && f.SeaNeighbours.ContainsKey(toId) && !f.Neighbours.Contains(toId);

    /// <summary>Facções de que `countryId` é membro (0, 1 ou várias).</summary>
    public IEnumerable<Faction> FactionsOf(int countryId) => Factions.Values.Where(f => f.Members.Contains(countryId));
    /// <summary>Há alguma facção com ambos como membros (HoI4: aliados na mesma aliança nunca se declaram guerra).</summary>
    public bool SameFaction(int a, int b) => a != b && Factions.Values.Any(f => f.Members.Contains(a) && f.Members.Contains(b));
    /// <summary>Acesso militar: região própria ou de um aliado de facção (HoI4: aliados partilham território).</summary>
    public bool CanTraverse(int countryId, Region r) => r.ControllerId == countryId || SameFaction(countryId, r.ControllerId);
    /// <summary>Todos os membros de todas as facções de `countryId`, sem ele próprio (dissuasão: força que conta contra atacá-lo).</summary>
    public HashSet<int> Allies(int countryId)
    {
        var result = new HashSet<int>();
        foreach (var f in FactionsOf(countryId))
            foreach (var m in f.Members)
                if (m != countryId) result.Add(m);
        return result;
    }

    /// <summary>Facções fundadas em jogo (CreateFactionCommand); persistem em s_faction. As da static.db não entram.</summary>
    public List<string> CustomFactionIds { get; } = new();
    public Faction CreateFaction(string id, string name, string description)
    {
        var f = new Faction(id, name, description, new List<int>());
        Factions[id] = f; CustomFactionIds.Add(id);
        return f;
    }
    public string NewFactionId()
    {
        int n = 0;
        foreach (var id in CustomFactionIds)
            if (id.StartsWith("fx_") && int.TryParse(id.AsSpan(3), out var k) && k > n) n = k;
        return "fx_" + (n + 1);
    }
    /// <summary>A IA (e a regra de adesão do jogador) aceita entrar numa facção só com inimigo comum:
    /// o candidato está em guerra com alguém com quem um membro também está. Nunca em guerra com membros.</summary>
    public bool FactionWouldAccept(int countryId, Faction f)
    {
        if (!Countries.TryGetValue(countryId, out var c) || c.Capitulated || f.Members.Contains(countryId)) return false;
        foreach (var m in f.Members) if (AreAtWar(countryId, m)) return false;
        foreach (var e in c.AtWarWith)
            foreach (var m in f.Members)
                if (AreAtWar(m, e)) return true;
        return false;
    }

    /// <summary>Lei activa de um grupo: a escolhida em Country.Laws, senão a is_default do grupo.</summary>
    public Law? ActiveLaw(Country c, string group) =>
        c.Laws.TryGetValue(group, out var id) && Laws.TryGetValue(id, out var chosen) && chosen.Group == group
            ? chosen
            : Laws.Values.FirstOrDefault(l => l.Group == group && l.IsDefault);

    public float TemplateCost(int templateId) =>
        Units.GetTemplate(templateId).Units.Sum(u => Units.GetUnitType(u.UnitTypeId).Cost * u.Qty);

    private int _nextGroupId;

    public int NewArmyGroupId()
    {
        if (_nextGroupId == 0) _nextGroupId = ArmyGroups.Count == 0 ? 1 : ArmyGroups.Keys.Max() + 1;
        return _nextGroupId++;
    }

    /// <summary>Grupo a que a divisão pertence, ou null — pelo ponteiro que a própria divisão guarda.</summary>
    public ArmyGroup? GroupOf(int divisionId) =>
        Divisions.TryGetValue(divisionId, out var d) && d.GroupId is int gid && ArmyGroups.TryGetValue(gid, out var g) ? g : null;

    /// <summary>Mete a divisão neste grupo (tirando-a do anterior). Único sítio que escreve a filiação:
    /// ArmyGroup.Divisions e Division.GroupId têm de andar sempre a par.</summary>
    public void JoinGroup(ArmyGroup g, int divisionId)
    {
        LeaveGroup(divisionId);
        g.Divisions.Add(divisionId);
        if (Divisions.TryGetValue(divisionId, out var d)) d.GroupId = g.Id;
    }

    /// <summary>Tira a divisão do grupo onde estiver (se estiver).</summary>
    public void LeaveGroup(int divisionId)
    {
        if (Divisions.TryGetValue(divisionId, out var d))
        {
            if (d.GroupId is int gid && ArmyGroups.TryGetValue(gid, out var old)) old.Divisions.Remove(divisionId);
            d.GroupId = null;
        }
        foreach (var g in ArmyGroups.Values) g.Divisions.Remove(divisionId);   // divisão já morta: limpa os restos
    }

    /// <summary>Multiplicador do comandante destacado para o grupo desta divisão (1 = sem general).
    ///
    /// Um general destacado deixa de contar para o país inteiro (ApplyGenerals salta-o) e o que dava a
    /// todos passa a valer só aqui, amplificado por general_command_bonus: é a troca que o jogador faz ao
    /// pôr o Muralha à frente de um exército em vez de o deixar no estado-maior.</summary>
    public float CommandMult(Division d, string key)
    {
        if (ArmyGroups.Count == 0 || d.GroupId is not int gid) return 1f;
        if (!ArmyGroups.TryGetValue(gid, out var g) || g.GeneralId is not string gen) return 1f;
        if (IsWounded(g.CountryId, gen)) return 1f;                                     // o homem está no hospital
        if (!GeneralDefs.TryGetValue(gen, out var def) || def.StatKey != key) return 1f;
        return 1f + (def.Mult - 1f) * (Rule("general_command_bonus", 2f) + RankBonus(g.CountryId, gen));
    }

    /// <summary>Posto actual de um comandante contratado: o mais alto cuja experiência ele já passou.
    /// Sem tabela de postos (ou sem experiência nenhuma) devolve null — o comando vale o de sempre.</summary>
    public GeneralRank? RankOf(int countryId, string generalId)
    {
        if (GeneralRanks.Count == 0 || !Countries.TryGetValue(countryId, out var c)) return null;
        float xp = c.GeneralXp.GetValueOrDefault(generalId);
        GeneralRank? best = null;
        foreach (var r in GeneralRanks)
            if (xp >= r.Xp && (best is null || r.Xp > best.Xp)) best = r;
        return best;
    }

    /// <summary>Posto seguinte, para a UI mostrar quanto falta para a promoção (null = já é o topo).</summary>
    public GeneralRank? NextRank(int countryId, string generalId)
    {
        float xp = Countries.TryGetValue(countryId, out var c) ? c.GeneralXp.GetValueOrDefault(generalId) : 0f;
        GeneralRank? next = null;
        foreach (var r in GeneralRanks)
            if (r.Xp > xp && (next is null || r.Xp < next.Xp)) next = r;
        return next;
    }

    /// <summary>Quanto o posto acrescenta ao multiplicador de destacamento deste comandante.</summary>
    public float RankBonus(int countryId, string generalId) => RankOf(countryId, generalId)?.Bonus ?? 0f;

    /// <summary>Está fora de serviço por ferimento? Vale para o país e para o exército: um comandante no
    /// hospital não soma stats nem amplifica nada (ver ApplyGenerals e CommandMult).</summary>
    public bool IsWounded(int countryId, string generalId) =>
        Countries.TryGetValue(countryId, out var c) && c.GeneralWound.TryGetValue(generalId, out var until) && until > Clock.Day;

    /// <summary>Dias que faltam até o comandante voltar ao serviço (0 = está de pé).</summary>
    public int WoundDaysLeft(int countryId, string generalId) =>
        Countries.TryGetValue(countryId, out var c) && c.GeneralWound.TryGetValue(generalId, out var until)
            ? Math.Max(0, until - Clock.Day) : 0;

    public int NewDivisionId()
    {
        if (_nextDivisionId == 0) _nextDivisionId = Divisions.Count == 0 ? 1 : Divisions.Keys.Max() + 1;
        return _nextDivisionId++;
    }

    // ---- contabilidade de divisões (sem regras; só mantém Regions[].DivisionIds e batalhas coerentes)
    public Division AddDivision(Division d)
    {
        Divisions[d.Id] = d; Regions[d.RegionId].DivisionIds.Add(d.Id);
        if (d.Id >= _nextDivisionId) _nextDivisionId = d.Id + 1;
        return d;
    }

    public void RemoveDivision(int id)
    {
        if (!Divisions.Remove(id, out var d)) return;
        Regions[d.RegionId].DivisionIds.Remove(id);
        foreach (var b in ActiveBattles) { b.Attackers.Remove(id); b.Defenders.Remove(id); }
        LeaveGroup(id);
    }

    /// <summary>Muda a divisão de região (sem custo nem regras — MovementSystem decide quando).</summary>
    public void PlaceDivision(Division d, int regionId)
    {
        Regions[d.RegionId].DivisionIds.Remove(d.Id);
        d.RegionId = regionId;
        Regions[regionId].DivisionIds.Add(d.Id);
    }

    public Battle? BattleAt(int regionId, int attackerCountryId) =>
        ActiveBattles.FirstOrDefault(b => b.RegionId == regionId && b.AttackerCountryId == attackerCountryId);

    public bool InBattle(int divisionId) =>
        ActiveBattles.Any(b => b.Attackers.Contains(divisionId) || b.Defenders.Contains(divisionId));
}

public sealed class Battle
{
    public int RegionId { get; init; }
    public int AttackerCountryId { get; init; }
    public List<int> Attackers { get; } = new();
    public List<int> Defenders { get; } = new();
    public int Days { get; set; }
}
