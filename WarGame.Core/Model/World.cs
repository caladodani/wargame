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
    /// <summary>Ramos da árvore de investigação (tabela tech_branch): o nome e a chapa de cada coluna.</summary>
    public Dictionary<string, TechBranchDef> TechBranches { get; } = new();
    /// <summary>O que cada número da ficha de combate quer dizer (unit_stat_def), pela chave do unit_stat.</summary>
    public Dictionary<string, UnitStatDef> UnitStatDefs { get; } = new();
    /// <summary>O chão (tabela terrain): o nome que se lê, o preço da marcha e a chapa que o mostra.</summary>
    public Dictionary<string, TerrainDef> TerrainDefs { get; } = new();
    /// <summary>Zonas estratégicas (tabela zone): as de terra são o céu que as asas disputam, as de mar são
    /// o oceano que as esquadras fecham. Ver Zones.</summary>
    public Dictionary<string, ZoneDef> Zones { get; } = new();
    /// <summary>As cidades do mundo, da maior para a menor (tabela city). Só o mapa as usa.</summary>
    public List<CityDef> Cities { get; } = new();
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

    /// <summary>Céus possíveis (tabela weather), pela ordem da tabela: o que o Weather sorteia por região e semana.</summary>
    public Dictionary<string, WeatherDef> WeatherDefs { get; } = new();

    /// <summary>Tácticas de combate (tabela tactic), pela ordem da tabela: o que cada lado pode escolher
    /// numa batalha e o que uma lê da outra (Tactics).</summary>
    public Dictionary<string, TacticDef> TacticDefs { get; } = new();

    /// <summary>Degraus de vassalagem (tabela subject_type), do mais preso ao mais solto (Subjects).</summary>
    public Dictionary<string, SubjectTypeDef> SubjectTypeDefs { get; } = new();

    /// <summary>Graus de ponto de vitória (tabela victory_tier): o que cada região vale na conta da guerra
    /// (VictoryPoints). Sem a tabela carregada não há pontos nenhuns e a paz mede-se como antes.</summary>
    public Dictionary<string, VictoryTierDef> VictoryTiers { get; } = new();

    /// <summary>Graus de veterania (tabela veterancy): o nome, os galões e a força extra que o XP de uma
    /// divisão vale (Veterancy). Sem a tabela carregada o bónus volta à recta de veterancy_bonus.</summary>
    public Dictionary<string, VeterancyDef> VeterancyTiers { get; } = new();

    /// <summary>Factor do desgaste da estação neste terreno (1 = o desgaste raso da estação).</summary>
    public float SeasonBite(string seasonId, string terrain) =>
        SeasonTerrain.TryGetValue(seasonId, out var byTerrain) && byTerrain.TryGetValue(terrain, out var f) ? f : 1f;
    /// <summary>Decisões nacionais (tabela decision) e as activas.</summary>
    public Dictionary<string, DecisionDef> DecisionDefs { get; } = new();
    /// <summary>Comandantes contratáveis (tabela general).</summary>
    public Dictionary<string, GeneralDef> GeneralDefs { get; } = new();
    /// <summary>Pastas do gabinete civil (tabela cabinet_slot), pela ordem em que se mostram.</summary>
    public List<CabinetSlotDef> CabinetSlots { get; } = new();
    /// <summary>Conselheiros civis contratáveis (tabelas advisor/advisor_effect).</summary>
    public Dictionary<string, AdvisorDef> AdvisorDefs { get; } = new();
    /// <summary>Postos de comandante (tabela general_rank), do mais baixo para o mais alto.</summary>
    public List<GeneralRank> GeneralRanks { get; } = new();
    /// <summary>Gravidades de baixa no comando (tabela wound_kind).</summary>
    public Dictionary<string, WoundKind> WoundKinds { get; } = new();
    /// <summary>Fundo de nomes de asas e esquadras (tabela formation_name).</summary>
    public List<FormationName> FormationNames { get; } = new();
    /// <summary>Patamares de potência mundial (tabela power_tier).</summary>
    public List<PowerTier> PowerTiers { get; } = new();
    public List<ActiveDecision> ActiveDecisions { get; } = new();
    public Dictionary<string, Law> Laws { get; } = new();
    /// <summary>Cabeçalhos das escadas de leis (tabela law_group): nome e chapa de cada grupo.</summary>
    public Dictionary<string, LawGroupDef> LawGroupDefs { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> LawEffects { get; } = new();

    /// <summary>Escolas de doutrina de exército (tabela army_doctrine_branch), pela ordem em que se mostram.</summary>
    public Dictionary<string, DoctrineBranch> DoctrineBranches { get; } = new();
    /// <summary>Doutrinas de exército (tabela army_doctrine) e os multiplicadores de cada uma.</summary>
    public Dictionary<string, ArmyDoctrine> ArmyDoctrines { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> DoctrineEffects { get; } = new();

    /// <summary>As três armas. Cada uma tem a sua árvore de escolas, a sua experiência e a sua escolha:
    /// um país pode ser da Guerra de Movimento em terra, do Bombardeamento no ar e do Corso no mar ao mesmo
    /// tempo — o que não pode é ser de duas escolas da mesma arma.</summary>
    public const string Land = "exercito", Air = "ar", Sea = "mar";
    public static readonly string[] Domains = { Land, Air, Sea };

    /// <summary>A arma de uma escola (o ramo manda) e a arma de um degrau.</summary>
    public string DomainOfBranch(string branchId) =>
        DoctrineBranches.TryGetValue(branchId, out var b) ? b.Domain : Land;
    public string DomainOf(ArmyDoctrine d) => DomainOfBranch(d.Branch);

    /// <summary>A experiência com que se paga uma escola desta arma, e a maneira de a gastar. São três
    /// bolsos separados: quem só faz guerra em terra nunca compra uma escola do mar.</summary>
    public static float Xp(Country c, string domain) => domain switch
    {
        Air => c.AirXp,
        Sea => c.NavyXp,
        _ => c.ArmyXp,
    };

    public static void SpendXp(Country c, string domain, float cost)
    {
        switch (domain)
        {
            case Air: c.AirXp = MathF.Max(0f, c.AirXp - cost); break;
            case Sea: c.NavyXp = MathF.Max(0f, c.NavyXp - cost); break;
            default: c.ArmyXp = MathF.Max(0f, c.ArmyXp - cost); break;
        }
    }

    /// <summary>Nome da experiência desta arma, para os avisos e o painel dizerem a mesma coisa.</summary>
    public static string XpName(string domain) => domain switch
    {
        Air => "experiência aérea",
        Sea => "experiência naval",
        _ => "experiência de exército",
    };

    /// <summary>O ramo em que este país se formou nesta arma (a primeira doutrina que adoptou manda), ou
    /// null. Por omissão fala-se do exército, que é a arma que existia antes de haver armas.</summary>
    public string? DoctrineBranchOf(Country c, string domain = Land)
    {
        foreach (var id in c.Doctrines.OrderBy(x => x))
            if (ArmyDoctrines.TryGetValue(id, out var d) && DomainOf(d) == domain) return d.Branch;
        return null;
    }

    /// <summary>Esta escola é deste país? As três escolas comuns são de toda a gente; a nacional só do dono
    /// da tag. Um exército não aprende a maneira de fazer a guerra de outro povo por decreto.</summary>
    public static bool BranchIsFor(DoctrineBranch b, Country c) => b.CountryTag is null || b.CountryTag == c.Tag;
    public static bool DoctrineIsFor(ArmyDoctrine d, Country c) => d.CountryTag is null || d.CountryTag == c.Tag;

    /// <summary>As escolas que este país pode abrir, pela ordem em que a árvore as desenha: as comuns por
    /// sort e, no fim, a nacional (que o seed põe em sort alto).</summary>
    public List<DoctrineBranch> Branches(Country c, string domain = Land) =>
        DoctrineBranches.Values.Where(b => b.Domain == domain && BranchIsFor(b, c))
                        .OrderBy(b => b.CountryTag is null ? 0 : 1).ThenBy(b => b.Sort).ThenBy(b => b.Id).ToList();

    /// <summary>Os degraus de uma escola, de baixo para cima, já sem o que é de outro país.</summary>
    public List<ArmyDoctrine> DoctrineSteps(Country c, string branch) =>
        ArmyDoctrines.Values.Where(d => d.Branch == branch && DoctrineIsFor(d, c))
                     .OrderBy(d => d.Sort).ThenBy(d => d.Cost).ThenBy(d => d.Id).ToList();

    /// <summary>O que impede este país de adoptar esta doutrina, ou null se está à mão. Devolve o id da
    /// doutrina que falta, ou "!"+id da doutrina que fechou a escola — a mesma convenção do FocusBlock.
    /// A experiência não entra aqui: quem a conta é o AdoptDoctrineCommand, para a UI poder mostrar uma
    /// doutrina aberta mas ainda por pagar.</summary>
    public string? DoctrineBlock(Country c, string doctrineId)
    {
        if (!ArmyDoctrines.TryGetValue(doctrineId, out var d) || c.Doctrines.Contains(doctrineId)) return doctrineId;
        if (!DoctrineIsFor(d, c)) return doctrineId;              // escola de outro povo: nem se abre
        if (d.Requires is string req && !c.Doctrines.Contains(req)) return req;
        string domain = DomainOf(d);
        foreach (var id in c.Doctrines.OrderBy(x => x))
            if (ArmyDoctrines.TryGetValue(id, out var have) && have.Branch != d.Branch && DomainOf(have) == domain)
                return "!" + id;                                  // só a mesma arma fecha portas
        return null;
    }

    /// <summary>Doutrina à mão e já paga: o que a IA adopta e o que o botão do painel aceita.</summary>
    public bool CanAdopt(Country c, string doctrineId) =>
        DoctrineBlock(c, doctrineId) is null && ArmyDoctrines.TryGetValue(doctrineId, out var d)
        && Xp(c, DomainOf(d)) >= d.Cost;

    public Dictionary<string, SpyOp> SpyOps { get; } = new();
    public List<ActiveSpyOp> ActiveSpyOps { get; } = new();
    /// <summary>Propostas à espera de resposta do jogador (OfferSystem). Uma por par e assunto.</summary>
    public List<PendingOffer> Offers { get; } = new();
    /// <summary>Acordos de comércio de recursos em vigor (TradeSystem).</summary>
    public List<TradeDeal> TradeDeals { get; } = new();
    /// <summary>Empréstimos de material em vigor (LendLeaseSystem): uma fatia do rendimento do benfeitor
    /// entra todos os dias no cofre de quem recebe. Um por par ordenado (quem dá, quem recebe).</summary>
    public List<LendLease> LendLeases { get; } = new();
    /// <summary>Amostras dos gráficos (HistorySystem): jogador + maiores potências, de history_sample_days em history_sample_days.</summary>
    public List<HistorySample> History { get; } = new();
    /// <summary>Rede de informação activa: (autor, alvo) → último dia com visibilidade (efeito intel).</summary>
    public Dictionary<(int A, int B), int> Intel { get; } = new();
    public bool HasIntel(int a, int b) => Intel.TryGetValue((a, b), out var until) && until >= Clock.Day;

    /// <summary>Pactos de não-agressão: (a,b) com a&lt;b → último dia em vigor. Bloqueia DeclareWar.</summary>
    public Dictionary<(int A, int B), int> Pacts { get; } = new();
    public bool HasPact(int a, int b) => Pacts.TryGetValue(WarKey(a, b), out var until) && until >= Clock.Day;

    /// <summary>Tocam-se? Terra com terra, ou mar curto — senão uma ilha não fazia fronteira com ninguém e
    /// ficava para sempre sem vizinhos. É a pergunta que separa a guerra ao lado da guerra do outro lado do
    /// mundo: a primeira faz-se com um pretexto, a segunda precisa que o mundo já esteja em brasa.</summary>
    public bool SharesBorder(int a, int b)
    {
        if (a == b) return false;
        float reach = Rule("vision_sea_km", 250f);
        foreach (var r in Regions.Values)
        {
            if (r.ControllerId != a) continue;
            foreach (int n in r.Neighbours)
                if (Regions.TryGetValue(n, out var o) && o.ControllerId == b) return true;
            foreach (var (n, km) in r.SeaNeighbours)
                if (km <= reach && Regions.TryGetValue(n, out var o) && o.ControllerId == b) return true;
        }
        return false;
    }
    /// <summary>Adidos militares destacados: quem manda → missão. Um por país (AttacheSystem).</summary>
    public Dictionary<int, Attache> Attaches { get; } = new();

    /// <summary>Tipos de missão aérea (tabela air_mission): o que os esquadrões podem ir fazer.</summary>
    public Dictionary<string, AirMissionDef> AirMissionDefs { get; } = new();
    /// <summary>Esquadrões destacados sobre regiões (AirMissionSystem; save s_air_mission). Uma missão por
    /// (país, região): mandar mais asas para o mesmo céu engrossa a que lá está.</summary>
    public List<AirMission> AirMissions { get; } = new();

    /// <summary>Tipos de missão naval (tabela naval_mission) e as esquadras destacadas hoje.</summary>
    public Dictionary<string, NavalMissionDef> NavalMissionDefs { get; } = new();
    public List<NavalMission> NavalMissions { get; } = new();

    /// <summary>Classes de navio (tabela ship_class; Navy): o que cada casco serve no mar.</summary>
    public Dictionary<string, ShipClassDef> ShipClasses { get; } = new();

    /// <summary>Modelos de avião (tabela plane_class; Air): o que cada asa serve no céu.</summary>
    public Dictionary<string, PlaneClassDef> PlaneClasses { get; } = new();

    /// <summary>Marcas de material (tabela equipment_mark; Marks): as gerações de equipamento que a
    /// investigação abre e que a fábrica passa a fazer.</summary>
    public Dictionary<string, EquipmentMarkDef> EquipmentMarks { get; } = new();

    /// <summary>Políticas de ocupação (tabela occupation_policy) e a que cada ocupante assinou sobre cada
    /// povo que tem debaixo de si (save s_occupation; OccupationSystem).</summary>
    public Dictionary<string, OccupationPolicyDef> OccupationPolicyDefs { get; } = new();
    public List<Occupation> Occupations { get; } = new();

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

    /// <summary>Porque é que não podemos mandar voluntários para a guerra deste país (null = podemos).
    /// É a mesma porta do adido, com uma condição a mais: só se manda quem se tem de sobra.</summary>
    public string? VolunteerBlock(int countryId, int hostId)
    {
        if (!Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "país inválido";
        if (!Countries.TryGetValue(hostId, out var h) || h.Capitulated) return "anfitrião inválido";
        if (countryId == hostId) return "para a nossa guerra não se mandam voluntários";
        if (AreAtWar(countryId, hostId)) return $"estamos em guerra com {h.Name}";
        if (!AtWar(hostId)) return $"{h.Name} não está em guerra";
        // o mundo tem de estar já bastante mexido para uma opinião pública aceitar tropa nossa numa guerra
        // que não é nossa — é a porta que a tensão mundial abre (HoI4)
        float need = Rule("volunteer_min_tension", 15f);
        if (need > 0f && WarGame.Core.Systems.WorldTension.Of(this) < need)
            return $"o mundo ainda está calmo demais (tensão {WarGame.Core.Systems.WorldTension.Of(this):0} de {need:0})";
        int cap = WarGame.Core.Systems.VolunteerSystem.Cap(this, countryId);
        if (cap == 0) return $"o exército não chega para emprestar (mínimo {Rule("volunteer_min_army", 5f):0} divisões)";
        int away = WarGame.Core.Systems.VolunteerSystem.Away(this, countryId);
        if (away >= cap) return $"já temos {away} lá fora, que é o limite";
        if (!Divisions.Values.Any(d => d.CountryId == countryId && !d.IsVolunteer)) return "não há divisões em casa";
        return null;
    }

    /// <summary>Escolha feita por evento (s_news_choice no save): event_id → option_id.</summary>
    public Dictionary<string, string> NewsChoices { get; } = new();
    /// <summary>Eventos que já caíram e em cima de quem (s_news_fired no save): event_id → (dia, país;
    /// 0 = o mundo inteiro). Os de data marcada dispensavam isto — via-se pelo calendário — mas os de
    /// estado não têm dia nenhum e o país em que caem sai do mundo do momento, por isso fica escrito.</summary>
    public Dictionary<string, (int Day, int CountryId)> NewsFired { get; } = new();

    /// <summary>Este evento já caiu em cima deste país? É o que decide se os efeitos dele contam no
    /// ApplyTechs. Um evento de estado só conta depois de ter caído; um de data marcada conta pelo
    /// calendário quando não há registo — é o que deixa os saves antigos continuarem certos.</summary>
    public bool NewsHit(NewsEvent e, Country c)
    {
        if (NewsFired.TryGetValue(e.Id, out var hit)) return hit.CountryId == 0 || hit.CountryId == c.Id;
        if (e.IsWatch) return false;
        return e.Day <= Clock.Day && (e.CountryId is null || e.CountryId == c.Id);
    }
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

    /// <summary>A rede ferroviária de partida: cada região nasce com o carril que a gente que lá vive
    /// justifica — uma linha a partir de rail_pop_base habitantes, mais uma por cada rail_pop_mult vezes
    /// essa população, com tecto em rail_max. Não é dado de tabela linha a linha porque não é escolha de
    /// ninguém: é a leitura do mundo que a static.db traz, e é por isso que a Europa nasce cosida de linhas
    /// e o deserto nasce sem nenhuma. A partir daqui quem quer mais carril assenta-o (BuildRailCommand) e é
    /// isso que o save guarda. Só toca em quem ainda não tem carril nenhum (−1), por isso correr isto duas
    /// vezes não desfaz obra nenhuma.</summary>
    public void SeedRails()
    {
        float bas = MathF.Max(1f, Rule("rail_pop_base", 500_000f));
        float mult = MathF.Max(1.01f, Rule("rail_pop_mult", 3f));
        int max = (int)Rule("rail_max", 4f);
        foreach (var r in Regions.Values)
        {
            if (r.Rail >= 0) continue;
            r.Rail = r.Population < bas ? 0
                   : Math.Clamp(1 + (int)MathF.Floor(MathF.Log(r.Population / bas) / MathF.Log(mult)), 0, max);
            r.BaseRail = r.Rail;
        }
    }

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
            // destacado manda no grupo, ferido não manda em nada, e um comandante de outro país (save antigo,
            // tag trocada) não conta para ninguém
            if (!detached.Contains(id) && !w.IsWounded(c.Id, id) && w.GeneralDefs.TryGetValue(id, out var g) && GeneralIsFor(g, c))
                c.GeneralMult[g.StatKey] = c.GeneralMult.GetValueOrDefault(g.StatKey, 1f) * g.Mult;
    }

    /// <summary>Recalcula Country.CabinetMult a partir do gabinete em funções (após nomear, demitir ou
    /// carregar um jogo, e todos os dias pelo CabinetSystem, que a rodagem cresce). Uma pasta com um
    /// conselheiro que já não existe na BD não conta.</summary>
    public static void ApplyCabinet(World w, Country c)
    {
        c.CabinetMult.Clear();
        foreach (var (slot, id) in c.Cabinet)
            if (w.AdvisorDefs.TryGetValue(id, out var a))
            {
                // rodagem: o homem que já lá está há muito conhece a casa e o que faz vale mais
                float factor = 1f + w.Rule("advisor_tenure_bonus", 0.5f) * CabinetTenure(w, c, slot);
                foreach (var (key, mult) in a.Effects)
                    c.CabinetMult[key] = c.CabinetMult.GetValueOrDefault(key, 1f) * (1f + (mult - 1f) * factor);
            }
    }

    /// <summary>Rodagem do conselheiro desta pasta, de 0 (chegou hoje) a 1 (casa conhecida): os dias de casa
    /// sobre a regra advisor_tenure_days. Uma pasta vazia, ou um conselheiro sem data, dá 0.</summary>
    public static float CabinetTenure(World w, Country c, string slot)
    {
        if (!c.CabinetSince.TryGetValue(slot, out int since)) return 0f;
        float days = MathF.Max(1f, w.Rule("advisor_tenure_days", 365f));
        return Math.Clamp((w.Clock.Day - since) / days, 0f, 1f);
    }

    /// <summary>Recalcula Country.TechMult a partir das tecnologias concluídas (chamar após LoadSave e ao concluir uma).</summary>
    public void ApplyTechs(Country c)
    {
        c.TechMult.Clear();
        foreach (var t in c.Techs)                // um programa nacional de outro país na lista não vale nada
            if (Techs.TryGetValue(t, out var def) && TechIsFor(def, c) && TechEffects.TryGetValue(t, out var effs))
                foreach (var (key, mul) in effs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var f in c.FocusesDone)
            if (FocusEffects.TryGetValue(f, out var effs))
                foreach (var (key, mul) in effs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var d in c.Doctrines)          // escolas de guerra: entram no mesmo bolo das tecnologias
            if (ArmyDoctrines.TryGetValue(d, out var def) && DoctrineIsFor(def, c)
                && DoctrineEffects.TryGetValue(d, out var deffs))
                foreach (var (key, mul) in deffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var grp in LawGroups(c))
            if (ActiveLaw(c, grp) is Law law && LawEffects.TryGetValue(law.Id, out var leffs))
                foreach (var (key, mul) in leffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var e in NewsEvents.Values)   // eventos noticiosos já disparados (NewsSystem)
        {
            if (!NewsHit(e, c)) continue;
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
    /// <summary>Este programa é para este país? Os comuns (sem country_tag) são de toda a gente; um programa
    /// nacional é só do dono — não aparece na lista de mais ninguém, e o comando recusa-o a quem o pedir.</summary>
    public static bool TechIsFor(Tech t, Country c) => t.CountryTag is null || t.CountryTag == c.Tag;

    public bool CanResearch(Country c, string techId) =>
        Techs.TryGetValue(techId, out var t) && TechIsFor(t, c) && !c.Techs.Contains(techId)
        && (t.Requires is null || c.Techs.Contains(t.Requires));
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

    /// <summary>A lei é deste país? As de country_tag null são de toda a gente; as outras só de quem lá está
    /// escrito — a questão nacional de um país não se vota no parlamento do vizinho.</summary>
    public static bool LawIsFor(Law l, Country c) => l.CountryTag is null || l.CountryTag == c.Tag;

    /// <summary>As escadas de leis que este país tem: as de toda a gente mais as suas, por ordem de
    /// law_group.sort (o que a base de dados não nomear vai atrás, por id).</summary>
    public List<string> LawGroups(Country c) =>
        Laws.Values.Where(l => LawIsFor(l, c)).Select(l => l.Group).Distinct()
            .OrderBy(g => LawGroupDefs.TryGetValue(g, out var d) ? d.Sort : 999).ThenBy(g => g).ToList();

    /// <summary>Lei activa de um grupo: a escolhida em Country.Laws, senão a is_default do grupo. Uma lei de
    /// outro país nunca conta, nem escolhida à mão nem como default.</summary>
    public Law? ActiveLaw(Country c, string group) =>
        c.Laws.TryGetValue(group, out var id) && Laws.TryGetValue(id, out var chosen)
            && chosen.Group == group && LawIsFor(chosen, c)
            ? chosen
            : Laws.Values.FirstOrDefault(l => l.Group == group && l.IsDefault && LawIsFor(l, c));

    /// <summary>O comandante é deste país? Os de country_tag null são mercenários — contrata-os quem os
    /// pagar; os outros são de casa e mais nenhum estado-maior os chama.</summary>
    public static bool GeneralIsFor(GeneralDef g, Country c) => g.CountryTag is null || g.CountryTag == c.Tag;

    /// <summary>A regra que diz quantas cadeiras tem o estado-maior desta arma. São três escadas
    /// separadas: encher o comando de terra não tira lugar a um almirante.</summary>
    public static string GeneralSlotRule(string domain) => domain switch
    {
        Air => "air_general_slots",
        Sea => "navy_general_slots",
        _ => "general_slots",
    };

    public int GeneralSlots(string domain) => (int)Rule(GeneralSlotRule(domain), domain == Land ? 3f : 2f);

    /// <summary>Quantos comandantes desta arma já servem este país.</summary>
    public int GeneralsInService(Country c, string domain) =>
        c.Generals.Count(id => GeneralDefs.TryGetValue(id, out var g) && g.Domain == domain);

    /// <summary>A arma de um comandante contratado (por omissão, terra: é o que o save antigo tem).</summary>
    public string DomainOfGeneral(string generalId) =>
        GeneralDefs.TryGetValue(generalId, out var g) ? g.Domain : Land;

    /// <summary>A folha de comandantes deste país: os de casa primeiro (é a marca do país, e é o que vale
    /// mais), depois os mercenários, cada bloco do mais barato ao mais caro.</summary>
    public List<GeneralDef> GeneralPool(Country c) =>
        GeneralDefs.Values.Where(g => GeneralIsFor(g, c))
                   .OrderByDescending(g => g.CountryTag is not null).ThenBy(g => g.Cost).ThenBy(g => g.Id).ToList();

    public float TemplateCost(int templateId) =>
        Units.GetTemplate(templateId).Units.Sum(u => Units.GetUnitType(u.UnitTypeId).Cost * u.Qty);

    /// <summary>O que uma encomenda custa à fábrica: a divisão inteira, ou um conjunto de material do tipo
    /// que a linha fabrica. A conta é a mesma para os dois feitios — é por isso que uma divisão de nove
    /// batalhões custa exactamente o mesmo que os nove conjuntos que a voltam a armar de novo.</summary>
    public float OrderCost(ProductionOrder o)
    {
        // material melhor custa mais a fazer: a marca da linha multiplica o preço do conjunto (Marks)
        try { return o.IsKit ? Units.GetUnitType(o.UnitTypeId).Cost * Marks.Cost(this, o.UnitTypeId, o.Mark)
                             : TemplateCost(o.TemplateId); }
        catch { return 0f; }
    }

    /// <summary>Quantos conjuntos de material daquele tipo é que este modelo pede — os batalhões que o
    /// modelo tem desse tipo. Uma divisão a 100% tem-nos todos; a 60% tem 60% de cada.</summary>
    public IReadOnlyList<(int UnitTypeId, int Qty)> KitNeed(int templateId)
    {
        try { return Units.GetTemplate(templateId).Units; } catch { return Array.Empty<(int, int)>(); }
    }

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

    /// <summary>Esta gravidade de baixa serve esta arma? Sem arma (wound_kind.domain nulo) serve todas —
    /// um estilhaço apanha qualquer um; com arma é só de lá, que não se cai de pára-quedas à frente de uma
    /// divisão de infantaria nem se vai ao fundo com um navio que não se tem.</summary>
    public static bool WoundIsFor(WoundKind k, string domain) => k.Domain is null || k.Domain == domain;

    /// <summary>A regra que diz a probabilidade de um comandante desta arma cair num combate dela. São
    /// três armas com três riscos: a batalha em terra é uma coisa, o céu disputado é outra.</summary>
    public static string WoundChanceRule(string domain) => domain switch
    {
        Air => "wound_chance_air",
        Sea => "wound_chance_sea",
        _ => "wound_chance",
    };

    /// <summary>Esta condecoração serve este país? A fita comum (country_tag nulo) serve todo o exército
    /// que não traga as suas; uma condecoração nacional é só de quem a traz — uma Victoria Cross não se
    /// prega numa divisão argentina.</summary>
    public static bool MedalIsFor(MedalDef m, Country c) => m.CountryTag is null || m.CountryTag == c.Tag;

    /// <summary>As condecorações COMUNS, do grau mais baixo para o mais alto.</summary>
    public List<MedalDef> Medals() =>
        MedalDefs.Values.Where(m => m.CountryTag is null).OrderBy(m => m.Sort).ToList();

    /// <summary>O medalheiro deste país: o dele, se trouxer um (data/countries/&lt;TAG&gt;.sql), senão o comum.
    /// Os limiares e os bónus são os mesmos dos dois lados — o que muda é o nome que a divisão passa a
    /// trazer, como nas escadas de postos.</summary>
    public List<MedalDef> Medals(Country c)
    {
        var own = MedalDefs.Values.Where(m => m.CountryTag == c.Tag).OrderBy(m => m.Sort).ToList();
        return own.Count > 0 ? own : Medals();
    }

    /// <summary>O mesmo medalheiro, pelo número do país — é o que a UI e o sistema têm à mão.</summary>
    public List<MedalDef> Medals(int countryId) =>
        Countries.TryGetValue(countryId, out var c) ? Medals(c) : Medals();

    /// <summary>O país condecora com fitas próprias (e não com as comuns)?</summary>
    public bool HasOwnMedals(Country c) => MedalDefs.Values.Any(m => m.CountryTag == c.Tag);

    /// <summary>Este nome de formação serve este país? O fundo comum (country_tag nulo) serve quem não traz
    /// o seu; um nome nacional é só de quem o traz — uma Home Fleet não zarpa de um porto argentino.</summary>
    public static bool FormationNameIsFor(FormationName n, Country c) => n.CountryTag is null || n.CountryTag == c.Tag;

    /// <summary>O fundo COMUM de nomes de uma arma, pela ordem por que se pegam.</summary>
    public List<FormationName> Formations(string domain) =>
        FormationNames.Where(n => n.Domain == domain && n.CountryTag is null).OrderBy(n => n.Sort).ToList();

    /// <summary>O fundo de nomes daquela arma por que este país baptiza as formações: o dele, se trouxer um
    /// (data/countries/&lt;TAG&gt;.sql), senão o comum. Um nome não muda conta nenhuma do jogo.</summary>
    public List<FormationName> Formations(string domain, Country c)
    {
        var own = FormationNames.Where(n => n.Domain == domain && n.CountryTag == c.Tag).OrderBy(n => n.Sort).ToList();
        return own.Count > 0 ? own : Formations(domain);
    }

    /// <summary>O país baptiza as formações daquela arma com nomes próprios (e não com os comuns)?</summary>
    public bool HasOwnFormations(Country c, string domain) =>
        FormationNames.Any(n => n.Domain == domain && n.CountryTag == c.Tag);

    /// <summary>Este nome é do fundo de casa deste país (e não do comum)? É o que a UI põe o selo ⚜ a dizer.</summary>
    public bool IsHomeFormationName(Country c, string domain, string name) =>
        FormationNames.Any(n => n.Domain == domain && n.CountryTag == c.Tag && n.Name == name);

    /// <summary>O nome da próxima formação daquela arma: o primeiro do fundo do país que ainda não esteja no
    /// ar (ou no mar). Esgotado o fundo, a formação fica com o nome da região onde serve — uma guerra grande
    /// tem mais esquadrilhas do que tradições, e "Asa de Braga" é melhor do que uma sem nome. Se até esse
    /// nome estiver tomado, numera-se.</summary>
    public string NextFormationName(int countryId, string domain, int regionId)
    {
        var taken = (domain == Sea
            ? NavalMissions.Where(m => m.CountryId == countryId).Select(m => m.Name)
            : AirMissions.Where(m => m.CountryId == countryId).Select(m => m.Name)).ToHashSet();

        if (Countries.TryGetValue(countryId, out var c))
            foreach (var n in Formations(domain, c))
                if (!taken.Contains(n.Name)) return n.Name;

        string place = Regions.TryGetValue(regionId, out var r) ? r.Name : "Fronteira";
        string bare = $"{(domain == Sea ? "Esquadra" : "Asa")} de {place}";
        if (!taken.Contains(bare)) return bare;
        for (int i = 2; ; i++)
            if (!taken.Contains($"{bare} {i}")) return $"{bare} {i}";
    }

    /// <summary>Este posto serve este país? A escada comum (country_tag nulo) serve toda a gente; uma
    /// escada nacional é só de quem a traz — um Generalfeldmarschall não se põe num exército português.</summary>
    public static bool RankIsFor(GeneralRank r, Country c) => r.CountryTag is null || r.CountryTag == c.Tag;

    /// <summary>A escada COMUM de uma arma, do mais baixo para o mais alto. Cada arma tem a sua: um
    /// brigadeiro não é um contra-almirante, e quem manda numa esquadra não sobe pela escada da infantaria.</summary>
    public List<GeneralRank> Ranks(string domain) =>
        GeneralRanks.Where(r => r.Domain == domain && r.CountryTag is null).OrderBy(r => r.Xp).ToList();

    /// <summary>A escada por que este país sobe naquela arma: a dele, se trouxer uma (data/countries/&lt;TAG&gt;.sql),
    /// senão a comum. Os degraus pedem a mesma experiência e valem o mesmo nos dois casos — muda o nome.</summary>
    public List<GeneralRank> Ranks(string domain, Country c)
    {
        var own = GeneralRanks.Where(r => r.Domain == domain && r.CountryTag == c.Tag).OrderBy(r => r.Xp).ToList();
        return own.Count > 0 ? own : Ranks(domain);
    }

    /// <summary>A mesma escada, pelo número do país — é o que a UI tem à mão.</summary>
    public List<GeneralRank> Ranks(string domain, int countryId) =>
        Countries.TryGetValue(countryId, out var c) ? Ranks(domain, c) : Ranks(domain);

    /// <summary>O país sobe por escada própria naquela arma (e não pela comum)?</summary>
    public bool HasOwnRanks(Country c, string domain) =>
        GeneralRanks.Any(r => r.Domain == domain && r.CountryTag == c.Tag);

    /// <summary>Posto actual de um comandante contratado: o mais alto da escada da ARMA dele cuja
    /// experiência ele já passou. Sem tabela de postos (ou sem escada para aquela arma) devolve null — o
    /// comando vale o de sempre.</summary>
    public GeneralRank? RankOf(int countryId, string generalId)
    {
        if (GeneralRanks.Count == 0 || !Countries.TryGetValue(countryId, out var c)) return null;
        float xp = c.GeneralXp.GetValueOrDefault(generalId);
        GeneralRank? best = null;
        foreach (var r in Ranks(DomainOfGeneral(generalId), c))
            if (xp >= r.Xp && (best is null || r.Xp > best.Xp)) best = r;
        return best;
    }

    /// <summary>Posto seguinte na escada da arma dele, para a UI mostrar quanto falta para a promoção
    /// (null = já é o topo).</summary>
    public GeneralRank? NextRank(int countryId, string generalId)
    {
        if (!Countries.TryGetValue(countryId, out var c)) return null;
        float xp = c.GeneralXp.GetValueOrDefault(generalId);
        GeneralRank? next = null;
        foreach (var r in Ranks(DomainOfGeneral(generalId), c))
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
        if (d.RegionId != regionId) d.Entrench = 0f;   // trincheira não se leva às costas
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
