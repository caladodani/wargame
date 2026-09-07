-- China: indústria colossal, exército de massa moderno mas sem experiência de combate real desde 1979.
-- Ordem de batalha aprox. 2024-2026: 13 Exércitos de Grupo do EPL + guarnições de fronteira
-- (Xinjiang, Tibete) especializadas em montanha/altitude.

-- ===== Unidades próprias =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (160,'Infantaria de Montanha','ground',1.1,32,0.9,22),
 (161,'Blindados Type 99','ground',4.3,62,2.1,40),
 (162,'Milícia de Massa','ground',0.6,20,0.8,20);

INSERT INTO unit_stat VALUES
 (160,'soft_atk',6),(160,'hard_atk',1),(160,'defense',28),(160,'breakthrough',8),(160,'armor',0),(160,'piercing',6),(160,'hardness',0.1),(160,'hp',27),
 (161,'soft_atk',13),(161,'hard_atk',15),(161,'defense',13),(161,'breakthrough',29),(161,'armor',68),(161,'piercing',58),(161,'hardness',0.9),(161,'hp',21),
 (162,'soft_atk',5),(162,'hard_atk',1),(162,'defense',18),(162,'breakthrough',6),(162,'armor',0),(162,'piercing',4),(162,'hardness',0.05),(162,'hp',20);

INSERT INTO unit_tag VALUES
 (160,'infantry'),(160,'ground'),(160,'montanha'),
 (161,'armored'),(161,'ground'),
 (162,'infantry'),(162,'ground');

-- ===== Espíritos nacionais =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('CHN_industria_colossal','CHN','Indústria Colossal','A maior base fabril do mundo mantém a artilharia e o apoio sempre bem municiados.'),
 ('CHN_falta_experiencia','CHN','Falta de Experiência de Combate','Sem uma guerra de grande escala desde 1979, os ataques rendem menos do que o equipamento faria prever.'),
 ('CHN_muralha_defensiva','CHN','Doutrina da Muralha Defensiva','Décadas de defesa territorial em profundidade tornam as tropas muito mais difíceis de desalojar em montanha ou zona urbana.'),
 ('CHN_exercito_povo','CHN','Exército do Povo','A doutrina de guerra popular compensa com números o que falta em sofisticação individual.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (170,'spirit',NULL,NULL,'str','support','add',0.10,'CHN','CHN_industria_colossal'),
 (171,'spirit',NULL,NULL,'str_attacker',NULL,'mul',0.90,'CHN','CHN_falta_experiencia'),
 (172,'spirit','terrain','mountain','str_defender',NULL,'mul',1.15,'CHN','CHN_muralha_defensiva'),
 (173,'spirit','terrain','urban','str_defender',NULL,'mul',1.10,'CHN','CHN_muralha_defensiva'),
 (174,'spirit',NULL,NULL,'str','infantry','add',0.05,'CHN','CHN_exercito_povo');

-- ===== country_stat / country_info =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('CHN','industry',1.3),
 ('CHN','production_speed',1.3),
 ('CHN','org_regain',0.95),
 ('CHN','start_army_mult',1.3);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('CHN','República popular de partido único','Presidente e Secretário-Geral do Partido',
  'Guerra popular moderna: massa industrial e territorial apoiada em mísseis, drones e defesa em profundidade.',
  'Não-alinhado',
  'A segunda maior economia do mundo transformou o Exército Popular de Libertação num dos maiores e mais bem equipados do planeta em poucas décadas. Falta-lhe, porém, experiência de combate recente à escala de uma grande guerra — a força vem da quantidade, da indústria e da defesa territorial, não ainda da prática de fogo real.');

-- ===== Templates próprios =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (151,'CHN','Divisão de Montanha'),
 (152,'CHN','Divisão Blindada Type 99'),
 (153,'CHN','Onda Humana');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (151,160,6),(151,4,2),
 (152,161,4),(152,2,3),(152,4,2),
 (153,162,8),(153,4,2);

-- ===== Brigadas/divisões reais (exército inicial) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (154,'CHN','71º Exército de Grupo','Mecanizada','Jiangsu'),
 (155,'CHN','72º Exército de Grupo','Blindada','Zhejiang'),
 (156,'CHN','73º Exército de Grupo','Infantaria','Fujian'),
 (157,'CHN','74º Exército de Grupo','Divisão Blindada Type 99','Guangdong'),
 (158,'CHN','75º Exército de Grupo','Infantaria','Guangxi'),
 (159,'CHN','76º Exército de Grupo','Blindada','Shaanxi'),
 (160,'CHN','77º Exército de Grupo','Mecanizada','Sichuan'),
 (161,'CHN','78º Exército de Grupo','Blindada','Heilongjiang'),
 (162,'CHN','79º Exército de Grupo','Infantaria AT','Liaoning'),
 (163,'CHN','80º Exército de Grupo','Infantaria','Shandong'),
 (164,'CHN','81º Exército de Grupo','Divisão Blindada Type 99','Hebei'),
 (165,'CHN','82º Exército de Grupo','Infantaria','Henan'),
 (166,'CHN','Guarnição de Xinjiang','Divisão de Montanha','Xinjiang'),
 (167,'CHN','Guarnição do Tibete','Divisão de Montanha','Xizang'),
 (168,'CHN','Guarnição de Pequim','Infantaria AT','Beijing'),
 (169,'CHN','Milícias Populares de Massa','Onda Humana','Hunan');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('chn_made_in_china_2025','CHN','Made in China 2025','Plano estatal para dominar semicondutores, robótica e manufatura avançada.',49,NULL,1),
 ('chn_belt_and_road','CHN','Nova Rota da Seda','Investimento maciço em infraestrutura e influência económica ao longo da Eurásia.',56,NULL,2),
 ('chn_pla_modernizacao','CHN','Modernização do EPL','Reforma estrutural do Exército de Libertação Popular em torno de exércitos de grupo integrados.',42,NULL,3),
 ('chn_civil_militar','CHN','Fusão Civil-Militar','Empresas tecnológicas privadas passam a fornecer diretamente o esforço de defesa.',35,'chn_made_in_china_2025',4),
 ('chn_string_of_pearls','CHN','Colar de Pérolas','Rede de portos e bases de apoio logístico do Índico ao Pacífico Ocidental.',49,'chn_belt_and_road',5),
 ('chn_mar_do_sul','CHN','Ilhas Artificiais do Mar do Sul da China','Consolidação de posições avançadas em recifes e atóis disputados.',63,'chn_pla_modernizacao',6),
 ('chn_conscricao_universal','CHN','Serviço Militar Universal Reforçado','Alargamento do recenseamento e treino de reservistas em todas as províncias.',35,'chn_pla_modernizacao',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('chn_made_in_china_2025','research_speed',1.10),
 ('chn_made_in_china_2025','industry',1.05),
 ('chn_belt_and_road','industry',1.08),
 ('chn_pla_modernizacao','org_regain',1.08),
 ('chn_civil_militar','production_speed',1.10),
 ('chn_string_of_pearls','production_speed',1.06),
 ('chn_mar_do_sul','org_regain',1.06),
 ('chn_conscricao_universal','conscription',1.25);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('chn_mar_do_sul','chn_conscricao_universal');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('chn_conscricao_universal','chn_made_in_china_2025');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('CHN_adv_plano','CHN','economia','Secretário do Plano','🏭',200,'Fábricas novas de província em província.'),
 ('CHN_adv_massas','CHN','propaganda','Comissário das Massas','📣',185,'Chama à tropa aldeias inteiras.');
INSERT INTO advisor_effect VALUES ('CHN_adv_plano','industry',1.16);
INSERT INTO advisor_effect VALUES ('CHN_adv_massas','conscription',1.2);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('CHN_planos','Planos Quinquenais','🏮',10,'CHN');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('CHN_law_abertura','CHN_planos','Reforma e abertura','As zonas económicas vendem ao mundo inteiro.',0,1,'CHN'),
 ('CHN_law_fabrico','CHN_planos','Fabrico avançado','O Estado escolhe os sectores e paga-lhes a fábrica.',1,0,'CHN'),
 ('CHN_law_dirigida','CHN_planos','Economia dirigida','Tudo o que sai das linhas serve o plano, não o comprador.',2,0,'CHN');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('CHN_law_abertura','export_share',1.15),
 ('CHN_law_abertura','industry',1.05),
 ('CHN_law_fabrico','industry',1.1),
 ('CHN_law_fabrico','research_speed',1.08),
 ('CHN_law_dirigida','industry',1.18),
 ('CHN_law_dirigida','production_speed',1.08),
 ('CHN_law_dirigida','export_share',0.7);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('CHN_gen_massa','Comandante de Massa','attack',1.14,150,'CHN','🏮','Sabe pôr no terreno mais gente do que o inimigo consegue contar.'),
 ('CHN_gen_planalto','General do Planalto','defense',1.15,135,'CHN','🏔','Guarda a fronteira alta, onde falta o ar e sobra a distância.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('CHN_escola','Guerra Popular','🏮',10,'CHN');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('CHN_doc_popular','CHN_escola','Escola da Guerra Popular','O povo é a água e o exército o peixe: falta uma coisa, morre a outra.',50,NULL,1,'CHN'),
 ('CHN_doc_profundidade','CHN_escola','Trocar Espaço por Tempo','Recua-se mil quilómetros e devolve-se a conta ao fim de um ano.',110,'CHN_doc_popular',2,'CHN'),
 ('CHN_doc_milhoes','CHN_escola','Exército de Milhões','Quem põe no terreno mais gente do que o inimigo consegue contar não perde a soma.',190,'CHN_doc_profundidade',3,'CHN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('CHN_doc_popular','conscription',1.1),
 ('CHN_doc_profundidade','defense',1.07),
 ('CHN_doc_profundidade','org_regain',1.05),
 ('CHN_doc_milhoes','conscription',1.12),
 ('CHN_doc_milhoes','attack',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('CHN_ar','Asas do Povo','🐉',11,'CHN','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('CHN_ar_numero','CHN_ar','Massa no Ar','Cem aparelhos simples enchem um céu que dez complicados não enchem.',50,NULL,1,'CHN'),
 ('CHN_ar_negacao','CHN_ar','Negação do Espaço Aéreo','Não é preciso mandar no céu: chega que o inimigo não mande.',110,'CHN_ar_numero',2,'CHN'),
 ('CHN_ar_foguete','CHN_ar','Força de Foguetes','O que se destrói no aeródromo não se combate no ar.',185,'CHN_ar_negacao',3,'CHN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('CHN_ar_numero','air_upkeep',0.9),
 ('CHN_ar_negacao','air_losses',0.93),
 ('CHN_ar_foguete','air_bombing',1.12);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('CHN_mar','Mar Próximo','⚓',12,'CHN','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('CHN_mar_negacao_mar','CHN_mar','Negação do Mar Próximo','Não é preciso ter o mar: chega que o outro não entre nele.',50,NULL,1,'CHN'),
 ('CHN_mar_estaleiro_chn','CHN_mar','Estaleiros em Massa','Meia frota mundial sai dos mesmos cais todos os anos.',110,'CHN_mar_negacao_mar',2,'CHN'),
 ('CHN_mar_milicia','CHN_mar','Milícia Marítima','Mil barcos de pesca também são uma esquadra, e ninguém lhes atira.',185,'CHN_mar_estaleiro_chn',3,'CHN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('CHN_mar_negacao_mar','naval_blockade',1.1),
 ('CHN_mar_estaleiro_chn','naval_upkeep',0.89),
 ('CHN_mar_milicia','naval_patrol',1.1);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('CHN_ar_gen_popular','Chefe da Asa Popular','air_bombing',1.14,145,'CHN','🪁','Manda muitos aparelhos ao mesmo alvo até o alvo deixar de existir.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('CHN_mar_gen_litoral','Almirante da Frota do Litoral','naval_patrol',1.15,145,'CHN','🌊','Guarda uma costa comprida com navios pequenos e olhos em terra.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('CHN_tech_ar_quinta_geracao','Aviação','Caça de Quinta Geração',260,'air_1','Furtividade e ligação de dados feitas em casa, aos milhares: o céu disputado fica mais barato.','CHN'),
 ('CHN_tech_mar_mar_do_sul','Marinha','Frota do Mar do Sul',260,'nav_1','Ilhas artificiais, mísseis costeiros e navios aos molhos: fecha-se um mar inteiro a quem lá passa.','CHN');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('CHN_tech_ar_quinta_geracao','air_losses',0.86),
 ('CHN_tech_mar_mar_do_sul','naval_blockade',1.18);

-- ===== escada de postos nacional (general_rank.country_tag) =====
INSERT INTO general_rank (domain,level,name,xp,bonus,country_tag) VALUES
 ('exercito',1,'Coronel Superior',0,0,'CHN'),
 ('exercito',2,'General-Major',40,0.5,'CHN'),
 ('exercito',3,'Tenente-General do Povo',110,1,'CHN'),
 ('exercito',4,'General do Povo',220,1.75,'CHN'),
 ('exercito',5,'Comandante da Comissão Militar Central',360,2.5,'CHN'),
 ('ar',1,'Coronel Superior da Aviação',0,0,'CHN'),
 ('ar',2,'General-Major da Aviação',40,0.5,'CHN'),
 ('ar',3,'Tenente-General da Aviação',110,1,'CHN'),
 ('ar',4,'General da Força Aérea',220,1.75,'CHN'),
 ('ar',5,'Comandante da Força Aérea',360,2.5,'CHN'),
 ('mar',1,'Comodoro do Povo',0,0,'CHN'),
 ('mar',2,'Contra-Almirante do Povo',40,0.5,'CHN'),
 ('mar',3,'Vice-Almirante do Povo',110,1,'CHN'),
 ('mar',4,'Almirante do Povo',220,1.75,'CHN'),
 ('mar',5,'Comandante da Marinha do Povo',360,2.5,'CHN');

-- ===== condecorações nacionais (medal.country_tag) =====
INSERT INTO medal (id,name,description,metric,threshold,bonus,sort,country_tag) VALUES
 ('CHN_baptismo','Medalha do Soldado do Povo','Aguentou a primeira batalha até ao fim.','battles',1,0.01,1,'CHN'),
 ('CHN_assalto','Medalha do Assalto Vermelho','Tomou três regiões ao inimigo.','captures',3,0.03,2,'CHN'),
 ('CHN_campanha','Louvor de Primeira Classe','Quarenta pontos de experiência em combate.','xp',40,0.02,3,'CHN'),
 ('CHN_aco','Medalha de Heroísmo em Combate','Dez batalhas travadas e sobrevividas.','battles',10,0.04,4,'CHN'),
 ('CHN_imortais','Ordem da Estrela de Agosto','Veterania quase no tecto: a divisão é uma lenda.','xp',90,0.05,5,'CHN');
