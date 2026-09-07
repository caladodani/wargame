-- Árvore tecnológica (2030+). cost = dias com research_speed 1. Efeitos de combate = modifier com condition_key
-- 'tech:<id>' (ids 900-999, reservados); efeitos de país = tech_effect (× Country.Stat). Nada disto está em código.
INSERT INTO tech (id,branch,name,cost,requires,description) VALUES
 ('inf_1',   'Infantaria', 'Equipamento de infantaria moderno', 90,  NULL,     'Fuzis, óptica e protecção individual actuais: +5 % força para infantaria.'),
 ('inf_2',   'Infantaria', 'Visão nocturna e comunicações',     120, 'inf_1',  'Combate 24 h e rádio encriptado: +6 % ao atacar com infantaria.'),
 ('inf_3',   'Infantaria', 'Soldado do futuro',                 180, 'inf_2',  'Exosqueleto, drones de esquadra, munição inteligente: +8 % força para infantaria.'),
 ('arm_1',   'Blindados',  'Blindagem reactiva',                100, NULL,     'ERA nos carros e VBCI: +6 % em defesa para blindados.'),
 ('arm_2',   'Blindados',  'Protecção activa (APS)',            150, 'arm_1',  'Intercepta mísseis e RPG antes do impacto: +8 % força para blindados.'),
 ('arm_3',   'Blindados',  'Carro de combate de nova geração',  220, 'arm_2',  'Torre não tripulada, canhão de 130 mm, IA de tiro: +10 % força para blindados.'),
 ('art_1',   'Artilharia', 'Munições guiadas',                  100, NULL,     'Excalibur e afins: +6 % ao atacar com apoio de fogo.'),
 ('art_2',   'Artilharia', 'Contra-bateria por radar',          140, 'art_1',  'Localiza e silencia a artilharia inimiga: +6 % em defesa para unidades de apoio.'),
 ('art_3',   'Artilharia', 'Foguetes de longo alcance',         200, 'art_2',  'HIMARS/PrSM ao nível de brigada: +8 % ao atacar.'),
 ('drones_1','Drones',     'Drones tácticos',                   80,  NULL,     'Reconhecimento e FPV em cada batalhão: +10 % força (já existia na base).'),
 ('drones_2','Drones',     'Enxames de drones',                 160, 'drones_1','Centenas de FPV coordenados: +10 % ao atacar.'),
 ('drones_3','Drones',     'Drones autónomos',                  240, 'drones_2','Sem operador, imunes a jamming: +10 % força.'),
 ('log_1',   'Logística',  'Logística digital',                 90,  NULL,     'Abastecimento por dados em tempo real: organização recupera 10 % mais depressa.'),
 ('log_2',   'Logística',  'Mobilidade estratégica',            140, 'log_1',  'Transportadores e comboios logísticos: divisões movem-se 15 % mais depressa.'),
 ('log_3',   'Logística',  'Cadeia de abastecimento resiliente',200, 'log_2',  'Redundância e stocks avançados: organização recupera mais 10 %.'),
 ('ind_1',   'Indústria',  'Produção em série',                 100, NULL,     'Linhas de montagem de defesa: encomendas 10 % mais rápidas.'),
 ('ind_2',   'Indústria',  'Automação industrial',              160, 'ind_1',  'Robótica e fábricas digitais: rendimento +10 %.'),
 ('ind_3',   'Indústria',  'Base industrial de defesa',         240, 'ind_2',  'Capacidade de guerra prolongada: encomendas mais 15 % rápidas.'),
 ('doc_1',   'Doutrina',   'Armas combinadas',                  110, NULL,     'Infantaria, blindados, artilharia e drones a operar juntos: +5 % comando.'),
 ('doc_2',   'Doutrina',   'Guerra de manobra',                 170, 'doc_1',  'Penetrar e envolver em vez de empurrar: +8 % ao atacar.'),
 ('doc_3',   'Doutrina',   'Defesa em profundidade',            170, 'doc_1',  'Linhas sucessivas e reservas móveis: +8 % em defesa.'),
 ('res_1',   'Ciência',    'Investigação em rede',              120, NULL,     'Universidades e indústria a trabalhar com as Forças Armadas: investigação 15 % mais rápida.'),
 -- Aviação e Marinha: a árvore era toda de terra e de fábrica, e quem comprava esquadrões e navios não
 -- tinha uma única linha de investigação onde os melhorar. Os efeitos são de país (tech_effect) porque é
 -- assim que o AirMissionSystem e o NavalMissionSystem perguntam pelo que a arma vale.
 ('air_1',   'Aviação',    'Caça de superioridade aérea',       110, NULL,     'Caças de nova geração e ligação de dados entre esquadras: perde-se menos gente no céu disputado.'),
 ('air_2',   'Aviação',    'Munições de precisão a distância',  170, 'air_1',  'Bate-se o alvo de fora do alcance da defesa antiaérea: bombardeamento 12 % mais fundo.'),
 ('air_3',   'Aviação',    'Manutenção expedicionária',         230, 'air_2',  'Oficina que vai com as asas para onde elas forem: 10 % menos de sustento por asa destacada.'),
 ('nav_1',   'Marinha',    'Guerra anti-submarina',             110, NULL,     'Sonares rebocados, helicópteros e drones de superfície: afunda-se menos aço nosso em cada combate.'),
 ('nav_2',   'Marinha',    'Mísseis anti-navio de longo alcance',170,'nav_1',  'Fecha-se um mar de muito mais longe: bloqueio 12 % mais apertado.'),
 ('nav_3',   'Marinha',    'Reabastecimento no mar',            230, 'nav_2',  'A esquadra deixa de voltar ao porto para comer: 10 % menos de sustento por navio no mar.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value) VALUES
 (901,'tech','tech:inf_1',   'true','str',         'infantry','add',0.05),
 (902,'tech','tech:inf_2',   'true','str_attacker','infantry','add',0.06),
 (903,'tech','tech:inf_3',   'true','str',         'infantry','add',0.08),
 (904,'tech','tech:arm_1',   'true','str_defender','armored', 'add',0.06),
 (905,'tech','tech:arm_2',   'true','str',         'armored', 'add',0.08),
 (906,'tech','tech:arm_3',   'true','str',         'armored', 'add',0.10),
 (907,'tech','tech:art_1',   'true','str_attacker','support', 'add',0.06),
 (908,'tech','tech:art_2',   'true','str_defender','support', 'add',0.06),
 (909,'tech','tech:art_3',   'true','str_attacker',NULL,      'add',0.08),
 (910,'tech','tech:drones_2','true','str_attacker',NULL,      'add',0.10),
 (911,'tech','tech:drones_3','true','str',         NULL,      'add',0.10),
 (912,'tech','tech:doc_1',   'true','command',     NULL,      'mul',1.05),
 (913,'tech','tech:doc_2',   'true','str_attacker',NULL,      'add',0.08),
 (914,'tech','tech:doc_3',   'true','str_defender',NULL,      'add',0.08);

INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('log_1','org_regain',1.10), ('log_2','move_speed',1.15), ('log_3','org_regain',1.10),
 ('ind_1','production_speed',1.10), ('ind_2','industry',1.10), ('ind_3','production_speed',1.15),
 ('res_1','research_speed',1.15),
 ('air_1','air_losses',0.92), ('air_2','air_bombing',1.12), ('air_3','air_upkeep',0.90),
 ('nav_1','naval_losses',0.92), ('nav_2','naval_blockade',1.12), ('nav_3','naval_upkeep',0.90);

-- Tecnologias iniciais: NATO/aliados avançados começam à frente; o resto começa do zero.
INSERT OR IGNORE INTO country_tech (country_tag,tech_id) VALUES
 ('USA','inf_1'),('USA','inf_2'),('USA','arm_1'),('USA','arm_2'),('USA','art_1'),('USA','drones_1'),('USA','log_1'),('USA','log_2'),('USA','ind_1'),('USA','doc_1'),
 ('GBR','inf_1'),('GBR','arm_1'),('GBR','art_1'),('GBR','drones_1'),('GBR','log_1'),('GBR','doc_1'),
 ('FRA','inf_1'),('FRA','arm_1'),('FRA','art_1'),('FRA','drones_1'),('FRA','log_1'),('FRA','doc_1'),
 ('DEU','inf_1'),('DEU','arm_1'),('DEU','arm_2'),('DEU','art_1'),('DEU','ind_1'),('DEU','doc_1'),
 ('ISR','inf_1'),('ISR','inf_2'),('ISR','arm_1'),('ISR','arm_2'),('ISR','drones_1'),('ISR','drones_2'),('ISR','doc_1'),
 ('KOR','inf_1'),('KOR','arm_1'),('KOR','art_1'),('KOR','ind_1'),('KOR','ind_2'),
 ('JPN','inf_1'),('JPN','arm_1'),('JPN','ind_1'),('JPN','ind_2'),('JPN','res_1'),
 ('CHN','inf_1'),('CHN','arm_1'),('CHN','drones_1'),('CHN','ind_1'),('CHN','ind_2'),
 ('RUS','arm_1'),('RUS','art_1'),('RUS','art_2'),('RUS','drones_1'),('RUS','drones_2'),
 ('UKR','inf_1'),('UKR','drones_1'),('UKR','drones_2'),('UKR','art_1'),('UKR','doc_3'),
 ('TUR','inf_1'),('TUR','drones_1'),('TUR','drones_2'),('TUR','ind_1'),
 ('POL','inf_1'),('POL','arm_1'),('POL','art_1'),('POL','ind_1'),
 ('IND','inf_1'),('IND','arm_1'),('IND','doc_1'),
 ('PRT','inf_1'),('PRT','drones_1'),('PRT','log_1'),('PRT','doc_1'),
 ('ESP','inf_1'),('ESP','arm_1'),('ESP','log_1'),('ESP','doc_1'),
 ('ITA','inf_1'),('ITA','arm_1'),('ITA','art_1'),('ITA','doc_1'),
 ('BRA','inf_1'),('BRA','ind_1'),
 ('AUS','inf_1'),('AUS','arm_1'),('AUS','drones_1'),('AUS','log_1'),
 ('CAN','inf_1'),('CAN','arm_1'),('CAN','log_1'),
 ('IRN','drones_1'),('IRN','drones_2'),('IRN','art_1'),
 ('PRK','art_1'),('PRK','art_2'),
 ('SAU','inf_1'),('SAU','arm_1'),
 ('EGY','inf_1'),('EGY','arm_1'),
 ('PAK','inf_1'),('PAK','drones_1'),
 ('IDN','inf_1'),
 ('ARG','inf_1');

-- Quem já chega a 2030 com força aérea e marinha a sério começa com o primeiro degrau da arma feito: sem
-- isto o mundo abria com toda a gente ao mesmo nível no ar e no mar, que é o contrário do que se vê.
INSERT OR IGNORE INTO country_tech (country_tag,tech_id) VALUES
 ('USA','air_1'),('USA','air_2'),('USA','nav_1'),('USA','nav_2'),
 ('GBR','air_1'),('GBR','nav_1'),('FRA','air_1'),('FRA','nav_1'),
 ('RUS','air_1'),('RUS','nav_1'),('CHN','air_1'),('CHN','nav_1'),
 ('JPN','nav_1'),('KOR','air_1'),('IND','nav_1'),('ISR','air_1'),
 ('TUR','air_1'),('ITA','nav_1'),('ESP','nav_1'),('AUS','nav_1'),
 ('PRT','nav_1'),('CAN','nav_1'),('BRA','nav_1');

-- Programa nuclear (appended): 2 patamares; nuc_2 dá o multiplicador "nuclear" que desbloqueia
-- BuildNukeCommand (Stat("nuclear") > 1). Só investigação — as ogivas compram-se depois.
INSERT INTO tech (id,branch,name,cost,requires,description) VALUES
 ('nuc_1', 'Nuclear', 'Enriquecimento de urânio', 200, 'res_1', 'Centrifugadoras e ciclo de combustível próprio: o caminho para a bomba abre-se.'),
 ('nuc_2', 'Nuclear', 'Arma nuclear',             320, 'nuc_1', 'Ogiva operacional e vector de lançamento: permite construir ogivas (☢).');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('nuc_2', 'nuclear', 2.0);
