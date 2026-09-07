-- ITA (k=8) — Esercito Italiano, ordem de batalha aproximada 2024-2026.
-- Fontes: Brigata Alpina "Taurinense" (Torino) e "Julia" (Udine), Brigata Corazzata "Ariete"
-- (Pordenone/Friuli), Brigata Paracadutisti "Folgore" (Livorno/Toscana), Brigata Meccanizzata
-- "Aosta" (Messina/Sicília), "Sassari" (Cagliari), "Pinerolo" (Bari), Brigata Bersaglieri
-- "Garibaldi" (Caserta), Brigata Cavalleria "Pozzuolo del Friuli", Brigata Granatieri di Sardegna
-- (Roma). Carácter: forte tradição de guerra de montanha (Alpini) e de elite aerotransportada
-- (Folgore), flanco sul da NATO no Mediterrâneo.

-- ===== unit_type próprios (100+20*8 .. 119+20*8 = 260..279) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (260,'Ariete','ground',7.2,80,2.3,36),
 (261,'Paracadutisti Folgore','ground',1.6,42,0.9,35),
 (262,'Alpini','ground',1.5,40,0.9,26),
 (263,'Freccia (Centauro)','ground',2.6,46,1.4,52);

INSERT INTO unit_stat VALUES
 (260,'soft_atk',13),  (260,'hard_atk',18.5),(260,'defense',14),(260,'breakthrough',31),(260,'armor',74),(260,'piercing',64),(260,'hardness',0.91),(260,'hp',22),
 (261,'soft_atk',7.5), (261,'hard_atk',1),   (261,'defense',21),(261,'breakthrough',12),(261,'armor',0), (261,'piercing',5), (261,'hardness',0.1), (261,'hp',21),
 (262,'soft_atk',8),   (262,'hard_atk',1.5), (262,'defense',25),(262,'breakthrough',10),(262,'armor',0), (262,'piercing',6), (262,'hardness',0.15),(262,'hp',24),
 (263,'soft_atk',10),  (263,'hard_atk',6),   (263,'defense',25),(263,'breakthrough',17),(263,'armor',22),(263,'piercing',24),(263,'hardness',0.5), (263,'hp',29);

INSERT INTO unit_tag VALUES
 (260,'armored'),(260,'ground'),
 (261,'infantry'),(261,'ground'),(261,'especial'),
 (262,'infantry'),(262,'ground'),(262,'especial'),
 (263,'infantry'),(263,'armored'),(263,'ground');

-- ===== espíritos nacionais + modificadores (ids 260..279) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('ITA_tradicao_alpina','ITA','Tradição Alpina',
   'Os Alpini treinam há mais de um século nas Dolomitas e nos Alpes: combatem e defendem-se melhor em terreno montanhoso.'),
 ('ITA_folgore_elite','ITA','Elite Aerotransportada Folgore',
   'A Brigata Paracadutisti Folgore mantém uma reputação de combate desde El Alamein: as tropas especiais atacam com mais força.'),
 ('ITA_industria_de_defesa','ITA','Indústria de Defesa (Leonardo)',
   'A Leonardo fornece sistemas de comando e vigilância de ponta às Forças Armadas: maior eficiência de comando.'),
 ('ITA_flanco_mediterraneo','ITA','Flanco Sul da NATO',
   'Guarnições distribuídas por toda a península e ilhas, com foco na defesa de portos e cidades costeiras: melhor defesa em terreno urbano.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (260,'spirit','terrain','mountain','str_defender','especial','mul',1.20,'ITA','ITA_tradicao_alpina'),
 (261,'spirit','terrain','mountain','str_attacker','especial','mul',1.15,'ITA','ITA_tradicao_alpina'),
 (262,'spirit',NULL,NULL,           'str_attacker','especial','mul',1.18,'ITA','ITA_folgore_elite'),
 (263,'spirit',NULL,NULL,           'command',     NULL,      'mul',1.08,'ITA','ITA_industria_de_defesa'),
 (264,'spirit','terrain','urban',   'str_defender',NULL,      'mul',1.10,'ITA','ITA_flanco_mediterraneo');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('ITA','production_speed',1.0),
 ('ITA','org_regain',1.15),
 ('ITA','start_army_mult',0.45);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('ITA','República Parlamentar','Presidente do Conselho de Ministros e Ministro da Defesa',
  'Guerra de montanha (Alpini), projecção rápida aerotransportada (Folgore), defesa do flanco sul mediterrânico da NATO.',
  'NATO',
  'O Esercito Italiano combina uma das mais antigas tradições de guerra de montanha da Europa, os Alpini, com uma força aerotransportada de elite, a Folgore. Geograficamente responsável pelo flanco sul da NATO no Mediterrâneo, mantém guarnições espalhadas pela península e pelas grandes ilhas, apoiadas pela indústria de defesa da Leonardo.');

-- ===== templates próprios (ids 401..450) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (401,'ITA','Brigata Alpina'),
 (402,'ITA','Brigata Corazzata Ariete'),
 (403,'ITA','Brigata Paracadutisti Folgore');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (401,262,4),(401,1,2),(401,4,1),
 (402,260,4),(402,263,2),(402,4,1),(402,6,1),
 (403,261,4),(403,4,1),(403,5,1);

-- ===== brigadas/divisões reais nomeadas (ids 404..450) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (404,'ITA','Brigata Alpina Taurinense','Brigata Alpina','Turin'),
 (405,'ITA','Brigata Alpina Julia','Brigata Alpina','Udine'),
 (406,'ITA','Brigata Meccanizzata Aosta','Mecanizada','Catania'),
 (407,'ITA','Brigata Meccanizzata Sassari','Mecanizada','Cagliari'),
 (408,'ITA','Brigata Corazzata Ariete','Brigata Corazzata Ariete','Udine'),
 (409,'ITA','Brigata Paracadutisti Folgore','Brigata Paracadutisti Folgore','Pisa'),
 (410,'ITA','Brigata Bersaglieri Garibaldi','Mecanizada','Napoli'),
 (411,'ITA','Brigata Meccanizzata Pinerolo','Mecanizada','Bari'),
 (412,'ITA','Brigata Cavalleria Pozzuolo del Friuli','Infantaria AT','Udine'),
 (413,'ITA','Brigata Granatieri di Sardegna','Infantaria','Roma'),
 (414,'ITA','Brigata Meccanizzata Friuli','Mecanizada','Bologna');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('ita_pilastro_mediterraneo','ITA','Pilastro do Mediterrâneo','A Marinha Militare reforça o controlo das rotas do Mediterrâneo central.',35,NULL,1),
 ('ita_nato_sud','ITA','Comando NATO Sul','Nápoles consolida-se como polo de comando da Aliança para o flanco sul.',42,'ita_pilastro_mediterraneo',2),
 ('ita_industria_difesa','ITA','Consórcios de Defesa Nacional','Leonardo, Fincantieri e Iveco Defence articulam-se num polo industrial único.',49,NULL,3),
 ('ita_caccia_multiruolo','ITA','Programa de Caça Multifunções','Investimento acelerado nos caças Eurofighter e no futuro programa GCAP.',56,'ita_industria_difesa',4),
 ('ita_cantieri_navali','ITA','Estaleiros Navais Renovados','Fincantieri moderniza-se para fornecer fragatas FREMM e navios de apoio.',42,'ita_industria_difesa',5),
 ('ita_servizio_volontario','ITA','Serviço Militar Voluntário','Campanha de recrutamento reforça os efetivos das Forze Armate profissionais.',28,NULL,6),
 ('ita_alpini_addestramento','ITA','Doutrina Alpina Renovada','As tropas Alpini retomam a tradição de guerra em montanha nos Alpes.',35,'ita_servizio_volontario',7),
 ('ita_riserva_nazionale','ITA','Reserva Estratégica Nacional','Reorganização da reserva militar para responder rapidamente a crises regionais.',42,'ita_servizio_volontario',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('ita_pilastro_mediterraneo','org_regain',1.08),
 ('ita_nato_sud','research_speed',1.10),
 ('ita_industria_difesa','industry',1.10),
 ('ita_caccia_multiruolo','production_speed',1.12),
 ('ita_cantieri_navali','industry',1.08),
 ('ita_cantieri_navali','production_speed',1.08),
 ('ita_servizio_volontario','conscription',1.15),
 ('ita_alpini_addestramento','org_regain',1.10),
 ('ita_riserva_nazionale','conscription',1.12);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('ita_caccia_multiruolo','ita_cantieri_navali'),
 ('ita_alpini_addestramento','ita_riserva_nazionale');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('ita_riserva_nazionale','ita_pilastro_mediterraneo');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('ITA_adv_turim','ITA','economia','Senhor das Oficinas de Turim','🏭',180,'As encomendas dele saem antes das outras.'),
 ('ITA_adv_mare','ITA','propaganda','Prefeito do Mare Nostrum','🌊',175,'Enche os quartéis com discursos de mar.');
INSERT INTO advisor_effect VALUES ('ITA_adv_turim','production_speed',1.16);
INSERT INTO advisor_effect VALUES ('ITA_adv_mare','conscription',1.12);
INSERT INTO advisor_effect VALUES ('ITA_adv_mare','occupied_yield',1.1);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('ITA_mediterraneo','Nosso Mar','🍋',10,'ITA');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('ITA_law_humanitarias','ITA_mediterraneo','Missões humanitárias','A marinha salva náufragos e faz boa figura.',0,1,'ITA'),
 ('ITA_law_guarda','ITA_mediterraneo','Guarda do Mediterrâneo','Patrulha permanente do Adriático à Sicília.',1,0,'ITA'),
 ('ITA_law_projeccao','ITA_mediterraneo','Projecção no Norte de África','Bases do outro lado do mar e tropa pronta a embarcar.',2,0,'ITA');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('ITA_law_humanitarias','research_speed',1.05),
 ('ITA_law_guarda','defense',1.08),
 ('ITA_law_guarda','industry',1.04),
 ('ITA_law_projeccao','attack',1.08),
 ('ITA_law_projeccao','occupied_yield',1.1);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('ITA_gen_alpini','Chefe dos Alpini','defense',1.16,130,'ITA','🏔','Guerra de montanha desde 1872, e ainda se ganham medalhas por lá.'),
 ('ITA_gen_mediterraneo_ita','Comandante do Mediterrâneo','move_speed',1.16,130,'ITA','🍋','Embarca a brigada num dia e desembarca-a do outro lado no seguinte.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('ITA_escola','Escola de Montanha','🏔',10,'ITA');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ITA_doc_alpini','ITA_escola','Alpini','Guerra de montanha desde 1872, e ainda se ganham medalhas por lá.',50,NULL,1,'ITA'),
 ('ITA_doc_mediterraneo','ITA_escola','Manobra Mediterrânica','Embarca-se num dia e desembarca-se do outro lado no seguinte.',110,'ITA_doc_alpini',2,'ITA'),
 ('ITA_doc_bersaglieri','ITA_escola','Bersaglieri','Infantaria que corre ao passo de carga há cento e cinquenta anos.',190,'ITA_doc_mediterraneo',3,'ITA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ITA_doc_alpini','defense',1.07),
 ('ITA_doc_mediterraneo','move_speed',1.07),
 ('ITA_doc_mediterraneo','attack',1.05),
 ('ITA_doc_bersaglieri','move_speed',1.08),
 ('ITA_doc_bersaglieri','org_regain',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('ITA_ar','Asas do Mediterrâneo','🍀',11,'ITA','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ITA_ar_acrobatica','ITA_ar','Escola Acrobática','Quem voa em formação apertada nos dias bons não se perde nos maus.',50,NULL,1,'ITA'),
 ('ITA_ar_torpedo_ar','ITA_ar','Aerotorpedeiros','A arma que fez do Mediterrâneo um mar perigoso para toda a gente.',110,'ITA_ar_acrobatica',2,'ITA'),
 ('ITA_ar_mare_nostrum','ITA_ar','Alcance Mediterrânico','De Sicília chega-se a todo o lado sem reabastecer.',185,'ITA_ar_torpedo_ar',3,'ITA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ITA_ar_acrobatica','air_losses',0.93),
 ('ITA_ar_torpedo_ar','air_bombing',1.1),
 ('ITA_ar_mare_nostrum','air_upkeep',0.93);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('ITA_mar','Mar Nosso','🍀',12,'ITA','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ITA_mar_maiale','ITA_mar','Assaltadores de Porto','Dois homens num torpedo tripulado afundaram uma esquadra inteira em Alexandria.',50,NULL,1,'ITA'),
 ('ITA_mar_sicilia','ITA_mar','Estreito da Sicília','O meio do Mediterrâneo é uma porta e a porta é nossa.',110,'ITA_mar_maiale',2,'ITA'),
 ('ITA_mar_fincantieri','ITA_mar','Estaleiros do Adriático','Constrói-se para meio mundo e repara-se o próprio em metade do tempo.',185,'ITA_mar_sicilia',3,'ITA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ITA_mar_maiale','naval_blockade',1.11),
 ('ITA_mar_sicilia','naval_patrol',1.09),
 ('ITA_mar_fincantieri','naval_upkeep',0.91);
