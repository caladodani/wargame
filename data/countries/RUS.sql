-- Rússia: doutrina de fogo de massa e blindados em profundidade, mas comando rígido e reservas
-- de baixa moral. Ordem de batalha aprox. 2024-2026: Armadas Combinadas por distrito militar +
-- VDV (aerotransportados de elite) + unidades de assalto tipo Storm-Z, apoiadas em artilharia massiva.

-- ===== Unidades próprias =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (180,'T-90/T-72B3 (Blindados)','ground',3.6,55,2.0,40),
 (181,'VDV (Aerotransportados)','ground',1.6,40,1.1,50),
 (182,'Artilharia Pesada (BM-21)','support',2.2,45,1.6,22),
 (183,'Storm-Z (Assalto Penal)','ground',0.5,20,0.8,25);

INSERT INTO unit_stat VALUES
 (180,'soft_atk',12),(180,'hard_atk',15),(180,'defense',12),(180,'breakthrough',27),(180,'armor',55),(180,'piercing',50),(180,'hardness',0.85),(180,'hp',20),
 (181,'soft_atk',8),(181,'hard_atk',1.4),(181,'defense',20),(181,'breakthrough',11),(181,'armor',5),(181,'piercing',7),(181,'hardness',0.15),(181,'hp',24),
 (182,'soft_atk',26),(182,'hard_atk',3),(182,'defense',5),(182,'breakthrough',8),(182,'armor',0),(182,'piercing',9),(182,'hardness',0.2),(182,'hp',6),
 (183,'soft_atk',8),(183,'hard_atk',1),(183,'defense',12),(183,'breakthrough',10),(183,'armor',0),(183,'piercing',5),(183,'hardness',0.05),(183,'hp',16);

INSERT INTO unit_tag VALUES
 (180,'armored'),(180,'ground'),
 (181,'infantry'),(181,'ground'),(181,'vdv'),
 (182,'support'),(182,'ground'),
 (183,'infantry'),(183,'ground');

-- ===== Espíritos nacionais =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('RUS_doutrina_fogo_massa','RUS','Doutrina de Fogo de Massa','Décadas de artilharia soviética/russa dão às unidades de apoio um efeito muito maior em ataque.'),
 ('RUS_comando_rigido','RUS','Comando Rígido e Centralizado','A cadeia de comando vertical trava a iniciativa no terreno — o comando geral rende menos.'),
 ('RUS_reservas_baixa_moral','RUS','Reservas de Baixa Moral','Tropas mobilizadas às pressas e mal treinadas defendem-se pior do que o número sugere.'),
 ('RUS_guerra_de_atrito','RUS','Guerra de Atrito e Profundidade','Em floresta, o terreno e a familiaridade favorecem quem defende o território russo.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (190,'spirit',NULL,NULL,'str_attacker','support','mul',1.20,'RUS','RUS_doutrina_fogo_massa'),
 (191,'spirit',NULL,NULL,'command',NULL,'mul',0.85,'RUS','RUS_comando_rigido'),
 (192,'spirit',NULL,NULL,'str_defender','infantry','mul',0.92,'RUS','RUS_reservas_baixa_moral'),
 (193,'spirit','terrain','forest','str_defender',NULL,'mul',1.15,'RUS','RUS_guerra_de_atrito');

-- ===== country_stat / country_info =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('RUS','industry',1.15),
 ('RUS','production_speed',1.15),
 ('RUS','org_regain',0.8),
 ('RUS','start_army_mult',1.5);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('RUS','República federal semipresidencialista','Presidente da Federação Russa',
  'Fogo de massa e profundidade estratégica: saturar com artilharia e blindados, absorver o desgaste com espaço e números.',
  'CSTO',
  'A maior economia de guerra da Europa mobilizou grande parte da indústria para sustentar um exército de massa, forte em artilharia e blindados mas com um comando muito centralizado e reservas mobilizadas de treino desigual. A vastidão do território compensa perdas que noutro país seriam decisivas.');

-- ===== Templates próprios =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (201,'RUS','Divisão Blindada T-90'),
 (202,'RUS','Regimento VDV'),
 (203,'RUS','Corpo de Assalto Storm-Z');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (201,180,4),(201,2,3),(201,182,2),
 (202,181,6),(202,182,2),
 (203,183,8),(203,182,2);

-- ===== Brigadas/divisões reais (exército inicial) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (204,'RUS','4ª Divisão de Tanques de Guardas Kantemirovskaya','Divisão Blindada T-90','Moskva'),
 (205,'RUS','2ª Divisão de Fuzileiros Motorizados de Guardas Tamanskaya','Mecanizada','Moskva'),
 (206,'RUS','1ª Armada de Guardas de Carros de Combate','Divisão Blindada T-90','Moskva'),
 (207,'RUS','76ª Divisão de Assalto Aéreo da Guarda (Pskov)','Regimento VDV','Pskov'),
 (208,'RUS','7ª Divisão de Assalto Aéreo da Guarda (Novorossiysk)','Regimento VDV','Krasnodar'),
 (209,'RUS','98ª Divisão Aerotransportada da Guarda','Regimento VDV','Ivanovo'),
 (210,'RUS','20ª Armada Combinada de Guardas','Infantaria AT','Rostov'),
 (211,'RUS','8ª Armada Combinada de Guardas','Infantaria AT','Voronezh'),
 (212,'RUS','58ª Armada Combinada','Infantaria','North Ossetia'),
 (213,'RUS','41ª Armada Combinada','Mecanizada','Novosibirsk'),
 (214,'RUS','35ª Armada Combinada','Infantaria','Primor''ye'),
 (215,'RUS','36ª Armada Combinada','Blindada','Irkutsk'),
 (216,'RUS','29ª Armada Combinada','Infantaria','Chita'),
 (217,'RUS','68º Corpo de Exército (Sacalina)','Infantaria','Sakhalin'),
 (218,'RUS','Corpo de Assalto Storm-Z do Donbass','Corpo de Assalto Storm-Z','Rostov'),
 (219,'RUS','18ª Divisão de Fuzileiros Motorizados da Guarda','Mecanizada','Chechnya');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('rus_rearmamento_estatal','RUS','Programa Estatal de Armamento','Plano plurianual do Kremlin para modernizar T-90M, artilharia e mísseis.',49,NULL,1),
 ('rus_complexo_militar_industrial','RUS','Complexo Militar-Industrial','Uralvagonzavod e as fábricas dos Urais em três turnos: mais tanques, mais depressa.',56,'rus_rearmamento_estatal',2),
 ('rus_mobilizacao_parcial','RUS','Mobilização Parcial','Decreto de recrutamento alarga a base de reservistas convocáveis.',35,NULL,3),
 ('rus_academias_militares','RUS','Reforma das Academias Militares','Currículo de Suvorov e Frunze revisto: oficiais recuperam terreno mais depressa.',42,'rus_mobilizacao_parcial',4),
 ('rus_vdv_elite','RUS','Elite Aerotransportada (VDV)','Doutrina Desantniki: unidades de para-quedistas com treino e prontidão reforçados.',42,NULL,5),
 ('rus_academia_ciencias_militares','RUS','Academia de Ciências Militares','Institutos de investigação de defesa aceleram o desenvolvimento de novas tecnologias.',49,'rus_rearmamento_estatal',6),
 ('rus_distritos_militares','RUS','Reorganização dos Distritos Militares','Comando por distrito (Sul, Centro, Leste, Oeste) ganha autonomia logística.',35,'rus_mobilizacao_parcial',7),
 ('rus_defesa_territorial','RUS','Tropas de Defesa Territorial','Milícias regionais e depósitos dispersos reconstituem forças mais depressa após o desgaste.',35,'rus_distritos_militares',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('rus_rearmamento_estatal','production_speed',1.10),
 ('rus_complexo_militar_industrial','industry',1.12),
 ('rus_mobilizacao_parcial','conscription',1.20),
 ('rus_academias_militares','org_regain',1.08),
 ('rus_vdv_elite','conscription',1.10),
 ('rus_vdv_elite','org_regain',1.06),
 ('rus_academia_ciencias_militares','research_speed',1.10),
 ('rus_distritos_militares','production_speed',1.06),
 ('rus_defesa_territorial','org_regain',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('rus_academias_militares','rus_distritos_militares'),
 ('rus_complexo_militar_industrial','rus_academia_ciencias_militares');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('rus_defesa_territorial','rus_rearmamento_estatal');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('RUS_adv_gosplan','RUS','economia','Comissário do Plano Quinquenal','📊',200,'A meta é a meta, custe o que custar.'),
 ('RUS_adv_orgaos','RUS','seguranca','Chefe dos Órgãos de Segurança','🕵',190,'Nenhum espião estrangeiro dorme descansado.');
INSERT INTO advisor_effect VALUES ('RUS_adv_gosplan','industry',1.15);
INSERT INTO advisor_effect VALUES ('RUS_adv_gosplan','conscription',1.08);
INSERT INTO advisor_effect VALUES ('RUS_adv_orgaos','counter_intel',1.35);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('RUS_hidrocarbonetos','Renda dos Hidrocarbonetos','🐻',10,'RUS');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('RUS_law_exportacao','RUS_hidrocarbonetos','Exportação livre','Gás e petróleo vendidos a quem pagar.',0,1,'RUS'),
 ('RUS_law_gasodutos','RUS_hidrocarbonetos','Gasodutos como arma','A torneira abre e fecha conforme a política do mês.',1,0,'RUS'),
 ('RUS_law_complexo','RUS_hidrocarbonetos','Complexo militar-industrial','A renda toda vai para as linhas de montagem.',2,0,'RUS');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('RUS_law_exportacao','export_share',1.2),
 ('RUS_law_exportacao','industry',1.03),
 ('RUS_law_gasodutos','export_price',1.15),
 ('RUS_law_gasodutos','counter_intel',1.05),
 ('RUS_law_complexo','production_speed',1.12),
 ('RUS_law_complexo','industry',1.08),
 ('RUS_law_complexo','export_share',0.7);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('RUS_gen_artilharia_rus','Deus da Guerra','attack',1.16,145,'RUS','🐻','Artilharia a metro: primeiro arrasa-se, depois é que se anda.'),
 ('RUS_gen_inverno','General Inverno','defense',1.17,135,'RUS','❄','Não precisa de ganhar a batalha: chega esperar por Janeiro.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('RUS_escola','Arte Operacional','🐻',10,'RUS');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('RUS_doc_artilharia','RUS_escola','Deus da Guerra','Primeiro arrasa-se, depois é que se anda.',50,NULL,1,'RUS'),
 ('RUS_doc_escalao','RUS_escola','Escalões em Profundidade','O que se perde à frente reconstitui-se atrás, e a frente continua.',110,'RUS_doc_artilharia',2,'RUS'),
 ('RUS_doc_inverno','RUS_escola','General Inverno','Não é preciso ganhar a batalha: chega esperar por Janeiro.',190,'RUS_doc_escalao',3,'RUS');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('RUS_doc_artilharia','attack',1.08),
 ('RUS_doc_escalao','org_regain',1.07),
 ('RUS_doc_escalao','conscription',1.06),
 ('RUS_doc_inverno','defense',1.09),
 ('RUS_doc_inverno','attack',1.05);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('RUS_ar','Asas da Pátria','🛫',11,'RUS','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('RUS_ar_frontal','RUS_ar','Aviação Frontal','O avião pertence à frente e à frente obedece.',50,NULL,1,'RUS'),
 ('RUS_ar_pvo','RUS_ar','Defesa Antiaérea Integrada','O céu de casa defende-se de baixo e de cima ao mesmo tempo.',110,'RUS_ar_frontal',2,'RUS'),
 ('RUS_ar_rustico','RUS_ar','Aparelho Rústico','Pista de terra, mecânico com luvas grossas, e o motor pega à mesma.',185,'RUS_ar_pvo',3,'RUS');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('RUS_ar_frontal','air_bombing',1.1),
 ('RUS_ar_pvo','air_losses',0.9),
 ('RUS_ar_rustico','air_upkeep',0.9);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('RUS_mar','Esquadra do Norte','🚢',12,'RUS','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('RUS_mar_bastiao','RUS_mar','Bastião do Norte','Um mar de casa fechado a ferrolho, com o que interessa lá dentro.',50,NULL,1,'RUS'),
 ('RUS_mar_quebra_gelo','RUS_mar','Rota do Gelo','Quem tem quebra-gelos tem um oceano que mais ninguém usa.',110,'RUS_mar_bastiao',2,'RUS'),
 ('RUS_mar_missil_rus','RUS_mar','Salva de Mísseis','Uma salva inteira de uma vez: ou passa toda ou não valia a pena.',185,'RUS_mar_quebra_gelo',3,'RUS');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('RUS_mar_bastiao','naval_blockade',1.1),
 ('RUS_mar_quebra_gelo','naval_patrol',1.1),
 ('RUS_mar_missil_rus','naval_losses',0.93);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('RUS_ar_gen_frontal_rus','Chefe da Aviação de Frente','air_bombing',1.14,145,'RUS','🪂','O avião dele trabalha para a artilharia, não para o comunicado.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('RUS_mar_gen_norte_rus','Almirante da Frota do Norte','naval_escort',1.16,145,'RUS','⚓','Traz comboios pelo gelo, que é meio caminho para os perder.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('RUS_tech_ar_assalto','Aviação','Aviação de Assalto',260,'air_1','Aparelhos blindados a bater a linha da frente de muito baixo, como sempre se fez aqui.','RUS'),
 ('RUS_tech_mar_frota_norte','Marinha','Frota do Norte',260,'nav_1','Submarinos e cruzadores a sair do Ártico: fecha-se o Atlântico Norte a partir de cima.','RUS');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('RUS_tech_ar_assalto','air_bombing',1.18),
 ('RUS_tech_mar_frota_norte','naval_blockade',1.18);

-- ===== escada de postos nacional (general_rank.country_tag) =====
INSERT INTO general_rank (domain,level,name,xp,bonus,country_tag) VALUES
 ('exercito',1,'General-Maior',0,0,'RUS'),
 ('exercito',2,'Tenente-General da Rússia',40,0.5,'RUS'),
 ('exercito',3,'Coronel-General',110,1,'RUS'),
 ('exercito',4,'General do Exército',220,1.75,'RUS'),
 ('exercito',5,'Marechal da Federação',360,2.5,'RUS'),
 ('ar',1,'General-Maior da Aviação',0,0,'RUS'),
 ('ar',2,'Tenente-General da Aviação',40,0.5,'RUS'),
 ('ar',3,'Coronel-General da Aviação',110,1,'RUS'),
 ('ar',4,'General do Ar',220,1.75,'RUS'),
 ('ar',5,'Marechal Chefe da Aviação',360,2.5,'RUS'),
 ('mar',1,'Contra-Almirante da Frota',0,0,'RUS'),
 ('mar',2,'Vice-Almirante da Frota',40,0.5,'RUS'),
 ('mar',3,'Almirante',110,1,'RUS'),
 ('mar',4,'Almirante de Esquadra',220,1.75,'RUS'),
 ('mar',5,'Almirante da Frota',360,2.5,'RUS');
