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
 (181,'soft_atk',8),(181,'hard_atk',2),(181,'defense',20),(181,'breakthrough',16),(181,'armor',5),(181,'piercing',10),(181,'hardness',0.15),(181,'hp',24),
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
