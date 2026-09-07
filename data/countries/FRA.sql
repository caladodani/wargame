-- FRA (k=27) — Armée de Terre, ordem de batalha aproximada 2024-2026.
-- Fontes: Légion étrangère (1er RE Aubagne, 2e REI Nîmes), 27e Brigade d'infanterie de montagne
-- (Chasseurs Alpins, Annecy/Haute-Savoie), 2e e 7e Brigade blindée (Leclerc), 1re e 3e Brigade
-- mécanisée (VBCI), 9e Brigade d'infanterie de Marine, 11e Brigade parachutiste (Toulouse),
-- 6e Brigade légère blindée. Carácter: dissuasão nuclear própria (force de frappe), forte
-- tradição de projecção expedicionária (Sahel, Líbano), Légion étrangère e Chasseurs Alpins únicos.

-- ===== unit_type próprios (100+20*27 .. 119+20*27 = 640..659) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (640,'Légion étrangère','ground',1.7,45,0.9,34),
 (641,'Chasseurs Alpins','ground',1.5,40,0.9,27),
 (642,'Leclerc','ground',7.3,82,2.3,40),
 (643,'VBCI','ground',2.8,47,1.5,47);

INSERT INTO unit_stat VALUES
 (640,'soft_atk',8),   (640,'hard_atk',1.5),(640,'defense',23),(640,'breakthrough',12),(640,'armor',0), (640,'piercing',6), (640,'hardness',0.1), (640,'hp',23),
 (641,'soft_atk',8),   (641,'hard_atk',1.5),(641,'defense',25),(641,'breakthrough',10),(641,'armor',0), (641,'piercing',6), (641,'hardness',0.15),(641,'hp',24),
 (642,'soft_atk',13.5),(642,'hard_atk',18.5),(642,'defense',14.5),(642,'breakthrough',32),(642,'armor',76),(642,'piercing',66),(642,'hardness',0.92),(642,'hp',22),
 (643,'soft_atk',10.5),(643,'hard_atk',5),  (643,'defense',27),(643,'breakthrough',18),(643,'armor',19),(643,'piercing',22),(643,'hardness',0.53),(643,'hp',30);

INSERT INTO unit_tag VALUES
 (640,'infantry'),(640,'ground'),(640,'especial'),
 (641,'infantry'),(641,'ground'),(641,'especial'),
 (642,'armored'),(642,'ground'),
 (643,'infantry'),(643,'armored'),(643,'ground');

-- ===== espíritos nacionais + modificadores (ids 640..659) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('FRA_legiao_estrangeira','FRA','Légion étrangère',
   'Corpo de elite bicentenário aberto a voluntários estrangeiros, forjado em campanhas coloniais e expedicionárias: as tropas especiais atacam com mais força.'),
 ('FRA_dissuasao_nuclear','FRA','Force de Frappe',
   'A dissuasão nuclear independente sustenta a autonomia estratégica francesa: maior eficiência de comando em todas as operações.'),
 ('FRA_chasseurs_alpins','FRA','Chasseurs Alpins',
   'Tropas de montanha treinadas nos Alpes franceses desde 1888: defendem-se e atacam melhor em terreno montanhoso.'),
 ('FRA_projecao_expedicionaria','FRA','Vocação Expedicionária',
   'Décadas de operações no Sahel, no Líbano e em África deram ao Exército rotina de projecção rápida além-fronteiras: ligeiro bónus de ataque generalizado.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (640,'spirit',NULL,NULL,           'str_attacker','especial','mul',1.20,'FRA','FRA_legiao_estrangeira'),
 (641,'spirit',NULL,NULL,           'command',     NULL,      'mul',1.10,'FRA','FRA_dissuasao_nuclear'),
 (642,'spirit','terrain','mountain','str_defender','especial','mul',1.18,'FRA','FRA_chasseurs_alpins'),
 (643,'spirit','terrain','mountain','str_attacker','especial','mul',1.15,'FRA','FRA_chasseurs_alpins'),
 (644,'spirit',NULL,NULL,           'str_attacker',NULL,      'add',0.05,'FRA','FRA_projecao_expedicionaria');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('FRA','production_speed',1.10),
 ('FRA','org_regain',1.15),
 ('FRA','start_army_mult',0.55);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('FRA','República Semipresidencialista','Presidente da República e Chefe do Estado-Maior das Forças Armadas',
  'Autonomia estratégica assente na dissuasão nuclear, projecção expedicionária rápida e tradições de elite (Légion étrangère, Chasseurs Alpins).',
  'NATO',
  'O Exército francês combina uma dissuasão nuclear própria com uma das forças expedicionárias mais activas da Europa, tendo operado com regularidade no Sahel e no Médio Oriente. A Légion étrangère e os Chasseurs Alpins dão-lhe capacidades de elite únicas, apoiadas pelo blindado Leclerc e pelo VBCI, numa doutrina que privilegia autonomia estratégica e resposta rápida.');

-- ===== templates próprios (ids 1351..1400) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1351,'FRA','Brigade de la Légion étrangère'),
 (1352,'FRA','Brigade Blindée Leclerc'),
 (1353,'FRA','Brigade de Chasseurs Alpins');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1351,640,4),(1351,4,1),(1351,5,1),
 (1352,642,4),(1352,643,2),(1352,4,1),(1352,6,1),
 (1353,641,4),(1353,1,2),(1353,4,1);

-- ===== brigadas/divisões reais nomeadas (ids 1354..1400) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (1354,'FRA','1er Régiment étranger','Brigade de la Légion étrangère','Bouches-du-Rhône'),
 (1355,'FRA','2e Régiment étranger d''infanterie','Infantaria','Gard'),
 (1356,'FRA','27e Brigade d''infanterie de montagne','Brigade de Chasseurs Alpins','Haute-Savoie'),
 (1357,'FRA','7e Brigade blindée','Brigade Blindée Leclerc','Doubs'),
 (1358,'FRA','2e Brigade blindée','Blindada','Bas-Rhin'),
 (1359,'FRA','3e Brigade mécanisée','Mecanizada','Marne'),
 (1360,'FRA','9e Brigade d''infanterie de Marine','Mecanizada','Vienne'),
 (1361,'FRA','1re Brigade mécanisée','Mecanizada','Meurthe-et-Moselle'),
 (1362,'FRA','11e Brigade parachutiste','Infantaria','Haute-Garonne'),
 (1363,'FRA','6e Brigade légère blindée','Blindada','Var'),
 (1364,'FRA','1er Régiment de Parachutistes d''Infanterie de Marine','Infantaria AT','Pyrénées-Atlantiques');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('fra_dissuasion','FRA','Dissuasion Nucléaire','A força de dissuasão nuclear francesa continua a ser o pilar da independência estratégica.',49,NULL,1),
 ('fra_force_frappe','FRA','Modernização da Force de Frappe','Renovação dos SNLE e dos vetores aéreos: a dissuasão entra na próxima geração.',56,'fra_dissuasion',2),
 ('fra_projection','FRA','Força de Projeção Expedicionária','Investimento em transporte estratégico e logística para operações longe de metrópole.',35,NULL,3),
 ('fra_porte_avions','FRA','Porta-Aviões de Nova Geração','O programa do sucessor do Charles de Gaulle garante presença naval de longo alcance.',63,'fra_projection',4),
 ('fra_legion','FRA','Reforço da Légion Étrangère','Recrutamento alargado e treino intensivo reforçam a ponta de lança expedicionária.',28,'fra_projection',5),
 ('fra_industrie_defense','FRA','Base Industrial e Tecnológica de Defesa','Dassault, Naval Group e Nexter recebem encomendas que sustentam a autonomia industrial.',42,NULL,6),
 ('fra_rafale_export','FRA','Diplomacia do Rafale','Contratos de exportação do Rafale financiam a próxima geração de caças franceses.',49,'fra_industrie_defense',7),
 ('fra_service_national','FRA','Service National Universel','Um serviço cívico e militar renovado alarga a reserva mobilizável da Nação.',35,NULL,8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('fra_dissuasion','research_speed',1.08),
 ('fra_force_frappe','research_speed',1.10),
 ('fra_projection','org_regain',1.06),
 ('fra_porte_avions','production_speed',1.10),
 ('fra_legion','org_regain',1.08),
 ('fra_industrie_defense','industry',1.10),
 ('fra_rafale_export','industry',1.08),
 ('fra_rafale_export','production_speed',1.08),
 ('fra_service_national','conscription',1.20);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('fra_porte_avions','fra_legion');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('FRA_adv_plano','FRA','economia','Comissário do Plano','📐',190,'A economia inteira num caderno, e o caderno cumpre-se.'),
 ('FRA_adv_atomico','FRA','ciencia','Director do Comissariado Atómico','⚛',200,'Laboratórios que não param nem aos domingos.');
INSERT INTO advisor_effect VALUES ('FRA_adv_plano','industry',1.13);
INSERT INTO advisor_effect VALUES ('FRA_adv_plano','production_speed',1.06);
INSERT INTO advisor_effect VALUES ('FRA_adv_atomico','research_speed',1.22);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('FRA_force','Força de Dissuasão','⚜',10,'FRA');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('FRA_law_minima','FRA_force','Dissuasão mínima','O suficiente para ninguém tentar, e nem um franco a mais.',0,1,'FRA'),
 ('FRA_law_triade','FRA_force','Tríade modernizada','Submarinos, aviões e mísseis renovados ao mesmo tempo.',1,0,'FRA'),
 ('FRA_law_autonomia','FRA_force','Autonomia estratégica','A França arma-se sozinha e decide sozinha.',2,0,'FRA');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('FRA_law_minima','defense',1.06),
 ('FRA_law_triade','research_speed',1.08),
 ('FRA_law_triade','defense',1.08),
 ('FRA_law_autonomia','attack',1.08),
 ('FRA_law_autonomia','industry',1.06),
 ('FRA_law_autonomia','export_share',0.9);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('FRA_gen_blindados_fra','Mestre dos Blindados','attack',1.15,140,'FRA','⚜','A doutrina do choque: entra pelo meio e não olha para os lados.'),
 ('FRA_gen_ultramar','Comandante do Ultramar','move_speed',1.17,130,'FRA','🌍','Intervém em três continentes com o que couber num avião.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('FRA_escola','Escola da Manobra','⚜',10,'FRA');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('FRA_doc_methodique','FRA_escola','Batalha Metódica','Nada se ataca sem fogo preparado: o método poupa homens.',50,NULL,1,'FRA'),
 ('FRA_doc_choc','FRA_escola','Doutrina do Choque','Entra-se pelo meio e não se olha para os lados.',110,'FRA_doc_methodique',2,'FRA'),
 ('FRA_doc_outremer','FRA_escola','Intervenção Ultramarina','Três continentes com o que couber num avião, e chega-se antes dos outros.',190,'FRA_doc_choc',3,'FRA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('FRA_doc_methodique','defense',1.06),
 ('FRA_doc_methodique','attack',1.04),
 ('FRA_doc_choc','attack',1.08),
 ('FRA_doc_outremer','move_speed',1.09),
 ('FRA_doc_outremer','org_regain',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('FRA_ar','Asas da República','🐓',11,'FRA','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('FRA_ar_mirage','FRA_ar','Indústria Própria','Um país que desenha os seus caças não pede licença para os usar.',50,NULL,1,'FRA'),
 ('FRA_ar_dissuasao','FRA_ar','Força de Dissuasão','Um esquadrão sempre pronto vale por uma guerra que não houve.',110,'FRA_ar_mirage',2,'FRA'),
 ('FRA_ar_sahel','FRA_ar','Intervenção no Sahel','Quatro mil quilómetros num salto, e a bomba cai onde tinha de cair.',185,'FRA_ar_dissuasao',3,'FRA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('FRA_ar_mirage','air_upkeep',0.91),
 ('FRA_ar_dissuasao','air_losses',0.93),
 ('FRA_ar_sahel','air_bombing',1.1);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('FRA_mar','Marinha Nacional','⚜',12,'FRA','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('FRA_mar_porta_avioes','FRA_mar','Grupo Aeronaval','Um porta-aviões nuclear é a única pista que ninguém pode negar.',50,NULL,1,'FRA'),
 ('FRA_mar_ssbn','FRA_mar','Patrulha Permanente','Há sessenta anos que há sempre um submarino no mar, sem falhar um dia.',110,'FRA_mar_porta_avioes',2,'FRA'),
 ('FRA_mar_outremer_mar','FRA_mar','Guarda do Ultramar','Ilhas em três oceanos são a segunda maior zona de mar do mundo.',185,'FRA_mar_ssbn',3,'FRA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('FRA_mar_porta_avioes','naval_escort',1.1),
 ('FRA_mar_ssbn','naval_blockade',1.09),
 ('FRA_mar_outremer_mar','naval_patrol',1.09);
