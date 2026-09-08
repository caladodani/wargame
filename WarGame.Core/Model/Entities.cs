using WarGame.Core.Stats;

namespace WarGame.Core.Model;

public sealed record UnitType(int Id, string Name, string Category, float Cost, int BuildDays, float Mobility, float SupplyUse, StatBlock Stats);

public sealed record DivisionTemplate(int Id, int CountryId, string Name, IReadOnlyList<(int UnitTypeId, int Qty)> Units);

/// <summary>Espírito nacional (tabela national_spirit); os efeitos são linhas modifier com SpiritId.</summary>
/// <summary>Tecnologia (tabela tech). Cost = dias com research_speed 1; Requires = id da anterior no ramo.</summary>
public sealed record Tech(string Id, string Branch, string Name, float Cost, string? Requires, string? Description,
                          string? CountryTag = null);
/// <summary>Evento noticioso com data marcada (tabela news_event); CountryId null = global.</summary>
public sealed record NewsEvent(string Id, int Day, int? CountryId, string Title, string Body);
/// <summary>Escolha de um evento noticioso (news_event_option). A IA fica com a primeira (sort).</summary>
public sealed record NewsOption(string Id, string EventId, string Title, int Sort);
/// <summary>Lei nacional (tabela law): grupos (conscrição, economia…) com uma lei activa por grupo.
/// Sort maior = mais mobilizada (a IA escala em guerra). Efeitos em law_effect.</summary>
/// <summary>Lei nacional (tabela law). CountryTag null = lei de toda a gente; com tag, só esse país a tem
/// na escada — é assim que cada país ganha a sua questão nacional sem a emprestar aos vizinhos.</summary>
public sealed record Law(string Id, string Group, string Name, string Description, int Sort, bool IsDefault,
                         string? CountryTag = null);

/// <summary>Cabeçalho de um grupo de leis (tabela law_group): como se chama a escada e que chapa leva. O
/// painel deixou de saber os nomes de cor — vêm da base de dados como tudo o resto.</summary>
public sealed record LawGroupDef(string Id, string Name, string Icon, int Sort, string? CountryTag = null);

/// <summary>Operação de espionagem (tabela spy_op): one-shot, paga à partida, efeito ao concluir.</summary>
public sealed record SpyOp(string Id, string Name, string Description, float Cost, int Days, string Effect, float Magnitude, string Scope = "country")
{
    /// <summary>Operação de sabotagem: escolhe-se uma região do inimigo, não só o país.</summary>
    public bool IsRegional => Scope == "region";
}

/// <summary>Operação em curso (World.ActiveSpyOps; EspionageSystem conta os dias).</summary>
public sealed class ActiveSpyOp
{
    public int CountryId { get; init; }
    public int TargetCountryId { get; init; }
    public string OpId { get; init; } = "";
    public float DaysLeft { get; set; }
    public int RegionId { get; init; }        // alvo da sabotagem (0 = operação contra o país inteiro)
}
/// <summary>Proposta que um país põe em cima da mesa de outro e que fica à espera de resposta. Só existe
/// quando quem recebe é gente que não decide sozinha (o jogador): entre países da IA a resposta sai no
/// mesmo dia. Kind diz de que é a proposta ("prisioneiros"); Men é o tamanho combinado no dia em que foi
/// feita, e serve para a UI mostrar números — quem manda no fim é o estado dos campos nesse momento.</summary>
public sealed class PendingOffer
{
    public int FromId { get; init; }
    public int ToId { get; init; }
    public string Kind { get; init; } = "prisioneiros";
    public int Men { get; set; }
    /// <summary>Região em cima da mesa (assunto "regiao"); 0 nos assuntos que não mexem no mapa.</summary>
    public int RegionId { get; init; }
    public int Day { get; init; }
    public int ExpiresDay { get; init; }
}

/// <summary>Foco nacional (HoI4): tabela focus; efeitos = focus_effect (multiplicadores de Stat).</summary>
public sealed record Focus(string Id, int CountryId, string Name, string Description, int Days, string? Requires, int Sort);

public sealed record NationalSpirit(string Id, string CountryTag, string Name, string Description);

/// <summary>Texto do painel de país (tabela country_info).</summary>
public sealed record CountryInfo(string CountryTag, string Government, string Leader, string Doctrine, string Alliance, string Description);

/// <summary>Ramo da árvore de doutrinas de exército (tabela army_doctrine_branch): a escola militar em que
/// um país se forma. Escolhida uma, as outras fecham-se — um exército não se treina em duas maneiras
/// contrárias de fazer a guerra ao mesmo tempo.</summary>
/// <summary>Ramo da árvore de doutrinas. Com CountryTag é a escola nacional desse país — a maneira própria
/// de fazer a guerra que mais ninguém pode aprender. Domain diz a que arma pertence a escola
/// (World.Land "exercito", World.Air "ar", World.Sea "mar"): cada arma tem a sua experiência e a sua
/// escolha, e escolher a escola do ar não fecha nenhuma escola de terra.</summary>
public sealed record DoctrineBranch(string Id, string Name, string Icon, int Sort, string? CountryTag = null,
                                    string Domain = "exercito");

/// <summary>Doutrina de exército (tabela army_doctrine): degrau de uma escola militar, pago com a
/// experiência de campanha que o país juntou (Country.ArmyXp). Efeitos em army_doctrine_effect, aplicados
/// como os das tecnologias. Requires é a doutrina anterior do mesmo ramo (null = raiz da escola).
///
/// Nada disto se confunde com as leis do grupo doctrine: essas são decretos do governo que se trocam à
/// vontade; estas são escolas de guerra que se aprendem com sangue e não se desaprendem.</summary>
public sealed record ArmyDoctrine(string Id, string Branch, string Name, string Description, float Cost,
                                  string? Requires, int Sort, string? CountryTag = null);

/// <summary>Aliança defensiva (tabelas faction + faction_member, HoI4: facção). Um país pode pertencer a várias;
/// declarar guerra a um membro chama os outros contra o agressor (DeclareWarCommand) — ver World.FactionsOf/Allies.</summary>
public sealed record Faction(string Id, string Name, string Description, List<int> Members);

public sealed class Region
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int OwnerId { get; set; }
    /// <summary>Dono na static.db; se OwnerId difere (capitulação), o save tem de guardar o novo dono.</summary>
    public int InitialOwnerId { get; init; }
    public int ControllerId { get; set; }
    public string Terrain { get; init; } = "plain";
    public bool River { get; init; }
    public float Infrastructure { get; set; } = 1f;
    /// <summary>Obra de infraestrutura em curso (ConstructionSystem); cancela se a região for capturada.</summary>
    public bool Building { get; set; }
    public float BuildProgress { get; set; }
    /// <summary>Nível de fortificação (0..fort_max): multiplica a força dos defensores (fort_defense_per_level).
    /// Captura tira um nível. Obra própria (FortBuilding/FortProgress) ao lado da de infraestrutura.</summary>
    public int Fort { get; set; }
    public bool FortBuilding { get; set; }
    public float FortProgress { get; set; }
    /// <summary>Níveis de edifícios construídos (tabela building; ConstructionSystem soma o efeito ao controlador).</summary>
    public Dictionary<string, int> Buildings { get; init; } = new();
    /// <summary>Edifício em obra (id da tabela building; null = nenhuma) e dias de progresso.</summary>
    public string? Project { get; set; }
    public float ProjectProgress { get; set; }
    /// <summary>Resistência da população ocupada (0..1, ResistanceSystem): cresce sem guarnição do ocupante,
    /// corta o rendimento (resistance_output_hit) e a 1.0 devolve o controlo ao dono.</summary>
    public float Resistance { get; set; }
    /// <summary>Dias de integração acumulados (IntegrationSystem): com a resistência dominada, a região
    /// ocupada passa a ser do controlador ao fim de integration_days.</summary>
    public float Integration { get; set; }
    /// <summary>Infraestrutura de origem (tabela region): tecto da reparação natural, nunca muda no jogo.
    /// Obras pagas passam deste valor; a guerra danifica abaixo dele e o tempo repõe-no.</summary>
    public float BaseInfrastructure { get; init; }
    public int Population { get; init; }
    public float CenterX { get; init; }            // centróide projectado (unidades do mapa); só para UI/IA
    public float CenterY { get; init; }
    public List<int> Neighbours { get; init; } = new();
    /// <summary>Ligações marítimas (sea_link): região costeira → km da travessia. Vazio = interior.</summary>
    public Dictionary<int, float> SeaNeighbours { get; init; } = new();
    public bool Coastal { get; init; }
    public List<int> DivisionIds { get; } = new();
    /// <summary>Depósitos de recursos (region_resource): resource id → unidades. Rende ao controlador.</summary>
    public Dictionary<string, float> Resources { get; init; } = new();
}

/// <summary>Edifício construível numa região (tabela building): cada nível multiplica StatKey do controlador por (1+PerLevel).</summary>
/// <summary>Edifício regional (tabela building). Coastal = só se constrói em região de costa;
/// SupplyRange = alcance em km a que o edifício projecta abastecimento por mar, por nível (0 = nenhum).</summary>
/// <summary>Condecoração de divisão (tabela medal). Metric: "xp", "battles" ou "captures"; ao passar
/// o limiar a divisão ganha-a para sempre e Bonus soma-se à sua força (tecto medal_bonus_max).</summary>
public sealed record MedalDef(string Id, string Name, string Description, string Metric, float Threshold, float Bonus,
                             int Sort, string? CountryTag = null);

/// <summary>Um nome de formação do fundo (tabela formation_name): o nome que a próxima asa ou esquadra do
/// país vai levar. Domain diz de que arma é (World.Air / World.Sea) e Sort a ordem por que se pegam.
/// CountryTag nulo = fundo comum, que serve quem não traz o seu.</summary>
public sealed record FormationName(string Id, string Name, string Domain, int Sort, string? CountryTag = null);

/// <summary>Honra de batalha (tabela division_honour). Ao contrário das condecorações, que se acumulam,
/// uma divisão só carrega UMA honra — a mais alta que mereceu — e ela passa a fazer parte do nome:
/// "3.ª de Infantaria «Leões de Braga»". Title é um molde onde {r} é o nome da região onde a honra foi
/// ganha, e Bonus é o que a tropa ganha em recomposição de organização (moral, não força bruta).</summary>
public sealed record HonourDef(string Id, string Title, string Description, string Metric, float Threshold, float Bonus, int Sort);

/// <summary>Estação do ano (tabela season, meses pela tabela season_month). Multiplica a marcha (MoveMult),
/// a recomposição de organização (OrgMult) e cobra Attrition de organização por dia a quem está em campo
/// nos terrenos que ela castiga (WeatherSystem). Inverno é o que muda a guerra: as colunas ficam atoladas
/// e a tropa em campo aberto gasta-se sem um tiro.</summary>
public sealed record SeasonDef(string Id, string Name, string Icon, float MoveMult, float OrgMult, float Attrition, string Note);

/// <summary>Género de acontecimento da crónica (tabela chronicle_kind): o ícone com que aparece na linha do
/// tempo e o peso (1 = rotina, 3 = história). A regra chronicle_min_weight decide o que chega a ser escrito —
/// mudar o que a campanha lembra é mudar uma linha de SQL, não o código.</summary>
public sealed record ChronicleKind(string Id, string Name, string Icon, int Weight);

/// <summary>Uma entrada da crónica da campanha (ChronicleSystem). Guarda-se no save: o Jornal morria com a
/// sessão e a guerra de há dois anos ficava sem memória nenhuma.</summary>
public sealed record ChronicleEntry(int Day, string Kind, string Text, int CountryId, int RegionId);

/// <summary>Um modo de mapa (tabela map_mode): como o mapa se pinta e o que a legenda diz nas duas pontas.
/// Métrica "owner" é o mapa político de sempre — cor do controlador, sem escala.</summary>
public sealed record MapModeDef(string Id, string Name, string Icon, string Metric, string Low, string High, int Sort);

/// <param name="Yard">Fila de fábricas que este edifício alimenta (coluna building.yard): "civil", "militar",
/// "naval" ou vazio. É o que liga um edifício aos contadores do Industry.</param>
/// <param name="Icon">Desenho do edifício na lista do Construir (coluna building.icon). Vem da tabela e não
/// do código pela mesma razão que o resto: um edifício novo é uma linha de SQL, não uma linha de C#.</param>
public sealed record BuildingDef(string Id, string Name, float Cost, float Days, string StatKey, float PerLevel, int MaxLevel,
    bool Coastal = false, float SupplyRange = 0f, string Yard = "", string Icon = "");

/// <summary>Decisão nacional (tabela decision): buff temporário pago — Mult no StatKey durante Days,
/// depois Cooldown dias de espera.</summary>
public sealed record DecisionDef(string Id, string Name, float Cost, int Days, int Cooldown, string StatKey, float Mult);

/// <summary>Nível de dificuldade (tabelas difficulty/difficulty_effect): as regras que reescreve.</summary>
public sealed record DifficultyDef(string Id, string Name, int Sort, Dictionary<string, float> Effects);

/// <summary>Comandante contratável (tabela general): custo único e um multiplicador num stat enquanto servir.
/// CountryTag null = mercenário, serve quem o pagar; com tag, é o comandante de casa e mais nenhum país o
/// chama. Icon é a chapa do retrato e Note a linha da folha de serviço que o estado-maior mostra.
///
/// Domain é a arma que ele comanda (exercito | ar | mar): ocupa uma cadeira dessa arma e o que multiplica
/// é do ofício dela. Xp é a experiência dessa arma que a nomeação custa além do dinheiro — um chefe de
/// caça tira-se das horas de voo do país, e essa experiência é a mesma com que se pagam as escolas.</summary>
public sealed record GeneralDef(string Id, string Name, string StatKey, float Mult, float Cost,
                                string? CountryTag = null, string Icon = "🎖", string Note = "",
                                string Domain = "exercito", float Xp = 0f);

/// <summary>Pasta do gabinete civil (tabela cabinet_slot): uma cadeira por pasta e por país.</summary>
public sealed record CabinetSlotDef(string Id, string Name, string Icon, int Sort);

/// <summary>Conselheiro civil (tabelas advisor/advisor_effect): senta-se numa pasta, custa a nomeação e um
/// salário por dia, e enquanto lá está multiplica os stats de Effects. CountryTag null = serve qualquer país.</summary>
public sealed record AdvisorDef(string Id, string? CountryTag, string Slot, string Name, string Icon,
                                float Cost, string Note, Dictionary<string, float> Effects);

/// <summary>Patamar de potência mundial (tabela power_tier): a partir de MinShare da potência total do
/// mundo, um país é chamado assim. Puro rótulo — quem faz a conta é o PowerIndex.</summary>
public sealed record PowerTier(int Level, string Name, float MinShare);

/// <summary>Posto de comandante (tabela general_rank): a partir de Xp de experiência de campanha o
/// comandante sobe a este posto e soma Bonus ao que o destacamento já amplifica. Os nomes e os
/// limiares são dados, não código — mudar a progressão é mexer na tabela.
///
/// Domain é a arma a que a escada pertence (World.Land/Air/Sea): um brigadeiro não é um contra-almirante,
/// e cada comandante só sobe pela escada da arma dele — ver World.RankOf.
///
/// CountryTag NULL é a escada comum; com tag é a escada daquele país e só dele (World.RankIsFor). Os
/// limiares e os bónus são os mesmos: o que a escada nacional muda é o NOME do posto — um Generalfeldmarschall
/// e um Marechal do Reino valem o mesmo, chamam-se é de maneira diferente.</summary>
public sealed record GeneralRank(string Domain, int Level, string Name, float Xp, float Bonus, string? CountryTag = null);

/// <summary>Gravidade de uma baixa no comando (tabela wound_kind): quantos dias tira o comandante de
/// serviço, o peso com que sai no sorteio e se é fatal. Um arranhão e um caixão são a mesma linha com
/// números diferentes — a progressão muda-se na tabela, não no código.</summary>
public sealed record WoundKind(string Id, string Name, string Icon, int Days, float Weight, bool Fatal,
                               string? Domain = null);

/// <summary>Decisão activa (World.ActiveDecisions; persistida em s_decision).</summary>
public sealed class ActiveDecision
{
    public int CountryId { get; init; }
    public string DecisionId { get; init; } = "";
    public int UntilDay { get; set; }
}

/// <summary>Amostra periódica para os gráficos de evolução (HistorySystem, tabela s_history).</summary>
/// <summary>Amostra periódica de um país (HistorySystem). Power é a nota do PowerIndex nesse dia: sem ela
/// a história só contava coisas contáveis e a subida de um império industrial não se via em lado nenhum.</summary>
public sealed record HistorySample(int Day, int CountryId, float Money, int Divisions, int Regions, float Power = 0f);

/// <summary>Acordo de comércio (World.TradeDeals): o comprador conta Units dos depósitos do vendedor e
/// paga-lhe Units × PricePerUnit por dia (TradeSystem). O preço é o do mercado no dia da assinatura e fica
/// travado até ao fim do contrato — é isso que faz um tratado valer alguma coisa quando o mercado aperta.
/// Cai com guerra, falta de depósitos, falta de dinheiro, ou no dia em que o prazo acaba.</summary>
/// <summary>Política de ocupação (tabela occupation_policy; OccupationSystem): o que se faz ao povo da terra
/// tomada. Resistance/Yield/Manpower são multiplicadores (1 = como era antes de haver políticas).</summary>
public sealed record OccupationPolicyDef(string Id, string Name, string Icon, float Resistance, float Yield,
                                         float Manpower, string Note, int Sort);

/// <summary>Política que um ocupante aplica ao povo de um país (save: s_occupation).</summary>
public sealed class Occupation
{
    public int CountryId { get; init; }
    public int TargetId { get; init; }
    public string PolicyId { get; set; } = "";
    /// <summary>Dia em que foi assinada: trava a troca seguinte por occupation_switch_days.</summary>
    public int SinceDay { get; set; }
}

public sealed class TradeDeal
{
    public int BuyerId { get; init; }
    public int SellerId { get; init; }
    public string ResourceId { get; init; } = "";
    public float Units { get; init; }
    /// <summary>Preço por unidade travado à assinatura (0 nos acordos velhos: lê-se a regra do dia).</summary>
    public float PricePerUnit { get; init; }
    /// <summary>Dia em que o contrato acaba (0 = sem prazo, como eram todos antes dos tratados).</summary>
    public int UntilDay { get; init; }
}

/// <summary>Empréstimo de material em vigor: uma fatia do rendimento diário de quem empresta passa para
/// quem recebe, todos os dias, sem contrapartida (LendLeaseSystem). Não é uma transferência única como o
/// TransferMoneyCommand — é uma torneira aberta que só fecha por ordem, por guerra entre os dois ou por
/// capitulação.</summary>
public sealed class LendLease
{
    /// <summary>Quem paga: sai-lhe do cofre Share do rendimento do dia.</summary>
    public int FromId { get; init; }
    public int ToId { get; init; }
    /// <summary>Fatia do rendimento diário do benfeitor (0..lend_lease_max_share).</summary>
    public float Share { get; set; }
    public int SinceDay { get; init; }
    /// <summary>Total já entregue ao destinatário desde a assinatura (o que se perdeu no caminho não conta).</summary>
    public float SentTotal { get; set; }
}

/// <summary>Tipo de recurso estratégico (tabela resource): cada unidade controlada multiplica
/// StatKey por (1+PerUnit), até Cap unidades (ResourceSystem).</summary>
/// <param name="FuelPerUnit">Combustível por dia que cada unidade deste recurso refina (0 = não se refina).
/// É por aqui que o jogo sabe qual é o petróleo sem ter a palavra "petróleo" escrita em código.</param>
public sealed record ResourceDef(string Id, string Name, string StatKey, float PerUnit, float Cap,
                                 float FuelPerUnit = 0f);

/// <summary>Contadores de um dos lados de uma guerra (WarStatsSystem alimenta-os por eventos).</summary>
public sealed class WarSide
{
    public int RegionsTaken { get; set; }    // regiões tiradas ao inimigo
    public int DivisionsLost { get; set; }   // divisões próprias destruídas
    public int BattlesWon { get; set; }      // batalhas ganhas (a atacar ou a defender)
    /// <summary>Objectivo de guerra: as regiões do inimigo que este lado quer (WarGoalSystem escolhe-as
    /// no início da guerra e nunca mais mexe). Vazio enquanto não houver candidatas.</summary>
    public HashSet<int> Goals { get; } = new();
}

/// <summary>Estado de uma guerra em curso (World.Wars, chave min,max).</summary>
public sealed class WarInfo
{
    /// <summary>Os dois beligerantes, já normalizados: A é o id menor (é a chave em World.Wars).</summary>
    public int A { get; init; }
    public int B { get; init; }
    public int StartDay { get; set; }
    /// <summary>Último dia em que um dos dois capturou região ao outro; estagnado → paz branca.</summary>
    public int LastProgressDay { get; set; }
    public WarSide SideA { get; } = new();
    public WarSide SideB { get; } = new();
    public bool Involves(int countryId) => countryId == A || countryId == B;
    /// <summary>Contadores do país indicado (só faz sentido para um dos dois beligerantes).</summary>
    public WarSide Side(int countryId) => countryId == A ? SideA : SideB;
    public WarSide Enemy(int countryId) => countryId == A ? SideB : SideA;
    public int EnemyOf(int countryId) => countryId == A ? B : A;
}

/// <summary>Guerra acabada, com o saldo final: fica em World.WarHistory para o resumo do jogador.</summary>
public sealed record WarRecord(int A, int B, int StartDay, int EndDay,
    int ARegions, int BRegions, int ALosses, int BLosses, int ABattles, int BBattles)
{
    public int Days => EndDay - StartDay;
    public int Regions(int countryId) => countryId == A ? ARegions : BRegions;
    public int Losses(int countryId) => countryId == A ? ALosses : BLosses;
    public int Battles(int countryId) => countryId == A ? ABattles : BBattles;
    public bool Involves(int countryId) => countryId == A || countryId == B;
    /// <summary>Quem saiu por cima: mais regiões tomadas; empate = ninguém (null).</summary>
    public int? Winner => ARegions == BRegions ? null : ARegions > BRegions ? A : B;
}

/// <summary>Um adido militar destacado junto de outro país (AttacheSystem): quem o manda paga todos os dias
/// e aprende com a guerra dos outros enquanto ela durar.</summary>
public sealed class Attache
{
    public int CountryId { get; init; }
    public int HostId { get; init; }
    public int SinceDay { get; init; }
    /// <summary>Experiência já trazida por esta missão (o painel mostra-a; o save guarda-a).</summary>
    public float Learned { get; set; }
}

/// <summary>Tipo de missão aérea (tabela air_mission): o que um esquadrão vai lá fazer. Effect diz qual dos
/// três papéis é — "superiority" (varrer o céu da região e pesar no combate), "support" (bater no chão ao
/// lado da nossa tropa) ou "bombing" (deitar abaixo a infraestrutura de quem lá manda) — e Value é o que
/// cada asa vale nesse papel. Trocar o que a aviação faz é trocar linhas desta tabela.</summary>
public sealed record AirMissionDef(string Id, string Name, string Icon, string Effect, float Value, string Note, int Sort);

/// <summary>Um esquadrão destacado para uma região (AirMissionSystem; save s_air_mission). Wings são asas
/// do pool nacional (Country.AirPower) que ficam presas a esta missão até serem chamadas de volta — ou até
/// serem abatidas no céu de lá.</summary>
public sealed class AirMission
{
    public int CountryId { get; init; }
    public int RegionId { get; init; }
    public string MissionId { get; init; } = "";
    public float Wings { get; set; }
    public int SinceDay { get; init; }
    /// <summary>Nome próprio da asa (tabela formation_name; save s_air_mission.name). Muda de tarefa sem
    /// mudar de nome: quem está no céu de uma região é sempre a mesma gente. "" = save antigo, sem nome.</summary>
    public string Name { get; set; } = "";
}

/// <summary>Tipo de missão naval (tabela naval_mission): o que uma esquadra vai fazer ao mar de uma costa.
/// Effect diz qual dos três papéis é — "blockade" (fechar o mar àquela costa), "escort" (acompanhar os
/// nossos comboios e desfazer o bloqueio) ou "patrol" (vigiar aquele mar e tirar a costa do nevoeiro).
/// Trocar o que a marinha faz é trocar linhas desta tabela.</summary>
public sealed record NavalMissionDef(string Id, string Name, string Icon, string Effect, float Value, string Note, int Sort);

/// <summary>Uma esquadra destacada para o mar de uma região costeira (NavalMissionSystem; save
/// s_naval_mission). Ships são navios do pool nacional (Country.Warships) que ficam presos a esta missão
/// até serem chamados de volta — ou até irem ao fundo naquele mar.</summary>
public sealed class NavalMission
{
    public int CountryId { get; init; }
    public int RegionId { get; init; }
    public string MissionId { get; init; } = "";
    public float Ships { get; set; }
    public int SinceDay { get; init; }
    /// <summary>Nome próprio da esquadra (tabela formation_name; save s_naval_mission.name).</summary>
    public string Name { get; set; } = "";
}

/// <summary>Uma encomenda na fila: divisão inteira de um template. Progress em pontos gastos.</summary>
public sealed class ProductionOrder
{
    public int TemplateId { get; init; }
    public float Progress { get; set; }
    /// <summary>Produção em série: ao ser entregue, a encomenda volta ao fim da fila (ProductionSystem).</summary>
    public bool Repeat { get; set; }
    /// <summary>Fábricas militares dedicadas a esta encomenda (HoI4: linhas de produção atribuídas). Cada uma
    /// vale um dia de trabalho por dia; uma encomenda com três anda três vezes mais depressa e tira duas
    /// fábricas ao resto da fila. Por omissão uma, que é o que sempre foi.</summary>
    public int Factories { get; set; } = 1;
    /// <summary>Ritmo da linha de montagem (HoI4: production efficiency). Uma linha nova anda ao ritmo de
    /// origem (1) e só ganha jeito depois de a primeira unidade sair — a primeira é sempre um protótipo. A
    /// partir daí sobe todos os dias em que produz, até ao tecto (line_efficiency_max), e arrefece nos dias
    /// em que fica parada. Mudar de modelo é uma linha nova: começa outra vez em 1.</summary>
    public float Efficiency { get; set; } = 1f;
    /// <summary>Quantas unidades esta linha já entregou (a série que a torna eficiente).</summary>
    public int Delivered { get; set; }
}

public sealed class Country
{
    public int Id { get; init; }
    public string Tag { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsPlayer { get; set; }
    public int CapitalRegionId { get; set; }
    /// <summary>Características do país (tabela country_stat): industry, production_speed, org_regain, start_army_mult…</summary>
    public StatBlock Stats { get; } = new();
    /// <summary>Multiplicadores acumulados das tecnologias concluídas (tech_effect); World.ApplyTechs recalcula.</summary>
    public Dictionary<string, float> TechMult { get; } = new();
    /// <summary>Multiplicadores dos recursos controlados (ResourceSystem recalcula todos os dias).</summary>
    public Dictionary<string, float> ResourceMult { get; } = new();
    /// <summary>Multiplicadores dos edifícios nas regiões controladas (ConstructionSystem recalcula todos os dias).</summary>
    public Dictionary<string, float> BuildingMult { get; } = new();
    /// <summary>Multiplicadores das decisões nacionais activas (DecisionSystem recalcula todos os dias).</summary>
    public Dictionary<string, float> DecisionMult { get; } = new();
    /// <summary>Prisioneiros de guerra que este país detém: país de origem → homens. Trabalham para quem
    /// os guarda (PrisonerSystem) e voltam a casa quando se assina a paz.</summary>
    public Dictionary<int, int> Prisoners { get; } = new();
    /// <summary>Multiplicadores do trabalho dos prisioneiros (PrisonerSystem recalcula todos os dias).</summary>
    public Dictionary<string, float> PrisonerMult { get; } = new();
    /// <summary>Comandantes contratados (tabela general; HireGeneralCommand) e o que somam aos stats.</summary>
    public List<string> Generals { get; } = new();
    public Dictionary<string, float> GeneralMult { get; } = new();
    /// <summary>Gabinete civil em funções (AppointAdvisorCommand): pasta → conselheiro sentado nela.</summary>
    public Dictionary<string, string> Cabinet { get; } = new();
    /// <summary>Dia da nomeação de cada pasta, para o painel dizer há quanto tempo o homem lá está.</summary>
    public Dictionary<string, int> CabinetSince { get; } = new();
    /// <summary>Multiplicadores do gabinete civil (World.ApplyCabinet recalcula ao nomear, demitir ou carregar).</summary>
    public Dictionary<string, float> CabinetMult { get; } = new();
    /// <summary>Experiência de campanha de cada comandante contratado (GeneralXpSystem): sobe com as
    /// batalhas do grupo que ele comanda e nunca desce. Manda no posto — ver World.RankOf.</summary>
    public Dictionary<string, float> GeneralXp { get; } = new();
    /// <summary>Comandantes fora de serviço por ferimento (CommandCasualtySystem): general → dia em que
    /// regressa. Enquanto lá está não conta para os stats do país nem comanda exército nenhum.</summary>
    public Dictionary<string, int> GeneralWound { get; } = new();
    /// <summary>A gravidade que tirou cada comandante de serviço (wound_kind.id; s_general.wound_kind).
    /// Só a enfermaria a lê: o que ela muda no jogo são os dias, que estão no GeneralWound.</summary>
    public Dictionary<string, string> GeneralWoundKind { get; } = new();
    /// <summary>Fim do período de espera por decisão (dia; ActivateDecisionCommand).</summary>
    public Dictionary<string, int> DecisionCooldownUntil { get; } = new();
    /// <summary>Stat de país com fallback 1 (multiplicadores): sem linha na tabela = neutro. × tecnologias.</summary>
    public float Stat(string key, float fallback = 1f) =>
        (Stats.Has(key) ? Stats[key] : fallback) * (TechMult.TryGetValue(key, out var m) ? m : 1f)
        * (ResourceMult.TryGetValue(key, out var rm) ? rm : 1f) * (BuildingMult.TryGetValue(key, out var bm) ? bm : 1f) * (DecisionMult.TryGetValue(key, out var dm) ? dm : 1f) * (GeneralMult.TryGetValue(key, out var gm) ? gm : 1f) * (PrisonerMult.TryGetValue(key, out var pm) ? pm : 1f)
        * (CabinetMult.TryGetValue(key, out var cm) ? cm : 1f);
    /// <summary>Ranhuras de investigação ocupadas: tecnologia → dias acumulados (× research_speed). Quantas
    /// cabem é do ResearchSystem.Slots (regra research_slots × stat do país). Antes era uma só linha; um
    /// país industrial que investigasse infantaria não podia estar ao mesmo tempo a tratar de blindados,
    /// o que obrigava a escolhas que nenhum estado-maior faz — os laboratórios são vários.</summary>
    public Dictionary<string, float> Research { get; } = new();
    /// <summary>A primeira linha de investigação. Fachada sobre Research para o código antigo (espionagem,
    /// save legado, UI curta) continuar a falar de "a" investigação; escrever null larga tudo.</summary>
    public string? ResearchTech
    {
        get => Research.Keys.FirstOrDefault();
        set { if (value is null) Research.Clear(); else if (!Research.ContainsKey(value)) Research[value] = 0f; }
    }
    /// <summary>Dias acumulados na primeira linha.</summary>
    public float ResearchProgress
    {
        get => ResearchTech is string t ? Research[t] : 0f;
        set { if (ResearchTech is string t) Research[t] = value; }
    }
    public float Money { get; set; }               // pontos de produção acumulados (EconomySystem +, ProductionSystem −)
    public float Manpower { get; set; } = -1f;     // pool de homens (ManpowerSystem); -1 = por inicializar
    public float Stability { get; set; } = 50f;    // 0..100 (StabilitySystem); 50 = neutro
    public float PortCapacity { get; set; }        // divisões que os nossos cais aguentam (SupplySystem, derivado)
    public int SeaSupplied { get; set; }           // divisões que hoje só bebem por mar (SupplySystem, derivado)
    public float WarExhaustion { get; set; }       // 0..exhaustion_max: baixas acumuladas puxam a estabilidade para baixo
    /// <summary>Batalhas perdidas seguidas (DefeatAlarmSystem). Ganhar uma põe-na a zero; a partir da regra
    /// defeat_streak_alarm o país entra em alarme e o desgaste de guerra sobe.</summary>
    public int DefeatStreak { get; set; }
    /// <summary>Dia da última batalha perdida (-1 = ainda não perdeu nenhuma), para a faixa de avisos saber
    /// se a derrota ainda é fresca.</summary>
    public int LastDefeatDay { get; set; } = -1;
    /// <summary>Região onde se perdeu a última batalha: é para lá que o aviso leva o mapa.</summary>
    public int LastDefeatRegion { get; set; }
    public float AirPower { get; set; }            // esquadrões aéreos (BuyAirWingCommand); pesam no combate terrestre
    public float Warships { get; set; }            // navios de guerra (BuyWarshipCommand); destacam-se por NavalMissionSystem
    public float Convoys { get; set; }             // saldo de mercantes por cima da marinha de partida (ConvoySystem)
    /// <summary>Combustível em depósito (FuelSystem). Único número desta família que vai no save: os outros
    /// refazem-se todos os dias a partir do que o país controla.</summary>
    public float Fuel { get; set; }
    /// <summary>Refinado por dia, bebido por dia e o que o depósito aguenta — derivados, para a barra de topo
    /// e os avisos não terem de refazer a conta.</summary>
    public float FuelIn { get; set; }
    public float FuelUse { get; set; }
    public float FuelCap { get; set; }
    /// <summary>O depósito não chegou para o dia de hoje. É esta bandeira que põe `fuel_out` no contexto do
    /// combate, e daí em diante quem cobra é a tabela modifier.</summary>
    public bool FuelOut { get; set; }
    public int Nukes { get; set; }                 // ogivas prontas (BuildNukeCommand); NuclearStrikeCommand gasta uma
    /// <summary>Lei activa por grupo (grupo → law_id); grupos ausentes usam a lei is_default.</summary>
    public Dictionary<string, string> Laws { get; } = new();
    public int? JustifyTarget { get; set; }        // a justificar guerra contra (DiplomacySystem)
    public float JustifyProgress { get; set; }
    /// <summary>Efeito da estabilidade no rendimento e no recrutamento: 0.5 (colapso) a 1.5 (união nacional).</summary>
    public float StabilityFactor => 0.5f + Stability / 100f;
    public List<ProductionOrder> Queue { get; } = new();
    public HashSet<string> Techs { get; } = new();
    /// <summary>Doutrinas de exército adoptadas (tabela army_doctrine). Só de um ramo: a primeira escolha
    /// fecha as outras escolas. Não se largam — o que o exército aprendeu, aprendeu.</summary>
    public HashSet<string> Doctrines { get; } = new();
    /// <summary>Experiência de exército por gastar (ArmyXpSystem): junta-se em campanha e em manobras,
    /// paga-se com ela cada degrau de doutrina. Tecto na regra army_xp_max.</summary>
    public float ArmyXp { get; set; }
    /// <summary>Experiência de aviação por gastar (AirMissionSystem): junta-se com asas destacadas em missão
    /// e ganha-se depressa em céu disputado. Paga as escolas do ar. Tecto na regra air_xp_max.</summary>
    public float AirXp { get; set; }
    /// <summary>Experiência de marinha por gastar (NavalMissionSystem): junta-se com esquadras no mar e
    /// ganha-se depressa onde se afunda aço. Paga as escolas do mar. Tecto na regra navy_xp_max.</summary>
    public float NavyXp { get; set; }
    public string? CurrentFocus { get; set; }      // foco nacional em curso (FocusSystem)
    public float FocusProgress { get; set; }
    public HashSet<string> FocusesDone { get; } = new();
    public HashSet<int> AtWarWith { get; } = new();
    /// <summary>Capitulou (PeaceSystem): sem regiões nem exército; a IA ignora-o. Persistido em s_country.</summary>
    /// <summary>Nota de potência mundial e lugar na tabela (PowerRankingSystem, de power_rank_days em
    /// power_rank_days). PowerRankPrev guarda o lugar anterior para a UI mostrar quem subiu e quem desceu;
    /// 0 = ainda sem classificação.</summary>
    public float PowerScore { get; set; }
    public int PowerRank { get; set; }
    public int PowerRankPrev { get; set; }
    public bool Capitulated { get; set; }
    public int? CapitulatedDay { get; set; }
}

/// <summary>Estado mutável mínimo; stats vêm do cache por template.</summary>
/// <summary>Postura de um grupo de exércitos: parado, a marchar sobre a frente, a segurar a linha do
/// lado de cá dela, ou recolhido à retaguarda a recompor-se.</summary>
public enum GroupStance { Hold = 0, Advance = 1, Defend = 2, Reserve = 3 }

/// <summary>Grupo de exércitos: divisões sob um comando só, com uma frente atribuída (o país inimigo
/// contra quem marcham) e uma postura. Até aqui cada divisão era uma ordem à parte ou um avanço automático
/// cego para o vizinho mais fraco; um grupo com frente marcha o mapa todo até ao inimigo que lhe deram,
/// pelo caminho mais curto, e só lá chegando é que escolhe onde bater. Quem executa é o ArmyGroupSystem.</summary>
public sealed class ArmyGroup
{
    public int Id { get; init; }
    public int CountryId { get; init; }
    public string Name { get; set; } = "";
    /// <summary>País inimigo atribuído como frente; null = grupo sem missão (fica onde está).</summary>
    public int? FrontCountryId { get; set; }
    /// <summary>Troço da frente a que o grupo se dedica: uma região do inimigo (o Theatre.FacingId de um
    /// teatro escolhido no painel), ou null para a fronteira inteira com FrontCountryId. Uma guerra grande
    /// tem sempre mais do que um troço — sem isto o grupo espalhava-se por todos ao mesmo tempo, e nunca
    /// dava para mandar um exército inteiro reforçar só o troço que estava a ceder.</summary>
    public int? FrontRegionId { get; set; }
    /// <summary>O que o grupo faz com a frente que lhe deram (ArmyGroupSystem).</summary>
    public GroupStance Stance { get; set; } = GroupStance.Hold;
    public bool Advancing => Stance == GroupStance.Advance;
    /// <summary>Avançar, defender e recolher à reserva exigem frente (a reserva precisa dela para saber
    /// para que lado é a retaguarda); parado não faz nada.</summary>
    public bool NeedsFront => Stance != GroupStance.Hold;
    /// <summary>Em reserva: as divisões saem da linha e recompõem-se mais depressa (RecoverySystem).</summary>
    public bool Resting => Stance == GroupStance.Reserve;
    /// <summary>Comandante destacado para este grupo (id da tabela general), ou null. Enquanto comanda
    /// aqui, o bónus dele sai do país e vale só para estas divisões — amplificado por general_command_bonus.</summary>
    public string? GeneralId { get; set; }
    /// <summary>Preparação do plano de batalha (0..planning_max), à maneira do HoI4: um exército que fica
    /// parado na frente que lhe deram estuda o terreno, marca as estradas e cava — e quando avança, avança
    /// com isso feito. Sobe planning_per_day por dia em que ninguém do grupo marcha nem se bate, e gasta-se
    /// (planning_decay) na proporção das divisões que estão em movimento ou em combate.
    ///
    /// Só há plano com frente atribuída e postura de avançar ou defender: um grupo parado ou em reserva não
    /// tem plano nenhum. O que o plano vale em combate é o BattlePlanSystem.Bonus que diz. Vai ao save.</summary>
    public float Planning { get; set; }
    /// <summary>Membros. Escrever só por World.JoinGroup/LeaveGroup, que mantêm Division.GroupId em sintonia.</summary>
    public HashSet<int> Divisions { get; } = new();
}

public sealed class Division
{
    public int Id { get; init; }
    public int CountryId { get; init; }
    public int TemplateId { get; set; }
    public int RegionId { get; set; }
    public string? Name { get; set; }             // "Brigada Mecanizada"… (start_division.name / produção); null = nome do template
    public float Hp { get; set; } = 100f;
    public float Org { get; set; } = 100f;
    public float Supply { get; set; } = 1f;
    public float Xp { get; set; }                  // 0..xp_max: veterania ganha em combate (CombatSystem)
    /// <summary>Trincheira cavada nesta posição (0..entrench_max + fortes): sobe a cada dia parado, zera ao
    /// mudar de região e gasta-se a assaltar. Só conta a defender (EntrenchSystem).</summary>
    public float Entrench { get; set; }
    /// <summary>Cortada da retaguarda: não há cadeia de terra (nem cais) que a ligue a casa. Estado
    /// derivado — quem o escreve é o SupplySystem, todos os dias, e por isso não entra no save.</summary>
    public bool Cut { get; set; }
    /// <summary>Dias seguidos em cerco (PocketSystem). Zera assim que a ligação a casa volta; passado
    /// pocket_surrender dias fechada, a divisão rende-se.</summary>
    public int PocketDays { get; set; }
    /// <summary>Batalhas em que esteve e de que saiu viva (CombatSystem, ao fechar a batalha).</summary>
    public int Battles { get; set; }
    /// <summary>Regiões inimigas que tomou, por assalto ou entrando em região vazia.</summary>
    public int Captures { get; set; }
    /// <summary>Condecorações ganhas (ids da tabela medal); MedalSystem só acrescenta.</summary>
    public HashSet<string> Medals { get; } = new();
    /// <summary>Honra de batalha em vigor (id da tabela division_honour) ou null. Só há uma de cada vez:
    /// uma honra maior substitui a anterior (DivisionHonourSystem).</summary>
    public string? Honour { get; set; }
    /// <summary>Nome de guerra já resolvido ("Leões de Braga"): guarda-se feito porque a região que lhe deu
    /// o nome pode mudar de mãos ou desaparecer do mapa e a honra é da divisão, não da região.</summary>
    public string? HonourName { get; set; }
    /// <summary>Nome que a tropa usa: o nome próprio ganho em campanha, se houver.</summary>
    public string? WarName => HonourName is null ? Name : $"{Name} «{HonourName}»";
    public float MoveProgress { get; set; }        // 0..1 dentro do salto actual (MovementSystem)
    /// <summary>Ordem permanente de avanço (AutoFrontSystem): parada e sem combate, a divisão ataca
    /// sozinha a região inimiga vizinha mais fraca. Desliga-se ao dar uma ordem manual.</summary>
    public bool AutoAdvance { get; set; }
    /// <summary>Grupo de exércitos a que obedece, ou null. É o retrato inverso de ArmyGroup.Divisions e
    /// quem o mantém é World.JoinGroup/LeaveGroup — serve para saber num salto quem comanda esta divisão,
    /// sem varrer os grupos todos a cada golpe de combate.</summary>
    public int? GroupId { get; set; }
    /// <summary>Saltos restantes, do próximo ao destino. Vazio = parada.</summary>
    public List<int> Path { get; } = new();
    public int? TargetRegionId => Path.Count > 0 ? Path[0] : null;
    public int? DestinationRegionId => Path.Count > 0 ? Path[^1] : null;
    public bool CanFight => Org >= 10f && Hp > 0f;

    public void SetPath(IEnumerable<int> hops) { Path.Clear(); Path.AddRange(hops); MoveProgress = 0f; }
    public void ClearPath() { Path.Clear(); MoveProgress = 0f; }
    public void AdvanceHop() { if (Path.Count > 0) Path.RemoveAt(0); MoveProgress = 0f; }
}
