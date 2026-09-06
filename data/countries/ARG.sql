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
