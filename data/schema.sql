-- ================= STATIC (static.db, só leitura, no APK) =================
CREATE TABLE IF NOT EXISTS unit_type (
  id INTEGER PRIMARY KEY, name TEXT NOT NULL, category TEXT NOT NULL,
  cost REAL NOT NULL DEFAULT 1, build_days INTEGER NOT NULL DEFAULT 30,
  supply REAL NOT NULL DEFAULT 1, mobility REAL NOT NULL DEFAULT 25
);
CREATE TABLE IF NOT EXISTS unit_stat (
  unit_type_id INTEGER NOT NULL REFERENCES unit_type(id),
  stat_key TEXT NOT NULL, value REAL NOT NULL,
  PRIMARY KEY (unit_type_id, stat_key)
);
CREATE TABLE IF NOT EXISTS unit_tag (
  unit_type_id INTEGER NOT NULL REFERENCES unit_type(id), tag TEXT NOT NULL,
  PRIMARY KEY (unit_type_id, tag)
);
CREATE TABLE IF NOT EXISTS modifier (
  id INTEGER PRIMARY KEY, source_kind TEXT NOT NULL,
  condition_key TEXT, condition_value TEXT,
  stat_key TEXT NOT NULL, required_tag TEXT,
  op TEXT NOT NULL CHECK (op IN ('add','mul')), value REAL NOT NULL,
  country_tag TEXT,                          -- NULL = todos os países; senão só divisões desse país
  spirit_id TEXT                             -- espírito nacional a que pertence (só para a UI agrupar)
);
CREATE TABLE IF NOT EXISTS terrain (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, color TEXT,
  move_cost REAL NOT NULL DEFAULT 1          -- multiplicador de dias para entrar na região
);
-- Constantes de jogo (economia, movimento, IA…). Nenhuma em código: World.Rules lê daqui.
CREATE TABLE IF NOT EXISTS rule (key TEXT PRIMARY KEY, value REAL NOT NULL, note TEXT);
CREATE TABLE IF NOT EXISTS country (
  id INTEGER PRIMARY KEY, tag TEXT UNIQUE NOT NULL, name TEXT NOT NULL, color TEXT,
  capital_region_id INTEGER,                 -- onde nascem as divisões produzidas (seed_armies.py)
  name_en TEXT, gdp_md REAL                  -- Natural Earth: NAME / GDP_MD (name = NAME_PT)
);
-- ===== Características únicas por país (data/countries/*.sql; ver tools/check_countries.py) =====
CREATE TABLE IF NOT EXISTS country_stat (        -- Country.Stats: industry, production_speed, org_regain, start_army_mult…
  country_tag TEXT NOT NULL, key TEXT NOT NULL, value REAL NOT NULL, PRIMARY KEY (country_tag, key)
);
CREATE TABLE IF NOT EXISTS country_info (        -- texto para o painel do país
  country_tag TEXT PRIMARY KEY, government TEXT, leader TEXT, doctrine TEXT, alliance TEXT, description TEXT
);
CREATE TABLE IF NOT EXISTS national_spirit (     -- espíritos nacionais (HoI4); efeitos = linhas modifier com spirit_id
  id TEXT PRIMARY KEY, country_tag TEXT NOT NULL, name TEXT NOT NULL, description TEXT
);
CREATE TABLE IF NOT EXISTS faction (             -- aliança defensiva (HoI4: facção); World.Factions
  id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT
);
CREATE TABLE IF NOT EXISTS faction_member (      -- um país pode estar em várias facções
  faction_id TEXT NOT NULL REFERENCES faction(id), country_tag TEXT NOT NULL,
  PRIMARY KEY (faction_id, country_tag)
);
CREATE TABLE IF NOT EXISTS country_template (    -- templates próprios do país (seed_armies cria template/template_unit)
  id INTEGER PRIMARY KEY, country_tag TEXT NOT NULL, name TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS country_template_unit (
  country_template_id INTEGER NOT NULL REFERENCES country_template(id), unit_type_id INTEGER NOT NULL, qty INTEGER NOT NULL,
  PRIMARY KEY (country_template_id, unit_type_id)
);
CREATE TABLE IF NOT EXISTS country_unit (        -- exército inicial nomeado (brigadas reais); o resto é gerado
  id INTEGER PRIMARY KEY, country_tag TEXT NOT NULL, name TEXT NOT NULL,
  template_name TEXT NOT NULL,                   -- nome de um template genérico ou de country_template desse país
  region_name TEXT                               -- região (nome) onde começa; NULL/desconhecida = capital
);
CREATE TABLE IF NOT EXISTS region (
  id INTEGER PRIMARY KEY, name TEXT NOT NULL, owner_id INTEGER REFERENCES country(id),
  terrain TEXT NOT NULL REFERENCES terrain(id), river INTEGER NOT NULL DEFAULT 0,
  population INTEGER NOT NULL DEFAULT 0, infrastructure REAL NOT NULL DEFAULT 1,
  centroid_x REAL, centroid_y REAL, coastal INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS region_polygon (   -- anéis exteriores, float32 x,y já projectados (Robinson, y para baixo)
  region_id INTEGER NOT NULL REFERENCES region(id), ring_index INTEGER NOT NULL, points BLOB NOT NULL,
  PRIMARY KEY (region_id, ring_index)
);
CREATE TABLE IF NOT EXISTS region_neighbour (
  region_id INTEGER NOT NULL REFERENCES region(id), neighbour_id INTEGER NOT NULL REFERENCES region(id),
  PRIMARY KEY (region_id, neighbour_id)
);
CREATE TABLE IF NOT EXISTS sea_link (         -- ligação marítima entre costeiras (uma linha por par, km reais)
  region_id INTEGER NOT NULL REFERENCES region(id), neighbour_id INTEGER NOT NULL REFERENCES region(id),
  km REAL NOT NULL,
  PRIMARY KEY (region_id, neighbour_id)
);
CREATE TABLE IF NOT EXISTS region_resource (
  region_id INTEGER NOT NULL REFERENCES region(id), resource TEXT NOT NULL, amount REAL NOT NULL,
  PRIMARY KEY (region_id, resource)
);
-- Exército inicial (tools/seed_armies.py): uma linha por divisão no dia 0. Só se lê quando não há save.
CREATE TABLE IF NOT EXISTS start_division (
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL REFERENCES country(id),
  template_id INTEGER NOT NULL, region_id INTEGER NOT NULL REFERENCES region(id), name TEXT
);
CREATE TABLE IF NOT EXISTS tech (                -- investigação (HoI4): cost = dias com research_speed 1; requires = id da anterior
  id TEXT PRIMARY KEY, branch TEXT NOT NULL, name TEXT NOT NULL, cost REAL NOT NULL, requires TEXT, description TEXT
);
CREATE TABLE IF NOT EXISTS tech_effect (         -- efeito de país ao concluir: Country.Stat(stat_key) × value (combate vai por modifier tech:<id>)
  tech_id TEXT NOT NULL REFERENCES tech(id), stat_key TEXT NOT NULL, value REAL NOT NULL, PRIMARY KEY (tech_id, stat_key)
);
CREATE TABLE IF NOT EXISTS country_tech (        -- tecnologias com que o país começa
  country_tag TEXT NOT NULL, tech_id TEXT NOT NULL REFERENCES tech(id), PRIMARY KEY (country_tag, tech_id)
);
CREATE TABLE IF NOT EXISTS news_event (          -- eventos noticiosos com data marcada (NewsSystem)
  id TEXT PRIMARY KEY, day INTEGER NOT NULL,     -- dia do jogo em que dispara (0 = arranque)
  country_tag TEXT,                              -- NULL = global; sem FK (os seeds correm antes dos países no TestWorld)
  title TEXT NOT NULL, body TEXT NOT NULL DEFAULT ''
);
CREATE TABLE IF NOT EXISTS news_event_effect (   -- efeito opcional: Country.Stat(stat_key) × value (país do evento; global = todos)
  event_id TEXT NOT NULL REFERENCES news_event(id), stat_key TEXT NOT NULL, value REAL NOT NULL,
  PRIMARY KEY (event_id, stat_key)
);
CREATE TABLE IF NOT EXISTS news_event_option (   -- escolhas de um evento (HoI4): o jogador escolhe, a IA fica com a primeira (sort)
  id TEXT PRIMARY KEY, event_id TEXT NOT NULL REFERENCES news_event(id),
  sort INTEGER NOT NULL DEFAULT 0, title TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS news_event_option_effect (
  option_id TEXT NOT NULL REFERENCES news_event_option(id), stat_key TEXT NOT NULL, value REAL NOT NULL,
  PRIMARY KEY (option_id, stat_key)
);
CREATE TABLE IF NOT EXISTS s_country_law (       -- lei activa por grupo (save)
  country_id INTEGER, grp TEXT, law_id TEXT, PRIMARY KEY (country_id, grp));
CREATE TABLE IF NOT EXISTS s_news_choice (       -- escolha feita por evento (save)
  event_id TEXT PRIMARY KEY, option_id TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS focus (               -- foco nacional (HoI4); árvore por país, requires = foco anterior
  id TEXT PRIMARY KEY, country_tag TEXT NOT NULL REFERENCES country(tag),
  name TEXT NOT NULL, description TEXT NOT NULL DEFAULT '',
  days INTEGER NOT NULL DEFAULT 35, requires TEXT REFERENCES focus(id), sort INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS focus_effect (        -- ao concluir: Country.Stat(stat_key) × value (como tech_effect)
  focus_id TEXT NOT NULL REFERENCES focus(id), stat_key TEXT NOT NULL, value REAL NOT NULL,
  PRIMARY KEY (focus_id, stat_key)
);
CREATE TABLE IF NOT EXISTS focus_link (         -- pré-requisitos além do focus.requires (todos obrigatórios)
  focus_id TEXT NOT NULL REFERENCES focus(id), requires_id TEXT NOT NULL REFERENCES focus(id),
  PRIMARY KEY (focus_id, requires_id)
);
CREATE TABLE IF NOT EXISTS focus_rival (        -- ramos que se excluem: escolher um fecha o outro (vale nos dois sentidos)
  focus_id TEXT NOT NULL REFERENCES focus(id), rival_id TEXT NOT NULL REFERENCES focus(id),
  PRIMARY KEY (focus_id, rival_id)
);
CREATE TABLE IF NOT EXISTS start_war (           -- guerras já a decorrer no dia 0 (tags)
  a_tag TEXT NOT NULL, b_tag TEXT NOT NULL, PRIMARY KEY (a_tag, b_tag)
);
CREATE TABLE IF NOT EXISTS event_def (
  id TEXT PRIMARY KEY, title TEXT NOT NULL, condition_json TEXT NOT NULL, effect_json TEXT NOT NULL
);

-- ================= SAVE (save_N.db em user://) =================
-- Só estado mutável. Referencia ids de static.db por valor.
CREATE TABLE IF NOT EXISTS save_meta (key TEXT PRIMARY KEY, value TEXT);
CREATE TABLE IF NOT EXISTS s_country (
  id INTEGER PRIMARY KEY, is_player INTEGER NOT NULL DEFAULT 0, money REAL NOT NULL DEFAULT 0,
  stability REAL NOT NULL DEFAULT 50, research_tech TEXT, research_progress REAL NOT NULL DEFAULT 0,
  capitulated INTEGER NOT NULL DEFAULT 0, capitulated_day INTEGER,
  manpower REAL NOT NULL DEFAULT -1,  -- -1 = por inicializar (ManpowerSystem)
  focus TEXT, focus_progress REAL NOT NULL DEFAULT 0,
  justify_target INTEGER, justify_progress REAL NOT NULL DEFAULT 0,
  war_exhaustion REAL NOT NULL DEFAULT 0,
  air_power REAL NOT NULL DEFAULT 0,
  nukes INTEGER NOT NULL DEFAULT 0,
  power_rank INTEGER NOT NULL DEFAULT 0,        -- lugar na tabela mundial (PowerRankingSystem)
  power_rank_prev INTEGER NOT NULL DEFAULT 0    -- lugar anterior, para a seta de subida/descida
);
CREATE TABLE IF NOT EXISTS s_country_tech (country_id INTEGER, tech_id TEXT, PRIMARY KEY (country_id, tech_id));
CREATE TABLE IF NOT EXISTS s_focus (country_id INTEGER, focus_id TEXT, PRIMARY KEY (country_id, focus_id));
CREATE TABLE IF NOT EXISTS s_war (a INTEGER, b INTEGER, since_day INTEGER, last_progress_day INTEGER,
  a_regions INTEGER NOT NULL DEFAULT 0, b_regions INTEGER NOT NULL DEFAULT 0,   -- regiões tomadas por lado (WarStatsSystem)
  a_losses INTEGER NOT NULL DEFAULT 0, b_losses INTEGER NOT NULL DEFAULT 0,     -- divisões perdidas por lado
  a_battles INTEGER NOT NULL DEFAULT 0, b_battles INTEGER NOT NULL DEFAULT 0,   -- batalhas ganhas por lado
  PRIMARY KEY (a, b));
CREATE TABLE IF NOT EXISTS s_war_goal (     -- regiões exigidas por lado em cada guerra (WarGoalSystem)
  a INTEGER NOT NULL, b INTEGER NOT NULL, country_id INTEGER NOT NULL, region_id INTEGER NOT NULL,
  PRIMARY KEY (a, b, country_id, region_id));
CREATE TABLE IF NOT EXISTS s_war_history (    -- guerras terminadas, com o saldo final (WarStatsSystem)
  id INTEGER PRIMARY KEY, a INTEGER NOT NULL, b INTEGER NOT NULL,
  start_day INTEGER NOT NULL, end_day INTEGER NOT NULL,
  a_regions INTEGER NOT NULL, b_regions INTEGER NOT NULL,
  a_losses INTEGER NOT NULL, b_losses INTEGER NOT NULL,
  a_battles INTEGER NOT NULL, b_battles INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS s_faction (            -- facções fundadas em jogo (CreateFactionCommand)
  id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT);
CREATE TABLE IF NOT EXISTS s_faction_member (     -- fotografia da composição; linhas presentes = substitui a estática
  faction_id TEXT, country_id INTEGER, PRIMARY KEY (faction_id, country_id));
CREATE TABLE IF NOT EXISTS law (              -- leis nacionais (grupos: conscription, economy…); World.Laws
  id TEXT PRIMARY KEY, grp TEXT NOT NULL, name TEXT NOT NULL, description TEXT,
  sort INTEGER NOT NULL DEFAULT 0,            -- maior = mais mobilizada (a IA escala em guerra)
  is_default INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS law_effect (       -- multiplicadores da lei (entram no ApplyTechs)
  law_id TEXT NOT NULL REFERENCES law(id), stat_key TEXT NOT NULL, value REAL NOT NULL,
  PRIMARY KEY (law_id, stat_key));
CREATE TABLE IF NOT EXISTS spy_op (           -- operações de espionagem (StartSpyOpCommand); World.SpyOps
  id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT,
  cost REAL NOT NULL, days INTEGER NOT NULL,  -- pontos pagos à partida; dias até concluir
  effect TEXT NOT NULL,                       -- steal_money | sabotage_production | stability_hit | sabotage_port | sabotage_infra | sabotage_fort
  magnitude REAL NOT NULL,
  scope TEXT NOT NULL DEFAULT 'country');     -- country = contra o país; region = contra uma região ocupada dele
CREATE TABLE IF NOT EXISTS s_spy_op (         -- operações em curso (save)
  country_id INTEGER, target_id INTEGER, op_id TEXT, days_left REAL NOT NULL,
  region_id INTEGER NOT NULL DEFAULT 0,       -- alvo da sabotagem (0 = operação contra o país inteiro)
  PRIMARY KEY (country_id, target_id));
CREATE TABLE IF NOT EXISTS s_intel (          -- rede de informação activa (efeito intel; até `until_day`)
  country_id INTEGER, target_id INTEGER, until_day INTEGER NOT NULL,
  PRIMARY KEY (country_id, target_id));
CREATE TABLE IF NOT EXISTS s_pact (           -- pactos de não-agressão (a<b, até `until_day`)
  a INTEGER, b INTEGER, until_day INTEGER NOT NULL,
  PRIMARY KEY (a, b));
CREATE TABLE IF NOT EXISTS s_region (
  id INTEGER PRIMARY KEY, controller_id INTEGER NOT NULL, infrastructure REAL NOT NULL,
  owner_id INTEGER,         -- NULL = dono da static.db (só muda com capitulações)
  building INTEGER NOT NULL DEFAULT 0, build_progress REAL NOT NULL DEFAULT 0,
  fort INTEGER NOT NULL DEFAULT 0, fort_building INTEGER NOT NULL DEFAULT 0, fort_progress REAL NOT NULL DEFAULT 0,
  resistance REAL NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS template (
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL, name TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS template_unit (
  template_id INTEGER NOT NULL REFERENCES template(id), unit_type_id INTEGER NOT NULL, qty INTEGER NOT NULL,
  PRIMARY KEY (template_id, unit_type_id)
);
CREATE TABLE IF NOT EXISTS s_division (
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL, template_id INTEGER NOT NULL,
  region_id INTEGER NOT NULL, target_region_id INTEGER,
  hp REAL NOT NULL, org REAL NOT NULL, supply REAL NOT NULL,
  move_progress REAL NOT NULL DEFAULT 0, path TEXT,     -- path: ids separados por vírgula, do próximo salto ao destino
  name TEXT,
  xp REAL NOT NULL DEFAULT 0,
  auto_advance INTEGER NOT NULL DEFAULT 0,
  battles INTEGER NOT NULL DEFAULT 0,       -- batalhas travadas e sobrevividas (MedalSystem)
  captures INTEGER NOT NULL DEFAULT 0,      -- regiões tomadas ao inimigo por esta divisão
  honour TEXT,                              -- honra de batalha em vigor (division_honour.id)
  honour_name TEXT                          -- nome de guerra já resolvido ("Leões de Braga")
);
CREATE TABLE IF NOT EXISTS s_division_medal (    -- condecorações ganhas (MedalSystem)
  division_id INTEGER NOT NULL, medal TEXT NOT NULL, PRIMARY KEY (division_id, medal));
CREATE INDEX IF NOT EXISTS ix_div_region ON s_division(region_id);
CREATE TABLE IF NOT EXISTS s_battle (region_id INTEGER PRIMARY KEY, attacker_country_id INTEGER NOT NULL, days INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS s_battle_division (region_id INTEGER, division_id INTEGER, side TEXT CHECK (side IN ('att','def')), PRIMARY KEY (region_id, division_id));
CREATE TABLE IF NOT EXISTS s_production_queue (
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL, template_id INTEGER NOT NULL, progress REAL NOT NULL,
  repeat_order INTEGER NOT NULL DEFAULT 0   -- produção em série: volta à fila ao ser entregue
);
CREATE TABLE IF NOT EXISTS s_stock (country_id INTEGER, unit_type_id INTEGER, qty INTEGER NOT NULL, PRIMARY KEY (country_id, unit_type_id));
CREATE TABLE IF NOT EXISTS resource (         -- tipos de recurso (data-driven); cada unidade controlada
  id TEXT PRIMARY KEY, name TEXT NOT NULL,    -- multiplica stat_key por (1+per_unit), até cap unidades
  stat_key TEXT NOT NULL, per_unit REAL NOT NULL, cap REAL NOT NULL);
CREATE TABLE IF NOT EXISTS s_trade_deal (     -- acordos de comércio de recursos em vigor (save)
  buyer_id INTEGER NOT NULL, seller_id INTEGER NOT NULL, resource TEXT NOT NULL, units REAL NOT NULL,
  PRIMARY KEY (buyer_id, seller_id, resource));
CREATE TABLE IF NOT EXISTS s_history (        -- amostras dos gráficos de evolução (HistorySystem)
  day INTEGER NOT NULL, country_id INTEGER NOT NULL, money REAL NOT NULL, divisions INTEGER NOT NULL, regions INTEGER NOT NULL,
  power REAL NOT NULL DEFAULT 0,               -- nota do PowerIndex no dia da amostra
  PRIMARY KEY (day, country_id));
CREATE TABLE IF NOT EXISTS s_chronicle (      -- crónica da campanha (ChronicleSystem)
  ord INTEGER PRIMARY KEY, day INTEGER NOT NULL, kind TEXT NOT NULL, text TEXT NOT NULL,
  country_id INTEGER NOT NULL DEFAULT 0, region_id INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS s_region_building (   -- níveis de edifícios por região (ConstructionSystem)
  region_id INTEGER NOT NULL, building TEXT NOT NULL, level INTEGER NOT NULL,
  PRIMARY KEY (region_id, building));
CREATE TABLE IF NOT EXISTS s_decision (          -- decisões nacionais (DecisionSystem): activa se until_day>=dia
  country_id INTEGER NOT NULL, decision TEXT NOT NULL, until_day INTEGER NOT NULL, cooldown_until INTEGER NOT NULL,
  PRIMARY KEY (country_id, decision));
CREATE TABLE IF NOT EXISTS s_general (           -- comandantes ao serviço (HireGeneralCommand)
  country_id INTEGER NOT NULL, general TEXT NOT NULL,
  xp REAL NOT NULL DEFAULT 0,                   -- experiência de campanha (GeneralXpSystem)
  wound_until INTEGER NOT NULL DEFAULT 0,       -- dia em que volta do hospital (CommandCasualtySystem)
  PRIMARY KEY (country_id, general));

CREATE TABLE IF NOT EXISTS s_prisoner (        -- prisioneiros de guerra (PrisonerSystem)
  country_id INTEGER NOT NULL,                  -- quem os guarda
  from_country_id INTEGER NOT NULL,             -- de quem são
  men INTEGER NOT NULL,
  PRIMARY KEY (country_id, from_country_id));

CREATE TABLE IF NOT EXISTS s_offer (           -- propostas à espera de resposta do jogador (OfferSystem)
  from_id INTEGER NOT NULL,                     -- quem propõe
  to_id INTEGER NOT NULL,                       -- a quem propõe (o jogador)
  kind TEXT NOT NULL,                           -- assunto ('prisioneiros', 'paz', 'regiao')
  men INTEGER NOT NULL,                         -- tamanho combinado no dia em que foi feita
  region_id INTEGER NOT NULL DEFAULT 0,         -- região cedida (assunto 'regiao'); 0 nos outros
  day INTEGER NOT NULL,
  expires_day INTEGER NOT NULL,                 -- dia em que cai da mesa
  PRIMARY KEY (from_id, to_id, kind));

CREATE TABLE IF NOT EXISTS s_army_group (        -- grupos de exércitos (ArmyGroupSystem)
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL, name TEXT NOT NULL,
  front_country_id INTEGER, advancing INTEGER NOT NULL DEFAULT 0,
  stance INTEGER NOT NULL DEFAULT 0,              -- 0 parado, 1 avançar, 2 defender
  planning REAL NOT NULL DEFAULT 0,               -- preparação do plano de batalha 0..planning_max (BattlePlanSystem)
  general TEXT);                                  -- comandante destacado (tabela general), ou NULL
CREATE TABLE IF NOT EXISTS s_army_group_member (
  group_id INTEGER NOT NULL, division_id INTEGER NOT NULL, PRIMARY KEY (group_id, division_id));

CREATE TABLE IF NOT EXISTS s_research (        -- ranhuras de investigação ocupadas (ResearchSystem)
  country_id INTEGER NOT NULL,
  tech_id TEXT NOT NULL,                        -- tecnologia nesta ranhura
  progress REAL NOT NULL DEFAULT 0,             -- dias acumulados × research_speed
  PRIMARY KEY (country_id, tech_id));
