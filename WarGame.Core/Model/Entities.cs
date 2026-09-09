using WarGame.Core.Stats;

namespace WarGame.Core.Model;

public sealed record UnitType(int Id, string Name, string Category, float Cost, int BuildDays, float Mobility, float SupplyUse, StatBlock Stats);

/// <summary>O que um número da ficha de combate quer dizer (tabela unit_stat_def). Os números de uma
/// divisão já vinham todos de unit_stat, somados pelo DivisionStatCache; o que faltava era o nome que se lê
/// e a frase que diz o que aquilo faz na conta do combate. `Shown` a false deixa o número na tabela e fora
/// da ficha — é o sítio honesto para um stat que ainda não pesa em conta nenhuma.</summary>
public sealed record UnitStatDef(string Key, string Name, string Note, int Sort, string Glyph,
                                 int Digits, bool Percent, bool Shown);

/// <summary>Um terreno (tabela terrain). O `move_cost` já vivia nas regras como `move_cost:&lt;id&gt;` e
/// continua lá — isto é o resto da linha: o nome que se lê e a chapa que se desenha, que andavam a ser
/// buscados à static.db por três painéis cada um por sua conta.</summary>
public sealed record TerrainDef(string Id, string Name, float MoveCost, string Glyph, string Color = "");

/// <summary>Uma zona estratégica (tabela zone): o pedaço de mundo com nome onde a guerra do ar e a do mar
/// acontecem. Kind = "terra" (zona aérea, gerada do Natural Earth) ou "mar" (zona naval, semeada à mão com
/// a caixa de lat/lon do mar). Nada disto se decide em código: uma zona nova é uma linha de SQL.</summary>
public sealed record ZoneDef(string Id, string Name, string Kind, string Color, string Glyph, int Sort);

/// <summary>Uma cidade do mapa (tabela city, do Natural Earth): nome, ponto já projectado, gente e se é
/// capital de país. Não entra na simulação — quem tem contas é a região; a cidade é o que se lê no mapa.</summary>
public sealed record CityDef(int Id, int RegionId, string Name, int Population, bool Capital, float X, float Y);

public sealed record DivisionTemplate(int Id, int CountryId, string Name, IReadOnlyList<(int UnitTypeId, int Qty)> Units);

/// <summary>Espírito nacional (tabela national_spirit); os efeitos são linhas modifier com SpiritId.</summary>
/// <summary>Tecnologia (tabela tech). Cost = dias com research_speed 1; Requires = id da anterior no ramo.</summary>
public sealed record Tech(string Id, string Branch, string Name, float Cost, string? Requires, string? Description,
                          string? CountryTag = null);
/// <summary>Evento noticioso (tabela news_event); CountryId null = global. Há dois feitios: os de data
/// marcada, que caem no dia (Day), e os de estado, que trazem uma sonda (Watch, do WorldWatch) e ficam à
/// espera que o mundo faça alguma coisa — a guerra começar, a capital cair, a revolta pegar. Arg é o
/// número que a sonda compara. Glyph é a estampa do cartão, Tone a cor (bom|mau|neutro) e Pause diz se
/// isto merece parar o relógio e ocupar o ecrã todo.</summary>
public sealed record NewsEvent(string Id, int Day, int? CountryId, string Title, string Body,
                               string Watch = "", float Arg = 0f, string Glyph = "", string Tone = "neutro",
                               bool Pause = false)
{
    /// <summary>Evento de estado: não tem data, espera pelo mundo.</summary>
    public bool IsWatch => Watch.Length > 0;
}
/// <summary>Escolha de um evento noticioso (news_event_option). A IA fica com a primeira (sort).</summary>
public sealed record NewsOption(string Id, string EventId, string Title, int Sort);
/// <summary>Lei nacional (tabela law): grupos (conscrição, economia…) com uma lei activa por grupo.
/// Sort maior = mais mobilizada (a IA escala em guerra). Efeitos em law_effect.</summary>
/// <summary>Lei nacional (tabela law). CountryTag null = lei de toda a gente; com tag, só esse país a tem
/// na escada — é assim que cada país ganha a sua questão nacional sem a emprestar aos vizinhos.</summary>
public sealed record Law(string Id, string Group, string Name, string Description, int Sort, bool IsDefault,
                         string? CountryTag = null, float MinTension = 0f);

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
    /// <summary>Nível da via férrea desta região (0..rail_max). Cada nível faz o salto da rede de
    /// abastecimento custar menos (rail_step) e desenha-se no mapa como linha de comboio. −1 = ainda por
    /// derivar da infraestrutura de origem (World.SeedRails).</summary>
    public int Rail { get; set; } = -1;
    public bool RailBuilding { get; set; }
    public float RailProgress { get; set; }
    /// <summary>O carril com que esta região nasceu (World.SeedRails). Serve ao save para só guardar as
    /// linhas que alguém mudou — o mundo tem quase 3000 regiões e a rede de origem é sempre a mesma.</summary>
    public int BaseRail { get; set; } = -1;
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
    /// <summary>Quem mandou levantar o edifício em curso. Só interessa aos depósitos, que se constroem em
    /// terra tomada: se a região mudar de mãos a meio da obra, ela morre com quem a pagou.</summary>
    public int ProjectOwner { get; set; }
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
    /// <summary>Latitude do centróide em graus (tabela region): positiva a norte, negativa a sul. O CenterY
    /// vai projectado em Robinson e não se desprojecta — quem quer saber o frio desta terra vem aqui (Weather).</summary>
    public float Lat { get; init; }
    public List<int> Neighbours { get; init; } = new();
    /// <summary>Ligações marítimas (sea_link): região costeira → km da travessia. Vazio = interior.</summary>
    public Dictionary<int, float> SeaNeighbours { get; init; } = new();
    public bool Coastal { get; init; }
    /// <summary>Zona estratégica de terra (tabela zone, kind='terra'): o céu desta região disputa-se ao
    /// nível da zona, não da província. "" = mundo de teste sem zonas — cada região é o seu próprio céu.</summary>
    public string ZoneId { get; init; } = "";
    /// <summary>Mar em frente a esta costa (tabela zone, kind='mar'); "" no interior. O bloqueio, a escolta
    /// e a patrulha valem para a zona inteira: fechar um mar fecha todos os cais que lá dão.</summary>
    public string SeaZoneId { get; init; } = "";
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
public sealed record SeasonDef(string Id, string Name, string Icon, float MoveMult, float OrgMult, float Attrition, string Note,
                               string Glyph = "", float Cold = 0f);

/// <summary>Um céu possível (tabela weather; Weather). A estação é o ano inteiro e o mundo inteiro; isto é a
/// semana e a região: ColdMin/ColdMax é a faixa de frio em que este céu aparece (0 trópico, 1 o círculo polar
/// em pleno Inverno), Terrain vazio serve qualquer chão e Weight é o peso no sorteio. MoveMult atrasa a
/// marcha, OrgMult a recomposição e AirMult o que a aviação consegue fazer; o que o céu faz ao assalto está
/// na tabela modifier (condition_key 'weather'), ao lado do terreno e do rio.</summary>
public sealed record WeatherDef(string Id, string Name, string Icon, float MoveMult, float OrgMult, float AirMult,
                                float ColdMin, float ColdMax, string Terrain, float Weight, string Note, int Sort,
                                string Glyph = "");

/// <summary>Um degrau de vassalagem (tabela subject_type; Subjects). AutonomyMin é a autonomia a partir da
/// qual o vassalo está neste degrau, YieldShare e ManpowerShare o que o suserano lhe leva por dia e Drift a
/// autonomia que ele ganha por dia aqui — quanto mais solto, mais depressa se solta.</summary>
public sealed record SubjectTypeDef(string Id, string Name, string Icon, float AutonomyMin, float YieldShare,
                                    float ManpowerShare, float Drift, string Note, int Sort, string Glyph = "");

/// <summary>Um grau de ponto de vitória (tabela victory_tier; VictoryPoints). MinPop é a população a partir
/// da qual a região é deste grau, Capital marca o grau que só a capital do país tem — e que ganha sempre ao
/// que a população dela daria. O grau não se guarda: lê-se da região, por isso nunca a contradiz.</summary>
public sealed record VictoryTierDef(string Id, string Name, string Icon, int Points, long MinPop, bool Capital,
                                    string Note, int Sort, string Glyph = "", string Shape = "circulo");

/// <summary>Um grau de veterania (tabela veterancy; Veterancy). MinXp é a experiência a partir da qual a
/// divisão é deste grau, Bonus a força extra que ele lhe dá e Chevrons os galões que o contador do mapa
/// desenha. O grau não se guarda: lê-se do Xp da divisão, por isso nunca contradiz o que o combate escreveu.</summary>
public sealed record VeterancyDef(string Id, string Name, string Icon, float MinXp, float Bonus, int Chevrons,
                                  string Note, int Sort, string Glyph = "");

/// <summary>Uma táctica de combate (tabela tactic; Tactics). Side diz quem a pode escolher ('attacker' ou
/// 'defender'), Mult o que ela vale à força desse lado, CounterId a táctica INIMIGA que esta lê e desmonta,
/// Terrain vazio serve qualquer chão e Weight é o peso no sorteio. É o pedra-papel-tesoura que o HoI4 põe
/// por cima da soma das fichas: quem é lido fica com tactic_counter_keep do que a sua valia acima de 1.</summary>
public sealed record TacticDef(string Id, string Name, string Icon, string Side, float Mult, string CounterId,
                               string Terrain, float Weight, string Note, int Sort, string Glyph = "");

/// <summary>Género de acontecimento da crónica (tabela chronicle_kind): o ícone com que aparece na linha do
/// tempo e o peso (1 = rotina, 3 = história). A regra chronicle_min_weight decide o que chega a ser escrito —
/// mudar o que a campanha lembra é mudar uma linha de SQL, não o código.</summary>
public sealed record ChronicleKind(string Id, string Name, string Icon, int Weight, string Glyph = "");

/// <summary>Uma entrada da crónica da campanha (ChronicleSystem). Guarda-se no save: o Jornal morria com a
/// sessão e a guerra de há dois anos ficava sem memória nenhuma.</summary>
public sealed record ChronicleEntry(int Day, string Kind, string Text, int CountryId, int RegionId);

/// <summary>Um modo de mapa (tabela map_mode): como o mapa se pinta e o que a legenda diz nas duas pontas.
/// Métrica "owner" é o mapa político de sempre — cor do controlador, sem escala.</summary>
public sealed record MapModeDef(string Id, string Name, string Icon, string Metric, string Low, string High, int Sort,
                                string Glyph = "");

/// <param name="Yard">Fila de fábricas que este edifício alimenta (coluna building.yard): "civil", "militar",
/// "naval" ou vazio. É o que liga um edifício aos contadores do Industry.</param>
/// <param name="Icon">Desenho do edifício na lista do Construir (coluna building.icon). Vem da tabela e não
/// do código pela mesma razão que o resto: um edifício novo é uma linha de SQL, não uma linha de C#.</param>
public sealed record BuildingDef(string Id, string Name, float Cost, float Days, string StatKey, float PerLevel, int MaxLevel,
    bool Coastal = false, float SupplyRange = 0f, float HubRange = 0f, string Yard = "", string Icon = "", string Glyph = "")
{
    /// <summary>Depósito: irradia rede à sua volta. É o que o distingue de uma fábrica — e o que lhe dá o
    /// direito de se levantar em terra tomada, que nenhuma outra obra tem.</summary>
    public bool IsHub => HubRange > 0f;
}

/// <summary>Ramo da árvore de investigação (tabela tech_branch). O id é o texto que está em tech.branch; o
/// Glyph é o nome de uma chapa desenhada — qual chapa cabe a que ramo é dado, não é decidido em código.</summary>
public sealed record TechBranchDef(string Id, string Name, string Glyph, int Sort);

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
public sealed record CabinetSlotDef(string Id, string Name, string Icon, int Sort, string Glyph = "");

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
                               string? Domain = null, string Glyph = "");

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
                                         float Manpower, string Note, int Sort, string Glyph = "");

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
                                 float FuelPerUnit = 0f, string Glyph = "caixa");

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
public sealed record AirMissionDef(string Id, string Name, string Icon, string Effect, float Value, string Note, int Sort,
                                   string Glyph = "");

/// <summary>Um esquadrão destacado para uma região (AirMissionSystem; save s_air_mission). Wings são asas
/// do pool nacional (Country.AirPower) que ficam presas a esta missão até serem chamadas de volta — ou até
/// serem abatidas no céu de lá.</summary>
public sealed class AirMission
{
    public int CountryId { get; init; }
    public int RegionId { get; init; }
    public string MissionId { get; init; } = "";
    /// <summary>Que aviões é que esta asa levou, por modelo (save s_air_mission_plane). O modelo vazio é o
    /// avião sem modelo: um mundo sem plane_class nenhuma continua a voar exactamente como antes.</summary>
    public Dictionary<string, float> Squadron { get; } = new();
    /// <summary>Asas desta missão ao todo. Escrever aqui reparte pela composição que já lá está — é como o
    /// resto do jogo continua a contar asas sem ter de saber de modelos.</summary>
    public float Wings
    {
        get { float t = 0f; foreach (var n in Squadron.Values) t += n; return t; }
        set
        {
            float now = Wings;
            if (value <= 0f) { Squadron.Clear(); return; }
            if (now <= 0.0001f) { Squadron.Clear(); Squadron[""] = value; return; }
            float k = value / now;
            foreach (var id in Squadron.Keys.ToList()) Squadron[id] *= k;
        }
    }
    public int SinceDay { get; init; }
    /// <summary>Nome próprio da asa (tabela formation_name; save s_air_mission.name). Muda de tarefa sem
    /// mudar de nome: quem está no céu de uma região é sempre a mesma gente. "" = save antigo, sem nome.</summary>
    public string Name { get; set; } = "";
}

/// <summary>Tipo de missão naval (tabela naval_mission): o que uma esquadra vai fazer ao mar de uma costa.
/// Effect diz qual dos três papéis é — "blockade" (fechar o mar àquela costa), "escort" (acompanhar os
/// nossos comboios e desfazer o bloqueio) ou "patrol" (vigiar aquele mar e tirar a costa do nevoeiro).
/// Trocar o que a marinha faz é trocar linhas desta tabela.</summary>
public sealed record NavalMissionDef(string Id, string Name, string Icon, string Effect, float Value, string Note, int Sort,
                                     string Glyph = "");

/// <summary>Uma classe de navio (tabela ship_class; Navy). A marinha do jogo era um número só: agora cada
/// casco tem classe e a classe decide para que serve. Screen é a couraça — quem tem screen leva os tiros
/// primeiro e poupa a linha; Battle é o que pesa no combate; Blockade/Escort/Patrol é quanto vale em cada
/// tarefa naval. Nada disto está em código: são linhas da tabela.</summary>
public sealed record ShipClassDef(string Id, string Name, string Icon, string Role, float Cost, float Upkeep,
                                  float Battle, float Screen, float Blockade, float Escort, float Patrol,
                                  bool Basic, string Note, int Sort, string Glyph);

/// <summary>Um modelo de avião (tabela plane_class; Air). O céu do jogo era um número só: agora cada asa
/// tem modelo e o modelo decide para que serve. Air é o que ele vale num combate aéreo — e é também o que
/// o salva do abate, porque quem não sabe lutar no ar é o primeiro a cair; Superiority/Support/Bombing/
/// Transport é quanto rende em cada tarefa. Nada disto está em código: são linhas da tabela.</summary>
public sealed record PlaneClassDef(string Id, string Name, string Icon, string Role, float Cost, float Upkeep,
                                   float Air, float Superiority, float Support, float Bombing, float Transport,
                                   bool Basic, string Note, int Sort, string Glyph);

/// <summary>Uma geração de material (tabela equipment_mark; Marks). O armazém tinha uma espingarda só: um
/// conjunto valia sempre o mesmo, e investigar não mudava o que a tropa levava ao ombro. Agora cada tipo de
/// unidade tem marcas, cada marca abre-se com uma tecnologia (TechId vazio = a de origem, que todos têm) e
/// custa mais (Cost), vale mais em combate (Power) e gasta-se menos (Wear). Nada disto está em código.</summary>
public sealed record EquipmentMarkDef(string Id, int UnitTypeId, int Mark, string Name, string TechId,
                                      float Cost, float Power, float Wear, string Note, string Glyph);

/// <summary>Uma esquadra destacada para o mar de uma região costeira (NavalMissionSystem; save
/// s_naval_mission). Ships são navios do pool nacional (Country.Warships) que ficam presos a esta missão
/// até serem chamados de volta — ou até irem ao fundo naquele mar.</summary>
public sealed class NavalMission
{
    public int CountryId { get; init; }
    public int RegionId { get; init; }
    public string MissionId { get; init; } = "";
    /// <summary>Que cascos é que esta esquadra levou, por classe (save s_naval_mission_ship). A classe vazia
    /// é o navio sem classe: um mundo sem ship_class nenhuma continua a lutar como antes.</summary>
    public Dictionary<string, float> Squadron { get; } = new();
    /// <summary>Navios desta esquadra ao todo. Escrever aqui reparte pela composição que já lá está — é como
    /// o resto do jogo continua a somar navios sem ter de saber de classes.</summary>
    public float Ships
    {
        get { float t = 0f; foreach (var n in Squadron.Values) t += n; return t; }
        set
        {
            float now = Ships;
            if (value <= 0f) { Squadron.Clear(); return; }
            if (now <= 0.0001f) { Squadron.Clear(); Squadron[""] = value; return; }
            float k = value / now;
            foreach (var id in Squadron.Keys.ToList()) Squadron[id] *= k;
        }
    }
    public int SinceDay { get; init; }
    /// <summary>Nome próprio da esquadra (tabela formation_name; save s_naval_mission.name).</summary>
    public string Name { get; set; } = "";
}

/// <summary>Uma operação anfíbia a preparar (NavalInvasionSystem; save s_naval_invasion). Marca a praia
/// inimiga, a costa nossa de embarque e a tropa que vai — e conta os dias até estar pronta. Enquanto
/// prepara, a tropa fica no cais: não marcha nem aceita ordens de marcha.</summary>
public sealed class NavalInvasion
{
    public int CountryId { get; init; }
    public int TargetId { get; init; }
    public int FromId { get; init; }
    /// <summary>Preparação feita, 0..1. A 1 a operação larga assim que houver mar e mercantes.</summary>
    public float Prep { get; set; }
    public int SinceDay { get; init; }
    /// <summary>Nome próprio da operação (formation_name), como as asas e as esquadras têm.</summary>
    public string Name { get; set; } = "";
    /// <summary>A tropa embarcada, por id de divisão (save s_naval_invasion_division).</summary>
    public List<int> DivisionIds { get; } = new();
}

/// <summary>Uma encomenda na fila: divisão inteira de um template. Progress em pontos gastos.</summary>
public sealed class ProductionOrder
{
    public int TemplateId { get; init; }
    /// <summary>Linha de material (HoI4: linha de produção de equipamento) em vez de divisão: > 0 = esta
    /// linha não monta divisão nenhuma, fabrica conjuntos de material daquele tipo de unidade e mete-os no
    /// armazém do país (Country.Stock). É de lá que saem os reforços das divisões gastas — sem armazém, uma
    /// divisão batida fica batida. Zero = encomenda de divisão, como sempre foi.</summary>
    public int UnitTypeId { get; init; }
    public bool IsKit => UnitTypeId > 0;
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
    /// <summary>A marca de material que esta linha está a fazer (Marks). Zero = ainda não pegou em nenhuma;
    /// a linha assume sozinha a melhor que o país tenha aberta e, quando a tecnologia abre a seguinte,
    /// reafina-se: perde ritmo (mark_switch_efficiency) e passa a fazer material melhor.</summary>
    public float Mark { get; set; }
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
    /// <summary>Poder político (PoliticsSystem +): a moeda da política, que nunca se converte em aço.
    /// Paga leis, gabinete, decisões, pactos e justificações de guerra — as coisas que no HoI4 não se
    /// compram com fábricas. Tecto na regra political_max; o que não se gasta perde-se.</summary>
    public float Political { get; set; }
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
    /// <summary>A aviação por modelo de avião (plane_class; save s_plane): 'caca' → 12, 'estrategico' → 3.
    /// O modelo vazio é o avião sem modelo — a asa genérica dos saves antigos e dos mundos de teste, que
    /// vale 1 em tudo. Escreve-se por aqui ou pelo AirPower, nunca pelos dois ao mesmo tempo.</summary>
    public Dictionary<string, float> Planes { get; } = new();
    /// <summary>Esquadrões aéreos ao todo (soma dos modelos). Continua a ser o pool nacional que se destaca
    /// para o céu e que pesa no combate terrestre; pôr um número aqui reparte-o pelos modelos que o país já
    /// tem (ou faz asas sem modelo, se ainda não tem nenhum), para os saves velhos e os mundos de teste não
    /// terem de saber de modelos.</summary>
    public float AirPower
    {
        get { float t = 0f; foreach (var n in Planes.Values) t += n; return t; }
        set
        {
            float now = AirPower;
            if (value <= 0f) { Planes.Clear(); return; }
            if (now <= 0.0001f) { Planes.Clear(); Planes[""] = value; return; }
            float k = value / now;
            foreach (var id in Planes.Keys.ToList()) Planes[id] *= k;
        }
    }
    /// <summary>A marinha por classe de casco (ship_class; save s_ship): 'destroier' → 12, 'submarino' → 4.
    /// A classe vazia é o navio sem classe — o casco genérico dos saves antigos e dos mundos de teste, que
    /// vale 1 em tudo. Escreve-se por aqui ou pelo Warships, nunca pelos dois ao mesmo tempo.</summary>
    public Dictionary<string, float> Ships { get; } = new();
    /// <summary>Navios de guerra ao todo (soma das classes). Continua a ser o pool nacional que se destaca
    /// para o mar; pôr um número aqui reparte-o pelas classes que o país já tem (ou faz cascos sem classe,
    /// se ainda não tem nenhuma), para os saves velhos e os mundos de teste não terem de saber de classes.</summary>
    public float Warships
    {
        get { float t = 0f; foreach (var n in Ships.Values) t += n; return t; }
        set
        {
            float now = Warships;
            if (value <= 0f) { Ships.Clear(); return; }
            if (now <= 0.0001f) { Ships.Clear(); Ships[""] = value; return; }
            float k = value / now;
            foreach (var id in Ships.Keys.ToList()) Ships[id] *= k;
        }
    }
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
    /// <summary>Armazém de material (HoI4: stockpile): tipo de unidade → conjuntos de material em depósito.
    /// Um conjunto é o que arma um batalhão daquele tipo. As fábricas enchem-no (linhas de material e as
    /// fábricas que sobram sem encomenda), as divisões gastas esvaziam-no (EquipmentSystem). Fracções contam:
    /// meio conjunto é meia divisão reforçada amanhã.</summary>
    public Dictionary<int, float> Stock { get; } = new();
    /// <summary>Material em armazém daquele tipo (0 se nunca lá houve nenhum).</summary>
    public float Stocked(int unitTypeId) => Stock.TryGetValue(unitTypeId, out var q) ? q : 0f;
    /// <summary>A marca média do que está na prateleira, por tipo (Marks). Uma prateleira é uma pilha
    /// misturada: chega material novo e a média sobe, gasta-se e a média fica onde estava. Zero = nunca lá
    /// entrou nada com marca (mundo sem tabela de marcas, ou save antigo).</summary>
    public Dictionary<int, float> StockMark { get; } = new();
    /// <summary>A marca média do material daquele tipo em armazém.</summary>
    public float StockedMark(int unitTypeId) => StockMark.TryGetValue(unitTypeId, out var m) ? m : 0f;
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
    /// <summary>País que acolhe o governo no exílio, ou null — capitulado sem anfitrião é um governo que
    /// acabou de vez. Enquanto lá está não tem terra nem exército: o que tem é a legitimidade
    /// (ExileLegitimacy, 0..1), que sobe enquanto o anfitrião se bate contra quem o derrubou e é o que lhe
    /// permite voltar quando a capital for libertada. Ver ExileSystem; persiste em s_exile.</summary>
    public int? ExileHostId { get; set; }
    public int? ExileDay { get; set; }
    public float ExileLegitimacy { get; set; }
    /// <summary>Está no exílio: capitulou e ainda tem quem o acolha.</summary>
    public bool InExile => Capitulated && ExileHostId is not null;

    /// <summary>Suserano deste país, ou 0 se é livre (SubjectSystem). Um vassalo continua a ser país — tem
    /// bandeira, terra e exército — mas paga ao suserano parte do que rende e dos homens que recruta.</summary>
    public int OverlordId { get; set; }
    /// <summary>Autonomia do vassalo, 0..subject_free_autonomy. Sobe todos os dias (mais depressa quanto
    /// mais solto ele já está, e mais ainda enquanto se bate); ao chegar ao topo, levanta-se e sai.
    /// O degrau em que ele está (protectorado, satélite, domínio) NÃO se guarda: sai daqui pela tabela
    /// subject_type — ver Subjects.Level.</summary>
    public float Autonomy { get; set; }
    /// <summary>É vassalo de alguém.</summary>
    public bool IsSubject => OverlordId != 0;
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
    /// <summary>Bandeira sob a qual esta divisão se bate hoje. Normalmente é a de casa e não muda em toda a
    /// vida da divisão; muda quando ela vai como voluntária para a guerra de outro (VolunteerSystem) e volta
    /// a mudar quando é chamada de volta. É por isto que não é `init`: o mundo tem uma forma legítima de a
    /// emprestar, e todo o resto do jogo — mapa, frentes, combate, abastecimento — só tem de olhar para
    /// quem ela obedece hoje, sem saber nada de voluntários.</summary>
    public int CountryId { get; set; }
    /// <summary>Casa, quando anda emprestada: o país que a criou e a quem ela volta. null = está em casa.
    /// Os homens e os reforços saem sempre daqui, mesmo com ela a combater por outro (RecoverySystem).</summary>
    public int? VolunteerFrom { get; set; }
    /// <summary>Quem paga esta divisão: a casa, ande ela onde andar.</summary>
    public int HomeId => VolunteerFrom ?? CountryId;
    public bool IsVolunteer => VolunteerFrom is not null;
    public int TemplateId { get; set; }
    public int RegionId { get; set; }
    public string? Name { get; set; }             // "Brigada Mecanizada"… (start_division.name / produção); null = nome do template
    public float Hp { get; set; } = 100f;
    public float Org { get; set; } = 100f;
    public float Supply { get; set; } = 1f;
    /// <summary>Quanto do material que o modelo pede é que esta divisão tem hoje (0..1). Sai da fábrica
    /// armada de raiz (1); o combate destrói equipamento e o número desce; o armazém do país repõe-no todos
    /// os dias, se lá houver material (EquipmentSystem). É o número do HoI4 que explica porque é que uma
    /// divisão inteira de homens se bate mal: os homens voltaram, as armas não.</summary>
    public float Kit { get; set; } = 1f;
    /// <summary>A marca do material com que esta divisão se bate hoje (Marks). Não é o que o país já sabe
    /// fabricar: é o que lhe chegou às mãos. Uma divisão só sobe de marca quando recebe reforços da
    /// prateleira nova, e por isso a tropa da frente anda sempre uma geração atrás do laboratório.</summary>
    public float Mark { get; set; }
    public float Xp { get; set; }                  // 0..xp_max: veterania ganha em combate (CombatSystem)
    /// <summary>Trincheira cavada nesta posição (0..entrench_max + fortes): sobe a cada dia parado, zera ao
    /// mudar de região e gasta-se a assaltar. Só conta a defender (EntrenchSystem).</summary>
    public float Entrench { get; set; }
    /// <summary>Cortada da retaguarda: não há cadeia de terra (nem cais) que a ligue a casa. Estado
    /// derivado — quem o escreve é o SupplySystem, todos os dias, e por isso não entra no save.</summary>
    public bool Cut { get; set; }
    /// <summary>Distância a que ficou da rede de abastecimento, em regiões tomadas (as estradas de cada uma
    /// pesam no que custa atravessá-la). 0 = em casa ou num cais. Derivado como o Cut: SupplySystem.</summary>
    public float SupplyDepth { get; set; }
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
    /// <summary>Região onde esta divisão vai saltar de pára-quedas, ou null se está em terra
    /// (ParadropSystem; save s_division.drop_target). Enquanto voa continua marcada na região de partida —
    /// é de lá que os aviões levantam — mas não marcha nem recebe ordens de marcha.</summary>
    public int? DropTargetId { get; set; }
    /// <summary>Dias que faltam em voo até à aterragem (save s_division.drop_days). Zero = em terra.</summary>
    public float DropDays { get; set; }
    /// <summary>Vai a caminho do salto: nem marcha, nem se lhe muda o destino.</summary>
    public bool InFlight => DropDays > 0f;
    /// <summary>Vai numa operação anfíbia largada hoje (NavalInvasionSystem; save s_division.seaborne). É a
    /// única maneira de assaltar uma praia inimiga: sem esta marca, uma divisão que chega ao mar pára na
    /// costa. Apaga-se ao pisar terra, seja a tomá-la ou a bater à porta.</summary>
    public bool Seaborne { get; set; }
    /// <summary>Vai em redespacho estratégico: atravessa a retaguarda pelos carris em vez de marchar
    /// (MovementSystem; save s_division.redeploy). Anda muito mais depressa, paga organização ao embarcar e
    /// quase não se recompõe pelo caminho — e o comboio pára sozinho se a frente lhe cortar a linha.</summary>
    public bool Redeploying { get; set; }
    /// <summary>Saltos restantes, do próximo ao destino. Vazio = parada.</summary>
    public List<int> Path { get; } = new();
    public int? TargetRegionId => Path.Count > 0 ? Path[0] : null;
    public int? DestinationRegionId => Path.Count > 0 ? Path[^1] : null;
    public bool CanFight => Org >= 10f && Hp > 0f;

    // A marca do assalto é um bilhete de uma viagem só: qualquer ordem nova apaga-a, e quem a quer põe-na
    // depois de traçar a rota (NavalInvasionSystem.Launch). Sem isto, uma operação largada uma vez dava
    // assaltos de graça para sempre.
    public void SetPath(IEnumerable<int> hops) { Path.Clear(); Path.AddRange(hops); MoveProgress = 0f; Seaborne = false; }
    public void ClearPath() { Path.Clear(); MoveProgress = 0f; Seaborne = false; }
    public void AdvanceHop() { if (Path.Count > 0) Path.RemoveAt(0); MoveProgress = 0f; }
}
