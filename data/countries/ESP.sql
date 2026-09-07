-- ESP (k=12) — Ejército de Tierra espanhol, ordem de batalha aproximada 2024-2026.
-- Fontes: La Legión (Brigada "Rey Alfonso XIII" II, Tercios de Ceuta/Melilla), Brigada Paracaidista
-- "Almogávares" VI (BRIPAC), brigadas de infantaria mecanizada "Guzmán el Bueno" X e "Guadarrama" XII,
-- brigadas de infantaria ligeira "Aragón" I e "Galicia" VII, Brigada de Caçadores de Montanha (Jaca),
-- Brigada "Canarias" XVI, Comandâncias Gerais de Ceuta e Melilla, Brigada de Cavalaria "Castillejos" II.
-- Carácter: exército de dimensão média-alta, profissional, ponta de lança em La Legión e forças
-- paraquedistas, forte presença nos enclaves norte-africanos e nas Canárias, pilar sul da NATO.

-- ===== unit_type próprios (100+20*12 .. 119+20*12 = 340..359) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (340,'Legionário (La Legión)','ground',1.3,35,0.9,28),
 (341,'Paraquedista BRIPAC','ground',1.4,38,0.9,36),
 (342,'Pizarro (mecanizada)','ground',2.8,48,1.6,48),
 (343,'Leopardo 2E','ground',7.0,80,2.2,37);

INSERT INTO unit_stat VALUES
 (340,'soft_atk',8), (340,'hard_atk',1.5),(340,'defense',27),(340,'breakthrough',10),(340,'armor',0), (340,'piercing',6), (340,'hardness',0.1), (340,'hp',27),
 (341,'soft_atk',7), (341,'hard_atk',1),  (341,'defense',22),(341,'breakthrough',12),(341,'armor',0), (341,'piercing',5), (341,'hardness',0.1), (341,'hp',22),
 (342,'soft_atk',10),(342,'hard_atk',5),  (342,'defense',27),(342,'breakthrough',18),(342,'armor',18),(342,'piercing',22),(342,'hardness',0.55),(342,'hp',32),
 (343,'soft_atk',13),(343,'hard_atk',18), (343,'defense',14),(343,'breakthrough',30),(343,'armor',75),(343,'piercing',65),(343,'hardness',0.93),(343,'hp',23);

INSERT INTO unit_tag VALUES
 (340,'infantry'),(340,'ground'),(340,'elite'),
 (341,'infantry'),(341,'ground'),(341,'elite'),
 (342,'infantry'),(342,'armored'),(342,'ground'),
 (343,'armored'),(343,'ground');

-- ===== espíritos nacionais + modificadores (ids 340..359) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('ESP_tradicion_legionaria','ESP','Tradição Legionária',
   'A Legión Española, forjada em quase um século de campanhas, mantém um núcleo de infantaria de elite muito acima da média em agressividade ofensiva.'),
 ('ESP_reaccion_rapida','ESP','Reação Rápida Paraquedista',
   'A Brigada Paracaidista BRIPAC treina para intervenção imediata em qualquer ponto do território: maior eficiência de comando na coordenação de operações aeroterrestres.'),
 ('ESP_defensa_pirenaica','ESP','Defesa Pirenaica e Cantábrica',
   'Séculos de guerra em terreno de montanha, dos Pirenéus à Cordilheira Cantábrica, deram às tropas espanholas vantagem clara a defender e atacar em relevo acidentado.'),
 ('ESP_pilar_sur_otan','ESP','Pilar Sul da OTAN',
   'Como charneira entre a Europa e o Norte de África, a Espanha investe pesadamente em interoperabilidade aliada: maior eficiência de comando em operações conjuntas.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (340,'spirit',NULL,NULL,       'str_attacker','elite','mul',1.20,'ESP','ESP_tradicion_legionaria'),
 (341,'spirit',NULL,NULL,       'str_defender','elite','mul',1.10,'ESP','ESP_tradicion_legionaria'),
 (342,'spirit',NULL,NULL,       'command',     NULL,   'mul',1.08,'ESP','ESP_reaccion_rapida'),
 (343,'spirit','terrain','mountain','str_defender',NULL,'mul',1.15,'ESP','ESP_defensa_pirenaica'),
 (344,'spirit','terrain','mountain','str_attacker',NULL,'mul',1.10,'ESP','ESP_defensa_pirenaica'),
 (345,'spirit',NULL,NULL,       'command',     NULL,   'mul',1.10,'ESP','ESP_pilar_sur_otan');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('ESP','production_speed',1.05),
 ('ESP','org_regain',1.05),
 ('ESP','start_army_mult',0.95);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('ESP','Monarquia parlamentar','Chefe do Estado-Maior da Defesa (JEMAD)',
  'Força expedicionária ligeira assente em La Legión e nas forças paraquedistas, defesa reforçada dos enclaves norte-africanos e das Canárias, plena integração na OTAN.',
  'NATO',
  'A Espanha mantém um exército profissional de dimensão média-alta, com La Legión e a Brigada Paracaidista BRIPAC como núcleo de projeção rápida. A presença permanente em Ceuta e Melilla e o arquipélago das Canárias obrigam a uma postura de defesa dispersa por três frentes. Membro fundador da atual estrutura de defesa europeia e pilar meridional da OTAN, investe na modernização blindada com o Leopardo 2E e na mecanização com o Pizarro.');

-- ===== templates próprios (ids 601..650) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (601,'ESP','Brigada de la Legión'),
 (602,'ESP','Brigada Paracaidista BRIPAC'),
 (603,'ESP','Brigada Blindada Leopardo'),
 (604,'ESP','Regimento de Artilharia');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (601,340,3),(601,1,2),(601,4,1),(601,6,1),
 (602,341,3),(602,1,2),(602,5,1),
 (603,343,3),(603,342,3),(603,4,1),
 (604,4,4),(604,1,3),(604,5,1);

-- ===== brigadas/regimentos reais nomeados (ids 605..650) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (605,'ESP','Brigada "Rey Alfonso XIII" II de La Legión','Brigada de la Legión','Almería'),
 (606,'ESP','Tercio "Don Juan de Austria" de La Legión','Brigada de la Legión','Melilla'),
 (607,'ESP','Brigada Paracaidista "Almogávares" VI','Brigada Paracaidista BRIPAC','Madrid'),
 (608,'ESP','Brigada Mecanizada "Guzmán el Bueno" X','Mecanizada','Córdoba'),
 (609,'ESP','Brigada Mecanizada "Guadarrama" XII','Mecanizada','Madrid'),
 (610,'ESP','Brigada de Infantaria Ligeira "Aragón" I','Infantaria','Zaragoza'),
 (611,'ESP','Brigada de Infantaria Ligeira "Galicia" VII','Infantaria','Pontevedra'),
 (612,'ESP','Brigada de Caçadores de Montanha','Infantaria','Huesca'),
 (613,'ESP','Brigada "Canarias" XVI','Infantaria','Las Palmas'),
 (614,'ESP','Comandância Geral de Ceuta','Infantaria AT','Ceuta'),
 (615,'ESP','Comandância Geral de Melilla','Infantaria AT','Melilla'),
 (616,'ESP','Brigada de Cavalaria "Castillejos" II','Brigada Blindada Leopardo','Zaragoza'),
 (617,'ESP','Regimento de Artilharia de Campanha','Regimento de Artilharia','Valladolid');

-- ===== correcção de terreno =====
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='ESP')
   AND name IN ('Huesca','Lérida','Gerona','Navarra','Asturias','León','Granada');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('esp_otan_ue','ESP','Compromisso Euro-Atlântico','Espanha aproxima-se da meta de 2% do PIB em defesa e aprofunda a interoperabilidade com a OTAN.',35,NULL,1),
 ('esp_eurocuerpo','ESP','Eurocuerpo e Reação Rápida da UE','Reforço do Eurocuerpo sediado em Bétera e das forças multinacionais de reação rápida europeias.',42,'esp_otan_ue',2),
 ('esp_industria_defensa','ESP','Plano de Indústria de Defesa','Navantia, Indra e Airbus Espanha recebem investimento industrial e tecnológico plurianual.',42,NULL,3),
 ('esp_navantia','ESP','Submarinos S-80 e Fragatas F-110','Os estaleiros de Cartagena e Ferrol entregam os novos submarinos S-80 Plus e as fragatas F-110.',49,'esp_industria_defensa',4),
 ('esp_pizarro_leopardo','ESP','Modernização Blindada','Actualização dos Pizarro e novo lote de carros Leopardo 2E para as brigadas mecanizadas.',42,'esp_navantia',5),
 ('esp_reserva_estrategica','ESP','Reserva Estratégica','Programa de reservistas voluntários para reforçar rapidamente as Forças Armadas em crise.',35,NULL,6),
 ('esp_legion','ESP','Legião e UME','A Legião e a Unidad Militar de Emergencias mantêm-se como pontas de lança de intervenção rápida.',35,'esp_reserva_estrategica',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('esp_otan_ue','research_speed',1.08),
 ('esp_eurocuerpo','org_regain',1.08),
 ('esp_industria_defensa','industry',1.10),
 ('esp_navantia','industry',1.10),
 ('esp_navantia','production_speed',1.08),
 ('esp_pizarro_leopardo','production_speed',1.12),
 ('esp_reserva_estrategica','conscription',1.20),
 ('esp_legion','conscription',1.10),
 ('esp_legion','org_regain',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('esp_legion','esp_otan_ue');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('ESP_adv_ferrol','ESP','economia','Chefe dos Estaleiros de Ferrol','⚓',185,'Carga e navios saem de Ferrol antes do prazo.'),
 ('ESP_adv_plazas','ESP','propaganda','Governador das Praças de África','🏛',180,'Sabe governar terra do outro lado do mar.');
INSERT INTO advisor_effect VALUES ('ESP_adv_ferrol','port_capacity',1.25);
INSERT INTO advisor_effect VALUES ('ESP_adv_ferrol','production_speed',1.06);
INSERT INTO advisor_effect VALUES ('ESP_adv_plazas','occupied_yield',1.22);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('ESP_autonomias','Estado das Autonomias','🏰',10,'ESP');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('ESP_law_alargada','ESP_autonomias','Autonomia alargada','Cada comunidade manda em quase tudo o que é seu.',0,1,'ESP'),
 ('ESP_law_equilibrio','ESP_autonomias','Equilíbrio constitucional','Madrid e as regiões repartem o que custa e o que rende.',1,0,'ESP'),
 ('ESP_law_centralizacao','ESP_autonomias','Centralização de Madrid','O Estado chama a si os recursos e o recenseamento.',2,0,'ESP');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('ESP_law_alargada','research_speed',1.05),
 ('ESP_law_alargada','conscription',0.95),
 ('ESP_law_equilibrio','industry',1.05),
 ('ESP_law_equilibrio','counter_intel',1.05),
 ('ESP_law_centralizacao','conscription',1.15),
 ('ESP_law_centralizacao','counter_intel',1.12),
 ('ESP_law_centralizacao','research_speed',0.95);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('ESP_gen_tercio','Herdeiro dos Tércios','attack',1.15,140,'ESP','🏰','A infantaria pesada de sempre, com quinhentos anos de escola.'),
 ('ESP_gen_legion','Chefe da Legião','org_regain',1.14,130,'ESP','🐐','Tropa de choque que se recompõe sozinha e nunca fica sem chefe.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('ESP_escola','Escola dos Tércios','🏰',10,'ESP');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ESP_doc_tercio','ESP_escola','Herança dos Tércios','Quinhentos anos de infantaria que se forma e não se desfaz.',50,NULL,1,'ESP'),
 ('ESP_doc_legion','ESP_escola','Tropa de Choque','A Legião entra primeiro e responde depois.',110,'ESP_doc_tercio',2,'ESP'),
 ('ESP_doc_pirenaico','ESP_escola','Defesa Peninsular','Uma península com duas fronteiras defende-se nas duas ao mesmo tempo.',190,'ESP_doc_legion',3,'ESP');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ESP_doc_tercio','defense',1.07),
 ('ESP_doc_legion','attack',1.07),
 ('ESP_doc_legion','org_regain',1.05),
 ('ESP_doc_pirenaico','defense',1.08),
 ('ESP_doc_pirenaico','conscription',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('ESP_ar','Asas do Estreito','🛬',11,'ESP','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ESP_ar_estreito_ar','ESP_ar','Ponte Aérea do Estreito','Catorze quilómetros de mar passam-se por cima em minutos.',50,NULL,1,'ESP'),
 ('ESP_ar_canarias','ESP_ar','Alcance das Canárias','Mil e trezentos quilómetros de casa é o voo de todos os dias.',110,'ESP_ar_estreito_ar',2,'ESP'),
 ('ESP_ar_apoio_esp','ESP_ar','Apoio Aproximado','A Legião pede fogo por rádio e o avião responde ao minuto.',185,'ESP_ar_canarias',3,'ESP');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ESP_ar_estreito_ar','air_upkeep',0.93),
 ('ESP_ar_canarias','air_losses',0.94),
 ('ESP_ar_apoio_esp','air_bombing',1.09);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('ESP_mar','Escola de Armada','🧭',12,'ESP','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ESP_mar_estreito_mar','ESP_mar','Guarda do Estreito','Catorze quilómetros decidem quem entra e quem sai do Mediterrâneo.',50,NULL,1,'ESP'),
 ('ESP_mar_lhd','ESP_mar','Projecção Anfíbia','Um navio que leva tropa e aviões vale por uma base que não se tem.',110,'ESP_mar_estreito_mar',2,'ESP'),
 ('ESP_mar_bazan','ESP_mar','Estaleiros do Ferrol','Constrói-se em casa, repara-se em casa, e o mar não espera.',185,'ESP_mar_lhd',3,'ESP');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ESP_mar_estreito_mar','naval_blockade',1.1),
 ('ESP_mar_lhd','naval_escort',1.08),
 ('ESP_mar_bazan','naval_upkeep',0.92);
