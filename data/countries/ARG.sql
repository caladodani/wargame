-- ARG (k=24) — Ejército Argentino, ordem de batalha aproximada 2024-2026.
-- Fontes: Brigadas de Montaña VI (Neuquén) e VIII (Mendoza), Brigada de Infantería de Monte IX
-- (Comodoro Rivadavia), Brigadas de Infantería XI (Posadas) e XII (Corrientes), Brigada Blindada II,
-- Brigada Mecanizada X (Córdoba), Agrupación Ejército Tierra del Fuego (guarnição das Malvinas), frota
-- de TAM (Tanque Argentino Mediano) da Fabricaciones Militares.
-- Carácter: exército médio, especializado em guerra de montanha nos Andes, blindado ligeiro próprio
-- (TAM) em vez de importado, e uma reivindicação territorial permanente sobre as Ilhas Malvinas.

-- ===== unit_type próprios (100+20*24 .. 119+20*24 = 580..599) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (580,'Infantaria de Montanha','ground',1.1,32,0.9,22),
 (581,'TAM (Tanque Argentino Mediano)','ground',3.2,55,1.9,42);

INSERT INTO unit_stat VALUES
 (580,'soft_atk',6), (580,'hard_atk',1),  (580,'defense',27),(580,'breakthrough',9), (580,'armor',0), (580,'piercing',5), (580,'hardness',0.1), (580,'hp',27),
 (581,'soft_atk',11),(581,'hard_atk',12), (581,'defense',13),(581,'breakthrough',24),(581,'armor',45),(581,'piercing',45),(581,'hardness',0.75),(581,'hp',19);

INSERT INTO unit_tag VALUES
 (580,'infantry'),(580,'ground'),(580,'montanha'),
 (581,'armored'),(581,'ground');

-- ===== espíritos nacionais + modificadores (ids 580..599) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('ARG_guerra_de_montanha','ARG','Doutrina de Guerra de Montanha',
   'As brigadas de montanha treinam há décadas nas cordilheiras andinas: as tropas argentinas atacam e defendem-se muito melhor em terreno de montanha.'),
 ('ARG_veteranos_malvinas','ARG','Memória das Malvinas',
   'A guerra de 1982 deixou uma doutrina defensiva cautelosa e uma firme determinação nacional: as divisões argentinas defendem-se com mais firmeza em território próprio.'),
 ('ARG_industria_tam','ARG','Indústria Blindada Nacional',
   'A Fabricaciones Militares produz e mantém o TAM localmente, sem depender de importação: os blindados argentinos atacam com mais eficácia.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (580,'spirit','terrain','mountain','str_attacker',NULL,'mul',1.20,'ARG','ARG_guerra_de_montanha'),
 (581,'spirit','terrain','mountain','str_defender',NULL,'mul',1.20,'ARG','ARG_guerra_de_montanha'),
 (582,'spirit',NULL,NULL,'str_attacker','montanha','mul',1.15,'ARG','ARG_guerra_de_montanha'),
 (583,'spirit',NULL,NULL,'str_defender',NULL,      'add',0.10,'ARG','ARG_veteranos_malvinas'),
 (584,'spirit',NULL,NULL,'str_attacker','armored', 'mul',1.15,'ARG','ARG_industria_tam');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('ARG','production_speed',0.9),
 ('ARG','org_regain',1.0),
 ('ARG','start_army_mult',0.9);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('ARG','República presidencialista federal','Presidente da República',
  'Defesa territorial andina, guerra de montanha e reivindicação permanente da soberania sobre as Ilhas Malvinas.',
  'Não-alinhado',
  'O Ejército Argentino é um exército de dimensão média, moldado pela geografia extrema do país: das selvas do Nordeste às cordilheiras andinas e à Patagónia gelada. As brigadas de montanha são a sua especialidade e o TAM, tanque de fabrico próprio, mantém alguma independência da indústria de defesa estrangeira. A memória da guerra das Malvinas de 1982 continua presente na doutrina e na guarnição permanente da Terra do Fogo.');

-- ===== templates próprios (ids 1+50*24 .. 50+50*24 = 1201..1250) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1201,'ARG','Brigada de Montanha'),
 (1202,'ARG','Regimento Blindado TAM');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1201,580,5),(1201,4,2),(1201,6,1),
 (1202,581,4),(1202,2,2),(1202,4,1);

-- ===== brigadas/regimentos reais nomeados (ids 1201..1250) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (1203,'ARG','Brigada de Montaña VI','Brigada de Montanha','Neuquén'),
 (1204,'ARG','Brigada de Montaña VIII','Brigada de Montanha','Mendoza'),
 (1205,'ARG','Brigada de Infantería de Monte IX','Brigada de Montanha','Chubut'),
 (1206,'ARG','Brigada de Infantería XI','Infantaria','Misiones'),
 (1207,'ARG','Brigada de Infantería XII','Infantaria','Corrientes'),
 (1208,'ARG','Brigada Blindada II','Regimento Blindado TAM','Buenos Aires'),
 (1209,'ARG','Brigada Mecanizada X','Mecanizada','Córdoba'),
 (1210,'ARG','Agrupación Ejército Tierra del Fuego','Infantaria AT','Tierra del Fuego'),
 (1211,'ARG','Regimiento de Infantería 25 Sarmiento','Infantaria','Santa Fe'),
 (1212,'ARG','Grupo de Artillería de Montaña','Brigada de Montanha','Salta');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('arg_atlantico_sul','ARG','Soberania no Atlântico Sul','Reforço da presença naval e aérea junto às Malvinas e à Patagônia costeira.',42,NULL,1),
 ('arg_cordilheira','ARG','Defesa da Cordilheira','Doutrina de montanha para os passos andinos: mobilidade e logística em altitude.',35,NULL,2),
 ('arg_fabricaciones','ARG','Fabricaciones Militares','Retomar a produção nacional de blindados TAM e munições em Río Tercero.',49,NULL,3),
 ('arg_fadea','ARG','FAdeA e Indústria Aeroespacial','Relançamento da fábrica de Córdoba: manutenção e peças para a frota aérea.',56,'arg_fabricaciones',4),
 ('arg_servicio_ciudadano','ARG','Serviço Cívico Voluntário','Programa de instrução militar voluntária alarga a base de recrutas disponíveis.',35,'arg_cordilheira',5),
 ('arg_gendarmeria_frontera','ARG','Gendarmería nas Fronteiras','Reforço do controlo fronteiriço no Norte Grande com apoio logístico do Exército.',28,'arg_servicio_ciudadano',6),
 ('arg_malvinas_doutrina','ARG','Doutrina das Malvinas','Planeamento operacional dedicado à projeção de força sobre o Atlântico Sul.',42,'arg_atlantico_sul',7),
 ('arg_industria_naval','ARG','Complexo Naval de Río Santiago','Modernização dos estaleiros para manutenção e construção de unidades da Armada.',49,'arg_fabricaciones',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('arg_atlantico_sul','org_regain',1.08),
 ('arg_cordilheira','org_regain',1.06),
 ('arg_fabricaciones','industry',1.10),
 ('arg_fadea','production_speed',1.12),
 ('arg_servicio_ciudadano','conscription',1.20),
 ('arg_gendarmeria_frontera','conscription',1.10),
 ('arg_malvinas_doutrina','research_speed',1.08),
 ('arg_industria_naval','industry',1.08),
 ('arg_industria_naval','production_speed',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('arg_fadea','arg_industria_naval');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('arg_industria_naval','arg_atlantico_sul');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('ARG_adv_pampas','ARG','economia','Barão dos Pampas','🐄',165,'Carne e trigo pagos em moeda estrangeira.'),
 ('ARG_adv_atlantico','ARG','seguranca','Comandante do Atlântico Sul','⚓',170,'Conhece cada baía do sul.');
INSERT INTO advisor_effect VALUES ('ARG_adv_pampas','export_price',1.15);
INSERT INTO advisor_effect VALUES ('ARG_adv_pampas','industry',1.05);
INSERT INTO advisor_effect VALUES ('ARG_adv_atlantico','port_capacity',1.2);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('ARG_atlantico_sul','Questão do Atlântico Sul','🐧',10,'ARG');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('ARG_law_reclamacao','ARG_atlantico_sul','Reclamação diplomática','A questão vive nos foros internacionais e nos manuais da escola.',0,1,'ARG'),
 ('ARG_law_guarnicoes','ARG_atlantico_sul','Guarnições reforçadas','A Patagónia deixa de ser retaguarda e passa a fronteira.',1,0,'ARG'),
 ('ARG_law_projeccao','ARG_atlantico_sul','Projecção sobre as ilhas','Aviação naval e desembarque treinados à vista de todos.',2,0,'ARG');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('ARG_law_reclamacao','research_speed',1.05),
 ('ARG_law_guarnicoes','defense',1.08),
 ('ARG_law_guarnicoes','conscription',1.05),
 ('ARG_law_projeccao','attack',1.1),
 ('ARG_law_projeccao','org_regain',1.05),
 ('ARG_law_projeccao','industry',0.97);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('ARG_gen_montanha','General de Montanha','defense',1.15,130,'ARG','⛰','Formou-se nos Andes, onde o frio mata mais do que o inimigo.'),
 ('ARG_gen_anfibio','Comandante Anfíbio','attack',1.14,145,'ARG','🚤','Treinou o desembarque nas ilhas até saber a praia de cor.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('ARG_escola','Escola dos Andes','⛰',10,'ARG');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ARG_doc_altura','ARG_escola','Escola de Altura','Treina-se onde falta o ar; no plano, o mesmo esforço custa metade.',50,NULL,1,'ARG'),
 ('ARG_doc_cordilheira','ARG_escola','Defesa da Cordilheira','Cada desfiladeiro tem posição estudada há trinta anos.',110,'ARG_doc_altura',2,'ARG'),
 ('ARG_doc_malvinas','ARG_escola','Projecção Ultramarina','Uma força que embarca depressa vale por duas que ficam em terra.',190,'ARG_doc_cordilheira',3,'ARG');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ARG_doc_altura','defense',1.06),
 ('ARG_doc_cordilheira','defense',1.07),
 ('ARG_doc_cordilheira','org_regain',1.05),
 ('ARG_doc_malvinas','move_speed',1.08),
 ('ARG_doc_malvinas','attack',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('ARG_ar','Asas do Sul','🛫',11,'ARG','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ARG_ar_pampa','ARG_ar','Escola do Pampa','Pista de terra, oficina de campanha, e a esquadrilha voa na mesma.',50,NULL,1,'ARG'),
 ('ARG_ar_rasante','ARG_ar','Ataque Rasante','Vinte metros acima da água ninguém tem tempo de apontar.',110,'ARG_ar_pampa',2,'ARG'),
 ('ARG_ar_exocet','ARG_ar','Golpe de Longo Alcance','Um só avião com a arma certa vale por um esquadrão sem ela.',185,'ARG_ar_rasante',3,'ARG');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ARG_ar_pampa','air_upkeep',0.93),
 ('ARG_ar_rasante','air_losses',0.93),
 ('ARG_ar_exocet','air_bombing',1.12);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('ARG_mar','Mar do Sul','🐧',12,'ARG','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('ARG_mar_atlantico_sul','ARG_mar','Patrulha do Atlântico Sul','O mar mais bravo do mundo treina quem lá vive todos os dias.',50,NULL,1,'ARG'),
 ('ARG_mar_submarino_arg','ARG_mar','Escola de Submarinos','Um submarino no mar prende dez navios à procura dele.',110,'ARG_mar_atlantico_sul',2,'ARG'),
 ('ARG_mar_austral','ARG_mar','Navegação Austral','Gelo, vento e cinquenta graus de latitude: o casco tem de aguentar.',185,'ARG_mar_submarino_arg',3,'ARG');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('ARG_mar_atlantico_sul','naval_patrol',1.09),
 ('ARG_mar_submarino_arg','naval_blockade',1.09),
 ('ARG_mar_austral','naval_losses',0.93);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('ARG_ar_gen_austral','Chefe do Grupo Austral','air_losses',0.88,145,'ARG','🦉','Voar no vento do sul ensina-se poucas vezes e aprende-se de uma vez.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('ARG_mar_gen_atlantico_sul','Almirante do Atlântico Sul','naval_blockade',1.16,145,'ARG','🐋','Sabe onde o mar é largo de mais para o inimigo se esconder.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('ARG_tech_ar_pampa','Aviação','Ataque Leve Pampa',260,'air_1','Aviões de fabrico próprio a bater alvos de perto, muitas saídas por dia e peças na porta ao lado.','ARG'),
 ('ARG_tech_mar_atlantico_sul','Marinha','Patrulha do Atlântico Sul',260,'nav_1','Corvetas a cobrir uma costa imensa e vazia: o que passa ao largo passa a ser sabido.','ARG');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('ARG_tech_ar_pampa','air_bombing',1.16),
 ('ARG_tech_mar_atlantico_sul','naval_patrol',1.18);

-- ===== escada de postos nacional (general_rank.country_tag) =====
INSERT INTO general_rank (domain,level,name,xp,bonus,country_tag) VALUES
 ('exercito',1,'Coronel Mayor',0,0,'ARG'),
 ('exercito',2,'General de Brigada',40,0.5,'ARG'),
 ('exercito',3,'General de División',110,1,'ARG'),
 ('exercito',4,'Teniente General',220,1.75,'ARG'),
 ('exercito',5,'Jefe del Estado Mayor del Ejército',360,2.5,'ARG'),
 ('ar',1,'Comodoro',0,0,'ARG'),
 ('ar',2,'Brigadier',40,0.5,'ARG'),
 ('ar',3,'Brigadier Mayor',110,1,'ARG'),
 ('ar',4,'Brigadier General',220,1.75,'ARG'),
 ('ar',5,'Jefe del Estado Mayor de la Fuerza Aérea',360,2.5,'ARG'),
 ('mar',1,'Capitán de Navío',0,0,'ARG'),
 ('mar',2,'Contralmirante de la Armada',40,0.5,'ARG'),
 ('mar',3,'Vicealmirante de la Armada',110,1,'ARG'),
 ('mar',4,'Almirante',220,1.75,'ARG'),
 ('mar',5,'Jefe del Estado Mayor de la Armada',360,2.5,'ARG');
